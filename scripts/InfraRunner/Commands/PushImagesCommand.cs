namespace InfraRunner.Commands;

// Pushes the locally built scale-to-zero-worker:<variant> images (see docs/images.md) to the registry as
// <login server>/scale-to-zero-worker:<variant>-<tag>, which is the image infra/apps.bicep runs, and prints the
// digests.
internal static class PushImagesCommand
{
    private const string DryRunOutputs =
        """
        {
          "properties": {
            "outputs": {
              "registryName":        { "value": "<registry>" },
              "registryLoginServer": { "value": "<registry>.azurecr.io" }
            }
          }
        }
        """;

    public static async Task RunAsync(PushImagesArguments arguments)
    {
        var runner = new ToolRunner(arguments.DryRun);

        await Subscription.EnsureAsync(runner, arguments.SubscriptionId);

        // The registry name has a generated suffix, so it comes from the base deployment's outputs.
        var deployment = await runner.CaptureJsonAsync(
            "az",
            ["deployment", "sub", "show", "--name", InfraNames.BaseDeployment(arguments.NamePrefix), "-o", "json"],
            DryRunOutputs);

        var registryName = ReadOutput(deployment, "registryName");
        var loginServer  = ReadOutput(deployment, "registryLoginServer");

        if (registryName is null || loginServer is null)
        {
            throw new InvalidOperationException(
                $"The deployment {InfraNames.BaseDeployment(arguments.NamePrefix)} has no registry outputs. "
                + "Deploy base first.");
        }

        // Every local image is checked first, so a missing one stops the run before anything is pushed.
        foreach (var variant in arguments.Variants)
        {
            await runner.CaptureAsync(
                "docker",
                ["image", "inspect", "--format", "{{.Id}}", $"{InfraNames.Repository}:{variant}"],
                "");
        }

        await runner.RunAsync("az", ["acr", "login", "--name", registryName]);

        var pushed = new List<(string Variant, string Image, string Digest)>();

        foreach (var variant in arguments.Variants)
        {
            var tag    = InfraNames.ImageTag(variant, arguments.ImageTag);
            var remote = $"{loginServer}/{InfraNames.Repository}:{tag}";

            await runner.CaptureAsync("docker", ["tag", $"{InfraNames.Repository}:{variant}", remote], "");
            await runner.RunAsync("docker", ["push", remote]);

            var repository = await runner.CaptureJsonAsync(
                "az",
                [
                    "acr", "repository", "show",
                    "--name", registryName,
                    "--image", $"{InfraNames.Repository}:{tag}",
                    "-o", "json"
                ],
                """{ "digest": "<digest>" }""");

            pushed.Add((variant, remote, repository.GetProperty("digest").GetString() ?? ""));
        }

        if (arguments.ForcedPulls > 0)
        {
            foreach (var variant in arguments.Variants)
            {
                await PushForcedPullCopiesAsync(runner, arguments, variant, registryName, loginServer, pushed);
            }
        }

        Console.WriteLine();
        var width = pushed.Max(image => image.Image.Length);
        foreach (var (variant, image, digest) in pushed)
        {
            Console.WriteLine($"{variant,-13} {image.PadRight(width)} {digest}");
        }

        if (arguments.ForcedPulls > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Docker keeps the copies' layers in its build cache; docker builder prune frees them.");
        }
    }

    // Builds, pushes and removes each copy in turn, so only one copy is on disk at a time apart from the build cache.
    private static async Task PushForcedPullCopiesAsync(
        ToolRunner runner,
        PushImagesArguments arguments,
        string variant,
        string registryName,
        string loginServer,
        List<(string Variant, string Image, string Digest)> pushed)
    {
        var source = $"{InfraNames.Repository}:{variant}";
        var config = await runner.CaptureJsonAsync(
            "docker",
            ["image", "inspect", "--format", "{{json .Config}}", source],
            """{ "Env": ["PATH=<path>"], "WorkingDir": "/app", "User": "<user>", "Entrypoint": ["<entrypoint>"] }""");
        var dockerfile = ForcedPullImage.Dockerfile(source, config);

        if (runner.DryRun)
        {
            Console.WriteLine(
                $"[dry run] Dockerfile for the {variant} copies, each with a new {ForcedPullImage.NonceFile}:");
            Console.Write(dockerfile);
        }

        for (var copy = 1; copy <= arguments.ForcedPulls; copy++)
        {
            var tag     = InfraNames.ForcedPullTag(variant, arguments.ImageTag, copy);
            var remote  = $"{loginServer}/{InfraNames.Repository}:{tag}";
            var context = Path.Combine(Path.GetTempPath(), $"stz-forced-pull-{Guid.NewGuid():N}");

            try
            {
                if (!runner.DryRun)
                {
                    Directory.CreateDirectory(context);
                    await File.WriteAllTextAsync(Path.Combine(context, "Dockerfile"), dockerfile);
                    await File.WriteAllTextAsync(
                        Path.Combine(context, ForcedPullImage.NonceFile),
                        Guid.NewGuid().ToString());
                }

                await runner.CaptureAsync(
                    "docker",
                    ["build", "--quiet", "--file", Path.Combine(context, "Dockerfile"), "--tag", remote, context],
                    "");
                await runner.RunAsync("docker", ["push", remote]);

                var repository = await runner.CaptureJsonAsync(
                    "az",
                    [
                        "acr", "repository", "show",
                        "--name", registryName,
                        "--image", $"{InfraNames.Repository}:{tag}",
                        "-o", "json"
                    ],
                    """{ "digest": "<digest>" }""");

                pushed.Add((variant, remote, repository.GetProperty("digest").GetString() ?? ""));
                await runner.CaptureAsync("docker", ["image", "rm", remote], "");
            }
            finally
            {
                if (Directory.Exists(context))
                {
                    Directory.Delete(context, recursive: true);
                }
            }
        }
    }

    private static string? ReadOutput(JsonElement deployment, string name) =>
        deployment.TryGetProperty("properties", out var properties)
        && properties.TryGetProperty("outputs", out var outputs)
        && outputs.TryGetProperty(name, out var output)
        && output.TryGetProperty("value", out var value)
            ? value.GetString()
            : null;
}
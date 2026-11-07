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

        Console.WriteLine();
        var width = pushed.Max(image => image.Image.Length);
        foreach (var (variant, image, digest) in pushed)
        {
            Console.WriteLine($"{variant,-13} {image.PadRight(width)} {digest}");
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
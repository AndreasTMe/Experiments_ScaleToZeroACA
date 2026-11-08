namespace Runner.Commands;

// Takes samples for each variant in parallel, one loop per variant, and appends each one to results/<run>/samples.csv
// as it finishes. A cold sample waits until the app has no replicas; a warm one is sent while a replica is running.
// In local mode the workers always run, so there is nothing to wait for and no az command runs. With --forced-pull,
// each cold sample first gives the app its own one-layer copy of the image, so the replica has to pull all of it, and
// the app gets its image back at the end.
internal static class SampleCommand
{
    private sealed record VariantOutcome(string Variant, int Ok, int Failed, string? Error);

    private static readonly TimeSpan ReplicaPollInterval = TimeSpan.FromSeconds(10);

    // Scale-in comes after the cooldown (300 s by default), so this is several cooldowns. Longer means something is
    // keeping the app up, such as min replicas above 0, and the variant's loop stops.
    private static readonly TimeSpan ZeroWaitLimit = TimeSpan.FromMinutes(30);

    // A new revision is usually ready within a minute or two. Longer means it can't start, such as a missing image.
    private static readonly TimeSpan RevisionReadyLimit = TimeSpan.FromMinutes(10);

    private static readonly string SdkVersion =
        typeof(ServiceBusClient).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
        ?? "unknown";

    public static async Task<int> RunAsync(SampleArguments arguments)
    {
        if (arguments.DryRun)
        {
            PrintPlan(arguments);
            return 0;
        }

        var connectionString = arguments.Local
            ? ServiceBusSettings.EmulatorConnectionString
            : Environment.GetEnvironmentVariable(ServiceBusSettings.ConnectionStringVariable);

        if (string.IsNullOrEmpty(connectionString))
        {
            Console.Error.WriteLine(
                $"Set {ServiceBusSettings.ConnectionStringVariable} to a connection string that can send to the "
                + "variant queues and receive from done.");
            return 2;
        }

        using var stopping = new CancellationTokenSource();

        void Stop(object? sender, ConsoleCancelEventArgs args)
        {
            // Finished samples are already in the file. The one in flight is dropped.
            args.Cancel = true;
            stopping.Cancel();
        }

        Console.CancelKeyPress += Stop;

        try
        {
            return await RunSamplesAsync(arguments, connectionString, stopping.Token);
        }
        catch (Exception exception) when (exception is InvalidOperationException or JsonException or IOException)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
        finally
        {
            Console.CancelKeyPress -= Stop;
        }
    }

    private static async Task<int> RunSamplesAsync(
        SampleArguments arguments,
        string connectionString,
        CancellationToken cancellationToken)
    {
        var started = DateTimeOffset.UtcNow;
        var kind    = arguments.Warm ? "warm" : "cold";
        var runName = arguments.RunName ?? $"{started:yyyyMMdd-HHmmss}-{kind}";

        // Local runs go under results/dev, which git ignores.
        var runDirectory = arguments.Local
            ? Path.Combine(RepositoryRoot.Find(), "results", "dev", runName)
            : Path.Combine(RepositoryRoot.Find(), "results", runName);

        if (Directory.Exists(runDirectory))
        {
            Console.Error.WriteLine($"{runDirectory} already exists. Pick another --run name.");
            return 2;
        }

        var apps = arguments.Local
            ? []
            : arguments.Variants.ToDictionary(
                variant => variant,
                variant => new ContainerApp(arguments.AppName(variant), arguments.ResourceGroup));

        // Read before creating the directory, so a run that can't reach Azure leaves nothing behind.
        var config = await DescribeRunAsync(arguments, runName, started, kind, apps, cancellationToken);

        foreach (var snapshot in config.Apps)
        {
            Console.WriteLine(DescribeScale(snapshot));
        }

        // Such an app never reaches zero, so a cold sample would only wait out ZeroWaitLimit.
        if (!arguments.Warm && config.Apps.FirstOrDefault(snapshot => snapshot.MinReplicas() > 0) is { } alwaysOn)
        {
            Console.Error.WriteLine(
                $"{alwaysOn.Name} has min replicas {alwaysOn.MinReplicas()}, so it never scales to zero. Sample it "
                + "with --warm.");
            return 2;
        }

        var minReplicas = config.Apps.ToDictionary(snapshot => snapshot.Variant, snapshot => snapshot.MinReplicas());
        var originalImages = arguments.ForcedPullTag is null
            ? []
            : await ReadOriginalImagesAsync(arguments, apps, cancellationToken);

        Directory.CreateDirectory(runDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(runDirectory, "config.json"),
            JsonSerializer.Serialize(config, ResultsJsonContext.Default.RunConfig),
            cancellationToken);

        var csvPath = Path.Combine(runDirectory, "samples.csv");
        Console.WriteLine($"Run {runName}: {arguments.Samples} {kind} sample(s) per variant, writing {csvPath}");

        await using var client     = new ServiceBusClient(connectionString);
        await using var dispatcher = new DoneDispatcher(client);
        using var       csv        = SamplesCsv.Create(csvPath);

        dispatcher.Start();

        var outcomes = await Task.WhenAll(
            arguments.Variants.Select(variant => RunVariantAsync(
                variant,
                kind,
                apps.GetValueOrDefault(variant),
                minReplicas.GetValueOrDefault(variant),
                originalImages.GetValueOrDefault(variant),
                client,
                dispatcher,
                csv,
                arguments,
                cancellationToken)));

        Console.WriteLine();
        foreach (var outcome in outcomes)
        {
            var note = outcome.Error is null ? "" : $", stopped early: {outcome.Error}";
            Console.WriteLine($"{outcome.Variant,-13} {outcome.Ok} ok, {outcome.Failed} failed{note}");
        }

        Console.WriteLine($"Samples: {csvPath}");

        return outcomes.Any(outcome => outcome.Failed > 0 || outcome.Error is not null) ? 1 : 0;
    }

    private static async Task<VariantOutcome> RunVariantAsync(
        string variant,
        string kind,
        ContainerApp? app,
        int minReplicas,
        string? originalImage,
        ServiceBusClient client,
        DoneDispatcher dispatcher,
        SamplesCsv csv,
        SampleArguments arguments,
        CancellationToken cancellationToken)
    {
        var ok     = 0;
        var failed = 0;

        await using var sender = client.CreateSender(variant);

        try
        {
            for (var sample = 1; sample <= arguments.Samples; sample++)
            {
                DateTimeOffset? zeroObserved = null;

                if (app is not null && originalImage is not null)
                {
                    var image = ForcedPullImage(originalImage, arguments.ForcedPullImageTag(variant, sample));
                    Console.WriteLine($"{variant}: setting the image to {PublicNames.Image(image)}.");
                    await app.SetImageAsync(image, ReplicaPollInterval, RevisionReadyLimit, cancellationToken);
                }

                if (app is not null && !arguments.Warm)
                {
                    zeroObserved = await app.WaitForZeroReplicasAsync(
                        ReplicaPollInterval,
                        ZeroWaitLimit,
                        cancellationToken);
                }
                else if (app is not null && await app.GetReplicaCountAsync(cancellationToken) == 0)
                {
                    if (minReplicas > 0)
                    {
                        await app.WaitForReplicaAsync(ReplicaPollInterval, RevisionReadyLimit, cancellationToken);
                    }
                    else
                    {
                        await WarmUpAsync(variant, sender, dispatcher, arguments.SampleTimeout, cancellationToken);
                    }
                }

                var row = await TakeSampleAsync(
                    variant,
                    sample,
                    kind,
                    zeroObserved,
                    app,
                    sender,
                    dispatcher,
                    arguments.SampleTimeout,
                    cancellationToken);

                csv.Append(row);
                Print(row, arguments.Samples);

                if (row.Status == "ok")
                {
                    ok++;
                }
                else
                {
                    failed++;
                }
            }

            return new VariantOutcome(variant, ok, failed, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new VariantOutcome(variant, ok, failed, "stopped");
        }
        catch (Exception exception) when (exception is TimeoutException
                                              or InvalidOperationException
                                              or JsonException
                                              or ServiceBusException)
        {
            Console.Error.WriteLine($"{variant}: {exception.Message}");
            return new VariantOutcome(variant, ok, failed, exception.Message);
        }
        finally
        {
            if (app is not null && originalImage is not null)
            {
                await RestoreImageAsync(variant, app, originalImage);
            }
        }
    }

    // The image each app runs before the run, checked to be a tagged registry image that isn't a copy, with the
    // first and last copies the run needs already in the registry.
    private static async Task<Dictionary<string, string>> ReadOriginalImagesAsync(
        SampleArguments arguments,
        Dictionary<string, ContainerApp> apps,
        CancellationToken cancellationToken)
    {
        var images = new Dictionary<string, string>();

        foreach (var variant in arguments.Variants)
        {
            var app   = apps[variant];
            var image = await app.GetTemplateImageAsync(cancellationToken);
            var split = ContainerApp.SplitRegistryImage(image);

            if (split is null || !split.Value.RepositoryAndTag.Contains(':'))
            {
                throw new InvalidOperationException(
                    $"{app.Name} runs {PublicNames.Image(image)}, which isn't a tagged image in a registry.");
            }

            if (split.Value.RepositoryAndTag.Contains("-pull-", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"{app.Name} still runs the forced-pull copy {PublicNames.Image(image)}. Deploy the apps again "
                    + "to set its image back first.");
            }

            foreach (var sample in (int[])[1, arguments.Samples])
            {
                var copy = ForcedPullImage(image, arguments.ForcedPullImageTag(variant, sample));

                try
                {
                    await app.GetDigestAsync(copy, cancellationToken);
                }
                catch (InvalidOperationException)
                {
                    throw new InvalidOperationException(
                        $"The registry has no {PublicNames.Image(copy)}. Push the copies first, with push-images "
                        + $"--image-tag {arguments.ForcedPullTag} --forced-pulls {arguments.Samples}.");
                }
            }

            images[variant] = image;
        }

        return images;
    }

    // Runs even when the run is stopped, so it doesn't take the run's cancellation token.
    private static async Task RestoreImageAsync(string variant, ContainerApp app, string originalImage)
    {
        Console.WriteLine($"{variant}: setting the image back to {PublicNames.Image(originalImage)}.");

        try
        {
            await app.SetImageAsync(originalImage, ReplicaPollInterval, RevisionReadyLimit, CancellationToken.None);
        }
        catch (Exception exception) when (exception is InvalidOperationException
                                              or TimeoutException
                                              or JsonException)
        {
            Console.Error.WriteLine(
                $"{variant}: couldn't set the image back ({exception.Message}). Deploy the apps again to restore it.");
        }
    }

    // "registry/repository:tag" with the tag replaced.
    private static string ForcedPullImage(string image, string tag) => $"{image[..image.LastIndexOf(':')]}:{tag}";

    private static async Task<SampleRow> TakeSampleAsync(
        string variant,
        int sample,
        string kind,
        DateTimeOffset? zeroObserved,
        ContainerApp? app,
        ServiceBusSender sender,
        DoneDispatcher dispatcher,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var triggerId = Guid.NewGuid().ToString();
        var waiting   = dispatcher.Expect(triggerId);
        var sent      = DateTimeOffset.UtcNow;

        await sender.SendMessageAsync(
            new ServiceBusMessage
            {
                MessageId = triggerId
            },
            cancellationToken);

        DoneReply reply;

        try
        {
            reply = await waiting.WaitAsync(timeout, cancellationToken);
        }
        catch (TimeoutException)
        {
            dispatcher.Forget(triggerId);
            return new SampleRow(
                variant,
                sample,
                kind,
                "timeout",
                triggerId,
                zeroObserved,
                sent,
                null,
                null,
                null,
                $"No done message within {timeout.TotalSeconds:F0} s.");
        }
        catch (JsonException exception)
        {
            return new SampleRow(
                variant,
                sample,
                kind,
                "invalid-reply",
                triggerId,
                zeroObserved,
                sent,
                null,
                null,
                null,
                exception.Message);
        }

        if (reply.Done.Variant != variant)
        {
            return new SampleRow(
                variant,
                sample,
                kind,
                "cross-talk",
                triggerId,
                zeroObserved,
                sent,
                reply,
                null,
                null,
                $"Answered by {reply.Done.Variant}.");
        }

        string? image  = null;
        string? digest = null;
        string? error  = null;

        if (app is not null && reply.Done.RevisionName is { } revision)
        {
            // A failed lookup costs the sample its image columns, not the sample.
            try
            {
                image  = await app.GetImageAsync(revision, cancellationToken);
                digest = await app.GetDigestAsync(image, cancellationToken);
            }
            catch (Exception exception) when (exception is InvalidOperationException
                                                  or KeyNotFoundException
                                                  or JsonException)
            {
                error = $"Image lookup failed: {exception.Message}";
            }
        }

        var publicImage = image is null ? null : PublicNames.Image(image);

        return new SampleRow(
            variant,
            sample,
            kind,
            "ok",
            triggerId,
            zeroObserved,
            sent,
            reply,
            publicImage,
            digest,
            error);
    }

    // Wakes the app for warm samples. Its done message is awaited but not recorded.
    private static async Task WarmUpAsync(
        string variant,
        ServiceBusSender sender,
        DoneDispatcher dispatcher,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"{variant}: no replica running, sending a warm-up trigger that isn't recorded.");

        var triggerId = Guid.NewGuid().ToString();
        var waiting   = dispatcher.Expect(triggerId);

        await sender.SendMessageAsync(
            new ServiceBusMessage
            {
                MessageId = triggerId
            },
            cancellationToken);

        try
        {
            await waiting.WaitAsync(timeout, cancellationToken);
        }
        catch (TimeoutException)
        {
            dispatcher.Forget(triggerId);
            throw new TimeoutException($"The warm-up trigger got no done message within {timeout.TotalSeconds:F0} s.");
        }
    }

    private static async Task<RunConfig> DescribeRunAsync(
        SampleArguments arguments,
        string runName,
        DateTimeOffset started,
        string kind,
        Dictionary<string, ContainerApp> apps,
        CancellationToken cancellationToken)
    {
        // The subscription id isn't recorded: results go into a public repo.
        JsonElement? azVersion = null;
        var          snapshots = new List<AppSnapshot>();

        if (!arguments.Local)
        {
            azVersion = await AzCli.RunJsonAsync(AzCommands.Version(), cancellationToken);

            foreach (var variant in arguments.Variants)
            {
                snapshots.Add(await apps[variant].GetSnapshotAsync(variant, cancellationToken));
            }
        }

        return new RunConfig(
            runName,
            started,
            arguments.Local ? "local" : "azure",
            kind,
            arguments.Variants,
            arguments.Samples,
            arguments.ForcedPullTag,
            arguments.SampleTimeout.TotalSeconds,
            ReplicaPollInterval.TotalSeconds,
            arguments.Local ? null : arguments.ResourceGroup,
            SdkVersion,
            RuntimeInformation.FrameworkDescription,
            RuntimeInformation.OSDescription,
            azVersion,
            snapshots);
    }

    // Printed at the start of a run, to check against the config the apps were meant to be deployed with.
    private static string DescribeScale(AppSnapshot snapshot)
    {
        static string Value(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "unset";

        return $"{snapshot.Variant,-13} config {snapshot.ScaleConfig ?? "untagged"}, "
               + $"min replicas {snapshot.MinReplicas()}, "
               + $"polling {Value(snapshot.ScaleValue("pollingInterval"))} s, "
               + $"cooldown {Value(snapshot.ScaleValue("cooldownPeriod"))} s";
    }

    private static void PrintPlan(SampleArguments arguments)
    {
        var kind = arguments.Warm ? "warm" : "cold";

        Console.WriteLine(
            $"Dry run: {arguments.Samples} {kind} sample(s) per variant. Nothing is sent and no file is written.");
        Console.WriteLine(
            $"The Service Bus connection string would come from {ServiceBusSettings.ConnectionStringVariable}.");

        Console.WriteLine();
        Console.WriteLine("Once, before the first sample:");
        PrintCommand(AzCommands.Version());
        foreach (var variant in arguments.Variants)
        {
            PrintCommand(AzCommands.AppShow(arguments.AppName(variant), arguments.ResourceGroup));
        }

        if (arguments.ForcedPullTag is { } tag)
        {
            Console.WriteLine();
            Console.WriteLine(
                "Once, before the first sample, each app's image is read and its first and last copies are looked "
                + "up in the registry:");
            foreach (var variant in arguments.Variants)
            {
                PrintCommand(AzCommands.AppShow(arguments.AppName(variant), arguments.ResourceGroup));
                PrintCommand(
                    AzCommands.RepositoryShow(
                        "<registry from the image>",
                        $"<repository>:{arguments.ForcedPullImageTag(variant, 1)}"));
                PrintCommand(
                    AzCommands.RepositoryShow(
                        "<registry from the image>",
                        $"<repository>:{arguments.ForcedPullImageTag(variant, arguments.Samples)}"));
            }

            Console.WriteLine();
            Console.WriteLine(
                $"Before each sample <nn>, the image is set to its copy, then the app is read every "
                + $"{ReplicaPollInterval.TotalSeconds:F0} s until latestReadyRevisionName equals latestRevisionName:");
            foreach (var variant in arguments.Variants)
            {
                PrintCommand(
                    AzCommands.AppUpdateImage(
                        arguments.AppName(variant),
                        arguments.ResourceGroup,
                        $"<registry>/<repository>:{variant}-{tag}-pull-<nn>"));
                PrintCommand(AzCommands.AppShow(arguments.AppName(variant), arguments.ResourceGroup));
            }
        }

        Console.WriteLine();
        Console.WriteLine(
            arguments.Warm
                ? "Before each sample, once. If it shows no replicas, an unrecorded warm-up trigger is sent first, or "
                  + $"for an app with min replicas above 0, it's read every {ReplicaPollInterval.TotalSeconds:F0} s "
                  + "until the platform has started one:"
                : $"Before each sample, every {ReplicaPollInterval.TotalSeconds:F0} s until it shows no replicas "
                  + $"(for up to {ZeroWaitLimit.TotalMinutes:F0} minutes). A run refuses an app with min replicas "
                  + "above 0, which never scales to zero:");
        foreach (var variant in arguments.Variants)
        {
            PrintCommand(AzCommands.ReplicaList(arguments.AppName(variant), arguments.ResourceGroup));
        }

        Console.WriteLine();
        Console.WriteLine(
            $"After each done message (waiting up to {arguments.SampleTimeout.TotalSeconds:F0} s), "
            + "the first time its revision or image is seen:");
        foreach (var variant in arguments.Variants)
        {
            PrintCommand(
                AzCommands.RevisionShow(
                    arguments.AppName(variant),
                    arguments.ResourceGroup,
                    "<revision from the done message>"));
        }

        PrintCommand(AzCommands.RepositoryShow("<registry from the image>", "<repository:tag from the revision>"));

        if (arguments.ForcedPullTag is not null)
        {
            Console.WriteLine();
            Console.WriteLine("After the last sample, or when the run stops, each app gets its image back:");
            foreach (var variant in arguments.Variants)
            {
                PrintCommand(
                    AzCommands.AppUpdateImage(
                        arguments.AppName(variant),
                        arguments.ResourceGroup,
                        "<the image it had before the run>"));
            }
        }
    }

    private static void PrintCommand(string[] arguments) => Console.WriteLine($"  {AzCli.Format(arguments)}");

    private static void Print(SampleRow row, int samples)
    {
        var detail = row.Reply is { } reply
            ? $"{(reply.DoneEnqueued - reply.Done.TriggerEnqueued).TotalMilliseconds,8:F0} ms  "
              + $"{reply.Done.ReplicaName ?? "(no replica name)"}"
            : row.Error;

        Console.WriteLine($"{row.Variant,-13} {row.Sample,3}/{samples} {row.Kind} {row.Status,-10} {detail}");
    }
}
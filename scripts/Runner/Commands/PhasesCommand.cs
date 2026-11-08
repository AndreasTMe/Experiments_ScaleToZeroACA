namespace Runner.Commands;

// Adds each cold sample's start-up phases, read from the system logs in the Log Analytics workspace, to
// results/<run>/samples.csv. A sample without them gets a phases status and the reason. Running it again replaces the
// phase columns, so a run whose events hadn't arrived yet can be filled in later. In a forced-pull run it also adds the
// pull of each sample's activation replica, which is the full one.
internal static class PhasesCommand
{
    private const string LogAnalyticsExtension = "log-analytics";
    private const string WorkspacePlaceholder  = "<workspace-guid>";

    private static readonly string[] PhaseColumns =
    [
        "scaledOut", "replicaAssigned", "pullStarted", "imagePulled", "pullMs", "imageBytes",
        "workerCreated", "workerStarted", "phasesStatus", "phasesNote",
        "activationReplica", "activationPullMs", "activationImageBytes"
    ];

    private static readonly string[] ReadColumns =
        ["variant", "sample", "kind", "status", "triggerSent", "doneEnqueued", "replicaName", "revisionName"];

    // TimeGenerated, which the query filters on, trails the event time, and a replica's events go on until it stops
    // after the cooldown. Both margins are wider than that. In a forced-pull run the first revision's activation
    // replica pulls about six minutes before the first trigger, which the margin before also covers.
    private static readonly TimeSpan WindowBefore = TimeSpan.FromMinutes(20);
    private static readonly TimeSpan WindowAfter  = TimeSpan.FromMinutes(15);

    public static async Task<int> RunAsync(PhasesArguments arguments)
    {
        var csvPath = Path.Combine(RepositoryRoot.Find(), "results", arguments.RunName, "samples.csv");
        if (!File.Exists(csvPath))
        {
            Console.Error.WriteLine($"{csvPath} doesn't exist.");
            return 2;
        }

        try
        {
            return await RunPhasesAsync(arguments, csvPath, CancellationToken.None);
        }
        catch (Exception exception) when (exception is InvalidOperationException or JsonException or IOException)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static async Task<int> RunPhasesAsync(
        PhasesArguments arguments,
        string csvPath,
        CancellationToken cancellationToken)
    {
        var records = Csv.Parse(await File.ReadAllTextAsync(csvPath, cancellationToken));
        if (records.Count < 2)
        {
            Console.Error.WriteLine($"{csvPath} has no samples.");
            return 2;
        }

        // Phase columns from an earlier run of this command are dropped, and written again at the end.
        var header = records[0];
        var kept   = Enumerable.Range(0, header.Length).Where(i => !PhaseColumns.Contains(header[i])).ToArray();
        var rows   = records.Skip(1).ToList();

        if (rows.Any(row => row.Length != header.Length))
        {
            Console.Error.WriteLine($"{csvPath} has a row whose field count doesn't match the header.");
            return 2;
        }

        var column = ReadColumns.ToDictionary(name => name, name => Array.IndexOf(header, name));
        if (column.FirstOrDefault(pair => pair.Value < 0) is { Key: not null } absent)
        {
            Console.Error.WriteLine($"{csvPath} has no {absent.Key} column.");
            return 2;
        }

        var variants = VariantList.Parse(string.Join(',', rows.Select(row => row[column["variant"]]).Distinct()));
        if (variants is null)
        {
            Console.Error.WriteLine($"{csvPath} has a variant that isn't one of jit, r2r, aot and jit-chiseled.");
            return 2;
        }

        var triggers = rows.Select(row => ParseTime(row[column["triggerSent"]])).ToList();
        if (triggers.Any(trigger => trigger is null))
        {
            Console.Error.WriteLine($"{csvPath} has a sample without a triggerSent time.");
            return 2;
        }

        var lastTimes = rows.Select((row, i) => ParseTime(row[column["doneEnqueued"]]) ?? triggers[i]!.Value);
        var from      = triggers.Min()!.Value - WindowBefore;
        var to        = lastTimes.Max() + WindowAfter;
        var query     = SystemLogQuery.Build(variants.Select(arguments.AppName), from, to);

        if (arguments.DryRun)
        {
            PrintPlan(arguments, query);
            return 0;
        }

        var events = arguments.EventsFile is null
            ? await QueryEventsAsync(arguments, query, cancellationToken)
            : await ReadEventsFileAsync(arguments.EventsFile, cancellationToken);

        if (events is null)
        {
            return 2;
        }

        Console.WriteLine($"{events.Count} system log event(s) from {from:u} to {to:u}");

        // Each forced-pull sample runs a revision of its own, so the revision's first replica is its activation one.
        var forcedPull = await ReadForcedPullTagAsync(csvPath, cancellationToken) is not null;

        var phases = rows.Select((row, i) => PhaseMatcher.Match(
                row[column["kind"]],
                row[column["status"]],
                triggers[i]!.Value,
                NullIfEmpty(row[column["replicaName"]]),
                NullIfEmpty(row[column["revisionName"]]),
                events))
            .ToList();

        var activations = rows.Select((row, i) => forcedPull && phases[i].Status is "ok" or "partial"
                ? PhaseMatcher.MatchActivation(
                    triggers[i]!.Value,
                    NullIfEmpty(row[column["replicaName"]]),
                    NullIfEmpty(row[column["revisionName"]]),
                    events)
                : null)
            .ToList();

        var output = new StringBuilder();
        output.Append(Csv.FormatLine(kept.Select(i => header[i]).Concat(PhaseColumns)));
        for (var i = 0; i < rows.Count; i++)
        {
            output.Append(Csv.FormatLine(kept.Select(k => rows[i][k]).Concat(Values(phases[i], activations[i]))));
        }

        // Written whole to a file beside it and moved over it, so a failed write leaves the samples as they were.
        var temporaryPath = csvPath + ".tmp";
        await File.WriteAllTextAsync(
            temporaryPath,
            output.ToString(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            cancellationToken);
        File.Move(temporaryPath, csvPath, overwrite: true);

        for (var i = 0; i < rows.Count; i++)
        {
            Console.WriteLine(
                $"{rows[i][column["variant"]],-13} {rows[i][column["sample"]],3} {phases[i].Status,-14} "
                + Describe(phases[i], activations[i]));
        }

        Console.WriteLine();
        foreach (var group in phases.GroupBy(phase => phase.Status).OrderBy(group => group.Key))
        {
            Console.WriteLine($"{group.Key,-14} {group.Count()}");
        }

        Console.WriteLine($"Samples: {csvPath}");
        return 0;
    }

    // The run's forced-pull tag from config.json beside the samples, or null for a run without one or without the file.
    private static async Task<string?> ReadForcedPullTagAsync(string csvPath, CancellationToken cancellationToken)
    {
        var path = Path.Combine(Path.GetDirectoryName(csvPath)!, "config.json");
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream   = File.OpenRead(path);
        using var       document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        return Property(document.RootElement, "forcedPullTag") is { ValueKind: JsonValueKind.String } tag
            ? tag.GetString()
            : null;
    }

    // Null when the extension or the workspace can't be reached, after saying why.
    private static async Task<List<SystemLogEvent>?> QueryEventsAsync(
        PhasesArguments arguments,
        string query,
        CancellationToken cancellationToken)
    {
        try
        {
            await AzCli.RunAsync(AzCommands.ExtensionShow(LogAnalyticsExtension), cancellationToken);
        }
        catch (InvalidOperationException)
        {
            Console.Error.WriteLine(
                $"The {LogAnalyticsExtension} extension isn't installed. "
                + $"Run: az extension add --name {LogAnalyticsExtension}");
            return null;
        }

        var workspace = await AzCli.RunJsonWithRetriesAsync(
            AzCommands.WorkspaceShow(arguments.ResourceGroup, arguments.WorkspaceName),
            cancellationToken);

        // The GUID isn't printed: the output goes into a public repository.
        if (WorkspaceGuid(workspace) is not { } guid)
        {
            Console.Error.WriteLine($"{arguments.WorkspaceName} has no customerId. Is Log Analytics switched on?");
            return null;
        }

        var rows = await AzCli.RunJsonWithRetriesAsync(AzCommands.LogAnalyticsQuery(guid, query), cancellationToken);
        if (rows.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("The query result isn't a list of rows.");
        }

        return SystemLogEvent.ParseAll(rows);
    }

    // The rows of a query run by hand, saved as the JSON az prints, for checking the matching without Azure.
    private static async Task<List<SystemLogEvent>> ReadEventsFileAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream   = File.OpenRead(path);
        using var       document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return SystemLogEvent.ParseAll(document.RootElement);
    }

    private static void PrintPlan(PhasesArguments arguments, string query)
    {
        Console.WriteLine("Dry run. Nothing is queried and samples.csv isn't changed. A real run would run:");
        Console.WriteLine(AzCli.Format(AzCommands.ExtensionShow(LogAnalyticsExtension)));
        Console.WriteLine(AzCli.Format(AzCommands.WorkspaceShow(arguments.ResourceGroup, arguments.WorkspaceName)));
        Console.WriteLine(AzCli.Format(AzCommands.LogAnalyticsQuery(WorkspacePlaceholder, "<query>")));
        Console.WriteLine($"with <workspace-guid> from customerId, and <query>: {query}");
    }

    private static string? WorkspaceGuid(JsonElement workspace)
    {
        foreach (var scope in new[]
                 {
                     workspace, Property(workspace, "properties")
                 })
        {
            if (Property(scope, "customerId") is { ValueKind: JsonValueKind.String } id
                && id.GetString() is { Length               : > 0 } text)
            {
                return text;
            }
        }

        return null;
    }

    private static JsonElement Property(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) ? value : default;

    private static string?[] Values(SamplePhases phases, ActivationPull? activation) =>
    [
        Time(phases.ScaledOut),
        Time(phases.ReplicaAssigned),
        Time(phases.PullStarted),
        Time(phases.ImagePulled),
        phases.PullMs?.ToString("0.###", CultureInfo.InvariantCulture),
        phases.ImageBytes?.ToString(CultureInfo.InvariantCulture),
        Time(phases.WorkerCreated),
        Time(phases.WorkerStarted),
        phases.Status,
        phases.Note,
        activation?.Replica,
        activation?.PullMs?.ToString("0.###", CultureInfo.InvariantCulture),
        activation?.ImageBytes?.ToString(CultureInfo.InvariantCulture)
    ];

    private static string Describe(SamplePhases phases, ActivationPull? activation)
    {
        var activationPull = activation?.PullMs is { } activationMs
            ? string.Create(CultureInfo.InvariantCulture, $", activation pull {activationMs:0} ms")
            : "";

        if (phases.ReplicaAssigned is not { } assigned || phases.WorkerStarted is not { } started)
        {
            return phases.Note ?? "";
        }

        var culture = CultureInfo.InvariantCulture;
        var pull    = phases.PullMs is { } ms ? string.Create(culture, $", pull {ms:0} ms") : "";
        var details = string.Create(
            culture,
            $"assigned to worker started {(started - assigned).TotalSeconds:0.000} s{pull}{activationPull}");
        return phases.Note is null ? details : $"{details}. {phases.Note}";
    }

    private static DateTimeOffset? ParseTime(string value) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var time)
            ? time
            : null;

    private static string? NullIfEmpty(string value) => value.Length == 0 ? null : value;

    private static string? Time(DateTimeOffset? value) =>
        value?.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
}
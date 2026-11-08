namespace Runner.Analysis;

// Works out a run's summary from its samples.csv and config.json, with the warm run's medians to subtract.
internal static class RunAnalyzer
{
    private const string Cold = "cold";
    private const string Warm = "warm";

    // Each phase ends where the next one starts, so a cold sample's phases add up to its end-to-end time: from the
    // trigger's enqueued time to the done reply's. The ends come from three clocks: Service Bus for the enqueued times,
    // the system logs for scale-out to image pulled, and the worker's container for the rest. A phase that crosses
    // clocks takes their skew with it.
    private static readonly Phase[] ColdPhases =
    [
        new("polling", "triggerEnqueued", "scaledOut"),
        new("scheduling", "scaledOut", "pullStarted"),
        new("imagePull", "pullStarted", "imagePulled"),
        new("containerStart", "imagePulled", "processStarted"),
        new("dotnetStartup", "processStarted", "hostStarted"),
        new("serviceBusConnection", "hostStarted", "messageReceived"),
        new("handling", "messageReceived", "doneEnqueued")
    ];

    // A warm replica started long before the trigger, so only delivery and handling belong to the sample.
    private static readonly Phase[] WarmPhases =
    [
        new("delivery", "triggerEnqueued", "messageReceived"),
        new("handling", "messageReceived", "doneEnqueued")
    ];

    private static readonly string[] PullKindOrder = ["first", "cached", "unknown", "activation"];

    public static RunSummary Analyze(
        string run,
        SampleTable samples,
        JsonElement? config,
        string? warmRun,
        SampleTable? warm)
    {
        var kinds = samples.Rows.Select(row => samples.Text(row, "kind")).Distinct().ToList();
        if (kinds is not [Cold or Warm])
        {
            throw new InvalidDataException($"{run} should have only cold or only warm samples.");
        }

        var kind = kinds[0]!;
        if (kind == Warm && warm is not null)
        {
            throw new InvalidDataException($"{run} is a warm run itself, so there's no warm run to subtract.");
        }

        if (warm is not null && warm.Rows.All(row => warm.Text(row, "kind") != Warm))
        {
            throw new InvalidDataException($"{warmRun} has no warm samples.");
        }

        var groups = samples.Rows
            .GroupBy(row => samples.Text(row, "variant") ?? "")
            .Select(group => Summarize(group.Key, group.ToList(), samples, kind, config, WarmMedian(warm, group.Key)))
            .ToList();

        return new RunSummary(run, kind, warmRun, groups);
    }

    private static GroupSummary Summarize(
        string variant,
        List<string[]> rows,
        SampleTable samples,
        string kind,
        JsonElement? config,
        double? warmMedian)
    {
        var phases   = kind == Cold ? ColdPhases : WarmPhases;
        var ok       = rows.Where(row => samples.Text(row, "status") == "ok").ToList();
        var statuses = rows.GroupBy(row => samples.Text(row, "status") ?? "").ToDictionary(g => g.Key, g => g.Count());
        var endToEnd = Statistics.Summarize(ok.Select(row => samples.Number(row, "endToEndMs")).OfType<double>());

        // One list per phase, with a duration or null for each ok sample.
        var durations = phases.Select(phase => ok.Select(row => Duration(samples, row, phase)).ToList()).ToList();
        var summaries = phases
            .Select((phase, i) => new PhaseSummary(
                phase.Name,
                phase.From,
                phase.To,
                Statistics.Summarize(durations[i].OfType<double>())))
            .ToList();

        var sumErrors = Enumerable.Range(0, ok.Count)
            .Where(i => durations.All(phase => phase[i] is not null) && samples.Number(ok[i], "endToEndMs") is not null)
            .Select(i => Math.Abs(durations.Sum(phase => phase[i]!.Value) - samples.Number(ok[i], "endToEndMs")!.Value))
            .ToList();

        var app = App(config, variant);
        var medianDiff = endToEnd is not null && warmMedian is not null
            ? Statistics.Round(endToEnd.MedianMs - warmMedian.Value)
            : (double?)null;
        var mediansSum = summaries.All(summary => summary.Duration is not null)
            ? Statistics.Round(summaries.Sum(summary => summary.Duration!.MedianMs))
            : (double?)null;

        return new GroupSummary(
            variant,
            ScaleSetting(app, "pollingInterval"),
            ScaleSetting(app, "cooldownPeriod"),
            rows.Count,
            statuses,
            endToEnd,
            warmMedian,
            medianDiff,
            summaries,
            mediansSum,
            sumErrors.Count,
            sumErrors.Count == 0 ? null : Statistics.Round(sumErrors.Max()),
            kind == Cold ? Pulls(samples, rows, ForcedPullTag(config) is not null) : []);
    }

    // A sample whose image or digest differs from the previous sample's pulled something new. The first sample of a run
    // is unknown: the node may still have its image from an earlier run. A forced-pull sample whose activation replica
    // pulled first is cached, and the activation pulls get a group of their own.
    private static List<PullSummary> Pulls(SampleTable samples, List<string[]> rows, bool forcedPull)
    {
        var     kinds    = new List<(string Kind, string[] Row)>();
        string? previous = null;

        foreach (var row in rows.OrderBy(row => samples.Number(row, "sample") ?? 0))
        {
            if (samples.Text(row, "image") is not { } image)
            {
                continue;
            }

            var reference = $"{image}@{samples.Text(row, "imageDigest")}";
            kinds.Add(
                (forcedPull && samples.Number(row, "activationPullMs") is not null
                    ? "cached"
                    : previous is null
                        ? "unknown"
                        : reference == previous
                            ? "cached"
                            : "first", row));
            previous = reference;
        }

        var pulls = kinds
            .Where(pair => samples.Text(pair.Row, "status") == "ok")
            .GroupBy(pair => pair.Kind)
            .OrderBy(group => Array.IndexOf(PullKindOrder, group.Key))
            .Select(group => new PullSummary(
                group.Key,
                Statistics.Summarize(group.Select(pair => samples.Number(pair.Row, "pullMs")).OfType<double>()),
                Statistics.Summarize(group.Select(pair => samples.Number(pair.Row, "endToEndMs")).OfType<double>())))
            .ToList();

        // The end-to-end time a sample would have had with the full pull: its own, with its pull swapped for that one.
        var activations = rows
            .Where(row => samples.Text(row, "status") == "ok")
            .Select(row => (
                Pull: samples.Number(row, "activationPullMs"),
                Own: samples.Number(row, "pullMs"),
                EndToEnd: samples.Number(row, "endToEndMs")))
            .Where(sample => sample is { Pull: not null, Own: not null, EndToEnd: not null })
            .ToList();

        if (forcedPull && activations.Count > 0)
        {
            pulls.Add(
                new PullSummary(
                    "activation",
                    Statistics.Summarize(activations.Select(sample => sample.Pull!.Value)),
                    Statistics.Summarize(
                        activations.Select(sample =>
                            sample.EndToEnd!.Value - sample.Own!.Value + sample.Pull!.Value))));
        }

        return pulls;
    }

    private static string? ForcedPullTag(JsonElement? config) =>
        config is { ValueKind: JsonValueKind.Object } root
        && root.TryGetProperty("forcedPullTag", out var tag)
        && tag.ValueKind == JsonValueKind.String
            ? tag.GetString()
            : null;

    private static double? WarmMedian(SampleTable? warm, string variant)
    {
        if (warm is null)
        {
            return null;
        }

        var values = warm.Rows
            .Where(row => warm.Text(row, "kind") == Warm
                          && warm.Text(row, "status") == "ok"
                          && warm.Text(row, "variant") == variant)
            .Select(row => warm.Number(row, "endToEndMs"))
            .OfType<double>();

        return Statistics.Summarize(values)?.MedianMs;
    }

    private static double? Duration(SampleTable samples, string[] row, Phase phase) =>
        samples.Time(row, phase.From) is { } from && samples.Time(row, phase.To) is { } to
            ? (to - from).TotalMilliseconds
            : null;

    private static JsonElement? App(JsonElement? config, string variant)
    {
        if (config is not { ValueKind: JsonValueKind.Object } root
            || !root.TryGetProperty("apps", out var apps)
            || apps.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var app in apps.EnumerateArray())
        {
            if (app.ValueKind == JsonValueKind.Object
                && app.TryGetProperty("variant", out var name)
                && name.ValueKind == JsonValueKind.String
                && name.GetString() == variant)
            {
                return app;
            }
        }

        return null;
    }

    private static double? ScaleSetting(JsonElement? app, string name) =>
        app is { } element
        && element.TryGetProperty("template", out var template)
        && template.ValueKind == JsonValueKind.Object
        && template.TryGetProperty("scale", out var scale)
        && scale.ValueKind == JsonValueKind.Object
        && scale.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number
            ? value.GetDouble()
            : null;
}
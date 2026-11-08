namespace Runner.Analysis;

// summary.md: the summary as tables, for reading and for pasting into the write-up.
internal static class SummaryMarkdown
{
    private const string Blank = "–";

    public static string Render(RunSummary summary)
    {
        var text = new StringBuilder();

        Line(text, $"# {summary.Run}");
        Line(text);
        Line(
            text,
            $"{summary.Groups.Sum(group => group.Samples)} {summary.Kind} samples. Times are in milliseconds; the "
            + "median and p90 are interpolated linearly between samples.");
        if (summary.WarmRun is not null)
        {
            Line(text, $"The warm medians are from {summary.WarmRun}.");
        }

        EndToEnd(text, summary);
        Failures(text, summary);
        Phases(text, summary);
        TokenPhases(text, summary);
        Pulls(text, summary);

        return text.ToString();
    }

    private static void EndToEnd(StringBuilder text, RunSummary summary)
    {
        var warm = summary.WarmRun is not null;

        Line(text);
        Line(text, "## End-to-end");
        Line(text);
        Row(
            text,
            [
                "Variant", "Polling (s)", "Cooldown (s)", "Samples", "Ok", "Median", "p90", "Max",
                .. warm
                    ? new[]
                    {
                        "Warm median", "Cold − warm median"
                    }
                    : []
            ]);
        Separator(text, warm ? 10 : 8);

        foreach (var group in summary.Groups)
        {
            Row(
                text,
                [
                    group.Variant,
                    Number(group.PollingIntervalSeconds),
                    Number(group.CooldownPeriodSeconds),
                    Number(group.Samples),
                    Number(group.Statuses.GetValueOrDefault("ok")),
                    Number(group.EndToEnd?.MedianMs),
                    Number(group.EndToEnd?.P90Ms),
                    Number(group.EndToEnd?.MaxMs),
                    .. warm
                        ? new[]
                        {
                            Number(group.WarmMedianMs), Number(group.ColdMinusWarmMedianMs)
                        }
                        : []
                ]);
        }
    }

    private static void Failures(StringBuilder text, RunSummary summary)
    {
        var failures = summary.Groups
            .SelectMany(group => group.Statuses
                .Where(status => status.Key != "ok")
                .Select(status => $"- {group.Variant}: {status.Value} {status.Key}"))
            .ToList();

        Line(text);
        Line(text, failures.Count == 0 ? "Every sample is ok." : "Samples that aren't ok:");
        foreach (var failure in failures)
        {
            Line(text, failure);
        }
    }

    private static void Phases(StringBuilder text, RunSummary summary)
    {
        var names = summary.Groups.FirstOrDefault()?.Phases ?? [];

        Line(text);
        Line(text, "## Phase medians");
        Line(text);
        Line(
            text,
            "Each phase ends where the next one starts. Medians don't add up the way samples do, so the sum of the "
            + "medians is close to the median end-to-end time but not equal to it. A complete sample has every phase; "
            + "the sum error is the largest gap between one's phases added up and its end-to-end time.");
        Line(text);
        Row(
            text,
            ["Variant", .. names.Select(phase => phase.Name), "Sum of medians", "Complete samples", "Sum error"]);
        Separator(text, names.Count + 4);

        foreach (var group in summary.Groups)
        {
            Row(
                text,
                [
                    group.Variant,
                    .. group.Phases.Select(phase => Number(phase.Duration?.MedianMs)),
                    Number(group.PhaseMediansSumMs),
                    Number(group.CompleteSamples),
                    Number(group.MaxPhaseSumErrorMs)
                ]);
        }

        Line(text);
        foreach (var phase in names)
        {
            Line(text, $"- {phase.Name}: {phase.From} to {phase.To}");
        }
    }

    private static void TokenPhases(StringBuilder text, RunSummary summary)
    {
        var groups = summary.Groups.Where(group => group.TokenPhases.Count > 0).ToList();
        if (groups.Count == 0)
        {
            return;
        }

        var names = groups[0].TokenPhases;

        Line(text);
        Line(text, "## Token medians");
        Line(text);
        Line(
            text,
            "serviceBusConnection split at the first token, for the variants that sign in with a managed identity. "
            + "The three phases add up to serviceBusConnection.");
        Line(text);
        Row(text, ["Variant", .. names.Select(phase => phase.Name), "Samples"]);
        Separator(text, names.Count + 2);

        foreach (var group in groups)
        {
            Row(
                text,
                [
                    group.Variant,
                    .. group.TokenPhases.Select(phase => Number(phase.Duration?.MedianMs)),
                    Number(group.TokenPhases.Min(phase => phase.Duration?.Count ?? 0))
                ]);
        }

        Line(text);
        foreach (var phase in names)
        {
            Line(text, $"- {phase.Name}: {phase.From} to {phase.To}");
        }
    }

    private static void Pulls(StringBuilder text, RunSummary summary)
    {
        if (summary.Groups.All(group => group.Pulls.Count == 0))
        {
            return;
        }

        Line(text);
        Line(text, "## Image pulls");
        Line(text);
        Line(
            text,
            "A first pull is a sample whose image or digest differs from the previous sample's. The first sample of a "
            + "run is unknown, since the node may have its image from an earlier run. The pull time is the one the "
            + "ImagePulled event reports. In a forced-pull run each sample's revision started a replica while it "
            + "activated, which pulled the copy in full before the sample's replica found it cached; the activation "
            + "row is those full pulls, and its end-to-end median is the samples' with their pull swapped for it.");
        Line(text);
        Row(text, ["Variant", "Pull", "Samples", "Pull median", "Pull p90", "End-to-end median"]);
        Separator(text, 6);

        foreach (var group in summary.Groups)
        {
            foreach (var pull in group.Pulls)
            {
                Row(
                    text,
                    [
                        group.Variant,
                        pull.Kind,
                        Number(pull.EndToEnd?.Count),
                        Number(pull.PullMs?.MedianMs),
                        Number(pull.PullMs?.P90Ms),
                        Number(pull.EndToEnd?.MedianMs)
                    ]);
            }
        }
    }

    private static void Row(StringBuilder text, IEnumerable<string> cells) =>
        Line(text, $"| {string.Join(" | ", cells)} |");

    private static void Separator(StringBuilder text, int columns) =>
        Line(text, $"|{string.Concat(Enumerable.Repeat("---|", columns))}");

    // The repository's Markdown files end their lines in CRLF.
    private static void Line(StringBuilder text, string line = "") => text.Append(line).Append("\r\n");

    private static string Number(double? value) => value?.ToString("0.#", CultureInfo.InvariantCulture) ?? Blank;
}
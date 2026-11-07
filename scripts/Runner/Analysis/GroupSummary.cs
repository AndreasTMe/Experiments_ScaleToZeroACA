namespace Runner.Analysis;

// One variant in one configuration. CompleteSamples have every phase, and MaxPhaseSumErrorMs is the largest gap
// between such a sample's phases added up and its end-to-end time.
internal sealed record GroupSummary(
    string Variant,
    double? PollingIntervalSeconds,
    double? CooldownPeriodSeconds,
    int Samples,
    IReadOnlyDictionary<string, int> Statuses,
    DurationStats? EndToEnd,
    double? WarmMedianMs,
    double? ColdMinusWarmMedianMs,
    IReadOnlyList<PhaseSummary> Phases,
    double? PhaseMediansSumMs,
    int CompleteSamples,
    double? MaxPhaseSumErrorMs,
    IReadOnlyList<PullSummary> Pulls);
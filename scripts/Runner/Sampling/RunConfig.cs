namespace Runner.Sampling;

// config.json: what a run was and what it ran against, written before the first sample.
internal sealed record RunConfig(
    string Run,
    DateTimeOffset Started,
    string Mode,
    string Kind,
    IReadOnlyList<string> Variants,
    int Samples,
    string? ForcedPullTag,
    double SampleTimeoutSeconds,
    double ReplicaPollSeconds,
    string? ResourceGroup,
    string RunnerSdkVersion,
    string RunnerRuntime,
    string RunnerOs,
    JsonElement? AzVersion,
    IReadOnlyList<AppSnapshot> Apps);
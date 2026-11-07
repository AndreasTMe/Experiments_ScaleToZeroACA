namespace Runner.Analysis;

// summary.json. Times are in milliseconds, rounded to 0.1.
internal sealed record RunSummary(string Run, string Kind, string? WarmRun, IReadOnlyList<GroupSummary> Groups);
namespace Runner.Analysis;

// Kind is "first", "cached" or "unknown"; RunAnalyzer says how a sample gets one.
internal sealed record PullSummary(string Kind, DurationStats? PullMs, DurationStats? EndToEnd);
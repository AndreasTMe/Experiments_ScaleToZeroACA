namespace Runner.Analysis;

// Kind is "first", "cached", "unknown" or "activation"; RunAnalyzer says how a sample gets one. An activation group's
// end-to-end times are worked out, with each sample's pull swapped for its activation replica's.
internal sealed record PullSummary(string Kind, DurationStats? PullMs, DurationStats? EndToEnd);
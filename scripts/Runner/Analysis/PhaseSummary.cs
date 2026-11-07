namespace Runner.Analysis;

internal sealed record PhaseSummary(string Name, string From, string To, DurationStats? Duration);
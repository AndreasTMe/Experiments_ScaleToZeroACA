namespace Runner.Analysis;

internal sealed record DurationStats(int Count, double MedianMs, double P90Ms, double MaxMs);
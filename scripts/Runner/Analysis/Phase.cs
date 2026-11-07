namespace Runner.Analysis;

// The span of a sample between two of its timestamp columns in samples.csv.
internal sealed record Phase(string Name, string From, string To);
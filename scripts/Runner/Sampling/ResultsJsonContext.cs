namespace Runner.Sampling;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(RunConfig))]
[JsonSerializable(typeof(RunSummary))]
internal sealed partial class ResultsJsonContext : JsonSerializerContext;
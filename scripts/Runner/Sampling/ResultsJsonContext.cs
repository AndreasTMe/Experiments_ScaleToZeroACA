namespace Runner.Sampling;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(RunConfig))]
internal sealed partial class ResultsJsonContext : JsonSerializerContext;
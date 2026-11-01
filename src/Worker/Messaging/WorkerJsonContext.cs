namespace Worker.Messaging;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(DoneMessage))]
internal sealed partial class WorkerJsonContext : JsonSerializerContext;
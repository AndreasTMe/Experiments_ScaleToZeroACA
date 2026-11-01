namespace Runner.Messaging;

// Strict on purpose: a done message with a missing or extra field fails here rather than giving a wrong timing.
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    RespectRequiredConstructorParameters = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(DoneMessage))]
internal sealed partial class RunnerJsonContext : JsonSerializerContext;
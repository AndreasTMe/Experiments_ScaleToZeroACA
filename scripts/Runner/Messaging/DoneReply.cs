namespace Runner.Messaging;

internal sealed record DoneReply(DateTimeOffset DoneEnqueued, DoneMessage Done);
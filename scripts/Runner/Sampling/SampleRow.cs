namespace Runner.Sampling;

// One line of samples.csv. Status is "ok", "timeout" or "cross-talk"; a sample that isn't ok has no done fields.
internal sealed record SampleRow(
    string Variant,
    int Sample,
    string Kind,
    string Status,
    string TriggerId,
    DateTimeOffset? ZeroObserved,
    DateTimeOffset TriggerSent,
    DoneReply? Reply,
    string? Image,
    string? ImageDigest,
    string? Error);
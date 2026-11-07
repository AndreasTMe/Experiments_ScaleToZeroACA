namespace Runner.Logs;

// The system log phases of one sample. Status is "ok", "partial" (some events missing, named in Note), "missing" (no
// events for the replica yet) or "not-applicable" (warm, or no replica to look up), with the reason in Note.
internal sealed record SamplePhases(
    string Status,
    string? Note,
    DateTimeOffset? ScaledOut,
    DateTimeOffset? ReplicaAssigned,
    DateTimeOffset? PullStarted,
    DateTimeOffset? ImagePulled,
    double? PullMs,
    long? ImageBytes,
    DateTimeOffset? WorkerCreated,
    DateTimeOffset? WorkerStarted)
{
    public static SamplePhases NotAvailable(string status, string note) =>
        new(status, note, null, null, null, null, null, null, null, null);
}
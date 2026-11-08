namespace Runner.Messaging;

internal sealed record DoneMessage(
    string Variant,
    string? ReplicaName,
    string? RevisionName,
    string SdkVersion,
    string RuntimeVersion,
    DateTimeOffset TriggerEnqueued,
    DateTimeOffset ProcessStarted,
    DateTimeOffset MainEntered,
    DateTimeOffset HostStarted,
    DateTimeOffset DoneSenderOpened,
    DateTimeOffset ProcessorStarted,
    DateTimeOffset MessageReceived,
    DateTimeOffset? FirstTokenRequested = null,
    DateTimeOffset? FirstTokenAcquired = null);
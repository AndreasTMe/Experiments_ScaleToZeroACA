namespace Worker.Messaging;

internal sealed record DoneMessage(
    string Variant,
    string? ReplicaName,
    string? RevisionName,
    string SdkVersion,
    string RuntimeVersion,
    DateTimeOffset ProcessStarted,
    DateTimeOffset MainEntered,
    DateTimeOffset HostStarted,
    DateTimeOffset DoneSenderOpened,
    DateTimeOffset ProcessorStarted,
    DateTimeOffset MessageReceived);
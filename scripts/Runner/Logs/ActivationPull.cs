namespace Runner.Logs;

// The image pull of the replica a new revision starts while it activates. In a forced-pull run that replica is the
// first to run the copy, so its pull is the full one and the sample's replica later finds the layers on the node.
internal sealed record ActivationPull(string Replica, double? PullMs, long? ImageBytes);
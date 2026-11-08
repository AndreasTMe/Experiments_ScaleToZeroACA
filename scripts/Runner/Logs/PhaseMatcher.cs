namespace Runner.Logs;

// Finds one sample's phases among the system log events, by the replica name the worker put in its done reply. The
// reasons and their order are in docs/platform-facts.md.
internal static partial class PhaseMatcher
{
    private const string AssigningReplica = "AssigningReplica";
    private const string PullingImage     = "PullingImage";
    private const string ImagePulled      = "ImagePulled";
    private const string ContainerCreated = "ContainerCreated";
    private const string ContainerStarted = "ContainerStarted";

    // The trigger time is the runner's clock and the events are Azure's, so they're compared with some slack.
    private static readonly TimeSpan ClockTolerance = TimeSpan.FromSeconds(2);

    public static SamplePhases Match(
        string kind,
        string status,
        DateTimeOffset triggerSent,
        string? replica,
        string? revision,
        IReadOnlyList<SystemLogEvent> events)
    {
        if (kind != "cold")
        {
            return SamplePhases.NotAvailable(
                "not-applicable",
                "Warm sample: the replica was already running, so it has no start-up events.");
        }

        if (status != "ok" || replica is null)
        {
            return SamplePhases.NotAvailable("not-applicable", "No done reply, so the replica isn't known.");
        }

        var own = events.Where(e => e.Replica == replica).OrderBy(e => e.Time).ToList();
        if (own.Count == 0)
        {
            return SamplePhases.NotAvailable(
                "missing",
                "No system log events for the replica yet. They can take several minutes to arrive; run this again.");
        }

        var assigned = First(own, AssigningReplica, DateTimeOffset.MinValue);
        if (assigned is not null && assigned.Time < triggerSent - ClockTolerance)
        {
            return SamplePhases.NotAvailable(
                "not-applicable",
                "The replica was assigned before the trigger was sent, so it didn't start for this sample.");
        }

        var pulling = First(own, PullingImage, assigned?.Time ?? DateTimeOffset.MinValue);
        var pulled  = First(own, ImagePulled, pulling?.Time ?? DateTimeOffset.MinValue);

        // The created and started events don't name the container. The worker's come after its image pull; the pairs
        // before it are taken to be containers the platform adds.
        var created = pulled is null ? null : First(own, ContainerCreated, pulled.Time);
        var started = pulled is null ? null : First(own, ContainerStarted, pulled.Time);

        // The scale-out event carries the revision but no replica. The first one after the trigger is this sample's.
        var scaledOut = revision is null
            ? null
            : events
                .Where(e => e.Replica is null
                            && e.Revision == revision
                            && e.Message.Contains(" from 0 to ", StringComparison.Ordinal)
                            && e.Time >= triggerSent - ClockTolerance)
                .MinBy(e => e.Time);

        string?[] absent =
        [
            scaledOut is null ? "scale-out" : null,
            assigned is null ? AssigningReplica : null,
            pulling is null ? PullingImage : null,
            pulled is null ? ImagePulled : null,
            created is null ? $"worker {ContainerCreated}" : null,
            started is null ? $"worker {ContainerStarted}" : null
        ];
        var missing = absent.OfType<string>().ToList();

        return new SamplePhases(
            missing.Count == 0 ? "ok" : "partial",
            missing.Count == 0 ? null : $"No event for: {string.Join(", ", missing)}.",
            scaledOut?.Time,
            assigned?.Time,
            pulling?.Time,
            pulled?.Time,
            pulled is null ? null : PullMilliseconds(pulled.Message),
            pulled is null ? null : ImageBytes(pulled.Message),
            created?.Time,
            started?.Time);
    }

    // The revision's first replica, if it was assigned before the trigger and isn't the sample's own. This only holds
    // for a run where every sample has a revision of its own; in others an earlier sample's replica would be taken.
    public static ActivationPull? MatchActivation(
        DateTimeOffset triggerSent,
        string? replica,
        string? revision,
        IReadOnlyList<SystemLogEvent> events)
    {
        if (revision is null)
        {
            return null;
        }

        var first = events
            .Where(e => e.Revision == revision && e.Replica is not null && e.Reason == AssigningReplica)
            .MinBy(e => e.Time);

        if (first is null || first.Replica == replica || first.Time >= triggerSent - ClockTolerance)
        {
            return null;
        }

        var pulled = events
            .Where(e => e.Replica == first.Replica && e.Reason == ImagePulled && e.Time >= first.Time)
            .MinBy(e => e.Time);

        return new ActivationPull(
            first.Replica!,
            pulled is null ? null : PullMilliseconds(pulled.Message),
            pulled is null ? null : ImageBytes(pulled.Message));
    }

    private static SystemLogEvent? First(List<SystemLogEvent> events, string reason, DateTimeOffset notBefore) =>
        events.FirstOrDefault(e => e.Reason == reason && e.Time >= notBefore);

    // "Successfully pulled image "..." in 109ms (109ms including waiting). Image size: 86056295 bytes."
    private static double? PullMilliseconds(string message) =>
        PullDurationPattern().Match(message) is { Success: true } match
        && GoDuration.TryParse(match.Groups["duration"].Value, out var duration)
            ? duration.TotalMilliseconds
            : null;

    private static long? ImageBytes(string message) =>
        ImageSizePattern().Match(message) is { Success: true } match
        && long.TryParse(match.Groups["bytes"].Value, CultureInfo.InvariantCulture, out var bytes)
            ? bytes
            : null;

    [GeneratedRegex(@" in (?<duration>[0-9][^ ]*) \(")]
    private static partial Regex PullDurationPattern();

    [GeneratedRegex(@"Image size: (?<bytes>[0-9]+) bytes")]
    private static partial Regex ImageSizePattern();
}
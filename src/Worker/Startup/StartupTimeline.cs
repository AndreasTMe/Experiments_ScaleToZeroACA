namespace Worker.Startup;

// When each startup phase ended, all UTC. Held in memory and sent with every done message.
internal sealed class StartupTimeline
{
    public required DateTimeOffset ProcessStarted { get; init; }
    public required DateTimeOffset MainEntered    { get; init; }

    public DateTimeOffset HostStarted      { get; set; }
    public DateTimeOffset DoneSenderOpened { get; set; }
    public DateTimeOffset ProcessorStarted { get; set; }

    // Set only by the managed identity build, which asks for a token while it opens the done sender.
    public DateTimeOffset? FirstTokenRequested { get; set; }
    public DateTimeOffset? FirstTokenAcquired  { get; set; }
}
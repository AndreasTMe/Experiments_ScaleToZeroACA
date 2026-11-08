namespace Worker.Identity;

// Passes token requests to the managed identity credential and records when the first one started and ended. The
// Service Bus client asks for a token when it opens its first link, so this is part of opening the done sender.
internal sealed class TimedCredential : TokenCredential
{
    private readonly TokenCredential _inner;
    private readonly StartupTimeline _timeline;

    private int _requested;
    private int _acquired;

    public TimedCredential(TokenCredential inner, StartupTimeline timeline)
    {
        _inner    = inner;
        _timeline = timeline;
    }

    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        MarkRequested();
        var token = _inner.GetToken(requestContext, cancellationToken);
        MarkAcquired();
        return token;
    }

    public override async ValueTask<AccessToken> GetTokenAsync(
        TokenRequestContext requestContext,
        CancellationToken cancellationToken)
    {
        MarkRequested();
        var token = await _inner.GetTokenAsync(requestContext, cancellationToken);
        MarkAcquired();
        return token;
    }

    private void MarkRequested()
    {
        if (Interlocked.Exchange(ref _requested, 1) == 0)
        {
            _timeline.FirstTokenRequested = DateTimeOffset.UtcNow;
        }
    }

    // A failed request throws before this, so the time is that of the first token actually returned.
    private void MarkAcquired()
    {
        if (Interlocked.Exchange(ref _acquired, 1) == 0)
        {
            _timeline.FirstTokenAcquired = DateTimeOffset.UtcNow;
        }
    }
}
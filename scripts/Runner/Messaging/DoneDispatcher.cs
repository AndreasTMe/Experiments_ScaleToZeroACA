namespace Runner.Messaging;

// Receives every done message on one receiver and hands each one to the sample waiting for its trigger, so variants
// sampled in parallel can share the done queue.
internal sealed class DoneDispatcher : IAsyncDisposable
{
    private static readonly TimeSpan ReceiveWait = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ErrorPause  = TimeSpan.FromSeconds(5);

    private readonly ServiceBusReceiver                                            _receiver;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<DoneReply>> _waiting  = new();
    private readonly CancellationTokenSource                                       _stopping = new();
    private          Task?                                                         _receiving;

    public DoneDispatcher(ServiceBusClient client)
    {
        _receiver = client.CreateReceiver(ServiceBusSettings.DoneQueue);
    }

    public void Start() => _receiving = ReceiveAsync(_stopping.Token);

    // Register before sending the trigger, so a fast reply can't arrive before anyone is waiting for it.
    public Task<DoneReply> Expect(string triggerId)
    {
        var waiter = new TaskCompletionSource<DoneReply>(TaskCreationOptions.RunContinuationsAsynchronously);
        _waiting[triggerId] = waiter;
        return waiter.Task;
    }

    // After a timeout. A reply that still arrives is then discarded rather than kept for nobody.
    public void Forget(string triggerId) => _waiting.TryRemove(triggerId, out _);

    public async ValueTask DisposeAsync()
    {
        await _stopping.CancelAsync();

        if (_receiving is not null)
        {
            await _receiving;
        }

        await _receiver.DisposeAsync();
        _stopping.Dispose();
    }

    private async Task ReceiveAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var message = await _receiver.ReceiveMessageAsync(ReceiveWait, cancellationToken);
                if (message is null)
                {
                    continue;
                }

                await _receiver.CompleteMessageAsync(message, CancellationToken.None);
                Dispatch(message);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (ServiceBusException exception) when (!cancellationToken.IsCancellationRequested)
            {
                // The SDK has already retried. Waiting samples time out on their own if this doesn't recover.
                Console.Error.WriteLine($"Receiving done messages failed, retrying: {exception.Message}");
                await Task.Delay(ErrorPause, CancellationToken.None);
            }
        }
    }

    private void Dispatch(ServiceBusReceivedMessage message)
    {
        if (!_waiting.TryRemove(message.CorrelationId, out var waiter))
        {
            // Left over from an earlier run, or from a sample that timed out.
            Console.WriteLine($"Discarded a done message for an unknown or expired trigger: {message.CorrelationId}");
            return;
        }

        try
        {
            var done = message.Body.ToObjectFromJson(RunnerJsonContext.Default.DoneMessage)!;
            waiter.TrySetResult(new DoneReply(message.EnqueuedTime, done));
        }
        catch (JsonException exception)
        {
            waiter.TrySetException(exception);
        }
    }
}
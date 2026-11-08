namespace Worker.Messaging;

// Runs a ServiceBusProcessor on the variant's queue with SDK defaults, and answers each message on the done queue.
internal sealed class QueueProcessor : IHostedService, IAsyncDisposable
{
    private static readonly string SdkVersion =
        typeof(ServiceBusClient).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
        ?? "unknown";

    private readonly WorkerOptions           _options;
    private readonly StartupTimeline         _timeline;
    private readonly ServiceBusProcessor     _processor;
    private readonly ServiceBusSender        _doneSender;
    private readonly ILogger<QueueProcessor> _logger;

    public QueueProcessor(
        ServiceBusClient client,
        WorkerOptions options,
        StartupTimeline timeline,
        ILogger<QueueProcessor> logger)
    {
        _options    = options;
        _timeline   = timeline;
        _processor  = client.CreateProcessor(options.QueueName);
        _doneSender = client.CreateSender(options.DoneQueueName);
        _logger     = logger;

        _processor.ProcessMessageAsync += HandleMessageAsync;
        _processor.ProcessErrorAsync   += HandleErrorAsync;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _timeline.HostStarted = DateTimeOffset.UtcNow;

        // A sender connects on first use, and creating a batch is the first use that sends nothing. This puts the link
        // cost in startup rather than in handling.
        using (await _doneSender.CreateMessageBatchAsync(cancellationToken))
        {
        }

        _timeline.DoneSenderOpened = DateTimeOffset.UtcNow;

        await _processor.StartProcessingAsync(cancellationToken);
        _timeline.ProcessorStarted = DateTimeOffset.UtcNow;

        _logger.LogInformation(
            "Processor started on {Queue}: process {ProcessStarted:O}, main {MainEntered:O}, host {HostStarted:O}, "
            + "done sender {DoneSenderOpened:O}, processor {ProcessorStarted:O}",
            _options.QueueName,
            _timeline.ProcessStarted,
            _timeline.MainEntered,
            _timeline.HostStarted,
            _timeline.DoneSenderOpened,
            _timeline.ProcessorStarted);
    }

    public Task StopAsync(CancellationToken cancellationToken) => _processor.StopProcessingAsync(cancellationToken);

    public async ValueTask DisposeAsync()
    {
        await _processor.DisposeAsync();
        await _doneSender.DisposeAsync();
    }

    private async Task HandleMessageAsync(ProcessMessageEventArgs args)
    {
        var received = DateTimeOffset.UtcNow;

        var done = new DoneMessage(
            _options.Variant,
            _options.ReplicaName,
            _options.RevisionName,
            SdkVersion,
            RuntimeInformation.FrameworkDescription,
            args.Message.EnqueuedTime,
            _timeline.ProcessStarted,
            _timeline.MainEntered,
            _timeline.HostStarted,
            _timeline.DoneSenderOpened,
            _timeline.ProcessorStarted,
            received,
            _timeline.FirstTokenRequested,
            _timeline.FirstTokenAcquired);

        var body = JsonSerializer.SerializeToUtf8Bytes(done, WorkerJsonContext.Default.DoneMessage);

        var reply = new ServiceBusMessage(body)
        {
            CorrelationId = args.Message.MessageId,
            ContentType   = "application/json"
        };

        await _doneSender.SendMessageAsync(reply, args.CancellationToken);

        // The done message can't carry the time it was sent, so this one is only in the log.
        _logger.LogInformation(
            "Handled {MessageId}: received {Received:O}, done sent {DoneSent:O}",
            args.Message.MessageId,
            received,
            DateTimeOffset.UtcNow);
    }

    private Task HandleErrorAsync(ProcessErrorEventArgs args)
    {
        _logger.LogError(args.Exception, "Processor error from {ErrorSource}", args.ErrorSource);
        return Task.CompletedTask;
    }
}
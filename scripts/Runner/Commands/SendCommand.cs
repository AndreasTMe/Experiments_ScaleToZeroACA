namespace Runner.Commands;

// Sends one trigger to each variant's queue at the same time, and waits for the done messages that answer them.
internal static class SendCommand
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    public static async Task<int> RunAsync(SendArguments arguments)
    {
        await using var client   = new ServiceBusClient(ServiceBusSettings.EmulatorConnectionString);
        await using var receiver = client.CreateReceiver(ServiceBusSettings.DoneQueue);

        // Trigger ID to the variant whose queue it was sent to.
        var pending   = arguments.Variants.ToDictionary(_ => Guid.NewGuid().ToString(), variant => variant);
        var crossTalk = false;

        await Task.WhenAll(pending.Select(trigger => SendTriggerAsync(client, trigger.Value, trigger.Key)));

        var deadline = DateTimeOffset.UtcNow + Timeout;

        while (pending.Count > 0 && DateTimeOffset.UtcNow < deadline)
        {
            var message = await receiver.ReceiveMessageAsync(deadline - DateTimeOffset.UtcNow);
            if (message is null)
            {
                break;
            }

            await receiver.CompleteMessageAsync(message);

            if (!pending.Remove(message.CorrelationId, out var variant))
            {
                // Left over from an earlier run that gave up. Completing it keeps it from answering a later one.
                Console.WriteLine($"Discarded a done message for an earlier trigger: {message.CorrelationId}");
                continue;
            }

            var done = message.Body.ToObjectFromJson(RunnerJsonContext.Default.DoneMessage)!;
            Print(message.CorrelationId, message.EnqueuedTime, done);

            if (done.Variant != variant)
            {
                Console.Error.WriteLine($"Cross-talk: the trigger sent to {variant} was answered by {done.Variant}.");
                crossTalk = true;
            }
        }

        foreach (var (triggerId, variant) in pending)
        {
            Console.Error.WriteLine($"No done message from {variant} for {triggerId} within {Timeout.TotalSeconds} s.");
        }

        return pending.Count > 0 || crossTalk ? 1 : 0;
    }

    private static async Task SendTriggerAsync(ServiceBusClient client, string variant, string triggerId)
    {
        await using var sender = client.CreateSender(variant);
        await sender.SendMessageAsync(
            new ServiceBusMessage
            {
                MessageId = triggerId
            });
    }

    private static void Print(string triggerId, DateTimeOffset doneEnqueued, DoneMessage done)
    {
        Console.WriteLine();
        Console.WriteLine($"Trigger           {triggerId}");
        Console.WriteLine($"Variant           {done.Variant}");
        Console.WriteLine($"Replica           {done.ReplicaName ?? "(none)"}");
        Console.WriteLine($"Revision          {done.RevisionName ?? "(none)"}");
        Console.WriteLine($"SDK               {done.SdkVersion}");
        Console.WriteLine($"Runtime           {done.RuntimeVersion}");
        Console.WriteLine($"Trigger enqueued  {done.TriggerEnqueued:O}");
        Console.WriteLine($"Done enqueued     {doneEnqueued:O}");
        Console.WriteLine($"End to end        {(doneEnqueued - done.TriggerEnqueued).TotalMilliseconds:F0} ms");
        Console.WriteLine($"Process started   {done.ProcessStarted:O}");
        Console.WriteLine($"Main entered      {done.MainEntered:O}");
        Console.WriteLine($"Host started      {done.HostStarted:O}");
        Console.WriteLine($"Done sender open  {done.DoneSenderOpened:O}");
        Console.WriteLine($"Processor started {done.ProcessorStarted:O}");
        Console.WriteLine($"Message received  {done.MessageReceived:O}");
    }
}
namespace Runner.Commands;

// Sends one trigger to the variant's queue and waits for the done message that answers it.
internal static class SendCommand
{
    private const string DoneQueue = "done";

    // The emulator's fixed local connection string. SAS_KEY_VALUE is a literal placeholder, not a secret.
    private const string EmulatorConnectionString =
        "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;"
        + "UseDevelopmentEmulator=true;";

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    public static async Task<int> RunAsync(SendArguments arguments)
    {
        await using var client   = new ServiceBusClient(EmulatorConnectionString);
        await using var sender   = client.CreateSender(arguments.Variant);
        await using var receiver = client.CreateReceiver(DoneQueue);

        var triggerId = Guid.NewGuid().ToString();
        await sender.SendMessageAsync(
            new ServiceBusMessage
            {
                MessageId = triggerId
            });

        var deadline = DateTimeOffset.UtcNow + Timeout;

        while (DateTimeOffset.UtcNow < deadline)
        {
            var message = await receiver.ReceiveMessageAsync(deadline - DateTimeOffset.UtcNow);
            if (message is null)
            {
                break;
            }

            await receiver.CompleteMessageAsync(message);

            if (message.CorrelationId == triggerId)
            {
                var done = message.Body.ToObjectFromJson(RunnerJsonContext.Default.DoneMessage)!;
                Print(triggerId, message.EnqueuedTime, done);
                return 0;
            }

            // Left over from an earlier run that gave up. Completing it keeps it from answering a later one.
            Console.WriteLine($"Discarded a done message for an earlier trigger: {message.CorrelationId}");
        }

        Console.Error.WriteLine($"No done message for {triggerId} within {Timeout.TotalSeconds} s.");
        return 1;
    }

    private static void Print(string triggerId, DateTimeOffset doneEnqueued, DoneMessage done)
    {
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
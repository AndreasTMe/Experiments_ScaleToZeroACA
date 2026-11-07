namespace Runner.Sampling;

// Appends rows to samples.csv as each sample finishes. Each row is written whole and flushed, so a run that's stopped
// or killed leaves every finished sample in a valid file.
internal sealed class SamplesCsv : IDisposable
{
    private static readonly string[] Columns =
    [
        "variant", "sample", "kind", "status", "triggerId",
        "zeroObserved", "triggerSent", "triggerEnqueued", "doneEnqueued", "endToEndMs",
        "processStarted", "mainEntered", "hostStarted", "doneSenderOpened", "processorStarted", "messageReceived",
        "replicaName", "revisionName", "image", "imageDigest", "sdkVersion", "runtimeVersion", "error"
    ];

    private readonly StreamWriter _writer;
    private readonly Lock         _lock = new();

    private SamplesCsv(StreamWriter writer)
    {
        _writer = writer;
    }

    public void Append(SampleRow row)
    {
        var line = FormatLine(Values(row));

        lock (_lock)
        {
            _writer.Write(line);
            _writer.Flush();
        }
    }

    public void Dispose() => _writer.Dispose();

    // Fails if the file exists, so a run never mixes its rows into another run's.
    public static SamplesCsv Create(string path)
    {
        var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        writer.Write(FormatLine(Columns));
        writer.Flush();
        return new SamplesCsv(writer);
    }

    private static string?[] Values(SampleRow row)
    {
        var done = row.Reply?.Done;

        return
        [
            row.Variant,
            row.Sample.ToString(CultureInfo.InvariantCulture),
            row.Kind,
            row.Status,
            row.TriggerId,
            Time(row.ZeroObserved),
            Time(row.TriggerSent),
            Time(done?.TriggerEnqueued),
            Time(row.Reply?.DoneEnqueued),
            row.Reply is null
                ? null
                : (row.Reply.DoneEnqueued - row.Reply.Done.TriggerEnqueued).TotalMilliseconds
                .ToString("F1", CultureInfo.InvariantCulture),
            Time(done?.ProcessStarted),
            Time(done?.MainEntered),
            Time(done?.HostStarted),
            Time(done?.DoneSenderOpened),
            Time(done?.ProcessorStarted),
            Time(done?.MessageReceived),
            done?.ReplicaName,
            done?.RevisionName,
            row.Image,
            row.ImageDigest,
            done?.SdkVersion,
            done?.RuntimeVersion,
            row.Error
        ];
    }

    private static string? Time(DateTimeOffset? value) =>
        value?.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

    // RFC 4180: a field with a comma, quote or line break is quoted, with quotes doubled. Lines end in CRLF.
    private static string FormatLine(IEnumerable<string?> values) => string.Join(',', values.Select(Escape)) + "\r\n";

    private static string Escape(string? value) =>
        value is null || value.IndexOfAny([',', '"', '\r', '\n']) < 0
            ? value ?? ""
            : $"\"{value.Replace("\"", "\"\"")}\"";
}
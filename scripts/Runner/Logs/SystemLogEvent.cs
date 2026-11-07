namespace Runner.Logs;

// One row of ContainerAppSystemLogs_CL. Time is the event's own time, from time_s to the nanosecond or, on the many
// rows where that's empty, from time_t to 100 ns. TimeGenerated isn't used: it's the ingestion batch, shared by
// events seconds apart, and puts some of them out of order.
internal sealed record SystemLogEvent(
    DateTimeOffset Time,
    string? App,
    string? Revision,
    string? Replica,
    string? Reason,
    string Message)
{
    // .NET reads at most seven fractional digits, so time_s is cut from nanoseconds to 100 ns.
    private const int MaxFractionDigits = 7;

    // The rows az prints for the query; every column comes back as a string. Rows without a time are skipped.
    public static List<SystemLogEvent> ParseAll(JsonElement rows)
    {
        var events = new List<SystemLogEvent>();

        foreach (var row in rows.EnumerateArray())
        {
            if ((TryParseTime(Column(row, "time_s")) ?? TryParseTime(Column(row, "time_t"))) is not { } time)
            {
                continue;
            }

            events.Add(
                new SystemLogEvent(
                    time,
                    Column(row, "ContainerAppName_s"),
                    Column(row, "RevisionName_s"),
                    Column(row, "ReplicaName_s"),
                    Column(row, "Reason_s"),
                    Column(row, "Log_s") ?? ""));
        }

        return events;
    }

    // "2026-11-07T17:15:12.549689419Z" to 17:15:12.5496894 UTC.
    public static DateTimeOffset ParseTime(string value)
    {
        var dot = value.IndexOf('.');
        if (dot >= 0)
        {
            var end = dot + 1;
            while (end < value.Length && char.IsAsciiDigit(value[end]))
            {
                end++;
            }

            if (end - dot - 1 > MaxFractionDigits)
            {
                value = value[..(dot + 1 + MaxFractionDigits)] + value[end..];
            }
        }

        return DateTimeOffset.Parse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
    }

    // An empty time_t comes back as "None", which isn't a time.
    private static DateTimeOffset? TryParseTime(string? value)
    {
        try
        {
            return value is null ? null : ParseTime(value);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static string? Column(JsonElement row, string name) =>
        row.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
        && value.GetString() is { Length: > 0 } text
            ? text
            : null;
}
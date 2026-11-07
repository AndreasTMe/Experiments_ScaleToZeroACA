namespace Runner.Logs;

internal static class SystemLogQuery
{
    // TimeGenerated bounds the window and the phases use time_s or time_t; the caller leaves a margin, since the first
    // trails the others by a second or so. Only the columns the matcher reads are returned. Strings use single quotes,
    // as a double quote would need escaping on its way through cmd on Windows.
    public static string Build(IEnumerable<string> apps, DateTimeOffset from, DateTimeOffset to)
    {
        var appList = string.Join(", ", apps.Select(app => $"'{app}'"));

        return "ContainerAppSystemLogs_CL"
               + $" | where TimeGenerated between (datetime({Utc(from)}) .. datetime({Utc(to)}))"
               + $" | where ContainerAppName_s in ({appList})"
               + " | project time_s, time_t, ContainerAppName_s, RevisionName_s, ReplicaName_s, Reason_s, Log_s";
    }

    private static string Utc(DateTimeOffset value) =>
        value.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
}
namespace Runner.Analysis;

// samples.csv read by column name. An empty field, or a column the file doesn't have, reads as null.
internal sealed class SampleTable
{
    private static readonly string[] RequiredColumns = ["variant", "sample", "kind", "status", "endToEndMs"];

    private readonly Dictionary<string, int> _columns;

    private SampleTable(string[] header, IReadOnlyList<string[]> rows)
    {
        _columns = header.Select((name, index) => (name, index)).ToDictionary(pair => pair.name, pair => pair.index);
        Rows     = rows;
    }

    public IReadOnlyList<string[]> Rows { get; }

    public string? Text(string[] row, string column) =>
        _columns.TryGetValue(column, out var index) && row[index] is { Length: > 0 } value ? value : null;

    public double? Number(string[] row, string column) =>
        double.TryParse(Text(row, column), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    public DateTimeOffset? Time(string[] row, string column) =>
        DateTimeOffset.TryParse(
            Text(row, column),
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var value)
            ? value
            : null;

    public static SampleTable Parse(string text, string path)
    {
        var records = Csv.Parse(text);
        if (records.Count < 2)
        {
            throw new InvalidDataException($"{path} has no samples.");
        }

        var header = records[0];
        if (records.Skip(1).Any(row => row.Length != header.Length))
        {
            throw new InvalidDataException($"{path} has a row whose field count doesn't match the header.");
        }

        foreach (var column in RequiredColumns)
        {
            if (!header.Contains(column))
            {
                throw new InvalidDataException($"{path} has no {column} column.");
            }
        }

        return new SampleTable(header, records.Skip(1).ToList());
    }
}
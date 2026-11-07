namespace Runner.Sampling;

// RFC 4180: a field with a comma, quote or line break is quoted, with quotes doubled. Lines end in CRLF.
internal static class Csv
{
    public static string FormatLine(IEnumerable<string?> values) => string.Join(',', values.Select(Escape)) + "\r\n";

    // Every record as its fields, the header included. An empty field reads as an empty string.
    public static List<string[]> Parse(string text)
    {
        var records = new List<string[]>();
        var fields  = new List<string>();
        var field   = new StringBuilder();
        var quoted  = false;
        var index   = 0;

        while (index < text.Length)
        {
            var c = text[index++];

            if (quoted)
            {
                if (c != '"')
                {
                    field.Append(c);
                }
                else if (index < text.Length && text[index] == '"')
                {
                    field.Append('"');
                    index++;
                }
                else
                {
                    quoted = false;
                }

                continue;
            }

            switch (c)
            {
                case '"':
                    quoted = true;
                    break;
                case ',':
                    fields.Add(field.ToString());
                    field.Clear();
                    break;
                case '\r':
                    break;
                case '\n':
                    fields.Add(field.ToString());
                    field.Clear();
                    records.Add([.. fields]);
                    fields.Clear();
                    break;
                default:
                    field.Append(c);
                    break;
            }
        }

        if (field.Length > 0 || fields.Count > 0)
        {
            fields.Add(field.ToString());
            records.Add([.. fields]);
        }

        return records;
    }

    private static string Escape(string? value) =>
        value is null || value.IndexOfAny([',', '"', '\r', '\n']) < 0
            ? value ?? ""
            : $"\"{value.Replace("\"", "\"\"")}\"";
}
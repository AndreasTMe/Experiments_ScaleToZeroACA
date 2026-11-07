namespace InfraRunner.Commands;

// Reads "--name value" options and the switches below, each given at most once.
internal sealed class OptionReader
{
    private static readonly string[] Switches = ["--dry-run", "--preview"];

    private readonly Dictionary<string, string> _values  = new(StringComparer.Ordinal);
    private readonly HashSet<string>            _present = new(StringComparer.Ordinal);
    private readonly HashSet<string>            _read    = new(StringComparer.Ordinal);

    public OptionReader(IReadOnlyList<string> arguments)
    {
        for (var index = 0; index < arguments.Count; index++)
        {
            var option = arguments[index];

            if (!option.StartsWith("--", StringComparison.Ordinal))
            {
                throw new UsageException($"Expected an option, got {option}.");
            }

            if (!_present.Add(option))
            {
                throw new UsageException($"{option} is given twice.");
            }

            if (Switches.Contains(option))
            {
                continue;
            }

            if (index + 1 >= arguments.Count || arguments[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                throw new UsageException($"{option} needs a value.");
            }

            _values[option] = arguments[++index];
        }
    }

    public bool Switch(string name)
    {
        _read.Add(name);
        return _present.Contains(name);
    }

    public string? Optional(string name)
    {
        _read.Add(name);
        return _values.GetValueOrDefault(name);
    }

    public string Required(string name) => Optional(name) ?? throw new UsageException($"{name} is required.");

    public int? OptionalInt(string name, int minimum)
    {
        if (Optional(name) is not { } text)
        {
            return null;
        }

        if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value < minimum)
        {
            throw new UsageException($"{name} takes a whole number from {minimum}.");
        }

        return value;
    }

    // Called once every option the command knows has been read, so a misspelt or misplaced one isn't ignored.
    public void RejectUnread(string command)
    {
        if (_present.FirstOrDefault(option => !_read.Contains(option)) is { } unread)
        {
            throw new UsageException($"{unread} isn't an option of {command}.");
        }
    }
}
namespace Runner.Commands;

internal sealed record PhasesArguments(
    string RunName,
    string ResourceGroup,
    string NamePrefix,
    string? EventsFile,
    bool DryRun)
{
    public const string Usage =
        "phases --run <name> [--dry-run] [--resource-group <name>] [--name-prefix <prefix>] [--events-file <path>]";

    // Matches infra/main.bicep and infra/apps.bicep with their defaults.
    private const string DefaultResourceGroup = "rg-stz-uksouth";
    private const string DefaultNamePrefix    = "stz";

    // Matches names.bicep.
    public string WorkspaceName => $"log-{NamePrefix}";

    // Matches apps.bicep.
    public string AppName(string variant) => $"ca-{NamePrefix}-{variant}";

    // Returns null, with the reason in error, unless the arguments are a valid phases command.
    public static PhasesArguments? Parse(string[] args, out string? error)
    {
        error = null;

        if (args is not ["phases", ..])
        {
            error = "Not a phases command.";
            return null;
        }

        string? runName       = null;
        string? eventsFile    = null;
        var     dryRun        = false;
        var     resourceGroup = DefaultResourceGroup;
        var     namePrefix    = DefaultNamePrefix;

        for (var index = 1; index < args.Length; index++)
        {
            var option = args[index];

            if (option == "--dry-run")
            {
                dryRun = true;
                continue;
            }

            if (index + 1 >= args.Length)
            {
                error = $"{option} needs a value.";
                return null;
            }

            var value = args[++index];

            switch (option)
            {
                case "--run":
                    runName = value;
                    break;
                case "--resource-group":
                    resourceGroup = value;
                    break;
                case "--name-prefix":
                    namePrefix = value;
                    break;
                case "--events-file":
                    eventsFile = value;
                    break;
                default:
                    error = $"Unknown option {option}.";
                    return null;
            }
        }

        if (runName is null || !IsSafeName(runName))
        {
            error = "--run is required, and takes letters, digits, '.', '_' and '-' only.";
            return null;
        }

        // The prefix goes into the query's app names, so it's held to what the Bicep allows.
        if (namePrefix.Length is < 2 or > 8 || !namePrefix.All(char.IsAsciiLetterOrDigit))
        {
            error = "--name-prefix takes 2 to 8 letters and digits.";
            return null;
        }

        if (dryRun && eventsFile is not null)
        {
            error = "--dry-run shows the az commands, and --events-file runs none.";
            return null;
        }

        return new PhasesArguments(runName, resourceGroup, namePrefix, eventsFile, dryRun);
    }

    private static bool IsSafeName(string value) =>
        value.Length > 0 && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-');
}
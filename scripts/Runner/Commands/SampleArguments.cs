namespace Runner.Commands;

internal sealed record SampleArguments(
    IReadOnlyList<string> Variants,
    int Samples,
    bool Warm,
    bool Local,
    bool DryRun,
    string? RunName,
    string ResourceGroup,
    string NamePrefix,
    TimeSpan SampleTimeout,
    string? ForcedPullTag,
    bool RandomDelay)
{
    public const string Usage =
        "sample --variant <names> --samples <n> [--warm] [--local] [--dry-run] [--run <name>]\n"
        + "       [--resource-group <name>] [--name-prefix <prefix>] [--timeout <seconds>] [--forced-pull <tag>]\n"
        + "       [--random-delay]";

    // Matches infra/main.bicep and infra/apps.bicep with their defaults.
    private const string DefaultResourceGroup = "rg-stz-uksouth";
    private const string DefaultNamePrefix    = "stz";

    // Matches InfraRunner's push-images --forced-pulls, which numbers the copies with two digits.
    private const int MaxForcedPullSamples = 99;

    private static readonly TimeSpan DefaultSampleTimeout = TimeSpan.FromMinutes(10);

    // Matches apps.bicep: ca-<prefix>-<variant>.
    public string AppName(string variant) => $"ca-{NamePrefix}-{variant}";

    // Matches InfraRunner's InfraNames.ForcedPullTag: the copy of <variant>-<tag> that sample n runs.
    public string ForcedPullImageTag(string variant, int sample) => $"{variant}-{ForcedPullTag}-pull-{sample:D2}";

    // Returns null, with the reason in error, unless the arguments are a valid sample command.
    public static SampleArguments? Parse(string[] args, out string? error)
    {
        error = null;

        if (args is not ["sample", ..])
        {
            error = "Not a sample command.";
            return null;
        }

        IReadOnlyList<string>? variants      = null;
        int?                   samples       = null;
        string?                runName       = null;
        string?                forcedPull    = null;
        var                    warm          = false;
        var                    local         = false;
        var                    dryRun        = false;
        var                    randomDelay   = false;
        var                    resourceGroup = DefaultResourceGroup;
        var                    namePrefix    = DefaultNamePrefix;
        var                    sampleTimeout = DefaultSampleTimeout;

        for (var index = 1; index < args.Length; index++)
        {
            var option = args[index];

            switch (option)
            {
                case "--warm":
                    warm = true;
                    continue;
                case "--local":
                    local = true;
                    continue;
                case "--dry-run":
                    dryRun = true;
                    continue;
                case "--random-delay":
                    randomDelay = true;
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
                case "--variant":
                    variants = VariantList.Parse(value);
                    if (variants is null)
                    {
                        error = "--variant takes distinct names, comma-separated, from jit, r2r, aot, jit-chiseled "
                                + "and jit-identity.";
                        return null;
                    }

                    break;
                case "--samples":
                    if (!int.TryParse(value, CultureInfo.InvariantCulture, out var count) || count < 1)
                    {
                        error = "--samples takes a whole number from 1.";
                        return null;
                    }

                    samples = count;
                    break;
                case "--run":
                    if (!IsSafeName(value))
                    {
                        error = "--run takes letters, digits, '.', '_' and '-' only.";
                        return null;
                    }

                    runName = value;
                    break;
                case "--forced-pull":
                    if (!IsSafeName(value) || !char.IsAsciiLetterOrDigit(value[0]))
                    {
                        error = "--forced-pull takes the image tag push-images was given, such as v1.";
                        return null;
                    }

                    forcedPull = value;
                    break;
                case "--resource-group":
                    resourceGroup = value;
                    break;
                case "--name-prefix":
                    namePrefix = value;
                    break;
                case "--timeout":
                    if (!int.TryParse(value, CultureInfo.InvariantCulture, out var seconds) || seconds < 1)
                    {
                        error = "--timeout takes a whole number of seconds from 1.";
                        return null;
                    }

                    sampleTimeout = TimeSpan.FromSeconds(seconds);
                    break;
                default:
                    error = $"Unknown option {option}.";
                    return null;
            }
        }

        if (variants is null || samples is null)
        {
            error = "--variant and --samples are required.";
            return null;
        }

        if (local && dryRun)
        {
            error = "--dry-run shows the az commands, and --local runs none.";
            return null;
        }

        if (forcedPull is not null && local)
        {
            error = "--forced-pull changes the image of the apps in Azure, and --local runs against the emulator.";
            return null;
        }

        if (forcedPull is not null && warm)
        {
            error = "--forced-pull is for cold samples: a warm sample runs on a replica that has already pulled.";
            return null;
        }

        if (forcedPull is not null && samples > MaxForcedPullSamples)
        {
            error = $"--forced-pull takes up to {MaxForcedPullSamples} samples, one image copy each.";
            return null;
        }

        if (randomDelay && (warm || local))
        {
            error = "--random-delay is for cold samples in Azure: it spreads each trigger across the app's KEDA "
                    + "polling interval after scale-in.";
            return null;
        }

        return new SampleArguments(
            variants,
            samples.Value,
            warm,
            local,
            dryRun,
            runName,
            resourceGroup,
            namePrefix,
            sampleTimeout,
            forcedPull,
            randomDelay);
    }

    private static bool IsSafeName(string value) =>
        value.Length > 0 && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-');
}
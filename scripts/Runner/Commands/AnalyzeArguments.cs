namespace Runner.Commands;

internal sealed record AnalyzeArguments(string RunFolder, string? WarmFolder)
{
    public const string Usage = "analyze <run> [--warm <run>]";

    // Returns null, with the reason in error, unless the arguments are a valid analyze command.
    public static AnalyzeArguments? Parse(string[] args, out string? error)
    {
        error = null;

        if (args is not ["analyze", var run, .. var options] || run.StartsWith("--", StringComparison.Ordinal))
        {
            error = "analyze needs a run: a folder such as results/<run>, or the name of one in results.";
            return null;
        }

        string? warm = null;

        for (var index = 0; index < options.Length; index++)
        {
            if (options[index] != "--warm" || index + 1 >= options.Length)
            {
                error = options[index] == "--warm" ? "--warm needs a value." : $"Unknown option {options[index]}.";
                return null;
            }

            warm = options[++index];
        }

        return new AnalyzeArguments(Folder(run), warm is null ? null : Folder(warm));
    }

    // A folder that exists is taken as it is; anything else is a run name in results.
    private static string Folder(string value) =>
        Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(Directory.Exists(value) ? value : Path.Combine(RepositoryRoot.Find(), "results", value)));
}
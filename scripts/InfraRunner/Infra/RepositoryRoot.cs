namespace InfraRunner.Infra;

internal static class RepositoryRoot
{
    private const string SolutionFile = "ScaleToZero.slnx";

    // Looks up from the build output, so the parameter files are found whichever folder the tool starts from.
    public static string Find()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, SolutionFile)))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException($"No {SolutionFile} above {AppContext.BaseDirectory}.");
    }

    public static string PathTo(string relativePath) => Path.Combine(Find(), relativePath);
}
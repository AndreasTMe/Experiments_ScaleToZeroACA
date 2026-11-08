namespace InfraRunner.Infra;

// The named scale settings for the apps, one parameter file each in infra/levers, named after the config it sets.
internal static class LeverFiles
{
    private const string Extension = ".bicepparam";

    public static string PathTo(string config) => Path.Combine(Folder(), config + Extension);

    public static IReadOnlyList<string> Names() =>
        Directory.Exists(Folder())
            ? Directory.GetFiles(Folder(), "*" + Extension)
                .Select(path => Path.GetFileNameWithoutExtension(path))
                .Order(StringComparer.Ordinal)
                .ToList()
            : [];

    private static string Folder() => RepositoryRoot.PathTo(Path.Combine("infra", "levers"));
}
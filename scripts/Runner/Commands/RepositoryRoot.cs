namespace Runner.Commands;

internal static class RepositoryRoot
{
    // The folder with ScaleToZero.slnx, so results land in the same place whichever folder the runner starts from.
    public static string Find()
    {
        for (var directory = new DirectoryInfo(Environment.CurrentDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ScaleToZero.slnx")))
            {
                return directory.FullName;
            }
        }

        return Environment.CurrentDirectory;
    }
}
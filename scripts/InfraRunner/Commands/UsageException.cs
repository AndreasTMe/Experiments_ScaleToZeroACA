namespace InfraRunner.Commands;

// The command line is wrong. Nothing has run yet.
internal sealed class UsageException : Exception
{
    public UsageException(string message)
        : base(message)
    {
    }
}
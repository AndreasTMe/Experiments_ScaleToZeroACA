namespace Runner.ContainerApps;

// Runs az and returns what it printed. Every command asks for JSON and the runner reads it, rather than passing
// JMESPath queries: on Windows az is a batch file run through cmd, which would reinterpret characters in a query.
internal static class AzCli
{
    // About two and a half minutes in all: enough to ride out a brief failure of the Azure API, such as a 500, which
    // would otherwise stop a run that takes hours.
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(40), TimeSpan.FromSeconds(80)
    ];

    public static string Format(IReadOnlyList<string> arguments) => "az " + string.Join(' ', arguments);

    public static async Task<JsonElement> RunJsonAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var       output   = await RunAsync(arguments, cancellationToken);
        using var document = JsonDocument.Parse(output);
        return document.RootElement.Clone();
    }

    // For commands that are harmless to run again: reads, and setting an image the app may already have. A failure is
    // retried after each delay in turn, and thrown once they've run out.
    public static async Task<JsonElement> RunJsonWithRetriesAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0;; attempt++)
        {
            try
            {
                return await RunJsonAsync(arguments, cancellationToken);
            }
            catch (InvalidOperationException exception) when (attempt < RetryDelays.Length)
            {
                var delay = RetryDelays[attempt];
                Console.Error.WriteLine(
                    $"{exception.Message} Retrying in {delay.TotalSeconds:F0} s "
                    + $"({attempt + 1} of {RetryDelays.Length} retries).");
                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    public static async Task<string> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo
        {
            FileName               = OperatingSystem.IsWindows() ? "cmd.exe" : "az",
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false
        };

        if (OperatingSystem.IsWindows())
        {
            start.ArgumentList.Add("/d");
            start.ArgumentList.Add("/c");
            start.ArgumentList.Add("az");
        }

        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)
                            ?? throw new InvalidOperationException($"Could not start {Format(arguments)}");

        var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error  = process.StandardError.ReadToEndAsync(cancellationToken);

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"{Format(arguments)} failed with exit code {process.ExitCode}: {(await error).Trim()}");
        }

        return await output;
    }
}
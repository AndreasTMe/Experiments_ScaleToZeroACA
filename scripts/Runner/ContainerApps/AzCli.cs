namespace Runner.ContainerApps;

// Runs az and returns what it printed. Every command asks for JSON and the runner reads it, rather than passing
// JMESPath queries: on Windows az is a batch file run through cmd, which would reinterpret characters in a query.
internal static class AzCli
{
    public static string Format(IReadOnlyList<string> arguments) => "az " + string.Join(' ', arguments);

    public static async Task<JsonElement> RunJsonAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var       output   = await RunAsync(arguments, cancellationToken);
        using var document = JsonDocument.Parse(output);
        return document.RootElement.Clone();
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
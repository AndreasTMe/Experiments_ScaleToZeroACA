namespace InfraRunner.Tools;

// Runs az and docker, printing each command line first. In a dry run it only prints, and hands back a stand-in for
// what the command would have printed, so the commands that depend on it can be shown too.
internal sealed class ToolRunner
{
    // On Windows az is a batch file run through cmd, which would act on these characters inside an argument.
    private static readonly char[] CmdSpecialCharacters = ['&', '|', '<', '>', '^', '%', '!', '"'];

    public ToolRunner(bool dryRun)
    {
        DryRun = dryRun;
    }

    public bool DryRun { get; }

    // The command's output goes straight to the console.
    public async Task RunAsync(string tool, IReadOnlyList<string> arguments)
    {
        if (Announce(tool, arguments))
        {
            return;
        }

        using var process = Start(tool, arguments, captureOutput: false);
        await process.WaitForExitAsync();
        ThrowOnFailure(process, tool, arguments);
    }

    // Errors and warnings still go to the console; only the standard output is returned.
    public async Task<string> CaptureAsync(string tool, IReadOnlyList<string> arguments, string dryRunOutput)
    {
        if (Announce(tool, arguments))
        {
            return dryRunOutput;
        }

        using var process = Start(tool, arguments, captureOutput: true);
        var       output  = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        ThrowOnFailure(process, tool, arguments);

        return output;
    }

    public async Task<JsonElement> CaptureJsonAsync(string tool, IReadOnlyList<string> arguments, string dryRunJson)
    {
        var       output   = await CaptureAsync(tool, arguments, dryRunJson);
        using var document = JsonDocument.Parse(output);

        return document.RootElement.Clone();
    }

    // Prints the command line. Returns true in a dry run, where the command isn't run.
    private bool Announce(string tool, IReadOnlyList<string> arguments)
    {
        Console.WriteLine($"{(DryRun ? "[dry run]" : ">")} {Format(tool, arguments)}");
        return DryRun;
    }

    public static string Format(string tool, IReadOnlyList<string> arguments) =>
        string.Join(' ', arguments.Select(Quote).Prepend(tool));

    private static string Quote(string argument) =>
        argument.Length > 0 && !argument.Any(c => char.IsWhiteSpace(c) || c is '"' or '\'')
            ? argument
            : $"'{argument.Replace("'", "''", StringComparison.Ordinal)}'";

    private static Process Start(string tool, IReadOnlyList<string> arguments, bool captureOutput)
    {
        var throughCmd = tool == "az" && OperatingSystem.IsWindows();

        if (throughCmd
            && arguments.FirstOrDefault(argument => argument.IndexOfAny(CmdSpecialCharacters) >= 0) is
                { } rejected)
        {
            throw new InvalidOperationException($"Not passing {rejected} to az through cmd.");
        }

        var start = new ProcessStartInfo
        {
            FileName               = throughCmd ? "cmd.exe" : tool,
            RedirectStandardOutput = captureOutput,
            UseShellExecute        = false
        };

        if (throughCmd)
        {
            start.ArgumentList.Add("/d");
            start.ArgumentList.Add("/c");
            start.ArgumentList.Add("az");
        }

        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        return Process.Start(start) ?? throw new InvalidOperationException($"Could not start {tool}.");
    }

    private static void ThrowOnFailure(Process process, string tool, IReadOnlyList<string> arguments)
    {
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"{Format(tool, arguments)} failed with exit code {process.ExitCode}.");
        }
    }
}
namespace Runner.Commands;

// Summarizes a run's samples.csv into summary.json and summary.md beside it: end-to-end statistics, phase medians and
// image pulls per variant, with a warm run's medians subtracted when one is given. Nothing is queried.
internal static class AnalyzeCommand
{
    private const string SamplesFile = "samples.csv";
    private const string ConfigFile  = "config.json";

    public static async Task<int> RunAsync(AnalyzeArguments arguments)
    {
        try
        {
            return await AnalyzeAsync(arguments, CancellationToken.None);
        }
        catch (Exception exception) when (exception is InvalidDataException or JsonException or IOException)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static async Task<int> AnalyzeAsync(AnalyzeArguments arguments, CancellationToken cancellationToken)
    {
        string?[] folders = [arguments.RunFolder, arguments.WarmFolder];
        foreach (var folder in folders.OfType<string>())
        {
            if (!File.Exists(Path.Combine(folder, SamplesFile)))
            {
                Console.Error.WriteLine($"{Path.Combine(folder, SamplesFile)} doesn't exist.");
                return 2;
            }
        }

        var samples = await ReadSamplesAsync(arguments.RunFolder, cancellationToken);
        var warm = arguments.WarmFolder is null
            ? null
            : await ReadSamplesAsync(arguments.WarmFolder, cancellationToken);
        var config = await ReadConfigAsync(arguments.RunFolder, cancellationToken);

        var summary = RunAnalyzer.Analyze(
            Path.GetFileName(arguments.RunFolder),
            samples,
            config,
            arguments.WarmFolder is null ? null : Path.GetFileName(arguments.WarmFolder),
            warm);

        var markdown = SummaryMarkdown.Render(summary);
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        var jsonPath = Path.Combine(arguments.RunFolder, "summary.json");
        var mdPath   = Path.Combine(arguments.RunFolder, "summary.md");

        await File.WriteAllTextAsync(
            jsonPath,
            JsonSerializer.Serialize(summary, ResultsJsonContext.Default.RunSummary),
            encoding,
            cancellationToken);
        await File.WriteAllTextAsync(mdPath, markdown, encoding, cancellationToken);

        Console.Write(markdown);
        Console.WriteLine();
        Console.WriteLine($"Summary: {jsonPath}");
        Console.WriteLine($"         {mdPath}");
        return 0;
    }

    private static async Task<SampleTable> ReadSamplesAsync(string folder, CancellationToken cancellationToken)
    {
        var path = Path.Combine(folder, SamplesFile);
        return SampleTable.Parse(await File.ReadAllTextAsync(path, cancellationToken), path);
    }

    // Null when the run has no config.json, as a hand-made one may not; the scale settings are then left out.
    private static async Task<JsonElement?> ReadConfigAsync(string folder, CancellationToken cancellationToken)
    {
        var path = Path.Combine(folder, ConfigFile);
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream   = File.OpenRead(path);
        using var       document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return document.RootElement.Clone();
    }
}
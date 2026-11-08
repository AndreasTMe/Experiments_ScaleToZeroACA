namespace InfraRunner.Infra;

// A copy of a variant's image with all its files in one layer that also holds a random file, plus the empty layer
// WORKDIR adds. That layer is new to every node, so a replica that runs the copy downloads the whole image. A new tag
// on the same digest isn't enough: the node already has the layers, and the baseline's pulls of them took about 100 ms.
internal static class ForcedPullImage
{
    public const string NonceFile = "pull-nonce";

    // COPY --from keeps the files with their owners and modes, but not the image config, so the settings a container
    // runs with are written out again. Labels aren't, since nothing reads them at run time.
    public static string Dockerfile(string sourceImage, JsonElement config)
    {
        var text = new StringBuilder();

        Line(text, $"FROM {sourceImage} AS source");
        Line(text, $"COPY {NonceFile} /{NonceFile}");
        Line(text, "FROM scratch");
        Line(text, "COPY --from=source / /");

        if (config.TryGetProperty("Env", out var env) && env.ValueKind == JsonValueKind.Array)
        {
            foreach (var variable in env.EnumerateArray().Select(item => item.GetString() ?? ""))
            {
                var equals = variable.IndexOf('=');
                if (equals > 0)
                {
                    Line(text, $"ENV {variable[..equals]}=\"{Escape(variable[(equals + 1)..])}\"");
                }
            }
        }

        if (Text(config, "WorkingDir") is { } workingDirectory)
        {
            Line(text, $"WORKDIR {workingDirectory}");
        }

        if (Text(config, "User") is { } user)
        {
            Line(text, $"USER {user}");
        }

        // docker image inspect prints these as JSON arrays, which is the exec form a Dockerfile takes.
        foreach (var instruction in (string[])["Entrypoint", "Cmd"])
        {
            if (config.TryGetProperty(instruction, out var value) && value.ValueKind == JsonValueKind.Array)
            {
                Line(text, $"{instruction.ToUpperInvariant()} {value.GetRawText()}");
            }
        }

        return text.ToString();
    }

    private static string? Text(JsonElement config, string name) =>
        config.TryGetProperty(name, out var value) && value.GetString() is { Length: > 0 } text ? text : null;

    // Inside double quotes a Dockerfile still expands $ and reads backslashes as escapes.
    private static string Escape(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("$", "\\$", StringComparison.Ordinal);

    private static void Line(StringBuilder text, string line) => text.Append(line).Append('\n');
}
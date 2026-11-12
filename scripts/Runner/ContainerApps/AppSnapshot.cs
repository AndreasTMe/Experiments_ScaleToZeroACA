namespace Runner.ContainerApps;

// The app as it was configured when a run started. ScaleConfig is the app's scaleConfig tag, which names the parameter
// file it was deployed with. Template holds the scale settings, image and resources.
internal sealed record AppSnapshot(
    string Variant,
    string Name,
    string? ScaleConfig,
    string? LatestRevisionName,
    JsonElement? Template)
{
    // Methods, not properties, so config.json doesn't repeat what the template already holds.
    public int? ScaleValue(string name) =>
        Template is { } template
        && template.TryGetProperty("scale", out var scale)
        && scale.ValueKind == JsonValueKind.Object
        && scale.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : null;

    public int MinReplicas() => ScaleValue("minReplicas") ?? 0;
}
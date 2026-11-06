namespace Runner.ContainerApps;

// The app as it was configured when a run started. Template holds the scale settings, image and resources.
internal sealed record AppSnapshot(string Variant, string Name, string? LatestRevisionName, JsonElement? Template);
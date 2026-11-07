namespace Runner.ContainerApps;

// The registry and Service Bus namespace names end in a suffix generated from the subscription and resource group.
// Results go into a public repo, so those names are written as placeholders.
internal static class PublicNames
{
    // Plain words, because the JSON writer escapes angle brackets.
    public const string Registry  = "redacted";
    public const string Namespace = "redacted";

    // "acrname.azurecr.io/repository:tag" to "redacted.azurecr.io/repository:tag". Other images stay as they are.
    public static string Image(string image) =>
        ContainerApp.SplitRegistryImage(image) is var (_, repositoryAndTag)
            ? $"{Registry}{ContainerApp.RegistryHostSuffix}/{repositoryAndTag}"
            : image;

    // The template with each container's image and each scale rule's namespace replaced.
    public static JsonElement Template(JsonElement template)
    {
        var node = JsonNode.Parse(template.GetRawText())!;

        foreach (var container in node["containers"] as JsonArray ?? [])
        {
            if (container?["image"] is JsonValue image && image.TryGetValue<string>(out var name))
            {
                container["image"] = Image(name);
            }
        }

        foreach (var rule in node["scale"]?["rules"] as JsonArray ?? [])
        {
            if (rule?["custom"]?["metadata"] is JsonObject metadata && metadata.ContainsKey("namespace"))
            {
                metadata["namespace"] = Namespace;
            }
        }

        using var document = JsonDocument.Parse(node.ToJsonString());
        return document.RootElement.Clone();
    }
}
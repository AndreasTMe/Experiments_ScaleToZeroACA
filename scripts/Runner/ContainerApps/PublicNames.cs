namespace Runner.ContainerApps;

// The registry and Service Bus namespace names end in a suffix generated from the subscription and resource group,
// and a resource ID holds the subscription ID. Results go into a public repo, so those are written as placeholders.
internal static class PublicNames
{
    // Plain words, because the JSON writer escapes angle brackets.
    public const string Registry  = "redacted";
    public const string Namespace = "redacted";

    // The identity variant's environment names its namespace and its identity's client ID.
    private static readonly Dictionary<string, string> EnvironmentValues = new(StringComparer.Ordinal)
    {
        ["SERVICEBUS_NAMESPACE"] = $"{Namespace}.servicebus.windows.net",
        ["AZURE_CLIENT_ID"]      = "redacted"
    };

    // "acrname.azurecr.io/repository:tag" to "redacted.azurecr.io/repository:tag". Other images stay as they are.
    public static string Image(string image) =>
        ContainerApp.SplitRegistryImage(image) is var (_, repositoryAndTag)
            ? $"{Registry}{ContainerApp.RegistryHostSuffix}/{repositoryAndTag}"
            : image;

    // The template with each container's image, the environment values above and each scale rule's namespace replaced,
    // and each scale rule's identity cut from a resource ID to the identity's name. "system" stays as it is.
    public static JsonElement Template(JsonElement template)
    {
        var node = JsonNode.Parse(template.GetRawText())!;

        foreach (var container in node["containers"] as JsonArray ?? [])
        {
            if (container?["image"] is JsonValue image && image.TryGetValue<string>(out var name))
            {
                container["image"] = Image(name);
            }

            foreach (var variable in container?["env"] as JsonArray ?? [])
            {
                if (variable?["name"] is JsonValue key
                    && key.TryGetValue<string>(out var variableName)
                    && EnvironmentValues.TryGetValue(variableName, out var value)
                    && variable is JsonObject entry
                    && entry.ContainsKey("value"))
                {
                    entry["value"] = value;
                }
            }
        }

        foreach (var rule in node["scale"]?["rules"] as JsonArray ?? [])
        {
            if (rule?["custom"]?["metadata"] is JsonObject metadata && metadata.ContainsKey("namespace"))
            {
                metadata["namespace"] = Namespace;
            }

            if (rule?["custom"] is JsonObject custom
                && custom["identity"] is JsonValue identity
                && identity.TryGetValue<string>(out var id)
                && id.Contains('/', StringComparison.Ordinal))
            {
                custom["identity"] = id[(id.LastIndexOf('/') + 1)..];
            }
        }

        using var document = JsonDocument.Parse(node.ToJsonString());
        return document.RootElement.Clone();
    }
}
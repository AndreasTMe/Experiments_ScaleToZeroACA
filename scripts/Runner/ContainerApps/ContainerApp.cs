namespace Runner.ContainerApps;

// One variant's Container App, read through az. Nothing here changes the app.
internal sealed class ContainerApp
{
    public const string RegistryHostSuffix = ".azurecr.io";

    private readonly Dictionary<string, string>  _imageByRevision = [];
    private readonly Dictionary<string, string?> _digestByImage   = [];

    public string Name          { get; }
    public string ResourceGroup { get; }

    public ContainerApp(string name, string resourceGroup)
    {
        Name          = name;
        ResourceGroup = resourceGroup;
    }

    public async Task<int> GetReplicaCountAsync(CancellationToken cancellationToken)
    {
        var replicas = await AzCli.RunJsonWithRetriesAsync(
            AzCommands.ReplicaList(Name, ResourceGroup),
            cancellationToken);

        // az prints the list itself; the REST API wraps it in "value". Either is counted the same way.
        if (replicas.ValueKind == JsonValueKind.Object && replicas.TryGetProperty("value", out var value))
        {
            replicas = value;
        }

        return replicas.GetArrayLength();
    }

    // Polls until the latest revision has no replicas, and returns when that was seen.
    public async Task<DateTimeOffset> WaitForZeroReplicasAsync(
        TimeSpan pollInterval,
        TimeSpan limit,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + limit;
        var waiting  = false;

        while (true)
        {
            var count = await GetReplicaCountAsync(cancellationToken);
            if (count == 0)
            {
                return DateTimeOffset.UtcNow;
            }

            if (DateTimeOffset.UtcNow >= deadline)
            {
                throw new TimeoutException(
                    $"{Name} still had {count} replica(s) after {limit.TotalMinutes:F0} minutes. "
                    + "Is min replicas above 0?");
            }

            if (!waiting)
            {
                Console.WriteLine($"{Name}: {count} replica(s) running, waiting for scale-in to zero.");
                waiting = true;
            }

            await Task.Delay(pollInterval, cancellationToken);
        }
    }

    public async Task<AppSnapshot> GetSnapshotAsync(string variant, CancellationToken cancellationToken)
    {
        var app        = await AzCli.RunJsonAsync(AzCommands.AppShow(Name, ResourceGroup), cancellationToken);
        var properties = app.GetProperty("properties");

        return new AppSnapshot(
            variant,
            Name,
            properties.TryGetProperty("latestRevisionName", out var revision) ? revision.GetString() : null,
            properties.TryGetProperty("template", out var template) ? PublicNames.Template(template) : null);
    }

    // The image the revision runs, as a tag. Revisions don't carry the digest, so that comes from the registry.
    public async Task<string> GetImageAsync(string revision, CancellationToken cancellationToken)
    {
        if (_imageByRevision.TryGetValue(revision, out var cached))
        {
            return cached;
        }

        var details = await AzCli.RunJsonWithRetriesAsync(
            AzCommands.RevisionShow(Name, ResourceGroup, revision),
            cancellationToken);

        var image = details.GetProperty("properties").GetProperty("template").GetProperty("containers")[0]
            .GetProperty("image")
            .GetString()!;

        _imageByRevision[revision] = image;
        return image;
    }

    // The digest the registry holds for the image's tag when this runs. Null for an image outside a registry.
    public async Task<string?> GetDigestAsync(string image, CancellationToken cancellationToken)
    {
        if (_digestByImage.TryGetValue(image, out var cached))
        {
            return cached;
        }

        string? digest = null;

        if (SplitRegistryImage(image) is var (registry, repositoryAndTag))
        {
            var manifest = await AzCli.RunJsonWithRetriesAsync(
                AzCommands.RepositoryShow(registry, repositoryAndTag),
                cancellationToken);
            digest = manifest.GetProperty("digest").GetString();
        }

        _digestByImage[image] = digest;
        return digest;
    }

    // "acrname.azurecr.io/repository:tag" to ("acrname", "repository:tag").
    public static (string Registry, string RepositoryAndTag)? SplitRegistryImage(string image)
    {
        var slash = image.IndexOf('/');
        if (slash < 0 || !image[..slash].EndsWith(RegistryHostSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return (image[..(slash - RegistryHostSuffix.Length)], image[(slash + 1)..]);
    }
}
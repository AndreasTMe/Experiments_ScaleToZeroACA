namespace Runner.ContainerApps;

// Every az command the sample command runs, in one place, so the dry run prints exactly what a real run would run.
// Flags are checked against docs/platform-facts.md.
internal static class AzCommands
{
    public static string[] AccountShow() => ["account", "show", "-o", "json"];

    public static string[] Version() => ["version", "-o", "json"];

    public static string[] AppShow(string app, string resourceGroup) =>
        ["containerapp", "show", "--name", app, "--resource-group", resourceGroup, "-o", "json"];

    // Lists the replicas of the latest revision, which is the only one: the apps run in single revision mode.
    public static string[] ReplicaList(string app, string resourceGroup) =>
        ["containerapp", "replica", "list", "--name", app, "--resource-group", resourceGroup, "-o", "json"];

    public static string[] RevisionShow(string app, string resourceGroup, string revision) =>
    [
        "containerapp", "revision", "show",
        "--name", app,
        "--resource-group", resourceGroup,
        "--revision", revision,
        "-o", "json"
    ];

    public static string[] RepositoryShow(string registry, string image) =>
        ["acr", "repository", "show", "--name", registry, "--image", image, "-o", "json"];
}
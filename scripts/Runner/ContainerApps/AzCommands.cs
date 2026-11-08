namespace Runner.ContainerApps;

// Every az command the sample command runs, in one place, so the dry run prints exactly what a real run would run.
// Flags are checked against docs/platform-facts.md.
internal static class AzCommands
{
    // The container's name in infra/apps.bicep.
    private const string WorkerContainer = "worker";

    public static string[] Version() => ["version", "-o", "json"];

    public static string[] AppShow(string app, string resourceGroup) =>
        ["containerapp", "show", "--name", app, "--resource-group", resourceGroup, "-o", "json"];

    // Lists the replicas of the latest revision, which is the only one: the apps run in single revision mode.
    // Sets the worker container's image, which makes a new revision.
    public static string[] AppUpdateImage(string app, string resourceGroup, string image) =>
    [
        "containerapp", "update",
        "--name", app,
        "--resource-group", resourceGroup,
        "--container-name", WorkerContainer,
        "--image", image,
        "-o", "json"
    ];

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

    public static string[] ExtensionShow(string name) => ["extension", "show", "--name", name, "-o", "json"];

    public static string[] WorkspaceShow(string resourceGroup, string workspace) =>
    [
        "monitor", "log-analytics", "workspace", "show",
        "--resource-group", resourceGroup,
        "--workspace-name", workspace,
        "-o", "json"
    ];

    // Without --timespan the query reads all retained data, so the window is the query's own TimeGenerated filter.
    public static string[] LogAnalyticsQuery(string workspaceGuid, string query) =>
        ["monitor", "log-analytics", "query", "--workspace", workspaceGuid, "--analytics-query", query, "-o", "json"];
}
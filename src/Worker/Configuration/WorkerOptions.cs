namespace Worker.Configuration;

internal sealed record WorkerOptions(
    string ConnectionString,
    string Variant,
    string QueueName,
    string DoneQueueName,
    string? ReplicaName,
    string? RevisionName)
{
    // CONTAINER_APP_REPLICA_NAME and CONTAINER_APP_REVISION are set by Container Apps, and are missing in local runs.
    public static WorkerOptions FromEnvironment() =>
        new(
            GetRequired("SERVICEBUS_CONNECTION_STRING"),
            GetRequired("WORKER_VARIANT"),
            GetRequired("WORKER_QUEUE"),
            GetRequired("WORKER_DONE_QUEUE"),
            Environment.GetEnvironmentVariable("CONTAINER_APP_REPLICA_NAME"),
            Environment.GetEnvironmentVariable("CONTAINER_APP_REVISION"));

    private static string GetRequired(string name) =>
        Environment.GetEnvironmentVariable(name)
        ?? throw new InvalidOperationException($"Environment variable {name} is not set.");
}
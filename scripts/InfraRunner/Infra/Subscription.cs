namespace InfraRunner.Infra;

internal static class Subscription
{
    // Stops unless az is signed in to the expected subscription. Never switches subscription itself.
    public static async Task EnsureAsync(ToolRunner runner, string subscriptionId)
    {
        var account = await runner.CaptureJsonAsync(
            "az",
            ["account", "show", "-o", "json"],
            $$"""{ "id": "{{subscriptionId}}" }""");

        var current = account.GetProperty("id").GetString();

        if (!string.Equals(current, subscriptionId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"az is on subscription {current}, not {subscriptionId}. Run: az account set --subscription "
                + subscriptionId);
        }

        if (!runner.DryRun)
        {
            Console.WriteLine($"Subscription {subscriptionId} confirmed.");
        }
    }

    // az group exists prints true or false.
    public static async Task<bool> ResourceGroupExistsAsync(ToolRunner runner, string resourceGroup)
    {
        var exists = await runner.CaptureJsonAsync(
            "az",
            ["group", "exists", "--name", resourceGroup, "-o", "json"],
            "true");

        return exists.ValueKind switch
        {
            JsonValueKind.True  => true,
            JsonValueKind.False => false,
            _                   => throw new InvalidOperationException($"az group exists printed {exists}.")
        };
    }
}
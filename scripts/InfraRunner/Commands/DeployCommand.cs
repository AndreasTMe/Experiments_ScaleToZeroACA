namespace InfraRunner.Commands;

// Deploys the base infrastructure at subscription scope, or the Container Apps into its resource group.
internal static class DeployCommand
{
    public static async Task RunAsync(DeployArguments arguments)
    {
        var runner = new ToolRunner(arguments.DryRun);
        var verb   = arguments.Preview ? "what-if" : "create";

        await Subscription.EnsureAsync(runner, arguments.SubscriptionId);

        if (arguments.Phase == DeployPhase.Base)
        {
            await runner.RunAsync(
                "az",
                [
                    "deployment", "sub", verb,
                    "--name", InfraNames.BaseDeployment(arguments.NamePrefix),
                    "--location", arguments.Location,
                    "--parameters", RepositoryRoot.PathTo(Path.Combine("infra", "main.bicepparam")),
                    "--parameters",
                    $"namePrefix={arguments.NamePrefix}",
                    $"location={arguments.Location}",
                    $"budgetEmail={arguments.BudgetEmail}"
                ]);
            return;
        }

        var resourceGroup = InfraNames.ResourceGroup(arguments.NamePrefix, arguments.Location);

        if (!await Subscription.ResourceGroupExistsAsync(runner, resourceGroup))
        {
            throw new InvalidOperationException($"Resource group {resourceGroup} doesn't exist. Deploy base first.");
        }

        // Inline values take precedence over the parameter file's.
        List<string> overrides =
        [
            $"namePrefix={arguments.NamePrefix}",
            $"imageTag={arguments.ImageTag}"
        ];

        if (arguments.MinReplicas is { } minReplicas)
        {
            overrides.Add($"minReplicas={minReplicas}");
        }

        if (arguments.PollingInterval is { } pollingInterval)
        {
            overrides.Add($"pollingInterval={pollingInterval}");
        }

        if (arguments.CooldownPeriod is { } cooldownPeriod)
        {
            overrides.Add($"cooldownPeriod={cooldownPeriod}");
        }

        await runner.RunAsync(
            "az",
            [
                "deployment", "group", verb,
                "--name", InfraNames.AppsDeployment(arguments.NamePrefix),
                "--resource-group", resourceGroup,
                "--parameters", RepositoryRoot.PathTo(Path.Combine("infra", "apps.bicepparam")),
                "--parameters",
                .. overrides
            ]);
    }
}
namespace InfraRunner.Commands;

// Deploys the base infrastructure at subscription scope, or the Container Apps into its resource group.
internal static class DeployCommand
{
    // A parameters file has to assign every parameter without a default, even one that's also passed inline, so the
    // two .bicepparam files read these values from the environment.
    private const string BudgetEmailVariable = "STZ_BUDGET_EMAIL";
    private const string ImageTagVariable    = "STZ_IMAGE_TAG";

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
                    $"location={arguments.Location}"
                ],
                new Dictionary<string, string>
                {
                    [BudgetEmailVariable] = arguments.BudgetEmail!
                });
            return;
        }

        var resourceGroup = InfraNames.ResourceGroup(arguments.NamePrefix, arguments.Location);

        if (!await Subscription.ResourceGroupExistsAsync(runner, resourceGroup))
        {
            throw new InvalidOperationException($"Resource group {resourceGroup} doesn't exist. Deploy base first.");
        }

        // Inline values take precedence over the parameter file's.
        List<string> overrides = [$"namePrefix={arguments.NamePrefix}"];

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
            ],
            new Dictionary<string, string>
            {
                [ImageTagVariable] = arguments.ImageTag!
            });
    }
}
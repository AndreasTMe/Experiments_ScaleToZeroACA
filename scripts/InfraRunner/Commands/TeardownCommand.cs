namespace InfraRunner.Commands;

// Deletes the resource group and the subscription budget, after a typed confirmation.
internal static class TeardownCommand
{
    public static async Task<int> RunAsync(TeardownArguments arguments)
    {
        var runner        = new ToolRunner(arguments.DryRun);
        var resourceGroup = InfraNames.ResourceGroup(arguments.NamePrefix, arguments.Location);
        var budget        = InfraNames.Budget(arguments.NamePrefix);

        await Subscription.EnsureAsync(runner, arguments.SubscriptionId);

        if (arguments.DryRun)
        {
            Console.WriteLine($"[dry run] would ask you to type '{resourceGroup}' to confirm");
        }
        else
        {
            Console.WriteLine(
                $"This deletes resource group {resourceGroup}, everything in it, and budget {budget} on subscription "
                + $"{arguments.SubscriptionId}.");
            Console.Write($"Type '{resourceGroup}' to confirm: ");

            if (Console.ReadLine() != resourceGroup)
            {
                Console.WriteLine("Not confirmed. Nothing was deleted.");
                return 1;
            }
        }

        if (await Subscription.ResourceGroupExistsAsync(runner, resourceGroup))
        {
            // Waits for the delete to finish, so the budget keeps watching until the resources are gone.
            await runner.RunAsync("az", ["group", "delete", "--name", resourceGroup, "--yes"]);
        }
        else
        {
            Console.WriteLine($"Resource group {resourceGroup} doesn't exist.");
        }

        // Without --resource-group, the command works on the subscription's budget.
        await runner.RunAsync("az", ["consumption", "budget", "delete", "--budget-name", budget]);

        return 0;
    }
}
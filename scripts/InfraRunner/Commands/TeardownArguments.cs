namespace InfraRunner.Commands;

internal sealed record TeardownArguments(string SubscriptionId, string NamePrefix, string Location, bool DryRun)
{
    public static TeardownArguments Parse(string[] args)
    {
        var options        = new OptionReader(args);
        var subscriptionId = OptionChecks.SubscriptionId(options.Required("--subscription-id"));
        var namePrefix     = OptionChecks.NamePrefix(options.Optional("--name-prefix"));
        var location       = OptionChecks.Location(options.Optional("--location"));
        var dryRun         = options.Switch("--dry-run");
        options.RejectUnread("teardown");

        return new TeardownArguments(subscriptionId, namePrefix, location, dryRun);
    }
}
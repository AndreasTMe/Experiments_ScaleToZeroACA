namespace InfraRunner.Commands;

// BudgetEmail is set for the base phase only, and the image and scale settings for the apps phase only. Scale settings
// left unset keep the template's values: min replicas 0, polling and cooldown at the platform defaults. ScaleConfig
// names a file in infra/levers, and comes instead of the separate settings, so a run's tag always matches a file.
internal sealed record DeployArguments(
    DeployPhase Phase,
    string SubscriptionId,
    string NamePrefix,
    string Location,
    string? BudgetEmail,
    string? ImageTag,
    string? ScaleConfig,
    int? MinReplicas,
    int? PollingInterval,
    int? CooldownPeriod,
    bool Preview,
    bool DryRun)
{
    public static DeployArguments Parse(string[] args)
    {
        var phase = args switch
        {
            ["base", ..] => DeployPhase.Base,
            ["apps", ..] => DeployPhase.Apps,
            _            => throw new UsageException("deploy takes base or apps first.")
        };

        var options        = new OptionReader(args[1..]);
        var subscriptionId = OptionChecks.SubscriptionId(options.Required("--subscription-id"));
        var namePrefix     = OptionChecks.NamePrefix(options.Optional("--name-prefix"));
        var location       = OptionChecks.Location(options.Optional("--location"));
        var preview        = options.Switch("--preview");
        var dryRun         = options.Switch("--dry-run");

        if (phase == DeployPhase.Base)
        {
            var budgetEmail = OptionChecks.Email(options.Required("--budget-email"));
            options.RejectUnread("deploy base");

            return new DeployArguments(
                phase,
                subscriptionId,
                namePrefix,
                location,
                budgetEmail,
                null,
                null,
                null,
                null,
                null,
                preview,
                dryRun);
        }

        // The minimums match infra/apps.bicep.
        var imageTag        = OptionChecks.ImageTag(options.Required("--image-tag"));
        var scaleConfig     = options.Optional("--config") is { } config ? OptionChecks.ScaleConfig(config) : null;
        var minReplicas     = options.OptionalInt("--min-replicas", 0);
        var pollingInterval = options.OptionalInt("--polling-interval", 1);
        var cooldownPeriod  = options.OptionalInt("--cooldown-period", 0);
        options.RejectUnread("deploy apps");

        if (scaleConfig is not null && (minReplicas ?? pollingInterval ?? cooldownPeriod) is not null)
        {
            throw new UsageException(
                "--config sets the scale settings itself, so it can't take --min-replicas, --polling-interval or "
                + "--cooldown-period too.");
        }

        return new DeployArguments(
            phase,
            subscriptionId,
            namePrefix,
            location,
            null,
            imageTag,
            scaleConfig,
            minReplicas,
            pollingInterval,
            cooldownPeriod,
            preview,
            dryRun);
    }
}
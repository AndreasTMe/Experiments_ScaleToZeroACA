namespace InfraRunner.Commands;

internal sealed record PushImagesArguments(
    string SubscriptionId,
    string ImageTag,
    IReadOnlyList<string> Variants,
    string NamePrefix,
    int ForcedPulls,
    bool DryRun)
{
    // Two digits in the tag. A run needs one copy per sample, and runs here are 30 samples.
    public const int MaxForcedPulls = 99;

    public static PushImagesArguments Parse(string[] args)
    {
        var options        = new OptionReader(args);
        var subscriptionId = OptionChecks.SubscriptionId(options.Required("--subscription-id"));
        var imageTag       = OptionChecks.ImageTag(options.Required("--image-tag"));
        var variants       = VariantList.Parse(options.Optional("--variants"));
        var namePrefix     = OptionChecks.NamePrefix(options.Optional("--name-prefix"));
        var forcedPulls    = options.OptionalInt("--forced-pulls", 1) ?? 0;
        var dryRun         = options.Switch("--dry-run");
        options.RejectUnread("push-images");

        if (forcedPulls > MaxForcedPulls)
        {
            throw new UsageException($"--forced-pulls takes a whole number from 1 to {MaxForcedPulls}.");
        }

        return new PushImagesArguments(subscriptionId, imageTag, variants, namePrefix, forcedPulls, dryRun);
    }
}
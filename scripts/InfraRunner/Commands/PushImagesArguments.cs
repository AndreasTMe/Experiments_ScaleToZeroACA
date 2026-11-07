namespace InfraRunner.Commands;

internal sealed record PushImagesArguments(
    string SubscriptionId,
    string ImageTag,
    IReadOnlyList<string> Variants,
    string NamePrefix,
    bool DryRun)
{
    public static PushImagesArguments Parse(string[] args)
    {
        var options        = new OptionReader(args);
        var subscriptionId = OptionChecks.SubscriptionId(options.Required("--subscription-id"));
        var imageTag       = OptionChecks.ImageTag(options.Required("--image-tag"));
        var variants       = VariantList.Parse(options.Optional("--variants"));
        var namePrefix     = OptionChecks.NamePrefix(options.Optional("--name-prefix"));
        var dryRun         = options.Switch("--dry-run");
        options.RejectUnread("push-images");

        return new PushImagesArguments(subscriptionId, imageTag, variants, namePrefix, dryRun);
    }
}
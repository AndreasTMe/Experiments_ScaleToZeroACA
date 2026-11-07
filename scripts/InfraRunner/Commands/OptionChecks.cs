namespace InfraRunner.Commands;

// Checks values before they reach a command line. Keeping them to plain characters also keeps them safe from cmd,
// which runs az on Windows.
internal static class OptionChecks
{
    public const string DefaultNamePrefix = "stz";
    public const string DefaultLocation   = "uksouth";

    private const int MaxImageTagLength = 100;

    public static string SubscriptionId(string value) =>
        Guid.TryParseExact(value, "D", out _)
            ? value
            : throw new UsageException("--subscription-id takes a subscription GUID.");

    // The 2 to 8 characters match main.bicep. Lower case, because Container Apps names must be.
    public static string NamePrefix(string? value)
    {
        value ??= DefaultNamePrefix;

        return value.Length is >= 2 and <= 8 && value.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c))
            ? value
            : throw new UsageException("--name-prefix takes 2 to 8 lower-case letters or digits.");
    }

    public static string Location(string? value)
    {
        value ??= DefaultLocation;

        return value.Length > 0 && value.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c))
            ? value
            : throw new UsageException("--location takes a region name such as uksouth.");
    }

    // Becomes the end of a Docker tag, <variant>-<tag>, which allows letters, digits, '_', '.' and '-'.
    public static string ImageTag(string value) =>
        value.Length is > 0 and <= MaxImageTagLength
        && (char.IsAsciiLetterOrDigit(value[0]) || value[0] == '_')
        && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '.' or '-')
            ? value
            : throw new UsageException(
                $"--image-tag takes up to {MaxImageTagLength} letters, digits, '_', '.' and '-', not starting with "
                + "'.' or '-'.");

    public static string Email(string value) =>
        value.Count(c => c == '@') == 1
        && value.Length >= 3
        && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '@' or '.' or '-' or '_' or '+')
            ? value
            : throw new UsageException("--budget-email takes an email address of letters, digits and @ . - _ +.");
}
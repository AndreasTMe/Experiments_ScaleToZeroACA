namespace Runner.Commands;

internal static class VariantList
{
    private static readonly string[] Known = ["jit", "r2r", "aot", "jit-chiseled", "jit-identity"];

    // Null unless the list is distinct known variants separated by commas.
    public static IReadOnlyList<string>? Parse(string list)
    {
        var variants = list.Split(',');

        if (variants.Any(variant => !Known.Contains(variant)) || variants.Distinct().Count() != variants.Length)
        {
            return null;
        }

        return variants;
    }
}
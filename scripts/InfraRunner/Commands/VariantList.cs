namespace InfraRunner.Commands;

internal static class VariantList
{
    public static readonly IReadOnlyList<string> All = ["jit", "r2r", "aot", "jit-chiseled"];

    public static IReadOnlyList<string> Parse(string? list)
    {
        if (list is null)
        {
            return All;
        }

        var variants = list.Split(',');

        if (variants.Any(variant => !All.Contains(variant)) || variants.Distinct().Count() != variants.Length)
        {
            throw new UsageException(
                "--variants takes distinct names, comma-separated, from jit, r2r, aot and jit-chiseled.");
        }

        return variants;
    }
}
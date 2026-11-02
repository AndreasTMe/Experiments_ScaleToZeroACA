namespace Runner.Commands;

internal sealed record SendArguments(IReadOnlyList<string> Variants)
{
    private static readonly string[] KnownVariants = ["jit", "r2r", "aot", "jit-chiseled"];

    // Returns null unless the arguments are "send --local --variant <names>", with distinct known variants separated
    // by commas.
    public static SendArguments? Parse(string[] args)
    {
        if (args is not ["send", "--local", "--variant", var list])
        {
            return null;
        }

        var variants = list.Split(',');

        if (variants.Any(variant => !KnownVariants.Contains(variant)) || variants.Distinct().Count() != variants.Length)
        {
            return null;
        }

        return new SendArguments(variants);
    }
}
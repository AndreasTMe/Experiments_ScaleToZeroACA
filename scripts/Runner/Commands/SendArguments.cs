namespace Runner.Commands;

internal sealed record SendArguments(string Variant)
{
    private static readonly string[] Variants = ["jit", "r2r", "aot", "jit-chiseled"];

    // Returns null if the arguments aren't "send --local --variant <name>" with a known variant.
    public static SendArguments? Parse(string[] args)
    {
        if (args is not ["send", "--local", "--variant", var variant] || !Variants.Contains(variant))
        {
            return null;
        }

        return new SendArguments(variant);
    }
}
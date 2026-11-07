namespace Runner.Commands;

internal sealed record SendArguments(IReadOnlyList<string> Variants)
{
    // Returns null unless the arguments are "send --local --variant <names>".
    public static SendArguments? Parse(string[] args)
    {
        if (args is not ["send", "--local", "--variant", var list])
        {
            return null;
        }

        return VariantList.Parse(list) is { } variants ? new SendArguments(variants) : null;
    }
}
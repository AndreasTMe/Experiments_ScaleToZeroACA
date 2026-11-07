namespace Runner.Analysis;

internal static class Statistics
{
    // Null when there are no values.
    public static DurationStats? Summarize(IEnumerable<double> values)
    {
        var sorted = values.Order().ToArray();

        return sorted.Length == 0
            ? null
            : new DurationStats(
                sorted.Length,
                Round(Quantile(sorted, 0.5)),
                Round(Quantile(sorted, 0.9)),
                Round(sorted[^1]));
    }

    // Linear between the two nearest samples, as Excel's PERCENTILE.INC and NumPy's default do.
    public static double Quantile(IReadOnlyList<double> sorted, double fraction)
    {
        var position = (sorted.Count - 1) * fraction;
        var lower    = (int)Math.Floor(position);
        var upper    = Math.Min(lower + 1, sorted.Count - 1);

        return sorted[lower] + (position - lower) * (sorted[upper] - sorted[lower]);
    }

    public static double Round(double value) => Math.Round(value, 1, MidpointRounding.AwayFromZero);
}
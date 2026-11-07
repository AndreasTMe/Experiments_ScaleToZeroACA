namespace Runner.Logs;

// The kubelet writes durations the Go way, such as "109ms", "1.2s" or "1m3.5s".
internal static class GoDuration
{
    public static bool TryParse(string text, out TimeSpan duration)
    {
        duration = TimeSpan.Zero;
        var index = 0;

        while (index < text.Length)
        {
            var numberStart = index;
            while (index < text.Length && (char.IsAsciiDigit(text[index]) || text[index] == '.'))
            {
                index++;
            }

            var unitStart = index;
            while (index < text.Length && !char.IsAsciiDigit(text[index]) && text[index] != '.')
            {
                index++;
            }

            if (!double.TryParse(
                    text.AsSpan(numberStart, unitStart - numberStart),
                    NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture,
                    out var number))
            {
                return false;
            }

            double? milliseconds = text[unitStart..index] switch
            {
                "ns"         => number / 1_000_000,
                "us" or "µs" => number / 1_000,
                "ms"         => number,
                "s"          => number * 1_000,
                "m"          => number * 60_000,
                "h"          => number * 3_600_000,
                _            => null
            };

            if (milliseconds is null)
            {
                return false;
            }

            duration += TimeSpan.FromMilliseconds(milliseconds.Value);
        }

        return text.Length > 0;
    }
}
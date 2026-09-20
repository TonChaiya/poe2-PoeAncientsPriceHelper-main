using System.Globalization;
using System.Text.RegularExpressions;

namespace PoeTradeOverlay.Parsing;

internal static partial class NumericText
{
    [GeneratedRegex(@"(?<![A-Za-z])[-+]?\d+(?:\.\d+)?", RegexOptions.CultureInvariant)]
    private static partial Regex NumberPattern();

    [GeneratedRegex(@"(?<min>\d+(?:\.\d+)?)\s*-\s*(?<max>\d+(?:\.\d+)?)", RegexOptions.CultureInvariant)]
    private static partial Regex UnsignedRangePattern();

    public static IReadOnlyList<decimal> Values(string text) =>
        NumberPattern().Matches(text)
            .Select(m => decimal.Parse(m.Value, NumberStyles.Number | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture))
            .ToArray();

    public static int? FirstInt(string text)
    {
        var value = Values(text).FirstOrDefault();
        return value == decimal.Truncate(value) ? (int)value : null;
    }

    public static bool TryUnsignedRange(string text, out decimal min, out decimal max)
    {
        var match = UnsignedRangePattern().Match(text);
        if (match.Success &&
            decimal.TryParse(match.Groups["min"].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out min) &&
            decimal.TryParse(match.Groups["max"].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out max))
            return true;
        min = max = 0;
        return false;
    }
}

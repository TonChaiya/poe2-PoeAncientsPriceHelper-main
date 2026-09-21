using System.Globalization;
using System.Text.RegularExpressions;
using PoeTradeOverlay.Models;

namespace PoeTradeOverlay.Parsing;

internal static partial class NumericText
{
    [GeneratedRegex(@"(?<![A-Za-z])[-+]?\d+(?:\.\d+)?", RegexOptions.CultureInvariant)]
    private static partial Regex NumberPattern();

    [GeneratedRegex(@"(?<min>\d+(?:\.\d+)?)\s*-\s*(?<max>\d+(?:\.\d+)?)", RegexOptions.CultureInvariant)]
    private static partial Regex UnsignedRangePattern();

    [GeneratedRegex(@"(?<current>[-+]?\d+(?:\.\d+)?)\s*\(\s*(?<min>[-+]?\d+(?:\.\d+)?)\s*-\s*(?<max>[-+]?\d+(?:\.\d+)?)\s*\)", RegexOptions.CultureInvariant)]
    private static partial Regex RollPattern();

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

    public static IReadOnlyList<NumericRoll> Rolls(string text)
    {
        var rolls = new List<NumericRoll>();
        foreach (Match match in RollPattern().Matches(text))
        {
            rolls.Add(new NumericRoll(Parse(match, "current"), Parse(match, "min"), Parse(match, "max")));
        }
        if (rolls.Count > 0) return rolls;
        return Values(text).Select(value => new NumericRoll(value, null, null)).ToArray();
    }

    private static decimal Parse(Match match, string group) =>
        decimal.Parse(match.Groups[group].Value, NumberStyles.Number | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture);
}

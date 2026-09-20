using System.Text.RegularExpressions;
using PoeTradeOverlay.Models;

namespace PoeTradeOverlay.Trade;

public sealed record ModifierMatch(bool IsSupported, string? StatId, string SourceText)
{
    public static ModifierMatch Unsupported(string source) => new(false, null, source);
}

public static partial class ModifierMatcher
{
    [GeneratedRegex(@"\d+(?:\.\d+)?", RegexOptions.CultureInvariant)]
    private static partial Regex NumberPattern();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespacePattern();

    public static ModifierMatch Match(ParsedModifier modifier, TradeMetadataSnapshot metadata)
    {
        string source = Normalize(modifier.Text);
        var matches = metadata.Stats.Where(stat =>
                (modifier.Kind == ModifierKind.Unknown || stat.Kind == modifier.Kind) &&
                string.Equals(Normalize(stat.Text), source, StringComparison.OrdinalIgnoreCase))
            .Take(2).ToArray();
        return matches.Length == 1
            ? new ModifierMatch(true, matches[0].Id, modifier.Text)
            : ModifierMatch.Unsupported(modifier.Text);
    }

    internal static string Normalize(string text)
    {
        text = Regex.Replace(text, @"\s*\((implicit|explicit|enchant|rune)\)\s*$", "",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        text = NumberPattern().Replace(text, "#");
        return WhitespacePattern().Replace(text.Trim(), " ");
    }
}

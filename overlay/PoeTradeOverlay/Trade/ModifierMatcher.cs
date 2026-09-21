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

    [GeneratedRegex(@"\s*\([-+]?\d+(?:\.\d+)?\s*-\s*[-+]?\d+(?:\.\d+)?\)", RegexOptions.CultureInvariant)]
    private static partial Regex RollRangePattern();

    public static ModifierMatch Match(ParsedModifier modifier, TradeMetadataSnapshot metadata)
    {
        var resolved = Resolve(modifier, metadata);
        return resolved.Status == ResolutionStatus.Resolved
            ? new ModifierMatch(true, resolved.StatId, modifier.Text)
            : ModifierMatch.Unsupported(modifier.Text);
    }

    public static ResolvedModifier Resolve(ParsedModifier modifier, TradeMetadataSnapshot metadata)
    {
        string source = Normalize(modifier.Text);
        var textMatches = metadata.Stats.Where(stat =>
            string.Equals(Normalize(stat.Text), source, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (textMatches.Length == 0)
            return new(ResolutionStatus.Unsupported, null, modifier.Kind, modifier.Text, "No Trade stat matches this text.");

        if (modifier.Kind != ModifierKind.Unknown)
        {
            var family = textMatches.Where(x => x.Kind == modifier.Kind).ToArray();
            if (family.Length == 1)
                return new(ResolutionStatus.Resolved, family[0].Id, family[0].Kind, modifier.Text,
                    $"Resolved from {modifier.Kind} header evidence.");
            if (family.Length > 1)
                return new(ResolutionStatus.Ambiguous, null, modifier.Kind, modifier.Text,
                    "More than one Trade stat exists in the declared modifier family.");
            return new(ResolutionStatus.Unsupported, null, modifier.Kind, modifier.Text,
                "The declared modifier family has no matching Trade stat.");
        }

        if (textMatches.Length == 1)
            return new(ResolutionStatus.Resolved, textMatches[0].Id, textMatches[0].Kind, modifier.Text,
                "Only one compatible Trade stat exists.");
        return new(ResolutionStatus.Ambiguous, null, ModifierKind.Unknown, modifier.Text,
            "The same text exists in multiple modifier families; left unchecked.");
    }

    internal static string Normalize(string text)
    {
        text = Regex.Replace(text, @"\s*\((implicit|explicit|fractured|crafted|enchant|rune|augment|desecrated|sanctum|skill)\)\s*$", "",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        text = RollRangePattern().Replace(text, "");
        text = NumberPattern().Replace(text, "#");
        return WhitespacePattern().Replace(text.Trim(), " ");
    }
}

using System.Globalization;
using PoeTradeOverlay.Models;

namespace PoeTradeOverlay.Parsing;

public static class PoeItemTextParser
{
    private const string Separator = "--------";

    public static bool TryParse(string? text, out ParsedItem item)
    {
        item = null!;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n', StringSplitOptions.TrimEntries);
        if (lines.Length < 4 || !lines[0].StartsWith("Item Class:", StringComparison.OrdinalIgnoreCase) ||
            !lines[1].StartsWith("Rarity:", StringComparison.OrdinalIgnoreCase)) return false;

        var itemClass = ValueAfterColon(lines[0]);
        if (!Enum.TryParse<ItemRarity>(ValueAfterColon(lines[1]), true, out var rarity)) return false;

        int firstSeparator = Array.IndexOf(lines, Separator);
        if (firstSeparator < 3) return false;
        var identity = lines[2..firstSeparator].Where(x => x.Length > 0).ToArray();
        if (identity.Length == 0) return false;
        string baseType = identity[^1];
        string name = identity.Length > 1 ? identity[0] : "";
        if (string.IsNullOrWhiteSpace(baseType)) return false;

        int? itemLevel = null;
        int? quality = null;
        decimal? physicalAverage = null;
        decimal? attacksPerSecond = null;
        bool corrupted = false, identified = true, mirrored = false, fractured = false, crafted = false, desecrated = false;
        int? requiredLevel = null, requiredStrength = null, requiredDexterity = null, requiredIntelligence = null;
        var modifiers = new List<ParsedModifier>();
        var properties = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var propertyBlocks = new List<ParsedProperty>();
        string? pendingHeader = null;

        for (int i = firstSeparator + 1; i < lines.Length; i++)
        {
            string line = lines[i];
            if (line.Length == 0 || line == Separator) continue;
            if (line.Equals("Corrupted", StringComparison.OrdinalIgnoreCase)) { corrupted = true; continue; }
            if (line.Equals("Unidentified", StringComparison.OrdinalIgnoreCase)) { identified = false; continue; }
            if (line.Equals("Mirrored", StringComparison.OrdinalIgnoreCase)) { mirrored = true; continue; }
            if (line.StartsWith('{') && line.EndsWith('}'))
            {
                pendingHeader = line;
                fractured |= line.Contains("Fractured", StringComparison.OrdinalIgnoreCase);
                crafted |= line.Contains("Crafted", StringComparison.OrdinalIgnoreCase);
                desecrated |= line.Contains("Desecrated", StringComparison.OrdinalIgnoreCase);
                continue;
            }
            if (line.StartsWith("Item Level:", StringComparison.OrdinalIgnoreCase))
            {
                itemLevel = NumericText.FirstInt(line);
                continue;
            }
            if (line.StartsWith("Quality:", StringComparison.OrdinalIgnoreCase))
            {
                quality = NumericText.FirstInt(line);
                propertyBlocks.Add(new("quality", line, NumericText.Values(line)));
                continue;
            }
            if (line.StartsWith("Physical Damage:", StringComparison.OrdinalIgnoreCase))
            {
                if (NumericText.TryUnsignedRange(ValueAfterColon(line), out var min, out var max))
                    physicalAverage = (min + max) / 2m;
                propertyBlocks.Add(new("physical_damage", line, NumericText.Values(line)));
                continue;
            }
            if (line.StartsWith("Attacks per Second:", StringComparison.OrdinalIgnoreCase))
            {
                attacksPerSecond = NumericText.Values(ValueAfterColon(line)).FirstOrDefault();
                propertyBlocks.Add(new("attacks_per_second", line, NumericText.Values(line)));
                continue;
            }
            if (line.StartsWith("Requires Level ", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("Requires:", StringComparison.OrdinalIgnoreCase))
            {
                requiredLevel = ValueFollowing(line, "Level");
                requiredStrength = ValueFollowing(line, "Str");
                requiredDexterity = ValueFollowing(line, "Dex");
                requiredIntelligence = ValueFollowing(line, "Int");
                continue;
            }
            if (line.StartsWith("Level:", StringComparison.OrdinalIgnoreCase)) { requiredLevel = NumericText.FirstInt(line); continue; }
            if (line.StartsWith("Str:", StringComparison.OrdinalIgnoreCase)) { requiredStrength = NumericText.FirstInt(line); continue; }
            if (line.StartsWith("Dex:", StringComparison.OrdinalIgnoreCase)) { requiredDexterity = NumericText.FirstInt(line); continue; }
            if (line.StartsWith("Int:", StringComparison.OrdinalIgnoreCase)) { requiredIntelligence = NumericText.FirstInt(line); continue; }
            if (IsNonModifierProperty(line)) continue;

            var kind = KindFromEvidence(line, pendingHeader);
            if (line.EndsWith("(implicit)", StringComparison.OrdinalIgnoreCase)) kind = ModifierKind.Implicit;
            else if (line.EndsWith("(enchant)", StringComparison.OrdinalIgnoreCase)) kind = ModifierKind.Enchant;
            else if (line.EndsWith("(rune)", StringComparison.OrdinalIgnoreCase)) kind = ModifierKind.Rune;
            else if (line.Any(ch => ch > 127)) kind = ModifierKind.Unknown;
            var rolls = NumericText.Rolls(line);
            modifiers.Add(new ParsedModifier(line, kind, rolls.Select(x => x.Current).ToArray(), pendingHeader, rolls));
            pendingHeader = null;
        }

        if (physicalAverage is { } average && attacksPerSecond is > 0)
            properties["physical_dps"] = decimal.Round(average * attacksPerSecond.Value, 2);
        if (attacksPerSecond is > 0) properties["attacks_per_second"] = attacksPerSecond.Value;

        item = new ParsedItem(rarity, name, baseType, itemClass, itemLevel, quality, corrupted,
            modifiers, properties,
            new(requiredLevel, requiredStrength, requiredDexterity, requiredIntelligence),
            new(identified, corrupted, mirrored, fractured, crafted, desecrated), propertyBlocks);
        return true;
    }

    private static int? ValueFollowing(string line, string label)
    {
        var match = System.Text.RegularExpressions.Regex.Match(line,
            $@"\b{System.Text.RegularExpressions.Regex.Escape(label)}\s*:?\s*(\d+)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        return match.Success && int.TryParse(match.Groups[1].Value, CultureInfo.InvariantCulture, out int value) ? value : null;
    }

    private static ModifierKind KindFromEvidence(string line, string? header)
    {
        string evidence = (header ?? "") + " " + line;
        if (evidence.Contains("Pseudo", StringComparison.OrdinalIgnoreCase)) return ModifierKind.Pseudo;
        if (evidence.Contains("Fractured", StringComparison.OrdinalIgnoreCase)) return ModifierKind.Fractured;
        if (evidence.Contains("Crafted", StringComparison.OrdinalIgnoreCase)) return ModifierKind.Crafted;
        if (evidence.Contains("Desecrated", StringComparison.OrdinalIgnoreCase)) return ModifierKind.Desecrated;
        if (evidence.Contains("Sanctum", StringComparison.OrdinalIgnoreCase)) return ModifierKind.Sanctum;
        if (evidence.Contains("Skill", StringComparison.OrdinalIgnoreCase)) return ModifierKind.Skill;
        if (evidence.Contains("Implicit", StringComparison.OrdinalIgnoreCase)) return ModifierKind.Implicit;
        if (evidence.Contains("Enchant", StringComparison.OrdinalIgnoreCase)) return ModifierKind.Enchant;
        if (evidence.Contains("Rune", StringComparison.OrdinalIgnoreCase) || evidence.Contains("Augment", StringComparison.OrdinalIgnoreCase)) return ModifierKind.Rune;
        if (evidence.Contains("Explicit", StringComparison.OrdinalIgnoreCase) || evidence.Contains("Prefix Modifier", StringComparison.OrdinalIgnoreCase) || evidence.Contains("Suffix Modifier", StringComparison.OrdinalIgnoreCase)) return ModifierKind.Explicit;
        return ModifierKind.Unknown;
    }

    private static string ValueAfterColon(string line)
    {
        int colon = line.IndexOf(':');
        return colon < 0 ? line.Trim() : line[(colon + 1)..].Trim();
    }

    private static bool IsNonModifierProperty(string line) =>
        line.StartsWith("Requirements:", StringComparison.OrdinalIgnoreCase) ||
        line.StartsWith("Requires Level ", StringComparison.OrdinalIgnoreCase) ||
        line.StartsWith("Requires:", StringComparison.OrdinalIgnoreCase) ||
        line.StartsWith("Level:", StringComparison.OrdinalIgnoreCase) ||
        line.StartsWith("Str:", StringComparison.OrdinalIgnoreCase) ||
        line.StartsWith("Dex:", StringComparison.OrdinalIgnoreCase) ||
        line.StartsWith("Int:", StringComparison.OrdinalIgnoreCase) ||
        line.StartsWith("Sockets:", StringComparison.OrdinalIgnoreCase) ||
        line.StartsWith("Armour:", StringComparison.OrdinalIgnoreCase) ||
        line.StartsWith("Evasion Rating:", StringComparison.OrdinalIgnoreCase) ||
        line.StartsWith("Energy Shield:", StringComparison.OrdinalIgnoreCase) ||
        line.StartsWith("Spirit:", StringComparison.OrdinalIgnoreCase);
}

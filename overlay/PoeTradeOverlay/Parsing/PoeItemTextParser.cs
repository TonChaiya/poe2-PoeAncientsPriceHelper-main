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
        bool corrupted = false;
        var modifiers = new List<ParsedModifier>();
        var properties = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        for (int i = firstSeparator + 1; i < lines.Length; i++)
        {
            string line = lines[i];
            if (line.Length == 0 || line == Separator) continue;
            if (line.Equals("Corrupted", StringComparison.OrdinalIgnoreCase)) { corrupted = true; continue; }
            if (line.StartsWith("Item Level:", StringComparison.OrdinalIgnoreCase))
            {
                itemLevel = NumericText.FirstInt(line);
                continue;
            }
            if (line.StartsWith("Quality:", StringComparison.OrdinalIgnoreCase))
            {
                quality = NumericText.FirstInt(line);
                continue;
            }
            if (line.StartsWith("Physical Damage:", StringComparison.OrdinalIgnoreCase))
            {
                if (NumericText.TryUnsignedRange(ValueAfterColon(line), out var min, out var max))
                    physicalAverage = (min + max) / 2m;
                continue;
            }
            if (line.StartsWith("Attacks per Second:", StringComparison.OrdinalIgnoreCase))
            {
                attacksPerSecond = NumericText.Values(ValueAfterColon(line)).FirstOrDefault();
                continue;
            }
            if (IsNonModifierProperty(line)) continue;

            var kind = ModifierKind.Explicit;
            if (line.EndsWith("(implicit)", StringComparison.OrdinalIgnoreCase)) kind = ModifierKind.Implicit;
            else if (line.EndsWith("(enchant)", StringComparison.OrdinalIgnoreCase)) kind = ModifierKind.Enchant;
            else if (line.EndsWith("(rune)", StringComparison.OrdinalIgnoreCase)) kind = ModifierKind.Rune;
            else if (line.Any(ch => ch > 127)) kind = ModifierKind.Unknown;
            modifiers.Add(new ParsedModifier(line, kind, NumericText.Values(line)));
        }

        if (physicalAverage is { } average && attacksPerSecond is > 0)
            properties["physical_dps"] = decimal.Round(average * attacksPerSecond.Value, 2);
        if (attacksPerSecond is > 0) properties["attacks_per_second"] = attacksPerSecond.Value;

        item = new ParsedItem(rarity, name, baseType, itemClass, itemLevel, quality, corrupted,
            modifiers, properties);
        return true;
    }

    private static string ValueAfterColon(string line)
    {
        int colon = line.IndexOf(':');
        return colon < 0 ? line.Trim() : line[(colon + 1)..].Trim();
    }

    private static bool IsNonModifierProperty(string line) =>
        line.StartsWith("Requirements:", StringComparison.OrdinalIgnoreCase) ||
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

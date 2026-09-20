namespace PoeTradeOverlay.Models;

public enum ItemRarity { Normal, Magic, Rare, Unique }

public enum ModifierKind { Implicit, Explicit, Enchant, Rune, Unknown }

public sealed record ParsedModifier(
    string Text,
    ModifierKind Kind,
    IReadOnlyList<decimal> Values);

public sealed record ParsedItem(
    ItemRarity Rarity,
    string Name,
    string BaseType,
    string ItemClass,
    int? ItemLevel,
    int? Quality,
    bool Corrupted,
    IReadOnlyList<ParsedModifier> Modifiers,
    IReadOnlyDictionary<string, decimal> Properties);

namespace PoeTradeOverlay.Models;

public enum ItemRarity { Normal, Magic, Rare, Unique }

public enum ModifierKind
{
    Pseudo, Explicit, Implicit, Fractured, Crafted, Enchant, Rune,
    Desecrated, Sanctum, Skill, Unknown
}

public sealed record ItemRequirements(int? Level, int? Strength, int? Dexterity, int? Intelligence);
public sealed record ItemStates(bool Identified, bool Corrupted, bool Mirrored,
    bool Fractured, bool Crafted, bool Desecrated);
public sealed record NumericRoll(decimal Current, decimal? RangeMin, decimal? RangeMax);
public sealed record ParsedProperty(string Key, string SourceText, IReadOnlyList<decimal> Values);

public sealed record ParsedModifier(
    string Text,
    ModifierKind Kind,
    IReadOnlyList<decimal> Values,
    string? Header = null,
    IReadOnlyList<NumericRoll>? ParsedRolls = null)
{
    public IReadOnlyList<NumericRoll> Rolls => ParsedRolls ?? [];
}

public sealed record ParsedModifierBlock(string Text, string? Header,
    ModifierKind DeclaredKind, IReadOnlyList<decimal> Values, IReadOnlyList<NumericRoll> Rolls);

public sealed record ParsedItem(
    ItemRarity Rarity,
    string Name,
    string BaseType,
    string ItemClass,
    int? ItemLevel,
    int? Quality,
    bool Corrupted,
    IReadOnlyList<ParsedModifier> Modifiers,
    IReadOnlyDictionary<string, decimal> Properties,
    ItemRequirements? ParsedRequirements = null,
    ItemStates? ParsedStates = null,
    IReadOnlyList<ParsedProperty>? ParsedProperties = null)
{
    public ItemRequirements Requirements => ParsedRequirements ?? new(null, null, null, null);
    public ItemStates States => ParsedStates ?? new(true, Corrupted, false, false, false, false);
    public IReadOnlyList<ParsedProperty> PropertyBlocks => ParsedProperties ?? [];
    public IReadOnlyList<ParsedModifierBlock> ModifierBlocks => Modifiers.Select(x =>
        new ParsedModifierBlock(x.Text, x.Header, x.Kind, x.Values, x.Rolls)).ToArray();
}

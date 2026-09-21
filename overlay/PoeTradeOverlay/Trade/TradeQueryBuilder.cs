using PoeTradeOverlay.Models;

namespace PoeTradeOverlay.Trade;

public static class TradeQueryBuilder
{
    private static readonly Dictionary<string, string> Categories = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Bows"] = "weapon.bow",
        ["Crossbows"] = "weapon.crossbow",
        ["Staves"] = "weapon.staff",
        ["Quarterstaves"] = "weapon.warstaff",
        ["Wands"] = "weapon.wand",
        ["Sceptres"] = "weapon.sceptre",
        ["One Hand Swords"] = "weapon.onesword",
        ["Two Hand Swords"] = "weapon.twosword",
        ["One Hand Axes"] = "weapon.oneaxe",
        ["Two Hand Axes"] = "weapon.twoaxe",
        ["One Hand Maces"] = "weapon.onemace",
        ["Two Hand Maces"] = "weapon.twomace",
        ["Daggers"] = "weapon.dagger",
        ["Flails"] = "weapon.flail",
        ["Spears"] = "weapon.spear",
        ["Shields"] = "armour.shield",
        ["Foci"] = "armour.focus",
        ["Helmets"] = "armour.helmet",
        ["Body Armours"] = "armour.chest",
        ["Gloves"] = "armour.gloves",
        ["Boots"] = "armour.boots",
        ["Rings"] = "accessory.ring",
        ["Amulets"] = "accessory.amulet",
        ["Belts"] = "accessory.belt",
        ["Charms"] = "flask.charm",
        ["Jewels"] = "jewel"
    };

    public static TradeQuery CreateRecommended(ParsedItem item, TradeMetadataSnapshot metadata)
        => Create(item, metadata, item.Rarity == ItemRarity.Normal ? SearchProfile.CraftingBase : SearchProfile.QuickPrice);

    public static TradeQuery Create(ParsedItem item, TradeMetadataSnapshot metadata, SearchProfile profile)
    {
        var filters = item.Modifiers.Select(modifier =>
        {
            var resolved = ModifierMatcher.Resolve(modifier, metadata);
            decimal? value = modifier.Values.Count > 0 ? modifier.Values[0] : null;
            bool supported = resolved.Status == ResolutionStatus.Resolved;
            bool enabled = supported && (profile != SearchProfile.CraftingBase || resolved.Kind == ModifierKind.Implicit);
            var range = profile == SearchProfile.Broad
                ? SearchProfileRules.Broad(new NumericRange(value, null))
                : new NumericRange(value, null);
            return new TradeFilter(modifier.Text, resolved.StatId, supported,
                enabled, range.Min, range.Max, resolved.Status, resolved.Kind);
        }).ToArray();

        Categories.TryGetValue(item.ItemClass, out var category);
        string? name = item.Rarity == ItemRarity.Unique && item.Name.Length > 0 ? item.Name : null;
        var type = new TypeFilterSet(item.ItemLevel is { } ilvl ? new(ilvl, null) : null);
        var requirements = new RequirementFilterSet(
            item.Requirements.Level is { } level ? new(null, level) : null,
            item.Requirements.Strength is { } strength ? new(null, strength) : null,
            item.Requirements.Dexterity is { } dexterity ? new(null, dexterity) : null,
            item.Requirements.Intelligence is { } intelligence ? new(null, intelligence) : null);
        var equipment = new EquipmentFilterSet(
            item.Quality is { } quality ? new(quality, null) : null,
            item.Properties.TryGetValue("physical_dps", out var pdps) ? new(pdps, null) : null,
            item.Properties.TryGetValue("attacks_per_second", out var aps) ? new(aps, null) : null);
        return new TradeQuery(name, item.BaseType, category, item.Rarity, item.Corrupted, filters,
            profile, type, requirements, equipment, new(item.Corrupted, item.States.Identified));
    }
}

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
        ["Charms"] = "accessory.charm",
        ["Jewels"] = "jewel"
    };

    public static TradeQuery CreateRecommended(ParsedItem item, TradeMetadataSnapshot metadata)
    {
        var filters = item.Modifiers.Select(modifier =>
        {
            var match = ModifierMatcher.Match(modifier, metadata);
            decimal? value = modifier.Values.Count > 0 ? modifier.Values[0] : null;
            return new TradeFilter(modifier.Text, match.StatId, match.IsSupported,
                match.IsSupported, value, null);
        }).ToArray();

        Categories.TryGetValue(item.ItemClass, out var category);
        string? name = item.Rarity == ItemRarity.Unique && item.Name.Length > 0 ? item.Name : null;
        return new TradeQuery(name, item.BaseType, category, item.Rarity, item.Corrupted, filters);
    }
}

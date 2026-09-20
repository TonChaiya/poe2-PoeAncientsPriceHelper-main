using PoeTradeOverlay.Models;

namespace PoeTradeOverlay.Parsing;

public static class ItemEligibility
{
    private static readonly HashSet<string> CommodityClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Currency", "Stackable Currency", "Skill Gems", "Support Gems", "Fragments",
        "Waystones", "Maps", "Quest Items"
    };

    public static bool IsDetailedTradeCandidate(ParsedItem item) =>
        !string.IsNullOrWhiteSpace(item.ItemClass) &&
        !string.IsNullOrWhiteSpace(item.BaseType) &&
        !CommodityClasses.Contains(item.ItemClass) &&
        (item.Modifiers.Count > 0 || item.Rarity is ItemRarity.Rare or ItemRarity.Unique);
}

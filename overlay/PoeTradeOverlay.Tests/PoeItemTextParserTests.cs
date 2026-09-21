using PoeTradeOverlay.Models;
using PoeTradeOverlay.Parsing;

namespace PoeTradeOverlay.Tests;

public sealed class PoeItemTextParserTests
{
    [Theory]
    [InlineData("rare-bow.txt", "Dualstring Bow", ItemRarity.Rare, 82)]
    [InlineData("unique-ring.txt", "Lazuli Ring", ItemRarity.Unique, 76)]
    [InlineData("charm.txt", "Dousing Charm", ItemRarity.Magic, 68)]
    public void Parses_supported_equipment(string fixture, string baseType, ItemRarity rarity, int itemLevel)
    {
        Assert.True(PoeItemTextParser.TryParse(Fixture.Read(fixture), out var item));
        Assert.Equal((baseType, rarity, itemLevel), (item.BaseType, item.Rarity, item.ItemLevel));
    }

    [Fact]
    public void Parses_properties_corruption_and_modifier_values()
    {
        Assert.True(PoeItemTextParser.TryParse(Fixture.Read("rare-bow.txt"), out var item));
        Assert.True(item.Corrupted);
        Assert.Equal(20, item.Quality);
        Assert.Equal(225m, item.Properties["physical_dps"]);
        Assert.Contains(item.Modifiers, x => x.Text == "+35% to Fire Resistance" && x.Values.SequenceEqual([35m]));
    }

    [Fact]
    public void Retains_unknown_localized_modifier_without_guessing()
    {
        var text = Fixture.Read("rare-bow.txt").Replace("+35% to Fire Resistance", "+35% ความต้านทานไฟ");
        Assert.True(PoeItemTextParser.TryParse(text, out var item));
        Assert.Contains(item.Modifiers, x => x.Text.Contains("ความต้านทานไฟ") && x.Kind == ModifierKind.Unknown);
    }

    [Fact]
    public void Requirements_and_modifier_annotations_are_not_trade_filters()
    {
        const string text = """
            Item Class: Boots
            Rarity: Rare
            Foe Pace
            Luxurious Slippers
            --------
            Item Level: 71
            --------
            Requires Level 70, 93 Int
            --------
            { Desecrated Prefix Modifier "Cheetah's" (Tier: 2) — Speed }
            30% increased Movement Speed
            +11(10-17) to maximum Energy Shield
            """;

        Assert.True(PoeItemTextParser.TryParse(text, out var item));
        Assert.DoesNotContain(item.Modifiers, x => x.Text.StartsWith("Requires", StringComparison.Ordinal));
        Assert.DoesNotContain(item.Modifiers, x => x.Text.StartsWith('{'));
        Assert.Contains(item.Modifiers, x => x.Text == "30% increased Movement Speed");
    }

    [Fact]
    public void Colon_requirement_line_is_parsed_and_never_becomes_a_modifier()
    {
        const string text = """
            Item Class: Belts
            Rarity: Normal
            Heavy Belt
            --------
            Item Level: 75
            --------
            Requires: Level 50
            --------
            { Implicit Modifier }
            Has 1(1-3) Charm Slot
            """;

        Assert.True(PoeItemTextParser.TryParse(text, out var item));
        Assert.Equal(50, item.Requirements.Level);
        Assert.DoesNotContain(item.Modifiers, x => x.Text.StartsWith("Requires", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("")]
    [InlineData("ordinary clipboard text")]
    [InlineData("Rarity: Rare\nOnly one incomplete line")]
    public void Rejects_non_item_or_incomplete_text(string text) =>
        Assert.False(PoeItemTextParser.TryParse(text, out _));
}

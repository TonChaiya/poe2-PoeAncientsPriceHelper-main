using PoeTradeOverlay.Models;
using PoeTradeOverlay.Parsing;

namespace PoeTradeOverlay.Tests;

public sealed class ItemEligibilityTests
{
    [Theory]
    [InlineData("Currency")]
    [InlineData("Stackable Currency")]
    [InlineData("Skill Gems")]
    [InlineData("Support Gems")]
    [InlineData("Fragments")]
    public void Commodities_stay_on_existing_price_path(string itemClass)
    {
        var item = new ParsedItem(ItemRarity.Normal, "", "Orb", itemClass, null, null, false, [],
            new Dictionary<string, decimal>());
        Assert.False(ItemEligibility.IsDetailedTradeCandidate(item));
    }

    [Fact]
    public void Rare_equipment_is_eligible()
    {
        Assert.True(PoeItemTextParser.TryParse(Fixture.Read("rare-bow.txt"), out var item));
        Assert.True(ItemEligibility.IsDetailedTradeCandidate(item));
    }
}

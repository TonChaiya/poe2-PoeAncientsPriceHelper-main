using PoeAncientsPriceHelper;

namespace PoeAncientsPriceHelper.Tests;

public class GroundLootTests
{
    private static PriceSnapshot Snapshot(params string[] keys)
    {
        var prices = keys.ToDictionary(x => x, _ => new PriceEntry(1m, 100m));
        return new PriceSnapshot(prices, prices.Keys.GroupBy(x => x.Length).ToDictionary(g => g.Key, g => g.ToList()));
    }

    [Theory]
    [InlineData("uncut skill gem level 20", "uncut skill gem level 20")]
    [InlineData("uncot spirit gem level 19", "uncut spirit gem level 19")]
    [InlineData("skill level 18 frost bomb", "uncut skill gem level 18")]
    public void ResolvePriceKey_PinsGemTypeAndLevel(string text, string expected)
    {
        var snap = Snapshot(expected);
        Assert.Equal(expected, GroundLootScanEngine.ResolvePriceKey(text, snap).Key);
    }

    [Fact]
    public void ResolvePriceKey_GemWithoutReadableLevel_DoesNotFuzzyGuess()
    {
        var snap = Snapshot("uncut spirit gem level 18", "uncut spirit gem level 19");
        Assert.Null(GroundLootScanEngine.ResolvePriceKey("uncut spirit gem", snap).Key);
    }

    [Theory]
    [InlineData("Exalted Orb", "Exalted Orb", 1, false)]
    [InlineData("Exalted Orb x12", "Exalted Orb", 12, true)]
    [InlineData("12x Exalted Orb", "Exalted Orb", 12, true)]
    [InlineData("Exalted Orb \u00D7 7", "Exalted Orb", 7, true)]
    [InlineData("Perfect Jeweller's Orb (3)", "Perfect Jeweller's Orb", 3, true)]
    [InlineData("Uncut Skill Gem (Level 20)", "Uncut Skill Gem (Level 20)", 1, false)]
    public void ParseGroundLabel_ExtractsOnlyTrailingStackCounts(
        string input, string expectedName, int expectedMultiplier, bool expectedExplicit)
    {
        var actual = GroundLootScanEngine.ParseGroundLabel(input);
        Assert.Equal(expectedName, actual.Name);
        Assert.Equal(expectedMultiplier, actual.Multiplier);
        Assert.Equal(expectedExplicit, actual.Explicit);
    }

    [Fact]
    public void ParseItemResponse_ConvertsPrimaryAndKeepsCheapestVariant()
    {
        const string json = """
        {
          "core": { "primary": "divine", "rates": { "exalted": 400 } },
          "lines": [
            { "name": "Test Unique", "primaryValue": 2.0 },
            { "name": "Test Unique", "primaryValue": 1.5 },
            { "name": "No Listings", "primaryValue": null }
          ]
        }
        """;

        var prices = PriceRepository.ParseItemResponse(json);
        Assert.Equal(1.5m, prices["test unique"].DivineValue);
        Assert.Equal(600m, prices["test unique"].ExaltedValue);
        Assert.False(prices["no listings"].HasMarketData);
    }

    [Fact]
    public void GroundLootOverlay_UsesAbsoluteLabelY_NotRegionOffset()
    {
        var row = new PriceRow(999, "Exalted Orb", 1m, 400m, true,
            LabelBounds: new System.Drawing.Rectangle(420, 150, 180, 32));

        int y = PriceOverlayForm.ScreenYForRow(row, new System.Drawing.Rectangle(0, 60, 1920, 1000));

        Assert.Equal(166, y);
    }

    [Fact]
    public void LegacyPanelOverlay_KeepsRegionRelativeY()
    {
        var row = new PriceRow(120, "Exalted Orb", 1m, 400m, true);
        Assert.Equal(180, PriceOverlayForm.ScreenYForRow(row,
            new System.Drawing.Rectangle(0, 60, 500, 500)));
    }
}

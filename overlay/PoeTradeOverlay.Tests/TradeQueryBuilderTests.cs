using PoeTradeOverlay.Models;
using PoeTradeOverlay.Parsing;
using PoeTradeOverlay.Trade;

namespace PoeTradeOverlay.Tests;

public sealed class TradeQueryBuilderTests
{
    [Fact]
    public void Unsupported_modifier_is_visible_but_not_supported()
    {
        Assert.True(PoeItemTextParser.TryParse(Fixture.Read("rare-bow.txt"), out var item));
        var query = TradeQueryBuilder.CreateRecommended(item, Metadata());

        Assert.Contains(query.Filters, x => !x.IsSupported && x.SourceText == "Adds 12 to 24 Physical Damage");
        Assert.Contains(query.Filters, x => x.IsSupported && x.StatId == "explicit.fire");
    }

    [Fact]
    public void Unique_query_keeps_unique_name_and_base_type()
    {
        Assert.True(PoeItemTextParser.TryParse(Fixture.Read("unique-ring.txt"), out var item));
        var query = TradeQueryBuilder.CreateRecommended(item, Metadata());
        Assert.Equal("Dream Fragments", query.Name);
        Assert.Equal("Lazuli Ring", query.BaseType);
        Assert.Equal("accessory.ring", query.Category);
    }

    private static TradeMetadataSnapshot Metadata() => new([], [
        new TradeStatDefinition("explicit.fire", "+#% to Fire Resistance", ModifierKind.Explicit),
        new TradeStatDefinition("implicit.mana", "+# to maximum Mana", ModifierKind.Implicit)
    ], [], DateTimeOffset.UtcNow);
}

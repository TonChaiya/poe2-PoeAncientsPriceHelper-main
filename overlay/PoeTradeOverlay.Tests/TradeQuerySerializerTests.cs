using System.Text.Json;
using PoeTradeOverlay.Models;
using PoeTradeOverlay.Trade;

namespace PoeTradeOverlay.Tests;

public sealed class TradeQuerySerializerTests
{
    [Fact]
    public void Disabled_and_unsupported_filters_are_not_serialized()
    {
        var query = new TradeQuery(null, "Dualstring Bow", "weapon.bow", ItemRarity.Rare, true,
        [
            new("fire", "explicit.fire", true, true, 30m, null),
            new("cold", "explicit.cold", true, false, 20m, null),
            new("unknown", null, false, false)
        ]);

        string json = TradeQuerySerializer.Serialize(query);
        using var doc = JsonDocument.Parse(json);
        var filters = doc.RootElement.GetProperty("query").GetProperty("stats")[0].GetProperty("filters");
        Assert.Single(filters.EnumerateArray());
        Assert.Equal("explicit.fire", filters[0].GetProperty("id").GetString());
        Assert.DoesNotContain("explicit.cold", json);
        Assert.DoesNotContain("unknown", json);
    }

    [Fact]
    public void Invalid_numeric_range_is_rejected_before_http()
    {
        var query = new TradeQuery(null, "Ring", "accessory.ring", ItemRarity.Rare, null,
            [new("fire", "explicit.fire", true, true, 40m, 20m)]);
        Assert.Throws<InvalidOperationException>(() => TradeQuerySerializer.Serialize(query));
    }

    [Fact]
    public void Serializes_identity_rarity_corruption_and_price_sort()
    {
        var query = new TradeQuery("Dream Fragments", "Lazuli Ring", "accessory.ring",
            ItemRarity.Unique, false, []);
        string json = TradeQuerySerializer.Serialize(query);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal("Dream Fragments", root.GetProperty("query").GetProperty("name").GetString());
        Assert.Equal("unique", root.GetProperty("query").GetProperty("filters")
            .GetProperty("type_filters").GetProperty("filters").GetProperty("rarity").GetProperty("option").GetString());
        Assert.Equal("asc", root.GetProperty("sort").GetProperty("price").GetString());
    }
}

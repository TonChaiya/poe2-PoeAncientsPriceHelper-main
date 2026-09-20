using PoeTradeOverlay.Models;
using PoeTradeOverlay.Presentation;

namespace PoeTradeOverlay.Tests;

public sealed class TradeOverlayViewModelTests
{
    [Fact]
    public void Unsupported_filter_is_disabled_and_labeled()
    {
        var vm = new FilterRowViewModel(new TradeFilter("Unknown local modifier", null, false, false));
        Assert.False(vm.CanEnable);
        Assert.False(vm.IsEnabled);
        Assert.Contains("Unsupported", vm.SupportLabel);
    }

    [Fact]
    public void Minimum_greater_than_maximum_disables_search()
    {
        var state = State(new TradeQuery(null, "Ring", "accessory.ring", ItemRarity.Rare, false,
            [new TradeFilter("Fire", "explicit.fire", true, true, 20m, 40m)]));
        var vm = new TradeOverlayViewModel();
        vm.Apply(state);
        vm.Filters[0].MinText = "50";
        vm.Filters[0].MaxText = "30";
        Assert.False(vm.CanSearch);
        Assert.Contains("Minimum", vm.Filters[0].ValidationMessage);
    }

    [Fact]
    public void Formats_market_estimate_and_confidence()
    {
        var estimate = new PriceEstimate(120, 14, 10m, 12m, 18m, 15m,
            PriceConfidence.High, "Large, closely grouped sample");
        var vm = new TradeOverlayViewModel();
        vm.Apply(State(new TradeQuery(null, "Ring", null, ItemRarity.Rare, false, [])) with
        {
            Estimate = estimate,
            IsLoading = false,
            Status = "Price estimate ready"
        });
        Assert.Contains("15", vm.MedianText);
        Assert.Contains("High", vm.ConfidenceText);
        Assert.Contains("14", vm.SampleText);
    }

    [Fact]
    public void Separates_supported_filters_and_formats_item_summary_and_listings()
    {
        var query = new TradeQuery(null, "Luxurious Slippers", "armour.boots", ItemRarity.Rare, true,
        [
            new("30% increased Movement Speed", "explicit.speed", true, true, 30m),
            new("Unknown modifier", null, false, false)
        ]);
        var state = new TradeOverlayState(1,
            new ParsedItem(ItemRarity.Rare, "Foe Pace", "Luxurious Slippers", "Boots", 71, 20, true, [],
                new Dictionary<string, decimal>()), query, false, null, null, "Ready",
            [new TradeListing("a", "seller#1234", 2m, "divine")]);
        var vm = new TradeOverlayViewModel();

        vm.Apply(state);

        Assert.Equal("RARE", vm.RarityText);
        Assert.Equal("Item Level 71", vm.ItemLevelText);
        Assert.Equal("Corrupted", vm.CorruptedText);
        Assert.Single(vm.SupportedFilters);
        Assert.Single(vm.UnsupportedFilters);
        Assert.Equal("2 divine", vm.Listings.Single().PriceText);
        Assert.Equal("seller#1234", vm.Listings.Single().Account);
    }

    private static TradeOverlayState State(TradeQuery query) => new(1,
        new ParsedItem(ItemRarity.Rare, "Storm Loop", "Ring", "Rings", 80, 20, false, [],
            new Dictionary<string, decimal>()), query, false, null, null, "Item read");
}

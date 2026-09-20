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

    private static TradeOverlayState State(TradeQuery query) => new(1,
        new ParsedItem(ItemRarity.Rare, "Storm Loop", "Ring", "Rings", 80, 20, false, [],
            new Dictionary<string, decimal>()), query, false, null, null, "Item read");
}

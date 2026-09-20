using PoeTradeOverlay.Abstractions;
using PoeTradeOverlay.Models;
using PoeTradeOverlay.Pricing;

namespace PoeTradeOverlay.Tests;

public sealed class PriceEstimatorTests
{
    [Fact]
    public void Duplicate_accounts_and_extreme_outlier_do_not_define_typical_price()
    {
        var listings = new[] { 10m, 11m, 12m, 12m, 13m, 14m, 15m, 500m }
            .Select((price, index) => new TradeListing($"id-{index}", $"account-{index}", price, "exalted"))
            .Append(new TradeListing("duplicate", "account-0", 1m, "exalted"))
            .ToArray();

        var estimate = PriceEstimator.Estimate(listings, 40, new OneToOneCurrency());

        Assert.NotNull(estimate);
        Assert.Equal(11m, estimate.LowestExalted);
        Assert.Equal(12.5m, estimate.MedianExalted);
        Assert.Equal(6, estimate.UsableListings);
        Assert.True(estimate.UsableListings < estimate.TotalMatches);
    }

    [Fact]
    public void Unknown_currency_and_unpriced_listing_are_excluded()
    {
        TradeListing[] listings =
        [
            new("a", "one", null, null),
            new("b", "two", 5m, "made-up"),
            new("c", "three", 7m, "exalted")
        ];
        var estimate = PriceEstimator.Estimate(listings, 3, new OneToOneCurrency());
        Assert.NotNull(estimate);
        Assert.Equal(1, estimate.UsableListings);
        Assert.Equal(PriceConfidence.Low, estimate.Confidence);
    }

    [Fact]
    public void No_convertible_prices_returns_no_estimate()
    {
        Assert.Null(PriceEstimator.Estimate(
            [new TradeListing("a", "one", 5m, "unknown")], 1, new OneToOneCurrency()));
    }

    private sealed class OneToOneCurrency : ICurrencyConverter
    {
        public bool TryToExalted(string currency, decimal amount, out decimal exalted)
        {
            exalted = amount;
            return currency.Equals("exalted", StringComparison.OrdinalIgnoreCase);
        }
    }
}

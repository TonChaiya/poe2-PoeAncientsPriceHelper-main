namespace PoeAncientsPriceHelper.Tests;

public sealed class OverlayCurrencyConverterTests
{
    [Fact]
    public void Exalted_is_identity_and_divine_uses_current_snapshot()
    {
        var prices = new Dictionary<string, PriceEntry>
        {
            ["divineorb"] = new PriceEntry(1m, 142m)
        };
        var converter = new OverlayCurrencyConverter(() => prices);

        Assert.True(converter.TryToExalted("exalted", 3m, out var exalted));
        Assert.Equal(3m, exalted);
        Assert.True(converter.TryToExalted("divine", 2m, out var divine));
        Assert.Equal(284m, divine);
    }

    [Fact]
    public void Missing_currency_rate_returns_false_without_fabricated_value()
    {
        var converter = new OverlayCurrencyConverter(() => new Dictionary<string, PriceEntry>());
        Assert.False(converter.TryToExalted("chaos", 20m, out var value));
        Assert.Equal(0m, value);
    }

    [Fact]
    public void Vaal_trade_code_uses_live_vall_orb_rate()
    {
        var converter = new OverlayCurrencyConverter(() => new Dictionary<string, PriceEntry>
        {
            ["vaalorb"] = new PriceEntry(0m, 6.309554m)
        });
        var conversion = converter.Convert("vaal", 4m);
        Assert.Equal("Vaal Orb", conversion.DisplayName);
        Assert.Equal(25.238216m, conversion.ExaltedValue);
    }
}

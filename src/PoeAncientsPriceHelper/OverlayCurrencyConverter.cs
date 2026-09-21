using PoeTradeOverlay.Abstractions;
using PoeTradeOverlay.Models;
using PoeTradeOverlay.Pricing;

namespace PoeAncientsPriceHelper;

internal sealed class OverlayCurrencyConverter(Func<IReadOnlyDictionary<string, PriceEntry>> prices) : ICurrencyConverter
{
    public bool TryToExalted(string currency, decimal amount, out decimal exalted)
    {
        var result = Convert(currency, amount);
        exalted = result.ExaltedValue ?? 0m;
        return result.ExaltedValue is not null;
    }

    public CurrencyConversion Convert(string currency, decimal amount)
    {
        if (amount <= 0 || string.IsNullOrWhiteSpace(currency))
            return new(currency, currency, amount, null, CurrencyRateSource.Unavailable);
        string display = CurrencyCatalog.DisplayName(currency);
        string key = CurrencyCatalog.Normalize(display);
        if (key == CurrencyCatalog.Normalize("Exalted Orb"))
            return new(currency, display, amount, amount, CurrencyRateSource.Identity);
        if (!prices().TryGetValue(key, out var entry) || !entry.HasMarketData || entry.ExaltedValue <= 0)
            return new(currency, display, amount, null, CurrencyRateSource.Unavailable);
        return new(currency, display, amount, amount * entry.ExaltedValue, CurrencyRateSource.LiveEconomy);
    }
}

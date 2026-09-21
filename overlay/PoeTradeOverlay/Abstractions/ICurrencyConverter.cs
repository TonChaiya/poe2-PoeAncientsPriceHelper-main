using PoeTradeOverlay.Models;

namespace PoeTradeOverlay.Abstractions;

public interface ICurrencyConverter
{
    bool TryToExalted(string currency, decimal amount, out decimal exalted);

    CurrencyConversion Convert(string currency, decimal amount)
    {
        bool converted = TryToExalted(currency, amount, out var exalted);
        return new(currency, currency, amount, converted ? exalted : null,
            currency.Equals("exalted", StringComparison.OrdinalIgnoreCase)
                ? CurrencyRateSource.Identity
                : converted ? CurrencyRateSource.LiveEconomy : CurrencyRateSource.Unavailable);
    }
}

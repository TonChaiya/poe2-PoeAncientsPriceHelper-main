namespace PoeTradeOverlay.Abstractions;

public interface ICurrencyConverter
{
    bool TryToExalted(string currency, decimal amount, out decimal exalted);
}

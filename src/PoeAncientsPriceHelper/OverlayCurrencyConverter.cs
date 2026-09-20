using PoeTradeOverlay.Abstractions;

namespace PoeAncientsPriceHelper;

internal sealed class OverlayCurrencyConverter(Func<IReadOnlyDictionary<string, PriceEntry>> prices) : ICurrencyConverter
{
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["divine"] = "divine orb",
        ["div"] = "divine orb",
        ["chaos"] = "chaos orb",
        ["alch"] = "orb of alchemy",
        ["regal"] = "regal orb"
    };

    public bool TryToExalted(string currency, decimal amount, out decimal exalted)
    {
        exalted = 0;
        if (amount <= 0 || string.IsNullOrWhiteSpace(currency)) return false;
        if (currency.Equals("exalted", StringComparison.OrdinalIgnoreCase) ||
            currency.Equals("exa", StringComparison.OrdinalIgnoreCase) ||
            currency.Equals("exalted orb", StringComparison.OrdinalIgnoreCase))
        {
            exalted = amount;
            return true;
        }

        string display = Aliases.TryGetValue(currency.Trim(), out var alias) ? alias : currency.Trim();
        string key = Normalize(display);
        if (!prices().TryGetValue(key, out var entry) || !entry.HasMarketData || entry.ExaltedValue <= 0)
            return false;
        exalted = amount * entry.ExaltedValue;
        return true;
    }

    private static string Normalize(string value) => new(value.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
}

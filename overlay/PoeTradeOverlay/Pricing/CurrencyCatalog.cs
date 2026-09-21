using PoeTradeOverlay.Abstractions;
using PoeTradeOverlay.Models;

namespace PoeTradeOverlay.Pricing;

public sealed class CurrencyCatalog : ICurrencyConverter
{
    private static readonly IReadOnlyDictionary<string, string> Codes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["exalted"] = "Exalted Orb", ["exa"] = "Exalted Orb", ["exalted orb"] = "Exalted Orb",
            ["divine"] = "Divine Orb", ["div"] = "Divine Orb", ["divine orb"] = "Divine Orb",
            ["vaal"] = "Vaal Orb", ["vaal orb"] = "Vaal Orb",
            ["chaos"] = "Chaos Orb", ["chaos orb"] = "Chaos Orb",
            ["regal"] = "Regal Orb", ["regal orb"] = "Regal Orb",
            ["alchemy"] = "Orb of Alchemy", ["alch"] = "Orb of Alchemy", ["orb of alchemy"] = "Orb of Alchemy",
            ["chance"] = "Orb of Chance", ["orb of chance"] = "Orb of Chance",
            ["annul"] = "Orb of Annulment", ["orb of annulment"] = "Orb of Annulment",
            ["mirror"] = "Mirror of Kalandra", ["mirror of kalandra"] = "Mirror of Kalandra"
        };

    private readonly IReadOnlyDictionary<string, decimal> _rates;
    private CurrencyCatalog(IReadOnlyDictionary<string, decimal> rates) => _rates = rates;

    public static CurrencyCatalog Create(IReadOnlyDictionary<string, decimal> exaltedRates)
    {
        var normalized = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, rate) in exaltedRates)
            if (rate > 0) normalized[Normalize(name)] = rate;
        normalized[Normalize("Exalted Orb")] = 1m;
        return new(normalized);
    }

    public CurrencyConversion Convert(string currency, decimal amount)
    {
        string trimmed = currency.Trim();
        string display = Codes.TryGetValue(trimmed, out var known) ? known : trimmed;
        if (amount <= 0 || !_rates.TryGetValue(Normalize(display), out var rate))
            return new(currency, display, amount, null, CurrencyRateSource.Unavailable);
        return new(currency, display, amount, amount * rate,
            Normalize(display) == Normalize("Exalted Orb") ? CurrencyRateSource.Identity : CurrencyRateSource.LiveEconomy);
    }

    public bool TryToExalted(string currency, decimal amount, out decimal exalted)
    {
        var result = Convert(currency, amount);
        exalted = result.ExaltedValue ?? 0m;
        return result.ExaltedValue is not null;
    }

    public static string DisplayName(string currency) =>
        Codes.TryGetValue(currency.Trim(), out var display) ? display : currency.Trim();

    public static string Normalize(string value) =>
        new(value.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
}

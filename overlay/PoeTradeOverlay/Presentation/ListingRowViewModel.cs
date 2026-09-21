using PoeTradeOverlay.Models;

namespace PoeTradeOverlay.Presentation;

public sealed class ListingRowViewModel
{
    public ListingRowViewModel(TradeListing listing, CurrencyConversion? conversion = null)
    {
        Account = string.IsNullOrWhiteSpace(listing.Account) ? "Unknown seller" : listing.Account;
        PriceText = listing.Amount is { } amount && !string.IsNullOrWhiteSpace(listing.Currency)
            ? $"{amount:0.##} {listing.Currency}"
            : "Unpriced";
        EquivalentText = conversion?.ExaltedValue is { } exalted &&
                         !conversion.OriginalCurrency.Equals("exalted", StringComparison.OrdinalIgnoreCase)
            ? $"≈ {exalted:0.##} exalted" : "";
    }

    public string PriceText { get; }
    public string Account { get; }
    public string EquivalentText { get; }
}

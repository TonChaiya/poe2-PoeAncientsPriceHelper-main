using PoeTradeOverlay.Models;

namespace PoeTradeOverlay.Presentation;

public sealed class ListingRowViewModel
{
    public ListingRowViewModel(TradeListing listing, CurrencyConversion? conversion = null,
        string? league = null, string? searchId = null)
    {
        Account = string.IsNullOrWhiteSpace(listing.Account) ? "Unknown seller" : listing.Account;
        PriceText = listing.Amount is { } amount && !string.IsNullOrWhiteSpace(listing.Currency)
            ? $"{amount:0.##} {listing.Currency}"
            : "Unpriced";
        EquivalentText = conversion?.ExaltedValue is { } exalted &&
                         !conversion.OriginalCurrency.Equals("exalted", StringComparison.OrdinalIgnoreCase)
            ? $"≈ {exalted:0.##} exalted" : "";
        OpenUrl = TradeListingLink.Build(league, searchId);
    }

    public string PriceText { get; }
    public string Account { get; }
    public string EquivalentText { get; }
    public string? OpenUrl { get; }
    public bool CanOpen => OpenUrl is not null;
}

internal static class TradeListingLink
{
    internal static string? Build(string? league, string? searchId)
    {
        if (string.IsNullOrWhiteSpace(league) || string.IsNullOrWhiteSpace(searchId)) return null;
        return "https://www.pathofexile.com/trade2/search/poe2/" + Uri.EscapeDataString(league) + "/" +
               Uri.EscapeDataString(searchId);
    }
}

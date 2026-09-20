using PoeTradeOverlay.Models;

namespace PoeTradeOverlay.Presentation;

public sealed class ListingRowViewModel
{
    public ListingRowViewModel(TradeListing listing)
    {
        Account = string.IsNullOrWhiteSpace(listing.Account) ? "Unknown seller" : listing.Account;
        PriceText = listing.Amount is { } amount && !string.IsNullOrWhiteSpace(listing.Currency)
            ? $"{amount:0.##} {listing.Currency}"
            : "Unpriced";
    }

    public string PriceText { get; }
    public string Account { get; }
}

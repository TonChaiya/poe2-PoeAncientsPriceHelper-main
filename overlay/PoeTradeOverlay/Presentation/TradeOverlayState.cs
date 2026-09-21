using PoeTradeOverlay.Models;

namespace PoeTradeOverlay.Presentation;

public sealed record TradeOverlayState(
    long Generation,
    ParsedItem Item,
    TradeQuery? Query,
    bool IsLoading,
    PriceEstimate? Estimate,
    TradeFailure? Failure,
    string Status,
    IReadOnlyList<TradeListing>? Listings = null,
    IReadOnlyList<CurrencyConversion>? ListingConversions = null,
    string? SearchId = null,
    string? League = null);

namespace PoeTradeOverlay.Models;

public sealed record TradeFilter(
    string SourceText,
    string? StatId,
    bool IsSupported,
    bool IsEnabled,
    decimal? Min = null,
    decimal? Max = null);

public sealed record TradeQuery(
    string? Name,
    string BaseType,
    string? Category,
    ItemRarity Rarity,
    bool? Corrupted,
    IReadOnlyList<TradeFilter> Filters);

public sealed record TradeListing(
    string Id,
    string Account,
    decimal? Amount,
    string? Currency);

public enum TradeFailureKind
{
    Network,
    Timeout,
    RateLimited,
    InvalidQuery,
    Unavailable,
    InvalidResponse
}

public sealed record TradeFailure(
    TradeFailureKind Kind,
    string Message,
    DateTimeOffset? RetryAt = null);

public sealed record TradeSearchResult(
    string? SearchId,
    int TotalMatches,
    IReadOnlyList<TradeListing> Listings,
    TradeFailure? Failure = null);

public enum PriceConfidence { Low, Medium, High }

public sealed record PriceEstimate(
    int TotalMatches,
    int UsableListings,
    decimal LowestExalted,
    decimal RangeLowExalted,
    decimal RangeHighExalted,
    decimal MedianExalted,
    PriceConfidence Confidence,
    string ConfidenceReason);

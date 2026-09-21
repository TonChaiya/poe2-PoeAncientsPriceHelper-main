namespace PoeTradeOverlay.Models;

public enum ResolutionStatus { Resolved, Unsupported, Ambiguous }
public sealed record ResolvedModifier(ResolutionStatus Status, string? StatId,
    ModifierKind Kind, string SourceText, string Explanation);
public enum SearchProfile { CraftingBase, QuickPrice, Broad }
public sealed record NumericRange(decimal? Min = null, decimal? Max = null);
public sealed record TypeFilterSet(NumericRange? ItemLevel = null);
public sealed record RequirementFilterSet(NumericRange? Level = null,
    NumericRange? Strength = null, NumericRange? Dexterity = null, NumericRange? Intelligence = null);
public sealed record EquipmentFilterSet(NumericRange? Quality = null,
    NumericRange? PhysicalDps = null, NumericRange? AttacksPerSecond = null);
public sealed record MiscFilterSet(bool? Corrupted = null, bool? Identified = null);

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
    IReadOnlyList<TradeFilter> Filters,
    SearchProfile Profile = SearchProfile.QuickPrice,
    TypeFilterSet? ParsedTypeFilters = null,
    RequirementFilterSet? ParsedRequirementFilters = null,
    EquipmentFilterSet? ParsedEquipmentFilters = null,
    MiscFilterSet? ParsedMiscFilters = null)
{
    public TypeFilterSet TypeFilters => ParsedTypeFilters ?? new();
    public RequirementFilterSet RequirementFilters => ParsedRequirementFilters ?? new();
    public EquipmentFilterSet EquipmentFilters => ParsedEquipmentFilters ?? new();
    public MiscFilterSet MiscFilters => ParsedMiscFilters ?? new(Corrupted, true);
}

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
    InvalidResponse,
    PartialFetch
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
    string ConfidenceReason,
    decimal ConversionCoverage = 1m);

public enum CurrencyRateSource { Identity, LiveEconomy, Unavailable }
public sealed record CurrencyConversion(string OriginalCurrency, string DisplayName,
    decimal OriginalAmount, decimal? ExaltedValue, CurrencyRateSource Source);

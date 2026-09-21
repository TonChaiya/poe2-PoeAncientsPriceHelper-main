using PoeTradeOverlay.Abstractions;
using PoeTradeOverlay.Models;

namespace PoeTradeOverlay.Pricing;

public static class PriceEstimator
{
    public static PriceEstimate? Estimate(
        IReadOnlyList<TradeListing> listings,
        int totalMatches,
        ICurrencyConverter converter)
    {
        var converted = new List<(string AccountKey, decimal Value)>();
        int pricedListings = 0;
        foreach (var listing in listings)
        {
            if (listing.Amount is not > 0 || string.IsNullOrWhiteSpace(listing.Currency)) continue;
            pricedListings++;
            if (!converter.TryToExalted(listing.Currency, listing.Amount.Value, out var value) || value <= 0) continue;
            string account = string.IsNullOrWhiteSpace(listing.Account) ? "listing:" + listing.Id : listing.Account;
            converted.Add((account, value));
        }

        var values = converted.GroupBy(x => x.AccountKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Min(x => x.Value)).Order().ToList();
        if (values.Count == 0) return null;

        if (values.Count >= 8)
        {
            decimal q1 = Percentile(values, 0.25m);
            decimal q3 = Percentile(values, 0.75m);
            decimal iqr = q3 - q1;
            decimal lower = q1 - 1.5m * iqr;
            decimal upper = q3 + 1.5m * iqr;
            values = values.Where(value => value >= lower && value <= upper).ToList();
            if (values.Count == 0) return null;
        }

        decimal median = Percentile(values, 0.5m);
        decimal rangeLow = Percentile(values, 0.25m);
        decimal rangeHigh = Percentile(values, 0.75m);
        var confidence = ConfidenceRules.Evaluate(values.Count, median, rangeLow, rangeHigh);
        decimal coverage = pricedListings == 0 ? 0m : decimal.Round((decimal)converted.Count / pricedListings, 2);
        if (coverage < 0.6m && confidence.Level != PriceConfidence.Low)
            confidence = new(PriceConfidence.Low, $"{confidence.Reason}; only {coverage:P0} of priced listings converted");
        return new PriceEstimate(totalMatches, values.Count, values[0], rangeLow, rangeHigh, median,
            confidence.Level, confidence.Reason, coverage);
    }

    private static decimal Percentile(IReadOnlyList<decimal> sorted, decimal percentile)
    {
        if (sorted.Count == 1) return sorted[0];
        decimal position = (sorted.Count - 1) * percentile;
        int lower = (int)decimal.Floor(position);
        int upper = (int)decimal.Ceiling(position);
        if (lower == upper) return sorted[lower];
        decimal fraction = position - lower;
        return sorted[lower] + (sorted[upper] - sorted[lower]) * fraction;
    }
}

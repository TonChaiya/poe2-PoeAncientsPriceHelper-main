using PoeTradeOverlay.Models;

namespace PoeTradeOverlay.Trade;

public static class SearchProfileRules
{
    public static NumericRange Broad(NumericRange range, bool invertedBenefit = false, bool integer = false)
    {
        decimal? min = range.Min;
        decimal? max = range.Max;
        if (invertedBenefit)
        {
            if (max is { } upper) max = Round(upper * 1.10m, integer);
            if (min is { } lower) min = Round(lower * 0.90m, integer);
        }
        else
        {
            if (min is { } lower) min = Round(lower >= 0 ? lower * 0.90m : lower * 1.10m, integer);
            if (max is { } upper) max = Round(upper >= 0 ? upper * 1.10m : upper * 0.90m, integer);
        }
        if (min is not null && max is not null && min > max) (min, max) = (max, min);
        return new(min, max);
    }

    private static decimal Round(decimal value, bool integer) => integer
        ? decimal.Round(value, 0, MidpointRounding.AwayFromZero)
        : decimal.Round(value, 4, MidpointRounding.AwayFromZero);
}

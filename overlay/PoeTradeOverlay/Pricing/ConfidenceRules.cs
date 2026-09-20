using PoeTradeOverlay.Models;

namespace PoeTradeOverlay.Pricing;

internal static class ConfidenceRules
{
    public static (PriceConfidence Level, string Reason) Evaluate(int sampleSize, decimal median, decimal low, decimal high)
    {
        decimal spread = median <= 0 ? decimal.MaxValue : (high - low) / median;
        if (sampleSize >= 12 && spread <= 0.35m)
            return (PriceConfidence.High, "Large, closely grouped sample");
        if (sampleSize >= 5 && spread <= 0.75m)
            return (PriceConfidence.Medium, "Usable sample with moderate spread");
        return (PriceConfidence.Low, sampleSize < 5 ? "Small usable sample" : "Prices vary widely");
    }
}

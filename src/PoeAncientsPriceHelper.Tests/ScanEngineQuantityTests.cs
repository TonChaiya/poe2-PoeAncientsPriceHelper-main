using PoeAncientsPriceHelper;

namespace PoeAncientsPriceHelper.Tests;

public class ScanEngineQuantityTests
{
    [Theory]
    // readMultiplier, readExplicit, priorLocked, remembered → expected
    [InlineData(3, true, 1, 1, 3)]   // explicit Nx this pass always wins
    [InlineData(1, false, 3, 1, 3)]  // OCR dropped the marker → keep the locked stack
    [InlineData(1, false, 1, 3, 3)]  // not locked yet, but memory remembers the stack
    [InlineData(1, true, 1, 3, 1)]   // explicit 1x read overrides stale memory
    [InlineData(1, false, 1, 1, 1)]  // genuine single
    public void ResolveMultiplierForDisplay_PrefersReliableStackSignal(
        int readMultiplier,
        bool readExplicit,
        int priorLocked,
        int remembered,
        int expected)
    {
        int actual = ScanEngine.ResolveMultiplierForDisplay(readMultiplier, readExplicit, priorLocked, remembered);
        Assert.Equal(expected, actual);
    }

    // Regression: two rows of the SAME item at different stack sizes ("2x" and "1x" annulment orb). When
    // OCR drops the small "Nx" markers on a later pass, the per-row stack memory must keep each row's own
    // multiplier — the 2x row's stack must NOT bleed onto the 1x row (which showed them as the same price).
    [Fact]
    public void MergeReads_SameItemDifferentStacks_DoNotCrossContaminate()
    {
        var slots = new List<ScanEngine.RowSlot>();
        var t0 = System.DateTime.UtcNow;

        // Pass 1: both rows read explicitly (exact match → lock in one pass).
        PriceRow[] pass1 =
        [
            new(100, "2x annulment orb", 1m, 0m, true, 2, "annulment orb", ExactMatch: true, MultiplierExplicit: true),
            new(140, "1x annulment orb", 1m, 0m, true, 1, "annulment orb", ExactMatch: true, MultiplierExplicit: true),
        ];
        ScanEngine.MergeReads(slots, pass1, t0);

        // Pass 2: OCR drops BOTH "Nx" markers — bare reads, no explicit multiplier.
        PriceRow[] pass2 =
        [
            new(100, "annulment orb", 1m, 0m, true, 1, "annulment orb", ExactMatch: true, MultiplierExplicit: false),
            new(140, "annulment orb", 1m, 0m, true, 1, "annulment orb", ExactMatch: true, MultiplierExplicit: false),
        ];
        var display = ScanEngine.MergeReads(slots, pass2, t0.AddMilliseconds(150));

        Assert.Equal(2, display.Single(r => r.CenterY == 100).Multiplier);   // 2x row stays 2x
        Assert.Equal(1, display.Single(r => r.CenterY == 140).Multiplier);   // 1x row must NOT inherit x2
    }
}

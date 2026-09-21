using System.Diagnostics;
using PoeTradeOverlay.Parsing;

namespace PoeTradeOverlay.Tests;

public sealed class TradeOverlayPerformanceTests
{
    [Fact]
    public void Cached_heavy_belt_parse_stays_below_interaction_budget()
    {
        string text = Fixture.Read("heavy-belt-advanced.txt");
        PoeItemTextParser.TryParse(text, out _);
        var watch = Stopwatch.StartNew();
        for (int i = 0; i < 100; i++) Assert.True(PoeItemTextParser.TryParse(text, out _));
        watch.Stop();
        Assert.True(watch.Elapsed < TimeSpan.FromMilliseconds(1500), $"100 parses took {watch.Elapsed.TotalMilliseconds:0.0} ms");
    }
}

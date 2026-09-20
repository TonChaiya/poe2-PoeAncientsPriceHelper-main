using PoeTradeOverlay.Models;
using PoeTradeOverlay.Trade;

namespace PoeTradeOverlay.Tests;

public sealed class ModifierMatcherTests
{
    [Fact]
    public void Exact_normalized_modifier_maps_to_stable_stat_id()
    {
        var snapshot = Snapshot(("explicit.fire", "+#% to Fire Resistance", ModifierKind.Explicit));
        var match = ModifierMatcher.Match(
            new ParsedModifier("+35% to Fire Resistance", ModifierKind.Explicit, [35m]), snapshot);

        Assert.True(match.IsSupported);
        Assert.Equal("explicit.fire", match.StatId);
    }

    [Fact]
    public void Ambiguous_modifier_is_not_guessed()
    {
        var snapshot = Snapshot(
            ("explicit.a", "+# to Level of all Skill Gems", ModifierKind.Unknown),
            ("explicit.b", "+# to Level of all Skill Gems", ModifierKind.Unknown));

        var match = ModifierMatcher.Match(
            new ParsedModifier("+1 to Level of all Skill Gems", ModifierKind.Unknown, [1m]), snapshot);

        Assert.False(match.IsSupported);
        Assert.Null(match.StatId);
    }

    [Fact]
    public void Different_modifier_kind_is_not_matched()
    {
        var snapshot = Snapshot(("implicit.fire", "+#% to Fire Resistance", ModifierKind.Implicit));
        var match = ModifierMatcher.Match(
            new ParsedModifier("+35% to Fire Resistance", ModifierKind.Explicit, [35m]), snapshot);
        Assert.False(match.IsSupported);
    }

    private static TradeMetadataSnapshot Snapshot(params (string Id, string Text, ModifierKind Kind)[] stats) =>
        new([], stats.Select(x => new TradeStatDefinition(x.Id, x.Text, x.Kind)).ToArray(), [], DateTimeOffset.UtcNow);
}

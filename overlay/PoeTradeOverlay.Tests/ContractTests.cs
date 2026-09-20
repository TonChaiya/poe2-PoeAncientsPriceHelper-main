using PoeTradeOverlay.Models;

namespace PoeTradeOverlay.Tests;

public sealed class ContractTests
{
    [Fact]
    public void ParsedItem_is_immutable_and_retains_modifier_kinds()
    {
        var item = new ParsedItem(ItemRarity.Rare, "Storm Song", "Dualstring Bow", "Bow",
            82, 20, false,
            [new ParsedModifier("+35% to Fire Resistance", ModifierKind.Explicit, [35m])],
            new Dictionary<string, decimal> { ["physical_dps"] = 410.5m });

        Assert.Equal("Dualstring Bow", item.BaseType);
        Assert.Equal(ModifierKind.Explicit, item.Modifiers.Single().Kind);
        Assert.Equal(410.5m, item.Properties["physical_dps"]);
    }
}

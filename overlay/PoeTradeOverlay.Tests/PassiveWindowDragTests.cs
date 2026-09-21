using System.Windows;
using PoeTradeOverlay.Presentation;

namespace PoeTradeOverlay.Tests;

public sealed class PassiveWindowDragTests
{
    [Fact]
    public void Passive_drag_calculates_no_activate_target()
    {
        var movement = PassiveWindowMovement.FromDrag(new(100, 100), new(140, 125), new(900, 80));
        Assert.Equal(new Point(940, 105), movement.Target);
        Assert.True(movement.UseNoActivate);
    }
}

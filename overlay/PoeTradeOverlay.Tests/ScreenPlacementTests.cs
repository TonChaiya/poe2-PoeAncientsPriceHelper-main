using System.Windows;
using PoeTradeOverlay.Presentation;

namespace PoeTradeOverlay.Tests;

public sealed class ScreenPlacementTests
{
    [Fact]
    public void Placement_is_clamped_and_avoids_item_when_right_edge_overlaps()
    {
        var working = new Rect(0, 0, 1920, 1080);
        var item = new Rect(1500, 400, 300, 100);
        var point = ScreenPlacement.Place(working, new Size(430, 620), item);
        var window = new Rect(point, new Size(430, 620));
        Assert.True(working.Contains(window));
        Assert.False(window.IntersectsWith(item));
        Assert.True(point.X < item.X);
    }
}

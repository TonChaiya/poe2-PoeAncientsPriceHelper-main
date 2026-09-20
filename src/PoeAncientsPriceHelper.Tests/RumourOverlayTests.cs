using System.Drawing;
using PoeAncientsPriceHelper;

namespace PoeAncientsPriceHelper.Tests;

public class RumourOverlayTests
{
    private static readonly Rectangle Screen = new(0, 0, 2560, 1440);

    [Fact]
    public void PlaceLeftOfPanel_RightHalfPanel_PlacesLeft()
    {
        var panel = new Rectangle(2000, 300, 400, 200);   // right half
        Assert.True(RumourOverlayLayout.PlaceLeftOfPanel(panel, Screen));
    }

    [Fact]
    public void PlaceLeftOfPanel_LeftHalfPanel_PlacesRight()
    {
        var panel = new Rectangle(200, 300, 400, 200);     // left half
        Assert.False(RumourOverlayLayout.PlaceLeftOfPanel(panel, Screen));
    }

    [Fact]
    public void Position_RightPanel_SitsToTheLeftOfPanel()
    {
        var panel = new Rectangle(2000, 300, 400, 200);
        var size = new Size(300, 120);
        var pos = RumourOverlayLayout.Position(panel, size, Screen, 16);
        Assert.Equal(2000 - 16 - 300, pos.X);   // right edge butts up to the panel's left
        Assert.Equal(300, pos.Y);
    }

    [Fact]
    public void Position_LeftPanel_SitsToTheRightOfPanel()
    {
        var panel = new Rectangle(200, 300, 400, 200);
        var size = new Size(300, 120);
        var pos = RumourOverlayLayout.Position(panel, size, Screen, 16);
        Assert.Equal(200 + 400 + 16, pos.X);
        Assert.Equal(300, pos.Y);
    }

    [Fact]
    public void Position_ClampsInsideScreen()
    {
        // A panel hard against the right edge with a wide overlay would push off-screen — clamp it.
        var panel = new Rectangle(2500, 1400, 60, 40);
        var size = new Size(500, 200);
        var pos = RumourOverlayLayout.Position(panel, size, Screen, 16);
        Assert.True(pos.X >= Screen.Left);
        Assert.True(pos.X + size.Width <= Screen.Right);
        Assert.True(pos.Y + size.Height <= Screen.Bottom);
    }

    // Expected tier passed by name (string) so this public xUnit signature doesn't expose the internal
    // RatingTier enum (CS0051).
    [Theory]
    [InlineData("S+", "SPlus")]
    [InlineData("S+(is a gamble)", "SPlus")]
    [InlineData("A+", "APlus")]
    [InlineData("A", "A")]
    [InlineData("B (see notes)", "B")]
    [InlineData("B+", "BPlus")]
    [InlineData("C", "C")]
    [InlineData("D", "D")]
    [InlineData("F(see notes)", "F")]
    [InlineData("?", "Unknown")]
    [InlineData("", "Unknown")]
    [InlineData(null, "Unknown")]
    public void RatingTier_ParsesLeadingToken(string? rating, string expected)
    {
        Assert.Equal(expected, RumourRating.Tier(rating).ToString());
    }
}

using System.Windows;

namespace PoeTradeOverlay.Presentation;

public static class ScreenPlacement
{
    private const double Margin = 14;

    public static Point Place(Rect workingArea, Size windowSize, Rect avoid)
    {
        double rightX = workingArea.Right - windowSize.Width - Margin;
        var right = new Rect(new Point(rightX, Clamp(avoid.Top, workingArea.Top + Margin,
            workingArea.Bottom - windowSize.Height - Margin)), windowSize);
        if (!right.IntersectsWith(avoid)) return right.TopLeft;

        double leftX = workingArea.Left + Margin;
        var left = new Rect(new Point(leftX, Clamp(avoid.Top, workingArea.Top + Margin,
            workingArea.Bottom - windowSize.Height - Margin)), windowSize);
        if (!left.IntersectsWith(avoid)) return left.TopLeft;

        return new Point(rightX, workingArea.Top + Margin);
    }

    private static double Clamp(double value, double min, double max) => Math.Max(min, Math.Min(value, max));
}

internal sealed record PassiveWindowMovement(Point Target, bool UseNoActivate)
{
    internal static PassiveWindowMovement FromDrag(Point cursorStart, Point cursorNow, Point windowStart) =>
        new(new Point(windowStart.X + cursorNow.X - cursorStart.X,
            windowStart.Y + cursorNow.Y - cursorStart.Y), true);
}

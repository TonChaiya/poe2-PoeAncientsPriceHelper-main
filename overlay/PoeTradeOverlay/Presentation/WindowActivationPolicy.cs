namespace PoeTradeOverlay.Presentation;

internal static class WindowActivationPolicy
{
    internal const long NoActivate = 0x08000000L;

    internal static long MakePassive(long extendedStyle) => extendedStyle | NoActivate;

    internal static long MakeInteractive(long extendedStyle) => extendedStyle & ~NoActivate;
}

internal sealed class WindowInteractionMode
{
    internal bool IsEditing { get; private set; }
    internal void ToggleEditing() => IsEditing = !IsEditing;
    internal void BeginEditing() => IsEditing = true;
    internal void EndEditing() => IsEditing = false;
    internal void Reset() => EndEditing();
}

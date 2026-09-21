using PoeTradeOverlay.Presentation;

namespace PoeTradeOverlay.Tests;

public sealed class WindowActivationPolicyTests
{
    [Fact]
    public void Passive_overlay_adds_no_activate_without_removing_existing_flags()
    {
        const long layeredAndToolWindow = 0x00080080;
        long style = WindowActivationPolicy.MakePassive(layeredAndToolWindow);

        Assert.Equal(layeredAndToolWindow, style & layeredAndToolWindow);
        Assert.NotEqual(0, style & WindowActivationPolicy.NoActivate);
    }

    [Fact]
    public void Edit_mode_removes_only_no_activate()
    {
        const long original = 0x08080080;
        long style = WindowActivationPolicy.MakeInteractive(original);

        Assert.Equal(0, style & WindowActivationPolicy.NoActivate);
        Assert.Equal(0x00080080, style);
    }

    [Fact]
    public void Closing_after_editing_resets_the_next_show_to_passive_mode()
    {
        var mode = new WindowInteractionMode();
        mode.ToggleEditing();
        Assert.True(mode.IsEditing);

        mode.Reset();

        Assert.False(mode.IsEditing);
    }

    [Fact]
    public void Interaction_mode_has_idempotent_begin_and_end_operations()
    {
        var mode = new WindowInteractionMode();
        mode.BeginEditing();
        mode.BeginEditing();
        Assert.True(mode.IsEditing);
        mode.EndEditing();
        mode.EndEditing();
        Assert.False(mode.IsEditing);
    }
}

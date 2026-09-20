using SharpHook.Data;

namespace PoeAncientsPriceHelper.Tests;

public sealed class TradeOverlayHostTests
{
    [Theory]
    [InlineData(KeyCode.VcC, Modifiers.Ctrl, true)]
    [InlineData(KeyCode.VcC, Modifiers.None, false)]
    [InlineData(KeyCode.VcC, Modifiers.Ctrl | Modifiers.Shift, false)]
    [InlineData(KeyCode.VcV, Modifiers.Ctrl, false)]
    public void Only_exact_ctrl_c_is_the_manual_copy_trigger(KeyCode key, Modifiers modifiers, bool expected) =>
        Assert.Equal(expected, TradeOverlayHost.IsManualCopyChord(new Chord(key, modifiers)));

    [Fact]
    public async Task Disabled_or_background_host_does_not_read_clipboard()
    {
        int copies = 0;
        var config = new AppConfig { DetailedTradeOverlayEnabled = false };
        await using var host = new TradeOverlayHost(config, () => true,
            () => { copies++; return Task.CompletedTask; }, _ => { }, () => ValueTask.CompletedTask);
        await host.OnManualCopyAsync();
        config.DetailedTradeOverlayEnabled = true;
        host.ForegroundProbe = () => false;
        await host.OnManualCopyAsync();
        Assert.Equal(0, copies);
    }

    [Fact]
    public async Task Enabled_focused_host_forwards_copy_and_league_once()
    {
        int copies = 0;
        string? league = null;
        await using var host = new TradeOverlayHost(new AppConfig(), () => true,
            () => { copies++; return Task.CompletedTask; }, value => league = value,
            () => ValueTask.CompletedTask);
        await host.OnManualCopyAsync();
        host.SetLeague("Future League");
        Assert.Equal(1, copies);
        Assert.Equal("Future League", league);
    }
}

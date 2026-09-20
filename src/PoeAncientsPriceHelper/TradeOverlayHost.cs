namespace PoeAncientsPriceHelper;

internal sealed class TradeOverlayHost : IAsyncDisposable
{
    private readonly AppConfig _config;
    private readonly Func<Task> _copy;
    private readonly Action<string> _setLeague;
    private readonly Func<ValueTask> _dispose;

    internal Func<bool> ForegroundProbe { get; set; }

    internal TradeOverlayHost(
        AppConfig config,
        Func<bool> foregroundProbe,
        Func<Task> copy,
        Action<string> setLeague,
        Func<ValueTask> dispose)
    {
        _config = config;
        ForegroundProbe = foregroundProbe;
        _copy = copy;
        _setLeague = setLeague;
        _dispose = dispose;
    }

    internal static bool IsManualCopyChord(Chord chord) =>
        chord.Key == SharpHook.Data.KeyCode.VcC && chord.Modifiers == Modifiers.Ctrl;

    internal Task OnManualCopyAsync() =>
        _config.DetailedTradeOverlayEnabled && ForegroundProbe() ? _copy() : Task.CompletedTask;

    internal void SetLeague(string league) => _setLeague(league);

    public ValueTask DisposeAsync() => _dispose();
}

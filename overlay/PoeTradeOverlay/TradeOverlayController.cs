using PoeTradeOverlay.Abstractions;
using PoeTradeOverlay.Clipboard;
using PoeTradeOverlay.Models;
using PoeTradeOverlay.Parsing;
using PoeTradeOverlay.Presentation;
using PoeTradeOverlay.Pricing;
using PoeTradeOverlay.Trade;

namespace PoeTradeOverlay;

public sealed class TradeOverlayController : IAsyncDisposable
{
    private readonly IClipboardReader _clipboard;
    private readonly ITradeMetadataProvider _metadata;
    private readonly ITradeClient _trade;
    private readonly ICurrencyConverter _currency;
    private readonly ITradeOverlayView _view;
    private readonly object _gate = new();
    private CancellationTokenSource? _active;
    private string _league;
    private string? _lastFingerprint;
    private long _generation;
    private ParsedItem? _currentItem;

    public TradeOverlayController(
        IClipboardReader clipboard,
        ITradeMetadataProvider metadata,
        ITradeClient trade,
        ICurrencyConverter currency,
        ITradeOverlayView view,
        string league)
    {
        _clipboard = clipboard;
        _metadata = metadata;
        _trade = trade;
        _currency = currency;
        _view = view;
        _league = league;
    }

    public async Task OnManualCopyAsync()
    {
        long generation;
        CancellationToken token;
        lock (_gate)
        {
            _active?.Cancel();
            _active?.Dispose();
            _active = new CancellationTokenSource();
            token = _active.Token;
            generation = ++_generation;
        }

        try
        {
            string? text = await _clipboard.ReadNewTextAsync(_lastFingerprint, token);
            if (text is null || !PoeItemTextParser.TryParse(text, out var item) ||
                !ItemEligibility.IsDetailedTradeCandidate(item)) return;
            _lastFingerprint = WpfClipboardReader.Fingerprint(text);
            _currentItem = item;

            var metadata = await _metadata.GetAsync(token);
            var query = TradeQueryBuilder.CreateRecommended(item, metadata);
            if (!IsCurrent(generation, token)) return;
            _view.Publish(new TradeOverlayState(generation, item, query, true, null, null, "Item read — searching price"));
            await SearchAndPublishAsync(generation, item, query, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }

    public async Task SearchEditedAsync(TradeQuery query)
    {
        ParsedItem? item = _currentItem;
        if (item is null) return;
        long generation;
        CancellationToken token;
        lock (_gate)
        {
            _active?.Cancel();
            _active?.Dispose();
            _active = new CancellationTokenSource();
            token = _active.Token;
            generation = ++_generation;
        }
        _view.Publish(new TradeOverlayState(generation, item, query, true, null, null, "Searching price"));
        try { await SearchAndPublishAsync(generation, item, query, token); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }

    public void SetLeague(string league)
    {
        lock (_gate)
        {
            _league = league;
            _active?.Cancel();
            _generation++;
        }
    }

    private async Task SearchAndPublishAsync(long generation, ParsedItem item, TradeQuery query, CancellationToken token)
    {
        string league;
        lock (_gate) league = _league;
        var result = await _trade.SearchAsync(league, query, token);
        if (!IsCurrent(generation, token)) return;
        var estimate = result.Failure is null
            ? PriceEstimator.Estimate(result.Listings, result.TotalMatches, _currency)
            : null;
        string status = result.Failure?.Message ?? (estimate is null ? "No priced listings found" : "Price estimate ready");
        _view.Publish(new TradeOverlayState(generation, item, query, false, estimate, result.Failure, status));
    }

    private bool IsCurrent(long generation, CancellationToken token) =>
        !token.IsCancellationRequested && Interlocked.Read(ref _generation) == generation;

    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            _active?.Cancel();
            _active?.Dispose();
            _active = null;
            _generation++;
        }
        _view.Close();
        return ValueTask.CompletedTask;
    }
}

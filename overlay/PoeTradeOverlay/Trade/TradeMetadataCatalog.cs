using System.Net.Http;
using PoeTradeOverlay.Storage;

namespace PoeTradeOverlay.Trade;

public sealed record MetadataCacheEnvelope(
    int SchemaVersion,
    DateTimeOffset FetchedAt,
    string ItemsJson,
    string StatsJson,
    string FiltersJson);

public sealed class TradeMetadataCatalog : ITradeMetadataProvider
{
    private static readonly TimeSpan MaxAge = TimeSpan.FromDays(7);
    private const int MaxMetadataBytes = 8 * 1024 * 1024;
    private readonly HttpClient _http;
    private readonly string _cachePath;
    private readonly TimeProvider _clock;
    private TradeMetadataSnapshot? _memory;

    public TradeMetadataCatalog(HttpClient http, string cachePath, TimeProvider? clock = null)
    {
        _http = http;
        _cachePath = cachePath;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<TradeMetadataSnapshot> GetAsync(CancellationToken cancellationToken)
    {
        if (_memory is not null && _clock.GetUtcNow() - _memory.FetchedAt <= MaxAge) return _memory;

        MetadataCacheEnvelope? cached = null;
        try { cached = await AtomicJsonCache.ReadAsync<MetadataCacheEnvelope>(_cachePath, cancellationToken); }
        catch (Exception) when (!cancellationToken.IsCancellationRequested) { }

        if (cached is not null && cached.SchemaVersion == 1 && _clock.GetUtcNow() - cached.FetchedAt <= MaxAge)
            return _memory = TradeMetadataParser.Parse(cached);

        try
        {
            var refreshed = await DownloadAsync(cancellationToken);
            var parsed = TradeMetadataParser.Parse(refreshed);
            await AtomicJsonCache.WriteAsync(_cachePath, refreshed, cancellationToken);
            return _memory = parsed;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested && cached is not null)
        {
            return _memory = TradeMetadataParser.Parse(cached);
        }
    }

    private async Task<MetadataCacheEnvelope> DownloadAsync(CancellationToken cancellationToken)
    {
        async Task<string> Get(string suffix)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get,
                "https://www.pathofexile.com/api/trade2/data/" + suffix);
            request.Headers.TryAddWithoutValidation("User-Agent",
                "Poe2GroundLootPriceHelper/1.2.0 (contact: https://github.com/TonChaiya/poe2-PoeAncientsPriceHelper-main)");
            request.Headers.Referrer = new Uri("https://www.pathofexile.com/trade2/search/poe2");
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            response.EnsureSuccessStatusCode();
            return await BoundedHttpContent.ReadStringAsync(response.Content, MaxMetadataBytes, cancellationToken);
        }

        var items = Get("items");
        var stats = Get("stats");
        var filters = Get("filters");
        await Task.WhenAll(items, stats, filters);
        return new MetadataCacheEnvelope(1, _clock.GetUtcNow(), items.Result, stats.Result, filters.Result);
    }
}

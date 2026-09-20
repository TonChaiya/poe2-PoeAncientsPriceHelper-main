using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using PoeTradeOverlay.Abstractions;
using PoeTradeOverlay.Models;

namespace PoeTradeOverlay.Trade;

public sealed class PathOfExileTradeClient : ITradeClient
{
    private const int MaxFetchIds = 20;
    private const int MaxBodyBytes = 2 * 1024 * 1024;
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(12);
    private readonly HttpClient _http;
    private readonly TimeProvider _clock;
    private readonly RateLimitGuard _rateLimits = new();

    public PathOfExileTradeClient(HttpClient http, TimeProvider? clock = null)
    {
        _http = http;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<TradeSearchResult> SearchAsync(string league, TradeQuery query, CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow();
        if (_rateLimits.IsBlocked(now, out var retryAt))
            return Failed(TradeFailureKind.RateLimited, "Trade search is rate limited.", retryAt);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(RequestTimeout);
        try
        {
            string payload = TradeQuerySerializer.Serialize(query);
            string searchUrl = "https://www.pathofexile.com/api/trade2/search/poe2/" + Uri.EscapeDataString(league);
            using var searchRequest = CreateRequest(HttpMethod.Post, searchUrl);
            searchRequest.Content = new StringContent(payload, Encoding.UTF8, "application/json");
            using var searchResponse = await _http.SendAsync(searchRequest, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            _rateLimits.Observe(searchResponse, _clock.GetUtcNow());
            if (searchResponse.StatusCode == HttpStatusCode.TooManyRequests)
                return Failed(TradeFailureKind.RateLimited, "Trade search is rate limited.",
                    _rateLimits.BlockedUntil ?? _clock.GetUtcNow().AddSeconds(60));
            if (!searchResponse.IsSuccessStatusCode)
                return Failed(searchResponse.StatusCode == HttpStatusCode.BadRequest
                    ? TradeFailureKind.InvalidQuery : TradeFailureKind.Unavailable,
                    $"Trade search returned HTTP {(int)searchResponse.StatusCode}.");

            string searchJson = await BoundedHttpContent.ReadStringAsync(searchResponse.Content, MaxBodyBytes, timeout.Token);
            using var searchDoc = JsonDocument.Parse(searchJson);
            string? searchId = searchDoc.RootElement.TryGetProperty("id", out var idNode) ? idNode.GetString() : null;
            if (string.IsNullOrWhiteSpace(searchId) ||
                !searchDoc.RootElement.TryGetProperty("result", out var resultNode) ||
                resultNode.ValueKind != JsonValueKind.Array)
                return Failed(TradeFailureKind.InvalidResponse, "Trade search returned an invalid response.");

            int total = searchDoc.RootElement.TryGetProperty("total", out var totalNode) && totalNode.TryGetInt32(out int parsedTotal)
                ? parsedTotal : resultNode.GetArrayLength();
            var ids = resultNode.EnumerateArray().Select(x => x.GetString()).Where(x => !string.IsNullOrWhiteSpace(x))
                .Take(MaxFetchIds).Cast<string>().ToArray();
            if (ids.Length == 0) return new TradeSearchResult(searchId, total, []);

            string fetchUrl = "https://www.pathofexile.com/api/trade2/fetch/" + string.Join(',', ids) +
                              "?query=" + Uri.EscapeDataString(searchId);
            using var fetchRequest = CreateRequest(HttpMethod.Get, fetchUrl);
            using var fetchResponse = await _http.SendAsync(fetchRequest, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            _rateLimits.Observe(fetchResponse, _clock.GetUtcNow());
            if (fetchResponse.StatusCode == HttpStatusCode.TooManyRequests)
                return Failed(TradeFailureKind.RateLimited, "Trade fetch is rate limited.",
                    _rateLimits.BlockedUntil ?? _clock.GetUtcNow().AddSeconds(60));
            if (!fetchResponse.IsSuccessStatusCode)
                return Failed(TradeFailureKind.Unavailable, $"Trade fetch returned HTTP {(int)fetchResponse.StatusCode}.");

            string fetchJson = await BoundedHttpContent.ReadStringAsync(fetchResponse.Content, MaxBodyBytes, timeout.Token);
            return new TradeSearchResult(searchId, total, ParseListings(fetchJson));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failed(TradeFailureKind.Timeout, "Trade search timed out.");
        }
        catch (OperationCanceledException) { throw; }
        catch (InvalidOperationException ex)
        {
            return Failed(TradeFailureKind.InvalidQuery, ex.Message);
        }
        catch (JsonException)
        {
            return Failed(TradeFailureKind.InvalidResponse, "Trade returned malformed JSON.");
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException)
        {
            return Failed(TradeFailureKind.Network, "Trade service is unavailable.");
        }
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string url)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.TryAddWithoutValidation("User-Agent",
            "Poe2GroundLootPriceHelper/1.2.0 (contact: https://github.com/TonChaiya/poe2-PoeAncientsPriceHelper-main)");
        request.Headers.Referrer = new Uri("https://www.pathofexile.com/trade2/search/poe2");
        return request;
    }

    private static TradeListing[] ParseListings(string json)
    {
        using var doc = JsonDocument.Parse(json);
        JsonElement rows = doc.RootElement;
        if (rows.ValueKind == JsonValueKind.Object && rows.TryGetProperty("result", out var result))
            rows = result;
        if (rows.ValueKind != JsonValueKind.Array) throw new JsonException();
        var listings = new List<TradeListing>();
        foreach (var row in rows.EnumerateArray())
        {
            string id = row.TryGetProperty("id", out var idNode) ? idNode.GetString() ?? "" : "";
            if (!row.TryGetProperty("listing", out var listing)) continue;
            string account = listing.TryGetProperty("account", out var accountNode) &&
                             accountNode.TryGetProperty("name", out var nameNode)
                ? nameNode.GetString() ?? "" : "";
            decimal? amount = null;
            string? currency = null;
            if (listing.TryGetProperty("price", out var price) && price.ValueKind == JsonValueKind.Object)
            {
                if (price.TryGetProperty("amount", out var amountNode) && amountNode.TryGetDecimal(out var value)) amount = value;
                if (price.TryGetProperty("currency", out var currencyNode)) currency = currencyNode.GetString();
            }
            listings.Add(new TradeListing(id, account, amount, currency));
        }
        return listings.ToArray();
    }

    private static TradeSearchResult Failed(TradeFailureKind kind, string message, DateTimeOffset? retryAt = null) =>
        new(null, 0, [], new TradeFailure(kind, message, retryAt));
}

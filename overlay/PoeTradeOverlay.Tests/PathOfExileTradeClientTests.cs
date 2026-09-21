using System.Net;
using System.Text;
using PoeTradeOverlay.Models;
using PoeTradeOverlay.Trade;

namespace PoeTradeOverlay.Tests;

public sealed class PathOfExileTradeClientTests
{
    [Fact]
    public async Task Successful_search_posts_query_then_fetches_bounded_listings_without_credentials()
    {
        var handler = new QueueHandler(
            Json(HttpStatusCode.OK, "{\"id\":\"search-1\",\"result\":[\"a\",\"b\"],\"total\":24}"),
            Json(HttpStatusCode.OK, "[{\"id\":\"a\",\"listing\":{\"account\":{\"name\":\"seller-a\"},\"price\":{\"amount\":10,\"currency\":\"exalted\"}}}]")
        );
        var client = new PathOfExileTradeClient(new HttpClient(handler));

        var result = await client.SearchAsync("Runes of Aldur", ValidQuery(), default);

        Assert.Null(result.Failure);
        Assert.Equal(24, result.TotalMatches);
        Assert.Single(result.Listings);
        Assert.Equal(2, handler.Requests.Count);
        Assert.EndsWith("/api/trade2/search/poe2/Runes%20of%20Aldur", handler.Requests[0].Uri);
        Assert.Contains("/api/trade2/fetch/a,b?query=search-1", handler.Requests[1].Uri);
        Assert.All(handler.Requests, request =>
        {
            Assert.Null(request.Authorization);
            Assert.Null(request.Cookie);
            Assert.Contains("Poe2GroundLootPriceHelper/1.3.1", request.UserAgent);
        });
    }

    [Fact]
    public async Task Http_429_blocks_until_retry_after_without_retrying()
    {
        var response = Json(HttpStatusCode.TooManyRequests, "{}");
        response.Headers.TryAddWithoutValidation("Retry-After", "17");
        var handler = new QueueHandler(response);
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 9, 20, 12, 0, 0, TimeSpan.Zero));
        var client = new PathOfExileTradeClient(new HttpClient(handler), clock);

        var result = await client.SearchAsync("Runes of Aldur", ValidQuery(), default);
        var blocked = await client.SearchAsync("Runes of Aldur", ValidQuery(), default);

        Assert.Equal(TradeFailureKind.RateLimited, result.Failure?.Kind);
        Assert.Equal(clock.GetUtcNow().AddSeconds(17), result.Failure?.RetryAt);
        Assert.Equal(TradeFailureKind.RateLimited, blocked.Failure?.Kind);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Malformed_response_fails_without_throwing_or_fetching()
    {
        var handler = new QueueHandler(Json(HttpStatusCode.OK, "not-json"));
        var client = new PathOfExileTradeClient(new HttpClient(handler));
        var result = await client.SearchAsync("Runes of Aldur", ValidQuery(), default);
        Assert.Equal(TradeFailureKind.InvalidResponse, result.Failure?.Kind);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Fetch_result_envelope_is_parsed()
    {
        var handler = new QueueHandler(
            Json(HttpStatusCode.OK, "{\"id\":\"search-2\",\"result\":[\"a\"],\"total\":1}"),
            Json(HttpStatusCode.OK, "{\"result\":[{\"id\":\"a\",\"listing\":{\"account\":{\"name\":\"seller-a\"},\"price\":{\"amount\":7,\"currency\":\"exalted\"}}}]}"));
        var client = new PathOfExileTradeClient(new HttpClient(handler));

        var result = await client.SearchAsync("Runes of Aldur", ValidQuery(), default);

        Assert.Null(result.Failure);
        Assert.Single(result.Listings);
        Assert.Equal(7m, result.Listings[0].Amount);
    }

    private static TradeQuery ValidQuery() =>
        new(null, "Dualstring Bow", "weapon.bow", ItemRarity.Rare, false, []);

    private static HttpResponseMessage Json(HttpStatusCode status, string json) => new(status)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private sealed record Captured(string Uri, string UserAgent, string? Authorization, string? Cookie);

    private sealed class QueueHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);
        public List<Captured> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new Captured(request.RequestUri!.AbsoluteUri,
                request.Headers.UserAgent.ToString(),
                request.Headers.Authorization?.ToString(),
                request.Headers.TryGetValues("Cookie", out var cookies) ? string.Join(";", cookies) : null));
            if (request.Content is not null) _ = await request.Content.ReadAsStringAsync(cancellationToken);
            return _responses.Dequeue();
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

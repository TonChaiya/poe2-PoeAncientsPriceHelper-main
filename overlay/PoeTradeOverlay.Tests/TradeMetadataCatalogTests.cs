using System.Net;
using PoeTradeOverlay.Storage;
using PoeTradeOverlay.Trade;

namespace PoeTradeOverlay.Tests;

public sealed class TradeMetadataCatalogTests
{
    [Fact]
    public void Current_trade_item_schema_without_entry_id_is_parsed()
    {
        const string items = """
            {"result":[{"id":"accessory","label":"Accessories","entries":[
              {"type":"Crimson Amulet"},
              {"type":"Sapphire Ring","text":"Dream Fragments Sapphire Ring","name":"Dream Fragments","flags":{"unique":true}}
            ]}]}
            """;
        var snapshot = TradeMetadataParser.Parse(new MetadataCacheEnvelope(1, DateTimeOffset.UtcNow,
            items, "{\"result\":[]}", "{\"result\":[]}"));

        Assert.Collection(snapshot.Items,
            item =>
            {
                Assert.Equal("Crimson Amulet", item.Id);
                Assert.Equal("Crimson Amulet", item.Name);
                Assert.Equal("accessory", item.Category);
            },
            item =>
            {
                Assert.Equal("Dream Fragments Sapphire Ring", item.Id);
                Assert.Equal("Dream Fragments", item.Name);
                Assert.Equal("accessory", item.Category);
            });
    }

    [Fact]
    public async Task Fresh_valid_cache_is_used_without_http()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "trade-metadata.json");
        await AtomicJsonCache.WriteAsync(path, new MetadataCacheEnvelope(1, DateTimeOffset.UtcNow,
            "{\"result\":[]}", StatsJson("explicit.fire", "+#% to Fire Resistance"), "{\"result\":[]}"), default);
        var handler = new CountingHandler(_ => throw new InvalidOperationException("HTTP must not be called"));
        var catalog = new TradeMetadataCatalog(new HttpClient(handler), path);

        var snapshot = await catalog.GetAsync(default);

        Assert.Contains(snapshot.Stats, x => x.Id == "explicit.fire");
        Assert.Equal(0, handler.Count);
    }

    [Fact]
    public async Task Malformed_refresh_does_not_replace_last_known_good_cache()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "trade-metadata.json");
        var old = new MetadataCacheEnvelope(1, DateTimeOffset.UtcNow.AddDays(-8),
            "{\"result\":[]}", StatsJson("explicit.fire", "+#% to Fire Resistance"), "{\"result\":[]}");
        await AtomicJsonCache.WriteAsync(path, old, default);
        var handler = new CountingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("not-json")
        });
        var catalog = new TradeMetadataCatalog(new HttpClient(handler), path);

        var snapshot = await catalog.GetAsync(default);
        var persisted = await AtomicJsonCache.ReadAsync<MetadataCacheEnvelope>(path, default);

        Assert.Contains(snapshot.Stats, x => x.Id == "explicit.fire");
        Assert.Equal(old.StatsJson, persisted!.StatsJson);
        Assert.True(handler.Count >= 1);
    }

    [Fact]
    public async Task Metadata_requests_are_identifiable_and_do_not_send_credentials()
    {
        using var temp = new TempDirectory();
        var handler = new CountingHandler(request => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(request.RequestUri!.AbsolutePath.EndsWith("/stats", StringComparison.Ordinal)
                ? StatsJson("explicit.fire", "+#% to Fire Resistance")
                : "{\"result\":[]}")
        });
        var catalog = new TradeMetadataCatalog(new HttpClient(handler), Path.Combine(temp.Path, "metadata.json"));

        await catalog.GetAsync(default);

        Assert.Equal(3, handler.Requests.Count);
        Assert.All(handler.Requests, request =>
        {
            Assert.Contains("Poe2GroundLootPriceHelper/1.3.0", request.UserAgent);
            Assert.Null(request.Authorization);
            Assert.Null(request.Cookie);
        });
    }

    private static string StatsJson(string id, string text) =>
        $$"""{"result":[{"label":"Explicit","entries":[{"id":"{{id}}","text":"{{text}}","type":"explicit"}]}]}""";

    private sealed class CountingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int Count { get; private set; }
        public List<(string UserAgent, string? Authorization, string? Cookie)> Requests { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Count++;
            Requests.Add((request.Headers.UserAgent.ToString(), request.Headers.Authorization?.ToString(),
                request.Headers.TryGetValues("Cookie", out var cookies) ? string.Join(";", cookies) : null));
            return Task.FromResult(respond(request));
        }
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        public TempDirectory() => Directory.CreateDirectory(Path);
        public void Dispose() => Directory.Delete(Path, true);
    }
}

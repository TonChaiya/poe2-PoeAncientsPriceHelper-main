using PoeAncientsPriceHelper;

namespace PoeAncientsPriceHelper.Tests;

public class LeagueCatalogTests
{
    [Fact]
    public async Task LoadAsync_UsesLiveLeagues_DeduplicatesAndWritesCache()
    {
        using var dir = new TempDir();
        using var http = new HttpClient(new FakeHttpMessageHandler("""
            [{"id":"New League","name":"New League"},{"id":"New League","name":"Duplicate"},{"id":"HC New League","name":"HC New League"}]
            """));
        var result = await new LeagueCatalog(http, dir.Path).LoadAsync();

        Assert.Equal(LeagueCatalogSource.Live, result.Source);
        Assert.Equal(["New League", "HC New League"], result.Leagues.Select(x => x.Id));
        Assert.True(File.Exists(Path.Combine(dir.Path, "league_cache.json")));
    }

    [Fact]
    public async Task LoadAsync_UsesCacheWhenLiveRequestFails()
    {
        using var dir = new TempDir();
        File.WriteAllText(Path.Combine(dir.Path, "league_cache.json"),
            "[{\"id\":\"Cached League\",\"name\":\"Cached League\"}]");
        using var http = new HttpClient(new FailingHttpHandler());

        var result = await new LeagueCatalog(http, dir.Path).LoadAsync();

        Assert.Equal(LeagueCatalogSource.Cache, result.Source);
        Assert.Equal("Cached League", Assert.Single(result.Leagues).Id);
    }

    [Fact]
    public async Task LoadAsync_UsesBuiltInListWhenLiveAndCacheAreInvalid()
    {
        using var dir = new TempDir();
        File.WriteAllText(Path.Combine(dir.Path, "league_cache.json"), "not-json");
        using var http = new HttpClient(new FakeHttpMessageHandler("[]"));

        var result = await new LeagueCatalog(http, dir.Path).LoadAsync();

        Assert.Equal(LeagueCatalogSource.BuiltIn, result.Source);
        Assert.Contains(result.Leagues, x => x.Id == "Standard");
    }
}

using System.IO;
using System.Net.Http;
using PoeAncientsPriceHelper;

namespace PoeAncientsPriceHelper.Tests;

public class RumourRefreshTests
{
    private const string SampleCsv =
        """
        Rumor,Map Type,Mods,Rating
        ,,,
        Fallen Stars,Moor,Runestones,S+
        Unknown Ruins,Exhumed Ruins,Precursor Leylines,B (see notes)
        "Wild,.Roaming Free",Grazed Praire,Azmeri Spirits,D
        Sagas,,,
        Aldurs,,Buffs expeditions,S+(is a gamble)
        """;

    [Fact]
    public void Parse_DropsHeaderBlankAndSectionRows()
    {
        var entries = RumourCsv.Parse(SampleCsv);
        Assert.Equal(4, entries.Count);   // header, blank, and "Sagas,,," are dropped
        Assert.DoesNotContain(entries, e => e.Rumor == "Sagas");
    }

    [Fact]
    public void Parse_HandlesQuotedCommaField()
    {
        var entries = RumourCsv.Parse(SampleCsv);
        var wild = entries.Single(e => e.Rumor == "Wild,.Roaming Free");
        Assert.Equal("Grazed Praire", wild.MapType);
        Assert.Equal("Azmeri Spirits", wild.Mods);
    }

    [Fact]
    public void Parse_PreservesRatingSuffixesAndEmptyMapType()
    {
        var entries = RumourCsv.Parse(SampleCsv);
        Assert.Equal("B (see notes)", entries.Single(e => e.Rumor == "Unknown Ruins").Rating);
        var aldurs = entries.Single(e => e.Rumor == "Aldurs");
        Assert.Equal("", aldurs.MapType);
        Assert.Equal("S+(is a gamble)", aldurs.Rating);
    }

    [Fact]
    public async Task RefreshAsync_Success_ParsesCachesAndReturnsRepository()
    {
        using var dir = new TempDir();
        var cache = Path.Combine(dir.Path, "rumours_cache.json");
        using var http = new HttpClient(new FakeHttpMessageHandler(SampleCsv));

        var result = await RumourRefresher.RefreshAsync(http, cache);

        Assert.True(result.Success);
        Assert.Equal(4, result.Count);
        Assert.NotNull(result.Repository);
        Assert.Equal("Moor", result.Repository!.Resolve("Fallen Stars")!.MapType);
        Assert.True(File.Exists(cache));   // cached for next launch
    }

    [Fact]
    public async Task RefreshAsync_NetworkFailure_FallsBackWithoutThrowing()
    {
        using var dir = new TempDir();
        var cache = Path.Combine(dir.Path, "rumours_cache.json");
        using var http = new HttpClient(new FailingHttpHandler());

        var result = await RumourRefresher.RefreshAsync(http, cache);

        Assert.False(result.Success);
        Assert.Null(result.Repository);
        Assert.False(File.Exists(cache));   // nothing cached on failure
        Assert.Contains("bundled", result.Message);
    }

    [Fact]
    public async Task RefreshAsync_EmptyResponse_ReportsNoRumours()
    {
        // Header + section/blank rows only → no real entries (also models an HTML error page, whose
        // lines have too few columns to parse).
        using var http = new HttpClient(new FakeHttpMessageHandler("Rumor,Map Type,Mods,Rating\n,,,\nUnique Maps,,,"));
        var result = await RumourRefresher.RefreshAsync(http, Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
        Assert.False(result.Success);
        Assert.Equal(0, result.Count);
    }

    [Fact]
    public void Load_PrefersCacheOverBundle()
    {
        using var dir = new TempDir();
        var cache = Path.Combine(dir.Path, "rumours_cache.json");
        File.WriteAllText(cache,
            """[ { "rumor": "Cache Only", "mapType": "Nowhere", "mods": "X", "rating": "S+" } ]""");

        var repo = RumourRepository.Load(cache);
        Assert.Equal(1, repo.Count);   // the cache, not the 24-entry bundle
        Assert.Equal("Nowhere", repo.Resolve("Cache Only")!.MapType);
    }

    [Fact]
    public void Load_NoCache_FallsBackToBundle()
    {
        var missing = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var repo = RumourRepository.Load(missing);
        Assert.True(repo.Count >= 20);   // bundled snapshot
    }
}

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http;

namespace PoeAncientsPriceHelper;

internal sealed record LeagueOption(string Id, string Name)
{
    public override string ToString() => Name;
}

internal enum LeagueCatalogSource { Live, Cache, BuiltIn }
internal sealed record LeagueCatalogResult(IReadOnlyList<LeagueOption> Leagues, LeagueCatalogSource Source);

internal sealed class LeagueCatalog
{
    private const string Endpoint = "https://poe.ninja/poe2/api/economy/leagues";
    private readonly HttpClient _http;
    private readonly string _cachePath;
    private static readonly LeagueOption[] BuiltIn =
    [
        new("Forbidden Rites", "Forbidden Rites"),
        new("HC Forbidden Rites", "HC Forbidden Rites"),
        new("Standard", "Standard"),
        new("Runes of Aldur", "Runes of Aldur"),
        new("HC Runes of Aldur", "HC Runes of Aldur")
    ];

    public LeagueCatalog(HttpClient http, string? dataDir = null)
    {
        _http = http;
        _cachePath = Path.Combine(dataDir ?? AppPaths.DataDir, "league_cache.json");
    }

    public async Task<LeagueCatalogResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            using var request = new HttpRequestMessage(HttpMethod.Get, Endpoint);
            request.Headers.TryAddWithoutValidation("User-Agent", "Poe2GroundLootPriceHelper/1.0");
            using var response = await _http.SendAsync(request, timeout.Token);
            response.EnsureSuccessStatusCode();
            var leagues = Parse(await response.Content.ReadAsStringAsync(timeout.Token));
            if (leagues.Count > 0)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_cachePath)!);
                var temp = _cachePath + ".tmp";
                await File.WriteAllTextAsync(temp, JsonConvert.SerializeObject(leagues), cancellationToken);
                File.Move(temp, _cachePath, true);
                return new(leagues, LeagueCatalogSource.Live);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or IOException)
        {
            Console.Error.WriteLine($"[LeagueCatalog] live list unavailable: {ex.Message}");
        }

        try
        {
            if (File.Exists(_cachePath))
            {
                var cached = Parse(await File.ReadAllTextAsync(_cachePath, cancellationToken));
                if (cached.Count > 0) return new(cached, LeagueCatalogSource.Cache);
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            Console.Error.WriteLine($"[LeagueCatalog] cache unavailable: {ex.Message}");
        }
        return new(BuiltIn, LeagueCatalogSource.BuiltIn);
    }

    private static List<LeagueOption> Parse(string json)
    {
        var result = new List<LeagueOption>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in JArray.Parse(json))
        {
            var id = item["id"]?.Value<string>()?.Trim();
            var name = item["name"]?.Value<string>()?.Trim();
            if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(name) && seen.Add(id))
                result.Add(new(id, name));
        }
        return result;
    }
}

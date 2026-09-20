using System.Net.Http;
using Newtonsoft.Json;

namespace PoeAncientsPriceHelper;

// Outcome of a "Refresh from sheet" attempt. On success Repository holds the freshly-parsed data (to
// swap in live) and a cache was written for next launch; on failure nothing is changed and Message
// explains why (shown in Settings).
internal sealed record RumourRefreshResult(bool Success, int Count, string Message, RumourRepository? Repository = null)
{
    public static RumourRefreshResult Ok(RumourRepository repo, int count) =>
        new(true, count, $"Updated — {count} rumours loaded from the sheet.", repo);

    public static RumourRefreshResult Failed(string message) => new(false, 0, message);
}

// Pulls the community rumour sheet's CSV export on demand, parses it, and caches it as the active
// dataset. Any failure (network, redirect, empty/garbage response) leaves the existing data in place
// and reports the reason — the bundled snapshot remains the floor.
internal static class RumourRefresher
{
    // The sheet's CSV export. Google issues a cross-host 307 to googleusercontent; HttpClient follows
    // it automatically (AllowAutoRedirect defaults on).
    public const string CsvUrl =
        "https://docs.google.com/spreadsheets/d/16YU8mSS7TdLPdmOunVjiPn_NrKVGfcnMkuMQDy8jgZA/export?format=csv&gid=0";

    public static async Task<RumourRefreshResult> RefreshAsync(
        HttpClient http, string? cachePath = null, CancellationToken ct = default)
    {
        try
        {
            var csv = await http.GetStringAsync(CsvUrl, ct);
            var entries = RumourCsv.Parse(csv);
            if (entries.Count == 0)
                return RumourRefreshResult.Failed("The sheet returned no rumours — keeping current data.");

            // Persist a cache the loader prefers next launch. A cache-write failure must not lose the
            // in-memory refresh, so it's best-effort.
            try
            {
                var json = JsonConvert.SerializeObject(entries, Formatting.Indented);
                File.WriteAllText(cachePath ?? RumourRepository.CachePath, json);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[RumourRefresher] cache write failed: {ex.Message}");
            }

            return RumourRefreshResult.Ok(new RumourRepository(entries), entries.Count);
        }
        catch (Exception ex)
        {
            return RumourRefreshResult.Failed($"Refresh failed ({ex.GetType().Name}) — using bundled data.");
        }
    }
}

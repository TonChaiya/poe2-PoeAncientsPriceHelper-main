using System.Net.Http;

namespace PoeTradeOverlay.Trade;

public sealed class RateLimitGuard
{
    public DateTimeOffset? BlockedUntil { get; private set; }

    public bool IsBlocked(DateTimeOffset now, out DateTimeOffset retryAt)
    {
        if (BlockedUntil is { } blocked && blocked > now)
        {
            retryAt = blocked;
            return true;
        }
        retryAt = default;
        return false;
    }

    public void Observe(HttpResponseMessage response, DateTimeOffset now)
    {
        int longestSeconds = 0;
        if (response.Headers.TryGetValues("X-Rate-Limit-Rules", out var rules))
        {
            foreach (var rule in string.Join(",", rules).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                string header = $"X-Rate-Limit-{rule}-State";
                if (!response.Headers.TryGetValues(header, out var states)) continue;
                foreach (var state in string.Join(",", states).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var parts = state.Split(':');
                    if (parts.Length == 3 && int.TryParse(parts[2], out int seconds) && seconds > longestSeconds)
                        longestSeconds = seconds;
                }
            }
        }

        if (response.Headers.RetryAfter?.Delta is { } delta)
            longestSeconds = Math.Max(longestSeconds, (int)Math.Ceiling(delta.TotalSeconds));
        else if (response.Headers.RetryAfter?.Date is { } date)
            longestSeconds = Math.Max(longestSeconds, (int)Math.Ceiling((date - now).TotalSeconds));

        if (longestSeconds > 0)
        {
            var candidate = now.AddSeconds(longestSeconds);
            if (BlockedUntil is null || candidate > BlockedUntil) BlockedUntil = candidate;
        }
    }
}

using System.Net;
using PoeTradeOverlay.Trade;

namespace PoeTradeOverlay.Tests;

public sealed class RateLimitGuardTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Most_restrictive_active_state_wins()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Headers.TryAddWithoutValidation("X-Rate-Limit-Rules", "ip");
        response.Headers.TryAddWithoutValidation("X-Rate-Limit-Ip-State", "11:5:10, 2:60:30");
        var guard = new RateLimitGuard();

        guard.Observe(response, Now);

        Assert.Equal(Now.AddSeconds(30), guard.BlockedUntil);
    }

    [Fact]
    public void Retry_after_overrides_shorter_or_malformed_state()
    {
        var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        response.Headers.TryAddWithoutValidation("Retry-After", "17");
        response.Headers.TryAddWithoutValidation("X-Rate-Limit-Rules", "ip");
        response.Headers.TryAddWithoutValidation("X-Rate-Limit-Ip-State", "bad, 9:5:3");
        var guard = new RateLimitGuard();

        guard.Observe(response, Now);

        Assert.Equal(Now.AddSeconds(17), guard.BlockedUntil);
    }
}

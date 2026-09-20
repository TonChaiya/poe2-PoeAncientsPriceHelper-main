using PoeTradeOverlay.Models;

namespace PoeTradeOverlay.Abstractions;

public interface ITradeClient
{
    Task<TradeSearchResult> SearchAsync(
        string league,
        TradeQuery query,
        CancellationToken cancellationToken);
}

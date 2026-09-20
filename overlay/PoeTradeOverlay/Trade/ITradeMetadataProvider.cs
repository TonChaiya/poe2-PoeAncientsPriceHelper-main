namespace PoeTradeOverlay.Trade;

public interface ITradeMetadataProvider
{
    Task<TradeMetadataSnapshot> GetAsync(CancellationToken cancellationToken);
}

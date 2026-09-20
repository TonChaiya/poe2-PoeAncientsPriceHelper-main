namespace PoeTradeOverlay.Presentation;

public interface ITradeOverlayView
{
    void Publish(TradeOverlayState state);
    void Close();
}

namespace PoeTradeOverlay.Abstractions;

public interface IClipboardReader
{
    Task<string?> ReadNewTextAsync(string? previousFingerprint, CancellationToken cancellationToken);
}

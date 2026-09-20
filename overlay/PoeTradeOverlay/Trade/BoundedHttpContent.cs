using System.IO;
using System.Net.Http;
using System.Text;

namespace PoeTradeOverlay.Trade;

internal static class BoundedHttpContent
{
    public static async Task<string> ReadStringAsync(HttpContent content, int maxBytes, CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength is > 0 and var length && length > maxBytes)
            throw new InvalidDataException("Trade response is larger than the configured limit.");

        await using var source = await content.ReadAsStreamAsync(cancellationToken);
        using var target = new MemoryStream(Math.Min(maxBytes, 64 * 1024));
        var buffer = new byte[16 * 1024];
        int total = 0;
        while (true)
        {
            int read = await source.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (read == 0) break;
            total += read;
            if (total > maxBytes) throw new InvalidDataException("Trade response is larger than the configured limit.");
            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
        return Encoding.UTF8.GetString(target.ToArray());
    }
}

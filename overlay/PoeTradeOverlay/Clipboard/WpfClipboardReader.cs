using System.Security.Cryptography;
using System.Text;
using System.Windows;
using PoeTradeOverlay.Abstractions;

namespace PoeTradeOverlay.Clipboard;

public sealed class WpfClipboardReader : IClipboardReader
{
    private static readonly TimeSpan[] Delays =
        [TimeSpan.Zero, TimeSpan.FromMilliseconds(35), TimeSpan.FromMilliseconds(90), TimeSpan.FromMilliseconds(180)];
    private readonly Func<string?> _read;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    public WpfClipboardReader() : this(ReadClipboard, Task.Delay) { }

    internal WpfClipboardReader(
        Func<string?> read,
        Func<TimeSpan, CancellationToken, Task> delay)
    {
        _read = read;
        _delay = delay;
    }

    public async Task<string?> ReadNewTextAsync(string? previousFingerprint, CancellationToken cancellationToken)
    {
        for (int index = 0; index < Delays.Length; index++)
        {
            if (index > 0) await _delay(Delays[index], cancellationToken);
            string? text;
            try { text = _read(); }
            catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidOperationException)
            {
                text = null;
            }
            if (string.IsNullOrWhiteSpace(text)) continue;
            if (previousFingerprint is not null &&
                string.Equals(Fingerprint(text), previousFingerprint, StringComparison.Ordinal)) continue;
            return text;
        }
        return null;
    }

    public static string Fingerprint(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    private static string? ReadClipboard() =>
        System.Windows.Clipboard.ContainsText() ? System.Windows.Clipboard.GetText() : null;
}

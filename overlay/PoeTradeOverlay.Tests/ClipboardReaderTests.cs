using PoeTradeOverlay.Clipboard;

namespace PoeTradeOverlay.Tests;

public sealed class ClipboardReaderTests
{
    [Fact]
    public async Task Reads_delayed_new_text_and_stops_after_success()
    {
        var values = new Queue<string?>([null, "", "new item text", "must not read"]);
        int delays = 0;
        var reader = new WpfClipboardReader(() => values.Dequeue(), (_, _) =>
        {
            delays++;
            return Task.CompletedTask;
        });

        var text = await reader.ReadNewTextAsync(null, default);

        Assert.Equal("new item text", text);
        Assert.Equal(2, delays);
        Assert.Single(values);
    }

    [Fact]
    public async Task Unchanged_fingerprint_is_ignored_after_bounded_attempts()
    {
        const string text = "same";
        int reads = 0;
        var reader = new WpfClipboardReader(() => { reads++; return text; }, (_, _) => Task.CompletedTask);
        var result = await reader.ReadNewTextAsync(WpfClipboardReader.Fingerprint(text), default);
        Assert.Null(result);
        Assert.Equal(4, reads);
    }
}

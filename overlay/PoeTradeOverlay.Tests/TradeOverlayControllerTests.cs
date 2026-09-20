using PoeTradeOverlay;
using PoeTradeOverlay.Abstractions;
using PoeTradeOverlay.Models;
using PoeTradeOverlay.Presentation;
using PoeTradeOverlay.Trade;

namespace PoeTradeOverlay.Tests;

public sealed class TradeOverlayControllerTests
{
    [Fact]
    public async Task Invalid_or_commodity_clipboard_does_not_publish_or_search()
    {
        var harness = new Harness("ordinary text");
        await harness.Controller.OnManualCopyAsync();
        Assert.Empty(harness.View.States);
        Assert.Equal(0, harness.Client.Count);
    }

    [Fact]
    public async Task Valid_copy_publishes_filters_and_performs_one_initial_search()
    {
        var harness = new Harness(Fixture.Read("rare-bow.txt"));
        await harness.Controller.OnManualCopyAsync();
        Assert.Equal(1, harness.Client.Count);
        Assert.Contains(harness.View.States, state => state.IsLoading && state.Query is not null);
        Assert.Equal("Storm Song", harness.View.Current!.Item.Name);
    }

    [Fact]
    public async Task Valid_copy_is_acknowledged_before_metadata_download_finishes()
    {
        var metadata = new TaskCompletionSource<TradeMetadataSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new FakeTradeClient(Task.FromResult(new TradeSearchResult("id", 0, [])));
        var view = new FakeView();
        await using var controller = new TradeOverlayController(
            new QueueClipboard(Fixture.Read("rare-bow.txt")),
            new DelayedMetadata(metadata.Task), client, new OneToOneCurrency(), view, "Runes of Aldur");

        var copy = controller.OnManualCopyAsync();

        Assert.Contains(view.States, state => state.IsLoading && state.Query is null && state.Status.Contains("Item read"));
        Assert.Equal(0, client.Count);

        metadata.SetResult(FakeMetadata.Snapshot());
        await copy;
    }

    [Fact]
    public async Task Metadata_failure_is_shown_without_starting_trade_search()
    {
        var client = new FakeTradeClient(Task.FromResult(new TradeSearchResult("id", 0, [])));
        var view = new FakeView();
        await using var controller = new TradeOverlayController(
            new QueueClipboard(Fixture.Read("rare-bow.txt")),
            new DelayedMetadata(Task.FromException<TradeMetadataSnapshot>(new HttpRequestException("offline"))),
            client, new OneToOneCurrency(), view, "Runes of Aldur");

        await controller.OnManualCopyAsync();

        Assert.Equal(TradeFailureKind.Unavailable, view.Current!.Failure!.Kind);
        Assert.Equal(0, client.Count);
    }

    [Fact]
    public async Task Late_result_from_previous_item_is_never_published()
    {
        var first = new TaskCompletionSource<TradeSearchResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new FakeTradeClient(first.Task,
            Task.FromResult(new TradeSearchResult("second", 1,
                [new TradeListing("b", "seller", 10m, "exalted")])));
        var clipboard = new QueueClipboard(Fixture.Read("rare-bow.txt"), Fixture.Read("unique-ring.txt"));
        var view = new FakeView();
        await using var controller = CreateController(clipboard, client, view);

        var firstCopy = controller.OnManualCopyAsync();
        await client.FirstRequestStarted.Task;
        var secondCopy = controller.OnManualCopyAsync();
        await secondCopy;
        first.SetResult(new TradeSearchResult("first", 1,
            [new TradeListing("a", "seller", 1m, "exalted")]));
        await firstCopy;

        Assert.Equal("Dream Fragments", view.Current!.Item.Name);
        Assert.DoesNotContain(view.States, state => state.Item.Name == "Storm Song" && state.Estimate is not null);
    }

    private static TradeOverlayController CreateController(IClipboardReader clipboard, ITradeClient client, ITradeOverlayView view) =>
        new(clipboard, new FakeMetadata(), client, new OneToOneCurrency(), view, "Runes of Aldur");

    private sealed class Harness
    {
        public FakeTradeClient Client { get; } = new(Task.FromResult(new TradeSearchResult("id", 1,
            [new TradeListing("x", "seller", 10m, "exalted")])));
        public FakeView View { get; } = new();
        public TradeOverlayController Controller { get; }
        public Harness(string text) => Controller = CreateController(new QueueClipboard(text), Client, View);
    }

    private sealed class QueueClipboard(params string[] values) : IClipboardReader
    {
        private readonly Queue<string> _values = new(values);
        public Task<string?> ReadNewTextAsync(string? previousFingerprint, CancellationToken cancellationToken) =>
            Task.FromResult<string?>(_values.Count == 0 ? null : _values.Dequeue());
    }

    private sealed class FakeMetadata : ITradeMetadataProvider
    {
        public Task<TradeMetadataSnapshot> GetAsync(CancellationToken cancellationToken) => Task.FromResult(Snapshot());

        public static TradeMetadataSnapshot Snapshot() =>
            new TradeMetadataSnapshot([], [
                new TradeStatDefinition("explicit.fire", "+#% to Fire Resistance", ModifierKind.Explicit),
                new TradeStatDefinition("implicit.mana", "+# to maximum Mana", ModifierKind.Implicit)
            ], [], DateTimeOffset.UtcNow);
    }

    private sealed class DelayedMetadata(Task<TradeMetadataSnapshot> result) : ITradeMetadataProvider
    {
        public Task<TradeMetadataSnapshot> GetAsync(CancellationToken cancellationToken) => result.WaitAsync(cancellationToken);
    }

    private sealed class FakeTradeClient(params Task<TradeSearchResult>[] results) : ITradeClient
    {
        private readonly Queue<Task<TradeSearchResult>> _results = new(results);
        public int Count { get; private set; }
        public TaskCompletionSource FirstRequestStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<TradeSearchResult> SearchAsync(string league, TradeQuery query, CancellationToken cancellationToken)
        {
            Count++;
            FirstRequestStarted.TrySetResult();
            return await _results.Dequeue().WaitAsync(cancellationToken);
        }
    }

    private sealed class OneToOneCurrency : ICurrencyConverter
    {
        public bool TryToExalted(string currency, decimal amount, out decimal exalted)
        {
            exalted = amount;
            return currency == "exalted";
        }
    }

    private sealed class FakeView : ITradeOverlayView
    {
        public List<TradeOverlayState> States { get; } = [];
        public TradeOverlayState? Current => States.LastOrDefault();
        public void Publish(TradeOverlayState state) => States.Add(state);
        public void Close() { }
    }
}

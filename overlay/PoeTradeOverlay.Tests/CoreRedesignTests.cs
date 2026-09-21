using System.Net;
using System.Text;
using System.Text.Json;
using PoeTradeOverlay.Models;
using PoeTradeOverlay.Parsing;
using PoeTradeOverlay.Pricing;
using PoeTradeOverlay.Trade;

namespace PoeTradeOverlay.Tests;

public sealed class CoreRedesignTests
{
    private static TradeMetadataSnapshot HeavyBeltMetadata() => new([], [
        new("implicit.stat_680068163", "#% increased Stun Threshold", ModifierKind.Implicit),
        new("explicit.stat_680068163", "#% increased Stun Threshold", ModifierKind.Explicit),
        new("implicit.stat_1416292992", "Has # Charm Slot", ModifierKind.Implicit),
        new("explicit.stat_1416292992", "Has # Charm Slot", ModifierKind.Explicit)
    ], [], DateTimeOffset.UtcNow);

    [Fact]
    public void Heavy_belt_separates_requirement_rolls_and_implicit_families()
    {
        Assert.True(PoeItemTextParser.TryParse(Fixture.Read("heavy-belt-advanced.txt"), out var item));
        Assert.Equal(50, item.Requirements.Level);
        Assert.DoesNotContain(item.Modifiers, x => x.Text.StartsWith("Requires", StringComparison.Ordinal));
        Assert.All(item.Modifiers, x => Assert.Equal(ModifierKind.Implicit, x.Kind));
        Assert.Equal(["implicit.stat_680068163", "implicit.stat_1416292992"],
            item.Modifiers.Select(x => ModifierMatcher.Resolve(x, HeavyBeltMetadata()).StatId));
        Assert.Equal(new NumericRoll(30m, 20m, 30m), item.ModifierBlocks[0].Rolls[0]);
    }

    [Fact]
    public void Same_text_without_family_evidence_is_ambiguous()
    {
        var modifier = new ParsedModifier("30% increased Stun Threshold", ModifierKind.Unknown, [30m]);
        var result = ModifierMatcher.Resolve(modifier, HeavyBeltMetadata());
        Assert.Equal(ResolutionStatus.Ambiguous, result.Status);
        Assert.Null(result.StatId);
    }

    [Fact]
    public void Crafting_base_uses_instant_buy_and_correct_query_domains()
    {
        Assert.True(PoeItemTextParser.TryParse(Fixture.Read("heavy-belt-advanced.txt"), out var item));
        var query = TradeQueryBuilder.Create(item, HeavyBeltMetadata(), SearchProfile.CraftingBase);
        using var json = JsonDocument.Parse(TradeQuerySerializer.Serialize(query));
        Assert.Equal("securable", json.RootElement.GetProperty("query").GetProperty("status").GetProperty("option").GetString());
        Assert.Equal(75m, query.TypeFilters.ItemLevel?.Min);
        Assert.Equal(50m, query.RequirementFilters.Level?.Max);
        Assert.Equal(2, query.Filters.Count(x => x.IsEnabled));
    }

    [Fact]
    public async Task Twenty_ids_are_fetched_as_two_requests_of_ten()
    {
        string ids = string.Join(',', Enumerable.Range(0, 20).Select(i => $"\"id{i}\""));
        var handler = new QueueHandler(
            Json(HttpStatusCode.OK, $"{{\"id\":\"q\",\"result\":[{ids}],\"total\":20}}"),
            Json(HttpStatusCode.OK, "{\"result\":[]}"),
            Json(HttpStatusCode.OK, "{\"result\":[]}"));
        var client = new PathOfExileTradeClient(new HttpClient(handler));
        await client.SearchAsync("Test", new(null, "Heavy Belt", "accessory.belt", ItemRarity.Normal, false, []), default);
        Assert.Equal([10, 10], handler.FetchSizes);
    }

    [Fact]
    public void Vaal_rate_preserves_original_and_converts_to_exalted()
    {
        var catalog = CurrencyCatalog.Create(new Dictionary<string, decimal> { ["Vaal Orb"] = 6.309554m });
        var result = catalog.Convert("vaal", 4m);
        Assert.Equal("Vaal Orb", result.DisplayName);
        Assert.Equal(4m, result.OriginalAmount);
        Assert.Equal(25.238216m, result.ExaltedValue);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string value) => new(status)
    { Content = new StringContent(value, Encoding.UTF8, "application/json") };

    private sealed class QueueHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);
        public List<int> FetchSizes { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Get)
            {
                var path = request.RequestUri!.AbsolutePath;
                FetchSizes.Add(path[(path.LastIndexOf('/') + 1)..].Split(',').Length);
            }
            return Task.FromResult(_responses.Dequeue());
        }
    }
}

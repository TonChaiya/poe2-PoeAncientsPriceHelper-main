using System.Text.Json;
using PoeTradeOverlay.Models;

namespace PoeTradeOverlay.Trade;

public sealed record TradeItemDefinition(string Id, string Name, string? Category);
public sealed record TradeStatDefinition(string Id, string Text, ModifierKind Kind, string? Group = null);
public sealed record TradeFilterDefinition(string Id, string Text);
public sealed record TradeMetadataSnapshot(
    IReadOnlyList<TradeItemDefinition> Items,
    IReadOnlyList<TradeStatDefinition> Stats,
    IReadOnlyList<TradeFilterDefinition> Filters,
    DateTimeOffset FetchedAt);

internal static class TradeMetadataParser
{
    public static TradeMetadataSnapshot Parse(MetadataCacheEnvelope envelope)
    {
        using var items = JsonDocument.Parse(envelope.ItemsJson);
        using var stats = JsonDocument.Parse(envelope.StatsJson);
        using var filters = JsonDocument.Parse(envelope.FiltersJson);
        RequireResult(items.RootElement);
        RequireResult(stats.RootElement);
        RequireResult(filters.RootElement);

        return new TradeMetadataSnapshot(
            ParseItems(items.RootElement),
            ParseStats(stats.RootElement),
            ParseFilters(filters.RootElement),
            envelope.FetchedAt);
    }

    private static void RequireResult(JsonElement root)
    {
        if (!root.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Array)
            throw new JsonException("Trade metadata has no result array.");
    }

    private static TradeItemDefinition[] ParseItems(JsonElement root)
    {
        var definitions = new List<TradeItemDefinition>();
        foreach (var group in root.GetProperty("result").EnumerateArray())
        {
            string? category = group.TryGetProperty("id", out var categoryNode)
                ? categoryNode.GetString()
                : null;
            if (!group.TryGetProperty("entries", out var entries) || entries.ValueKind != JsonValueKind.Array)
                continue;
            foreach (var entry in entries.EnumerateArray())
            {
                string type = entry.TryGetProperty("type", out var typeNode) ? typeNode.GetString() ?? "" : "";
                string text = entry.TryGetProperty("text", out var textNode) ? textNode.GetString() ?? "" : "";
                string name = entry.TryGetProperty("name", out var nameNode) ? nameNode.GetString() ?? "" : "";
                string id = text.Length > 0 ? text : type;
                string displayName = name.Length > 0 ? name : id;
                if (id.Length > 0 && displayName.Length > 0)
                    definitions.Add(new TradeItemDefinition(id, displayName, category));
            }
        }
        return definitions.ToArray();
    }

    private static TradeStatDefinition[] ParseStats(JsonElement root)
    {
        var result = new List<TradeStatDefinition>();
        foreach (var group in root.GetProperty("result").EnumerateArray())
        {
            string? groupId = group.TryGetProperty("id", out var groupNode) ? groupNode.GetString() : null;
            if (!group.TryGetProperty("entries", out var entries) || entries.ValueKind != JsonValueKind.Array) continue;
            foreach (var x in entries.EnumerateArray())
            {
                string id = x.TryGetProperty("id", out var idNode) ? idNode.GetString() ?? "" : "";
                string text = x.TryGetProperty("text", out var textNode) ? textNode.GetString() ?? "" : "";
                string? type = x.TryGetProperty("type", out var typeNode) ? typeNode.GetString() : groupId;
                if (id.Length > 0 && text.Length > 0) result.Add(new(id, text, ParseKind(type), groupId));
            }
        }
        return result.ToArray();
    }

    private static TradeFilterDefinition[] ParseFilters(JsonElement root) =>
        Entries(root).Select(x => new TradeFilterDefinition(
                x.GetProperty("id").GetString() ?? "",
                x.TryGetProperty("text", out var text) ? text.GetString() ?? "" : ""))
            .Where(x => x.Id.Length > 0).ToArray();

    private static IEnumerable<JsonElement> Entries(JsonElement root)
    {
        foreach (var group in root.GetProperty("result").EnumerateArray())
        {
            if (group.TryGetProperty("entries", out var entries) && entries.ValueKind == JsonValueKind.Array)
                foreach (var entry in entries.EnumerateArray()) yield return entry;
            else if (group.TryGetProperty("id", out _)) yield return group;
        }
    }

    private static ModifierKind ParseKind(string? type) => type?.ToLowerInvariant() switch
    {
        "implicit" => ModifierKind.Implicit,
        "explicit" => ModifierKind.Explicit,
        "pseudo" => ModifierKind.Pseudo,
        "fractured" => ModifierKind.Fractured,
        "crafted" => ModifierKind.Crafted,
        "enchant" => ModifierKind.Enchant,
        "rune" or "augment" => ModifierKind.Rune,
        "desecrated" => ModifierKind.Desecrated,
        "sanctum" => ModifierKind.Sanctum,
        "skill" => ModifierKind.Skill,
        _ => ModifierKind.Unknown
    };
}

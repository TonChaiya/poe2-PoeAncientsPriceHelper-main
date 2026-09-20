using System.Text.Json;
using System.Text.Json.Nodes;
using PoeTradeOverlay.Models;

namespace PoeTradeOverlay.Trade;

public static class TradeQuerySerializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Serialize(TradeQuery query)
    {
        if (string.IsNullOrWhiteSpace(query.BaseType))
            throw new InvalidOperationException("A Trade query requires a base type.");

        var statFilters = new JsonArray();
        foreach (var filter in query.Filters.Where(x => x.IsSupported && x.IsEnabled))
        {
            if (filter.Min is not null && filter.Max is not null && filter.Min > filter.Max)
                throw new InvalidOperationException($"Minimum exceeds maximum for '{filter.SourceText}'.");
            if (string.IsNullOrWhiteSpace(filter.StatId)) continue;
            var value = new JsonObject();
            if (filter.Min is not null) value["min"] = filter.Min.Value;
            if (filter.Max is not null) value["max"] = filter.Max.Value;
            statFilters.Add(new JsonObject
            {
                ["id"] = filter.StatId,
                ["value"] = value
            });
        }

        var typeValues = new JsonObject
        {
            ["rarity"] = new JsonObject { ["option"] = query.Rarity.ToString().ToLowerInvariant() }
        };
        if (!string.IsNullOrWhiteSpace(query.Category))
            typeValues["category"] = new JsonObject { ["option"] = query.Category };

        var allFilters = new JsonObject
        {
            ["type_filters"] = new JsonObject
            {
                ["filters"] = typeValues
            }
        };
        if (query.Corrupted is not null)
        {
            allFilters["misc_filters"] = new JsonObject
            {
                ["filters"] = new JsonObject
                {
                    ["corrupted"] = new JsonObject
                    {
                        ["option"] = query.Corrupted.Value ? "true" : "false"
                    }
                }
            };
        }

        var queryNode = new JsonObject
        {
            ["status"] = new JsonObject { ["option"] = "online" },
            ["type"] = query.BaseType,
            ["stats"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "and",
                    ["filters"] = statFilters
                }
            },
            ["filters"] = allFilters
        };
        if (!string.IsNullOrWhiteSpace(query.Name)) queryNode["name"] = query.Name;

        return new JsonObject
        {
            ["query"] = queryNode,
            ["sort"] = new JsonObject { ["price"] = "asc" }
        }.ToJsonString(Options);
    }
}

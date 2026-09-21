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
        AddRange(typeValues, "ilvl", query.TypeFilters.ItemLevel);

        var allFilters = new JsonObject
        {
            ["type_filters"] = new JsonObject
            {
                ["filters"] = typeValues
            }
        };
        var misc = new JsonObject();
        if (query.MiscFilters.Corrupted is not null)
        {
            misc["corrupted"] = new JsonObject { ["option"] = query.MiscFilters.Corrupted.Value ? "true" : "false" };
        }
        if (query.MiscFilters.Identified is not null)
            misc["identified"] = new JsonObject { ["option"] = query.MiscFilters.Identified.Value ? "true" : "false" };
        if (misc.Count > 0) allFilters["misc_filters"] = new JsonObject { ["filters"] = misc };

        var requirements = new JsonObject();
        AddRange(requirements, "lvl", query.RequirementFilters.Level);
        AddRange(requirements, "str", query.RequirementFilters.Strength);
        AddRange(requirements, "dex", query.RequirementFilters.Dexterity);
        AddRange(requirements, "int", query.RequirementFilters.Intelligence);
        if (requirements.Count > 0) allFilters["req_filters"] = new JsonObject { ["filters"] = requirements };

        var equipment = new JsonObject();
        AddRange(equipment, "quality", query.EquipmentFilters.Quality);
        AddRange(equipment, "pdps", query.EquipmentFilters.PhysicalDps);
        AddRange(equipment, "aps", query.EquipmentFilters.AttacksPerSecond);
        if (equipment.Count > 0) allFilters["equipment_filters"] = new JsonObject { ["filters"] = equipment };

        var queryNode = new JsonObject
        {
            ["status"] = new JsonObject { ["option"] = "securable" },
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

    private static void AddRange(JsonObject target, string key, NumericRange? range)
    {
        if (range is null) return;
        if (range.Min is not null && range.Max is not null && range.Min > range.Max)
            throw new InvalidOperationException($"Minimum exceeds maximum for '{key}'.");
        var value = new JsonObject();
        if (range.Min is not null) value["min"] = range.Min.Value;
        if (range.Max is not null) value["max"] = range.Max.Value;
        target[key] = value;
    }
}

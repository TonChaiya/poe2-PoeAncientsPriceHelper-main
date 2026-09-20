# PoE 2 Detailed Trade Overlay Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an integrated, manually triggered PoE 2 equipment price-check window that reads the game's normal `Ctrl+C` clipboard output, builds editable Trade filters, and shows a rate-limit-aware market estimate without touching game files, memory, credentials, or input.

**Architecture:** A root-level WPF class library under `overlay/PoeTradeOverlay` owns parsing, Trade metadata/query/client code, valuation, cache, controller, and the detailed window. The existing executable references that library and forwards only focused-game `Ctrl+C`, league changes, settings, and a read-only currency snapshot; the existing OCR and ground-loot paths remain independent.

**Tech Stack:** .NET 10, C# 14, WPF, SharpHook 7.1.2, `System.Net.Http`, `System.Text.Json`, xUnit 2.9.3, existing poe.ninja price snapshots, PowerShell/IExpress installer scripts.

**Spec:** `docs/superpowers/specs/2026-09-20-poe2-trade-overlay-design.md`

## Global Constraints

- Target `net10.0-windows10.0.19041.0`; ship inside the existing executable/installer, not as a second app or service.
- All new feature implementation lives below root `overlay/`, except the minimum host integration, settings, versioning, installer, and documentation edits.
- Never modify or inspect game files or memory, inject code, capture packets, synthesize input, automate seller contact/trading, or access cookies, tokens, `POESESSID`, OAuth credentials, or browser state.
- React only to a manually pressed `Ctrl+C` while Path of Exile 2 is foreground; do not suppress or replace that input.
- No continuous clipboard polling, screenshot capture, OCR trigger, timer-triggered search, telemetry, or raw clipboard persistence/logging.
- Trade requests use anonymous public market data, a descriptive User-Agent, strict timeouts/body limits, dynamic `X-Rate-Limit-*` parsing, and exact `Retry-After` compliance.
- Currency, fragments, gems, and fixed-price commodities stay on the existing poe.ninja path and do not open the detailed window.
- Version the backward-compatible feature as fork `1.1.0`; external auto-update remains absent.
- Preserve `old/` unchanged and ignored; modify only this repository.

## Review Focus

- Clipboard text can arrive after key release or remain unchanged; tests must prove bounded retry, fingerprint de-duplication, and no polling after the read window.
- Localized/unknown modifier text can resemble a known stat; tests must prove ambiguous lines stay unsupported rather than map to the wrong Trade ID.
- A late response from item A can arrive after item B was copied; tests must prove cancellation plus generation checks prevent stale UI publication.
- Trade can return partial prices, duplicate accounts, unsupported currencies, malformed JSON, or extreme outliers; tests must prove deterministic exclusion and honest confidence.
- Rate-limit headers can contain multiple rules/windows and `Retry-After` can be absent or malformed; tests must prove the most restrictive valid block wins and no automatic retry occurs.

---

### Task 1: Create the isolated overlay projects and public contracts

**Files:**
- Create: `overlay/PoeTradeOverlay/PoeTradeOverlay.csproj`
- Create: `overlay/PoeTradeOverlay/Models/ParsedItem.cs`
- Create: `overlay/PoeTradeOverlay/Models/TradeModels.cs`
- Create: `overlay/PoeTradeOverlay/Abstractions/IClipboardReader.cs`
- Create: `overlay/PoeTradeOverlay/Abstractions/ITradeClient.cs`
- Create: `overlay/PoeTradeOverlay/Abstractions/ICurrencyConverter.cs`
- Create: `overlay/PoeTradeOverlay.Tests/PoeTradeOverlay.Tests.csproj`
- Create: `overlay/PoeTradeOverlay.Tests/ContractTests.cs`
- Modify: `PoeAncientsPriceHelper.sln`
- Modify: `src/PoeAncientsPriceHelper/PoeAncientsPriceHelper.csproj`

**Interfaces:**
- Produces: `ParsedItem`, `ParsedModifier`, `TradeFilter`, `TradeQuery`, `TradeListing`, `PriceEstimate`, `TradeFailure`, `IClipboardReader`, `ITradeClient`, and `ICurrencyConverter` used by every later task.
- Consumes: no feature code; only .NET/WPF primitives.

- [ ] **Step 1: Write the failing contract test**

```csharp
[Fact]
public void ParsedItem_is_immutable_and_retains_modifier_kinds()
{
    var item = new ParsedItem(ItemRarity.Rare, "Storm Song", "Dualstring Bow", "Bow",
        82, 20, false,
        [new ParsedModifier("+35% to Fire Resistance", ModifierKind.Explicit, [35m])],
        new Dictionary<string, decimal> { ["physical_dps"] = 410.5m });

    Assert.Equal("Dualstring Bow", item.BaseType);
    Assert.Equal(ModifierKind.Explicit, item.Modifiers.Single().Kind);
    Assert.Equal(410.5m, item.Properties["physical_dps"]);
}
```

- [ ] **Step 2: Run the test and verify the missing project/types failure**

Run: `dotnet test overlay/PoeTradeOverlay.Tests/PoeTradeOverlay.Tests.csproj --filter ParsedItem_is_immutable`

Expected: FAIL because the overlay project and contracts do not exist.

- [ ] **Step 3: Add projects and minimal immutable contracts**

Use a WPF class library with nullable enabled and expose records similar to:

```csharp
public sealed record ParsedModifier(string Text, ModifierKind Kind, IReadOnlyList<decimal> Values);
public sealed record ParsedItem(ItemRarity Rarity, string Name, string BaseType, string ItemClass,
    int? ItemLevel, int? Quality, bool Corrupted,
    IReadOnlyList<ParsedModifier> Modifiers, IReadOnlyDictionary<string, decimal> Properties);

public interface ITradeClient
{
    Task<TradeSearchResult> SearchAsync(string league, TradeQuery query, CancellationToken cancellationToken);
}

public interface ICurrencyConverter
{
    bool TryToExalted(string currency, decimal amount, out decimal exalted);
}
```

Reference `PoeTradeOverlay` from the host app, add both new projects to the solution, and grant `InternalsVisibleTo` to the overlay tests.

- [ ] **Step 4: Run contract tests and solution build**

Run: `dotnet test overlay/PoeTradeOverlay.Tests/PoeTradeOverlay.Tests.csproj`

Expected: PASS.

Run: `dotnet build PoeAncientsPriceHelper.sln -c Debug --no-restore`

Expected: PASS with zero warnings and errors.

- [ ] **Step 5: Commit the project boundary**

```text
git add PoeAncientsPriceHelper.sln src/PoeAncientsPriceHelper/PoeAncientsPriceHelper.csproj overlay
git commit -m feat:scaffold-trade-overlay-module
```

### Task 2: Parse and classify manually copied PoE 2 equipment

**Files:**
- Create: `overlay/PoeTradeOverlay/Parsing/PoeItemTextParser.cs`
- Create: `overlay/PoeTradeOverlay/Parsing/ItemEligibility.cs`
- Create: `overlay/PoeTradeOverlay/Parsing/NumericText.cs`
- Create: `overlay/PoeTradeOverlay.Tests/Fixtures/rare-bow.txt`
- Create: `overlay/PoeTradeOverlay.Tests/Fixtures/unique-ring.txt`
- Create: `overlay/PoeTradeOverlay.Tests/Fixtures/charm.txt`
- Create: `overlay/PoeTradeOverlay.Tests/PoeItemTextParserTests.cs`
- Create: `overlay/PoeTradeOverlay.Tests/ItemEligibilityTests.cs`

**Interfaces:**
- Consumes: `ParsedItem` and `ParsedModifier` from Task 1.
- Produces: `PoeItemTextParser.TryParse(string, out ParsedItem)` and `ItemEligibility.IsDetailedTradeCandidate(ParsedItem)`.

- [ ] **Step 1: Add failing parser tests for equipment, malformed text, and delayed-risk inputs**

```csharp
[Theory]
[InlineData("rare-bow.txt", "Dualstring Bow", ItemRarity.Rare, 82)]
[InlineData("unique-ring.txt", "Lazuli Ring", ItemRarity.Unique, 76)]
[InlineData("charm.txt", "Dousing Charm", ItemRarity.Magic, 68)]
public void Parses_supported_equipment(string fixture, string baseType, ItemRarity rarity, int itemLevel)
{
    Assert.True(PoeItemTextParser.TryParse(Fixture.Read(fixture), out var item));
    Assert.Equal((baseType, rarity, itemLevel), (item.BaseType, item.Rarity, item.ItemLevel));
}

[Theory]
[InlineData("")]
[InlineData("ordinary clipboard text")]
[InlineData("Rarity: Rare\nOnly one incomplete line")]
public void Rejects_non_item_or_incomplete_text(string text) =>
    Assert.False(PoeItemTextParser.TryParse(text, out _));
```

Add tests that parse decimal DPS, signed/ranged modifier numbers, `(implicit)`, `(rune)`, corruption, and a modifier containing numbers in its label. Add a Thai/localized unknown modifier fixture and assert it is retained as `ModifierKind.Unknown`, not assigned a guessed stat.

- [ ] **Step 2: Run parser tests and verify failure**

Run: `dotnet test overlay/PoeTradeOverlay.Tests/PoeTradeOverlay.Tests.csproj --filter "PoeItemTextParserTests|ItemEligibilityTests"`

Expected: FAIL because parsing and eligibility classes are missing.

- [ ] **Step 3: Implement conservative block parsing and capability-based eligibility**

Split on the literal PoE separator line, require `Item Class:` and `Rarity:`, derive unique name/base lines by rarity, parse known property labels with invariant decimal handling, and classify suffix markers without deleting the original line. Eligibility accepts equippable Trade classes and rejects known commodity classes:

```csharp
private static readonly HashSet<string> CommodityClasses = new(StringComparer.OrdinalIgnoreCase)
{
    "Currency", "Stackable Currency", "Skill Gems", "Support Gems", "Fragments"
};

public static bool IsDetailedTradeCandidate(ParsedItem item) =>
    !CommodityClasses.Contains(item.ItemClass) &&
    (item.Modifiers.Count > 0 || item.Rarity is ItemRarity.Rare or ItemRarity.Unique);
```

Keep the accepted equipment classes data-driven and fail closed when `ItemClass` is blank or commodity-like.

- [ ] **Step 4: Run parser and eligibility tests**

Run: `dotnet test overlay/PoeTradeOverlay.Tests/PoeTradeOverlay.Tests.csproj --filter "PoeItemTextParserTests|ItemEligibilityTests"`

Expected: PASS.

- [ ] **Step 5: Commit parsing**

```text
git add overlay/PoeTradeOverlay/Parsing overlay/PoeTradeOverlay.Tests
git commit -m feat:parse-copied-poe2-equipment
```

### Task 3: Cache Trade metadata and map modifiers without guessing

**Files:**
- Create: `overlay/PoeTradeOverlay/Trade/TradeMetadataCatalog.cs`
- Create: `overlay/PoeTradeOverlay/Trade/TradeMetadataParser.cs`
- Create: `overlay/PoeTradeOverlay/Trade/ModifierMatcher.cs`
- Create: `overlay/PoeTradeOverlay/Storage/AtomicJsonCache.cs`
- Create: `overlay/PoeTradeOverlay.Tests/TradeMetadataCatalogTests.cs`
- Create: `overlay/PoeTradeOverlay.Tests/ModifierMatcherTests.cs`

**Interfaces:**
- Consumes: normalized `ParsedModifier.Text` from Task 2 and `HttpClient` supplied by the host.
- Produces: `TradeMetadataCatalog.GetAsync(CancellationToken)`, `TradeMetadataSnapshot`, and `ModifierMatcher.Match(ParsedModifier, TradeMetadataSnapshot)` returning either one stable stat ID or an explicit unsupported result.

- [ ] **Step 1: Write failing metadata/cache/matcher tests**

Cover valid cached load without HTTP, malformed refresh preserving last-known-good cache, atomic temp-file replacement, exact normalized template matching, multiple numeric placeholders, and ambiguous templates returning unsupported:

```csharp
[Fact]
public void Ambiguous_modifier_is_not_guessed()
{
    var snapshot = Metadata.WithStats(
        ("explicit.a", "+# to Level of all Skill Gems"),
        ("explicit.b", "+# to Level of all Spell Skill Gems"));

    var match = ModifierMatcher.Match(
        new ParsedModifier("+1 to Level of all unknown Skill Gems", ModifierKind.Unknown, [1m]), snapshot);

    Assert.False(match.IsSupported);
    Assert.Null(match.StatId);
}
```

- [ ] **Step 2: Run focused tests and verify failure**

Run: `dotnet test overlay/PoeTradeOverlay.Tests/PoeTradeOverlay.Tests.csproj --filter "TradeMetadataCatalogTests|ModifierMatcherTests"`

Expected: FAIL because catalog and matcher do not exist.

- [ ] **Step 3: Implement lazy metadata retrieval and safe matching**

Use `/api/trade2/data/items`, `/api/trade2/data/stats`, and `/api/trade2/data/filters` behind constants private to the adapter. Normalize templates by replacing numeric spans with `#` while retaining all other words and modifier kind. Accept only a unique exact normalized template match. Cache a versioned envelope:

```csharp
internal sealed record MetadataCacheEnvelope(int SchemaVersion, DateTimeOffset FetchedAt,
    JsonElement Items, JsonElement Stats, JsonElement Filters);
```

Write to a sibling `.tmp`, flush, validate by reading it back, then replace the destination. Never replace a valid cache with an empty or unparsable response.

- [ ] **Step 4: Run metadata tests**

Run: `dotnet test overlay/PoeTradeOverlay.Tests/PoeTradeOverlay.Tests.csproj --filter "TradeMetadataCatalogTests|ModifierMatcherTests"`

Expected: PASS.

- [ ] **Step 5: Commit metadata support**

```text
git add overlay/PoeTradeOverlay/Trade overlay/PoeTradeOverlay/Storage overlay/PoeTradeOverlay.Tests
git commit -m feat:add-safe-trade-metadata-mapping
```

### Task 4: Build minimal editable Trade queries

**Files:**
- Create: `overlay/PoeTradeOverlay/Trade/TradeQueryBuilder.cs`
- Create: `overlay/PoeTradeOverlay/Trade/TradeQuerySerializer.cs`
- Create: `overlay/PoeTradeOverlay/Presentation/FilterRowViewModel.cs`
- Create: `overlay/PoeTradeOverlay.Tests/TradeQueryBuilderTests.cs`
- Create: `overlay/PoeTradeOverlay.Tests/TradeQuerySerializerTests.cs`

**Interfaces:**
- Consumes: `ParsedItem`, `TradeMetadataSnapshot`, and `ModifierMatcher`.
- Produces: `TradeQueryBuilder.CreateRecommended(ParsedItem, TradeMetadataSnapshot)` and `TradeQuerySerializer.Serialize(TradeQuery)`.

- [ ] **Step 1: Write failing exact-query tests**

Assert base/category, rarity or unique identity, corruption, and supported modifiers serialize to the expected anonymous Trade JSON. Verify disabled filters are omitted, user min/max values round-trip, unknown modifiers remain visible but absent from JSON, and invalid `min > max` prevents a query:

```csharp
[Fact]
public void Unsupported_modifier_is_visible_but_not_serialized()
{
    var query = TradeQueryBuilder.CreateRecommended(Fixtures.RareBow, Metadata.Standard);
    Assert.Contains(query.Filters, x => !x.IsSupported && x.SourceText == "Unknown local modifier");
    Assert.DoesNotContain("Unknown local modifier", TradeQuerySerializer.Serialize(query));
}
```

- [ ] **Step 2: Run query tests and verify failure**

Run: `dotnet test overlay/PoeTradeOverlay.Tests/PoeTradeOverlay.Tests.csproj --filter "TradeQueryBuilderTests|TradeQuerySerializerTests"`

Expected: FAIL because builder and serializer do not exist.

- [ ] **Step 3: Implement recommended filters and deterministic serialization**

Create one `TradeFilter` per parsed line, preserving display order. Enable unique identity and price-relevant explicit/implicit filters; keep unsupported rows disabled. Use `System.Text.Json` DTOs rather than string concatenation, omit null bounds, and sort by price ascending:

```csharp
var payload = new
{
    query = new { status = new { option = "online" }, name, type, stats, filters },
    sort = new { price = "asc" }
};
```

- [ ] **Step 4: Run query tests**

Run: `dotnet test overlay/PoeTradeOverlay.Tests/PoeTradeOverlay.Tests.csproj --filter "TradeQueryBuilderTests|TradeQuerySerializerTests"`

Expected: PASS.

- [ ] **Step 5: Commit query building**

```text
git add overlay/PoeTradeOverlay/Trade overlay/PoeTradeOverlay/Presentation overlay/PoeTradeOverlay.Tests
git commit -m feat:build-editable-trade-queries
```

### Task 5: Implement the anonymous Trade client and dynamic rate-limit guard

**Files:**
- Create: `overlay/PoeTradeOverlay/Trade/PathOfExileTradeClient.cs`
- Create: `overlay/PoeTradeOverlay/Trade/RateLimitGuard.cs`
- Create: `overlay/PoeTradeOverlay/Trade/BoundedHttpContent.cs`
- Create: `overlay/PoeTradeOverlay.Tests/PathOfExileTradeClientTests.cs`
- Create: `overlay/PoeTradeOverlay.Tests/RateLimitGuardTests.cs`

**Interfaces:**
- Consumes: league ID and serialized `TradeQuery` from Task 4.
- Produces: `ITradeClient.SearchAsync`, typed `TradeSearchResult`, `TradeFailure`, and `RateLimitGuard.BlockedUntil`.

- [ ] **Step 1: Write failing HTTP and rate-limit tests with a fake handler**

Test URL encoding, User-Agent, one POST plus one bounded fetch, a maximum result ID count, response-size rejection, timeout/cancellation, forbidden/redirect failure, malformed JSON, and no credential headers. Cover multiple rate-limit rules and malformed/missing `Retry-After`:

```csharp
[Fact]
public async Task Http_429_blocks_until_retry_after_without_retrying()
{
    var handler = FakeHttp.Once(HttpStatusCode.TooManyRequests,
        headers: new() { ["Retry-After"] = "17" });
    var client = CreateClient(handler, now: Instant);

    var result = await client.SearchAsync("Runes of Aldur", Query.Valid, default);

    Assert.Equal(TradeFailureKind.RateLimited, result.Failure?.Kind);
    Assert.Equal(Instant.AddSeconds(17), result.Failure?.RetryAt);
    Assert.Equal(1, handler.RequestCount);
}
```

- [ ] **Step 2: Run Trade client tests and verify failure**

Run: `dotnet test overlay/PoeTradeOverlay.Tests/PoeTradeOverlay.Tests.csproj --filter "PathOfExileTradeClientTests|RateLimitGuardTests"`

Expected: FAIL because the client and guard do not exist.

- [ ] **Step 3: Implement bounded search/fetch and restriction parsing**

Use `POST https://www.pathofexile.com/api/trade2/search/poe2/{escapedLeague}`, then fetch at most 20 IDs with `GET /api/trade2/fetch/{ids}?query={searchId}`. Configure headers per request:

```csharp
request.Headers.UserAgent.ParseAdd("Poe2GroundLootPriceHelper/1.1.0 (contact: https://github.com/TonChaiya/poe2-PoeAncientsPriceHelper-main)");
request.Headers.Referrer = new Uri("https://www.pathofexile.com/trade2/search/poe2");
```

Do not attach cookies or authorization. Before every request, fail locally when `BlockedUntil > clock.UtcNow`. Parse all valid state headers and use the longest active restriction; on `429`, prefer a valid `Retry-After`, otherwise use the active header restriction. Return the block to the caller and never schedule a retry.

- [ ] **Step 4: Run client and full overlay tests**

Run: `dotnet test overlay/PoeTradeOverlay.Tests/PoeTradeOverlay.Tests.csproj --filter "PathOfExileTradeClientTests|RateLimitGuardTests"`

Expected: PASS and fake handler request counts match exactly.

- [ ] **Step 5: Commit the client**

```text
git add overlay/PoeTradeOverlay/Trade overlay/PoeTradeOverlay.Tests
git commit -m feat:add-rate-limited-trade-client
```

### Task 6: Estimate credible prices and adapt existing currency rates

**Files:**
- Create: `overlay/PoeTradeOverlay/Pricing/PriceEstimator.cs`
- Create: `overlay/PoeTradeOverlay/Pricing/ConfidenceRules.cs`
- Create: `overlay/PoeTradeOverlay.Tests/PriceEstimatorTests.cs`
- Create: `src/PoeAncientsPriceHelper/OverlayCurrencyConverter.cs`
- Create: `src/PoeAncientsPriceHelper.Tests/OverlayCurrencyConverterTests.cs`

**Interfaces:**
- Consumes: `IReadOnlyList<TradeListing>` and `ICurrencyConverter`; host adapter reads immutable `PriceRepository.Current` only.
- Produces: `PriceEstimator.Estimate(IReadOnlyList<TradeListing>, int totalMatches, ICurrencyConverter)` returning `PriceEstimate?`.

- [ ] **Step 1: Write failing estimator and adapter tests**

Cover unpriced listings, same-account duplicates, unknown currency, mixed exalted/divine, a single extreme outlier, no usable values, and low/medium/high confidence. Pin the expected deterministic rule: keep one lowest normalized listing per account, require positive prices, calculate Tukey fences only with at least eight samples, and report median plus 25th–75th percentile range.

```csharp
[Fact]
public void Duplicate_accounts_and_extreme_outlier_do_not_define_typical_price()
{
    var estimate = PriceEstimator.Estimate(Listings.Exalted(10, 11, 12, 12, 13, 14, 15, 500,
        duplicateAccountAt: 0), 40, Currency.OneToOne);
    Assert.Equal(12.5m, estimate!.MedianExalted);
    Assert.True(estimate.UsableListings < estimate.TotalMatches);
}
```

Add an adapter test proving a missing poe.ninja currency rate returns `false` instead of a fabricated value.

- [ ] **Step 2: Run pricing tests and verify failure**

Run: `dotnet test PoeAncientsPriceHelper.sln --filter "PriceEstimatorTests|OverlayCurrencyConverterTests"`

Expected: FAIL because estimator and adapter do not exist.

- [ ] **Step 3: Implement deterministic statistics and snapshot-only conversion**

Normalize recognized currency aliases, collapse account duplicates, sort decimal values, calculate interpolated quartiles, and assign confidence from sample size, interquartile dispersion, filter count, and conversion coverage. The host adapter takes a `Func<PriceSnapshot>` and reads the current immutable snapshot per call; it never triggers poe.ninja I/O.

- [ ] **Step 4: Run pricing and repository regression tests**

Run: `dotnet test PoeAncientsPriceHelper.sln --filter "PriceEstimatorTests|OverlayCurrencyConverterTests|PriceRepositoryTests"`

Expected: PASS.

- [ ] **Step 5: Commit valuation**

```text
git add overlay/PoeTradeOverlay/Pricing overlay/PoeTradeOverlay.Tests src/PoeAncientsPriceHelper/OverlayCurrencyConverter.cs src/PoeAncientsPriceHelper.Tests/OverlayCurrencyConverterTests.cs
git commit -m feat:estimate-detailed-item-prices
```

### Task 7: Add bounded clipboard reading and stale-safe controller orchestration

**Files:**
- Create: `overlay/PoeTradeOverlay/Clipboard/WpfClipboardReader.cs`
- Create: `overlay/PoeTradeOverlay/TradeOverlayController.cs`
- Create: `overlay/PoeTradeOverlay/Presentation/TradeOverlayState.cs`
- Create: `overlay/PoeTradeOverlay.Tests/ClipboardReaderTests.cs`
- Create: `overlay/PoeTradeOverlay.Tests/TradeOverlayControllerTests.cs`

**Interfaces:**
- Consumes: parser, eligibility, catalog, query builder, `ITradeClient`, `ICurrencyConverter`, `Func<string>` active league, and an `ITradeOverlayView` implemented in Task 8.
- Produces: `TradeOverlayController.OnManualCopyAsync()`, `SetLeague(string)`, and `DisposeAsync()`.

- [ ] **Step 1: Write failing clipboard and generation tests**

Use fake clipboard/clock/view dependencies. Test text arriving on the second bounded attempt, unchanged fingerprint ignored, invalid/commodity text producing no view/request, one valid copy producing one initial request, filter edit producing no request, explicit search producing one request, disposal cancellation, and item A completing after item B:

```csharp
[Fact]
public async Task Late_result_from_previous_item_is_never_published()
{
    var first = new TaskCompletionSource<TradeSearchResult>();
    var harness = ControllerHarness.WithFirstSearch(first.Task);
    await harness.CopyAsync(Fixtures.RareBow);
    await harness.CopyAsync(Fixtures.UniqueRing);
    first.SetResult(Results.For("Rare Bow"));

    Assert.DoesNotContain(harness.View.States, s => s.ItemName == "Rare Bow" && s.HasResults);
    Assert.Equal("Unique Ring", harness.View.Current.ItemName);
}
```

- [ ] **Step 2: Run controller tests and verify failure**

Run: `dotnet test overlay/PoeTradeOverlay.Tests/PoeTradeOverlay.Tests.csproj --filter "ClipboardReaderTests|TradeOverlayControllerTests"`

Expected: FAIL because reader/controller do not exist.

- [ ] **Step 3: Implement the bounded read and single-pipeline controller**

Use dispatcher clipboard reads at delays `0, 35, 90, 180` ms, stop at the first new non-empty text, and retain only a SHA-256 fingerprint after parsing. `OnManualCopyAsync` increments a generation, cancels/disposes the old CTS, parses/classifies, publishes filters, performs one initial search, and checks both cancellation and generation before every view update. `SearchEditedAsync(TradeQuery)` is the only later request entry point.

- [ ] **Step 4: Run clipboard/controller tests repeatedly**

Run: `dotnet test overlay/PoeTradeOverlay.Tests/PoeTradeOverlay.Tests.csproj --filter "ClipboardReaderTests|TradeOverlayControllerTests" -- RunConfiguration.MaxCpuCount=1`

Expected: PASS on three consecutive runs with exact fake request counts.

- [ ] **Step 5: Commit orchestration**

```text
git add overlay/PoeTradeOverlay/Clipboard overlay/PoeTradeOverlay/TradeOverlayController.cs overlay/PoeTradeOverlay/Presentation/TradeOverlayState.cs overlay/PoeTradeOverlay.Tests
git commit -m feat:orchestrate-manual-clipboard-price-check
```

### Task 8: Build the integrated WPF filter and result window

**Files:**
- Create: `overlay/PoeTradeOverlay/Presentation/ITradeOverlayView.cs`
- Create: `overlay/PoeTradeOverlay/Presentation/TradeOverlayWindow.xaml`
- Create: `overlay/PoeTradeOverlay/Presentation/TradeOverlayWindow.xaml.cs`
- Create: `overlay/PoeTradeOverlay/Presentation/TradeOverlayViewModel.cs`
- Create: `overlay/PoeTradeOverlay/Presentation/ScreenPlacement.cs`
- Create: `overlay/PoeTradeOverlay.Tests/TradeOverlayViewModelTests.cs`
- Create: `overlay/PoeTradeOverlay.Tests/ScreenPlacementTests.cs`

**Interfaces:**
- Consumes: immutable `TradeOverlayState` updates and calls `TradeOverlayController.SearchEditedAsync` only from the Search button.
- Produces: `ITradeOverlayView.Publish(TradeOverlayState)` and `Close()`.

- [ ] **Step 1: Write failing view-model and placement tests**

Test visible item acknowledgement, unsupported rows disabled/labeled, numeric min/max validation, Search disabled while invalid/loading/rate-limited, countdown display from a supplied clock, confidence/result formatting, no seller action, and placement clamped to the game monitor working area without covering the cursor rectangle when an alternative edge fits.

- [ ] **Step 2: Run presentation tests and verify failure**

Run: `dotnet test overlay/PoeTradeOverlay.Tests/PoeTradeOverlay.Tests.csproj --filter "TradeOverlayViewModelTests|ScreenPlacementTests"`

Expected: FAIL because presentation types do not exist.

- [ ] **Step 3: Implement a non-activating-on-open, user-interactive tool window**

Create a dark, compact WPF window with item header, “Item read” status, scrollable filter rows, min/max text boxes, Search button, match/sample counts, credible low, typical range, median, currencies, confidence, and error/rate-limit panels. Initial display uses `ShowActivated="False"`; clicking the window allows normal interaction. Do not set click-through flags because filters must be editable. Do not add whisper, buy, browser automation, or seller buttons.

Bind all values through `TradeOverlayViewModel`; code-behind is limited to window placement, close, and forwarding the Search command.

- [ ] **Step 4: Run presentation tests and build the library**

Run: `dotnet test overlay/PoeTradeOverlay.Tests/PoeTradeOverlay.Tests.csproj --filter "TradeOverlayViewModelTests|ScreenPlacementTests"`

Expected: PASS.

Run: `dotnet build overlay/PoeTradeOverlay/PoeTradeOverlay.csproj -c Debug --no-restore`

Expected: PASS with zero warnings and errors.

- [ ] **Step 5: Commit the UI**

```text
git add overlay/PoeTradeOverlay/Presentation overlay/PoeTradeOverlay.Tests
git commit -m feat:add-detailed-trade-filter-window
```

### Task 9: Wire focused-game Ctrl+C, settings, league changes, and lifecycle

**Files:**
- Modify: `src/PoeAncientsPriceHelper/App.xaml.cs`
- Modify: `src/PoeAncientsPriceHelper/AppConfig.cs`
- Modify: `src/PoeAncientsPriceHelper/GameWindow.cs`
- Modify: `src/PoeAncientsPriceHelper/MainWindow.xaml.cs`
- Modify: `src/PoeAncientsPriceHelper/SettingsWindow.xaml`
- Modify: `src/PoeAncientsPriceHelper/SettingsWindow.xaml.cs`
- Create: `src/PoeAncientsPriceHelper/TradeOverlayHost.cs`
- Create: `src/PoeAncientsPriceHelper.Tests/TradeOverlayHostTests.cs`
- Modify: `src/PoeAncientsPriceHelper.Tests/ConfigStoreTests.cs`
- Modify: `src/PoeAncientsPriceHelper.Tests/HotkeyBindingTests.cs`

**Interfaces:**
- Consumes: `TradeOverlayController` and WPF view from Tasks 7–8.
- Produces: host lifetime, `AppConfig.DetailedTradeOverlayEnabled`, exact focused-game Ctrl+C forwarding, league updates, and shutdown cleanup.

- [ ] **Step 1: Write failing host/config/hotkey tests**

Test a missing config property defaults enabled, disabling persists, exact `Ctrl+C` matches while `Ctrl+Shift+C` and bare `C` do not, no action during hotkey capture, no action when game is absent/background, one notification when focused, and league changes cancel/update the controller.

- [ ] **Step 2: Run host integration tests and verify failure**

Run: `dotnet test src/PoeAncientsPriceHelper.Tests/PoeAncientsPriceHelper.Tests.csproj --filter "TradeOverlayHostTests|ConfigStoreTests|HotkeyBindingTests"`

Expected: FAIL because host and setting are missing.

- [ ] **Step 3: Implement minimal host integration**

Add a public `GameWindow.TryGet`-backed predicate that returns true only when the actual PoE process owns foreground focus; unlike OCR's fail-open behavior, clipboard price checks fail closed when detection is uncertain. In the existing key-release handler, after capture and modifier handling:

```csharp
else if (chord == new Chord(KeyCode.VcC, Modifiers.Ctrl))
    InvokeDetailedTradeCopy();
```

`InvokeDetailedTradeCopy` marshals to the UI dispatcher; `TradeOverlayHost` rechecks setting/focus before calling `OnManualCopyAsync`. Construct the host after configuration and price repository initialization, call `SetLeague` after a successful selection change, and dispose it before `_http.Dispose()`.

Add a Settings checkbox labeled “Detailed Trade overlay — read items copied with Ctrl+C”. The description states that supported structured item filters are sent anonymously to Path of Exile Trade and that no credentials or input automation are used.

- [ ] **Step 4: Run host regressions and all tests**

Run: `dotnet test PoeAncientsPriceHelper.sln`

Expected: all existing and new tests PASS.

- [ ] **Step 5: Commit host integration**

```text
git add src/PoeAncientsPriceHelper src/PoeAncientsPriceHelper.Tests
git commit -m feat:integrate-focused-ctrl-c-trade-check
```

### Task 10: Version, document, package, and verify release 1.1.0

**Files:**
- Modify: `src/PoeAncientsPriceHelper/PoeAncientsPriceHelper.csproj`
- Modify: `src/PoeAncientsPriceHelper/MainWindow.xaml`
- Modify: `src/PoeAncientsPriceHelper/PriceRepository.cs`
- Modify: `installer/Build-Installer.ps1`
- Modify: `installer/Verify-Installer.ps1`
- Modify: `README.md`
- Modify: `CHANGELOG.md`
- Modify: `docs/ARCHITECTURE.md`
- Modify: `docs/PROJECT_STRUCTURE.md`
- Modify: `docs/DEVELOPMENT.md`
- Create: `overlay/README.md`
- Create: `docs/releases/1.1.0.md`
- Modify after build: `docs/releases/1.1.0.md` with final installer SHA-256

**Interfaces:**
- Consumes: the completed feature and passing tests.
- Produces: a documented 1.1.0 Release build and verified installer under `install/`.

- [ ] **Step 1: Add a failing packaging assertion**

Extend `installer/Verify-Installer.ps1` to require the published overlay assembly/resources and assert the installed main executable reports file/product version `1.1.0.0`. Run it against the previous installer.

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File installer/Verify-Installer.ps1`

Expected: FAIL because the existing 1.0.0 installer lacks the new version and overlay payload.

- [ ] **Step 2: Update all visible/versioned metadata and network identification**

Set `<Version>1.1.0</Version>`, title/header text to 1.1.0 and “Ground Loot + Detailed Trade Overlay”, installer version/output name to `Poe2GroundLootPriceHelper-v1.1.0-Setup.exe`, and every project-owned User-Agent version to `1.1.0`. Do not add update-check code.

- [ ] **Step 3: Document boundaries, privacy, endpoints, cache, and maintenance**

Document:

- `overlay/` responsibilities and public interfaces;
- exact manual `Ctrl+C` flow and supported item classes;
- anonymous Trade request behavior, rate-limit handling, and unstable-adapter boundary;
- data that stays local and data represented in structured Trade queries;
- absence of credentials, telemetry, input synthesis, game file/memory access, seller automation, and auto-update;
- how to update Trade DTOs/stat matching safely with fixtures;
- release provenance from upstream 3.7.1 and fork history from 1.0.0.

Create `docs/releases/1.1.0.md` with feature summary, safety notes, verification commands/results, installer filename, and a checksum field populated only after the final artifact exists.

- [ ] **Step 4: Run formatting, vulnerability, tests, and Release build**

Run:

```text
dotnet format PoeAncientsPriceHelper.sln --verify-no-changes
dotnet list PoeAncientsPriceHelper.sln package --vulnerable --include-transitive
dotnet test PoeAncientsPriceHelper.sln -c Release
dotnet build PoeAncientsPriceHelper.sln -c Release --no-restore
```

Expected: formatting check passes; no vulnerable packages; all tests pass; build has zero warnings and errors.

- [ ] **Step 5: Perform manual safety and behavior checks**

With PoE 2 running, verify one manual `Ctrl+C` over each fixture class produces one window/query, unsupported commodities do not open it, editing does not query until Search, league changes route correctly, and the OCR overlay cadence remains responsive. With a fake/debug HTTP endpoint, verify timeout, malformed response, stale response, and 429 states. Inspect Process Monitor or equivalent read-only evidence to confirm no paths under the Steam PoE installation are written and no credential/browser stores are opened.

- [ ] **Step 6: Build and verify the installer**

Run:

```text
powershell -NoProfile -ExecutionPolicy Bypass -File installer/Build-Installer.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File installer/Verify-Installer.ps1
Get-FileHash install/Poe2GroundLootPriceHelper-v1.1.0-Setup.exe -Algorithm SHA256
```

Expected: installer build succeeds, verification passes, and a SHA-256 is printed. Insert that exact hash into `docs/releases/1.1.0.md`, then rerun `git diff --check`.

- [ ] **Step 7: Commit release materials**

```text
git add src installer install README.md CHANGELOG.md docs overlay/README.md
git commit -m release:prepare-fork-1.1.0
```

- [ ] **Step 8: Final clean-tree verification**

Run:

```text
git status --short
git log -12 --oneline
```

Expected: no uncommitted files; commit history contains the isolated feature slices and final 1.1.0 release commit. Do not push or create a GitHub Release unless the user explicitly requests publication after reviewing the verified local artifact.

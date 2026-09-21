# PoE 2 Trade Evaluation Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the unreliable 1.2.0 equipment-price path with correct modifier-family detection, profile-based Instant Buy searches, complete cross-currency valuation, and the approved Professional Compact no-focus UI.

**Architecture:** Keep the existing `overlay/PoeTradeOverlay` boundary. Convert the flat item/query representation into identity, requirements, properties, state flags, and typed modifier blocks; build one of three deterministic search profiles; fetch Trade results in chunks of ten; normalize every listing currency through selected-league economy data; and publish immutable state to the compact WPF window.

**Tech Stack:** .NET 10, C# 14, WPF, `System.Net.Http`, `System.Text.Json`, existing Newtonsoft.Json host code, xUnit 2.9.3, public Path of Exile 2 Trade data, existing poe.ninja snapshots, PowerShell/IExpress packaging.

**Spec:** `docs/superpowers/specs/2026-09-21-trade-evaluation-redesign.md`

## Global Constraints

- Modify only this repository and preserve `old/` and the Steam Path of Exile 2 installation unchanged.
- Never inspect or modify game files or memory, inject code, hook rendering, capture packets, access cookies/tokens, synthesize input, or automate trading.
- React only after the player's normal manual `Ctrl+C` while Path of Exile 2 owns foreground focus.
- Default detailed searches to Instant Buy (`securable`), fetch at most ten IDs per request, sample at most twenty listings, and honor rate limits.
- Preserve original listing currencies and never fabricate a missing conversion rate.
- Keep network and parsing work off the UI thread and keep the ground-loot OCR path unchanged.
- Ship Professional Compact at nominal width 420 DIP with passive mouse interaction and explicit keyboard-edit mode.
- Version the release as fork `1.3.0`; do not restore external auto-update.

## Test Execution Policy

To avoid repeated work and token waste, tests are written alongside each implementation batch but are not run after every file or micro-change. Execute exactly these checkpoints unless a checkpoint fails and a targeted rerun is necessary:

1. **Core checkpoint:** parser, metadata, query, client, currency, and estimator tests once after Batch 1.
2. **Interaction checkpoint:** controller, view-model, window-policy, placement, host, and performance tests once after Batch 2.
3. **Release checkpoint:** formatting, vulnerability audit, complete Release test suite, Release build, bounded live contract/focus checks, and installer verification once after all source work is complete.

Do not rerun a passing unchanged test group. On failure, run only the failing test class or exact test until fixed; the complete suite runs only at the release checkpoint.

## Review Focus

- Advanced headers can share text across families; ambiguous evidence must remain visible and unchecked.
- Broad -10% must relax positive, negative, inverted-benefit, and integer filters without reversing ranges.
- A second fetch chunk can fail after the first succeeds; valid partial listings must remain visible.
- Currency codes can lack a live rate; the original listing must remain while only conversion is unavailable.
- Passive WPF mouse actions must preserve game focus; keyboard focus is allowed only after explicit Edit.

---

### Batch 1: Rebuild parsing, modifier resolution, Trade queries, transport, and valuation

**Files:**
- Modify: `overlay/PoeTradeOverlay/Models/ParsedItem.cs`
- Modify: `overlay/PoeTradeOverlay/Models/TradeModels.cs`
- Modify: `overlay/PoeTradeOverlay/Abstractions/ICurrencyConverter.cs`
- Modify: `overlay/PoeTradeOverlay/Parsing/PoeItemTextParser.cs`
- Modify: `overlay/PoeTradeOverlay/Parsing/NumericText.cs`
- Modify: `overlay/PoeTradeOverlay/Trade/TradeMetadataParser.cs`
- Modify: `overlay/PoeTradeOverlay/Trade/ModifierMatcher.cs`
- Create: `overlay/PoeTradeOverlay/Trade/SearchProfileRules.cs`
- Modify: `overlay/PoeTradeOverlay/Trade/TradeQueryBuilder.cs`
- Modify: `overlay/PoeTradeOverlay/Trade/TradeQuerySerializer.cs`
- Modify: `overlay/PoeTradeOverlay/Trade/PathOfExileTradeClient.cs`
- Modify: `overlay/PoeTradeOverlay/Trade/RateLimitGuard.cs`
- Create: `overlay/PoeTradeOverlay/Pricing/CurrencyCatalog.cs`
- Modify: `overlay/PoeTradeOverlay/Pricing/PriceEstimator.cs`
- Modify: `src/PoeAncientsPriceHelper/PriceRepository.cs`
- Modify: `src/PoeAncientsPriceHelper/OverlayCurrencyConverter.cs`
- Create: `overlay/PoeTradeOverlay.Tests/Fixtures/heavy-belt-advanced.txt`
- Create: `overlay/PoeTradeOverlay.Tests/Fixtures/mixed-mod-families.txt`
- Modify: parser, metadata, matcher, query, client, rate-limit, and price tests under `overlay/PoeTradeOverlay.Tests/`
- Modify: `src/PoeAncientsPriceHelper.Tests/OverlayCurrencyConverterTests.cs`
- Modify: `src/PoeAncientsPriceHelper.Tests/PriceRepositoryTests.cs`

**Interfaces produced:**

```csharp
public sealed record ItemRequirements(int? Level, int? Strength, int? Dexterity, int? Intelligence);
public sealed record ItemStates(bool Identified, bool Corrupted, bool Mirrored,
    bool Fractured, bool Crafted, bool Desecrated);
public sealed record NumericRoll(decimal Current, decimal? RangeMin, decimal? RangeMax);
public sealed record ParsedProperty(string Key, string SourceText, IReadOnlyList<decimal> Values);
public sealed record ParsedModifierBlock(string Text, string? Header,
    ModifierKind DeclaredKind, IReadOnlyList<decimal> Values, IReadOnlyList<NumericRoll> Rolls);

public enum ResolutionStatus { Resolved, Unsupported, Ambiguous }
public sealed record ResolvedModifier(ResolutionStatus Status, string? StatId,
    ModifierKind Kind, string SourceText, string Explanation);

public enum SearchProfile { CraftingBase, QuickPrice, Broad }
public sealed record NumericRange(decimal? Min, decimal? Max);

public sealed record CurrencyConversion(string OriginalCurrency, string DisplayName,
    decimal OriginalAmount, decimal? ExaltedValue, CurrencyRateSource Source);
```

- [ ] **Step 1: Add the complete core regression fixtures and tests without running them yet**

Add tests covering:

```csharp
[Fact]
public void Heavy_belt_separates_requirements_and_resolves_both_implicits()
{
    Assert.True(PoeItemTextParser.TryParse(Fixture.Read("heavy-belt-advanced.txt"), out var item));
    Assert.Equal(50, item.Requirements.Level);
    Assert.DoesNotContain(item.Modifiers, x => x.Text.StartsWith("Requires", StringComparison.Ordinal));
    var resolved = item.Modifiers.Select(x => ModifierMatcher.Resolve(x, Metadata.Standard)).ToArray();
    Assert.Equal(["implicit.stat_680068163", "implicit.stat_1416292992"],
        resolved.Select(x => x.StatId));
}

[Fact]
public void Same_text_without_family_evidence_is_ambiguous()
{
    var result = ModifierMatcher.Resolve(HeavyBelt.WithoutHeader, Metadata.StunThresholdBothFamilies);
    Assert.Equal(ResolutionStatus.Ambiguous, result.Status);
    Assert.Null(result.StatId);
}

[Fact]
public void Crafting_base_query_uses_instant_buy_and_correct_domains()
{
    var query = TradeQueryBuilder.Create(HeavyBelt.Item, Metadata.Standard, SearchProfile.CraftingBase);
    var json = TradeQuerySerializer.Serialize(query);
    Assert.Contains("\"option\":\"securable\"", json);
    Assert.Equal(75, query.TypeFilters.ItemLevel?.Min);
    Assert.Equal(50, query.RequirementFilters.Level?.Max);
}

[Fact]
public async Task Twenty_result_ids_are_fetched_in_two_chunks_of_ten()
{
    var handler = TradeHttp.SearchWithIds(20).ThenFetch(10).ThenFetch(10);
    var result = await CreateClient(handler).SearchAsync("Forbidden Rites", Query.HeavyBelt, default);
    Assert.Equal([10, 10], handler.FetchRequests.Select(x => x.Ids.Count));
    Assert.Equal(20, result.Listings.Count);
}

[Fact]
public void Four_vaal_converts_with_the_live_rate_and_preserves_original_price()
{
    var catalog = CurrencyCatalog.FromEconomy(Economy.With("Vaal Orb", 6.309554m));
    var result = catalog.Convert("vaal", 4m);
    Assert.Equal("Vaal Orb", result.DisplayName);
    Assert.Equal(4m, result.OriginalAmount);
    Assert.Equal(25.238216m, result.ExaltedValue);
}
```

Also cover explicit, implicit, fractured, crafted, enchant, rune/augment, desecrated, pseudo, sanctum, and skill metadata; multi-line modifiers; current values versus roll ranges; no-header unique matches; negative/inverted/integer Broad relaxation; each query domain; invalid Min/Max; HTTP 400, 429, timeout, malformed response, partial second-chunk failure; direct Exalted plus mixed currencies; missing conversion; duplicate accounts; outliers; and conversion coverage confidence.

- [ ] **Step 2: Implement the structured parser and evidence-ordered modifier resolver**

Parse separator-delimited sections, associate brace headers with following modifier lines, model requirements/properties/states separately, and preserve current values plus roll ranges. Expand `ModifierKind` for every family exposed by Trade metadata. Preserve both metadata group and entry type; map `augment` to Rune. Resolve declared family first, then structural/base evidence, then a unique compatible metadata candidate. Return `Ambiguous` rather than choosing the first duplicate.

- [ ] **Step 3: Implement the three deterministic search profiles and four query domains**

`CraftingBase` selects base identity, item level, maximum requirement, intrinsic properties, and implicits. `QuickPrice` selects a bounded item-class-aware set of confidently resolved relevant properties/modifiers. `Broad` clones Quick Price and relaxes bounds ten percent through a pure direction-aware rule. Serialize identity, type, equipment, requirement, stat, and miscellaneous filters into their correct Trade groups; omit unsupported/ambiguous/disabled filters and use status `securable`.

- [ ] **Step 4: Implement ten-ID fetch chunks and typed partial failures**

Set `FetchChunkSize = 10` and `MaxSampleSize = 20`. Search once, fetch sequential chunks, observe rate headers after every response, combine successes, and preserve the first chunk with `TradeFailureKind.PartialFetch` if the second transiently fails. Map HTTP 400 to `InvalidQuery`, stop immediately on 429, and never retry automatically.

- [ ] **Step 5: Implement the league currency catalog and cross-currency estimator**

Extend the immutable host price snapshot with canonical economy ID/display-name lookup while preserving the existing ground-loot dictionary. Resolve Trade codes through a small canonical bootstrap map plus the selected-league live catalog. Missing rates return `ExaltedValue=null`. Retain every original priced listing for display, use only converted listings for statistics, collapse duplicate accounts, and include conversion coverage in confidence.

- [ ] **Step 6: Run the core checkpoint once**

Run:

```text
dotnet test PoeAncientsPriceHelper.sln --filter "PoeItemTextParserTests|ContractTests|TradeMetadataCatalogTests|ModifierMatcherTests|TradeQueryBuilderTests|TradeQuerySerializerTests|PathOfExileTradeClientTests|RateLimitGuardTests|CurrencyCatalogTests|PriceEstimatorTests|OverlayCurrencyConverterTests|PriceRepositoryTests"
```

Expected: every listed core test passes. If anything fails, rerun only the exact failing test until corrected; do not rerun the full core checkpoint after unrelated fixes.

- [ ] **Step 7: Commit the complete core batch**

```text
git add overlay/PoeTradeOverlay overlay/PoeTradeOverlay.Tests src/PoeAncientsPriceHelper/PriceRepository.cs src/PoeAncientsPriceHelper/OverlayCurrencyConverter.cs src/PoeAncientsPriceHelper.Tests
git commit -m feat:rebuild-poe2-trade-evaluation-core
```

### Batch 2: Build Professional Compact and stabilize interaction/orchestration

**Files:**
- Modify: `overlay/PoeTradeOverlay/Presentation/TradeOverlayState.cs`
- Modify: `overlay/PoeTradeOverlay/Presentation/TradeOverlayViewModel.cs`
- Modify: `overlay/PoeTradeOverlay/Presentation/FilterRowViewModel.cs`
- Modify: `overlay/PoeTradeOverlay/Presentation/ListingRowViewModel.cs`
- Modify: `overlay/PoeTradeOverlay/Presentation/TradeOverlayWindow.xaml`
- Modify: `overlay/PoeTradeOverlay/Presentation/TradeOverlayWindow.xaml.cs`
- Modify: `overlay/PoeTradeOverlay/Presentation/WindowActivationPolicy.cs`
- Modify: `overlay/PoeTradeOverlay/Presentation/ScreenPlacement.cs`
- Modify: `overlay/PoeTradeOverlay/TradeOverlayController.cs`
- Modify: `src/PoeAncientsPriceHelper/TradeOverlayHost.cs`
- Modify: `src/PoeAncientsPriceHelper/MainWindow.xaml.cs`
- Modify: presentation/controller tests under `overlay/PoeTradeOverlay.Tests/`
- Modify: `src/PoeAncientsPriceHelper.Tests/TradeOverlayHostTests.cs`
- Create: `overlay/PoeTradeOverlay.Tests/PassiveWindowDragTests.cs`
- Create: `overlay/PoeTradeOverlay.Tests/TradeOverlayPerformanceTests.cs`

**Interfaces produced:** Bindable profile selection, mouse-based clear/step controls, typed status panels, original-plus-equivalent listing rows, passive title-bar movement, explicit `BeginEditing`/`EndEditing`, and one cancelable controller generation.

- [ ] **Step 1: Add the complete interaction regression tests without running them yet**

Include:

```csharp
[Fact]
public void Clear_min_removes_only_the_bound_and_keeps_query_valid()
{
    var vm = ViewModel.For(HeavyBelt.State);
    var row = vm.Filters.Single(x => x.Label.Contains("Stun Threshold"));
    row.ClearMin.Execute(null);
    Assert.Null(row.Min);
    Assert.True(vm.CanSearch);
}

[Fact]
public void Passive_drag_calculates_no_activate_target()
{
    var movement = PassiveWindowMovement.FromDrag(new(100,100), new(140,125), new(900,80));
    Assert.Equal(new Point(940,105), movement.Target);
    Assert.True(movement.UseNoActivate);
}

[Fact]
public async Task New_copy_cancels_old_search_and_blocks_stale_publication()
{
    var harness = ControllerHarness.WithDelayedFirstSearch();
    await harness.CopyAsync(Fixture.Read("heavy-belt-advanced.txt"));
    await harness.CopyAsync(Fixture.Read("rare-bow.txt"));
    harness.CompleteFirstSearch(Result.HeavyBelt);
    Assert.Equal("Storm Song", harness.View.Current.Item.Name);
}
```

Cover profile changes without requests, first-copy single request, explicit Search single request, clear/step/mouse-wheel Min/Max, invalid bounds, family captions, no-results Broad suggestion, HTTP 400, 429 countdown, partial sample, missing conversion, multi-monitor clamping, remembered off-screen position, explicit Edit focus, passive restoration, and cached Heavy Belt parse/classify/query median under 15 ms on this machine.

- [ ] **Step 2: Implement immutable presentation mapping and local filter commands**

Map the new query/estimate/error contracts into view state. Profile selection and Min/Max edits stay local. Provide clear, step, and validation commands. Format original listing price and optional Exalted equivalent separately. Distinguish no matches, invalid query, rate limit, network, partial fetch, stale metadata, and conversion unavailable.

- [ ] **Step 3: Replace the XAML with approved Professional Compact**

Use width 420 DIP, a 34-DIP full-width title bar, centered identity, three profile buttons, dense filter rows, estimate card, currency/listing/age controls, compact listing table, and status/Search footer. Follow the approved eight-pixel section rhythm and dark-charcoal/muted-gold/parchment/restrained-green palette. Bound modifier and listing height with scrolling instead of expanding off-screen.

- [ ] **Step 4: Implement passive mouse interaction and deliberate Edit mode**

Replace `DragMove` with manual cursor-delta movement through `SetWindowPos(..., SWP_NOACTIVATE)` from the entire title bar. Keep checkboxes, profiles, Search, Close, clear, and step controls mouse-operable while `WM_MOUSEACTIVATE` returns `MA_NOACTIVATE`. `BeginEditing` changes styles and activates only after the explicit click; Done/Escape restores passive flags. Clamp and persist position per work area.

- [ ] **Step 5: Migrate the controller to profile generations and sanitized diagnostics**

On each valid copy, cancel the previous CTS, increment generation, parse/classify/build from cached metadata, publish immediately, and perform one initial profile search. Check cancellation/generation after every await. Profile changes never search; Search submits exactly once. Diagnostics contain only operation, generation, item-class/base hash, family/status IDs, counts, query-shape counts, status codes, timings, rate state, and cache age—never raw item text, seller/account data, listing IDs, or response bodies.

- [ ] **Step 6: Run the interaction checkpoint once**

Run:

```text
dotnet test PoeAncientsPriceHelper.sln --filter "TradeOverlayViewModelTests|WindowActivationPolicyTests|ScreenPlacementTests|PassiveWindowDragTests|TradeOverlayControllerTests|TradeOverlayHostTests|TradeOverlayPerformanceTests"
dotnet build overlay/PoeTradeOverlay/PoeTradeOverlay.csproj -c Debug --no-restore
```

Expected: all listed tests pass and the overlay library builds with zero warnings/errors. Rerun only an exact failing test after a fix.

- [ ] **Step 7: Render and inspect one Heavy Belt debug preview**

Launch the existing debug preview path once with the Heavy Belt fixture, save one screenshot under ignored `.artifacts/`, and compare hierarchy, spacing, clipping, scroll bounds, contrast, and displayed values against the approved Professional Compact mockup. Make visual corrections before committing; do not rerun unchanged automated tests.

- [ ] **Step 8: Commit the complete interaction batch**

```text
git add overlay/PoeTradeOverlay/Presentation overlay/PoeTradeOverlay/TradeOverlayController.cs overlay/PoeTradeOverlay.Tests src/PoeAncientsPriceHelper/TradeOverlayHost.cs src/PoeAncientsPriceHelper/MainWindow.xaml.cs src/PoeAncientsPriceHelper.Tests
git commit -m feat:ship-professional-compact-trade-overlay
```

### Batch 3: Finish release 1.3.0, perform final verification once, and package

**Files:**
- Modify: `src/PoeAncientsPriceHelper/PoeAncientsPriceHelper.csproj`
- Modify: `src/PoeAncientsPriceHelper/MainWindow.xaml`
- Modify: project-owned User-Agent strings and tests
- Modify: `installer/Build-Installer.ps1`
- Modify: `installer/Install.ps1`
- Modify: `installer/Verify-Installer.ps1`
- Modify: `README.md`
- Modify: `CHANGELOG.md`
- Modify: `docs/ARCHITECTURE.md`
- Modify: `docs/PROJECT_STRUCTURE.md`
- Modify: `docs/DEVELOPMENT.md`
- Modify: `overlay/README.md`
- Create: `docs/releases/1.3.0.md`
- Create: `install/Poe2GroundLootPriceHelper-v1.3.0-Setup.exe`

**Produces:** A fully documented, verified, self-contained 1.3.0 installer with no upstream auto-update.

- [ ] **Step 1: Update all version, installer, verification, and documentation surfaces**

Set application/package/UI/installer/uninstall/User-Agent versions to `1.3.0`. Make installer verification require file/product version `1.3.0.0`, overlay payload, and absence of `Update.exe`, Squirrel update metadata, external updater executables, and configured upstream update URLs. Document structured parsing, modifier families, profiles, ten-ID chunks, mixed-currency valuation, Professional Compact, Borderless recommendation for typing, caches/diagnostics, provenance, and the safety boundary.

- [ ] **Step 2: Run the single automated Release checkpoint**

Run once:

```text
dotnet format PoeAncientsPriceHelper.sln --verify-no-changes
dotnet list PoeAncientsPriceHelper.sln package --vulnerable --include-transitive
dotnet test PoeAncientsPriceHelper.sln -c Release
dotnet build PoeAncientsPriceHelper.sln -c Release --no-restore
```

Expected: formatting passes, no vulnerable packages are reported, the entire test suite passes, and the Release build has zero warnings/errors. If a command fails, run only the affected exact formatter/project/test until fixed, then continue; do not restart all four commands unless the fix changes their domain.

- [ ] **Step 3: Perform one bounded live Heavy Belt and focus check**

With `Forbidden Rites` selected and PoE 2 in Borderless Windowed, manually copy the Heavy Belt once. Verify correct implicit IDs, one `securable` search, no fetch chunk over ten IDs, original currencies, Exalted conversions, approximate market agreement with PoE Overlay II under the same basis, no stale results, passive Search/title drag/mouse controls preserving game focus, explicit Edit typing, and Done restoring passive mode. Record only sanitized counts, rates, timing, focus handles, and rate-limit state under ignored `.artifacts/`; do not access game files, memory, packets, or credentials.

- [ ] **Step 4: Build and verify the installer once**

Run:

```text
powershell -NoProfile -ExecutionPolicy Bypass -File installer/Build-Installer.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File installer/Verify-Installer.ps1 -Version 1.3.0
Get-FileHash install/Poe2GroundLootPriceHelper-v1.3.0-Setup.exe -Algorithm SHA256
Get-Item install/Poe2GroundLootPriceHelper-v1.3.0-Setup.exe | Select-Object Name,Length,LastWriteTime
```

Expected: build and payload verification pass and exact checksum/size values are printed. Insert only those measured values and actual verification results into `docs/releases/1.3.0.md`. Documentation-only checksum insertion does not require rebuilding or rerunning tests.

- [ ] **Step 5: Commit release materials and inspect final state without rerunning tests**

```text
git add src overlay installer install/Poe2GroundLootPriceHelper-v1.3.0-Setup.exe README.md CHANGELOG.md docs
git commit -m release:prepare-fork-1.3.0
git diff --check HEAD~1 HEAD
git status --short
git log -8 --oneline
```

Expected: the release commit is present, its diff has no whitespace errors, and the working tree is clean. Publish only to the configured `TonChaiya/poe2-PoeAncientsPriceHelper-main` repository if the existing publication request is still in scope and authentication is available; never publish to the upstream repository.

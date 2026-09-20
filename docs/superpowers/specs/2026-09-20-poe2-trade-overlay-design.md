# PoE 2 Detailed Trade Overlay Design

**Status:** Approved design

**Date:** 2026-09-20

**Target fork version:** 1.1.0

**Upstream base:** PoeAncientsPriceHelper 3.7.1

## Purpose

Add an on-demand detailed price-check window to the existing application. The feature is intended for equipment whose value depends on its modifiers, similar to the filtering workflow familiar from PoE Overlay. It complements rather than replaces the existing full-screen ground-loot scanner and its poe.ninja price data.

The player hovers an item in Path of Exile 2 and presses `Ctrl+C` once. Path of Exile performs its normal copy action. The application observes that completed copy, parses the new clipboard item text, and opens a filter window. The application never synthesizes keyboard or mouse input.

## Safety and non-goals

The feature must not:

- modify, replace, or inspect Path of Exile 2 installation files;
- read or write game process memory;
- inject a DLL, hook DirectX, capture network packets, or connect to game servers;
- automate movement, combat, trading, whispering, purchasing, or repeated input;
- synthesize `Ctrl+C` or any other input into the game;
- copy code, credentials, cookies, tokens, or private services from PoE Overlay;
- require `POESESSID`, an OAuth token, browser cookies, or account credentials;
- continuously search based on OCR, timers, screen changes, or dropped labels;
- upload screenshots, OCR output, configuration, clipboard history, or machine telemetry.

The application will include the visible notice: “This product isn't affiliated with or endorsed by Grinding Gear Games in any way.” Third-party tools cannot promise zero account risk; this design minimizes exposure by using the game's normal manual copy action, public market information, and no game automation.

## Scope

### Supported by the detailed overlay

The first release targets items whose price depends on searchable properties or modifiers:

- normal, magic, rare, and unique weapons;
- normal, magic, rare, and unique armour;
- quivers, shields, and foci;
- rings, amulets, and belts;
- jewels and charms;
- other equippable item classes represented by the current Trade item/stat catalog.

Support is capability-based rather than a permanently hard-coded list: an item is eligible when the parser identifies a Trade-supported equipment type and can build a valid query. Unknown types fail closed and do not generate a request.

### Kept on the existing price path

Currency, fragments, gems, and other fixed-price commodities continue to use the existing poe.ninja-backed ground-loot price system. Copying one of these items must not open the detailed modifier filter window. Waystones without meaningful searchable modifiers also remain on the existing price path; a future version may add a dedicated waystone search workflow.

### Explicitly deferred

- authenticated or account-specific Trade features;
- automatic seller whisper or trade-site interaction;
- live-search subscriptions;
- price prediction or machine learning;
- automatic valuation from screenshots or ground-label OCR;
- bulk item scanning;
- console-platform support.

## Repository and project structure

The feature belongs to this repository and ships inside the existing executable, but its implementation is isolated in a root-level `overlay/` subtree:

```text
overlay/
  PoeTradeOverlay/
    PoeTradeOverlay.csproj
    Clipboard/
    Parsing/
    Trade/
    Pricing/
    Presentation/
    Storage/
  PoeTradeOverlay.Tests/
    PoeTradeOverlay.Tests.csproj
```

`PoeTradeOverlay` is a `net10.0-windows10.0.19041.0` WPF class library referenced by `src/PoeAncientsPriceHelper/PoeAncientsPriceHelper.csproj`. The main application owns process lifetime and the existing global keyboard hook. The overlay library owns clipboard interpretation, Trade requests, valuation, its window, and its private cache. This boundary prevents Trade failures or UI work from entering the ground-loot OCR loop.

The test project references only the overlay library. Pure parser, query, rate-limit, and valuation code must remain independently testable without starting WPF or a keyboard hook.

## Component responsibilities

### Main application integration

The existing SharpHook keyboard hook detects release of the exact `Ctrl+C` chord. It forwards a notification only when:

- Path of Exile 2 is the foreground game window, using the existing `GameWindow` boundary;
- hotkey capture/rebinding is not active;
- the detailed overlay feature is enabled in settings.

The hook does not suppress the key and does not send replacement input. The integration creates and disposes one `TradeOverlayController` with the main application. League changes update the controller's active league. Application shutdown cancels requests and closes the overlay cleanly.

### Clipboard reader

Clipboard contents may be committed after the key-release event. A dispatcher-based reader performs a small bounded sequence of delayed reads after the observed `Ctrl+C`, stopping as soon as it sees new text. This is event-triggered work, not continuous clipboard polling.

The reader:

- never clears or rewrites the clipboard;
- ignores text identical to the last handled clipboard fingerprint;
- accepts only a complete PoE 2 item-text structure;
- keeps raw text in memory only for the active check;
- does not log raw clipboard text in normal or debug logs.

### Item parser

The parser produces an immutable `ParsedItem` containing:

- rarity, display name, base type, and item class;
- item level, quality, sockets, and corruption state;
- numeric properties such as damage, attacks per second, armour, evasion, energy shield, and spirit when present;
- separated implicit, explicit, enchant, rune, and other recognized modifier lines;
- normalized numeric ranges and localization-safe raw labels where available.

Parsing is conservative. If required separators or identity fields are missing, the result is rejected before any network call. Parsing must not infer a different base type merely to obtain results.

### Trade metadata catalog

`TradeMetadataCatalog` downloads the public Trade item, stat, and filter definitions needed to map parsed text into stable identifiers. A last-known-good copy is stored under `%LOCALAPPDATA%\PoeAncientsPriceHelper\overlay\` using atomic replacement. Startup does not block on a refresh when a valid cache exists.

Definitions are refreshed at a low frequency and only after an explicit item check requires them. A malformed response never replaces a valid cache.

### Query builder

The query builder creates the smallest valid query for the active Path of Exile 2 league. Initial recommended selections include:

- correct item category and base type;
- rarity or unique identity where applicable;
- corruption state when price-relevant;
- item level and quality only when materially useful;
- individually selectable recognized modifiers;
- calculated equipment properties where the Trade schema supports them.

Each filter records whether it is enabled, its parsed value, and editable minimum/maximum bounds. Unsupported modifier lines remain visible as unsupported and are never silently translated to an unrelated stat.

### Trade client

`ITradeClient` isolates all Path of Exile Trade HTTP behavior from parsing and presentation. The initial adapter uses the public Path of Exile Trade search/fetch surface without account credentials. It sends a descriptive project/version/contact User-Agent and the non-affiliation notice appears in the application.

The adapter:

- sends no request until a valid manually copied item exists;
- performs one initial search for that copied item with recommended filters;
- fetches only the bounded result set needed for valuation;
- parses every applicable `X-Rate-Limit-*` header;
- honors `Retry-After` and never bypasses a restriction;
- rejects redirects or challenges that would require account credentials;
- has strict connection and response timeouts;
- exposes typed errors instead of Trade-specific JSON to consumers.

The Trade website search surface is not guaranteed by the documented developer API to remain stable. For that reason, endpoint paths, request DTOs, and response DTOs stay entirely inside the adapter. If anonymous access stops working, the feature reports that Trade search is unavailable; it must not scrape browser state or ask for a password.

### Price estimator

The estimator receives fetched listings and produces:

- total matching listings reported by search;
- number of usable listings sampled;
- lowest credible price;
- a typical price range;
- median normalized price;
- original listing currencies;
- confidence level and a short reason.

Listings without a price are excluded. Duplicate listings from the same account are collapsed for estimation. Extreme outliers are excluded only by a deterministic, tested rule and remain counted in the raw match total. Currency normalization uses existing poe.ninja exchange values exposed through a narrow application-provided interface. A value is never fabricated when a conversion rate is missing.

Confidence is based on sample size, price dispersion, conversion coverage, and filter specificity. The UI clearly labels the result as a market estimate rather than a guaranteed sale price.

### Overlay presentation

The detailed price window opens near a safe screen edge without covering the hovered item when practical. It is independent of `PriceOverlay`, which continues to draw ground-label badges.

The window shows:

- item identity and a short “Item read” acknowledgement;
- editable filter rows with enable/disable controls;
- numeric minimum and maximum fields where supported;
- loading, results, rate-limit, unavailable, and validation states;
- match count, usable sample count, lowest credible price, typical range, median, currencies, and confidence;
- a `Search price` button for queries after filter edits;
- close and feature-disable controls.

The first valid `Ctrl+C` starts one search using recommended filters. Editing filters never sends requests automatically; the player presses `Search price`. Copying another supported item cancels the previous pending operation and replaces the window contents only after the new item is parsed.

The UI does not contain seller-contact, whisper, purchase, or automated browser actions in version 1.1.0.

## Data flow

1. The player hovers an item and manually presses `Ctrl+C` in focused PoE 2.
2. The existing keyboard hook observes the released chord without suppressing or replacing it.
3. `TradeOverlayController` performs bounded delayed clipboard reads.
4. The parser validates and classifies the copied item.
5. Commodity or unsupported items exit without opening the detailed window or calling Trade.
6. A supported item opens the filter window with recommended selections.
7. The catalog resolves item and modifier identifiers from cached/refreshed metadata.
8. The controller cancels any older request and submits one search for the active league.
9. The client fetches a bounded listing sample while tracking server rate limits.
10. The estimator normalizes usable prices and builds a confidence-qualified estimate.
11. The controller publishes the result to the active item window only if its request generation is still current.
12. Subsequent filter edits remain local until the player clicks `Search price`.

## Concurrency and performance

Clipboard parsing, metadata refresh, HTTP, and valuation run outside the UI thread. Only immutable view models cross into WPF presentation. At most one active Trade search/fetch pipeline is allowed per application instance. A new copied item or application shutdown cancels the previous pipeline.

The feature must add no timer to the OCR scan path, no screen capture, and no continuous clipboard polling. When disabled or idle, its only runtime cost is the exact-chord comparison inside the existing keyboard event handler. Network concurrency is bounded and response bodies have explicit size limits.

Short-lived caches may reuse:

- metadata definitions across sessions;
- exact item/filter query results for a brief period;
- currency conversions from the current `PriceRepository` snapshot.

The cache stores no account identity and no raw clipboard history.

## Error handling

- **Clipboard unavailable:** retry only within the bounded post-copy window, then stop silently.
- **Invalid or incomplete item text:** do not open the window and do not call Trade.
- **Supported item with unmapped modifiers:** show those lines as unsupported and search only explicitly mapped enabled filters.
- **No results:** show the active filters and invite the player to loosen them; do not auto-repeat.
- **Timeout or network failure:** retain the parsed filter window, show a retryable error, and wait for a player action.
- **HTTP 429:** disable searching until `Retry-After` expires and display the remaining wait; no automatic retry.
- **Invalid-request response:** show a non-retrying query error and record sanitized structural diagnostics without raw item text.
- **Malformed metadata/response:** retain last-known-good data and fail the active operation without affecting the ground-loot scanner.
- **League change:** cancel the active query, clear result caches scoped to the old league, and use the newly selected league thereafter.

## Settings and privacy

Version 1.1.0 adds an opt-in/out setting for detailed clipboard price checks. It is enabled by default after upgrade because it reacts only to a manual `Ctrl+C` while PoE 2 is focused. The settings page explains that supported item text is sent to Path of Exile Trade only as a structured search query and that no credentials are used.

Local cache location:

```text
%LOCALAPPDATA%\PoeAncientsPriceHelper\overlay\
```

The cache contains metadata, anonymous short-lived query results, and rate-limit state only. It contains no raw clipboard history, screenshots, authentication material, character information, or telemetry identifiers.

## Testing strategy

### Unit tests

- representative normal, magic, rare, and unique item parsing;
- weapons, armour, accessories, jewels, and charms;
- separator, numeric range, decimal, quality, item-level, corruption, and modifier classification cases;
- rejection of arbitrary clipboard text and incomplete items;
- category capability checks that keep commodities on the existing path;
- stable stat mapping and explicit unsupported-mod behavior;
- minimal query generation and league routing;
- response parsing, duplicate-account handling, currency conversion, median/range calculation, outlier rules, and confidence;
- rate-limit header parsing, `Retry-After`, cancellation, cache key isolation, and stale-generation protection.

Fixtures contain synthetic or publicly documented item text and no account identifiers.

### Integration tests

A fake `HttpMessageHandler` verifies complete search/fetch flows without hitting live services. A clipboard abstraction verifies delayed update, unchanged fingerprint, focus gating, and cancellation. Live Trade calls are excluded from the normal automated test suite to avoid consuming service capacity or creating unstable builds.

### Manual verification

- `Ctrl+C` outside PoE 2 does nothing;
- `Ctrl+C` over supported items opens exactly one window and performs one initial search;
- commodities do not open the detailed window;
- changing filters does not search until the button is pressed;
- repeated copying does not leave stale results;
- `429`, offline, and timeout states recover only through permitted player action;
- the ground-loot OCR overlay retains its current cadence and responsiveness;
- no game file, game memory, input synthesis, seller action, or credential access occurs.

## Documentation, versioning, and packaging

This backward-compatible substantial capability increments the independent fork from `1.0.0` to `1.1.0`. Implementation updates:

- the application project version and visible version label;
- installer metadata and output filename;
- `CHANGELOG.md`;
- `docs/ARCHITECTURE.md` and `docs/PROJECT_STRUCTURE.md`;
- privacy/network documentation in the main README where applicable;
- immutable `docs/releases/1.1.0.md` with provenance, features, verification, installer name, and checksum.

The existing installer includes the referenced overlay library with the main application. It does not install a second executable, browser extension, service, driver, game modification, or auto-updater.

## Acceptance criteria

The implementation is complete when all of the following hold:

1. A manual `Ctrl+C` over a supported item while PoE 2 is focused opens the integrated detailed filter window without synthesized input.
2. The parsed filters accurately represent the copied item and unsupported lines are visible rather than misidentified.
3. The selected application league is used for every search.
4. One initial query occurs per new supported item; edited filters query only after the player presses the search button.
5. Results show sample size, credible low, typical range, median, currencies, and confidence.
6. Rate limits, cancellation, timeouts, malformed responses, and unavailable anonymous Trade access fail safely.
7. Currency/gem/fixed-price handling and the ground-loot OCR path retain existing behavior and performance.
8. The application never modifies or inspects game files or memory, synthesizes input, accesses credentials, or automates a trade action.
9. Automated tests pass, the Release build succeeds without warnings, and the installer verification succeeds.
10. Documentation and the immutable 1.1.0 release record describe the final implementation accurately.

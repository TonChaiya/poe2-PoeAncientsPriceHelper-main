# PoE 2 Trade Evaluation Redesign

**Status:** Approved design

**Date:** 2026-09-21

**Target fork version:** 1.3.0

**Upstream base:** PoeAncientsPriceHelper 3.7.1

**Supersedes:** The parser, query-building, pricing, and presentation details in
`2026-09-20-poe2-trade-overlay-design.md`. Its safety, privacy, and integration boundaries remain in force.

## Purpose and success criteria

Make the detailed Trade evaluation useful for real Path of Exile 2 equipment. A manually copied item must be classified correctly, searched in the selected league, valued across listing currencies, and displayed in a compact professional window comparable in information hierarchy to PoE Overlay II.

The redesign succeeds when:

- modifier families and ordinary item properties are separated without guessing;
- the Heavy Belt reference item returns live results instead of zero matches;
- Instant Buy listings in different currencies contribute to one Exalted-equivalent estimate;
- Min/Max controls, filter toggles, search, and window dragging are usable without unexpectedly minimizing the game;
- the feature remains an external, passive companion with no game-file access, process-memory access, injection, packet capture, credential access, input synthesis, or trade automation;
- network, parsing, and pricing work stays off the UI thread and does not slow the existing ground-loot scanner.

## Verified defects motivating the redesign

The current 1.2.0 implementation has several independently verified faults:

1. Modifier lines without a trailing type marker default to `explicit`. Heavy Belt's base modifiers are therefore sent as explicit stats even though Trade identifies them as implicit stats. The wrong query returns zero matches; the corrected implicit query returns live matches.
2. Advanced-copy metadata such as `Requires: Level 50` is not modeled and appears as an unsupported modifier.
3. Every matched modifier is enabled in one `and` group with its current roll as the minimum. This over-constrains many searches and provides no base, quick, or broad search intent.
4. The Trade fetch client asks for up to 20 IDs in one request. The observed PoE 2 fetch endpoint rejects that request with HTTP 400; ten IDs per fetch succeeds.
5. The default search uses in-person `online` listings instead of the Instant Buy market represented by Trade status `securable`.
6. Currency conversion recognizes only a small hand-written alias set. Trade code `vaal` is not mapped to Vaal Orb, so valid Vaal-priced listings are discarded from valuation.
7. The no-activate window makes text fields intentionally unusable until editing is enabled. Dragging is restricted to the title text and editing mode, so the window feels immovable and broken.
8. Search errors are collapsed into a sparse state that does not explain whether the cause is no matches, an invalid query, a fetch-size error, missing conversion, rate limiting, or service failure.

The reference Heavy Belt query demonstrated the expected cross-currency behavior: an Instant Buy listing of 4 Vaal at the observed rate of approximately 6.3096 Exalted per Vaal converts to approximately 25.24 Exalted, close to the approximately 27 Exalted estimate shown by PoE Overlay II. A forced Exalted-only market sample produced a different median, confirming that valuation must not discard or artificially force listing currencies.

## Safety and non-goals

The implementation retains the strict safety boundary from the original overlay design:

- react only after the player manually performs the game's normal `Ctrl+C` copy;
- never suppress or synthesize keyboard or mouse input;
- never modify or inspect Path of Exile installation files;
- never read or write game-process memory, inject code, hook rendering, or capture packets;
- never read browser cookies, `POESESSID`, OAuth tokens, or credentials;
- never automate seller contact, purchasing, whispering, or repeated searches;
- never copy proprietary PoE Overlay II implementation code.

Local inspection of PoE Overlay II is limited to observable behavior and its public application manifest. That manifest confirms that its editable evaluation window uses Overwolf's in-game window and keyboard-focus facility. A standalone WPF window cannot reproduce that compositor integration in exclusive fullscreen. This application will instead provide passive mouse interaction and a deliberate keyboard-edit mode, with Borderless Windowed recommended when typing values.

## Architecture

The existing `overlay/PoeTradeOverlay` library remains the subsystem boundary. The redesign introduces focused models and services within that library rather than moving Trade logic into the host application.

```text
Manual Ctrl+C
    -> clipboard reader
    -> structured item parser
    -> modifier/property classifier
    -> metadata resolver
    -> search-profile builder
    -> Trade search + chunked fetch
    -> currency resolver
    -> price estimator
    -> compact presentation state
```

The host application continues to provide the active league, foreground-game check, current poe.ninja economy snapshot, lifecycle, and settings. It does not learn Trade request DTOs or modifier parsing rules.

### Structured item model

`ParsedItem` becomes a structured description rather than a flat modifier list. It contains:

- identity: item class, rarity, name, and base type;
- requirements: level, strength, dexterity, and intelligence;
- base/search properties: item level, quality, sockets, armour, evasion, energy shield, spirit, damage, attack speed, and other Trade-supported properties;
- state flags: identified, corrupted, mirrored, fractured, crafted, desecrated, and similar published flags when present;
- ordered modifier blocks with source text, current values, displayed roll ranges, advanced-copy header, and resolved modifier family;
- unclassified lines retained visibly for diagnostics but excluded from queries.

Properties and requirements are never represented as modifier stats.

### Modifier classification

The parser preserves line blocks and associates an Advanced Mod Description header with the following modifier line or lines. Classification evidence is applied in this order:

1. an explicit advanced-copy header, including prefix/suffix, implicit, fractured, crafted, enchant, rune/augment, desecrated, or other published family;
2. structural position within the copied item and separator boundaries;
3. the item's base definition and exact candidates in the current Trade metadata;
4. a unique metadata match when only one compatible family exists.

The resolver supports the families currently exposed by Trade metadata: `pseudo`, `explicit`, `implicit`, `fractured`, `crafted`, `enchant`, `rune/augment`, `desecrated`, `sanctum`, and `skill`. The metadata layer must preserve both the result-group ID and entry `type`; it must not collapse an unknown type to explicit.

When the same normalized text exists in multiple families and the evidence does not distinguish them, the line is marked ambiguous. An ambiguous line remains visible and unchecked and is never silently sent as the first matching stat.

For the Heavy Belt reference:

- `30% increased Stun Threshold` resolves to `implicit.stat_680068163`;
- `Has 1 Charm Slot` resolves to `implicit.stat_1416292992`;
- `Requires: Level 50` becomes a level requirement;
- `Item Level: 75` becomes a type-filter property.

### Search profiles

The UI exposes three mutually exclusive profiles. Changing profile rebuilds recommended selections locally and does not search until the player presses Search, except for the first automatic query after a new item is copied.

#### Crafting Base

Designed for normal or otherwise modifiable bases. It selects:

- base type and category;
- relevant rarity/state flags;
- item level and maximum requirement when useful;
- base implicits and intrinsic properties;
- crafting-relevant socket or defence properties.

It does not select unrelated explicit affixes merely because they exist on the copied item.

#### Quick Price

Designed for rare, magic, and unique equipment valuation. It selects identity and a bounded set of meaningful, confidently resolved modifiers and properties. Selection rules are deterministic and item-class aware. Unsupported or ambiguous lines are visible but off. It avoids enabling every modifier in a single exact `and` query.

#### Broad -10%

Starts from Quick Price's selected filters and reduces positive minimum thresholds by ten percent using direction-aware numeric handling. Negative or inverted-benefit stats must use explicit tested rules rather than naïve multiplication. Integer-only filters are rounded conservatively. The UI shows both the copied value and effective search value.

### Query construction

The query model represents four separate filter domains:

- identity and category;
- type filters such as rarity, item level, and quality;
- equipment and requirement filters;
- typed stat filters and miscellaneous state flags.

The default listing mode is Instant Buy (`securable`). Search payloads use the selected league verbatim after URI escaping. The serializer emits only enabled, supported filters and rejects invalid ranges before any network request.

The result IDs are fetched in chunks of no more than ten. The first release samples at most twenty listings through two sequential fetches, stopping earlier when fewer IDs exist or a rate-limit state prohibits another request. Results are combined and deduplicated for estimation. A new copied item cancels the previous generation so an older response cannot overwrite the new window.

## Currency normalization and valuation

Trade listing currency codes must resolve through a metadata-backed currency catalog, not a small hard-coded alias list. The resolver combines:

- well-known canonical Trade codes needed for bootstrap and offline tests;
- current poe.ninja exchange item IDs/names and rates for the selected league;
- normalized display-name aliases derived from that live catalog.

The result retains both the original amount/currency and its optional Exalted equivalent. Missing conversion rates do not remove the listing from the table; they exclude only that listing from the converted estimate and visibly show `conversion unavailable`.

Valuation:

- uses Instant Buy listings by default;
- collapses repeated listings from one account to that account's lowest equivalent price;
- keeps original currencies visible;
- applies the existing deterministic outlier rule only after conversion;
- reports match count, usable converted sample count, lowest credible value, interquartile typical range, median, and confidence;
- includes conversion coverage in confidence;
- never invents a converted value.

The Heavy Belt regression fixture must demonstrate both direct-Exalted and Vaal-priced samples. A 4 Vaal listing must convert using the injected league rate, and the displayed result must include `4 Vaal` alongside its Exalted equivalent.

## Approved presentation: Professional Compact

The approved layout is the `Professional Compact` mockup at a nominal width of 420 device-independent pixels. Height is content-aware with a bounded scroll region for modifiers and listings. Spacing follows an eight-pixel rhythm, with compact four- and six-pixel internal gaps where dense rows require them.

Visual hierarchy, top to bottom:

1. 34-pixel draggable title bar with product context, settings/edit affordance, and close button;
2. centered item name and a concise identity row for rarity, item level, requirement, and state;
3. profile selector: Crafting Base, Quick Price, Broad -10%;
4. modifier/property rows with checkbox, label, family/evidence caption, Min, and Max;
5. bordered estimate card with Exalted-equivalent value, typical range, match/sample count, and confidence;
6. currency, listing mode, and age controls;
7. compact listing table showing original price, item level, account, and age;
8. league/metadata status and primary Search button.

The UI follows the PoE-inspired dark charcoal, muted gold, parchment, and restrained green palette shown in the approved mockup. It does not copy proprietary art or assets.

### Passive and editing interaction

The default window remains non-activating so copying and the initial search do not minimize or remove focus from the game.

Available in passive mode:

- drag from the entire title bar using manual no-activate window movement;
- toggle a filter checkbox;
- select a search profile;
- press Search and Close;
- clear Min or Max with a dedicated mouse control;
- adjust numeric values through bounded mouse-wheel or step controls.

Keyboard text editing is entered deliberately through Edit. Edit makes the WPF window focusable, places focus only after the user's action, and visibly changes the mode indicator. Leaving Edit restores no-activate behavior. The documentation recommends Borderless Windowed for typing because Windows can minimize an exclusive-fullscreen game when another top-level window takes keyboard focus.

The window remembers its last valid position per display/work area, clamps it on-screen, and falls back to safe placement near a screen edge. Position persistence contains no game or account data.

## Error handling and recovery

The presentation distinguishes these states:

- parsing rejected: no Trade call; show a concise unsupported-copy message only when a PoE-shaped item was detected;
- ambiguous modifier: visible unchecked row with the ambiguity reason;
- no matches: show active filter count and offer switching to Broad without automatically submitting it;
- invalid range/query: highlight the offending field and do not call Trade;
- HTTP 400: show sanitized structural diagnostics and never label it as simply no results;
- HTTP 429: honor `Retry-After` and observed `X-Rate-Limit-*` state, disable Search until allowed, and do not auto-retry;
- timeout/network/unavailable: keep the parsed item and editable filters, with a player-triggered retry;
- metadata refresh failure: retain last-known-good metadata and disclose its age;
- currency conversion failure: display original listing prices and reduced conversion coverage;
- stale response: discard through generation/cancellation checks.

Raw clipboard item text, seller details, and response bodies are not written to normal logs. Debug output contains only sanitized counts, family IDs, query shape, status codes, timing, and rate-limit state.

## Performance and caching

Parsing, classification, query creation, HTTP, conversion, and estimation run outside the UI thread. WPF receives immutable presentation state. The popup renders the parsed item immediately from cached metadata and updates pricing asynchronously.

The redesign adds no screen capture, OCR, or timer to the detailed Trade path. At most one search pipeline is active. Metadata and exchange rates use last-known-good caches with atomic replacement. Repeated identical item/profile/filter queries may use a short league-scoped memory cache; cache entries contain no raw clipboard history or credentials.

## Testing and verification

### Parser and classifier tests

- advanced-copy fixtures for implicit, prefix, suffix, fractured, crafted, enchant, rune/augment, desecrated, pseudo, sanctum, and skill families;
- multi-line modifiers and identical text present in multiple metadata families;
- requirements and base properties excluded from modifier collections;
- current value versus displayed roll-range extraction;
- ambiguous evidence fails closed;
- Heavy Belt resolves both base modifiers as implicit.

### Query and client tests

- each search profile selects only its documented domains;
- Broad applies direction-aware ten-percent relaxation;
- serializer routes filters to the correct Trade groups;
- default status is `securable`;
- no fetch URL contains more than ten IDs;
- a twenty-listing sample uses two ordered fetches;
- cancellation prevents stale publication;
- 400, 429, timeout, malformed response, and partial-fetch behavior remain distinct.

### Currency and estimator tests

- canonical Trade codes including `vaal`, `exalted`, `divine`, `chaos`, `regal`, and `alch` resolve through the catalog;
- live-name aliases normalize without conflating different currencies;
- direct Exalted and cross-currency listings participate in one estimate;
- missing rates retain original listings but reduce usable sample/confidence;
- duplicate accounts, deterministic outliers, range, median, and confidence are verified;
- the Heavy Belt 4-Vaal fixture converts to the injected expected Exalted value.

### Presentation and host tests

- Professional Compact view-model ordering and visibility states;
- clearing, stepping, and validating Min/Max;
- passive title-bar movement preserves the foreground window;
- Edit transition intentionally enables keyboard focus and Done restores passive mode;
- popup placement and remembered position are clamped to the active work area;
- game-foreground gating and manual-copy-only behavior remain intact.

### Manual acceptance

- compare the same Heavy Belt in this application and PoE Overlay II using the same league, profile, listing mode, and currency basis;
- verify original listing currencies and converted values, allowing market movement between requests;
- confirm Search and passive dragging do not minimize the game;
- confirm keyboard editing works in Borderless Windowed and is explicitly initiated;
- profile CPU, UI responsiveness, and the existing OCR loop while playing;
- verify no game file, process-memory, packet, credential, or synthesized-input access.

Live Trade checks are diagnostic/manual contract tests only and are never part of the routine automated suite, preventing unstable builds and unnecessary service load.

## Documentation, release, and packaging

This substantial redesign targets fork version 1.3.0. Completion updates:

- application/package/installer versions and visible UI version;
- `CHANGELOG.md` and the main README;
- `docs/ARCHITECTURE.md`, `docs/PROJECT_STRUCTURE.md`, and relevant flow documentation;
- immutable `docs/releases/1.3.0.md` containing provenance, behavior, safety boundary, verification results, installer filename, size, and SHA-256;
- a rebuilt installer under `install/`, replacing only the version-specific 1.3.0 deliverable;
- the configured Git repository and release workflow only after tests and installer verification pass.

No upstream auto-update channel is restored. The installer must remain self-contained and must not install a game modification, driver, browser extension, second overlay framework, or background service.

## Acceptance criteria

1. The Heavy Belt reference is parsed into requirements/properties plus two implicit modifiers and returns Instant Buy results in the selected league.
2. Original listing currencies remain visible and all known convertible currencies contribute to an Exalted-equivalent estimate.
3. Trade fetches contain at most ten IDs and a bounded twenty-listing sample cannot generate HTTP 400 from fetch size.
4. Crafting Base, Quick Price, and Broad -10% produce deterministic, visibly different filter selections.
5. Ambiguous or unsupported lines are visible, unchecked, and never mistranslated.
6. Professional Compact is the shipped layout, with usable density, scroll bounds, and clear hierarchy at supported DPI levels.
7. Passive Search, Close, filter toggles, mouse value controls, and title-bar dragging preserve game focus; keyboard editing requires an explicit Edit action.
8. Rate limits, no matches, invalid queries, missing currency rates, network failure, and stale responses have distinct safe states.
9. Automated tests, Release build, focus/interaction probe, performance check, and installer verification pass.
10. The ground-loot scanner retains its behavior and responsiveness, and the implementation does not cross the documented game-safety boundary.

# Architecture

## Runtime flow

1. `MainWindow` loads persisted configuration.
2. `LeagueCatalog` requests active PoE 2 leagues from poe.ninja. A valid response is cached atomically; failure falls back to that cache and then a built-in emergency list.
3. `PriceRepository` downloads exchange and stash-category prices for the selected league, merges custom overrides, and atomically publishes a `PriceSnapshot`.
4. `GroundLootScanEngine` captures the PoE viewport at a bounded cadence, performs OCR, normalizes/translates names, resolves prices, and sends screen coordinates to `PriceOverlay`.
5. The transparent overlay draws the badge to the right of the original label, without replacing game text.

## Detailed Trade overlay flow

1. The existing keyboard hook observes an exact `Ctrl+C` release and the host verifies the real Path of Exile process owns foreground focus.
2. `TradeOverlayController` performs four bounded clipboard attempts and rejects unchanged, incomplete, commodity, or unsupported text.
3. `PoeItemTextParser` separates identity, requirements, properties, state flags, headers, current values, and roll ranges. `ModifierMatcher` resolves declared families first and leaves duplicate-family text ambiguous.
4. `TradeQueryBuilder` creates Crafting Base, Quick Price, or Broad queries across identity, type, equipment, requirement, stat, and miscellaneous domains.
5. `PathOfExileTradeClient` sends one anonymous Instant Buy search and fetches at most twenty IDs in sequential chunks of ten, honoring rate-limit headers and preserving partial results.
6. `PriceEstimator` normalizes every available listing currency from the current poe.ninja snapshot, removes duplicate-account listings and statistical outliers, then reports range, median, conversion coverage, and confidence.
7. `TradeOverlayWindow` displays Professional Compact in passive no-activate mode. Mouse controls and title dragging remain passive; explicit Edit enables typing and returns focus before Search.

## Main components

- WPF UI: `MainWindow`, `SettingsWindow`, themes, tray behavior.
- Capture: Windows Graphics Capture with GDI fallback through `IScreenCaptureBackend`.
- OCR and matching: `OcrScanner`, `NameNormalizer`, `NameTranslator`, `GroundLootScanEngine`.
- Market data: `LeagueCatalog`, `PriceRepository`, `IconCache`.
- Overlay: `PriceOverlay` and row models.
- Optional island-rumour helper: `Rumour*` classes, isolated from ground-loot pricing.
- Detailed item pricing: root `overlay/PoeTradeOverlay`, referenced by the host but independent of capture/OCR loops.

## Network and local data

The application reads league and market data from poe.ninja and icon assets from the URLs contained in its data. Configuration, diagnostics, icon data, and `league_cache.json` live under `%LOCALAPPDATA%\PoeAncientsPriceHelper`. It does not upload OCR text, screenshots, configuration, or gameplay data.

Detailed item checks send structured anonymous filters to Path of Exile Trade after a manual focused-game `Ctrl+C`. Trade metadata and short-lived anonymous data live under `%LOCALAPPDATA%\PoeAncientsPriceHelper\overlay`. The app never reads browser cookies or account credentials and never stores raw clipboard history.

There is deliberately no software auto-updater. New binaries are built and installed manually from this repository. Price refresh is market-data refresh, not program update.

## Performance safeguards

Full-screen OCR runs at a limited cadence, pauses when PoE is not focused by default, caches name resolutions per price generation, publishes price snapshots atomically, limits HTTP concurrency, and cancels long-running work during shutdown or league changes.

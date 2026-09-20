# Architecture

## Runtime flow

1. `MainWindow` loads persisted configuration.
2. `LeagueCatalog` requests active PoE 2 leagues from poe.ninja. A valid response is cached atomically; failure falls back to that cache and then a built-in emergency list.
3. `PriceRepository` downloads exchange and stash-category prices for the selected league, merges custom overrides, and atomically publishes a `PriceSnapshot`.
4. `GroundLootScanEngine` captures the PoE viewport at a bounded cadence, performs OCR, normalizes/translates names, resolves prices, and sends screen coordinates to `PriceOverlay`.
5. The transparent overlay draws the badge to the right of the original label, without replacing game text.

## Main components

- WPF UI: `MainWindow`, `SettingsWindow`, themes, tray behavior.
- Capture: Windows Graphics Capture with GDI fallback through `IScreenCaptureBackend`.
- OCR and matching: `OcrScanner`, `NameNormalizer`, `NameTranslator`, `GroundLootScanEngine`.
- Market data: `LeagueCatalog`, `PriceRepository`, `IconCache`.
- Overlay: `PriceOverlay` and row models.
- Optional island-rumour helper: `Rumour*` classes, isolated from ground-loot pricing.

## Network and local data

The application reads league and market data from poe.ninja and icon assets from the URLs contained in its data. Configuration, diagnostics, icon data, and `league_cache.json` live under `%LOCALAPPDATA%\PoeAncientsPriceHelper`. It does not upload OCR text, screenshots, configuration, or gameplay data.

There is deliberately no software auto-updater. New binaries are built and installed manually from this repository. Price refresh is market-data refresh, not program update.

## Performance safeguards

Full-screen OCR runs at a limited cadence, pauses when PoE is not focused by default, caches name resolutions per price generation, publishes price snapshots atomically, limits HTTP concurrency, and cancels long-running work during shutdown or league changes.


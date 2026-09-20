# Changelog

All notable changes to the TonChaiya fork are documented here. This project uses an independent
version sequence beginning at 1.0.0; it does not continue the upstream project's version numbers.

## 1.2.0 — 2026-09-20

### Changed

- Rebuilt the detailed Trade window around a compact PoE-style item summary, modifier grid, estimate card, and lowest-listings table.
- Automatic display and Search clicks now use a native no-activate window policy so they do not take focus from the game.
- Keyboard editing is an explicit mode; Borderless display mode is recommended while typing into filters.
- Requirement and modifier-tier annotation lines no longer appear as Trade filters.
- Rolled-value annotations such as `+11(10-17)` now normalize to the corresponding Trade stat template.

### Safety and performance

- The focus fix uses only normal Windows window styles; it does not inject into or modify the game.
- Listing display remains read-only and does not whisper sellers or automate trades.

## 1.1.0 — 2026-09-20

### Added

- Integrated detailed equipment price check triggered only by the player's normal `Ctrl+C` while PoE 2 is foreground.
- Editable Trade filters for recognized equipment modifiers with unsupported lines shown instead of guessed.
- Anonymous, rate-limit-aware Path of Exile Trade search/fetch client with cancellation and bounded responses.
- Credible-low, typical range, median, sample size, and confidence display with duplicate-account and outlier handling.
- Isolated `overlay/` library and test project for continued development.

### Safety and performance

- No synthesized input, game-file or memory access, packet capture, credentials, cookies, telemetry, seller contact, or trade automation.
- No continuous clipboard polling; clipboard reads occur only within a bounded window after focused-game `Ctrl+C`.
- Trade outages and schema changes remain isolated from the existing ground-loot OCR path.

## 1.0.0 — 2026-09-20

### Added

- Full-screen OCR for visible Path of Exile 2 ground-loot labels.
- Prices drawn immediately after each detected item label without covering the label.
- Price coverage for every documented PoE2 exchange category and current unique/tablet categories.
- Leading and trailing stack quantity support.
- Dynamic active-league discovery from poe.ninja with local cache and offline fallback.
- Safe selected-league reload so every price request uses the chosen league.
- Deterministic Uncut Skill, Spirit, and Support Gem pricing by gem type and level.
- Self-contained Windows installer that does not require a separate .NET installation.

### Performance

- Full-screen OCR is capped to protect game frame time and pauses while PoE2 is not focused by default.
- Price requests use bounded concurrency and cached price snapshots.
- GPU capture is used where available with a GDI fallback.

### Security and ownership

- Removed Velopack, GitHub update checks, staged downloads, and silent external updates.
- This build is updated only from this source repository by its maintainer.

See [FORK_NOTES.md](FORK_NOTES.md) for upstream provenance.

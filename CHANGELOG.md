# Changelog

All notable changes to the TonChaiya fork are documented here. This project uses an independent
version sequence beginning at 1.0.0; it does not continue the upstream project's version numbers.

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

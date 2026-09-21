# Changelog

All notable changes to the TonChaiya fork are documented here. This project uses an independent
version sequence beginning at 1.0.0; it does not continue the upstream project's version numbers.

## 1.3.1 — 2026-09-21

### Fixed

- Split the draggable title surface from Edit and Close so routed mouse events no longer swallow button clicks.
- Added explicit local templates and hit areas for editable Min/Max fields and modifier checkboxes.
- Recognize `Requires: Level ...` as an item property instead of an unsupported Trade modifier.
- Added a safe per-listing button that opens the corresponding anonymous official Trade result.

### UI and safety

- Tightened modifier row spacing and listing columns while preserving the approved 420-DIP compact layout.
- Editing remains opt-in and passive mode still avoids taking focus from the game.
- Official Trade links use only the selected league and returned search ID; no account session or game access is introduced.

## 1.3.0 — 2026-09-21

### Added

- Evidence-based modifier-family resolution for implicit, explicit, fractured, crafted, enchant, rune/augment, desecrated, pseudo, sanctum, and skill stats.
- Crafting Base, Quick Price, and Broad −10% profiles with editable Min/Max controls.
- Mixed-currency listing display and live league conversion, including Vaal Orb prices.

### Fixed

- Requirements are no longer misclassified as modifiers; current rolls and roll ranges are preserved separately.
- Trade searches use Instant Buy, correct filter domains, and two bounded fetch chunks of ten.
- Ambiguous same-text modifiers remain visible and unchecked instead of choosing a wrong family.
- The overlay can be dragged in passive mode and restores the previous game focus after Edit/Search.

### UI and safety

- Replaced the detailed popup with the approved 420-DIP Professional Compact layout.
- No game files, memory, packets, input synthesis, browser cookies, account tokens, or seller automation.
- No external software auto-update; release binaries are built from this fork only.

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

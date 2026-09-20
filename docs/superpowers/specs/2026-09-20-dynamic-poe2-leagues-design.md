# Dynamic PoE2 League Selection Design

## Intent

Keep the locally improved full-screen ground-loot price overlay while allowing users to select any
Path of Exile 2 economy league currently supported by poe.ninja. New leagues should normally appear
without rebuilding the application. The application must remain locally maintained and must not
check, download, stage, or apply updates from the upstream GitHub repository.

This fork uses an independent release sequence beginning at `1.0.0`. Upstream version `3.7.1` is
provenance only and must never be combined with the fork's release number.

The `old/` directory is an immutable reference copy. All implementation changes target the project
at the repository root.

## Success criteria

- Ground-loot labels across the detected PoE2 viewport continue to be scanned and priced.
- The League dropdown lists the live PoE2 economy leagues returned by poe.ninja.
- Selecting a league reloads prices for that league and safely restarts an active ground-loot scan.
- The selected league persists between launches.
- A poe.ninja or network outage does not leave the dropdown empty: the app uses a local league cache,
  then a built-in fallback list if no cache exists.
- No GitHub release check, Velopack dependency, update download, or silent update application exists.
- League discovery is lightweight: one cached request at startup, not polling during gameplay.
- Price refresh remains limited and uses the selected league identifier for every price category.
- Ground labels for Uncut Skill, Spirit, and Support Gems resolve by exact gem type and level rather
  than relying on generic fuzzy matching.
- The completed source is published only to
  `https://github.com/TonChaiya/poe2-PoeAncientsPriceHelper-main.git`.

## Upstream assessment

The upstream repository at `https://github.com/pedro-quiterio/PoeAncientsPriceHelper.git` was reviewed
at commit `e2b48e7032044042b6b219145a709b6a6b813b64` (version 3.11.0). It adds Forbidden Rites league
entries, Standard support, a Ritual completion chime, and rune-panel uncut skill-gem pricing. It also
retains Velopack auto-update and uses the calibrated `ScanEngine` rather than this fork's full-screen
`GroundLootScanEngine`.

The upstream tree will therefore not be merged wholesale. The dynamic league catalog is implemented
locally; unrelated Ritual features and the upstream updater are outside this change. The existing
full-screen engine and the broader ground-loot price categories remain authoritative.

## Architecture

### League catalog

Add a focused `LeagueCatalog` service. It requests:

`GET https://poe.ninja/poe2/api/economy/leagues`

The response is parsed as entries containing `id` and `name`. Only entries with non-empty identifiers
and names are accepted; duplicate identifiers are removed case-insensitively while preserving server
order. The first returned entry remains first because poe.ninja defines it as the current temporary
challenge league.

`LeagueOption` exposes:

- `Id`: the exact value sent as the `league` parameter to all economy endpoints.
- `Name`: the user-facing value shown in the dropdown.

`AppConfig.LeagueName` remains the persisted league identifier for backward compatibility with
existing config files. Renaming it would create an unnecessary migration risk.

### Cache and fallback

After a successful non-empty response, the validated catalog is written to
`%LocalAppData%\PoeAncientsPriceHelper\league_cache.json`. A failed request follows this order:

1. Load the last valid cache.
2. If no valid cache exists, use the built-in list: Forbidden Rites, HC Forbidden Rites, Standard,
   Runes of Aldur, and HC Runes of Aldur.

Malformed responses and malformed cache files are treated as unavailable and never overwrite a good
cache. Cache writes use a temporary file followed by replacement so an interrupted write cannot
destroy the last usable catalog.

### Startup and selection flow

1. Load `AppConfig`.
2. Fetch the league catalog once, with a short timeout and the application's existing HTTP client.
3. Populate the dropdown with `LeagueOption` objects and display `Name`.
4. Preserve `AppConfig.LeagueName` if its identifier exists in the catalog.
5. Otherwise select the first catalog entry and save that identifier.
6. Fetch every supported ground-loot price category using the selected identifier.
7. Enable scanning only after a usable price snapshot exists.

When the user changes league, the UI disables the dropdown and Start button, stops the active
`GroundLootScanEngine`, disposes the old repository/icon state, fetches the new league's prices, then
restarts the scanner only if it was previously running. A failed price load keeps the previous good
price snapshot where possible and shows a clear failure status instead of silently displaying prices
as if they belonged to the new league.

### Pricing scope

Retain the fork's complete PoE2 exchange and stash-item category lists because full-screen ground loot
can include more than the five categories used by the upstream rune panel. Every request receives the
same selected league identifier. Existing bounded concurrency remains in place to avoid a burst of
requests or gameplay slowdown.

### Update isolation

Do not import upstream `Program`, updater fields, Velopack package references, GitHub release checks,
or update UI. The only automatic network activity introduced by this design is the poe.ninja economy
league request. Existing price and icon requests remain unchanged.

## Error handling

- League endpoint timeout or HTTP failure: use cache, then fallback.
- Empty or malformed league response: reject it without overwriting cache.
- Previously selected league is no longer active: select the first current league and tell the user in
  the status line.
- Price fetch fails after a league switch: do not relabel an old snapshot as prices for the new league;
  leave scanning stopped and allow retry or another league selection.
- Rapid dropdown changes: preserve the existing reentrancy guard and cancellation/disposal ordering.

## Performance

- Discover leagues once per application launch.
- Do not poll the league endpoint while playing.
- Reuse the existing HTTP client and response compression.
- Keep OCR cadence, full-screen capture behavior, bounded price-fetch concurrency, and overlay drawing
unchanged unless a test exposes a regression.

### Uncut gem resolution

The price repository already loads poe.ninja's `UncutGems` category. The full-screen ground-loot
engine must additionally run a dedicated canonical resolver before its generic exact/fuzzy resolver:

- `Uncut Skill Gem (Level 20)` maps to `uncut skill gem level 20`.
- Equivalent Spirit and Support labels preserve their respective type and level.
- A recognised gem with an unreadable level is left unpriced instead of guessing an adjacent level.
- The upstream rune-panel form `Skill Level 20: Skyfall` may map to the same level-20 skill-gem key,
  but support rewards without a level remain unpriced because poe.ninja prices them per level.

This keeps ground-gem pricing deterministic and prevents a noisy level read from becoming the price
of a different gem level.

### Provenance and GitHub publication

Add `FORK_NOTES.md` and a concise README section stating that this fork started from upstream version
3.7.1 of `pedro-quiterio/PoeAncientsPriceHelper`. Record every TonChaiya improvement under the single
independent `1.0.0` release: full-screen ground-loot OCR, expanded price categories, placement after
labels, performance limits, installer, removal of Velopack/external auto-update, dynamic league
catalog, cached fallback behavior, correct per-league price reload, and deterministic Uncut Gem level
pricing. Do not reuse any intermediate local version labels in public release history. The setup
filename uses version `1.0.0`.

After extending `.gitignore` to exclude the local archive and generated artifacts, initialize the
project root as a local Git repository so implementation can be committed incrementally. Do not add a
remote until final verification. Then configure exactly one remote named `origin` pointing to
`https://github.com/TonChaiya/poe2-PoeAncientsPriceHelper-main.git`. Do not configure or push to the
upstream Pedro repository. Generated build directories, review clones, caches, and local diagnostics
must remain excluded. The local `old/` archive is also excluded because it duplicates the project and
contains generated binaries; provenance is preserved in `FORK_NOTES.md` instead. The verified
self-contained `1.0.0` setup executable under `install/` is intentionally included so the repository
has a directly downloadable build. Push the verified source, documentation, and installer to the
empty repository's `main`
branch after confirming the remote URL immediately before the push.

## Testing

- Parse and deduplicate a valid live-league response while preserving order.
- Reject empty identifiers, empty names, malformed JSON, and an empty response.
- Prefer the live catalog, fall back to a valid cache, then fall back to built-ins.
- Preserve a saved league that is present; choose the first current league when it is absent.
- Verify the selected `LeagueOption.Id` is sent to price endpoints.
- Verify full-screen ground labels pin Uncut Skill, Spirit, and Support Gem prices by type and level;
  verify an unreadable level is never guessed.
- Verify a league switch does not keep scanning with the prior league's repository.
- Run the complete existing test suite to protect full-screen OCR, overlay positioning, quantity
  handling, settings, and network parsing.
- Build a self-contained Release and regenerate the local installer only after all tests pass.
- Verify the only configured Git remote is the TonChaiya repository before publishing.

## Out of scope

- Importing the upstream Ritual chime helper.
- Restoring upstream auto-update or any other GitHub background connection.
- Automatically updating OCR rules when the game UI changes.
- Proxying poe.ninja through a separately operated backend.

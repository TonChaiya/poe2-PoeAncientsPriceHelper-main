# Dynamic PoE2 Leagues 1.0.0 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Publish the independently versioned 1.0.0 full-screen ground-loot overlay with automatic PoE2 league discovery, correct selected-league prices, deterministic Uncut Gem pricing, no upstream updater, and complete fork notes.

**Architecture:** A new `LeagueCatalog` owns the poe.ninja leagues request, validation, atomic cache, and built-in fallback. `MainWindow` binds the returned `LeagueOption` records while continuing to store the selected league identifier in the existing `AppConfig.LeagueName`; `PriceRepository` receives that identifier unchanged. The existing full-screen `GroundLootScanEngine` stays authoritative and gains the shared gem canonicalizer before generic fuzzy matching.

**Tech Stack:** C# 14, .NET 10 WPF, Newtonsoft.Json, xUnit, poe.ninja PoE2 economy API, Windows self-contained publish/IExpress installer, Git.

**Spec:** `docs/superpowers/specs/2026-09-20-dynamic-poe2-leagues-design.md`

## Global Constraints

- The fork version begins at `1.0.0`; upstream `3.7.1` is provenance only.
- Preserve full-screen `GroundLootScanEngine`; do not restore calibrated `ScanEngine` as the main engine.
- Do not add Velopack, GitHub update checks, staged updates, or any automatic source update path.
- Fetch the league catalog once per launch and use cache/built-ins on failure.
- Keep price request concurrency bounded at four and retain all documented ground-loot price categories.
- Do not modify or publish `old/`, `.artifacts/`, `bin/`, `obj/`, user configuration, or diagnostics.
- The only Git remote permitted is `https://github.com/TonChaiya/poe2-PoeAncientsPriceHelper-main.git`.

## Review Focus

- A 200 response containing malformed/empty leagues must not erase a previously valid cache.
- A retired saved league must select the first live league and must never fetch prices under the retired identifier.
- A failed price load after switching leagues must leave scanning disabled instead of showing stale prices as the new league.
- OCR ambiguity between Uncut Gem levels must produce no price, never an adjacent level's price.
- The final Git index must exclude `old/`, `.artifacts/`, every `bin/` and `obj/`, local config/logs, and the upstream review clone.

---

### Task 1: Repository hygiene and independent version baseline

**Files:**
- Modify: `.gitignore`
- Modify: `src/PoeAncientsPriceHelper/PoeAncientsPriceHelper.csproj`
- Modify: `CHANGELOG.md`

**Interfaces:**
- Consumes: existing project tree without `.git`.
- Produces: local `main` branch, version `1.0.0`, generated/archive exclusions, no configured remote.

- [ ] **Step 1: Add repository-only exclusions**

Append the following exact root exclusions while intentionally leaving `install/` trackable:

```gitignore
/.artifacts/
/old/
```

- [ ] **Step 2: Set the independent product version**

Change the project property to:

```xml
<Version>1.0.0</Version>
```

Replace the inherited/local changelog sequence with a fork-owned `1.0.0 — 2026-09-20` entry covering
all TonChaiya features. State that upstream 3.7.1 is provenance, not part of this fork's version
number; link readers to `FORK_NOTES.md` for upstream history. Do not publish temporary local
development version labels.

- [ ] **Step 3: Verify updater isolation before creating history**

Run:

```powershell
rg -n -i "Velopack|UpdateManager|CheckForUpdates|ApplyUpdates|GithubSource" src/PoeAncientsPriceHelper --glob '!**/bin/**' --glob '!**/obj/**'
```

Expected: no matches.

- [ ] **Step 4: Initialize local history without a remote**

Run:

```powershell
git init -b main
git add .gitignore src/PoeAncientsPriceHelper/PoeAncientsPriceHelper.csproj CHANGELOG.md docs/superpowers
git commit -m "chore: start independent 1.0.0 fork"
git remote -v
```

Expected: commit succeeds and `git remote -v` prints nothing.

---

### Task 2: Live league catalog with atomic cache and fallback

**Files:**
- Create: `src/PoeAncientsPriceHelper/LeagueCatalog.cs`
- Create: `src/PoeAncientsPriceHelper.Tests/LeagueCatalogTests.cs`
- Modify: `src/PoeAncientsPriceHelper/AppPaths.cs`
- Modify: `src/PoeAncientsPriceHelper.Tests/TestHelpers.cs`

**Interfaces:**
- Consumes: `HttpClient`, optional cache directory, poe.ninja JSON `[{"id":"...","name":"..."}]`.
- Produces: `LeagueOption(string Id, string Name)`, `LeagueCatalogResult`, and `Task<LeagueCatalogResult> LoadAsync(CancellationToken)`.

- [ ] **Step 1: Write parsing and ordering tests**

Add tests proving blank entries are rejected, duplicate identifiers are removed case-insensitively,
and server order remains unchanged:

```csharp
[Fact]
public void Parse_ValidatesDeduplicatesAndPreservesOrder()
{
    var actual = LeagueCatalog.Parse("""
        [{"id":"Forbidden Rites","name":"Forbidden Rites"},
         {"id":"","name":"Broken"},
         {"id":"forbidden rites","name":"Duplicate"},
         {"id":"Standard","name":"Standard"}]
        """);
    Assert.Collection(actual,
        x => Assert.Equal("Forbidden Rites", x.Id),
        x => Assert.Equal("Standard", x.Id));
}
```

- [ ] **Step 2: Run the parser test and verify RED**

Run:

```powershell
dotnet test src/PoeAncientsPriceHelper.Tests/PoeAncientsPriceHelper.Tests.csproj -c Release --filter FullyQualifiedName~LeagueCatalogTests.Parse
```

Expected: FAIL because `LeagueCatalog` does not exist.

- [ ] **Step 3: Implement the domain records and parser minimally**

Create:

```csharp
internal sealed record LeagueOption(string Id, string Name)
{
    public override string ToString() => Name;
}

internal enum LeagueCatalogSource { Live, Cache, BuiltIn }
internal sealed record LeagueCatalogResult(
    IReadOnlyList<LeagueOption> Leagues,
    LeagueCatalogSource Source);
```

Implement `Parse` with `JArray.Parse`, trimmed non-empty values, a case-insensitive `HashSet<string>`,
and preserved input order.

- [ ] **Step 4: Run the parser test and verify GREEN**

Run the command from Step 2. Expected: PASS.

- [ ] **Step 5: Write live/cache/built-in fallback tests**

Add tests with `TempDir`, `FakeHttpMessageHandler`, and `FailingHttpHandler` proving:

```csharp
Assert.Equal(LeagueCatalogSource.Live, live.Source);
Assert.Equal(LeagueCatalogSource.Cache, cached.Source);
Assert.Equal(LeagueCatalogSource.BuiltIn, fallback.Source);
Assert.Contains(fallback.Leagues, x => x.Id == "Forbidden Rites");
Assert.Contains(fallback.Leagues, x => x.Id == "Standard");
```

Add a malformed-live test that prewrites a valid cache, returns invalid JSON, and asserts the cache
file contents remain unchanged.

- [ ] **Step 6: Run fallback tests and verify RED**

Run:

```powershell
dotnet test src/PoeAncientsPriceHelper.Tests/PoeAncientsPriceHelper.Tests.csproj -c Release --filter FullyQualifiedName~LeagueCatalogTests
```

Expected: fallback/cache tests FAIL because `LoadAsync` is absent.

- [ ] **Step 7: Implement request, timeout, atomic cache, and fallback**

Implement `LoadAsync` with endpoint
`https://poe.ninja/poe2/api/economy/leagues`, a linked five-second cancellation source, and this order:

```csharp
var live = await TryLoadLiveAsync(ct);
if (live.Count > 0) { SaveCache(live); return new(live, LeagueCatalogSource.Live); }
var cached = LoadCache();
if (cached.Count > 0) return new(cached, LeagueCatalogSource.Cache);
return new(BuiltIn, LeagueCatalogSource.BuiltIn);
```

Write `league_cache.json.tmp`, then `File.Replace` or `File.Move`, matching `ConfigStore`'s safe-write
pattern. Add `AppPaths.LeagueCachePath` and permit tests to pass an explicit cache directory.

- [ ] **Step 8: Run catalog tests and commit**

Run the command from Step 6. Expected: all `LeagueCatalogTests` PASS.

```powershell
git add src/PoeAncientsPriceHelper/LeagueCatalog.cs src/PoeAncientsPriceHelper/AppPaths.cs src/PoeAncientsPriceHelper.Tests/LeagueCatalogTests.cs src/PoeAncientsPriceHelper.Tests/TestHelpers.cs
git commit -m "feat: discover and cache active poe2 leagues"
```

---

### Task 3: Bind live leagues and reload the correct price repository

**Files:**
- Modify: `src/PoeAncientsPriceHelper/MainWindow.xaml.cs`
- Modify: `src/PoeAncientsPriceHelper.Tests/LeagueCatalogTests.cs`
- Modify: `src/PoeAncientsPriceHelper.Tests/PriceRepositoryTests.cs`

**Interfaces:**
- Consumes: `LeagueCatalogResult.Leagues`, existing `AppConfig.LeagueName`, `PriceRepository`.
- Produces: deterministic `LeagueCatalog.Select(...)` and dropdown entries whose `Id` drives all price requests.

- [ ] **Step 1: Write selection migration tests**

```csharp
[Theory]
[InlineData("Standard", "Standard")]
[InlineData("Retired League", "Forbidden Rites")]
public void Select_PreservesActiveOrUsesFirst(string saved, string expected)
{
    LeagueOption[] options = [new("Forbidden Rites", "Forbidden Rites"), new("Standard", "Standard")];
    Assert.Equal(expected, LeagueCatalog.Select(options, saved).Id);
}
```

- [ ] **Step 2: Run selection tests and verify RED**

Run the catalog filter command. Expected: FAIL because `Select` is absent.

- [ ] **Step 3: Implement minimal selection helper**

Implement case-insensitive identifier selection, otherwise return the first option. Throw only if the
caller violates the catalog contract by passing an empty list.

- [ ] **Step 4: Expand price request tests for new identifiers**

Add these rows to `LeagueName_DrivesApiParamAndReferer`:

```csharp
[InlineData("Forbidden Rites", "league=Forbidden%20Rites&", "/economy/forbiddenrites/")]
[InlineData("HC Forbidden Rites", "league=HC%20Forbidden%20Rites&", "/economy/hcforbiddenrites/")]
[InlineData("Standard", "league=Standard&", "/economy/standard/")]
```

Run the focused test and confirm it passes with the existing repository; this pins the required
selected-ID behavior before UI integration.

- [ ] **Step 5: Integrate catalog loading into startup**

Add `_leagueOptions` and `_leagueCatalogSource`. In `OnLoaded`, load config, await `LeagueCatalog`,
select the persisted identifier, save only if selection changed, then call `PopulateFields` and
`StartupAsync`. Bind records instead of strings:

```csharp
LeagueBox.ItemsSource = _leagueOptions;
LeagueBox.SelectedItem = LeagueCatalog.Select(_leagueOptions, _config.LeagueName);
```

Change the selection handler to require `LeagueOption` and store `option.Id`. Preserve the existing
`_startingUp` guard and `StartupAsync` order: stop scanner, dispose old repo/icons, build new state,
fetch selected-league prices, and restart only when the previous scanner was running and the new
repository has usable prices.

- [ ] **Step 6: Make catalog source visible without disrupting price status**

During initial load show `Loading active leagues…`; on cache/built-in fallback include a concise
offline/fallback note until the price result replaces it. Never label old prices as belonging to the
new league after a failed switch.

- [ ] **Step 7: Run catalog, repository, and full tests; commit**

```powershell
dotnet test src/PoeAncientsPriceHelper.Tests/PoeAncientsPriceHelper.Tests.csproj -c Release --filter "FullyQualifiedName~LeagueCatalogTests|FullyQualifiedName~PriceRepositoryTests"
dotnet test src/PoeAncientsPriceHelper.Tests/PoeAncientsPriceHelper.Tests.csproj -c Release
git add src/PoeAncientsPriceHelper/MainWindow.xaml.cs src/PoeAncientsPriceHelper.Tests/LeagueCatalogTests.cs src/PoeAncientsPriceHelper.Tests/PriceRepositoryTests.cs
git commit -m "feat: reload prices for selected live league"
```

Expected: all tests PASS and the total is not lower than the pre-change 303 tests.

---

### Task 4: Deterministic Uncut Gem pricing in the full-screen engine

**Files:**
- Modify: `src/PoeAncientsPriceHelper/ScanEngine.cs`
- Modify: `src/PoeAncientsPriceHelper/GroundLootScanEngine.cs`
- Modify: `src/PoeAncientsPriceHelper.Tests/FuzzyMatchTests.cs`
- Modify: `src/PoeAncientsPriceHelper.Tests/GroundLootTests.cs`

**Interfaces:**
- Consumes: normalized OCR label and `PriceSnapshot`.
- Produces: `ScanEngine.TryResolveGemKey(string, out string?)` supporting direct and rune-panel forms; `GroundLootScanEngine.ResolvePriceKey(string, PriceSnapshot)` returning `(string? Key, bool Exact)`.

- [ ] **Step 1: Write rune-panel gem tests**

Add:

```csharp
[Theory]
[InlineData("skill level 20 skyfall", "uncut skill gem level 20")]
[InlineData("skill level 7 leylines", "uncut skill gem level 7")]
public void TryResolveGemKey_RuneSkillPinsLevel(string text, string expected)
{
    Assert.True(ScanEngine.TryResolveGemKey(text, out var key));
    Assert.Equal(expected, key);
}
```

- [ ] **Step 2: Run the new tests and verify RED**

Run:

```powershell
dotnet test src/PoeAncientsPriceHelper.Tests/PoeAncientsPriceHelper.Tests.csproj -c Release --filter FullyQualifiedName~TryResolveGemKey_RuneSkill
```

Expected: FAIL because the local resolver does not support `skill level N` without `gem`.

- [ ] **Step 3: Implement the special rune form minimally**

Add a compiled `\bskill\s+level\s+(\d+)\b` regex and return
`uncut skill gem level {N}` while leaving level-less support rewards unresolved.

- [ ] **Step 4: Write ground resolver tests**

Build a real `PriceSnapshot` containing levels 19 and 20, then assert:

```csharp
Assert.Equal("uncut skill gem level 20",
    GroundLootScanEngine.ResolvePriceKey("uncut skill gem level 20", snapshot).Key);
Assert.Equal("uncut spirit gem level 19",
    GroundLootScanEngine.ResolvePriceKey("uncot spirit gem level 19", snapshot).Key);
Assert.Null(GroundLootScanEngine.ResolvePriceKey("uncut skill gem", snapshot).Key);
```

- [ ] **Step 5: Run ground resolver tests and verify RED**

Run the `GroundLootTests` filter. Expected: FAIL because `ResolvePriceKey` does not exist.

- [ ] **Step 6: Implement gem-first ground resolution**

Move the non-cache portion of `ResolveName` into the internal static `ResolvePriceKey`. Before exact,
digit-fold, or fuzzy matching, call `TryResolveGemKey`; a recognised gem with no canonical level or no
matching price returns `(null, false)` and never falls through to fuzzy matching. Keep the instance
cache as a wrapper around this pure method.

- [ ] **Step 7: Run focused and complete tests; commit**

```powershell
dotnet test src/PoeAncientsPriceHelper.Tests/PoeAncientsPriceHelper.Tests.csproj -c Release --filter "FullyQualifiedName~GroundLootTests|FullyQualifiedName~FuzzyMatchTests"
dotnet test src/PoeAncientsPriceHelper.Tests/PoeAncientsPriceHelper.Tests.csproj -c Release
git add src/PoeAncientsPriceHelper/ScanEngine.cs src/PoeAncientsPriceHelper/GroundLootScanEngine.cs src/PoeAncientsPriceHelper.Tests/FuzzyMatchTests.cs src/PoeAncientsPriceHelper.Tests/GroundLootTests.cs
git commit -m "feat: pin ground uncut gem prices by level"
```

---

### Task 5: Fork notes, UI copy, README, and 1.0.0 installer

**Files:**
- Create: `FORK_NOTES.md`
- Create: `docs/README.md`
- Create: `docs/ARCHITECTURE.md`
- Create: `docs/PROJECT_STRUCTURE.md`
- Create: `docs/DEVELOPMENT.md`
- Create: `docs/VERSIONING.md`
- Create: `docs/releases/1.0.0.md`
- Modify: `README.md`
- Modify: `src/PoeAncientsPriceHelper/MainWindow.xaml`
- Modify: `src/PoeAncientsPriceHelper/docs/README.html`
- Modify: `installer/Build-Installer.ps1`
- Modify: `installer/Install.ps1`

**Interfaces:**
- Consumes: verified feature behavior and upstream provenance.
- Produces: clear public documentation and `install/Poe2GroundLootPriceHelper-v1.0.0-Setup.exe`.

- [ ] **Step 1: Write provenance and feature notes**

`FORK_NOTES.md` must state:

```markdown
# Fork notes

This project is an independently versioned fork based on
pedro-quiterio/PoeAncientsPriceHelper 3.7.1.

The TonChaiya release line starts at 1.0.0. Upstream version numbers are not
continued or combined with this fork's versions.
```

Then list full-screen ground-loot OCR, prices placed after labels, all exchange/unique categories,
stack pricing, automatic active-league discovery and cache, correct league reload, Uncut Gem level
pricing, performance controls, local installer, and removed external auto-update.

Create a durable documentation set: `docs/README.md` as the index, `ARCHITECTURE.md` for component and
data flow, `PROJECT_STRUCTURE.md` for folder/file ownership, `DEVELOPMENT.md` for setup/build/test and
release workflow, `VERSIONING.md` for independent Semantic Versioning, and `docs/releases/1.0.0.md`
as the immutable release note. Future releases add a new file under `docs/releases/` and never rewrite
older release notes.

- [ ] **Step 2: Update public UI and guides**

Remove hardcoded `0.5.4` and `3.8.1` copy. Use `PoE2 Ground Loot Price Helper 1.0.0`, describe dynamic
league selection, and state that this build never updates itself from upstream.

- [ ] **Step 3: Update installer version constants and filenames**

Change setup output and uninstall display version to `1.0.0`; ensure all installer scripts agree on:

```text
Poe2GroundLootPriceHelper-v1.0.0-Setup.exe
```

- [ ] **Step 4: Verify documentation has no misleading updater or old-version claims**

```powershell
rg -n "3\.8\.1|0\.5\.4|keeps itself up to date|auto-update" README.md FORK_NOTES.md installer src/PoeAncientsPriceHelper/docs src/PoeAncientsPriceHelper/MainWindow.xaml
```

Expected: only intentional historical/removal statements; no active updater claim or old installer name.

- [ ] **Step 5: Commit documentation and packaging source**

```powershell
git add FORK_NOTES.md README.md CHANGELOG.md src/PoeAncientsPriceHelper/MainWindow.xaml src/PoeAncientsPriceHelper/docs/README.html installer
git commit -m "docs: document TonChaiya 1.0.0 fork"
```

---

### Task 6: Verification, release build, and installer inspection

**Files:**
- Replace: `install/Poe2GroundLootPriceHelper-v1.0.0-Setup.exe`
- Remove from release output only: obsolete `install/Poe2GroundLootPriceHelper-0.5.4-v3.8.1-Setup.exe`

**Interfaces:**
- Consumes: completed source and installer scripts.
- Produces: tested Release binaries, verified installer payload, checksums.

- [ ] **Step 1: Read and follow verification-before-completion skill**

Run its evidence checklist before making any completion claim.

- [ ] **Step 2: Run the complete Release suite**

```powershell
dotnet test src/PoeAncientsPriceHelper.Tests/PoeAncientsPriceHelper.Tests.csproj -c Release
```

Expected: zero failed tests and total greater than 303.

- [ ] **Step 3: Build self-contained installer**

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File installer/Build-Installer.ps1
```

Expected: `install/Poe2GroundLootPriceHelper-v1.0.0-Setup.exe` exists and contains a self-contained
`PoeAncientsPriceHelper.exe` with ProductVersion `1.0.0`.

- [ ] **Step 4: Inspect without installing**

Extract the setup to a fresh `.artifacts/installer-verify-<guid>` directory using IExpress `/Q /T /C`.
Verify `App.zip`, `Install.cmd`, `Install.ps1`, and `Uninstall.ps1`; expand `App.zip` and inspect the
payload executable version. Parse all installer PowerShell scripts with the PowerShell parser and
require zero syntax errors.

- [ ] **Step 5: Verify dependency and updater state**

```powershell
dotnet list src/PoeAncientsPriceHelper/PoeAncientsPriceHelper.csproj package --vulnerable --include-transitive
rg -n -i "Velopack|UpdateManager|CheckForUpdates|ApplyUpdates|GithubSource" src/PoeAncientsPriceHelper --glob '!**/bin/**' --glob '!**/obj/**'
```

Expected: no vulnerable packages and no active updater references.

- [ ] **Step 6: Record SHA-256 and commit installer**

```powershell
Get-FileHash install/Poe2GroundLootPriceHelper-v1.0.0-Setup.exe -Algorithm SHA256
git add install/Poe2GroundLootPriceHelper-v1.0.0-Setup.exe
git commit -m "build: add verified 1.0.0 installer"
```

---

### Task 7: Publish only to the TonChaiya repository

**Files:**
- No source changes expected.

**Interfaces:**
- Consumes: verified commits and installer.
- Produces: `main` branch at the authorized empty GitHub repository.

- [ ] **Step 1: Audit tracked files and secrets**

```powershell
git status --short
git ls-files | rg "(^old/|^\.artifacts/|/bin/|/obj/|config\.json|scan_log|debug_ocr)"
git grep -n -i -E "(github_pat_|ghp_|api[_-]?key|client[_-]?secret|password\s*=)"
```

Expected: clean status, no forbidden tracked paths, and no credentials.

- [ ] **Step 2: Configure and verify exactly one remote**

```powershell
git remote add origin https://github.com/TonChaiya/poe2-PoeAncientsPriceHelper-main.git
git remote -v
```

Expected: only `origin`, and both fetch/push URLs exactly match the TonChaiya repository. Abort before
push if any other remote or URL appears.

- [ ] **Step 3: Push the verified branch**

```powershell
git push -u origin main
```

Expected: the empty repository receives `main`. If GitHub authentication is unavailable, leave all
verified local commits intact and report the single authentication action required; never substitute
another repository or remote.

- [ ] **Step 4: Verify remote content read-only**

```powershell
git ls-remote --heads origin main
git status --short --branch
```

Expected: remote `main` resolves to local `HEAD`, local branch tracks `origin/main`, and the worktree is clean.

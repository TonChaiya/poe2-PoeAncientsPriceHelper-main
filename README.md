# PoE 2 Ground Loot Price Helper — fork 1.0.0

Windows overlay for Path of Exile 2 that scans visible ground-item labels and places the current market price immediately after each label. It keeps the game's item name visible and works across the viewport rather than only a calibrated price panel.

## Highlights

- Discovers current PoE 2 leagues from poe.ninja, including future leagues, with cached/offline fallback.
- Reloads prices for the exact league selected in the UI.
- Covers currency, fragments, mechanics, runes, essences, uniques, charms, jewels, tablets, and other configured market categories.
- Prices Uncut Skill, Spirit, and Support Gems by exact type and level.
- Understands stack counts and shows the total value.
- Limits OCR/network work and pauses scanning while PoE is unfocused by default.
- Contains no external software auto-update mechanism.

## Install

Run `install/Poe2GroundLootPriceHelper-v1.0.0-Setup.exe`. It installs per user, requires no administrator rights, and includes the required .NET runtime. Close an older running copy before installing.

After launch, choose the league and start the ground-loot scan. Settings include capture mode, language, theme, hotkeys, focus pause, and diagnostics.

## Price and network behavior

League lists and market prices come from poe.ninja. The program downloads only those read-only market feeds and referenced icons; it does not upload screenshots, OCR results, configuration, or gameplay data. Price refreshes are not application updates.

## Development

See the [developer documentation](docs/README.md), [fork provenance](FORK_NOTES.md), [changelog](CHANGELOG.md), and [1.0.0 release record](docs/releases/1.0.0.md).

```powershell
dotnet test PoeAncientsPriceHelper.sln -c Release
powershell -NoProfile -ExecutionPolicy Bypass -File .\installer\Build-Installer.ps1
```

This fork was derived from [pedro-quiterio/PoeAncientsPriceHelper](https://github.com/pedro-quiterio/PoeAncientsPriceHelper) version 3.7.1 and uses its own version sequence beginning at 1.0.0.

# PoE 2 Ground Loot + Detailed Trade Price Helper — fork 1.3.1

Windows overlay for Path of Exile 2 that scans visible ground-item labels and places the current market price immediately after each label. It keeps the game's item name visible and works across the viewport rather than only a calibrated price panel.

Version 1.3.1 fixes all interaction surfaces in the 420-DIP Professional Compact window: Edit unlocks selectable Min/Max fields, modifier checkboxes have dedicated hit areas, the title drag surface no longer intercepts buttons, `Requires:` lines stay out of modifier filters, and every fetched listing can open its official Trade result. Hover an item in PoE 2 and press the game's normal `Ctrl+C`; the helper reads that copied item only.

## Highlights

- Discovers current PoE 2 leagues from poe.ninja, including future leagues, with cached/offline fallback.
- Reloads prices for the exact league selected in the UI.
- Covers currency, fragments, mechanics, runes, essences, uniques, charms, jewels, tablets, and other configured market categories.
- Prices Uncut Skill, Spirit, and Support Gems by exact type and level.
- Understands stack counts and shows the total value.
- Limits OCR/network work and pauses scanning while PoE is unfocused by default.
- Contains no external software auto-update mechanism.
- Checks modifier-dependent equipment through Path of Exile Trade without cookies, account credentials, input synthesis, or game-file/memory access.

## Install

Run `install/Poe2GroundLootPriceHelper-v1.3.1-Setup.exe`. It installs per user, requires no administrator rights, and includes the required .NET runtime. Close an older running copy before installing.

After launch, choose the league and start the ground-loot scan. For detailed equipment pricing, hover the item and press `Ctrl+C`; supported weapons, armour, accessories, jewels, and charms open the filter window. Settings can disable this behavior.

## Price and network behavior

League lists, commodity prices, and currency conversion rates come from poe.ninja. Modifier-dependent equipment searches use the anonymous Path of Exile Trade search/fetch surface. The program does not upload screenshots, OCR results, configuration, gameplay data, raw clipboard history, cookies, tokens, or account credentials. Price refreshes are not application updates.

The Trade website search surface can change without notice. The helper honors returned rate-limit headers and never retries a restricted request automatically. This product isn't affiliated with or endorsed by Grinding Gear Games in any way.

## Development

See the [developer documentation](docs/README.md), [overlay module notes](overlay/README.md), [fork provenance](FORK_NOTES.md), [changelog](CHANGELOG.md), and [1.3.1 release record](docs/releases/1.3.1.md).

```powershell
dotnet test PoeAncientsPriceHelper.sln -c Release
powershell -NoProfile -ExecutionPolicy Bypass -File .\installer\Build-Installer.ps1
```

This fork was derived from [pedro-quiterio/PoeAncientsPriceHelper](https://github.com/pedro-quiterio/PoeAncientsPriceHelper) version 3.7.1 and uses its own version sequence beginning at 1.0.0.

# Development workflow

## Requirements

- Windows 10/11 x64
- .NET 10 SDK
- PowerShell 5.1+ and Windows IExpress for installer packaging

## Common commands

```powershell
dotnet restore PoeAncientsPriceHelper.sln
dotnet test PoeAncientsPriceHelper.sln -c Release
dotnet build PoeAncientsPriceHelper.sln -c Release
powershell -NoProfile -ExecutionPolicy Bypass -File .\installer\Build-Installer.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\installer\Verify-Installer.ps1
```

Run tests before and after behavior changes. Add a failing regression test first for fixes. Keep captures, caches, and generated artifacts out of Git.

## Extending the application

New leagues normally need no code change: `LeagueCatalog` consumes poe.ninja's league endpoint. Update the built-in emergency list only when useful for offline first launch. A new market category belongs in the relevant array in `PriceRepository`, with response-parsing tests. OCR matching changes require clean, noisy, and false-positive test cases.

For diagnostics, launch with `--debug`; logs and debug images are written beneath `%LOCALAPPDATA%\PoeAncientsPriceHelper`.

## Release checklist

1. Choose the next SemVer from `docs/VERSIONING.md`.
2. Update the project version, UI wording, installer version/name, and `CHANGELOG.md`.
3. Add `docs/releases/X.Y.Z.md`; never edit old release files.
4. Run all tests, Release build, package validation, updater scan, and dependency vulnerability scan.
5. Build the self-contained installer and record its SHA-256.
6. Confirm only the intended installer is in `install/`, commit, and push only to the owner-approved remote.

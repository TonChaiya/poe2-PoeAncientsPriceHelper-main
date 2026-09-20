# Detailed Trade overlay module

`PoeTradeOverlay` is a WPF class library shipped inside the main helper. It is not a separate executable and has no access to game files, process memory, browser state, credentials, or input injection.

## Boundaries

- `Clipboard/` performs four bounded reads after the host observes a manual focused-game `Ctrl+C`.
- `Parsing/` validates copied PoE 2 item text and keeps commodities on the existing poe.ninja path.
- `Trade/` owns metadata, unique stat-template matching, request DTOs, anonymous search/fetch, body limits, cancellation, and rate limits.
- `Pricing/` converts listing currencies through a read-only host snapshot and calculates credible estimates.
- `Presentation/` owns filter rows, view state, placement, and the WPF window.
- `TradeOverlayController` is the single-request orchestration and stale-result guard.

## Maintenance rules

Keep all Trade endpoint paths and JSON details inside `Trade/`. Add or update sanitized fixtures before changing item parsing. A modifier may be sent only when exactly one normalized stat template of the compatible kind matches. Do not log or cache raw copied item text.

Run:

```powershell
dotnet test .\overlay\PoeTradeOverlay.Tests\PoeTradeOverlay.Tests.csproj
```

The public website search surface is not guaranteed stable. A schema/authentication change must fail visibly and remain isolated from the ground-loot scanner; never work around it by reading browser cookies or asking for account credentials.

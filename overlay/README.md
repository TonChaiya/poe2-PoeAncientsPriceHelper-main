# Detailed Trade overlay module

`PoeTradeOverlay` is a WPF class library shipped inside the main helper. It is not a separate executable and has no access to game files, process memory, browser state, credentials, or input injection.

## Boundaries

- `Clipboard/` performs four bounded reads after the host observes a manual focused-game `Ctrl+C`.
- `Parsing/` validates copied PoE 2 item text and separates requirements/properties/states from typed modifier blocks.
- `Trade/` owns metadata families, evidence-ordered resolution, three search profiles, request DTOs, anonymous Instant Buy search/fetch, body limits, cancellation, and rate limits.
- `Pricing/` resolves Trade currency codes through the selected-league snapshot, preserves original prices, and calculates credible estimates with conversion coverage.
- `Presentation/` owns filter rows, view state, placement, and the WPF window.
- `TradeOverlayController` is the single-request orchestration and stale-result guard.

## Maintenance rules

Keep all Trade endpoint paths and JSON details inside `Trade/`. Add or update sanitized fixtures before changing item parsing. A modifier may be sent only when exactly one normalized stat template of the compatible family matches. Ambiguous lines remain visible and disabled. Do not log or cache raw copied item text.

Run:

```powershell
dotnet test .\overlay\PoeTradeOverlay.Tests\PoeTradeOverlay.Tests.csproj
```

The public website search surface is not guaranteed stable. A schema/authentication change must fail visibly and remain isolated from the ground-loot scanner; never work around it by reading browser cookies or asking for account credentials.

## Focus behavior

The 420-DIP Professional Compact window starts in passive mode with the native `WS_EX_NOACTIVATE` extended style and rejects mouse activation. Automatic display, profile/filter mouse controls, title dragging, closing, and Search therefore do not activate it. `Edit` explicitly removes that style so text boxes can receive keyboard input; Done, Escape, and Search restore passive mode and the previous foreground window. Players using exclusive fullscreen should switch PoE 2 to Borderless before entering edit mode. This is an ordinary external window policy and does not inject into the game or use an in-game overlay SDK.

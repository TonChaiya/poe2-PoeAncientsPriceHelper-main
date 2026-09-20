# Project structure

| Path | Purpose |
|---|---|
| `src/PoeAncientsPriceHelper/` | WPF application, scanning, OCR, pricing, overlays, bundled runtime assets. |
| `src/PoeAncientsPriceHelper.Tests/` | xUnit behavior and regression tests. |
| `overlay/PoeTradeOverlay/` | Isolated parser, Trade metadata/query/client, estimator, controller, and WPF filter window. |
| `overlay/PoeTradeOverlay.Tests/` | Unit/integration tests using fixtures and fake HTTP/clipboard dependencies. |
| `installer/` | Reproducible publish and per-user IExpress installer scripts. |
| `install/` | Final distributable setup executable only. |
| `docs/` | Living technical documentation and immutable release records. |
| `docs/releases/` | One permanent Markdown record per released fork version. |
| `old/` | Local archive supplied by the owner; intentionally ignored and never published. |
| `.artifacts/` | Temporary build/review/extraction output; ignored. |

Core ownership boundaries: `LeagueCatalog` owns league discovery and cache policy; `PriceRepository` owns market snapshots; scan engines own capture/OCR scheduling; overlays own drawing only; `MainWindow` coordinates lifecycle and user selection.

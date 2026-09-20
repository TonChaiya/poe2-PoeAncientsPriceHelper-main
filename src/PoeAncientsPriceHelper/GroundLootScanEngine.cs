using System.Diagnostics;
using System.Drawing;

namespace PoeAncientsPriceHelper;

// Watches the PoE client viewport and attaches a price to every visible ground label that can be
// valued from poe.ninja. It is intentionally independent from the old calibrated list-panel loop:
// full-frame OCR has a much lower cadence and uses each OCR line's X/Y bounds for placement.
internal sealed class GroundLootScanEngine : IDisposable
{
    private readonly AppConfig _config;
    private readonly PriceRepository _prices;
    private readonly IconCache _icons;
    private readonly IScreenCaptureBackend _capture;
    private readonly NameTranslator _translator;
    private readonly Dictionary<string, (string? Key, bool Exact)> _cache = new();
    private CancellationTokenSource? _cts;
    private Task? _loopTask;
    private int _generation = -1;
    private static long _dismissedUntil;
    private static volatile bool _showing;

    private static readonly Regex TrailingQuantity = new(
        @"(?:\s+[x\u00D7]\s*(\d{1,4})|\s*[(\[]\s*(\d{1,4})\s*[)\]])\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex LeadingQuantity = new(
        @"^\s*(\d{1,4})\s*[x\u00D7]\s+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public bool IsRunning => _loopTask is { IsCompleted: false };
    public static bool IsShowing
    {
        get => _showing;
        private set
        {
            if (_showing == value) return;
            _showing = value;
            App.UpdateClickWatcher();
        }
    }

    public static void RequestDismiss() => Interlocked.Exchange(ref _dismissedUntil, Environment.TickCount64 + 900);

    public GroundLootScanEngine(AppConfig config, PriceRepository prices, IconCache icons,
        IScreenCaptureBackend capture)
    {
        _config = config;
        _prices = prices;
        _icons = icons;
        _capture = capture;
        _translator = NameTranslator.ForLanguage(config.GameLanguage);
    }

    public void Start()
    {
        if (IsRunning) return;
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        _loopTask = Task.Run(() => RunAsync(_cts.Token));
    }

    public void StopAndWait(TimeSpan timeout)
    {
        _cts?.Cancel();
        try { _loopTask?.Wait(timeout); } catch { }
    }

    private async Task RunAsync(CancellationToken ct)
    {
        var scanner = new OcrScanner(App.DebugMode ? Console.WriteLine : null, false, _config.GameLanguage);
        Rectangle lastViewport = Rectangle.Empty;
        const int ActiveIntervalMs = 650;  // <=1.54 full-screen OCR passes/s; keeps game CPU/GPU headroom
        const int IdleIntervalMs = 900;

        while (!ct.IsCancellationRequested)
        {
            var started = Stopwatch.GetTimestamp();
            try
            {
                bool foundGame = GameWindow.TryGet(out var game);
                if (!foundGame && _config.IsCalibrated)
                    game = new GameWindowInfo(IntPtr.Zero, _config.RegionRect, true, "calibrated-fallback");
                if ((!foundGame && !_config.IsCalibrated) ||
                    (_config.PauseWhenGameNotFocused && !game.IsForeground))
                {
                    SetOverlay([], lastViewport, false);
                    await Task.Delay(IdleIntervalMs, ct);
                    continue;
                }

                // Exclude the bottom HUD strip and a very thin top strip. Ground labels cannot be
                // interacted with behind those controls; omitting them also prevents UI text from
                // becoming OCR candidates and reduces the full-frame pixel count.
                var viewport = game.ClientBounds;
                int topTrim = Math.Max(2, viewport.Height / 100);
                int bottomTrim = Math.Max(24, viewport.Height * 8 / 100);
                var scanRect = Rectangle.FromLTRB(viewport.Left, viewport.Top + topTrim,
                    viewport.Right, viewport.Bottom - bottomTrim);
                lastViewport = viewport;

                using var bmp = _capture.CaptureRegion(scanRect);
                var lines = scanner.RecognizeLines(bmp, invert: true);
                var rows = Resolve(lines, scanRect, viewport);
                bool dismissed = Environment.TickCount64 < Interlocked.Read(ref _dismissedUntil);
                SetOverlay(dismissed ? [] : rows, viewport, !dismissed && rows.Count > 0);

                if (App.DebugMode)
                    Console.WriteLine($"[ground-loot] OCR lines={lines.Count} priced={rows.Count} region={scanRect}");
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                if (App.DebugMode) Console.Error.WriteLine($"[ground-loot] {ex.GetType().Name}: {ex.Message}");
                SetOverlay([], lastViewport, false);
            }

            int elapsed = (int)Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            int wait = Math.Max(20, ActiveIntervalMs - elapsed);
            try { await Task.Delay(wait, ct); } catch (OperationCanceledException) { break; }
        }

        IsShowing = false;
        PriceOverlayManager.Hide();
    }

    private void SetOverlay(IReadOnlyList<PriceRow> rows, Rectangle viewport, bool showing)
    {
        IsShowing = showing;
        if (viewport.Width <= 0 || viewport.Height <= 0)
        {
            PriceOverlayManager.HideNow();
            return;
        }
        PriceOverlayManager.EnsureVisible(viewport, _config.OverlayXOffset, _icons);
        PriceOverlayManager.UpdateState(rows, showing, false,
            App.DebugMode ? $"ground labels: {rows.Count}" : null);
    }

    private List<PriceRow> Resolve(IReadOnlyList<OcrTextLine> lines, Rectangle scanRect, Rectangle viewport)
    {
        if (_generation != _prices.PriceGeneration)
        {
            _generation = _prices.PriceGeneration;
            _cache.Clear();
        }

        var snap = _prices.Current;
        var result = new List<PriceRow>();
        foreach (var line in lines)
        {
            var (raw, multiplier, explicitQuantity) = ParseGroundLabel(line.Text);

            var normalized = _translator.Translate(NameNormalizer.Normalize(raw));
            if (normalized.Length < 4) continue;
            var resolved = ResolveName(normalized, snap);
            if (resolved.Key is null || !snap.Prices.TryGetValue(resolved.Key, out var price)) continue;

            int centerY = scanRect.Top + line.Bounds.Top + line.Bounds.Height / 2 - viewport.Top;
            int absoluteRight = scanRect.Left + line.Bounds.Right;
            var absoluteBounds = new Rectangle(scanRect.Left + line.Bounds.Left,
                scanRect.Top + line.Bounds.Top, line.Bounds.Width, line.Bounds.Height);
            result.Add(new PriceRow(centerY, line.Text, price.DivineValue, price.ExaltedValue,
                true, multiplier, resolved.Key, resolved.Exact,
                price.HasMarketData ? MemeKind.None : MemeKind.NoInfo,
                MultiplierExplicit: explicitQuantity, PriceX: absoluteRight + _config.OverlayXOffset,
                LabelBounds: absoluteBounds));
        }
        return result;
    }

    internal static (string Name, int Multiplier, bool Explicit) ParseGroundLabel(string text)
    {
        var raw = text.Trim();
        var leading = LeadingQuantity.Match(raw);
        if (leading.Success)
        {
            int leadingMultiplier = int.TryParse(leading.Groups[1].Value, out var leadingN) && leadingN > 0 ? leadingN : 1;
            return (raw[leading.Length..].TrimStart(), leadingMultiplier, true);
        }
        var match = TrailingQuantity.Match(raw);
        if (!match.Success) return (raw, 1, false);
        var digits = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
        int multiplier = int.TryParse(digits, out var n) && n > 0 ? n : 1;
        return (raw[..match.Index].TrimEnd(), multiplier, true);
    }

    private (string? Key, bool Exact) ResolveName(string name, PriceSnapshot snap)
    {
        if (_cache.TryGetValue(name, out var hit)) return hit;
        if (snap.Prices.ContainsKey(name)) return _cache[name] = (name, true);

        string lookup = name.Any(char.IsDigit) ? NameNormalizer.DigitFold(name) : name;
        if (snap.Prices.ContainsKey(lookup)) return _cache[name] = (lookup, true);

        string? best = null;
        double bestScore = 0.87; // tighter than panel OCR: full-screen text has more false candidates
        for (int len = Math.Max(4, lookup.Length - 2); len <= lookup.Length + 2; len++)
        {
            if (!snap.KeysByLength.TryGetValue(len, out var keys)) continue;
            foreach (var key in keys)
            {
                double score = 1.0 - (double)ScanEngine.Levenshtein(lookup, key) /
                    Math.Max(lookup.Length, key.Length);
                if (score > bestScore) { bestScore = score; best = key; }
            }
        }
        return _cache[name] = (best, bestScore >= 0.94);
    }

    public void Dispose()
    {
        StopAndWait(TimeSpan.FromSeconds(2));
        _cts?.Dispose();
        _capture.Dispose();
    }
}

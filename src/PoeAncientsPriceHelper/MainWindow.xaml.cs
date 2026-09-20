using System.Net;
using System.Net.Http;
using System.Windows;
using System.Windows.Media;
using SharpHook.Data;

namespace PoeAncientsPriceHelper;

public partial class MainWindow : Window
{
    private AppConfig _config = new();
    private PriceRepository? _repo;
    private IconCache? _icons;
    private GroundLootScanEngine? _engine;
    // Rumour helper: bundled data + a dedicated capture backend, plus the WORLD-gated auto-detect loop
    // (#35). Created once on load; the loop runs in the background (idle off the Atlas map).
    private RumourRepository? _rumours;
    private RumourScanner? _rumourScanner;
    private IScreenCaptureBackend? _rumourCapture;
    private RumourScanEngine? _rumourEngine;
    // 15s cap so a stalled poe.ninja/poecdn connection can't hang a whole fetch cycle for the
    // default 100s. Per-fetch cancellation (shutdown) is handled inside PriceRepository.
    // HTTP/2 + compression enabled for faster parallel fetches (5 concurrent requests multiplexed
    // over a single connection instead of 5 separate TCP handshakes).
    private readonly HttpClient _http = new(new SocketsHttpHandler
    {
        AutomaticDecompression = System.Net.DecompressionMethods.All,
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        MaxConnectionsPerServer = 4,
        // Happy Eyeballs (#16): race IPv4/IPv6 connects so a dead IPv6 path (Starlink/CGNAT) falls
        // back to IPv4 in a fraction of a second instead of stalling out the 15s timeout below.
        ConnectCallback = static (context, ct) => HappyEyeballs.ConnectAsync(context.DnsEndPoint, ct)
    })
    {
        Timeout = TimeSpan.FromSeconds(15),
        DefaultRequestVersion = HttpVersion.Version20,
        DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower
    };
    private bool _loading;
    private IReadOnlyList<LeagueOption> _leagueOptions = [];
    // Reentrancy guard for LeagueBox_SelectionChanged → StartupAsync (rapid league changes could
    // otherwise overlap and dispose repo/icons mid-fetch). Also remembers whether the scanner was
    // running before a league change so StartupAsync can restart it against the new repo/icons.
    private bool _startingUp;
    private bool _engineWasRunning;

    // Minimize-to-tray (#2). The window hides to a tray icon on minimize and restores from it; the X
    // button still fully exits. Scanning is independent of this window, so it keeps running in the tray.
    private System.Windows.Forms.NotifyIcon? _trayIcon;
    private bool _trayBalloonShown;

    // All three hotkeys (Start/Stop, Debug, Calibrate) now live on the App-level SharpHook hook and are
    // user-configurable — no Win32 RegisterHotKey here anymore.

    public MainWindow()
    {
        InitializeComponent();
        var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        if (v is not null) VersionLabel.Text = $"v{v.Major}.{v.Minor}.{v.Build}";
        Loaded += OnLoaded;
        StateChanged += OnStateChanged;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _config = ConfigStore.Load();
        var catalog = await new LeagueCatalog(_http).LoadAsync();
        _leagueOptions = catalog.Leagues;
        var selected = _leagueOptions.FirstOrDefault(x =>
            string.Equals(x.Id, _config.LeagueName, StringComparison.OrdinalIgnoreCase))
            ?? _leagueOptions.First();
        if (!string.Equals(_config.LeagueName, selected.Id, StringComparison.Ordinal))
        {
            _config.LeagueName = selected.Id;
            ConfigStore.Save(_config);
        }
        PopulateFields();
        // When already running in debug mode the relaunch is pointless — repurpose the link to open the
        // folder the logs land in. App.DebugMode is settled by now (it's set in App.OnStartup, before the
        // window's Loaded fires).
        if (App.DebugMode)
        {
            DiagnosticsLink.Text = "Open logs";
            DiagnosticsLink.ToolTip = "Open the folder with scan_log.txt and debug_ocr.png";
        }
        await StartupAsync();
        InitRumourHelper();
        // Auto-start QoL: with a calibrated region and the option enabled, begin scanning and drop
        // straight to the tray, so the user just opens the app and it runs (saving the Start + minimize
        // clicks). Skipped under --debug (keep the window and console visible for troubleshooting) and
        // when not calibrated (the user has to calibrate first, so the window must stay up for input).
        // _engine is null here on a fresh load (StartupAsync only restarts it across a league change).
        if (_config.AutoStart && !App.DebugMode && _engine is null)
        {
            ToggleStartStop();                     // start the engine; flips the button to Stop
            WindowState = WindowState.Minimized;   // OnStateChanged hides to tray + shows the balloon once
        }
    }

    private void CreditsLink_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        new CreditsWindow { Owner = this }.ShowDialog();
    }

    // "Diagnostics" link. In a normal run it offers to restart with --debug so the user can capture
    // scan_log.txt / debug_ocr.png for a bug report. When already running in debug mode it instead
    // opens the folder those files land in.
    private void DiagnosticsLink_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (App.DebugMode) { OpenDataFolder(); return; }

        var choice = System.Windows.MessageBox.Show(this,
            "Restart with diagnostics logging enabled?\n\n" +
            "A console window will open and detailed logs (scan_log.txt and debug_ocr.png) will be " +
            "written to your data folder so you can attach them to a bug report.\n\n" +
            "The app will close and reopen now.",
            "Diagnostics", System.Windows.MessageBoxButton.OKCancel, System.Windows.MessageBoxImage.Question);
        if (choice != System.Windows.MessageBoxResult.OK) return;

        if (App.RelaunchWithDebug())
            Close();   // routes through Window_Closing for a clean shutdown; the new --debug copy takes over
        else
            System.Windows.MessageBox.Show(this,
                "Couldn't restart in diagnostics mode. You can launch the app from a terminal with the " +
                "--debug switch instead.",
                "Diagnostics", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
    }

    private void OpenDataFolder()
    {
        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(AppPaths.DataDir) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Diagnostics] open data folder failed: {ex.Message}");
        }
    }

    private void PopulateFields()
    {
        _loading = true;
        LeagueBox.ItemsSource = _leagueOptions;
        LeagueBox.SelectedItem = _leagueOptions.FirstOrDefault(x =>
            string.Equals(x.Id, _config.LeagueName, StringComparison.OrdinalIgnoreCase));
        // Arm the global hook with all three persisted bindings. The keybind UI lives in the Settings
        // window now, but the hook must be armed at startup so the hotkeys work before it's ever opened.
        App.SetStartStopChord(HotkeyBinding.ParseChord(_config.StartStopHotkey));
        App.SetDebugChord(HotkeyBinding.ParseChord(_config.DebugHotkey));
        App.SetCalibrateChord(HotkeyBinding.ParseChord(_config.CalibrateHotkey));
        UpdateRegionLabel();
        ThemePresets.Apply(ThemePresets.Resolve(_config.Theme));
        _loading = false;
    }

    // Opens the modal Settings window. It edits the same _config instance and persists each change, so
    // nothing needs syncing back here: theme is applied app-wide live, hotkey rebinds re-arm the hook,
    // and capture/auto-start are read straight from _config when next needed.
    private void SettingsButton_Click(object sender, RoutedEventArgs e) =>
        new SettingsWindow(_config, RefreshRumourDataAsync) { Owner = this }.ShowDialog();

    // Opens the bundled HTML guide (docs\README.html, shipped next to the exe) in the default browser.
    // Falls back to the online README if the local copy is somehow missing.
    private void HelpButton_Click(object sender, RoutedEventArgs e)
    {
        var local = System.IO.Path.Combine(AppContext.BaseDirectory, "docs", "README.html");
        var target = System.IO.File.Exists(local)
            ? local
            : "https://github.com/TonChaiya/poe2-PoeAncientsPriceHelper-main#readme";
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Help] failed to open guide: {ex.Message}");
        }
    }

    // "Refresh from sheet" (#37): pull the latest rumour CSV, cache it, and swap it into the running
    // scanner so it applies live. On failure the existing data is kept; the result message is shown in
    // Settings.
    internal async Task<RumourRefreshResult> RefreshRumourDataAsync()
    {
        InitRumourHelper();
        var result = await RumourRefresher.RefreshAsync(_http);
        if (result is { Success: true, Repository: not null })
            _rumourScanner!.UseData(result.Repository);
        return result;
    }

    private void UpdateRegionLabel()
    {
        RegionLabel.Text = "PoE 2 viewport (automatic)";
    }

    private async Task StartupAsync()
    {
        StatusLabel.Text = "Fetching prices from poe.ninja…";
        StartStopButton.IsEnabled = false;

        // Stop the scanner before disposing the repo/icons it depends on (league change, initial load).
        // Otherwise a running ScanEngine keeps referencing the OLD repo (stale prices) and icons
        // (disposed IconCache → crashes/stale icons).
        _engineWasRunning = _engine is { IsRunning: true };
        if (_engine is not null)
        {
            _engine.StopAndWait(TimeSpan.FromSeconds(2));
            _engine.Dispose();
            _engine = null;
        }

        _repo?.Dispose();
        _icons?.Dispose();

        _repo = new PriceRepository(_http);
        _repo.PricesUpdated += OnPricesUpdated;   // keep the "last fetch" label live on each refresh
        _repo.FetchFailed += OnFetchFailed;       // flag a failed fetch (red/amber) in the status label
        _icons = new IconCache(_http);

        await Task.WhenAll(
            _repo.InitialFetchAsync(_config),
            _icons.LoadAsync());

        _repo.StartAutoRefresh(_config);

        UpdateStatusLabel();
        StartStopButton.IsEnabled = _repo.ItemCount > 0;

        // If the scanner was running before the league change, restart it with the new repo/icons.
        if (_engineWasRunning)
        {
            _engine = new GroundLootScanEngine(_config, _repo, _icons, CreateCaptureBackend());
            _engine.Start();
            StartStopButton.Content = "Stop Ground Loot Scan";
            StartStopButton.Background = System.Windows.Media.Brushes.DarkRed;
        }
        else
        {
            StartStopButton.Content = "Start Ground Loot Scan";
            StartStopButton.Background = System.Windows.Media.Brushes.DarkGreen;
        }
        _engineWasRunning = false;
    }

    // The 30-min background refresh fires on a thread-pool thread — marshal to the UI thread
    // before touching the label. (Previously the label was set once at startup and never updated,
    // so it stayed frozen at the launch-time fetch even though prices kept refreshing.)
    private void OnPricesUpdated()
    {
        Dispatcher.BeginInvoke(UpdateStatusLabel);
    }

    // Status-label colors: normal (matches the XAML default), amber when a refresh failed but prices
    // from an earlier fetch are still showing, red when no prices have ever loaded.
    private static readonly System.Windows.Media.Brush StatusNormalBrush = FrozenBrush(0x66, 0x66, 0x66);
    private static readonly System.Windows.Media.Brush StatusStaleBrush = FrozenBrush(0xE0, 0xB0, 0x60);
    private static readonly System.Windows.Media.Brush StatusFailBrush = FrozenBrush(0xFF, 0x55, 0x55);

    private static System.Windows.Media.Brush FrozenBrush(byte r, byte g, byte b)
    {
        var brush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    private void UpdateStatusLabel()
    {
        if (_repo is null) return;
        // Called after a fetch completes (initial load + each successful refresh). If we still have no
        // prices, the fetch failed — show the red state (the repo is retrying every 30s) rather than a
        // misleading "0 items loaded". This also avoids a successful-path call clobbering the failure.
        if (_repo.ItemCount == 0)
        {
            StatusLabel.Foreground = StatusFailBrush;
            StatusLabel.Text = "Failed to get prices from poe.ninja — retrying…";
            return;
        }
        string fetched = _repo.LastFetchedAt is { } t ? t.ToString("MMM d HH:mm") : "never";
        StatusLabel.Foreground = StatusNormalBrush;
        StatusLabel.Text = $"{_repo.ItemCount} items loaded  ·  last fetch {fetched}";
    }

    // A fetch failed (network error or 0 items). Prices already loaded from a prior fetch are kept, so
    // distinguish "couldn't refresh, still showing the last set" (amber) from "never got any" (red).
    // The repository retries every 30s until it succeeds, when OnPricesUpdated restores the normal label.
    private void OnFetchFailed()
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (_repo is null) return;
            if (_repo.ItemCount > 0)
            {
                string t = _repo.LastFetchedAt is { } at ? at.ToString("MMM d HH:mm") : "earlier";
                StatusLabel.Foreground = StatusStaleBrush;
                StatusLabel.Text = $"Couldn't refresh — retrying (showing last fetch {t})";
            }
            else
            {
                StatusLabel.Foreground = StatusFailBrush;
                StatusLabel.Text = "Failed to get prices from poe.ninja — retrying…";
            }
        });
    }

    private void DonateButton_Click(object sender, RoutedEventArgs e)
    {
        const string url = "https://www.paypal.com/donate/?business=pedro.levi.magic%40gmail.com&currency_code=USD&item_name=PoeAncientsPriceHelper";
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Donate] failed to open browser: {ex.Message}");
        }
    }

    // internal so the App-level hook (configurable Calibrate key) can trigger it too.
    internal void RunCalibration()
    {
        var rect = CalibrationOverlay.RunOnStaThread();
        if (rect is null) return;
        _config.RegionRect = rect.Value;
        ConfigStore.Save(_config);
        Dispatcher.Invoke(() =>
        {
            UpdateRegionLabel();
            StartStopButton.IsEnabled = _repo is { ItemCount: > 0 };
        });
    }

    private void CalibrateButton_Click(object sender, RoutedEventArgs e) => RunCalibration();

    // Creates the rumour helper (bundled data + dedicated capture backend + scanner) and starts the
    // WORLD-gated auto-detect loop (#35). Idempotent and independent of the price repo/icons, so it is
    // created once on load and not torn down on a league change.
    private void InitRumourHelper()
    {
        if (_rumourScanner is not null) return;
        _rumours = RumourRepository.Load();   // refreshed cache if present, else bundled snapshot
        _rumourCapture = CreateCaptureBackend();
        _rumourScanner = new RumourScanner(_rumourCapture, new OcrScanner(), _rumours);
        _rumourEngine = new RumourScanEngine(_rumourScanner, RumourScreen,
            () => _config.RumourHelperEnabled, () => _config.RumourScanIntervalMs, ManualWorldRegion);
        _rumourEngine.Start();
    }

    // The screen the rumour loop watches: the monitor PoE runs on (derived from the calibrated price
    // region when available), else the primary monitor.
    // The manual WORLD gate region (#45), or null to let the loop auto-detect it. Read live each gate
    // tick, so toggling auto-detect or re-marking the region in Settings takes effect without a restart.
    private System.Drawing.Rectangle? ManualWorldRegion() =>
        !_config.RumourWorldAutoDetect && _config.HasManualWorldRegion ? _config.RumourWorldRect : null;

    private System.Drawing.Rectangle RumourScreen() =>
        GameWindow.TryGet(out var game)
            ? game.ClientBounds
            : _config.IsCalibrated
                ? System.Windows.Forms.Screen.FromRectangle(_config.RegionRect).Bounds
                : (System.Windows.Forms.Screen.PrimaryScreen?.Bounds ?? new System.Drawing.Rectangle(0, 0, 1920, 1080));

    // Debug-only one-shot rumour read (#34 spine): detect the Island Rumours panel now and show the
    // ratings next to it. Wired to F8 via the App hook under --debug; the auto-detect loop (#35) is the
    // real interaction. Capture + OCR run off the UI thread.
    internal void RunRumourScanOnce()
    {
        InitRumourHelper();
        var scanner = _rumourScanner!;
        var screen = RumourScreen();
        Task.Run(() =>
        {
            try
            {
                var result = scanner.ReadOnce(screen);
                if (result is { Rows.Count: > 0 })
                    RumourOverlayManager.Show(result.Rows, result.PanelBounds);
                else
                    RumourOverlayManager.HideNow();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[Rumour] scan failed: {ex.Message}");
            }
        });
    }

    private void StartStopButton_Click(object sender, RoutedEventArgs e) => ToggleStartStop();

    // Selects the screen-capture backend based on config. "GDI" forces legacy BitBlt;
    // "Auto"/"WGC" use Windows Graphics Capture (GPU) with built-in GDI fallback per call.
    private IScreenCaptureBackend CreateCaptureBackend() =>
        _config.CaptureBackend == "GDI"
            ? new GdiScreenCaptureBackend()
            : new WgcScreenCaptureBackend();

    // Shared by the Start/Stop button and the configurable global hotkey (invoked via App, marshalled
    // to the UI thread). internal so the App-level hook can reach it.
    internal void ToggleStartStop()
    {
        if (_engine is null)
        {
            // The hotkey can fire even when the button is disabled — don't start until we're ready.
            if (_repo is null || _icons is null || _repo.ItemCount == 0) return;
            _engine = new GroundLootScanEngine(_config, _repo, _icons, CreateCaptureBackend());
            _engine.Start();
            StartStopButton.Content = "Stop Ground Loot Scan";
            StartStopButton.Background = System.Windows.Media.Brushes.DarkRed;
        }
        else
        {
            _engine.StopAndWait(TimeSpan.FromSeconds(2));
            _engine.Dispose();
            _engine = null;
            StartStopButton.Content = "Start Ground Loot Scan";
            StartStopButton.Background = System.Windows.Media.Brushes.DarkGreen;
        }
    }

    // Minimize → hide the window and drop to the tray (scanning keeps running). Restore/Exit live on
    // the tray icon. The X button is unaffected and still quits via Window_Closing.
    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (WindowState != WindowState.Minimized) return;
        EnsureTrayIcon();
        _trayIcon!.Visible = true;
        Hide();   // remove the taskbar button; the tray icon is now the way back
        if (!_trayBalloonShown)
        {
            _trayIcon.ShowBalloonTip(3000, "Poe Ancients Price Helper",
                "Still running — double-click the tray icon to restore.",
                System.Windows.Forms.ToolTipIcon.Info);
            _trayBalloonShown = true;
        }
    }

    private void EnsureTrayIcon()
    {
        if (_trayIcon is not null) return;
        var exe = Environment.ProcessPath;
        var icon = exe is not null
            ? System.Drawing.Icon.ExtractAssociatedIcon(exe)
            : System.Drawing.SystemIcons.Application;
        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = icon,
            Text = "Poe Ancients Price Helper",
            Visible = false,
        };
        _trayIcon.DoubleClick += (_, _) => RestoreFromTray();
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Show", null, (_, _) => RestoreFromTray());
        menu.Items.Add("Exit", null, (_, _) => ExitFromTray());
        _trayIcon.ContextMenuStrip = menu;
    }

    private void RestoreFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        if (_trayIcon is not null) _trayIcon.Visible = false;
    }

    private void ExitFromTray()
    {
        if (_trayIcon is not null) _trayIcon.Visible = false;
        Close();   // routes through Window_Closing for the normal shutdown/cleanup
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        _trayIcon?.Dispose();
        _engine?.StopAndWait(TimeSpan.FromSeconds(2));
        _engine?.Dispose();
        _rumourEngine?.Dispose();
        RumourOverlayManager.Close();
        _rumourCapture?.Dispose();
        _repo?.Dispose();
        _icons?.Dispose();
        _http.Dispose();
        System.Windows.Application.Current.Shutdown();
    }

    private async void LeagueBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_loading || LeagueBox.SelectedItem is not LeagueOption league || league.Id == _config.LeagueName) return;
        if (_startingUp) return;  // prevent overlapping StartupAsync calls (rapid league changes)
        _startingUp = true;
        try
        {
            LeagueBox.IsEnabled = false;  // disable during reload
            _config.LeagueName = league.Id;
            ConfigStore.Save(_config);
            await StartupAsync();   // re-fetch prices for the newly selected league
        }
        finally
        {
            LeagueBox.IsEnabled = true;
            _startingUp = false;
        }
    }

}

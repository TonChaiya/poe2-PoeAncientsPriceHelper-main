using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using PoeTradeOverlay.Models;

namespace PoeTradeOverlay.Presentation;

public partial class TradeOverlayWindow : Window, ITradeOverlayView
{
    private readonly TradeOverlayViewModel _viewModel = new();
    private Func<TradeQuery, Task>? _search;
    private bool _shutdown;
    private readonly WindowInteractionMode _interaction = new();
    private IntPtr _handle;
    private HwndSource? _source;
    private IntPtr _returnFocus;
    private bool _dragging;
    private bool _positioned;
    private POINT _dragCursorStart;
    private RECT _dragWindowStart;

    public TradeOverlayWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _handle = new WindowInteropHelper(this).Handle;
        _source = HwndSource.FromHwnd(_handle);
        _source?.AddHook(WindowProc);
        ApplyActivationMode();
    }

    public void SetSearchHandler(Func<TradeQuery, Task> search) => _search = search;

    public void Publish(TradeOverlayState state)
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(() => Publish(state)); return; }
        _viewModel.Apply(state);
        if (!IsVisible)
        {
            PositionWindow();
            Show();
        }
    }

    private async void Search_Click(object sender, RoutedEventArgs e)
    {
        var query = _viewModel.BuildEditedQuery();
        if (query is not null && _search is not null)
        {
            if (_interaction.IsEditing) ResetInteraction(restoreFocus: true);
            await _search(query);
        }
    }

    private void Profile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string tag } && Enum.TryParse<SearchProfile>(tag, out var profile))
            _viewModel.SetProfile(profile);
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (!_interaction.IsEditing)
        {
            _returnFocus = GetForegroundWindow();
            _interaction.BeginEditing();
            _viewModel.BeginEditing();
        }
        else
        {
            _interaction.EndEditing();
            _viewModel.EndEditing();
        }
        EditButton.Content = _interaction.IsEditing ? "Done" : "Edit";
        ApplyActivationMode();
        if (_interaction.IsEditing)
        {
            Activate();
            Focus();
        }
        else RestorePreviousFocus();
    }

    private void OpenListing_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ListingRowViewModel { OpenUrl: { } url } }) return;
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // The result remains visible when Windows has no browser association.
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        ResetInteraction(restoreFocus: true);
        Hide();
    }

    private void Title_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || sender is not UIElement element) return;
        GetCursorPos(out _dragCursorStart);
        GetWindowRect(_handle, out _dragWindowStart);
        _dragging = true;
        element.CaptureMouse();
        e.Handled = true;
    }

    private void Title_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragging || e.LeftButton != MouseButtonState.Pressed) return;
        GetCursorPos(out var cursor);
        var movement = PassiveWindowMovement.FromDrag(
            new Point(_dragCursorStart.X, _dragCursorStart.Y), new Point(cursor.X, cursor.Y),
            new Point(_dragWindowStart.Left, _dragWindowStart.Top));
        var work = SystemParameters.WorkArea;
        int x = (int)Math.Clamp(movement.Target.X, work.Left, Math.Max(work.Left, work.Right - ActualWidth));
        int y = (int)Math.Clamp(movement.Target.Y, work.Top, Math.Max(work.Top, work.Bottom - ActualHeight));
        SetWindowPos(_handle, new IntPtr(-1), x, y, 0, 0, SwpNoSize | SwpNoActivate);
    }

    private void Title_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _dragging = false;
        if (sender is UIElement element) element.ReleaseMouseCapture();
        e.Handled = true;
    }

    private void ApplyActivationMode()
    {
        if (_handle == IntPtr.Zero) return;
        long current = GetWindowLongPtr(_handle, GwlExStyle).ToInt64();
        long next = _interaction.IsEditing
            ? WindowActivationPolicy.MakeInteractive(current)
            : WindowActivationPolicy.MakePassive(current);
        SetWindowLongPtr(_handle, GwlExStyle, new IntPtr(next));
        SetWindowPos(_handle, new IntPtr(-1), 0, 0, 0, 0,
            SwpNoMove | SwpNoSize | SwpNoActivate | SwpFrameChanged);
    }

    private IntPtr WindowProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == WmMouseActivate && !_interaction.IsEditing)
        {
            handled = true;
            return new IntPtr(MaNoActivate);
        }
        return IntPtr.Zero;
    }

    private void PositionWindow()
    {
        if (_positioned) return;
        GetCursorPos(out var cursor);
        var avoid = new Rect(cursor.X - 180, cursor.Y - 45, 360, 90);
        var point = ScreenPlacement.Place(SystemParameters.WorkArea, new Size(Width, Height), avoid);
        Left = point.X;
        Top = point.Y;
        _positioned = true;
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _interaction.IsEditing)
        {
            ResetInteraction(restoreFocus: true);
            e.Handled = true;
        }
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_shutdown) return;
        e.Cancel = true;
        ResetInteraction(restoreFocus: true);
        Hide();
    }

    private void ResetInteraction(bool restoreFocus = false)
    {
        _interaction.Reset();
        _viewModel.EndEditing();
        EditButton.Content = "Edit";
        ApplyActivationMode();
        if (restoreFocus) RestorePreviousFocus();
    }

    private void RestorePreviousFocus()
    {
        if (_returnFocus != IntPtr.Zero && _returnFocus != _handle) SetForegroundWindow(_returnFocus);
        _returnFocus = IntPtr.Zero;
    }

    void ITradeOverlayView.Close()
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(() => ((ITradeOverlayView)this).Close()); return; }
        _shutdown = true;
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_source is not null) _source.RemoveHook(WindowProc);
        base.OnClosed(e);
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT point);
    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);
    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int width, int height,
        uint flags);

    private const int GwlExStyle = -20;
    private const int WmMouseActivate = 0x0021;
    private const int MaNoActivate = 3;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpFrameChanged = 0x0020;
    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }
    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
}

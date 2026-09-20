using System.ComponentModel;
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
        if (query is not null && _search is not null) await _search(query);
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        _interaction.ToggleEditing();
        EditButton.Content = _interaction.IsEditing ? "Done editing" : "Edit filters";
        ApplyActivationMode();
        if (_interaction.IsEditing) Activate();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        ResetInteraction();
        Hide();
    }

    private void Title_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_interaction.IsEditing && e.LeftButton == MouseButtonState.Pressed) DragMove();
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
        GetCursorPos(out var cursor);
        var avoid = new Rect(cursor.X - 180, cursor.Y - 45, 360, 90);
        var point = ScreenPlacement.Place(SystemParameters.WorkArea, new Size(Width, Height), avoid);
        Left = point.X;
        Top = point.Y;
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_shutdown) return;
        e.Cancel = true;
        ResetInteraction();
        Hide();
    }

    private void ResetInteraction()
    {
        _interaction.Reset();
        EditButton.Content = "Edit filters";
        ApplyActivationMode();
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
}

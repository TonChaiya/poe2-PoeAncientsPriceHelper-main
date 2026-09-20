using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using PoeTradeOverlay.Models;

namespace PoeTradeOverlay.Presentation;

public partial class TradeOverlayWindow : Window, ITradeOverlayView
{
    private readonly TradeOverlayViewModel _viewModel = new();
    private Func<TradeQuery, Task>? _search;
    private bool _shutdown;

    public TradeOverlayWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
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
        Hide();
    }

    void ITradeOverlayView.Close()
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(() => ((ITradeOverlayView)this).Close()); return; }
        _shutdown = true;
        Close();
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT point);
    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }
}

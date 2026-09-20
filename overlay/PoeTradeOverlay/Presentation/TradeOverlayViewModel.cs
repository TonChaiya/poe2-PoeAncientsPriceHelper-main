using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using PoeTradeOverlay.Models;

namespace PoeTradeOverlay.Presentation;

public sealed class TradeOverlayViewModel : INotifyPropertyChanged
{
    private TradeOverlayState? _state;
    public ObservableCollection<FilterRowViewModel> Filters { get; } = [];

    public string ItemName => _state is null ? "" :
        string.IsNullOrWhiteSpace(_state.Item.Name) ? _state.Item.BaseType : $"{_state.Item.Name} · {_state.Item.BaseType}";
    public string Status => _state?.Status ?? "Copy an item with Ctrl+C";
    public bool IsLoading => _state?.IsLoading == true;
    public bool HasEstimate => _state?.Estimate is not null;
    public string MatchText => _state?.Estimate is { } e ? $"{e.TotalMatches:N0} matches" : "";
    public string SampleText => _state?.Estimate is { } e ? $"{e.UsableListings:N0} credible listings sampled" : "";
    public string LowestText => _state?.Estimate is { } e ? $"{e.LowestExalted:0.##} exalted" : "—";
    public string RangeText => _state?.Estimate is { } e ? $"{e.RangeLowExalted:0.##}–{e.RangeHighExalted:0.##} exalted" : "—";
    public string MedianText => _state?.Estimate is { } e ? $"{e.MedianExalted:0.##} exalted" : "—";
    public string ConfidenceText => _state?.Estimate is { } e ? $"{e.Confidence} · {e.ConfidenceReason}" : "";
    public bool CanSearch => _state?.Query is not null && !IsLoading && Filters.All(x => x.IsValid);

    public void Apply(TradeOverlayState state)
    {
        bool replaceFilters = _state?.Item != state.Item || _state.Query is null;
        _state = state;
        if (replaceFilters && state.Query is not null)
        {
            Filters.Clear();
            foreach (var filter in state.Query.Filters)
            {
                var row = new FilterRowViewModel(filter);
                row.PropertyChanged += (_, _) => Raise(nameof(CanSearch));
                Filters.Add(row);
            }
        }
        RaiseAll();
    }

    public TradeQuery? BuildEditedQuery() => _state?.Query is { } query && Filters.All(x => x.IsValid)
        ? query with { Filters = Filters.Select(x => x.ToFilter()).ToArray() }
        : null;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Raise([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    private void RaiseAll() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
}

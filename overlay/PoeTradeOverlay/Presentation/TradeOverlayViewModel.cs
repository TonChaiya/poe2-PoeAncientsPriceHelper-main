using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using PoeTradeOverlay.Models;

namespace PoeTradeOverlay.Presentation;

public sealed class TradeOverlayViewModel : INotifyPropertyChanged
{
    private TradeOverlayState? _state;
    private SearchProfile _selectedProfile = SearchProfile.QuickPrice;
    private bool _isEditing;
    public ObservableCollection<FilterRowViewModel> Filters { get; } = [];
    public ObservableCollection<FilterRowViewModel> SupportedFilters { get; } = [];
    public ObservableCollection<FilterRowViewModel> UnsupportedFilters { get; } = [];
    public ObservableCollection<ListingRowViewModel> Listings { get; } = [];

    public string ItemName => _state is null ? "" :
        string.IsNullOrWhiteSpace(_state.Item.Name) ? _state.Item.BaseType : $"{_state.Item.Name} · {_state.Item.BaseType}";
    public string DisplayName => _state?.Item.Name ?? "";
    public string BaseTypeText => _state?.Item.BaseType ?? "";
    public string RarityText => _state?.Item.Rarity.ToString().ToUpperInvariant() ?? "";
    public string ItemLevelText => _state?.Item.ItemLevel is { } level ? $"Item Level {level}" : "";
    public string QualityText => _state?.Item.Quality is { } quality ? $"Quality {quality}%" : "";
    public string CorruptedText => _state?.Item.Corrupted == true ? "Corrupted" : "";
    public string RarityBrush => _state?.Item.Rarity switch
    {
        ItemRarity.Unique => "#AF6025",
        ItemRarity.Rare => "#FFF77A",
        ItemRarity.Magic => "#8888FF",
        _ => "#D7D7D7"
    };
    public string Status => _state?.Status ?? "Copy an item with Ctrl+C";
    public bool IsLoading => _state?.IsLoading == true;
    public bool HasEstimate => _state?.Estimate is not null;
    public string MatchText => _state?.Estimate is { } e ? $"{e.TotalMatches:N0} matches" : "";
    public string SampleText => _state?.Estimate is { } e ? $"{e.UsableListings:N0} credible listings sampled" : "";
    public string LowestText => _state?.Estimate is { } e ? $"{e.LowestExalted:0.##} exalted" : "—";
    public string RangeText => _state?.Estimate is { } e ? $"{e.RangeLowExalted:0.##}–{e.RangeHighExalted:0.##} exalted" : "—";
    public string MedianText => _state?.Estimate is { } e ? $"{e.MedianExalted:0.##} exalted" : "—";
    public string ConfidenceText => _state?.Estimate is { } e ? $"{e.Confidence} · {e.ConfidenceReason}" : "";
    public bool HasUnsupported => UnsupportedFilters.Count > 0;
    public string UnsupportedCountText => $"{UnsupportedFilters.Count} unsupported modifier{(UnsupportedFilters.Count == 1 ? "" : "s")}";
    public bool HasListings => Listings.Count > 0;
    public bool CanSearch => _state?.Query is not null && !IsLoading && Filters.All(x => x.IsValid);
    public SearchProfile SelectedProfile => _selectedProfile;
    public bool IsCraftingBase => _selectedProfile == SearchProfile.CraftingBase;
    public bool IsQuickPrice => _selectedProfile == SearchProfile.QuickPrice;
    public bool IsBroad => _selectedProfile == SearchProfile.Broad;
    public bool IsEditing => _isEditing;

    public void Apply(TradeOverlayState state)
    {
        bool newGeneration = _state?.Generation != state.Generation;
        bool replaceFilters = state.Query is not null &&
                              (newGeneration || _state?.Item != state.Item || _state.Query is null);
        _state = state;
        if (replaceFilters && state.Query is not null) _selectedProfile = state.Query.Profile;
        if (state.Query is null && newGeneration)
        {
            Filters.Clear();
            SupportedFilters.Clear();
            UnsupportedFilters.Clear();
        }
        if (replaceFilters && state.Query is not null)
        {
            Filters.Clear();
            SupportedFilters.Clear();
            UnsupportedFilters.Clear();
            foreach (var filter in state.Query.Filters)
            {
                var row = new FilterRowViewModel(filter);
                row.PropertyChanged += (_, _) => Raise(nameof(CanSearch));
                Filters.Add(row);
                (filter.IsSupported ? SupportedFilters : UnsupportedFilters).Add(row);
            }
        }
        Listings.Clear();
        var conversions = state.ListingConversions ?? [];
        int listingIndex = 0;
        foreach (var listing in state.Listings ?? [])
        {
            Listings.Add(new ListingRowViewModel(listing,
                listingIndex < conversions.Count ? conversions[listingIndex] : null,
                state.League, state.SearchId));
            listingIndex++;
        }
        RaiseAll();
    }

    public TradeQuery? BuildEditedQuery() => _state?.Query is { } query && Filters.All(x => x.IsValid)
        ? query with { Filters = Filters.Select(x => x.ToFilter()).ToArray(), Profile = _selectedProfile }
        : null;

    public void SetProfile(SearchProfile profile)
    {
        _selectedProfile = profile;
        foreach (var filter in Filters) filter.ApplyProfile(profile);
        RaiseAll();
    }

    public void BeginEditing()
    {
        if (_isEditing) return;
        _isEditing = true;
        Raise(nameof(IsEditing));
    }

    public void EndEditing()
    {
        if (!_isEditing) return;
        _isEditing = false;
        Raise(nameof(IsEditing));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Raise([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    private void RaiseAll() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
}

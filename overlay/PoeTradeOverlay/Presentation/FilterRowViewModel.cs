using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using PoeTradeOverlay.Models;
using PoeTradeOverlay.Trade;

namespace PoeTradeOverlay.Presentation;

public sealed class FilterRowViewModel : INotifyPropertyChanged
{
    private bool _isEnabled;
    private string _minText;
    private string _maxText;

    public FilterRowViewModel(TradeFilter filter)
    {
        Source = filter;
        _isEnabled = filter.IsSupported && filter.IsEnabled;
        _minText = filter.Min?.ToString(CultureInfo.InvariantCulture) ?? "";
        _maxText = filter.Max?.ToString(CultureInfo.InvariantCulture) ?? "";
        ClearMin = new DelegateCommand(() => MinText = "");
        ClearMax = new DelegateCommand(() => MaxText = "");
        DecreaseMin = new DelegateCommand(() => Step(nameof(MinText), -1));
        IncreaseMin = new DelegateCommand(() => Step(nameof(MinText), 1));
        DecreaseMax = new DelegateCommand(() => Step(nameof(MaxText), -1));
        IncreaseMax = new DelegateCommand(() => Step(nameof(MaxText), 1));
    }

    public TradeFilter Source { get; }
    public string Text => Source.SourceText;
    public string FamilyCaption => Source.Resolution == ResolutionStatus.Ambiguous
        ? "AMBIGUOUS — NOT SENT"
        : Source.IsSupported ? Source.Kind.ToString().ToUpperInvariant() : "UNSUPPORTED — NOT SENT";
    public bool CanEnable => Source.IsSupported;
    public string SupportLabel => Source.IsSupported ? "Matched" : "Unsupported — not sent";
    public ICommand ClearMin { get; }
    public ICommand ClearMax { get; }
    public ICommand DecreaseMin { get; }
    public ICommand IncreaseMin { get; }
    public ICommand DecreaseMax { get; }
    public ICommand IncreaseMax { get; }

    public bool IsEnabled
    {
        get => _isEnabled;
        set { _isEnabled = CanEnable && value; Changed(); }
    }

    public string MinText
    {
        get => _minText;
        set { _minText = value; Changed(); }
    }

    public string MaxText
    {
        get => _maxText;
        set { _maxText = value; Changed(); }
    }

    public bool IsValid => TryValue(MinText, out var min) && TryValue(MaxText, out var max) &&
                           (min is null || max is null || min <= max);

    public string ValidationMessage => !TryValue(MinText, out _) || !TryValue(MaxText, out _)
        ? "Enter a number or leave blank"
        : !IsValid ? "Minimum must not exceed maximum" : "";

    public TradeFilter ToFilter()
    {
        _ = TryValue(MinText, out var min);
        _ = TryValue(MaxText, out var max);
        return Source with { IsEnabled = IsEnabled, Min = min, Max = max };
    }

    internal void ApplyProfile(SearchProfile profile)
    {
        IsEnabled = Source.IsSupported && (profile != SearchProfile.CraftingBase || Source.Kind == ModifierKind.Implicit);
        var range = profile == SearchProfile.Broad
            ? SearchProfileRules.Broad(new NumericRange(Source.Min, Source.Max))
            : new NumericRange(Source.Min, Source.Max);
        MinText = range.Min?.ToString(CultureInfo.InvariantCulture) ?? "";
        MaxText = range.Max?.ToString(CultureInfo.InvariantCulture) ?? "";
    }

    private void Step(string property, int direction)
    {
        string text = property == nameof(MinText) ? MinText : MaxText;
        decimal value = decimal.TryParse(text, NumberStyles.Number | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture, out var parsed) ? parsed : 0m;
        string next = (value + direction).ToString(CultureInfo.InvariantCulture);
        if (property == nameof(MinText)) MinText = next; else MaxText = next;
    }

    private static bool TryValue(string text, out decimal? value)
    {
        if (string.IsNullOrWhiteSpace(text)) { value = null; return true; }
        if (decimal.TryParse(text, NumberStyles.Number | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out var parsed))
        {
            value = parsed;
            return true;
        }
        value = null;
        return false;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsValid)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ValidationMessage)));
    }
}

internal sealed class DelegateCommand(Action execute) : ICommand
{
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => execute();
    public event EventHandler? CanExecuteChanged { add { } remove { } }
}

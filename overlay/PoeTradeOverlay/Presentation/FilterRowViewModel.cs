using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using PoeTradeOverlay.Models;

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
    }

    public TradeFilter Source { get; }
    public string Text => Source.SourceText;
    public bool CanEnable => Source.IsSupported;
    public string SupportLabel => Source.IsSupported ? "Matched" : "Unsupported — not sent";

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

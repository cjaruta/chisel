using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Chisel.Core.Converter;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chisel.App.ViewModels;

// One line of the "other units" list: the typed value in another unit.
public sealed record CommonConversion(Unit Unit, string Text);

// The unit converter widget. Typing converts at once; nothing else runs (no timers). The category and units are saved when they
// change (not on each keystroke), and each category keeps its last units for the session.
public partial class ConverterViewModel : ViewModelBase
{
    private readonly IConverterSettingsRepository? _repository;
    private readonly CultureInfo _culture;
    private readonly Dictionary<string, (string From, string To)> _lastUnits = new();
    private UnitCategory _category;
    private Unit _from;
    private Unit _to;
    private string _input = "1";

    [ObservableProperty]
    public partial string ResultText { get; private set; } = "";

    [ObservableProperty]
    public partial bool IsError { get; private set; }

    // The result as a plain number (the culture's decimal separator, no grouping), or null when there is none.
    [ObservableProperty]
    public partial string? CopyText { get; private set; }

    [ObservableProperty]
    public partial IReadOnlyList<CommonConversion> Common { get; private set; } = [];

    public IReadOnlyList<UnitCategory> Categories => UnitCatalog.All;

    public UnitCategory Category => _category;

    public Unit From
    {
        get => _from;
        set
        {
            if (value is not null && _category.Units.Contains(value) && SetProperty(ref _from, value))
            {
                Changed();
            }
        }
    }

    public Unit To
    {
        get => _to;
        set
        {
            if (value is not null && _category.Units.Contains(value) && SetProperty(ref _to, value))
            {
                Changed();
            }
        }
    }

    public string Input
    {
        get => _input;
        set
        {
            if (SetProperty(ref _input, value ?? ""))
            {
                Convert();
            }
        }
    }

    public ConverterViewModel(IConverterSettingsRepository? repository, CultureInfo? culture = null)
    {
        _repository = repository;
        _culture = culture ?? CultureInfo.CurrentCulture;
        var saved = (repository?.Get() ?? ConverterSettings.Default).Valid();
        _category = UnitCatalog.Get(saved.Category)!;
        _from = _category.Unit(saved.From)!;
        _to = _category.Unit(saved.To)!;
        Convert();
    }

    public void SelectCategory(string id)
    {
        if (UnitCatalog.Get(id) is not { } category || category == _category)
        {
            return;
        }
        _lastUnits[_category.Id] = (_from.Id, _to.Id);
        var (from, to) = _lastUnits.TryGetValue(category.Id, out var last) ? last : (category.DefaultFrom, category.DefaultTo);
        _category = category;
        _from = category.Unit(from)!;
        _to = category.Unit(to)!;
        OnPropertyChanged(nameof(Category));
        OnPropertyChanged(nameof(From));
        OnPropertyChanged(nameof(To));
        Changed();
    }

    [RelayCommand]
    private void Swap()
    {
        (_from, _to) = (_to, _from);
        OnPropertyChanged(nameof(From));
        OnPropertyChanged(nameof(To));
        Changed();
    }

    private void Changed()
    {
        _repository?.Save(new ConverterSettings(_category.Id, _from.Id, _to.Id));
        Convert();
    }

    private void Convert()
    {
        var parsed = ConverterNumber.Parse(_input, _culture);
        var result = parsed.Value is { } value ? UnitConverter.Convert(value, _from, _to, _category) : parsed;
        IsError = result.Error is not null;
        ResultText = result.Value is { } v ? ConverterNumber.Format(v, _culture) : result.Error ?? "";
        CopyText = result.Value is { } c ? ConverterNumber.Plain(c, _culture) : null;
        Common = result.Value is not null && parsed.Value is { } typed ? OtherUnits(typed) : [];
    }

    // The typed value in the category's common units, leaving out the two already shown.
    private List<CommonConversion> OtherUnits(decimal value)
    {
        var list = new List<CommonConversion>();
        foreach (var id in _category.Common)
        {
            var unit = _category.Unit(id)!;
            if (unit == _from || unit == _to || list.Count == ConverterLayout.MaxCommonRows)
            {
                continue;
            }
            if (UnitConverter.Convert(value, _from, unit, _category).Value is { } converted)
            {
                list.Add(new CommonConversion(unit, ConverterNumber.Format(converted, _culture)));
            }
        }
        return list;
    }
}

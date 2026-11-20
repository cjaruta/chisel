using System.ComponentModel;
using System.Globalization;
using Chisel.Core.Calculator;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chisel.App.ViewModels;

// Maps typed characters and the special keys to calculator keys.
public static class CalculatorKeys
{
    public const CalcKey Enter = CalcKey.Equals;
    public const CalcKey Back = CalcKey.Backspace;
    public const CalcKey Escape = CalcKey.Clear;

    public static CalcKey? FromChar(char c) => c switch
    {
        >= '0' and <= '9' => CalcKey.Digit0 + (c - '0'),
        '.' or ',' => CalcKey.Decimal,
        '+' => CalcKey.Add,
        '-' => CalcKey.Subtract,
        '*' or 'x' or 'X' => CalcKey.Multiply,
        '/' => CalcKey.Divide,
        '=' => CalcKey.Equals,
        '%' => CalcKey.Percent,
        '(' => CalcKey.OpenParen,
        ')' => CalcKey.CloseParen,
        '^' => CalcKey.Power,
        '!' => CalcKey.Factorial,
        _ => null,
    };
}

// The calculator widget: the engine plus what the view needs to draw it. The widget reports its size (Resize); from it the view model
// works out the layout and whether the advanced panel fits. The advanced setting only asks for the panel: a widget too narrow for it
// shows the basic calculator (keeping the number), and the panel comes back when the widget is wide again.
public partial class CalculatorViewModel : ViewModelBase
{
    private readonly CalculatorEngine _engine;
    private readonly CalculatorSettingsViewModel _settings;
    private double _width = 260;
    private double _height = 420;

    [ObservableProperty]
    public partial string Display { get; private set; } = "0";

    [ObservableProperty]
    public partial string Secondary { get; private set; } = "";

    [ObservableProperty]
    public partial bool IsError { get; private set; }

    [ObservableProperty]
    public partial bool HasMemory { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AngleLabel))]
    public partial AngleMode Angle { get; private set; }

    [ObservableProperty]
    public partial bool ShowAdvanced { get; private set; }

    [ObservableProperty]
    public partial CalculatorLayout Layout { get; private set; }

    // The decimal key's label: the culture's separator.
    public string DecimalSeparator { get; }

    public string AngleLabel => Angle == AngleMode.Degrees ? "DEG" : "RAD";

    // The number to copy with Ctrl+C.
    public string Result => _engine.Result;

    public CalculatorViewModel(CalculatorSettingsViewModel settings, CultureInfo? culture = null)
    {
        _settings = settings;
        _engine = new CalculatorEngine(culture) { Angle = settings.Angle };
        DecimalSeparator = (culture ?? CultureInfo.CurrentCulture).NumberFormat.NumberDecimalSeparator;
        Layout = CalculatorLayout.For(_width, _height, settings.Advanced);
        settings.PropertyChanged += OnSettingsChanged;
        Refresh();
    }

    [RelayCommand]
    private void Press(CalcKey key)
    {
        _engine.Press(key);
        Refresh();
    }

    // A typed character; false when it is not a calculator key.
    public bool PressChar(char c)
    {
        if (CalculatorKeys.FromChar(c) is not { } key)
        {
            return false;
        }
        Press(key);
        return true;
    }

    // The widget's room, in pixels (inside its padding).
    public void Resize(double width, double height)
    {
        if (width <= 0 || height <= 0)
        {
            return;
        }
        _width = width;
        _height = height;
        ApplyLayout();
    }

    private void ApplyLayout()
    {
        Layout = CalculatorLayout.For(_width, _height, _settings.Advanced);
        _engine.Advanced = Layout.ShowAdvanced;
        Refresh();
    }

    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CalculatorSettingsViewModel.Advanced))
        {
            ApplyLayout();
        }
        else if (e.PropertyName == nameof(CalculatorSettingsViewModel.Angle))
        {
            _engine.Angle = _settings.Angle;
            Refresh();
        }
    }

    private void Refresh()
    {
        Display = _engine.Display;
        Secondary = _engine.Secondary;
        IsError = _engine.IsError;
        HasMemory = _engine.HasMemory;
        Angle = _engine.Angle;
        ShowAdvanced = _engine.Advanced;
        OnPropertyChanged(nameof(Result));
    }
}

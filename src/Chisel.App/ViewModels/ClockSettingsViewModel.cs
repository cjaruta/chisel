using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Chisel.Core.Clock;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Chisel.App.ViewModels;

// The Clock page in the Widgets settings: a preset picker plus every ClockStyle option. Each change is applied to the style and
// saved at once; the option controls are then re-synced from the style, so choosing a preset moves them and editing one option
// makes the look "custom" (no preset selected). Index properties are positions in the matching Names list.
public partial class ClockSettingsViewModel : ViewModelBase
{
    private readonly IClockStyleRepository? _repository;
    private bool _syncing;

    public static IReadOnlyList<string> PresetNames { get; } = ClockPresets.All.Select(p => p.Title).ToList();
    public static IReadOnlyList<string> PaletteNames { get; } = ClockPalettes.All.Select(p => p.Title).ToList();
    public static IReadOnlyList<string> SwatchNames { get; } = ClockSwatches.All.Select(s => s.Title).ToList();
    public static IReadOnlyList<string> AlignmentNames { get; } = ["Left", "Center", "Right"];
    public static IReadOnlyList<string> ShapeNames { get; } = ["Round", "Square", "Rounded square"];
    public static IReadOnlyList<string> MarkerNames { get; } = ["None", "Ticks", "Dots", "Bars"];
    public static IReadOnlyList<string> NumeralNames { get; } = ["None", "12 only", "12 / 3 / 6 / 9", "All 12", "Roman"];
    public static IReadOnlyList<string> HandNames { get; } = ["Line", "Tapered", "Baton", "Leaf"];

    public double MinThickness => ClockStyle.MinThickness;
    public double MaxThickness => ClockStyle.MaxThickness;
    public double MaxBorder => ClockStyle.MaxBorder;

    [ObservableProperty]
    public partial ClockStyle Style { get; private set; } = ClockStyle.Default;

    [ObservableProperty]
    public partial int PresetIndex { get; set; } = -1;

    [ObservableProperty]
    public partial int PaletteIndex { get; set; } = -1;

    [ObservableProperty]
    public partial int AlignmentIndex { get; set; }

    [ObservableProperty]
    public partial int ShapeIndex { get; set; }

    [ObservableProperty]
    public partial double BorderWidth { get; set; }

    [ObservableProperty]
    public partial int MarkerIndex { get; set; }

    [ObservableProperty]
    public partial bool EmphasizeQuarters { get; set; }

    [ObservableProperty]
    public partial int NumeralIndex { get; set; }

    [ObservableProperty]
    public partial int HandIndex { get; set; }

    [ObservableProperty]
    public partial double HourThickness { get; set; }

    [ObservableProperty]
    public partial double MinuteThickness { get; set; }

    [ObservableProperty]
    public partial double SecondThickness { get; set; }

    [ObservableProperty]
    public partial bool ShowSeconds { get; set; }

    [ObservableProperty]
    public partial int DialColorIndex { get; set; }

    [ObservableProperty]
    public partial int MarkerColorIndex { get; set; }

    [ObservableProperty]
    public partial int NumeralColorIndex { get; set; }

    [ObservableProperty]
    public partial int HourColorIndex { get; set; }

    [ObservableProperty]
    public partial int MinuteColorIndex { get; set; }

    [ObservableProperty]
    public partial int SecondColorIndex { get; set; }

    public ClockSettingsViewModel(IClockStyleRepository? repository = null)
    {
        _repository = repository;
        Style = (repository?.Get() ?? ClockStyle.Default).Normalized();
        Sync();
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (_syncing || e.PropertyName is null)
        {
            return;
        }
        Func<ClockStyle, ClockStyle>? change = e.PropertyName switch
        {
            nameof(PresetIndex) when PresetIndex >= 0 => s => ClockPresets.Apply(ClockPresets.All[PresetIndex].Id, s),
            nameof(PaletteIndex) when PaletteIndex >= 0 => s => ClockPalettes.Apply(ClockPalettes.All[PaletteIndex].Id, s),
            nameof(AlignmentIndex) => s => s with { Alignment = (ClockAlignment)AlignmentIndex },
            nameof(ShapeIndex) => s => s with { Shape = (DialShape)ShapeIndex },
            nameof(BorderWidth) => s => s with { BorderWidth = BorderWidth },
            nameof(MarkerIndex) => s => s with { Markers = (MarkerStyle)MarkerIndex },
            nameof(EmphasizeQuarters) => s => s with { EmphasizeQuarters = EmphasizeQuarters },
            nameof(NumeralIndex) => s => s with { Numerals = (NumeralStyle)NumeralIndex },
            nameof(HandIndex) => s => s with { Hands = (HandStyle)HandIndex },
            nameof(HourThickness) => s => s with { HourThickness = HourThickness },
            nameof(MinuteThickness) => s => s with { MinuteThickness = MinuteThickness },
            nameof(SecondThickness) => s => s with { SecondThickness = SecondThickness },
            nameof(ShowSeconds) => s => s with { ShowSeconds = ShowSeconds },
            nameof(DialColorIndex) when DialColorIndex >= 0 => s => Recolor(s, c => c with { Dial = Hex(DialColorIndex) }),
            nameof(MarkerColorIndex) when MarkerColorIndex >= 0 => s => Recolor(s, c => c with { Markers = Hex(MarkerColorIndex) }),
            nameof(NumeralColorIndex) when NumeralColorIndex >= 0 => s => Recolor(s, c => c with { Numerals = Hex(NumeralColorIndex) }),
            nameof(HourColorIndex) when HourColorIndex >= 0 => s => Recolor(s, c => c with { Hour = Hex(HourColorIndex) }),
            nameof(MinuteColorIndex) when MinuteColorIndex >= 0 => s => Recolor(s, c => c with { Minute = Hex(MinuteColorIndex) }),
            nameof(SecondColorIndex) when SecondColorIndex >= 0 => s => Recolor(s, c => c with { Second = Hex(SecondColorIndex) }),
            _ => null,
        };
        if (change is not null)
        {
            Style = change(Style).Normalized();
            _repository?.Save(Style);
            Sync();
        }
    }

    private static string? Hex(int swatch) => ClockSwatches.All[swatch].Hex;

    private static ClockStyle Recolor(ClockStyle style, Func<ClockColors, ClockColors> change) => style with { Colors = change(style.Colors) };

    // Points every option control at the current style.
    private void Sync()
    {
        _syncing = true;
        var s = Style;
        PresetIndex = ClockPresets.All.ToList().FindIndex(p => p.Id == ClockPresets.IdOf(s));
        var shades = s.Colors;
        PaletteIndex = ClockPalettes.All.ToList().FindIndex(p => p.Colors == shades);
        AlignmentIndex = (int)s.Alignment;
        ShapeIndex = (int)s.Shape;
        BorderWidth = s.BorderWidth;
        MarkerIndex = (int)s.Markers;
        EmphasizeQuarters = s.EmphasizeQuarters;
        NumeralIndex = (int)s.Numerals;
        HandIndex = (int)s.Hands;
        HourThickness = s.HourThickness;
        MinuteThickness = s.MinuteThickness;
        SecondThickness = s.SecondThickness;
        ShowSeconds = s.ShowSeconds;
        DialColorIndex = ClockSwatches.IndexOf(shades.Dial);
        MarkerColorIndex = ClockSwatches.IndexOf(shades.Markers);
        NumeralColorIndex = ClockSwatches.IndexOf(shades.Numerals);
        HourColorIndex = ClockSwatches.IndexOf(shades.Hour);
        MinuteColorIndex = ClockSwatches.IndexOf(shades.Minute);
        SecondColorIndex = ClockSwatches.IndexOf(shades.Second);
        _syncing = false;
    }
}

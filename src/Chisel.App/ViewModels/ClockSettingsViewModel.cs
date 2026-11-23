using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Chisel.Core.Clock;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chisel.App.ViewModels;

// The Clock page in the Widgets settings: a preset picker plus every ClockStyle option. Each change is applied to the style and
// saved at once; the option controls are then re-synced from the style, so choosing a preset moves them and editing one option
// makes the look "custom" (no preset selected). Index properties are positions in the matching Names list.
public enum ClockSettingsTab { Layout, Face, Tools, Behavior }

public partial class ClockSettingsViewModel : ViewModelBase
{
    private readonly IClockStyleRepository? _repository;
    private bool _syncing;

    public static IReadOnlyList<string> PresetNames { get; } = ClockPresets.All.Select(p => p.Title).ToList();
    public static IReadOnlyList<string> PaletteNames { get; } = ClockPalettes.All.Select(p => p.Title).ToList();
    public static IReadOnlyList<string> SwatchNames { get; } = ClockSwatches.All.Select(s => s.Title).ToList();
    public static IReadOnlyList<string> AlignmentNames { get; } = ["Left", "Center", "Right"];
    public static IReadOnlyList<string> ShapeNames { get; } = ["Round", "Square", "Rounded square"];
    public static IReadOnlyList<string> MarkerNames { get; } = ["None", "Ticks", "Dots", "Bars", "Luminous"];
    public static IReadOnlyList<string> NumeralNames { get; } = ["None", "12 only", "12 / 3 / 6 / 9", "All 12", "Roman"];
    public static IReadOnlyList<string> HandNames { get; } = ["Line", "Tapered", "Baton", "Leaf", "Sword"];
    public static IReadOnlyList<string> KindNames { get; } = ["Standard", "24-hour", "Chronograph"];
    public static IReadOnlyList<string> StopwatchNames { get; } = ["Digital", "Analog"];

    public double MinThickness => ClockStyle.MinThickness;
    public double MaxThickness => ClockStyle.MaxThickness;
    public double MaxBorder => ClockStyle.MaxBorder;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewStyle))]
    public partial ClockStyle Style { get; private set; } = ClockStyle.Default;

    // The live preview at the top of the page: the same look, always centered (alignment is about the widget, not the preview).
    public ClockStyle PreviewStyle => Style with { Alignment = ClockAlignment.Center };

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ResetToPresetCommand))]
    public partial int PresetIndex { get; set; } = -1;

    [ObservableProperty]
    public partial int PaletteIndex { get; set; } = -1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAlignmentLeft))]
    [NotifyPropertyChangedFor(nameof(IsAlignmentCenter))]
    [NotifyPropertyChangedFor(nameof(IsAlignmentRight))]
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

    // The dial: standard 12-hour, 24-hour, or a chronograph with sub-dials; a minute track; a tachymeter ring.
    [ObservableProperty]
    public partial int KindIndex { get; set; }

    [ObservableProperty]
    public partial bool MinuteTrack { get; set; }

    [ObservableProperty]
    public partial bool Tachymeter { get; set; }

    // How the stopwatch tool shows the time: digits or an analog dial.
    [ObservableProperty]
    public partial int StopwatchIndex { get; set; }

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

    // The tabs of the Clock page: Layout, Face, Tools and Behavior. The page opens on Layout.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLayoutTab))]
    [NotifyPropertyChangedFor(nameof(IsFaceTab))]
    [NotifyPropertyChangedFor(nameof(IsToolsTab))]
    [NotifyPropertyChangedFor(nameof(IsBehaviorTab))]
    public partial ClockSettingsTab Tab { get; set; }

    public bool IsLayoutTab => Tab == ClockSettingsTab.Layout;
    public bool IsFaceTab => Tab == ClockSettingsTab.Face;
    public bool IsToolsTab => Tab == ClockSettingsTab.Tools;
    public bool IsBehaviorTab => Tab == ClockSettingsTab.Behavior;

    public bool IsAlignmentLeft => AlignmentIndex == (int)ClockAlignment.Left;
    public bool IsAlignmentCenter => AlignmentIndex == (int)ClockAlignment.Center;
    public bool IsAlignmentRight => AlignmentIndex == (int)ClockAlignment.Right;

    [RelayCommand]
    private void SelectTab(ClockSettingsTab tab) => Tab = tab;

    [RelayCommand]
    private void SelectAlignment(ClockAlignment alignment) => AlignmentIndex = (int)alignment;

    // The preset last chosen (or the one the saved look matches); Reset to preset goes back to its look.
    private string _basePreset = ClockPresets.All[0].Id;

    private bool CanReset() => PresetIndex < 0;

    // Puts the chosen preset's look back after the options were changed one by one. Alignment, seconds and colors are kept.
    [RelayCommand(CanExecute = nameof(CanReset))]
    private void ResetToPreset() => Commit(s => ClockPresets.Apply(_basePreset, s));

    // Wears a whole look at once (a saved face): saved and shown like any other change.
    public void ApplyStyle(ClockStyle style) => Commit(_ => style);

    private void Commit(Func<ClockStyle, ClockStyle> change)
    {
        Style = change(Style).Normalized();
        _repository?.Save(Style);
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
            nameof(KindIndex) => s => s with { Kind = (DialKind)KindIndex },
            nameof(MinuteTrack) => s => s with { MinuteTrack = MinuteTrack },
            nameof(Tachymeter) => s => s with { Tachymeter = Tachymeter },
            nameof(StopwatchIndex) => s => s with { Stopwatch = (StopwatchDisplay)StopwatchIndex },
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
            Commit(change);
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
        if (PresetIndex >= 0)
        {
            _basePreset = ClockPresets.All[PresetIndex].Id;
        }
        var shades = s.Colors;
        PaletteIndex = ClockPalettes.All.ToList().FindIndex(p => p.Colors == shades);
        AlignmentIndex = (int)s.Alignment;
        ShapeIndex = (int)s.Shape;
        BorderWidth = s.BorderWidth;
        MarkerIndex = (int)s.Markers;
        EmphasizeQuarters = s.EmphasizeQuarters;
        NumeralIndex = (int)s.Numerals;
        HandIndex = (int)s.Hands;
        KindIndex = (int)s.Kind;
        MinuteTrack = s.MinuteTrack;
        Tachymeter = s.Tachymeter;
        StopwatchIndex = (int)s.Stopwatch;
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

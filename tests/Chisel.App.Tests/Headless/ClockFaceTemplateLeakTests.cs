using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chisel.App.ViewModels;
using Chisel.App.Views;
using Chisel.App.Views.Widgets;
using Chisel.Core.Clock;

namespace Chisel.App.Tests.Headless;

// Clicking through the watch faces: each one is drawn and previewed as itself, with nothing from the face before it.
public class ClockFaceTemplateLeakTests
{
    private static readonly string[] Ids = ClockPresets.All.Select(p => p.Id).ToArray();

    private static byte[] Draw(ClockFace face)
    {
        face.Measure(new Size(200, 200));
        face.Arrange(new Rect(0, 0, 200, 200));
        var bitmap = new RenderTargetBitmap(new PixelSize(200, 200));
        using (var context = bitmap.CreateDrawingContext())
        {
            face.Draw(context);
        }
        using var stream = new MemoryStream();
        bitmap.Save(stream);
        return stream.ToArray();
    }

    private static ClockFace Fresh(string id) =>
        new() { Style = ClockPresets.Apply(id, ClockStyle.Default), HourAngle = 305, MinuteAngle = 60, SecondAngle = 150, Hour24Angle = 152.5 };

    [AvaloniaFact]
    public void OneFaceControl_ShownAsEveryFaceInTurn_LooksExactlyLikeAFreshOne()
    {
        var expected = Ids.ToDictionary(id => id, id => Draw(Fresh(id)));
        var reused = Fresh(Ids[0]);
        Draw(reused);

        // Every order of switching on one long-lived control, as the widget does when the style changes.
        var orders = new List<string[]>
        {
            Ids, Ids.Reverse().ToArray(),
            Ids.SelectMany(a => new[] { a, "pilot", a }).ToArray(), // A -> glow -> A
            Ids.SelectMany(a => new[] { a, "classic", a }).ToArray(), // A -> white -> A
        };
        var random = new Random(9);
        orders.Add(Enumerable.Range(0, 60).Select(_ => Ids[random.Next(Ids.Length)]).ToArray());
        foreach (var order in orders)
        {
            foreach (var id in order)
            {
                reused.Style = ClockPresets.Apply(id, reused.Style);
                Assert.True(expected[id].SequenceEqual(Draw(reused)), $"{id} did not look like itself after the faces before it");
            }
        }
    }

    [AvaloniaFact]
    public void TheFaces_AreDrawnTheSameWhateverWasDrawnBefore()
    {
        var first = Ids.ToDictionary(id => id, id => Draw(Fresh(id)));
        foreach (var id in Ids.Reverse().Concat(Ids))
        {
            Assert.True(first[id].SequenceEqual(Draw(Fresh(id))), $"{id} changed after other faces were drawn (a shared brush or cache)");
        }
    }

    // The Face tab.

    private static (MainWindow Window, MainViewModel Main) OpenFaceTab()
    {
        var window = UiSnapshot.ShowMainWindow(1200, 760);
        var main = (MainViewModel)window.DataContext!;
        main.Settings.IsOpen = true;
        main.Settings.SelectTabCommand.Execute(SettingsTab.Widgets);
        main.Settings.SelectedWidgetPage = main.Settings.WidgetPages.Single(p => p.Id == "clock");
        main.ClockSettings.SelectTabCommand.Execute(ClockSettingsTab.Face);
        Dispatcher.UIThread.RunJobs();
        return (window, main);
    }

    private static byte[] Preview(MainWindow w, string id)
    {
        var image = w.GetVisualDescendants().OfType<Image>().Single(i => i.Name == "ClockPresetThumb_" + id);
        var bitmap = (Bitmap)image.Source!;
        using var stream = new MemoryStream();
        bitmap.Save(stream);
        return stream.ToArray();
    }

    private static void Click(MainWindow w, string name)
    {
        w.GetVisualDescendants().OfType<Button>().Single(b => b.Name == name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void ThePickersPreviews_ShowEachFaceAsDefined_NotWithTheColorsChosenNow()
    {
        var (window, main) = OpenFaceTab();
        var before = Ids.ToDictionary(id => id, id => Preview(window, id));

        main.ClockSettings.PaletteIndex = ClockPalettes.All.ToList().FindIndex(p => p.Id == "ocean");
        Click(window, "ClockPreset_pilot");
        Click(window, "ClockPreset_classic");
        Click(window, "ClockPreset_slim");
        Dispatcher.UIThread.RunJobs();

        foreach (var id in Ids)
        {
            Assert.True(before[id].SequenceEqual(Preview(window, id)), $"the preview of {id} changed with the colors in use");
        }
        window.Close();
    }

    [AvaloniaFact]
    public void ClickingThroughTheFaces_SettlesOnEachOneAsItsDefinition_AndTheFirstIsDefaultAgain()
    {
        var (window, main) = OpenFaceTab();
        main.ClockSettings.PaletteIndex = ClockPalettes.All.ToList().FindIndex(p => p.Id == "amber");
        main.ClockSettings.HourThickness = 9; // customizing

        foreach (var id in Ids.Concat(new[] { Ids[0], "pilot", Ids[0] }))
        {
            Click(window, "ClockPreset_" + id);
            var template = ClockPresets.All.Single(p => p.Id == id).Style;
            Assert.Equal(template.WithoutPreferences(), main.ClockSettings.Style.WithoutPreferences());
            Assert.Equal(ClockPresets.All.ToList().FindIndex(p => p.Id == id), main.ClockSettings.PresetIndex);
        }
        window.Close();
    }
}

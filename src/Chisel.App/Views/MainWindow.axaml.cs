using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Chisel.App.Services;
using Avalonia.VisualTree;
using Avalonia.Threading;
using Chisel.App.Startup;
using Chisel.App.ViewModels;
using Chisel.Core.Clock;
using Chisel.Core.Media;
using Chisel.App.Views.Widgets;

namespace Chisel.App.Views;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private readonly string? _windowPlacementPath;
    private readonly WindowPlacementTracker _placement = new();
    private readonly DebouncedSaver<WindowPlacement>? _placementSaver;
    private bool _spaceDown;

    // Choosing a link in the "more" menu opens it (its own command) and closes the menu.
    private void OnMoreLinkClick(object? sender, RoutedEventArgs e) => MoreLinksButton.Flyout?.Hide();

    public MainWindow() : this(AppPaths.GetWindowPlacementPath())
    {
    }

    // windowPlacementPath: where size/position are remembered; null turns that off (used by tests).
    public MainWindow(string? windowPlacementPath)
    {
        _windowPlacementPath = windowPlacementPath;
        InitializeComponent();
        if (windowPlacementPath is not null)
        {
            _placementSaver = new DebouncedSaver<WindowPlacement>(TimeProvider.System, TimeSpan.FromMilliseconds(500), SavePlacement);
            ApplySavedPlacement();
            PositionChanged += (_, _) => TrackPlacement();
        }

        // Keep the layout toolbar centered, but clear of the clock and date on narrow windows.
        LayoutToolbar.SizeChanged += (_, _) => PlaceLayoutToolbar();
        ClockPanel.SizeChanged += (_, _) => PlaceLayoutToolbar();
        SizeChanged += (_, _) => { PlaceLayoutToolbar(); PlaceLibrary(); TrackPlacement(); };
        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainViewModel main)
            {
                main.Library.PropertyChanged += (_, _) => PlaceLibrary();
            }
        };

        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, OnPreviewKeyUp, RoutingStrategies.Tunnel, handledEventsToo: true);
        Deactivated += (_, _) => _spaceDown = false;

        DataContextChanged += (_, _) => (DataContext as MainViewModel)?.Settings.Theme?.Track(this);

        // The show/hide shortcut box: while focused, the next shortcut pressed becomes the new one.
        HotkeyBox.GotFocus += (_, _) => (DataContext as MainViewModel)?.Settings.SystemOptions?.BeginHotkeyCapture();
        HotkeyBox.LostFocus += (_, _) => (DataContext as MainViewModel)?.Settings.SystemOptions?.EndHotkeyCapture();
        HotkeyBox.AddHandler(KeyDownEvent, OnHotkeyBoxKeyDown, RoutingStrategies.Tunnel);

        // Pictures dragged in from Explorer or a browser: accepted in edit mode only.
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, OnPictureDragOver);
        AddHandler(DragDrop.DragLeaveEvent, (_, _) => LayoutHost.ExternalDragLeave());
        AddHandler(DragDrop.DropEvent, OnPictureDrop);
        PropertyChanged += (_, e) =>
        {
            // Picture frames stop their timers and animations while the window is minimized.
            if (e.Property == WindowStateProperty)
            {
                TrackPlacement();
            }
            if (e.Property == WindowStateProperty && DataContext is MainViewModel main)
            {
                main.PictureFrames.WindowMinimized = WindowState == WindowState.Minimized;
            }
            // Back on screen: tick at the visible rate straight away rather than after the next slow tick.
            if (e.Property == WindowStateProperty || e.Property == IsVisibleProperty)
            {
                UpdateTickInterval();
            }
        };

        _timer.Tick += (_, _) =>
        {
            if (DataContext is not MainViewModel main)
            {
                return;
            }
            main.Tick();
            UpdateTickInterval();
        };
        Loaded += (_, _) =>
        {
            _timer.Start();
            // Wait for layout so the grid has its size before scrolling to the current time.
            Dispatcher.UIThread.Post(ScrollToNow, DispatcherPriority.Background);
        };
        Unloaded += (_, _) => _timer.Stop();

        // Minimized or in the tray the dashboard ticks once a second; visible it ticks gently, or fast for a running stopwatch.
        void UpdateTickInterval()
        {
            var interval = MainTick.Interval(windowHidden: !IsVisible || WindowState == WindowState.Minimized,
                stopwatchRunning: DataContext is MainViewModel { ClockTools.StopwatchRunning: true });
            if (_timer.Interval != interval)
            {
                _timer.Interval = interval;
            }
        }
        Closing += (_, _) =>
        {
            (DataContext as MainViewModel)?.Notes.Flush();
            TrackPlacement();
            _placementSaver?.Flush();
        };
    }

    private void OnPictureDragOver(object? sender, DragEventArgs e)
    {
        if (DataContext is not MainViewModel { Layout.IsEditing: true } || !PictureDropPayload.HasPictures(e.DataTransfer))
        {
            LayoutHost.ExternalDragLeave();
            e.DragEffects = DragDropEffects.None;
            return;
        }
        e.DragEffects = LayoutHost.ExternalDragOver(e.GetPosition(LayoutHost)) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnPictureDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not MainViewModel { Layout.IsEditing: true } main)
        {
            LayoutHost.ExternalDragLeave();
            e.DragEffects = DragDropEffects.None;
            return;
        }
        var sources = PictureDropPayload.Read(e.DataTransfer, main.PictureFrames.Log);
        if (sources.Count == 0)
        {
            LayoutHost.ExternalDragLeave();
            return;
        }
        var placement = LayoutHost.ExternalDrop(e.GetPosition(LayoutHost), main.PictureFrames.FramesFor(sources.Count));
        e.Handled = true;
        e.DragEffects = placement is null ? DragDropEffects.None : DragDropEffects.Copy;
        if (placement is not null)
        {
            _ = main.PictureFrames.DropSafelyAsync(placement, sources);
        }
    }

    // Space plays or pauses the current media session, from the tunnel phase so it is decided in one place before any
    // control sees the key. MediaShortcuts holds the rules; this only gathers the facts. Holding the key does not repeat.
    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Space || e.KeyModifiers != KeyModifiers.None || DataContext is not MainViewModel main)
        {
            return;
        }
        var repeat = _spaceDown;
        _spaceDown = true;
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        var context = new SpaceKeyContext(
            IsRepeat: repeat,
            OverlayOpen: main.Settings.IsOpen || main.Schedule.BlockEditor.IsOpen || main.Schedule.MarkerEditor.IsOpen
                || (focused is Visual popupFocus && TopLevel.GetTopLevel(popupFocus) != this),
            FocusUsesSpace: focused is Visual visual && visual.GetSelfAndVisualAncestors().Any(UsesSpace),
            IsEditing: main.Layout.IsEditing,
            CanPlayPause: main.Media.CanToggleFromShortcut);
        if (MediaShortcuts.ShouldTogglePlayPause(context))
        {
            e.Handled = true;
            _ = main.Media.ToggleFromShortcut();
        }
    }

    // Only reads the keys pressed while the shortcut box has focus. Tab still moves on; Esc gives up.
    private void OnHotkeyBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel { Settings.SystemOptions: { } system } || (e.Key == Key.Tab && e.KeyModifiers == KeyModifiers.None))
        {
            return;
        }
        e.Handled = true;
        if (e.Key == Key.Escape)
        {
            LeaveHotkeyBox();
            return;
        }
        if (!HotkeyKeys.IsModifier(e.Key) && system.CaptureHotkey(HotkeyKeys.Modifiers(e.KeyModifiers), HotkeyKeys.NameOf(e.Key)))
        {
            LeaveHotkeyBox();
        }
    }

    // Moves focus on from the shortcut box, which ends the capture.
    private void LeaveHotkeyBox()
    {
        if (!HotkeyOffButton.IsEffectivelyEnabled || !HotkeyOffButton.Focus())
        {
            GeneralTab.Focus();
        }
    }

    private void OnPreviewKeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space)
        {
            _spaceDown = false;
        }
    }

    // Text inputs and controls that react to Space themselves keep it.
    private static bool UsesSpace(Visual visual) =>
        visual is TextBox or AutoCompleteBox or Button or ToggleButton or ComboBox or Slider or ListBox or ListBoxItem
            or TabItem or MenuItem or TreeViewItem or NumericUpDown or Widgets.VolumeSlider;

    // Below this window width the library is a centered overlay with a dim layer; at or above it, a side panel.
    private const double WideWindow = 1000;

    private void PlaceLibrary()
    {
        var narrow = Bounds.Width < WideWindow;
        LibraryScrim.IsVisible = narrow && DataContext is MainViewModel { Library.IsOpen: true };
        LibraryPanel.HorizontalAlignment = narrow ? Avalonia.Layout.HorizontalAlignment.Center : Avalonia.Layout.HorizontalAlignment.Right;
        LibraryPanel.Width = narrow ? Math.Clamp(Bounds.Width - 80, 240, 480) : 340;
        LibraryPanel.Margin = narrow ? new Thickness(0, 56, 0, 20) : new Thickness(0, 56, 0, 0);
    }

    private void OnLibraryScrimPressed(object? sender, PointerPressedEventArgs e)
    {
        (DataContext as MainViewModel)?.Library.CloseCommand.Execute(null);
        e.Handled = true;
    }

    // Centered over the window, or just right of the clock and date when centering would cover them.
    private void PlaceLayoutToolbar()
    {
        if (LayoutToolbar.Parent is not Control area || LayoutToolbar.Bounds.Width <= 0)
        {
            return;
        }
        var centered = (area.Bounds.Width - LayoutToolbar.Bounds.Width) / 2;
        var left = Math.Max(centered, ClockPanel.Bounds.Right + 16);
        LayoutToolbar.Margin = new Thickness(left, LayoutToolbar.Margin.Top, 0, 0);
    }

    private ScheduleWidgetView? ScheduleView => this.GetVisualDescendants().OfType<ScheduleWidgetView>().FirstOrDefault();

    // Scrolls the schedule so the now-line sits about a third of the way down.
    public void ScrollToNow() => ScheduleView?.ScrollToNow();

    public static double ScheduleScrollOffset(double nowY, double viewportHeight, double extentHeight) =>
        ScheduleWidgetView.ScheduleScrollOffset(nowY, viewportHeight, extentHeight);

    // One step of schedule auto-scroll while dragging near its top or bottom edge.
    public void AutoScrollTick() => ScheduleView?.AutoScrollTick();

    // Puts the window where it was (same monitor, size and maximized state) if that is still on a connected screen, else a
    // default window centered on the primary screen. Runs before the window is shown, so it never flashes elsewhere first.
    private void ApplySavedPlacement()
    {
        if (_windowPlacementPath is null)
        {
            return;
        }

        var screens = Screens.All.Select(s => new ScreenArea(s.WorkingArea, s.Scaling, s.IsPrimary)).ToList();
        if (WindowPlacementPolicy.Restore(WindowPlacementStore.Load(_windowPlacementPath), screens) is not { } plan)
        {
            return;
        }

        WindowStartupLocation = WindowStartupLocation.Manual;
        Position = plan.Position;
        Width = plan.Width;
        Height = plan.Height;
        _placement.Observe(WindowState.Normal, plan.Width, plan.Height, plan.Position);
        if (plan.Maximized)
        {
            WindowState = WindowState.Maximized;
        }
    }

    // Moving, resizing or maximizing: saved half a second after it settles, and again when the window closes.
    private void TrackPlacement()
    {
        if (_placementSaver is null)
        {
            return;
        }
        _placement.Observe(WindowState, ClientSize.Width, ClientSize.Height, Position);
        if (_placement.Current is { } current)
        {
            _placementSaver.Note(current);
        }
    }

    private void SavePlacement(WindowPlacement placement)
    {
        try
        {
            WindowPlacementStore.Save(_windowPlacementPath!, placement);
        }
        catch (Exception)
        {
            // Remembering the window position is a convenience; never block closing over it.
        }
    }
}

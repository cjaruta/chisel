using System;
using System.Collections.Generic;
using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Chisel.App.ViewModels;
using Chisel.Core.SystemMonitor;

namespace Chisel.App.Views.Widgets;

// The system monitor widget: one ring gauge per stat, laid out by SystemMonitorLayout for the widget's size. It watches the shared
// sampler only while it is on screen (attached, effectively visible, window not minimized), so a hidden, minimized or removed
// widget costs nothing. Each sample goes to the gauges, and a gauge redraws only when something it shows has changed. A stat the
// system cannot report is left out; if nothing is left a short message says why.
public sealed class SystemMonitorWidgetView : WidgetView
{
    private readonly Canvas _canvas = new() { Name = "SystemMonitorCanvas" };
    private readonly TextBlock _empty = new()
    {
        Name = "SystemMonitorEmpty", Opacity = 0.6, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12), IsVisible = false,
    };
    private static readonly SystemStat[] AllStats = Enum.GetValues<SystemStat>();
    private readonly StatGauge[] _gauges;
    private readonly GaugePaint _paint = new();
    private SystemMonitorViewModel? _monitor;
    private IDisposable? _windowState;
    private bool _minimized;
    private bool _watching;
    private SystemStats _available = SystemStats.All; // until the first reading says otherwise

    public SystemMonitorWidgetView()
    {
        ClipToBounds = true;
        Margin = new Thickness(0);
        VerticalAlignment = VerticalAlignment.Stretch;
        AutomationProperties.SetName(this, "System monitor");
        _gauges = new StatGauge[AllStats.Length];
        foreach (var stat in AllStats)
        {
            var gauge = new StatGauge(stat, _paint) { IsVisible = false };
            _gauges[(int)stat] = gauge;
            _canvas.Children.Add(gauge);
        }
        var root = new Grid();
        root.Children.Add(_canvas);
        root.Children.Add(_empty);
        Content = root;
    }

    public SystemMonitorLayout Layout { get; private set; } = SystemMonitorLayout.For(0, 0, SystemStats.None, false);

    public IReadOnlyList<StatGauge> Gauges => _gauges;

    private SystemMonitorSampler? Sampler => _monitor?.Sampler;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _monitor = (DataContext as MainViewModel)?.SystemMonitor;
        if (_monitor is null)
        {
            return;
        }
        _monitor.Settings.PropertyChanged += OnSettingsChanged;
        if (TopLevel.GetTopLevel(this) is Window window)
        {
            _windowState = window.GetObservable(Window.WindowStateProperty).Subscribe(new StateObserver(this));
        }
        Relayout();
        Follow();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Watch(false);
        _windowState?.Dispose();
        _windowState = null;
        if (_monitor is not null)
        {
            _monitor.Settings.PropertyChanged -= OnSettingsChanged;
            _monitor = null;
        }
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsVisibleProperty || change.Property.Name == nameof(IsEffectivelyVisible))
        {
            Follow();
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        Relayout();
    }

    private sealed class StateObserver(SystemMonitorWidgetView view) : IObserver<WindowState>
    {
        public void OnCompleted() { }
        public void OnError(Exception error) { }
        public void OnNext(WindowState value)
        {
            view._minimized = value == WindowState.Minimized;
            view.Follow();
        }
    }

    private void Follow() => Watch(_monitor is not null && IsEffectivelyVisible && !_minimized);

    private void Watch(bool on)
    {
        if (on == _watching || Sampler is not { } sampler)
        {
            return;
        }
        _watching = on;
        if (on)
        {
            sampler.Sampled += OnSampled;
            sampler.Attach(this); // takes a reading at once if it was idle
            Show();
        }
        else
        {
            sampler.Sampled -= OnSampled;
            sampler.Detach(this);
        }
    }

    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SystemMonitorSettingsViewModel.Shown))
        {
            Relayout();
        }
    }

    private void OnSampled(object? sender, EventArgs e)
    {
        var available = Available(Sampler!.Current);
        if (available != _available)
        {
            _available = available;
            Relayout();
        }
        else
        {
            Show();
        }
    }

    private static SystemStats Available(SystemSnapshot snapshot)
    {
        var set = SystemStats.None;
        foreach (var stat in AllStats)
        {
            set = set.With(stat, snapshot.Has(stat));
        }
        return set;
    }

    private void Relayout()
    {
        if (_monitor is null)
        {
            return;
        }
        var chosen = _monitor.Settings.Shown;
        Layout = SystemMonitorLayout.For(Bounds.Width, Bounds.Height, chosen & _available, _available.Includes(SystemStat.Battery));
        foreach (var gauge in _gauges)
        {
            gauge.IsVisible = false;
        }
        for (var i = 0; i < Layout.Stats.Count; i++)
        {
            var gauge = _gauges[(int)Layout.Stats[i]];
            var cell = Layout.Cell(i);
            Canvas.SetLeft(gauge, cell.X);
            Canvas.SetTop(gauge, cell.Y);
            gauge.Width = cell.Width;
            gauge.Height = cell.Height;
            gauge.Configure(Layout);
            gauge.IsVisible = true;
        }
        _empty.IsVisible = Layout.Stats.Count == 0;
        _empty.Text = (chosen & SystemStats.All) == SystemStats.None ? "Choose what to show in Settings." : "System stats are not available.";
        Show();
    }

    private void Show()
    {
        if (Sampler is not { } sampler)
        {
            return;
        }
        var stats = Layout.Stats;
        for (var i = 0; i < stats.Count; i++)
        {
            _gauges[(int)stats[i]].Show(sampler);
        }
    }
}

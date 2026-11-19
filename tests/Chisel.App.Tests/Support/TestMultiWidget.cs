using System;
using Avalonia.Controls;
using Chisel.App.ViewModels;
using Chisel.App.Views.Widgets;
using Xunit;

namespace Chisel.App.Tests.Support;

// Tests that register the multi-instance test widget share this collection, so they never run beside
// tests that read the registry or the tray.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class MultiWidgetCollection
{
    public const string Name = "multi-instance widget";
}

// A test-only widget view. Each instance has its own draft text.
public sealed class TestFrameView : WidgetView, IDisposable
{
    public TestFrameView()
    {
        Content = Draft;
    }

    public TextBox Draft { get; } = new() { Name = "Draft" };

    public bool IsDisposed { get; private set; }

    public void Dispose() => IsDisposed = true;
}

// A test-only widget type that allows several instances. It is not part of the app's registry.
public static class TestMultiWidget
{
    public const string TypeId = "testframe";

    public static readonly WidgetDefinition Definition = new(
        TypeId, "Test frame", Icon: "M4,5 H20 V19 H4 Z", DefaultHeight: 1, WidgetRegistry.MinimumHeight,
        ScrollsContent: false, typeof(TestFrameView), AllowMultiple: true);

    public static IDisposable Register() => WidgetRegistry.Extend(Definition);

    public static string Id(string key) => $"{TypeId}:{key}";
}

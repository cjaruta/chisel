using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Chisel.App.ViewModels;

namespace Chisel.App.Views.Widgets;

// The card around one widget: its heading (with the widget's header actions on the right) and its content,
// scrolled inside the card when the widget asks for it. Created once per widget and reused when the layout changes.
public sealed class WidgetFrame : Border
{
    public WidgetFrame(WidgetDefinition widget, WidgetView view)
    {
        Widget = widget;
        View = view;
        Classes.Add("card");
        Classes.Add("widgetFrame");
        MinHeight = widget.MinHeight;

        var heading = new TextBlock { Text = widget.Title, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0) };
        heading.Classes.Add("cardHeading");
        heading.Classes.Add("widgetHeading");
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        header.Classes.Add("widgetHeader");
        header.Children.Add(heading);
        if (view.HeaderActions is { } actions)
        {
            Grid.SetColumn(actions, 1);
            header.Children.Add(actions);
        }
        DockPanel.SetDock(header, Dock.Top);

        Control body = view;
        if (widget.ScrollsContent)
        {
            view.Classes.Add("scrolledWidget");
            body = new ScrollViewer
            {
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                Content = view,
            };
        }

        Child = new DockPanel { Children = { header, body } };
    }

    // Styled as a plain Border, so the shared card look ("Border.card") applies.
    protected override System.Type StyleKeyOverride => typeof(Border);

    public WidgetDefinition Widget { get; }
    public string WidgetId => Widget.Id;
    public WidgetView View { get; }
}

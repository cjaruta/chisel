using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Chisel.App.ViewModels;
using Chisel.Core.Converter;

namespace Chisel.App.Views.Widgets;

// The unit converter widget. Two rows: the typed value with its unit, and the result with its unit. Larger, a category picker
// above them, a swap button by the value and a copy button by the result; larger still, category tabs in place of the picker and
// the value in other common units below. What is shown and every size come from ConverterLayout. Keyboard: Tab moves through the
// controls in reading order, Enter copies the result, Ctrl+Shift+S swaps the units. The view only works when the input, the units
// or its size change: nothing runs while idle.
public sealed class ConverterWidgetView : WidgetView
{
    private const string SwapIcon = "M7,20 V4 M3,8 L7,4 L11,8 M17,4 V20 M13,16 L17,20 L21,16";
    private const string CopyIcon = "M9,9 H20 V20 H9 Z M5,15 H4 V4 H15 V5";

    private readonly StackPanel _root = new() { Name = "ConverterRoot" };
    private readonly ComboBox _category = new() { Name = "ConverterCategory" };
    private readonly UniformGrid _tabs = new() { Name = "ConverterTabs", Columns = ConverterLayout.TabColumns, Rows = ConverterLayout.TabRows };
    private readonly List<ToggleButton> _tabButtons = new();
    private readonly Grid _valueRow = Row();
    private readonly Grid _resultRow = Row();
    private readonly TextBox _input = new() { Name = "ConverterInput" };
    private readonly TextBox _result = new() { Name = "ConverterResult", IsReadOnly = true };
    private readonly ComboBox _from = new() { Name = "ConverterFrom" };
    private readonly ComboBox _to = new() { Name = "ConverterTo" };
    private readonly Button _swap = IconButton("ConverterSwap", SwapIcon, "Swap units", "Swap units (Ctrl+Shift+S)");
    private readonly Button _copy = IconButton("ConverterCopy", CopyIcon, "Copy result", "Copy the result (Enter)");
    private readonly StackPanel _common = new() { Name = "ConverterCommon" };
    private readonly List<(Grid Row, TextBlock Value, TextBlock Unit)> _commonRows = new();
    private ConverterViewModel? _vm;
    private bool _syncing;

    public ConverterLayout Layout { get; private set; } = ConverterLayout.For(0, 0);

    public ConverterWidgetView()
    {
        ClipToBounds = true;
        VerticalAlignment = VerticalAlignment.Stretch;
        AutomationProperties.SetName(this, "Converter");

        foreach (var category in UnitCatalog.All)
        {
            _category.Items.Add(new ComboBoxItem { Content = category.Title, Tag = category.Id });
            var tab = new ToggleButton { Content = category.ShortTitle, Tag = category.Id };
            tab.Classes.Add("convTab");
            AutomationProperties.SetName(tab, category.Title);
            ToolTip.SetTip(tab, category.Title);
            tab.PropertyChanged += OnTabChanged;
            _tabButtons.Add(tab);
            _tabs.Children.Add(tab);
        }
        _category.Classes.Add("convField");
        AutomationProperties.SetName(_category, "Category");
        _category.SelectionChanged += (_, _) =>
        {
            if (!_syncing && _category.SelectedItem is ComboBoxItem { Tag: string id })
            {
                _vm?.SelectCategory(id);
            }
        };

        foreach (var field in new Control[] { _input, _result, _from, _to })
        {
            field.Classes.Add("convField");
        }
        AutomationProperties.SetName(_input, "From value");
        AutomationProperties.SetName(_result, "To value");
        AutomationProperties.SetName(_from, "From unit");
        AutomationProperties.SetName(_to, "To unit");
        _input.TextChanged += (_, _) =>
        {
            if (!_syncing && _vm is not null)
            {
                _vm.Input = _input.Text ?? "";
            }
        };
        _from.SelectionChanged += (_, _) => PickUnit(_from, u => _vm!.From = u);
        _to.SelectionChanged += (_, _) => PickUnit(_to, u => _vm!.To = u);
        _swap.Click += (_, _) => _vm?.SwapCommand.Execute(null);
        _copy.Click += (_, _) => Copy();

        Place(_valueRow, _input, _from, _swap);
        Place(_resultRow, _result, _to, _copy);
        for (var i = 0; i < ConverterLayout.MaxCommonRows; i++)
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), IsVisible = false };
            var value = new TextBlock { VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            var unit = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
            unit.Classes.Add("cdDim");
            Grid.SetColumn(unit, 1);
            row.Children.Add(value);
            row.Children.Add(unit);
            _commonRows.Add((row, value, unit));
            _common.Children.Add(row);
        }

        _root.Children.Add(_category);
        _root.Children.Add(_tabs);
        _root.Children.Add(_valueRow);
        _root.Children.Add(_resultRow);
        _root.Children.Add(_common);
        Content = _root;

        // The window binds some keys before the bubble phase, so the shortcuts are taken on the way down.
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
    }

    private static Grid Row() => new() { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto") };

    private static void Place(Grid row, Control field, Control unit, Control button)
    {
        Grid.SetColumn(unit, 1);
        Grid.SetColumn(button, 2);
        row.Children.Add(field);
        row.Children.Add(unit);
        row.Children.Add(button);
    }

    private static Button IconButton(string name, string icon, string automationName, string tip)
    {
        var path = new Path { Data = Geometry.Parse(icon), Stretch = Stretch.Uniform, StrokeThickness = 1.6, StrokeLineCap = PenLineCap.Round };
        path.Bind(Shape.StrokeProperty, new Binding(nameof(Button.Foreground)) { RelativeSource = new RelativeSource { AncestorType = typeof(Button) } });
        var button = new Button { Name = name, Content = path };
        button.Classes.Add("convButton");
        AutomationProperties.SetName(button, automationName);
        ToolTip.SetTip(button, tip);
        return button;
    }

    // Lifecycle --------------------------------------------------------------------------------------------------------------

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _vm = (DataContext as MainViewModel)?.Converter;
        if (_vm is null)
        {
            return;
        }
        _vm.PropertyChanged += OnChanged;
        _syncing = true;
        _input.Text = _vm.Input;
        _syncing = false;
        ShowCategory();
        ShowResult();
        ApplyLayout();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_vm is not null)
        {
            _vm.PropertyChanged -= OnChanged;
            _vm = null;
        }
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        ApplyLayout();
    }

    private void OnChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ConverterViewModel.Category):
                ShowCategory();
                break;
            case nameof(ConverterViewModel.From) or nameof(ConverterViewModel.To):
                ShowUnits();
                break;
            case nameof(ConverterViewModel.ResultText) or nameof(ConverterViewModel.IsError) or nameof(ConverterViewModel.Common):
                ShowResult();
                break;
        }
    }

    // Input ------------------------------------------------------------------------------------------------------------------

    private bool InEditMode => this.FindAncestorOfType<WidgetFrame>() is { IsEditing: true };

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (_vm is null || e.Handled || InEditMode)
        {
            return;
        }
        if (e.Key == Key.S && e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift))
        {
            _vm.SwapCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.None && !_from.IsDropDownOpen && !_to.IsDropDownOpen && !_category.IsDropDownOpen)
        {
            Copy();
            e.Handled = true;
        }
    }

    private void Copy()
    {
        if (_vm?.CopyText is { } text && TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
        {
            _ = clipboard.SetTextAsync(text);
        }
    }

    private void OnTabChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != ToggleButton.IsCheckedProperty || _syncing || sender is not ToggleButton { Tag: string id } tab)
        {
            return;
        }
        if (tab.IsChecked == true)
        {
            _vm?.SelectCategory(id);
        }
        SyncTabs(); // a click on the current tab leaves it checked
    }

    private void PickUnit(ComboBox box, System.Action<Unit> set)
    {
        if (!_syncing && _vm is not null && box.SelectedIndex >= 0 && box.SelectedIndex < _vm.Category.Units.Count)
        {
            set(_vm.Category.Units[box.SelectedIndex]);
        }
    }

    // Showing ----------------------------------------------------------------------------------------------------------------

    private void ShowCategory()
    {
        if (_vm is not { } vm)
        {
            return;
        }
        _syncing = true;
        _category.SelectedIndex = UnitCatalog.All.ToList().IndexOf(vm.Category);
        foreach (var box in new[] { _from, _to })
        {
            box.Items.Clear();
            foreach (var unit in vm.Category.Units)
            {
                var item = new ComboBoxItem { Content = unit.Symbol };
                AutomationProperties.SetName(item, unit.Name);
                ToolTip.SetTip(item, unit.Name);
                box.Items.Add(item);
            }
        }
        _syncing = false;
        SyncTabs();
        ShowUnits();
    }

    private void SyncTabs()
    {
        _syncing = true;
        foreach (var tab in _tabButtons)
        {
            tab.IsChecked = Equals(tab.Tag, _vm?.Category.Id);
        }
        _syncing = false;
    }

    private void ShowUnits()
    {
        if (_vm is not { } vm)
        {
            return;
        }
        _syncing = true;
        _from.SelectedIndex = IndexOf(vm.Category, vm.From);
        _to.SelectedIndex = IndexOf(vm.Category, vm.To);
        ToolTip.SetTip(_from, vm.From.Name);
        ToolTip.SetTip(_to, vm.To.Name);
        _syncing = false;
    }

    private static int IndexOf(UnitCategory category, Unit unit)
    {
        for (var i = 0; i < category.Units.Count; i++)
        {
            if (category.Units[i] == unit)
            {
                return i;
            }
        }
        return -1;
    }

    private void ShowResult()
    {
        if (_vm is not { } vm)
        {
            return;
        }
        _result.Text = vm.ResultText;
        _result.Classes.Set("convError", vm.IsError);
        for (var i = 0; i < _commonRows.Count; i++)
        {
            var (row, value, unit) = _commonRows[i];
            var has = i < vm.Common.Count && i < Layout.CommonRows;
            row.IsVisible = has;
            if (has)
            {
                value.Text = vm.Common[i].Text + " " + vm.Common[i].Unit.Symbol;
                unit.Text = vm.Common[i].Unit.Name;
                AutomationProperties.SetName(row, value.Text);
            }
        }
    }

    private void ApplyLayout()
    {
        var layout = Layout = ConverterLayout.For(Bounds.Width, Bounds.Height);
        _root.Margin = new Thickness(layout.Pad);
        _root.Spacing = 0;

        _category.IsVisible = layout.ShowCategoryPicker;
        _category.Height = layout.FieldHeight;
        _category.FontSize = Font(layout.FieldFont);
        _category.Margin = new Thickness(0, 0, 0, layout.Gap);

        _tabs.IsVisible = layout.ShowTabs;
        _tabs.Height = ConverterLayout.TabRows * layout.TabHeight;
        _tabs.Margin = new Thickness(0, 0, 0, layout.Gap);
        foreach (var tab in _tabButtons)
        {
            tab.FontSize = Font(layout.TabFont);
            tab.Margin = new Thickness(1);
        }

        foreach (var (row, bottom) in new[] { (_valueRow, layout.Gap), (_resultRow, 0.0) })
        {
            row.Height = layout.FieldHeight;
            row.Margin = new Thickness(0, 0, 0, bottom);
        }
        foreach (var field in new Control[] { _input, _result })
        {
            ((TextBox)field).FontSize = Font(layout.FieldFont);
        }
        foreach (var box in new[] { _from, _to })
        {
            box.Width = layout.UnitWidth;
            box.FontSize = Font(layout.FieldFont);
            box.Margin = new Thickness(layout.Gap, 0, 0, 0);
        }
        foreach (var button in new[] { _swap, _copy })
        {
            button.IsVisible = layout.ShowButtons;
            button.Width = layout.ButtonWidth;
            button.Margin = new Thickness(layout.Gap, 0, 0, 0);
            ((Path)button.Content!).Width = ((Path)button.Content!).Height = layout.ButtonWidth * 0.45;
        }

        _common.IsVisible = layout.CommonRows > 0;
        _common.Margin = new Thickness(0, layout.Gap, 0, 0);
        foreach (var (row, value, unit) in _commonRows)
        {
            row.Height = layout.CommonRowHeight;
            value.FontSize = unit.FontSize = Font(layout.CommonFont);
        }
        ShowResult();
    }

    private static double Font(double size) => System.Math.Max(1, size);
}

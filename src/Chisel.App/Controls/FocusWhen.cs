using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Chisel.App.Controls;

// FocusWhen.IsTrue="{Binding IsEditing}" on a TextBox: when the value turns true the box takes focus and selects its text
// (for an in-place editor that appears on demand). Does nothing at all while the value does not change.
public sealed class FocusWhen : AvaloniaObject
{
    public static readonly AttachedProperty<bool> IsTrueProperty =
        AvaloniaProperty.RegisterAttached<FocusWhen, TextBox, bool>("IsTrue");

    static FocusWhen()
    {
        IsTrueProperty.Changed.AddClassHandler<TextBox>((box, e) =>
        {
            if (e.NewValue is true)
            {
                // After the box has become visible and been laid out.
                Dispatcher.UIThread.Post(() =>
                {
                    box.Focus();
                    box.SelectAll();
                }, DispatcherPriority.Background);
            }
        });
    }

    public static bool GetIsTrue(TextBox box) => box.GetValue(IsTrueProperty);

    public static void SetIsTrue(TextBox box, bool value) => box.SetValue(IsTrueProperty, value);
}

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace AccessibleMediaController.Windows.Controls;

public static class EditableFieldSelection
{
    public static readonly DependencyProperty ReplaceOnKeyboardFocusProperty =
        DependencyProperty.RegisterAttached(
            "ReplaceOnKeyboardFocus",
            typeof(bool),
            typeof(EditableFieldSelection),
            new PropertyMetadata(false, OnReplaceOnKeyboardFocusChanged));

    private static readonly DependencyProperty MouseFocusPendingProperty =
        DependencyProperty.RegisterAttached(
            "MouseFocusPending",
            typeof(bool),
            typeof(EditableFieldSelection),
            new PropertyMetadata(false));

    public static void SetReplaceOnKeyboardFocus(DependencyObject element, bool value) =>
        element.SetValue(ReplaceOnKeyboardFocusProperty, value);

    public static bool GetReplaceOnKeyboardFocus(DependencyObject element) =>
        (bool)element.GetValue(ReplaceOnKeyboardFocusProperty);

    public static void Attach(System.Windows.Forms.NumericUpDown input)
    {
        ArgumentNullException.ThrowIfNull(input);
        input.Enter += (_, _) => SelectAllForKeyboardEntry(input);
    }

    internal static void SelectAllForKeyboardEntry(TextBox input)
    {
        if (input.IsReadOnly || !input.IsEnabled) return;
        input.SelectAll();
    }

    internal static void SelectAllForKeyboardEntry(System.Windows.Forms.NumericUpDown input)
    {
        if (!input.Enabled || input.ReadOnly) return;
        input.Select(0, input.Text.Length);
    }

    private static void OnReplaceOnKeyboardFocusChanged(
        DependencyObject element,
        DependencyPropertyChangedEventArgs args)
    {
        if (element is not TextBox input) return;
        if (args.NewValue is true)
        {
            input.GotKeyboardFocus += TextBox_GotKeyboardFocus;
            input.PreviewMouseLeftButtonDown += TextBox_PreviewMouseLeftButtonDown;
        }
        else
        {
            input.GotKeyboardFocus -= TextBox_GotKeyboardFocus;
            input.PreviewMouseLeftButtonDown -= TextBox_PreviewMouseLeftButtonDown;
        }
    }

    private static void TextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is not TextBox input
            || (bool)input.GetValue(MouseFocusPendingProperty)) return;
        SelectAllForKeyboardEntry(input);
    }

    private static void TextBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not TextBox input || input.IsKeyboardFocusWithin) return;
        input.SetValue(MouseFocusPendingProperty, true);
        input.Dispatcher.BeginInvoke(
            () => input.SetValue(MouseFocusPendingProperty, false),
            DispatcherPriority.Input);
    }
}

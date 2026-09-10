using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AccessibleMediaController.Windows.Controls;

internal static class ListRefreshFocus
{
    internal static void Run(ListBox list, Action refresh, Action restoreRowFocus)
    {
        var ownedFocus = list.IsKeyboardFocusWithin && Window.GetWindow(list)?.IsActive == true;
        if (ownedFocus)
        {
            list.Focus();
            Keyboard.Focus(list);
        }
        refresh();
        // Never take focus from a filter, menu, dialog, player or another app.
        // No unconditional deferred restoration: that can steal the next keystroke.
        if (ownedFocus && list.IsKeyboardFocusWithin && Window.GetWindow(list)?.IsActive == true)
            restoreRowFocus();
    }
}

using System.Windows.Automation;
using System.Windows.Controls;

namespace AccessibleMediaController.Windows;

internal static class MenuAccessibility
{
    public static void NormalizeMainMenu(Menu menu)
    {
        foreach (var item in menu.Items.OfType<MenuItem>())
        {
            NormalizeItem(item, preserveHeaderMnemonic: true);
            NormalizeChildren(item);
        }
    }

    public static void NormalizeContextMenu(ContextMenu? menu)
    {
        if (menu is null) return;

        foreach (var item in menu.Items.OfType<MenuItem>())
        {
            NormalizeItem(item, preserveHeaderMnemonic: false);
            NormalizeChildren(item);
        }
    }

    public static void SetPresentation(MenuItem menuItem, string label)
    {
        var accessibleLabel = RemoveMnemonic(label);
        menuItem.Header = accessibleLabel;
        AutomationProperties.SetName(menuItem, accessibleLabel);
    }

    private static void NormalizeChildren(MenuItem parent)
    {
        foreach (var item in parent.Items.OfType<MenuItem>())
        {
            NormalizeItem(item, preserveHeaderMnemonic: false);
            NormalizeChildren(item);
        }
    }

    private static void NormalizeItem(MenuItem item, bool preserveHeaderMnemonic)
    {
        if (item.Header is not string header) return;

        var accessibleLabel = RemoveMnemonic(header);
        if (!preserveHeaderMnemonic)
        {
            item.Header = accessibleLabel;
        }

        // InputGestureText/AcceleratorKey is the single source of the shortcut
        // for UI Automation. Repeating it in Name makes NVDA speak it twice.
        AutomationProperties.SetName(item, accessibleLabel);
    }

    private static string RemoveMnemonic(string label) => label.Replace("_", string.Empty, StringComparison.Ordinal);
}

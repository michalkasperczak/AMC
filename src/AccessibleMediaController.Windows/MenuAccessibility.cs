using System.Windows;
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

        UpdateVisibleItemSetMetadata(menu);
    }

    public static void UpdateVisibleItemSetMetadata(Menu menu)
    {
        SetVisibleItemPositions(menu.Items.OfType<MenuItem>());

        foreach (var item in menu.Items.OfType<MenuItem>())
        {
            UpdateVisibleChildSetMetadata(item);
        }
    }

    public static void UpdateVisibleItemSetMetadata(ContextMenu menu)
    {
        SetVisibleItemPositions(menu.Items.OfType<MenuItem>());

        foreach (var item in menu.Items.OfType<MenuItem>())
        {
            UpdateVisibleChildSetMetadata(item);
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

        UpdateVisibleItemSetMetadata(menu);
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

    private static void UpdateVisibleChildSetMetadata(MenuItem parent)
    {
        var children = parent.Items.OfType<MenuItem>().ToArray();
        SetVisibleItemPositions(children);

        foreach (var child in children)
        {
            UpdateVisibleChildSetMetadata(child);
        }
    }

    private static void SetVisibleItemPositions(IEnumerable<MenuItem> items)
    {
        var allItems = items.ToArray();
        var visibleItems = allItems
            .Where(item => item.Visibility == Visibility.Visible)
            .ToArray();

        for (var index = 0; index < visibleItems.Length; index++)
        {
            AutomationProperties.SetPositionInSet(visibleItems[index], index + 1);
            AutomationProperties.SetSizeOfSet(visibleItems[index], visibleItems.Length);
        }

        foreach (var hiddenItem in allItems.Except(visibleItems))
        {
            AutomationProperties.SetPositionInSet(hiddenItem, -1);
            AutomationProperties.SetSizeOfSet(hiddenItem, -1);
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
        if (string.IsNullOrWhiteSpace(AutomationProperties.GetAcceleratorKey(item))
            && !string.IsNullOrWhiteSpace(item.InputGestureText))
        {
            AutomationProperties.SetAcceleratorKey(item, item.InputGestureText);
        }
    }

    private static string RemoveMnemonic(string label) => label.Replace("_", string.Empty, StringComparison.Ordinal);
}

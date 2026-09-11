using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Devices.WiiM;
using AccessibleMediaController.Windows.Controls;
using AccessibleMediaController.Windows.Services;

namespace AccessibleMediaController.Windows;

public partial class WiiMDevicePresetsWindow : AccessibleWindow
{
    private IReadOnlyList<WiiMDevicePresetRow> rows;
    private readonly Action<WiiMDevicePresetsWindow, int>? _assignShortcut;
    private readonly Func<IReadOnlyDictionary<int, int>>? _reloadShortcuts;

    public WiiMDevicePresetsWindow(
        string deviceName,
        IReadOnlyList<WiiMPresetInformation> presets,
        bool selectForShortcut = false,
        IReadOnlyDictionary<int, int>? shortcutSlotsByNativePreset = null,
        int? initialPresetNumber = null,
        Action<WiiMDevicePresetsWindow, int>? assignShortcut = null,
        Func<IReadOnlyDictionary<int, int>>? reloadShortcuts = null)
    {
        InitializeComponent();
        _assignShortcut = assignShortcut;
        _reloadShortcuts = reloadShortcuts;
        Title = selectForShortcut
            ? $"Wybierz gotowy preset — {deviceName}"
            : $"Presety urządzenia — {deviceName}";
        DescriptionText.Text = selectForShortcut
            ? $"Wybierz istniejący preset urządzenia {deviceName}, który chcesz przypisać do skrótu AMC. "
              + "Cyfra wybiera preset. Enter lub Spacja przechodzi do wyboru skrótu. Escape anuluje. Ustawienia urządzenia nie zostaną zmienione."
            : $"Presety zapisane w urządzeniu {deviceName}. "
              + "Cyfra wybiera miejsce. Page Up i Page Down przechodzą po zajętych miejscach bez ich uruchamiania. Enter lub Spacja uruchamia zajęty preset. "
              + "Ctrl+Alt+Shift+P przypisuje zaznaczony preset do skrótu AMC. Ta lista nie zmienia ustawień urządzenia.";
        ActivateButton.Content = selectForShortcut ? "_Wybierz" : "_Uruchom";
        AssignShortcutButton.Visibility = selectForShortcut ? Visibility.Collapsed : Visibility.Visible;
        AutomationProperties.SetName(
            ActivateButton,
            selectForShortcut ? "Wybierz preset do przypisania" : "Uruchom preset");
        AutomationProperties.SetHelpText(
            PresetList,
            selectForShortcut
                ? "Strzałki albo cyfra wybierają gotowy preset. Enter lub Spacja przechodzi do wyboru skrótu AMC."
                : "Strzałki albo cyfra wybierają preset. Page Up i Page Down przechodzą po zajętych presetach. Enter lub Spacja uruchamia zajęty preset. Ctrl+Alt+Shift+P przypisuje skrót AMC.");
        rows = Enumerable.Range(1, 12)
            .Select(number => new WiiMDevicePresetRow(
                number,
                presets.FirstOrDefault(preset => preset.Number == number),
                shortcutSlotsByNativePreset?.GetValueOrDefault(number)))
            .ToArray();
        PresetList.ItemsSource = rows;
        var requestedIndex = initialPresetNumber is >= 1 and <= 12
            ? initialPresetNumber.Value - 1
            : rows.ToList().FindIndex(row => row.Preset is not null);
        PresetList.SelectedIndex = Math.Max(0, requestedIndex);
        SelectForShortcut = selectForShortcut;
    }

    public int? SelectedPresetNumber { get; private set; }
    public bool SelectForShortcut { get; }
    public bool ShortcutAssignmentRequested { get; private set; }

    private void Window_ContentRendered(object? sender, EventArgs e) => FocusSelectedRow();

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!PresetList.IsKeyboardFocusWithin) return;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var modifiers = Keyboard.Modifiers;
        if (modifiers == ModifierKeys.Control && key == Key.C)
        {
            CopySelected(includeLinks: false);
            e.Handled = true;
            return;
        }
        if (modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && key == Key.C)
        {
            CopySelected(includeLinks: true);
            e.Handled = true;
            return;
        }
        if (modifiers == ModifierKeys.None && RadioPresetKeyMap.TryGetSlot(key, out var slot))
        {
            PresetList.SelectedItems.Clear();
            PresetList.SelectedIndex = slot - 1;
            PresetList.ScrollIntoView(PresetList.SelectedItem);
            FocusSelectedRow();
            e.Handled = true;
            return;
        }
        if (modifiers == ModifierKeys.None && key is Key.PageUp or Key.PageDown)
        {
            MoveToOccupiedPreset(key == Key.PageDown ? 1 : -1);
            e.Handled = true;
            return;
        }
        if (modifiers == ModifierKeys.None && key is Key.Enter or Key.Space)
        {
            if (!SelectForShortcut) ShortcutAssignmentRequested = false;
            ActivateSelected();
            e.Handled = true;
            return;
        }
        if (!SelectForShortcut
            && modifiers == (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift)
            && key == Key.P)
        {
            AssignShortcut_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
    }

    private void AssignShortcut_Click(object sender, RoutedEventArgs e)
    {
        if (_assignShortcut is not null && PresetList.SelectedItem is WiiMDevicePresetRow { Preset: not null } row)
        {
            _assignShortcut(this, row.Number);
            if (_reloadShortcuts is not null)
            {
                var slots = _reloadShortcuts();
                rows = rows.Select(item => item with { ShortcutSlot = slots.GetValueOrDefault(item.Number) }).ToArray();
                PresetList.ItemsSource = rows;
                PresetList.SelectedIndex = row.Number - 1;
            }
            FocusSelectedRow();
            return;
        }
        ShortcutAssignmentRequested = true;
        ActivateSelected();
    }

    internal bool MoveToOccupiedPreset(int direction)
    {
        if (rows.All(row => row.Preset is null))
        {
            PresetStatus.Announce("Brak zajętych presetów urządzenia");
            return false;
        }
        var current = Math.Max(0, PresetList.SelectedIndex);
        for (var offset = 1; offset <= rows.Count; offset++)
        {
            var index = (current + direction * offset + rows.Count) % rows.Count;
            if (rows[index].Preset is null) continue;
            PresetList.SelectedIndex = index;
            PresetList.ScrollIntoView(PresetList.SelectedItem);
            FocusSelectedRow();
            return true;
        }
        return false;
    }

    private void FocusSelectedRow()
    {
        PresetList.UpdateLayout();
        if (PresetList.ItemContainerGenerator.ContainerFromIndex(PresetList.SelectedIndex)
            is System.Windows.Controls.ListBoxItem item)
        {
            item.Focus();
            Keyboard.Focus(item);
            return;
        }
        Keyboard.Focus(PresetList);
    }

    private void ActivateSelected()
    {
        if (PresetList.SelectedItem is not WiiMDevicePresetRow row) return;
        if (row.Preset is null)
        {
            PresetStatus.Announce($"Preset {row.Number} jest pusty");
            return;
        }
        SelectedPresetNumber = row.Number;
        DialogResult = true;
    }

    private void CopySelected(bool includeLinks)
    {
        var selected = PresetList.SelectedItems
            .OfType<WiiMDevicePresetRow>()
            .Where(row => row.Preset is not null)
            .OrderBy(row => row.Number)
            .ToArray();
        if (selected.Length == 0)
        {
            PresetStatus.Announce("Zaznaczenie nie zawiera zajętego presetu");
            return;
        }
        var text = includeLinks
            ? string.Join(
                Environment.NewLine + Environment.NewLine,
                selected.Select(row => string.IsNullOrWhiteSpace(row.Preset!.Uri)
                    ? row.Preset.Name
                    : $"{row.Preset.Name}{Environment.NewLine}{row.Preset.Uri}"))
            : string.Join(Environment.NewLine, selected.Select(row => row.Preset!.Name));
        if (!ClipboardRetry.TrySetText(text, out var error))
        {
            PresetStatus.Announce(error);
            return;
        }
        PresetStatus.Announce(selected.Length == 1
            ? includeLinks ? "Skopiowano nazwę i dostępne łącze" : "Skopiowano nazwę"
            : includeLinks
                ? $"Skopiowano nazwy i dostępne łącza: {selected.Length}"
                : $"Skopiowano nazwy: {selected.Length}");
    }

    private void Activate_Click(object sender, RoutedEventArgs e)
    {
        if (!SelectForShortcut) ShortcutAssignmentRequested = false;
        ActivateSelected();
    }
}

public sealed record WiiMDevicePresetRow(
    int Number,
    WiiMPresetInformation? Preset,
    int? ShortcutSlot = null)
{
    public string NavigationText => Preset?.Name ?? $"Preset {Number}";
    public string Label
    {
        get
        {
            if (Preset is null) return $"Preset {Number}, pusty";
            var source = string.IsNullOrWhiteSpace(Preset.Source)
                ? string.Empty
                : $", {Preset.Source}";
            var shortcut = ShortcutSlot is >= 1 and <= RadioPresetSlots.Count
                ? $", skrót Ctrl+Shift+{RadioPresetSlots.SpokenShortcutLabel(ShortcutSlot.Value)}"
                : string.Empty;
            return $"Preset {Number}, {Preset.Name}{source}{shortcut}";
        }
    }

    public override string ToString() => Label;
}

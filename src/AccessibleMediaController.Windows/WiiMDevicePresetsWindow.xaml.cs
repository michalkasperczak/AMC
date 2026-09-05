using System.Windows;
using System.Windows.Input;
using AccessibleMediaController.Core.Devices.WiiM;
using AccessibleMediaController.Windows.Controls;
using AccessibleMediaController.Windows.Services;

namespace AccessibleMediaController.Windows;

public partial class WiiMDevicePresetsWindow : AccessibleWindow
{
    private readonly IReadOnlyList<WiiMDevicePresetRow> rows;

    public WiiMDevicePresetsWindow(
        string deviceName,
        IReadOnlyList<WiiMPresetInformation> presets)
    {
        InitializeComponent();
        Title = $"Presety urządzenia — {deviceName}";
        DescriptionText.Text = $"Presety zapisane w urządzeniu {deviceName}. "
            + "Cyfra wybiera miejsce. Enter lub Spacja uruchamia zajęty preset. "
            + "Ta lista nie zmienia ustawień urządzenia.";
        rows = Enumerable.Range(1, 12)
            .Select(number => new WiiMDevicePresetRow(
                number,
                presets.FirstOrDefault(preset => preset.Number == number)))
            .ToArray();
        PresetList.ItemsSource = rows;
        PresetList.SelectedIndex = Math.Max(0, rows.ToList().FindIndex(row => row.Preset is not null));
    }

    public int? SelectedPresetNumber { get; private set; }

    private void Window_ContentRendered(object? sender, EventArgs e) => FocusSelectedRow();

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
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
        if (modifiers == ModifierKeys.None && key is Key.Enter or Key.Space)
        {
            ActivateSelected();
            e.Handled = true;
        }
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

    private void Activate_Click(object sender, RoutedEventArgs e) => ActivateSelected();
}

public sealed record WiiMDevicePresetRow(int Number, WiiMPresetInformation? Preset)
{
    public string NavigationText => Preset?.Name ?? $"Preset {Number}";
    public string Label => Preset is null
        ? $"Preset {Number}, pusty"
        : string.IsNullOrWhiteSpace(Preset.Source)
            ? $"Preset {Number}, {Preset.Name}"
            : $"Preset {Number}, {Preset.Name}, {Preset.Source}";

    public override string ToString() => Label;
}

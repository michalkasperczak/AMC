using System.Windows;
using System.Windows.Input;
using AccessibleMediaController.Windows.Controls;

namespace AccessibleMediaController.Windows;

public partial class RadioPresetsWindow : AccessibleWindow
{
    private readonly IReadOnlyList<RadioPresetChoice> _choices;

    public RadioPresetsWindow(IReadOnlyList<RadioPresetChoice> choices, string? currentStationId)
    {
        InitializeComponent();
        _choices = choices;
        PresetList.ItemsSource = choices;
        var selected = choices.ToList().FindIndex(choice =>
            choice.StationId is not null
            && string.Equals(choice.StationId, currentStationId, StringComparison.Ordinal));
        if (selected < 0) selected = choices.ToList().FindIndex(choice => choice.StationId is not null);
        PresetList.SelectedIndex = selected >= 0 ? selected : 0;
    }

    public int? SelectedSlot { get; private set; }

    private void Window_ContentRendered(object? sender, EventArgs e)
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

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.None && e.Key is Key.Enter or Key.Space)
        {
            ActivateSelected();
            e.Handled = true;
        }
    }

    private void ActivateSelected()
    {
        if (PresetList.SelectedItem is not RadioPresetChoice choice) return;
        if (choice.StationId is null)
        {
            PresetStatus.Announce(
                $"Preset {choice.SlotLabel} pusty. Ctrl+Shift+P dodaje bieżącą stację");
            return;
        }
        SelectedSlot = choice.Slot;
        DialogResult = true;
    }

    private void Activate_Click(object sender, RoutedEventArgs e) => ActivateSelected();
}

public sealed record RadioPresetChoice(
    int Slot,
    string SlotLabel,
    string? StationId,
    string? StationName)
{
    public string Label => StationId is null
        ? $"Preset {SlotLabel} — pusty"
        : $"Preset {SlotLabel} — {StationName}";

    public override string ToString() => Label;
}

using System.Windows;
using System.Windows.Input;
using AccessibleMediaController.Windows.Controls;
using AccessibleMediaController.Windows.Services;

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
        var modifiers = Keyboard.Modifiers;
        if (modifiers == ModifierKeys.Control && e.Key == Key.C)
        {
            CopySelectedNames();
            e.Handled = true;
            return;
        }
        if (modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.C)
        {
            CopySelectedNamesAndLinks();
            e.Handled = true;
            return;
        }
        if (modifiers == ModifierKeys.None && e.Key is Key.Enter or Key.Space)
        {
            ActivateSelected();
            e.Handled = true;
        }
    }

    private IReadOnlyList<RadioPresetChoice> SelectedOccupiedChoices() =>
        PresetList.SelectedItems
            .OfType<RadioPresetChoice>()
            .Where(choice => choice.StationId is not null)
            .OrderBy(choice => choice.Slot)
            .ToArray();

    private void CopySelectedNames()
    {
        var choices = SelectedOccupiedChoices();
        if (choices.Count == 0)
        {
            PresetStatus.Announce("Zaznaczenie nie zawiera zajętego presetu");
            return;
        }
        var text = string.Join(Environment.NewLine, choices.Select(choice => choice.StationName));
        if (!ClipboardRetry.TrySetText(text, out var error))
        {
            PresetStatus.Announce(error);
            return;
        }
        PresetStatus.Announce(choices.Count == 1
            ? "Skopiowano nazwę stacji"
            : $"Skopiowano nazwy stacji: {choices.Count}");
    }

    private void CopySelectedNamesAndLinks()
    {
        var choices = SelectedOccupiedChoices()
            .Where(choice => !string.IsNullOrWhiteSpace(choice.ShareableLocation))
            .ToArray();
        if (choices.Length == 0)
        {
            PresetStatus.Announce("Zaznaczenie nie zawiera zajętego presetu z adresem");
            return;
        }
        var text = string.Join(
            Environment.NewLine,
            choices.SelectMany(choice => new[] { choice.StationName!, choice.ShareableLocation! }));
        if (!ClipboardRetry.TrySetText(text, out var error))
        {
            PresetStatus.Announce(error);
            return;
        }
        PresetStatus.Announce(choices.Length == 1
            ? "Skopiowano nazwę i łącze"
            : $"Skopiowano nazwy i łącza: {choices.Length}");
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
    string SpokenShortcutLabel,
    string? StationId,
    string? StationName,
    string? ShareableLocation)
{
    public string Label => StationId is null
        ? $"Preset {SlotLabel}, skrót Ctrl+Shift+{SpokenShortcutLabel} — pusty"
        : $"Preset {SlotLabel}, skrót Ctrl+Shift+{SpokenShortcutLabel} — {StationName}";

    public override string ToString() => Label;
}

using System.Collections.Specialized;
using System.IO;
using System.Windows;
using System.Windows.Input;
using AccessibleMediaController.Windows.Controls;
using AccessibleMediaController.Windows.Services;

namespace AccessibleMediaController.Windows;

public partial class RadioPresetsWindow : AccessibleWindow
{
    private readonly IReadOnlyList<RadioPresetChoice> _choices;
    private readonly bool _copyLocalTargets;

    public RadioPresetsWindow(
        IReadOnlyList<RadioPresetChoice> choices,
        string? currentTargetId,
        string sessionName = "Radio internetowe",
        bool copyLocalTargets = false)
    {
        InitializeComponent();
        _choices = choices;
        _copyLocalTargets = copyLocalTargets;
        Title = $"Presety — {sessionName}";
        DescriptionText.Text = $"Presety sesji {sessionName}. Cyfry wybierają miejsce. " +
            "Enter lub Spacja uruchamia zajętą pozycję. " +
            "Ta lista nigdy nie zmienia ani nie nadpisuje presetów.";
        System.Windows.Automation.AutomationProperties.SetName(PresetList, $"Presety, {sessionName}");
        PresetList.ItemsSource = choices;
        var selected = choices.ToList().FindIndex(choice =>
            choice.StationId is not null
            && string.Equals(choice.StationId, currentTargetId, StringComparison.Ordinal));
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
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
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
        if (modifiers == ModifierKeys.None && RadioPresetKeyMap.TryGetSlot(key, out var slot))
        {
            SelectSlot(slot);
            e.Handled = true;
            return;
        }
        if (modifiers == ModifierKeys.None && key is Key.Enter or Key.Space)
        {
            ActivateSelected();
            e.Handled = true;
        }
    }

    private void SelectSlot(int slot)
    {
        var index = slot - 1;
        if (index < 0 || index >= _choices.Count) return;
        PresetList.SelectedItems.Clear();
        PresetList.SelectedIndex = index;
        PresetList.ScrollIntoView(PresetList.SelectedItem);
        PresetList.UpdateLayout();
        if (PresetList.ItemContainerGenerator.ContainerFromIndex(index)
            is System.Windows.Controls.ListBoxItem item)
        {
            item.Focus();
            Keyboard.Focus(item);
            return;
        }
        Keyboard.Focus(PresetList);
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
            ? "Skopiowano nazwę elementu"
            : $"Skopiowano nazwy elementów: {choices.Count}");
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
        var text = _copyLocalTargets
            ? string.Join(Environment.NewLine, choices.Select(choice => choice.ShareableLocation))
            : string.Join(
                Environment.NewLine,
                choices.SelectMany(choice => new[] { choice.StationName!, choice.ShareableLocation! }));
        var data = new DataObject();
        data.SetData(DataFormats.UnicodeText, text);
        if (_copyLocalTargets)
        {
            var paths = choices
                .Select(choice => choice.ShareableLocation!)
                .Where(path => File.Exists(path) || Directory.Exists(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (paths.Length > 0)
            {
                var fileDropList = new StringCollection();
                fileDropList.AddRange(paths);
                data.SetFileDropList(fileDropList);
            }
        }
        if (!ClipboardRetry.TrySetDataObject(data, out var error))
        {
            PresetStatus.Announce(error);
            return;
        }
        PresetStatus.Announce(_copyLocalTargets
            ? choices.Length == 1
                ? "Skopiowano pełną ścieżkę i dostępny element"
                : $"Skopiowano pełne ścieżki i dostępne elementy: {choices.Length}"
            : choices.Length == 1
                ? "Skopiowano nazwę i łącze"
                : $"Skopiowano nazwy i łącza: {choices.Length}");
    }

    private void ActivateSelected()
    {
        if (PresetList.SelectedItem is not RadioPresetChoice choice) return;
        if (choice.StationId is null)
        {
            PresetStatus.Announce(
                $"Preset {choice.SlotLabel} pusty. Ctrl+Alt+Shift+P przypisuje bieżący element");
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
    private string PositionLabel => Slot <= 9
        ? $"Preset numer {SlotLabel}"
        : $"Preset numer {SlotLabel}, klawisz {SpokenShortcutLabel}";

    public string Label => StationId is null
        ? $"{PositionLabel}, skrót Ctrl+Shift+{SpokenShortcutLabel} — pusty"
        : $"{PositionLabel}, skrót Ctrl+Shift+{SpokenShortcutLabel} — {StationName}";

    public override string ToString() => Label;
}

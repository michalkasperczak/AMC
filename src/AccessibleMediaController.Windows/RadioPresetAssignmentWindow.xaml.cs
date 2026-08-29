using System.Windows;
using System.Windows.Input;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Windows.Controls;

namespace AccessibleMediaController.Windows;

public enum RadioPresetAssignmentAction
{
    Save,
    Remove
}

public partial class RadioPresetAssignmentWindow : AccessibleWindow
{
    private readonly string _stationName;
    private readonly string _stationId;
    private readonly IReadOnlyList<RadioPresetChoice> _choices;
    private bool _removePending;
    private int? _lastRequestedSlot;
    private int? _replacementArmedSlot;

    public RadioPresetAssignmentWindow(
        string stationName,
        string stationId,
        IReadOnlyList<RadioPresetChoice> choices,
        int? firstFreeSlot,
        int initialSlot,
        string sessionName = "Radio internetowe")
    {
        InitializeComponent();
        Title = $"Przypisz preset — {sessionName}";
        _stationName = stationName;
        _stationId = stationId;
        _choices = choices;
        PresetList.ItemsSource = choices;
        PresetList.SelectedIndex = Math.Clamp(initialSlot - 1, 0, choices.Count - 1);
        DescriptionText.Text = firstFreeSlot is int freeSlot
            ? $"Dodaj preset: {stationName}. Pierwsze wolne miejsce: preset {RadioPresetSlots.Label(freeSlot)}, " +
              $"skrót Ctrl+Shift+{RadioPresetSlots.SpokenShortcutLabel(freeSlot)}. " +
              "Naciśnij cyfrę, minus albo znak równości, a następnie Enter. Escape anuluje."
            : $"Dodaj preset: {stationName}. Nie ma wolnego miejsca. " +
              "Aby zastąpić zajęty preset, wskaż dwukrotnie to samo miejsce i naciśnij Enter. Escape anuluje.";
    }

    public int SelectedSlot { get; private set; }
    public RadioPresetAssignmentAction SelectedAction { get; private set; }

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
        if (Keyboard.Modifiers == ModifierKeys.None && RadioPresetKeyMap.TryGetSlot(key, out var slot))
        {
            SelectSlot(slot, announce: true);
            e.Handled = true;
            return;
        }
        if (Keyboard.Modifiers == ModifierKeys.None && key == Key.Delete)
        {
            PrepareRemoval();
            e.Handled = true;
            return;
        }
        if (Keyboard.Modifiers == ModifierKeys.None && key == Key.Enter)
        {
            Confirm();
            e.Handled = true;
        }
    }

    private void SelectSlot(int slot, bool announce)
    {
        _removePending = false;
        var previouslySelectedSlot = (PresetList.SelectedItem as RadioPresetChoice)?.Slot;
        var repeatedSlot = _lastRequestedSlot == slot && previouslySelectedSlot == slot;
        _lastRequestedSlot = slot;
        PresetList.SelectedIndex = slot - 1;
        PresetList.ScrollIntoView(PresetList.SelectedItem);
        if (!announce || PresetList.SelectedItem is not RadioPresetChoice choice) return;
        var replacesOtherStation = choice.StationId is not null
            && !string.Equals(choice.StationId, _stationId, StringComparison.Ordinal);
        _replacementArmedSlot = replacesOtherStation && repeatedSlot ? slot : null;
        AssignmentStatus.Announce(choice.StationId is null
            ? $"Preset {choice.SlotLabel} pusty. {_stationName}. Enter zapisuje, Escape anuluje"
            : !replacesOtherStation
                ? $"Preset {choice.SlotLabel} już zawiera tę stację. Enter zatwierdza, Escape anuluje"
                : _replacementArmedSlot == slot
                    ? $"Potwierdzono miejsce {choice.SlotLabel}. Enter zastępuje element {choice.StationName} elementem {_stationName}, Escape anuluje"
                    : $"Preset {choice.SlotLabel} zajęty: {choice.StationName}. Naciśnij ponownie {choice.SpokenShortcutLabel}, a następnie Enter, aby zastąpić; inny klawisz wybiera inne miejsce; Escape anuluje");
    }

    private void PrepareRemoval()
    {
        if (PresetList.SelectedItem is not RadioPresetChoice choice) return;
        if (choice.StationId is null)
        {
            AssignmentStatus.Announce($"Preset {choice.SlotLabel} jest już pusty");
            return;
        }
        _removePending = true;
        AssignmentStatus.Announce(
            $"Usunąć preset {choice.SlotLabel}: {choice.StationName}? Enter potwierdza, Escape anuluje");
    }

    private void Confirm()
    {
        if (PresetList.SelectedItem is not RadioPresetChoice choice) return;
        var replacesOtherStation = choice.StationId is not null
            && !string.Equals(choice.StationId, _stationId, StringComparison.Ordinal);
        if (!_removePending && replacesOtherStation && _replacementArmedSlot != choice.Slot)
        {
            AssignmentStatus.Announce(
                $"Preset {choice.SlotLabel} jest zajęty przez {choice.StationName}. Naciśnij dwa razy {choice.SpokenShortcutLabel}, a następnie Enter, aby zastąpić, albo Escape, aby anulować");
            return;
        }
        SelectedSlot = choice.Slot;
        SelectedAction = _removePending
            ? RadioPresetAssignmentAction.Remove
            : RadioPresetAssignmentAction.Save;
        DialogResult = true;
    }

    private void Save_Click(object sender, RoutedEventArgs e) => Confirm();

}

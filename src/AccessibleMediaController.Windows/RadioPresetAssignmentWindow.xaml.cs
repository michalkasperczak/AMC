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
    private readonly bool _shortcutOnly;
    private bool _removePending;
    private int? _lastRequestedSlot;
    private int? _replacementArmedSlot;

    public RadioPresetAssignmentWindow(
        string stationName,
        string stationId,
        IReadOnlyList<RadioPresetChoice> choices,
        int? firstFreeSlot,
        int initialSlot,
        string sessionName = "Radio internetowe",
        bool shortcutOnly = false)
    {
        InitializeComponent();
        Title = $"Przypisz preset — {sessionName}";
        _stationName = stationName;
        _stationId = stationId;
        _choices = choices;
        _shortcutOnly = shortcutOnly;
        if (shortcutOnly)
        {
            Title = $"Przypisz skrót — {sessionName}";
            System.Windows.Automation.AutomationProperties.SetName(PresetList, "Skróty AMC do presetów WiiM");
            System.Windows.Automation.AutomationProperties.SetHelpText(
                PresetList,
                "Naciśnij cyfrę, minus lub znak równości, aby wybrać skrót. Enter przypisuje lub zastępuje. Delete usuwa tylko przypisanie skrótu AMC. Escape anuluje.");
        }
        PresetList.ItemsSource = choices;
        PresetList.SelectedIndex = Math.Clamp(initialSlot - 1, 0, choices.Count - 1);
        DescriptionText.Text = shortcutOnly
            ? firstFreeSlot is int shortcutSlot
                ? $"Przypisz skrót AMC do gotowego presetu urządzenia: {stationName}. Pierwszy wolny skrót: Ctrl+Shift+{RadioPresetSlots.SpokenShortcutLabel(shortcutSlot)}. "
                  + "Naciśnij cyfrę, minus albo znak równości, a następnie Enter. Escape anuluje."
                : $"Przypisz skrót AMC do gotowego presetu urządzenia: {stationName}. Nie ma wolnego skrótu. "
                  + "Aby zastąpić zajęte przypisanie, wskaż dwukrotnie ten sam skrót i naciśnij Enter. Escape anuluje."
            : firstFreeSlot is int freeSlot
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
        if (!PresetList.IsKeyboardFocusWithin) return;
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
        var slotName = _shortcutOnly ? $"Skrót Ctrl+Shift+{choice.SpokenShortcutLabel}" : $"Preset {choice.SlotLabel}";
        AssignmentStatus.Announce(choice.StationId is null
            ? $"{slotName} pusty. {_stationName}. Enter zapisuje, Escape anuluje"
            : !replacesOtherStation
                ? $"{slotName} już zawiera ten element. Enter zatwierdza, Escape anuluje"
                : _replacementArmedSlot == slot
                    ? $"Potwierdzono {slotName}. Enter zastępuje element {choice.StationName} elementem {_stationName}, Escape anuluje"
                    : $"{slotName} zajęty: {choice.StationName}. Naciśnij ponownie {choice.SpokenShortcutLabel}, a następnie Enter, aby zastąpić; inny klawisz wybiera inne miejsce; Escape anuluje");
    }

    private void PrepareRemoval()
    {
        if (PresetList.SelectedItem is not RadioPresetChoice choice) return;
        if (choice.StationId is null)
        {
            AssignmentStatus.Announce(_shortcutOnly
                ? $"Skrót Ctrl+Shift+{choice.SpokenShortcutLabel} jest już pusty"
                : $"Preset {choice.SlotLabel} jest już pusty");
            return;
        }
        _removePending = true;
        AssignmentStatus.Announce(_shortcutOnly
            ? $"Usunąć przypisanie Ctrl+Shift+{choice.SpokenShortcutLabel}: {choice.StationName}? Preset w urządzeniu pozostanie bez zmian. Enter potwierdza, Escape anuluje"
            : $"Usunąć preset {choice.SlotLabel}: {choice.StationName}? Enter potwierdza, Escape anuluje");
    }

    private void Confirm()
    {
        if (PresetList.SelectedItem is not RadioPresetChoice choice) return;
        var replacesOtherStation = choice.StationId is not null
            && !string.Equals(choice.StationId, _stationId, StringComparison.Ordinal);
        if (!_removePending && replacesOtherStation && _replacementArmedSlot != choice.Slot)
        {
            AssignmentStatus.Announce(_shortcutOnly
                ? $"Skrót Ctrl+Shift+{choice.SpokenShortcutLabel} jest zajęty przez {choice.StationName}. Naciśnij dwa razy {choice.SpokenShortcutLabel}, a następnie Enter, aby zastąpić, albo Escape, aby anulować"
                : $"Preset {choice.SlotLabel} jest zajęty przez {choice.StationName}. Naciśnij dwa razy {choice.SpokenShortcutLabel}, a następnie Enter, aby zastąpić, albo Escape, aby anulować");
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

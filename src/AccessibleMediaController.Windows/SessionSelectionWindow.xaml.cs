using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AccessibleMediaController.Core.Sessions;

namespace AccessibleMediaController.Windows;

public partial class SessionSelectionWindow : Window
{
    private readonly SessionManager _sessions;
    private readonly ObservableCollection<SessionRow> _rows = [];

    public SessionSelectionWindow(SessionManager sessions)
    {
        InitializeComponent();
        _sessions = sessions;
        foreach (var pair in sessions.SessionSlots.OrderBy(pair => pair.Key))
        {
            _rows.Add(new SessionRow(
                pair.Key,
                pair.Value,
                sessions.Sessions.FirstOrDefault(session => session.Id == pair.Value)?.DisplayName ?? "nieprzypisana"));
        }
        SessionList.ItemsSource = _rows;
        var currentIndex = _rows.ToList().FindIndex(row =>
            string.Equals(row.SessionId, sessions.Current.Id, StringComparison.Ordinal));
        SessionList.SelectedIndex = currentIndex >= 0 ? currentIndex : 0;
        Loaded += (_, _) =>
        {
            SessionList.UpdateLayout();
            if (SessionList.ItemContainerGenerator.ContainerFromIndex(SessionList.SelectedIndex)
                is System.Windows.Controls.ListBoxItem item)
            {
                item.Focus();
                Keyboard.Focus(item);
                return;
            }
            SessionList.Focus();
        };
    }

    public int? SelectedSlot { get; private set; }
    public bool OrderChanged { get; private set; }
    public event EventHandler? OrderChangedCommitted;

    private void Select()
    {
        if (SessionList.SelectedItem is not SessionRow row) return;
        SelectedSlot = row.Slot;
        DialogResult = true;
    }

    private void Select_Click(object sender, RoutedEventArgs e) => Select();
    private void SessionList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => Select();

    public bool MoveSelectedSession(int direction)
    {
        if (SessionList.SelectedItem is not SessionRow selected) return false;
        var result = _sessions.MoveSessionSlot(selected.Slot, direction);
        if (!result.Moved)
        {
            SessionOrderStatus.Announce(direction < 0
                ? "Ta sesja jest już pierwsza"
                : "Ta sesja jest już ostatnia");
            return false;
        }

        var oldIndex = _rows.IndexOf(selected);
        var newIndex = oldIndex + direction;
        _rows.Move(oldIndex, newIndex);
        UpdateSlots();
        SessionList.SelectedIndex = newIndex;
        SessionList.ScrollIntoView(selected);
        FocusSelectedItem();
        OrderChanged = true;
        OrderChangedCommitted?.Invoke(this, EventArgs.Empty);
        SessionOrderStatus.Announce(
            $"Przeniesiono {result.SessionName} {(direction < 0 ? "nad" : "pod")} {result.NeighborName}. Ctrl+{result.Slot}");
        return true;
    }

    private void UpdateSlots()
    {
        for (var index = 0; index < _rows.Count; index++)
        {
            _rows[index].Slot = index + 1;
        }
    }

    private void FocusSelectedItem()
    {
        SessionList.UpdateLayout();
        if (SessionList.ItemContainerGenerator.ContainerFromItem(SessionList.SelectedItem)
            is ListBoxItem item)
        {
            item.Focus();
            Keyboard.Focus(item);
            return;
        }
        SessionList.Focus();
        Keyboard.Focus(SessionList);
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Handled || !SessionList.IsKeyboardFocusWithin) return;
        HandleAltArrow(e);
    }

    private void SessionList_PreviewKeyDown(object sender, KeyEventArgs e) => HandleAltArrow(e);

    private void HandleAltArrow(KeyEventArgs e)
    {
        if (!e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Alt)) return;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Up)
        {
            MoveSelectedSession(-1);
            e.Handled = true;
        }
        else if (key == Key.Down)
        {
            MoveSelectedSession(1);
            e.Handled = true;
        }
    }

    public sealed class SessionRow(int slot, string sessionId, string name) : INotifyPropertyChanged
    {
        private int _slot = slot;
        public int Slot
        {
            get => _slot;
            set
            {
                if (_slot == value) return;
                _slot = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Label));
            }
        }
        public string SessionId { get; } = sessionId;
        public string Name { get; } = name;
        public string Label => $"{Slot}, {Name}";
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        public override string ToString() => Label;
    }
}

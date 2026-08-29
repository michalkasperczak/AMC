using System.Windows;
using System.Windows.Input;
using AccessibleMediaController.Core.Sessions;

namespace AccessibleMediaController.Windows;

public partial class SessionSelectionWindow : Window
{
    public SessionSelectionWindow(SessionManager sessions)
    {
        InitializeComponent();
        var rows = sessions.SessionSlots
            .OrderBy(pair => pair.Key)
            .Select(pair => new SessionRow(
                pair.Key,
                pair.Value,
                sessions.Sessions.FirstOrDefault(session => session.Id == pair.Value)?.DisplayName ?? "nieprzypisana"))
            .ToList();
        SessionList.ItemsSource = rows;
        var currentIndex = rows.FindIndex(row =>
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

    private void Select()
    {
        if (SessionList.SelectedItem is not SessionRow row) return;
        SelectedSlot = row.Slot;
        DialogResult = true;
    }

    private void Select_Click(object sender, RoutedEventArgs e) => Select();
    private void SessionList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => Select();

    private sealed record SessionRow(int Slot, string SessionId, string Name)
    {
        public string Label => $"{Slot}, {Name}";
        public override string ToString() => Label;
    }
}

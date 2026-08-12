using System.Windows;
using System.Windows.Input;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sessions;

namespace AccessibleMediaController.Windows;

public partial class SessionSelectionWindow : Window
{
    public SessionSelectionWindow(SessionManager sessions, AppSettings settings)
    {
        InitializeComponent();
        SessionList.ItemsSource = settings.SessionSlots
            .OrderBy(pair => pair.Key)
            .Select(pair => new SessionRow(
                pair.Key,
                sessions.Sessions.FirstOrDefault(session => session.Id == pair.Value)?.DisplayName ?? "nieprzypisana"))
            .ToList();
        SessionList.SelectedIndex = 0;
        Loaded += (_, _) => SessionList.Focus();
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

    private sealed record SessionRow(int Slot, string Name)
    {
        public string Label => $"{Slot}, {Name}";
        public override string ToString() => Label;
    }
}

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using AccessibleMediaController.Core.Podcasts;

namespace AccessibleMediaController.Windows;

public partial class PodcastOpmlImportWindow : Window
{
    private readonly IReadOnlyList<PodcastOpmlChoice> _choices;

    public PodcastOpmlImportWindow(IReadOnlyList<PodcastOpmlEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        InitializeComponent();
        Title = "Importuj podcasty z OPML";
        _choices = entries.Select(entry => new PodcastOpmlChoice(entry)).ToArray();
        FeedsList.ItemsSource = _choices;
        ImportStatus.Text = SelectionSummary();
        Loaded += (_, _) => Dispatcher.BeginInvoke(FocusFirstEntry, DispatcherPriority.ContextIdle);
    }

    public IReadOnlyList<PodcastOpmlEntry> SelectedEntries =>
        _choices.Where(choice => choice.IsIncluded).Select(choice => choice.Entry).ToArray();

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedEntries.Count == 0)
        {
            MessageBox.Show(this, "Zaznacz co najmniej jeden kanał.", Title, MessageBoxButton.OK, MessageBoxImage.Information);
            FocusFirstEntry();
            return;
        }
        DialogResult = true;
    }

    private void FocusFirstEntry()
    {
        if (FeedsList.Items.Count > 0 && FeedsList.SelectedIndex < 0) FeedsList.SelectedIndex = 0;
        if (FeedsList.Items.Count > 0) FeedsList.ScrollIntoView(FeedsList.Items[0]);
        FeedsList.UpdateLayout();
        if (FeedsList.ItemContainerGenerator.ContainerFromIndex(0) is ListBoxItem item)
        {
            item.Focus();
            Keyboard.Focus(item);
            return;
        }
        FeedsList.Focus();
        Keyboard.Focus(FeedsList);
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!FeedsList.IsKeyboardFocusWithin) return;
        if (e.Key == Key.Space && Keyboard.Modifiers == ModifierKeys.None)
        {
            e.Handled = ToggleCurrentEntry();
        }
        else if (e.Key == Key.A && Keyboard.Modifiers == ModifierKeys.Control)
        {
            SelectAllEntries();
            e.Handled = true;
        }
    }

    private void FeedsList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => ToggleCurrentEntry();

    internal bool ToggleCurrentEntry()
    {
        if (FeedsList.SelectedItem is not PodcastOpmlChoice choice) return false;
        choice.IsIncluded = !choice.IsIncluded;
        ImportStatus.Announce($"{choice.Entry.Title}: {(choice.IsIncluded ? "zaznaczony do importu" : "odznaczony")}. {SelectionSummary()}");
        return true;
    }

    internal void SelectAllEntries()
    {
        foreach (var choice in _choices) choice.IsIncluded = true;
        ImportStatus.Announce(SelectionSummary());
    }

    private string SelectionSummary() =>
        $"Wybrano podcasty: {_choices.Count(choice => choice.IsIncluded)} z {_choices.Count}.";

    private sealed class PodcastOpmlChoice(PodcastOpmlEntry entry) : INotifyPropertyChanged
    {
        private bool _isIncluded = true;

        public event PropertyChangedEventHandler? PropertyChanged;

        public PodcastOpmlEntry Entry { get; } = entry;
        public string Label => Entry.Label;
        public string NavigationText => Entry.Title;
        public string AccessibleLabel => $"{Label}, {(_isIncluded ? "zaznaczony do importu" : "niezaznaczony")}";
        public bool IsIncluded
        {
            get => _isIncluded;
            set
            {
                if (_isIncluded == value) return;
                _isIncluded = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AccessibleLabel));
            }
        }

        public override string ToString() => AccessibleLabel;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

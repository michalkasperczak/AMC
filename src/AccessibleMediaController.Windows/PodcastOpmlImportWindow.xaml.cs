using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using AccessibleMediaController.Core.Podcasts;

namespace AccessibleMediaController.Windows;

public partial class PodcastOpmlImportWindow : Window
{
    public PodcastOpmlImportWindow(IReadOnlyList<PodcastOpmlEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        InitializeComponent();
        Title = "Importuj podcasty z OPML";
        FeedsList.ItemsSource = entries;
        Loaded += (_, _) => Dispatcher.BeginInvoke(FocusFirstEntry, DispatcherPriority.ContextIdle);
    }

    public IReadOnlyList<PodcastOpmlEntry> SelectedEntries =>
        FeedsList.SelectedItems.OfType<PodcastOpmlEntry>().ToArray();

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
        if (FeedsList.Items.Count > 0 && FeedsList.SelectedItems.Count == 0) FeedsList.SelectAll();
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
        if (e.Key == Key.A && Keyboard.Modifiers == ModifierKeys.Control && FeedsList.IsKeyboardFocusWithin)
        {
            FeedsList.SelectAll();
            e.Handled = true;
        }
    }
}

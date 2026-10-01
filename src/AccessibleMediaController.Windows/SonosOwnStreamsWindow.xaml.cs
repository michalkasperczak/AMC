using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sonos;
using AccessibleMediaController.Windows.Services;

namespace AccessibleMediaController.Windows;

public partial class SonosOwnStreamsWindow : Window
{
    internal sealed record PlayRequest(SonosOwnStreamsWindow Origin, SonosOwnStreamSettings Station,
        CancellationToken Lifetime);
    private readonly ObservableCollection<SonosOwnStreamSettings> _rows;
    private readonly Action<IReadOnlyList<SonosOwnStreamSettings>> _save;
    private readonly Func<PlayRequest, Task>? _play;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly string? _groupName;
    private bool _closed;
    private bool _busy;
    private int _feedback;

    internal SonosOwnStreamsWindow(IEnumerable<SonosOwnStreamSettings> stations, string? groupName,
        Action<IReadOnlyList<SonosOwnStreamSettings>> save, Func<PlayRequest, Task>? play)
    {
        InitializeComponent();
        _rows = new(stations.Select(Copy));
        _save = save;
        _play = play;
        _groupName = groupName;
        IntroductionText.Text = "Stacje zapisane tylko w AMC, nie w Ulubionych Sonosa. Zapis nie uruchamia muzyki. "
            + (string.IsNullOrWhiteSpace(groupName) ? "Aby odtwarzać, zamknij to okno i wybierz cel przez Control F5."
                : $"Enter lub Odtwórz wysyła stację do: {groupName}.");
        StationsList.ItemsSource = _rows;
        if (_rows.Count > 0) StationsList.SelectedIndex = 0;
        StationsList.SelectionChanged += (_, _) => UpdateButtons();
        Loaded += (_, _) => { UpdateButtons(); FocusRow(); };
        Closed += (_, _) => { _closed = true; _lifetime.Cancel(); _lifetime.Dispose(); };
        StatusText.Text = _rows.Count == 0 ? "Brak własnych stacji. Wybierz Dodaj." : string.Empty;
    }

    internal bool IsLiveOwnerTarget => !_closed && IsVisible;
    internal Task? LastPlayTaskForTests { get; private set; }
    internal string StatusForTests => StatusText.Text;
    private SonosOwnStreamSettings? Selected => StationsList.SelectedItem as SonosOwnStreamSettings;
    internal static SonosOwnStreamSettings Copy(SonosOwnStreamSettings station) => new()
        { Id = station.Id, Name = station.Name, StreamUrl = station.StreamUrl };

    internal void AnnounceForOwner(string message)
    {
        if (_closed) return;
        _feedback++;
        if (IsActive) StatusText.Announce(message);
        else StatusText.Text = message;
    }

    private void UpdateButtons()
    {
        // Nie wyłączamy skupionego przycisku podczas oczekiwania; bramka blokuje ponowienie.
        PlayButton.IsEnabled = Selected is not null && _play is not null && !string.IsNullOrWhiteSpace(_groupName);
        EditButton.IsEnabled = RemoveButton.IsEnabled = Selected is not null && !_busy;
        AddButton.IsEnabled = !_busy;
    }

    private void FocusRow()
    {
        StationsList.UpdateLayout();
        if (Selected is not null)
        {
            StationsList.ScrollIntoView(Selected);
            StationsList.UpdateLayout();
            if (StationsList.ItemContainerGenerator.ContainerFromItem(Selected) is ListBoxItem row)
            { row.Focus(); Keyboard.Focus(row); return; }
        }
        AddButton.Focus();
    }

    private void Add_Click(object sender, RoutedEventArgs e) => EditStation(null);
    private void Edit_Click(object sender, RoutedEventArgs e) { if (Selected is { } row) EditStation(row); }
    private void EditStation(SonosOwnStreamSettings? row)
    {
        if (_busy || _closed) return;
        var editor = new RadioStationWindow(row?.Name, row?.StreamUrl, false, true) { Owner = this };
        if (editor.ShowDialog() != true)
        { AnnounceForOwner("Bez zmian na liście stacji."); return; }
        var entry = new SonosOwnStreamSettings
        {
            Id = row?.Id ?? Guid.NewGuid().ToString("N"), Name = editor.StationName, StreamUrl = editor.StreamUrl
        };
        var index = row is null ? _rows.Count : _rows.IndexOf(row);
        if (row is null) _rows.Add(entry); else _rows[index] = entry;
        StationsList.SelectedItem = entry;
        _save(_rows.Select(Copy).ToArray());
        FocusRow();
        AnnounceForOwner(row is null ? $"Dodano stację: {entry.Name}." : $"Zmieniono stację: {entry.Name}.");
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _closed || Selected is not { } row) return;
        var result = AccessibleDialog.Show($"Usunąć stację „{row.Name}” z listy AMC? "
            + "Nie zmieni to odtwarzania ani Ulubionych Sonosa.", "Usuń własną stację",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) { AnnounceForOwner("Stacja pozostaje na liście."); return; }
        var index = _rows.IndexOf(row);
        _rows.Remove(row);
        StationsList.SelectedIndex = _rows.Count == 0 ? -1 : Math.Min(index, _rows.Count - 1);
        _save(_rows.Select(Copy).ToArray());
        FocusRow();
        AnnounceForOwner($"Usunięto stację: {row.Name}.");
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Handled) return;
        if (e.Key == Key.Escape) { e.Handled = true; Close(); return; }
        if (e.Key == Key.Enter && StationsList.IsKeyboardFocusWithin)
        { e.Handled = true; if (!e.IsRepeat) StartPlay(); }
    }
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void Play_Click(object sender, RoutedEventArgs e) => StartPlay();
    private void StartPlay()
    {
        if (_closed) return;
        if (_busy) { AnnounceForOwner("Polecenie jest już w toku."); return; }
        if (Selected is not { } row) { AnnounceForOwner("Najpierw dodaj i wybierz stację."); return; }
        if (string.IsNullOrWhiteSpace(_groupName)) { AnnounceForOwner(SonosPlaylistsLabels.PlayNeedsGroup); return; }
        if (_play is null) { AnnounceForOwner("To połączenie nie obsługuje własnych stacji."); return; }
        LastPlayTaskForTests = RunPlayAsync(Copy(row));
    }
    private async Task RunPlayAsync(SonosOwnStreamSettings row)
    {
        _busy = true;
        UpdateButtons();
        try
        {
            var feedback = _feedback;
            var task = _play!(new(this, row, _lifetime.Token));
            if (!task.IsCompleted && feedback == _feedback)
                AnnounceForOwner("Wysyłam polecenie uruchomienia stacji. Czekaj.");
            await task.ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        { AnnounceForOwner("Próbę przerwano. Jeśli wysłano polecenie, jego skutek może być nieznany."); }
        catch (Exception)
        { AnnounceForOwner("Nie udało się zakończyć próby. Sprawdź stan Sonosa przed ponowieniem."); }
        finally { _busy = false; if (!_closed) UpdateButtons(); }
    }
}

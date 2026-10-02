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

    /// <summary>Callback PRZYPISANIA PRESETU albo null. Okno nie zapisuje presetu samo.</summary>
    private readonly Action<SonosOwnStreamSettings>? _assignPreset;

    /// <summary>
    /// IMPORT PLAYLISTY albo null. Okno NIE czyta pliku i NIE zapisuje stanu:
    /// oddaje to wlascicielowi (<c>MainWindow.SonosImport.cs</c>), zeby istniala
    /// JEDNA droga importu dla menu Plik i dla tego przycisku.
    ///
    /// Zwrocony wynik z <c>Stations</c> rownym null znaczy "nic nie zmieniono" -
    /// wtedy lista zostaje DOKLADNIE taka, jaka byla, razem z zaznaczeniem.
    /// </summary>
    internal Func<MainWindow.SonosOwnStreamsImportUiOutcome>? ImportPlaylist { get; set; }

    internal SonosOwnStreamsWindow(IEnumerable<SonosOwnStreamSettings> stations, string? groupName,
        Action<IReadOnlyList<SonosOwnStreamSettings>> save, Func<PlayRequest, Task>? play)
        : this(stations, groupName, save, play, assignPreset: null)
    {
    }

    /// <summary>WARIANT Z PRZYPISYWANIEM PRESETU; istniejace wywolania nietkniete.</summary>
    internal SonosOwnStreamsWindow(IEnumerable<SonosOwnStreamSettings> stations, string? groupName,
        Action<IReadOnlyList<SonosOwnStreamSettings>> save, Func<PlayRequest, Task>? play,
        Action<SonosOwnStreamSettings>? assignPreset)
    {
        InitializeComponent();
        _rows = new(stations.Select(Copy));
        _save = save;
        _play = play;
        _assignPreset = assignPreset;
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

    /// <summary>
    /// PRZYWROCENIE ZAZNACZENIA po powrocie z innej sesji (Ctrl+cyfra tam i z
    /// powrotem). Szukamy po IDENTYFIKATORZE, nie po indeksie: lista mogla sie w
    /// miedzyczasie zmienic, a indeks wskazalby wtedy CZYJS INNY material.
    ///
    /// Nieznany identyfikator ladnie spada na pierwszy wiersz - to zachowanie
    /// opisuje <c>SonosSublistReturnPolicy.ResolveRowIndex</c> i jest tam mierzone.
    /// </summary>
    internal void RestoreSelectedRow(string? stationId)
    {
        var index = SonosSublistReturnPolicy.ResolveRowIndex(
            _rows.Select(row => row.Id).ToArray(), stationId);
        if (index < 0) return;
        StationsList.SelectedIndex = index;
        StationsList.UpdateLayout();
        FocusRow();
    }

    internal IReadOnlyList<string> RowLabelsForTests =>
        _rows.Select(row => string.IsNullOrWhiteSpace(row.Name) ? row.StreamUrl : row.Name).ToArray();

    internal string? HighlightedStationIdForTests => Selected?.Id;

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
        // Import nie wymaga zaznaczenia ani celu: to dopisanie do listy lokalnej.
        ImportButton.IsEnabled = ImportPlaylist is not null && !_busy;
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

    private void Remove_Click(object sender, RoutedEventArgs e) => RemoveSelectedStation();

    private void Import_Click(object sender, RoutedEventArgs e) => RunImport();

    /// <summary>
    /// JEDNA sciezka importu dla przycisku Importuj i dla Ctrl+O.
    ///
    /// Po udanym imporcie przeladowujemy liste PELNYM wynikiem scalenia (stare +
    /// dodane) i PRZYWRACAMY zaznaczenie - na pierwszej dodanej stacji, gdy
    /// cokolwiek przyszlo, a inaczej na tej samej stacji co przed importem.
    /// Przy anulowaniu, bledzie pliku i braku nowych stacji nie dotykamy ani
    /// wierszy, ani zaznaczenia; leci tylko komunikat.
    /// </summary>
    private void RunImport()
    {
        if (_busy || _closed) return;
        if (ImportPlaylist is null) { AnnounceForOwner("Tu nie można zaimportować playlisty."); return; }
        var previousId = Selected?.Id;
        var outcome = ImportPlaylist();
        if (outcome.Stations is null) { AnnounceForOwner(outcome.Message); return; }
        _rows.Clear();
        foreach (var station in outcome.Stations) _rows.Add(Copy(station));
        // Zaznaczenie po IDENTYFIKATORZE, nie po indeksie: lista wlasnie urosla.
        RestoreSelectedRow(outcome.FirstAddedId ?? previousId);
        UpdateButtons();
        AnnounceForOwner(outcome.Message);
    }

    /// <summary>
    /// JEDNA sciezka usuwania dla przycisku Usun i dla klawisza Delete - zeby nie
    /// istnialy dwie kopie potwierdzenia i zapisu.
    /// </summary>
    private void RemoveSelectedStation()
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
        // CTRL+CYFRA: PRZELACZENIE SESJI BEZ RECZNEGO ZAMYKANIA LISTY.
        //
        // Modal wylacza okno glowne, wiec jego router skrotow nie dostanie tego
        // gestu - przechwytujemy go tu i oddajemy wlascicielowi, tak samo jak
        // Ctrl+Alt+Shift+P nizej. Wlasciciel zapamieta TE liste i TEN wiersz,
        // zeby powrot Ctrl+cyfra wrocil tutaj, a nie do korzenia sesji.
        if (SonosSublistSessionSwitch.TryHandle(
            this,
            e,
            SonosLibraryPresentation.OwnStreamsCategoryId,
            () => Selected?.Id))
        {
            return;
        }

        if (e.Key == Key.Escape)
        {
            // SWIADOME wyjscie uzytkownika: zadne zadanie powrotu nie zostaje,
            // bo nastepne wejscie w sesje nie ma otwierac tej listy od nowa.
            (Owner as MainWindow)?.ClearSonosSublistReturn();
            e.Handled = true;
            Close();
            return;
        }
        // Z ALTEM WPF podaje Key.System, a litera siedzi w SystemKey.
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.P
            && (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift))
                == (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift))
        {
            // PRZYPISANIE PRESETU: glowny Owner jest wylaczony, przechwytujemy tu.
            e.Handled = true;
            if (!e.IsRepeat) RequestPresetAssignment();
            return;
        }
        // CTRL+O IMPORTUJE - ten sam gest, co w sesji Radia. Modal wylacza okno
        // glowne, wiec jego router skrotow tu nie dojdzie i gest trzeba obsluzyc
        // na miejscu. Importuje tez z pola listy i spod przyciskow.
        if (key == Key.O
            && (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift))
                == ModifierKeys.Control)
        {
            e.Handled = true;
            if (!e.IsRepeat) RunImport();
            return;
        }
        // F2 EDYTUJE, DELETE USUWA - na liscie wlasnych stacji.
        //
        // ZGLOSZENIE MICHALA: "Ulubione stacje graja, ale F2 nie edytuje i Delete
        // nie usuwa". Okno mialo TYLKO przyciski Zmien/Usun, a tych dwoch klawiszy
        // nie bylo wcale - wiec nie dzialo sie nic, bez zadnego komunikatu.
        //
        // To sa WLASNE stacje AMC (lista lokalna), wiec edycja i usuniecie naleza
        // do nas i nie dotykaja Ulubionych Sonosa. Oba klawisze wchodza w TE SAME
        // sciezki, co przyciski - bez drugiej kopii logiki zapisu.
        //
        // Wymagamy fokusu NA LISCIE: w polu tekstowym edytora Delete musi dalej
        // kasowac znaki, a nie stacje.
        if (StationsList.IsKeyboardFocusWithin && (Keyboard.Modifiers & ~ModifierKeys.None) == ModifierKeys.None)
        {
            if (key == Key.F2)
            {
                e.Handled = true;
                if (e.IsRepeat) return;
                if (Selected is { } toEdit) EditStation(toEdit);
                else AnnounceForOwner("Najpierw dodaj i wybierz stację.");
                return;
            }
            if (key == Key.Delete)
            {
                e.Handled = true;
                if (e.IsRepeat) return;
                if (Selected is not null) RemoveSelectedStation();
                else AnnounceForOwner("Najpierw dodaj i wybierz stację.");
                return;
            }
        }
        if (e.Key == Key.Enter && StationsList.IsKeyboardFocusWithin)
        { e.Handled = true; if (!e.IsRepeat) StartPlay(); }
    }

    /// <summary>
    /// PRZYPISANIE PRESETU: oddajemy LOKALNY identyfikator stacji. Adres NIE idzie
    /// do presetu - przy uruchomieniu pobierzemy go z AKTUALNEGO wpisu, wiec
    /// edycja adresu nie zostawia zamrozonego URL.
    /// </summary>
    private void RequestPresetAssignment()
    {
        if (_closed) return;
        if (_assignPreset is null) { AnnounceForOwner("Tu nie można przypisać presetu."); return; }
        if (Selected is not { } row) { AnnounceForOwner("Najpierw dodaj i wybierz stację."); return; }
        _assignPreset(Copy(row));
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

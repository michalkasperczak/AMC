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
    /// KOLEJNOSC I TRYB SORTOWANIA z ISTNIEJACEGO magazynu wlasciciela. Okno samo
    /// nie zapisuje stanu na dysk: zmienia te obiekty i wola <see cref="_saveOrder"/>,
    /// czyli TE SAMA kolejke zapisu, ktorej uzywa reszta listy.
    /// </summary>
    private readonly CollectionOrderSettings? _orders;

    private readonly Func<CollectionSortMode>? _readMode;
    private readonly Action<CollectionSortMode>? _writeMode;
    private readonly Action? _saveOrder;

    /// <summary>
    /// WYCIETE stacje czekajace na Ctrl+V. Ctrl+X NIE usuwa i NIE przestawia -
    /// zapamietuje tylko identyfikatory, wiec zamkniecie okna nic nie przenosi.
    /// To NIE jest schowek Windows: adresy nie opuszczaja aplikacji.
    /// </summary>
    private List<string>? _pendingMove;

    /// <summary>
    /// IMPORT PLAYLISTY albo null. Okno NIE czyta pliku i NIE zapisuje stanu:
    /// oddaje to wlascicielowi (<c>MainWindow.SonosImport.cs</c>), zeby istniala
    /// JEDNA droga importu dla menu Plik i dla tego przycisku.
    ///
    /// Zwrocony wynik z <c>Stations</c> rownym null znaczy "nic nie zmieniono" -
    /// wtedy lista zostaje DOKLADNIE taka, jaka byla, razem z zaznaczeniem.
    /// </summary>
    internal Func<MainWindow.SonosOwnStreamsImportUiOutcome>? ImportPlaylist { get; set; }

    /// <summary>
    /// ODCZYT PARAMETRÓW STACJI (Strzalka w lewo) albo null. Okno NIE ma wlasnej
    /// sondy ani wlasnego formatera: wlasciciel oddaje TE SAMA droge, ktorej
    /// uzywa radio internetowe. Bez tego callbacku gest mowi krotka odmowe.
    /// </summary>
    internal Func<SonosOwnStreamSettings, Task<string>>? DescribeStation
    {
        get => _describeStation;
        set { _describeStation = value; _describedParameters.Clear(); }
    }

    private Func<SonosOwnStreamSettings, Task<string>>? _describeStation;

    /// <summary>
    /// ZAPAMIETANE PARAMETRY po identyfikatorze stacji. Powtorzony gest na tej
    /// samej stacji NIE pyta sieci drugi raz - inaczej przytrzymana strzalka
    /// zasypywalaby serwer stacji zadaniami.
    /// </summary>
    private readonly Dictionary<string, string> _describedParameters = new(StringComparer.Ordinal);

    /// <summary>
    /// ZADANIE W LOCIE dla JEDNEJ stacji. Serie szybkich Strzalek w lewo na tym
    /// samym wierszu WSPOLDZIELA jeden odczyt sieci zamiast otwierac kolejny -
    /// zapamietany wynik pojawia sie dopiero PO zakonczeniu, wiec bez tego kazde
    /// nacisniecie szloby osobno do serwera stacji.
    /// </summary>
    private (string Id, Task<string> Read)? _describeInFlight;

    private int _describeRequest;

    internal SonosOwnStreamsWindow(IEnumerable<SonosOwnStreamSettings> stations, string? groupName,
        Action<IReadOnlyList<SonosOwnStreamSettings>> save, Func<PlayRequest, Task>? play)
        : this(stations, groupName, save, play, assignPreset: null)
    {
    }

    /// <summary>WARIANT Z PRZYPISYWANIEM PRESETU; istniejace wywolania nietkniete.</summary>
    internal SonosOwnStreamsWindow(IEnumerable<SonosOwnStreamSettings> stations, string? groupName,
        Action<IReadOnlyList<SonosOwnStreamSettings>> save, Func<PlayRequest, Task>? play,
        Action<SonosOwnStreamSettings>? assignPreset)
        : this(stations, groupName, save, play, assignPreset, null, null, null, null)
    {
    }

    /// <summary>
    /// WARIANT Z SORTOWANIEM I KOLEJNOSCIA WLASNA (Alt+1/2/3, Alt+strzalki,
    /// Ctrl+X/Ctrl+V). Wszystkie wczesniejsze wywolania dzialaja bez zmian: bez
    /// tych czterech zalezności okno zachowuje sie dokladnie jak dotad, a gesty
    /// kolejnosci mowia krotka odmowe zamiast cicho nic nie robic.
    /// </summary>
    internal SonosOwnStreamsWindow(IEnumerable<SonosOwnStreamSettings> stations, string? groupName,
        Action<IReadOnlyList<SonosOwnStreamSettings>> save, Func<PlayRequest, Task>? play,
        Action<SonosOwnStreamSettings>? assignPreset,
        CollectionOrderSettings? orders,
        Func<CollectionSortMode>? readMode,
        Action<CollectionSortMode>? writeMode,
        Action? saveOrder)
    {
        InitializeComponent();
        _save = save;
        _play = play;
        _assignPreset = assignPreset;
        _groupName = groupName;
        _orders = orders;
        _readMode = readMode;
        _writeMode = writeMode;
        _saveOrder = saveOrder;
        // KOLEJNOSC NA STARCIE z ZAPISANEGO trybu. Brak zapisu znaczy "zostaw tak,
        // jak bylo": stare ustawienia nie zostaja przetasowane bez gestu.
        var initial = stations.Select(Copy).ToList();
        if (_orders is not null)
        {
            // Chronologia dodania musi powstac PRZED pierwszym sortowaniem, zeby
            // przyjela DOTYCHCZASOWA kolejnosc listy, a nie wynik Alt+2.
            SonosOwnStreamsOrder.EnsureAddedOrder(_orders, initial);
            SonosOwnStreamsOrder.EnsureCustomOrder(_orders, initial);
            initial = SonosOwnStreamsOrder
                .Arrange(_orders, initial, _readMode?.Invoke() ?? CollectionSortMode.Custom)
                .ToList();
        }

        _rows = new(initial);
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

    /// <summary>
    /// RUTYNOWY POSTEP: WIDOCZNY status bez notyfikacji czytnika. Doslownie
    /// zgloszone: "jak klikamy w stację to niepotrzebnie przekazuje taki
    /// komunikat ... nawet nie kończy". Wynik, odmowa i blad zostaja
    /// <see cref="AnnounceForOwner"/>, wiec licznika <c>_feedback</c> tu NIE
    /// ruszamy - nic nie zostalo oglOSZONE wlascicielowi.
    /// </summary>
    private void ShowProgress(string message)
    {
        if (_closed) return;
        StatusText.ShowProgress(message);
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
        CommitStation(row, entry);
        AnnounceForOwner(row is null ? $"Dodano stację: {entry.Name}." : $"Zmieniono stację: {entry.Name}.");
    }

    /// <summary>
    /// JEDNA droga zapisu dodanej albo zmienionej stacji.
    ///
    /// DODANIE PRZY AKTYWNYM SORTOWANIU: samo dopisanie na koniec zostawialo
    /// liste, ktora przestawala byc tym, co obiecuje tryb - nowa stacja ladowala
    /// pod spodem, choc alfabetycznie nalezala wyzej. Dlatego po zapisie
    /// PRZEBUDOWUJEMY wiersze aktualna regula i przywracamy zaznaczenie PO
    /// IDENTYFIKATORZE. W kolejnosci wlasnej <c>Rebuild</c> zachowuje miejsce
    /// nowego wpisu, wiec dopisanie na koniec nadal wyglada tak samo.
    /// </summary>
    private void CommitStation(SonosOwnStreamSettings? row, SonosOwnStreamSettings entry)
    {
        var index = row is null ? _rows.Count : _rows.IndexOf(row);
        if (row is null) _rows.Add(entry); else _rows[index] = entry;
        // ZMIANA ADRESU POD TYM SAMYM IDENTYFIKATOREM UNIEWAZNIA PARAMETRY.
        // Edycja zachowuje Id, wiec zapamietany opis NADAL by sie dopasowal i
        // Strzalka w lewo czytalaby parametry STAREGO strumienia jako nowe.
        // Zadanie w locie tez porzucamy - jego wynik dotyczy juz nieistniejacego
        // adresu.
        if (_describedParameters.Remove(entry.Id)) _describeRequest++;
        StationsList.SelectedItem = entry;
        _save(_rows.Select(Copy).ToArray());
        if (_orders is not null)
        {
            Rebuild(entry.Id);
            _saveOrder?.Invoke();
        }
        else
        {
            FocusRow();
        }
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
        // PRZY AKTYWNYM SORTOWANIU przebudowujemy regula, bo zaimportowane
        // stacje przyszly w kolejnosci pliku, a nie w tej, ktora obiecuje tryb.
        if (_orders is not null)
        {
            Rebuild(outcome.FirstAddedId ?? previousId);
            _saveOrder?.Invoke();
        }
        else
        {
            RestoreSelectedRow(outcome.FirstAddedId ?? previousId);
        }

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

        // TRANSPORT (Spacja) i PRESETY (Ctrl+Shift+cyfra) z wnetrza podlisty -
        // modal wylacza okno glowne, wiec jego router tych gestow nie dostaje.
        // Ta sama droga oddania wlascicielowi, co Ctrl+cyfra wyzej. Podlista
        // ZOSTAJE otwarta: wiersz i fokus maja sie nie zmienic.
        if (SonosSublistSessionSwitch.TryHandleTransportAndPresets(this, e))
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

        // ALT+1/2/3 SORTUJE, ALT+STRZALKI PRZESUWA - dokladnie jak w edycji radia
        // internetowego. Gesty NIE wysylaja zadnego zadania do Sonosa i NIE ruszaja
        // odtwarzania: zmienia sie wylacznie porzadek wierszy.
        if ((Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift))
            == ModifierKeys.Alt)
        {
            var mode = key switch
            {
                Key.D1 or Key.NumPad1 => (CollectionSortMode?)CollectionSortMode.AddedNewest,
                Key.D2 or Key.NumPad2 => CollectionSortMode.Alphabetical,
                Key.D3 or Key.NumPad3 => CollectionSortMode.Custom,
                _ => null
            };
            if (mode is { } requested)
            {
                e.Handled = true;
                if (!e.IsRepeat) ApplySortMode(requested);
                return;
            }

            if (key is Key.Up or Key.Down)
            {
                e.Handled = true;
                // AUTOREPEAT TU ZOSTAJE: przytrzymanie Alt+strzalki ma przesuwac
                // dalej, tak samo jak w oknie glownym.
                MoveSelection(key == Key.Up ? -1 : 1);
                return;
            }
        }

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
        // CTRL+X / CTRL+V PRZENOSZA W KOLEJNOSCI WLASNEJ - wzor z edycji radia.
        //
        // WAZNE: wymagamy fokusu NA LISCIE. W edytorze nazwy i adresu Ctrl+X oraz
        // Ctrl+V musza dalej wycinac i wklejac TEKST, a nie przestawiac stacje.
        // Nie dotykamy tez schowka Windows ani zadnego pliku: wycinamy wylacznie
        // POZYCJE na naszej liscie, a adresy nie opuszczaja aplikacji.
        if (StationsList.IsKeyboardFocusWithin
            && (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift))
                == ModifierKeys.Control
            && key is Key.X or Key.V)
        {
            e.Handled = true;
            if (e.IsRepeat) return;
            if (key == Key.X) StartInternalMove(); else PasteInternalMove();
            return;
        }

        // CTRL+C KOPIUJE NAZWE, CTRL+SHIFT+C SAM ADRES - dokladnie ten podzial, co
        // w sesji Radia. Modal wylacza okno glowne, wiec jego router tu nie dojdzie.
        // Wymagamy fokusu NA LISCIE: w edytorze nazwy i adresu Ctrl+C ma dalej
        // kopiowac TEKST.
        if (StationsList.IsKeyboardFocusWithin
            && key == Key.C
            && (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift))
                is ModifierKeys.Control or (ModifierKeys.Control | ModifierKeys.Shift))
        {
            e.Handled = true;
            if (e.IsRepeat) return;
            CopySelectedStation(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
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
            if (key == Key.Left)
            {
                // STRZALKA W LEWO ODCZYTUJE PARAMETRY - ten sam gest, co na
                // liscie radia internetowego. Nie otwiera okna wlasciwosci i nie
                // rusza odtwarzania.
                e.Handled = true;
                if (e.IsRepeat) return;
                if (Selected is { } toDescribe) AnnounceStationParameters(toDescribe);
                else AnnounceForOwner("Najpierw dodaj i wybierz stację.");
                return;
            }
        }
        if (e.Key == Key.Enter && StationsList.IsKeyboardFocusWithin)
        { e.Handled = true; if (!e.IsRepeat) StartPlay(); }
    }

    /// <summary>
    /// CTRL+C NAZWA, CTRL+SHIFT+C SAM ADRES - podzial wziety z sesji Radia, bez
    /// nowego skrotu do nauczenia. Adres jest kopiowany DOKLADNIE taki, jaki
    /// siedzi we wpisie: zadnej nazwy w linku, zadnego sklejania.
    /// </summary>
    private void CopySelectedStation(bool locationOnly)
    {
        if (_closed) return;
        if (Selected is not { } row) { AnnounceForOwner("Najpierw dodaj i wybierz stację."); return; }
        if (!locationOnly)
        {
            if (!ClipboardRetry.TrySetText(row.Name, out var nameError))
            { AnnounceForOwner(nameError); return; }
            AnnounceForOwner("Skopiowano nazwę");
            return;
        }

        if (string.IsNullOrWhiteSpace(row.StreamUrl))
        {
            // BRAK ADRESU NIE CZYSCI SCHOWKA i nie udaje sukcesu.
            AnnounceForOwner("Ta stacja nie ma zapisanego adresu");
            return;
        }

        if (!ClipboardRetry.TrySetText(row.StreamUrl.Trim(), out var locationError))
        { AnnounceForOwner(locationError); return; }
        AnnounceForOwner("Skopiowano adres");
    }

    /// <summary>
    /// STRZALKA W LEWO: PARAMETRY ZAZNACZONEJ STACJI.
    ///
    /// Odczyt idzie TA SAMA droga, co w radiu internetowym - wlasciciel podaje
    /// nam istniejaca sonde strumienia i istniejacy formater, wiec nie ma tu
    /// drugiego silnika ani zgadywanych wartosci. Sonda NIE uruchamia audio.
    ///
    /// ASYNCHRONICZNIE i z OCHRONA: wynik odrzucamy, gdy okno sie zamknelo albo
    /// zaznaczenie przeszlo na inna stacje, zeby parametry jednej stacji nigdy
    /// nie zostaly przypisane do drugiej. Powtorzony gest na tej samej stacji
    /// korzysta z zapamietanego wyniku i NIE pyta sieci po raz drugi.
    /// </summary>
    private async void AnnounceStationParameters(SonosOwnStreamSettings row)
    {
        if (_closed) return;
        if (_describeStation is null)
        { AnnounceForOwner("Tu nie można odczytać parametrów stacji."); return; }
        if (string.IsNullOrWhiteSpace(row.StreamUrl))
        { AnnounceForOwner("Ta stacja nie ma zapisanego adresu"); return; }

        if (_describedParameters.TryGetValue(row.Id, out var remembered))
        { AnnounceForOwner(remembered); return; }

        var request = ++_describeRequest;
        // JEDEN ODCZYT NA STACJE, takze gdy gest powtorzy sie PRZED koncem.
        // Zapamietanie wyniku nastepuje dopiero po await, wiec sam slownik nie
        // obroni sie przed druga i trzecia Strzalka w lewo w tym samym momencie.
        Task<string> read;
        if (_describeInFlight is { } pending
            && string.Equals(pending.Id, row.Id, StringComparison.Ordinal))
        {
            read = pending.Read;
        }
        else
        {
            read = _describeStation(Copy(row));
            _describeInFlight = (row.Id, read);
        }

        AnnounceForOwner("Czytam parametry stacji…");
        string description;
        try
        {
            description = await read.ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            // BLAD NAZWANY, nie cisza i nie udawana wartosc. Porzucamy tez
            // nieudane zadanie, zeby kolejny gest mogl sprobowac od nowa.
            if (_describeInFlight?.Read == read) _describeInFlight = null;
            if (_closed || request != _describeRequest) return;
            AnnounceForOwner("Nie udało się odczytać parametrów stacji: " + exception.Message);
            return;
        }

        if (_describeInFlight?.Read == read) _describeInFlight = null;
        if (_closed || request != _describeRequest) return;
        // ZAZNACZENIE MOGLO SIE ZMIENIC W CZASIE ODCZYTU - wtedy milczymy, zeby
        // nie opisac cudzej stacji.
        if (!string.Equals(Selected?.Id, row.Id, StringComparison.Ordinal)) return;
        _describedParameters[row.Id] = description;
        AnnounceForOwner(description);
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
    /// <summary>
    /// ALT+1/2/3: zmiana trybu sortowania. ZAPISUJEMY tryb, przebudowujemy wiersze
    /// i PRZYWRACAMY zaznaczenie PO IDENTYFIKATORZE - po zmianie porzadku indeks
    /// wskazywalby czyjs inny material. Zero zadan sieciowych, zero muzyki.
    /// </summary>
    private void ApplySortMode(CollectionSortMode mode)
    {
        if (_closed) return;
        if (_orders is null)
        {
            AnnounceForOwner("Tu nie można zmienić kolejności stacji.");
            return;
        }

        var selectedId = Selected?.Id;
        _writeMode?.Invoke(mode);
        Rebuild(selectedId);
        _saveOrder?.Invoke();
        // KROTKO: sam tryb i liczba stacji, bez technicznego sprawozdania.
        AnnounceForOwner($"{SonosOwnStreamsOrder.DescribeMode(mode)}. Stacji: {_rows.Count}.");
    }

    /// <summary>
    /// ALT+STRZALKI: przesuniecie zaznaczenia w KOLEJNOSCI WLASNEJ. W innym trybie
    /// przesuwanie nie ma sensu (porzadek wyliczamy z nazwy albo z chronologii),
    /// wiec mowimy KROTKA podpowiedz zamiast cicho nic nie robic.
    /// </summary>
    private void MoveSelection(int direction)
    {
        if (_closed) return;
        if (!RequireCustomOrder(out var orders)) return;
        var ids = SelectedIds();
        if (ids.Count == 0) { AnnounceForOwner("Najpierw wybierz stację."); return; }
        var result = SonosOwnStreamsOrder.Move(orders, Snapshot(), ids, direction);
        if (result != SonosOwnStreamsOrderResult.Moved)
        {
            AnnounceForOwner(DescribeRefusal(result, direction));
            return;
        }

        Rebuild(ids);
        _saveOrder?.Invoke();
        AnnounceMovedPosition(ids);
    }

    /// <summary>
    /// CTRL+X: ZAZNACZENIE DO PRZENIESIENIA. Nic sie jeszcze nie rusza i nic nie
    /// ginie - zapamietujemy tylko identyfikatory. Zamkniecie okna bez Ctrl+V nie
    /// przenosi niczego.
    /// </summary>
    private void StartInternalMove()
    {
        if (_closed) return;
        if (!RequireCustomOrder(out _)) return;
        var ids = SelectedIds();
        if (ids.Count == 0) { AnnounceForOwner("Najpierw wybierz stację."); return; }
        _pendingMove = ids.ToList();
        AnnounceForOwner(ids.Count == 1
            ? "Zaznaczono stację do przeniesienia. Wybierz miejsce i naciśnij Control V."
            : $"Zaznaczono {ids.Count} stacji do przeniesienia. "
                + "Wybierz miejsce i naciśnij Control V.");
    }

    /// <summary>
    /// CTRL+V: wstawienie wycietych stacji PRZED wierszem docelowym. Cel w swoim
    /// wlasnym zaznaczeniu, brak celu i ta sama pozycja maja ROZNE komunikaty, zeby
    /// zaden nie klamal o skutku. Stacja usunieta albo zmieniona miedzy Ctrl+X i
    /// Ctrl+V konczy sie odmowa, a nie przeniesieniem czegos innego.
    /// </summary>
    private void PasteInternalMove()
    {
        if (_closed) return;
        if (!RequireCustomOrder(out var orders)) return;
        if (_pendingMove is not { Count: > 0 } pending)
        {
            AnnounceForOwner("Najpierw naciśnij Control X na stacji do przeniesienia.");
            return;
        }

        var target = Selected?.Id;
        var result = SonosOwnStreamsOrder.PlaceBefore(orders, Snapshot(), pending, target);
        if (result != SonosOwnStreamsOrderResult.Moved)
        {
            AnnounceForOwner(result switch
            {
                SonosOwnStreamsOrderResult.TargetInSelection =>
                    "Cel jest wśród przenoszonych stacji. Wybierz inne miejsce.",
                SonosOwnStreamsOrderResult.TargetMissing =>
                    "Nie wybrano miejsca docelowego.",
                SonosOwnStreamsOrderResult.Unchanged =>
                    "Stacja już jest w tym miejscu.",
                _ => "Nie ma czego przenieść. Naciśnij Control X na stacji."
            });
            // Nieudane wklejenie NIE gubi zaznaczenia do przeniesienia poza
            // przypadkiem, gdy zrodlo zniknelo z listy.
            if (result == SonosOwnStreamsOrderResult.InvalidSelection) _pendingMove = null;
            return;
        }

        _pendingMove = null;
        Rebuild(pending);
        _saveOrder?.Invoke();
        AnnounceMovedPosition(pending);
    }

    /// <summary>Kolejnosc wlasna to WARUNEK przenoszenia - jak w oknie glownym.</summary>
    private bool RequireCustomOrder(out CollectionOrderSettings orders)
    {
        orders = _orders!;
        if (_orders is null)
        {
            AnnounceForOwner("Tu nie można zmienić kolejności stacji.");
            return false;
        }

        if ((_readMode?.Invoke() ?? CollectionSortMode.Custom) != CollectionSortMode.Custom)
        {
            AnnounceForOwner("Przenoszenie działa w kolejności własnej. Naciśnij Alt 3.");
            return false;
        }

        if (_rows.Count == 0) { AnnounceForOwner("Brak własnych stacji."); return false; }
        return true;
    }

    private string DescribeRefusal(SonosOwnStreamsOrderResult result, int direction) => result switch
    {
        SonosOwnStreamsOrderResult.Boundary => direction < 0
            ? "Już na początku listy."
            : "Już na końcu listy.",
        SonosOwnStreamsOrderResult.NonContiguousSelection =>
            "Zaznaczenie nie jest ciągłe. Wybierz sąsiadujące stacje.",
        _ => "Najpierw wybierz stację."
    };

    /// <summary>KROTKA WYPOWIEDZ po przeniesieniu: nazwa i pozycja, nic wiecej.</summary>
    private void AnnounceMovedPosition(IReadOnlyCollection<string> ids)
    {
        var first = _rows.FirstOrDefault(row => ids.Contains(row.Id));
        if (first is null) return;
        var position = _rows.IndexOf(first) + 1;
        AnnounceForOwner(ids.Count == 1
            ? $"{SonosOwnStreamsOrder.Label(first)}, pozycja {position} z {_rows.Count}."
            : $"Przeniesiono {ids.Count} stacji, od pozycji {position} z {_rows.Count}.");
    }

    private IReadOnlyList<SonosOwnStreamSettings> Snapshot() => _rows.Select(Copy).ToArray();

    private List<string> SelectedIds() => StationsList.SelectedItems
        .OfType<SonosOwnStreamSettings>()
        .Select(row => row.Id)
        .Where(id => !string.IsNullOrEmpty(id))
        .ToList();

    private void Rebuild(string? selectedId) =>
        Rebuild(selectedId is null ? [] : new[] { selectedId });

    /// <summary>
    /// PRZEBUDOWA WIERSZY z zapisanej kolejnosci + PRZYWROCENIE ZAZNACZENIA PO
    /// IDENTYFIKATORACH i FOKUS na pierwszym z nich.
    /// </summary>
    private void Rebuild(IReadOnlyCollection<string> selectedIds)
    {
        if (_orders is null) return;
        var arranged = SonosOwnStreamsOrder.Arrange(
            _orders, Snapshot(), _readMode?.Invoke() ?? CollectionSortMode.Custom);
        _rows.Clear();
        foreach (var station in arranged) _rows.Add(station);
        // POJEDYNCZY WYBOR: lista stacji jest w trybie Single, a w nim WPF
        // zabrania ruszac SelectedItems - i samo Clear() rzucalo wyjatkiem,
        // ktory wywracal okno przy KAZDYM Alt+1/2/3 i Alt+strzalce. Dlatego
        // czyscimy ta droga, ktora dany tryb dopuszcza.
        if (StationsList.SelectionMode == SelectionMode.Single)
        {
            StationsList.SelectedItem = _rows.FirstOrDefault(row => selectedIds.Contains(row.Id));
        }
        else
        {
            StationsList.SelectedItems.Clear();
            foreach (var row in _rows.Where(row => selectedIds.Contains(row.Id)))
            {
                StationsList.SelectedItems.Add(row);
            }
        }

        if (StationsList.SelectedItem is null && _rows.Count > 0) StationsList.SelectedIndex = 0;
        UpdateButtons();
        FocusRow();
    }

    /// <summary>ETYKIETY WIERSZY w AKTUALNEJ kolejnosci - kwit pomiaru sortowania.</summary>
    internal IReadOnlyList<string> OrderedLabelsForTests =>
        _rows.Select(SonosOwnStreamsOrder.Label).ToArray();

    /// <summary>Czy cos czeka na Ctrl+V - pomiar anulowania bez skutku.</summary>
    internal bool HasPendingMoveForTests => _pendingMove is { Count: > 0 };

    /// <summary>IDENTYFIKATORY w AKTUALNEJ kolejnosci - pomiar trwalosci ID.</summary>
    internal IReadOnlyList<string> RowIdsForTests => _rows.Select(x => x.Id).ToArray();

    /// <summary>
    /// DODANIE STACJI produkcyjna droga wiersza, z pominieciem MODALNEGO okna
    /// edytora (<c>RadioStationWindow</c>), ktorego w pomiarze nie ma kto obsluzyc.
    /// Caly skutek na liscie, zapisie i kolejnosci jest prawdziwy.
    /// </summary>
    internal void AppendStationForTests(SonosOwnStreamSettings entry) => CommitStation(null, entry);

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
                ShowProgress("Wysyłam polecenie uruchomienia stacji. Czekaj.");
            await task.ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        { AnnounceForOwner("Próbę przerwano. Jeśli wysłano polecenie, jego skutek może być nieznany."); }
        catch (Exception)
        { AnnounceForOwner("Nie udało się zakończyć próby. Sprawdź stan Sonosa przed ponowieniem."); }
        finally { _busy = false; if (!_closed) UpdateButtons(); }
    }
}

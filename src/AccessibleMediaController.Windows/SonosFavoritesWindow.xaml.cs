using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using AccessibleMediaController.Core.Sonos;

namespace AccessibleMediaController.Windows;

/// <summary>
/// MALE dostepne okno ULUBIONYCH Sonos. Organizacyjnie jak istniejace okno
/// presetow WiiM i okno wyboru domu: okno NIE zna konta, klienta HTTP ani
/// magazynu - dostaje GOTOWA, swieza liste, a uruchomienie oddaje ODEBRANYM
/// callbackiem.
///
/// DWA WARIANTY, oba mierzone:
///  * TYLKO PODGLAD (konstruktor bez callbacka): zadnego przycisku Odtworz,
///    Enter na liscie POCHLONIETY, ZERO POST. Uzywany, gdy zaplecze nie umie
///    ladowania ulubionego (opcjonalna granica nieobecna).
///  * Z URUCHAMIANIEM (F3c): przycisk Odtworz i Enter na liscie wolaja TEN SAM
///    callback, ktory na wyzszej warstwie idzie przez jedno wpiecie do
///    JEDNEGO POST. Okno samo nie zna ani grupy, ani domu, ani akcji kolejki.
///
/// Zasady dostepnosci, ktore tu obowiazuja bez wyjatku:
///  * CEL (aktywna grupa) czyta sie RAZ, w opisie okna - nie w nazwie kazdego
///    wiersza listy,
///  * strzalki, Tab i samo zaznaczenie NIE uruchamiaja niczego,
///  * powtorzenie klawisza (autorepeat) i przytrzymanie NIE dubluja proby:
///    <c>e.IsRepeat</c> konczy droge przed callbackiem,
///  * modal po probie ZOSTAJE otwarty, a zaznaczenie i fokus sa zachowane,
///  * Escape, Alt+F4 i Zamknij zamykaja okno i uniewazniaja WYLACZNIE lokalne
///    oczekiwanie - zadnego anulowania calej sesji Sonos.
///
/// Etykiety pochodza z bezpiecznego <see cref="SonosFavoritesLabels"/>:
/// identyfikator ulubionego, identyfikator uslugi ani dom NIE wyciekaja do
/// widoku - ale wiersz NIESIE typowany <see cref="SonosFavorite"/> wewnetrznie,
/// wiec uruchomienie nigdy nie szuka pozycji PO NAZWIE.
/// </summary>
public partial class SonosFavoritesWindow : Window
{
    private readonly ObservableCollection<FavoriteRow> _rows = [];

    /// <summary>
    /// ODEBRANY callback uruchomienia albo <c>null</c> w wariancie tylko do
    /// odczytu. Okno nie tworzy tu zadnej wlasnej drogi do sieci. Callback
    /// dostaje NIEZMIENNE <see cref="PlayRequest"/>, wiec wynik proby zna
    /// instancje, ktora go ZLECILA - i nie ma gdzie sie "odbic", gdy ta zniknie.
    /// </summary>
    private readonly Func<PlayRequest, Task>? _loadFavorite;

    /// <summary>
    /// LOKALNE oczekiwanie na wynik proby. Pilnuje JEDNEGO POST na akcje
    /// uzytkownika: przytrzymanie, autorepeat i drugie klikniecie odmawiaja.
    /// </summary>
    private bool _playInFlight;

    /// <summary>
    /// LOKALNE ZYCIE tego modalu. Zamkniecie anuluje WYLACZNIE oczekiwania
    /// zlecone przez TO okno - zadnego <c>CancelSonosPendingWork</c> calej sesji,
    /// zadnego kasowania konta, zadnego cofania polecenia, ktore JUZ poszlo.
    /// </summary>
    private readonly CancellationTokenSource _lifetime = new();

    /// <summary>
    /// Czy okno jest JUZ zamykane albo zamkniete. Po zamknieciu spozniony wynik
    /// NIE mowi, nie rusza fokusu i nie pisze statusu.
    /// </summary>
    private bool _closed;

    /// <summary>
    /// WARIANT TYLKO DO ODCZYTU. Zachowany dla zaplecza BEZ opcjonalnej granicy
    /// ladowania: podglad dziala, uruchamianie jest uczciwie nieobecne.
    /// </summary>
    internal SonosFavoritesWindow(IReadOnlyList<SonosFavorite> favorites)
        : this(favorites, groupName: null, loadFavorite: null)
    {
    }

    /// <summary>
    /// WARIANT Z URUCHAMIANIEM. <paramref name="groupName"/> to nazwa AKTYWNEJ
    /// grupy z ODCZYTU topologii (nigdy identyfikator i nigdy zgadnieta);
    /// <c>null</c> albo biale znaczy BRAK grupy - lista nadal sie czyta, a
    /// Odtworz jest niedostepne z wyjasnieniem.
    ///
    /// <paramref name="loadFavorite"/> <c>null</c> to wariant tylko do odczytu.
    /// Okno NIE wola niczego, czego nie dostalo.
    /// </summary>
    internal SonosFavoritesWindow(
        IReadOnlyList<SonosFavorite> favorites,
        string? groupName,
        Func<PlayRequest, Task>? loadFavorite)
    {
        ArgumentNullException.ThrowIfNull(favorites);
        InitializeComponent();

        _loadFavorite = loadFavorite;
        GroupNameForTests = groupName;

        // KOLEJNOSC API zachowana 1:1. Zadnego sortowania i zadnego scalania
        // rownych nazw - rozne identyfikatory to rozne pozycje. Wiersz niesie
        // TYPOWANY ulubiony, wiec dwie identyczne nazwy nadal daja dwa rozne
        // identyfikatory w POST.
        var labels = SonosFavoritesLabels.DescribeAll(favorites);
        for (var index = 0; index < favorites.Count; index++)
        {
            _rows.Add(new FavoriteRow(labels[index], favorites[index]));
        }

        FavoritesList.ItemsSource = _rows;

        // PIERWSZA tresc okna mowi, CO okno robi, i - gdy uruchamianie jest
        // dostepne - gdzie material poleci. RAZ, nie w kazdym wierszu.
        var canPlay = _loadFavorite is not null;
        var hasGroup = !string.IsNullOrWhiteSpace(groupName);
        var introduction = canPlay
            ? SonosFavoritesLabels.DescribePlayIntroduction(groupName)
            : SonosFavoritesLabels.ViewIntroduction;
        // PUSTY STAN mowi sie w PIERWSZEJ tresci okna, a nie udawanym wierszem.
        IntroductionText.Text = _rows.Count == 0
            ? SonosFavoritesLabels.EmptyState + " " + introduction
            : introduction;

        // PRZYCISK istnieje TYLKO w wariancie z uruchamianiem. Brak grupy albo
        // pusta lista nie usuwa przycisku, tylko go WYLACZA - nazwa i powod
        // zostaja do przeczytania.
        PlayButton.Visibility = canPlay ? Visibility.Visible : Visibility.Collapsed;
        PlayButton.IsEnabled = canPlay && hasGroup && _rows.Count > 0;
        if (canPlay && !hasGroup)
        {
            // WYJASNIENIE zamiast martwego przycisku bez powodu.
            AutomationProperties.SetHelpText(PlayButton, SonosFavoritesLabels.PlayNeedsGroup);
        }

        AutomationProperties.SetHelpText(FavoritesList, canPlay
            ? "Strzałki czytają kolejne ulubione. Enter albo przycisk Odtwórz uruchamia wybraną "
                + "pozycję w aktywnej grupie. Escape albo Zamknij kończy okno."
            : "Strzałki czytają kolejne ulubione. To tylko podgląd: nic tu nie uruchamia "
                + "odtwarzania. Escape albo Zamknij kończy podgląd.");

        if (_rows.Count > 0) FavoritesList.SelectedIndex = 0;

        Loaded += (_, _) => FocusInitialElement();
        // ZAMKNIECIE uniewaznia WYLACZNIE lokalne oczekiwanie tego okna. Zadnego
        // CancelSonosPendingWork calej sesji, zadnego kasowania konta.
        Closed += (_, _) =>
        {
            _closed = true;
            // ANULUJEMY WLASNE oczekiwanie: rzeczywisty klient honoruje token, a
            // zaplecze, ktore go ignoruje, i tak nie przemowi - sprawdza to
            // granica tozsamosci u zlecajacego.
            try
            {
                _lifetime.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        };
    }

    internal int RowCountForTests => _rows.Count;

    internal IReadOnlyList<string> RowLabelsForTests => _rows.Select(row => row.Label).ToArray();

    internal string IntroductionForTests => IntroductionText.Text;

    internal string StatusForTests => StatusText.Text;

    /// <summary>Nazwa grupy, jaka okno DOSTALO. Nie sklada jej i nie zgaduje.</summary>
    internal string? GroupNameForTests { get; }

    internal bool PlayEnabledForTests => PlayButton.IsEnabled;

    internal bool PlayVisibleForTests => PlayButton.Visibility == Visibility.Visible;

    internal int SelectedIndexForTests => FavoritesList.SelectedIndex;

    /// <summary>Ile razy okno WOLALO odebrany callback. Odmowa nie liczy sie.</summary>
    internal int LoadCallsForTests { get; private set; }

    /// <summary>OSTATNIE zadanie uruchomienia - pomiar ma na czym czekac.</summary>
    internal Task? LastPlayTaskForTests { get; private set; }

    /// <summary>
    /// Czy FOKUS naprawde jest na liscie (albo na jej wierszu). Pusty stan nie
    /// ma wiersza, wiec fokus idzie na sama liste - nadal do czytania strzalkami.
    /// </summary>
    internal bool ListHasFocusForTests =>
        FavoritesList.IsKeyboardFocusWithin || FavoritesList.IsKeyboardFocused;

    /// <summary>
    /// Czy przy TYM oknie wolno oczekiwac JAKIEJKOLWIEK drogi uruchomienia.
    /// Nie jest to staly fakt typu, tylko wlasnosc TEJ instancji: wariant bez
    /// callbacka NIE MA prawa niczego odtwarzac, a rzeczywiste okno z loadem nie
    /// ma prawa udawac, ze nie umie.
    /// </summary>
    internal bool OffersPlaybackForTests => _loadFavorite is not null;

    /// <summary>
    /// ZAMKNIECIE dziala i modalnie, i przy zwyklym Show. DialogResult poza
    /// ShowDialog rzuca, wiec wyjatek jest pochlaniany i okno zamyka sie jawnie.
    /// </summary>
    private void CloseSelf()
    {
        try
        {
            DialogResult = false;
            return;
        }
        catch (InvalidOperationException)
        {
        }

        Close();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => CloseSelf();

    /// <summary>KLIKNIECIE Odtworz: ta SAMA metoda co Enter na liscie.</summary>
    private void Play_Click(object sender, RoutedEventArgs e) => StartPlaySelected();

    /// <summary>
    /// Escape zamyka. ENTER na liscie uruchamia wybrana pozycje w wariancie z
    /// callbackiem, a w wariancie tylko do odczytu jest SWIADOMIE POCHLONIETY -
    /// bez tego poszedlby do domyslnego przycisku albo dalej w gore i uzytkownik
    /// moglby uznac, ze cos zostalo wlaczone. Tab i strzalki zostaja standardowe.
    /// </summary>
    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Handled) return;
        if (e.Key == Key.Escape)
        {
            CloseSelf();
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Enter || !FavoritesList.IsKeyboardFocusWithin) return;

        // KLAWISZ ZATRZYMANY w obu wariantach: Enter na liscie nie ma prawa
        // wpasc do domyslnego przycisku ani wyjsc z modalu.
        e.Handled = true;
        // AUTOPOWTARZANIE i przytrzymanie: JEDNA proba, nie seria POST.
        // Sprawdzamy to PRZED callbackiem, nie po nim.
        if (e.IsRepeat) return;
        StartPlaySelected();
    }

    /// <summary>
    /// JEDNA droga uruchomienia dla przycisku i dla Entera.
    ///
    /// Kolejnosc jest cala trescia bezpieczenstwa:
    ///  1) wariant bez callbacka NIE wola niczego,
    ///  2) trwajaca proba odmawia - ZERO drugiego POST,
    ///  3) brak zaznaczenia (takze pusta lista) odmawia bez POST,
    ///  4) dopiero potem JEDEN callback, z typowanym ulubionym Z LISTY - nigdy
    ///     szukanym po nazwie,
    ///  5) po zakonczeniu okno ZOSTAJE, a zaznaczenie i fokus sa przywracane.
    /// </summary>
    private void StartPlaySelected()
    {
        if (_closed) return;
        if (_loadFavorite is not { } load)
        {
            // Wariant tylko do odczytu: ZERO POST i zero udawania.
            Announce(SonosFavoritesLabels.PlayUnsupported);
            return;
        }

        if (_playInFlight)
        {
            Announce(SonosFavoritesLabels.PlayAlreadyInFlight);
            return;
        }

        if (!PlayButton.IsEnabled)
        {
            // BRAK grupy albo pusta lista: wyjasnienie, nie cicha odmowa.
            Announce(string.IsNullOrWhiteSpace(GroupNameForTests)
                ? SonosFavoritesLabels.PlayNeedsGroup
                : SonosFavoritesLabels.PlayNothingSelected);
            return;
        }

        if (FavoritesList.SelectedItem is not FavoriteRow row)
        {
            Announce(SonosFavoritesLabels.PlayNothingSelected);
            return;
        }

        LastPlayTaskForTests = RunPlayAsync(load, row);
    }

    /// <summary>
    /// JEDEN przelot proby. Zaznaczenie i fokus SA ZACHOWANE: zapisujemy je
    /// PRZED await i przywracamy tylko wtedy, gdy okno nadal zyje i fokus nadal
    /// jest w tym oknie. Po zamknieciu albo przy CUDZYM fokusie nie mowimy i nie
    /// ruszamy fokusu.
    /// </summary>
    private async Task RunPlayAsync(Func<PlayRequest, Task> load, FavoriteRow row)
    {
        var index = FavoritesList.SelectedIndex;
        var focusWasInList = FavoritesList.IsKeyboardFocusWithin;
        _playInFlight = true;
        PlayButton.IsEnabled = false;
        Announce(SonosFavoritesLabels.PlayPending);
        try
        {
            LoadCallsForTests++;
            // JEDEN callback na akcje uzytkownika. Wynik (przyjecie, odmowa,
            // nieznany skutek) oglasza warstwa, ktora zna kontrakt polecenia -
            // okno nie tlumaczy statusow HTTP i nie obiecuje, ze muzyka gra.
            //
            // NIESIEMY TOZSAMOSC: ta instancja okna i JEJ token zycia. Wlasciciel
            // nie musi zgadywac, KTORY modal zlecil - ani szukac "aktualnego".
            await load(new PlayRequest(this, _lifetime.Token, row.Favorite)).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // Nasze wlasne zamykanie albo anulowanie: CISZA.
        }
        finally
        {
            _playInFlight = false;
            if (!_closed)
            {
                // PRZYWROCENIE zaznaczenia i fokusu: stan sprzed proby, o ile
                // fokus nadal nalezy do tego okna. Nie kradniemy go obcemu oknu.
                PlayButton.IsEnabled = !string.IsNullOrWhiteSpace(GroupNameForTests) && _rows.Count > 0;
                if (index >= 0 && index < _rows.Count) FavoritesList.SelectedIndex = index;
                if (focusWasInList && IsActive && !FavoritesList.IsKeyboardFocusWithin)
                {
                    FocusSelectedRow();
                }
            }
        }
    }

    /// <summary>
    /// KOMUNIKAT: widoczny tekst statusu ORAZ programowe powiadomienie czytnika
    /// ISTNIEJACYM mechanizmem <see cref="Controls.AccessibleStatusTextBlock"/>.
    /// Po zamknieciu okna NIC nie mowimy.
    /// </summary>
    private void Announce(string message)
    {
        if (_closed) return;
        StatusText.Announce(message);
    }

    /// <summary>
    /// KOMUNIKAT od WLASCICIELA (okna glownego), ktory zna kontrakt polecenia.
    /// Okno nie tlumaczy statusow HTTP, tylko je pokazuje i oglasza ISTNIEJACYM
    /// mechanizmem. Po zamknieciu NIC nie mowi.
    /// </summary>
    internal void AnnounceForOwner(string message) => Announce(message);

    /// <summary>
    /// Czy TA instancja jest nadal ZYWYM, WIDOCZNYM adresatem statusu. Po
    /// zamknieciu (albo gdy okno zniklo z ekranu) wlasciciel NIE MA gdzie
    /// odpowiedziec - i nie wolno mu "odbic" komunikatu do okna glownego ani do
    /// nowszego modalu.
    /// </summary>
    internal bool IsLiveOwnerTarget => !_closed && IsVisible;

    /// <summary>
    /// NIEZMIENNE zlecenie uruchomienia. Niesie TOZSAMOSC modalu, ktory je
    /// zlecil, JEGO token zycia i typowany ulubiony Z LISTY tego okna.
    ///
    /// Dzieki temu spozniona odpowiedz ma DOKLADNIE JEDEN adres: instancje
    /// <paramref name="Origin"/>. Gdy ta nie zyje, wynik jest CICHY - nie
    /// przenosi sie do okna glownego i nie wchodzi do nowo otwartego modalu.
    /// </summary>
    /// <param name="Origin">Modalna instancja, ktora ZLECILA te probe.</param>
    /// <param name="Lifetime">Token zycia TEJ instancji - anulowany przy jej zamknieciu.</param>
    /// <param name="Favorite">Typowany ulubiony z biezacej listy tego okna.</param>
    internal sealed record PlayRequest(
        SonosFavoritesWindow Origin,
        CancellationToken Lifetime,
        SonosFavorite Favorite);

    /// <summary>
    /// POCZATKOWY FOKUS: pierwszy wiersz listy, a gdy lista jest pusta - sama
    /// lista. Nie przycisk Zamknij i nie Odtworz, bo uzytkownik przychodzi tu
    /// czytac i wybierac.
    /// </summary>
    private void FocusInitialElement()
    {
        FavoritesList.UpdateLayout();
        if (_rows.Count > 0 && FocusSelectedRow()) return;

        FavoritesList.Focus();
        Keyboard.Focus(FavoritesList);
    }

    /// <summary>FOKUS na kontenerze ZAZNACZONEGO wiersza, jesli istnieje.</summary>
    private bool FocusSelectedRow()
    {
        if (FavoritesList.ItemContainerGenerator.ContainerFromIndex(FavoritesList.SelectedIndex)
            is not ListBoxItem item)
        {
            return false;
        }

        item.Focus();
        Keyboard.Focus(item);
        return true;
    }

    /// <summary>
    /// WIERSZ LISTY: DEDYKOWANY, typowany. Widok pokazuje WYLACZNIE bezpieczna
    /// etykiete, a identyfikator jedzie w NIESIONYM <see cref="SonosFavorite"/> -
    /// wiec uruchomienie nigdy nie szuka pozycji po nazwie i dwie identyczne
    /// nazwy nadal daja dwa rozne identyfikatory.
    ///
    /// Nadal nie ma tu ani udawanego <c>Track</c>/<c>Device</c>, ani nowego
    /// rodzaju pozycji multimedialnej: ogolne odtwarzanie nie ma czego przechwycic.
    /// <c>ToString</c> zostaje BEZPIECZNY: sama etykieta, zero identyfikatora.
    /// </summary>
    internal sealed class FavoriteRow(string label, SonosFavorite favorite)
    {
        public string Label { get; } = label;

        /// <summary>TYPOWANY ulubiony Z BIEZACEJ listy. Tylko do odczytu.</summary>
        internal SonosFavorite Favorite { get; } = favorite;

        public override string ToString() => Label;
    }
}

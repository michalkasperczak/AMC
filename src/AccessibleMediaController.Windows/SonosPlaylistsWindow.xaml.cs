using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using AccessibleMediaController.Core.Sonos;

namespace AccessibleMediaController.Windows;

/// <summary>
/// PLAYLISTY SONOSA z konta.
///
/// Uklad i dyscyplina sa SWIADOMA kopia odebranego <see cref="SonosFavoritesWindow"/>,
/// a nie nowym, ogolnym frameworkiem list: te same bramki, ten sam status dla
/// czytnika, ten sam poczatkowy fokus i ta sama ochrona fokusu w trakcie proby.
/// Playlista ma jednak WLASNY typ i WLASNY endpoint - nie podszywamy jej pod
/// ulubiony tylko po to, zeby wejsc w gotowa droge.
///
/// ZERO POST poza JAWNA akcja: otwarcie okna, strzalki i Tab nie wysylaja niczego.
/// Enter na liscie albo przycisk "Odtwórz" to DOKLADNIE JEDNA proba.
///
/// TOZSAMOSC POZYCJI: wiersz niesie typowana playliste, wiec wlasciciel wysyla
/// IDENTYFIKATOR Z WIERSZA, nigdy tytul. Dwie playlisty o tym samym tytule i
/// roznych identyfikatorach pozostaja rozroznione wewnetrznie.
/// </summary>
public partial class SonosPlaylistsWindow : Window
{
    private readonly ObservableCollection<PlaylistRow> _rows = [];
    private readonly Func<PlayRequest, Task>? _loadPlaylist;

    /// <summary>Callback PRZYPISANIA PRESETU albo null. Okno nie zapisuje nic samo.</summary>
    private readonly Action<SonosPlaylist>? _assignPreset;
    private readonly CancellationTokenSource _lifetime = new();
    private bool _closed;
    private bool _playInFlight;
    private long _ownerFeedbackVersion;

    /// <summary>
    /// WARIANT TYLKO DO ODCZYTU: zaplecze nie umie uruchamiac playlist, wiec
    /// przycisk jest WYLACZONY, a Enter uczciwie mowi, ze uruchamianie jest
    /// niedostepne. Zero martwych obietnic.
    /// </summary>
    internal SonosPlaylistsWindow(IReadOnlyList<SonosPlaylist> playlists)
        : this(playlists, groupName: null, loadPlaylist: null)
    {
    }

    /// <summary>
    /// WARIANT Z URUCHAMIANIEM. <paramref name="groupName"/> to nazwa AKTYWNEJ
    /// grupy w chwili otwarcia; jej brak NIE blokuje odczytu listy, tylko
    /// wylacza uruchamianie i mowi, gdzie wybrac cel (Ctrl+F5).
    /// </summary>
    internal SonosPlaylistsWindow(
        IReadOnlyList<SonosPlaylist> playlists,
        string? groupName,
        Func<PlayRequest, Task>? loadPlaylist)
        : this(playlists, groupName, loadPlaylist, assignPreset: null)
    {
    }

    /// <summary>
    /// WARIANT Z PRZYPISYWANIEM PRESETU. Osobny konstruktor, zeby ISTNIEJACE
    /// wywolania zostaly nietkniete.
    /// </summary>
    internal SonosPlaylistsWindow(
        IReadOnlyList<SonosPlaylist> playlists,
        string? groupName,
        Func<PlayRequest, Task>? loadPlaylist,
        Action<SonosPlaylist>? assignPreset)
    {
        ArgumentNullException.ThrowIfNull(playlists);
        InitializeComponent();

        _loadPlaylist = loadPlaylist;
        _assignPreset = assignPreset;
        GroupNameForTests = string.IsNullOrWhiteSpace(groupName) ? null : groupName;

        foreach (var playlist in playlists)
        {
            _rows.Add(new PlaylistRow(playlist, SonosPlaylistsLabels.Describe(playlist)));
        }

        PlaylistsList.ItemsSource = _rows;

        var parts = new List<string>
        {
            _loadPlaylist is null
                ? SonosPlaylistsLabels.ViewIntroduction
                : SonosPlaylistsLabels.DescribePlayIntroduction(GroupNameForTests),
            SonosPlaylistsLabels.SummarizeCount(_rows.Count)
        };

        // PUSTA KOLEKCJA to POPRAWNY wynik, nie blad: SummarizeCount(0) mowi to
        // wprost ("nie ma zapisanych playlist"), wiec nie dopisujemy drugiego,
        // konkurencyjnego zdania i nie podstawiamy udawanej pozycji.
        IntroductionText.Text = string.Join(" ", parts);

        if (_rows.Count > 0) PlaylistsList.SelectedIndex = 0;

        // PRZYCISK dziala tylko gdy JEST czym grac i JEST gdzie grac.
        PlayButton.IsEnabled = _loadPlaylist is not null
            && _rows.Count > 0
            && !string.IsNullOrWhiteSpace(GroupNameForTests);

        if (_loadPlaylist is null)
        {
            AutomationProperties.SetHelpText(PlayButton, SonosPlaylistsLabels.PlayUnsupported);
        }
        else if (string.IsNullOrWhiteSpace(GroupNameForTests))
        {
            AutomationProperties.SetHelpText(PlayButton, SonosPlaylistsLabels.PlayNeedsGroup);
        }

        AutomationProperties.SetHelpText(PlaylistsList, _loadPlaylist is null
            ? "Strzałki czytają kolejne playlisty. To podgląd: uruchamianie jest niedostępne."
            : "Strzałki czytają kolejne playlisty. Enter albo przycisk Odtwórz uruchamia "
                + "zaznaczoną playlistę w wybranej grupie. Escape zamyka okno.");

        Closed += (_, _) =>
        {
            _closed = true;
            // ZYCIE ZLECENIA KONCZY SIE Z OKNEM, ale NIE ruszamy cudzych oczekiwan:
            // anulujemy WYLACZNIE wlasny token.
            try
            {
                _lifetime.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

            _lifetime.Dispose();
        };

        Loaded += (_, _) => FocusInitialElement();
    }

    /// <summary>Nazwa grupy, do ktorej to okno adresuje proby. Null = brak celu.</summary>
    internal string? GroupNameForTests { get; }

    internal int RowCountForTests => _rows.Count;

    /// <summary>
    /// PRZYWROCENIE ZAZNACZENIA po powrocie z innej sesji. Po IDENTYFIKATORZE, nie
    /// po indeksie: dwie playlisty moga miec ten sam tytul, a lista z konta mogla
    /// sie w miedzyczasie zmienic.
    /// </summary>
    internal void RestoreSelectedRow(string? playlistId)
    {
        var index = SonosSublistReturnPolicy.ResolveRowIndex(
            _rows.Select(row => row.Playlist.Id).ToArray(), playlistId);
        if (index < 0) return;
        PlaylistsList.SelectedIndex = index;
        PlaylistsList.UpdateLayout();
        FocusSelectedRow();
    }


    internal IReadOnlyList<string> RowLabelsForTests => _rows.Select(row => row.Label).ToArray();

    internal string IntroductionForTests => IntroductionText.Text;

    internal string StatusForTests => StatusText.Text;

    internal bool PlayEnabledForTests => PlayButton.IsEnabled;

    internal int LoadCallsForTests { get; private set; }

    internal int AnnouncementsForTests { get; private set; }

    internal Task? LastPlayTaskForTests { get; private set; }

    /// <summary>Zaznaczona playlista BEZ wysylki - sam ruch nic nie wysyla.</summary>
    internal string? HighlightedPlaylistIdForTests =>
        (PlaylistsList.SelectedItem as PlaylistRow)?.Playlist.Id;

    internal bool ListHasFocusForTests =>
        PlaylistsList.IsKeyboardFocusWithin || PlaylistsList.IsKeyboardFocused;

    /// <summary>Lista playlist dla POMIARU drogi klawiatury (prawdziwe zdarzenia).</summary>
    internal ListBox ListForTests => PlaylistsList;

    /// <summary>
    /// Czy przy TEJ instancji wolno oczekiwac drogi uruchomienia. Wlasnosc
    /// INSTANCJI, nie typu: wariant bez callbacka nie ma prawa niczego odtworzyc.
    /// </summary>
    internal bool OffersPlaybackForTests => _loadPlaylist is not null;

    internal string FocusedElementNameForTests => Keyboard.FocusedElement switch
    {
        null => "brak",
        var element when ReferenceEquals(element, PlayButton) => "PlayButton",
        var element when ReferenceEquals(element, CloseButton) => "CloseButton",
        var element when ReferenceEquals(element, PlaylistsList) => "PlaylistsList",
        ListBoxItem => "ListBoxItem",
        var element when ReferenceEquals(element, this) => "Window",
        var element => element.GetType().Name
    };

    /// <summary>Czy TA instancja jest nadal ZYWYM, WIDOCZNYM adresatem statusu.</summary>
    internal bool IsLiveOwnerTarget => !_closed && IsVisible;

    /// <summary>PRODUKCYJNA droga Enter/Odtwórz - ten sam kod, co klik.</summary>
    internal void PlaySelectedForTests() => StartPlaySelected();

    internal void SelectRowForTests(int index)
    {
        if (index < 0 || index >= _rows.Count) throw new ArgumentOutOfRangeException(nameof(index));
        PlaylistsList.SelectedIndex = index;
        PlaylistsList.UpdateLayout();
        FocusSelectedRow();
    }

    /// <summary>
    /// KOMUNIKAT od WLASCICIELA, ktory zna kontrakt polecenia. Gdy okno ZYJE, ale
    /// NIE JEST AKTYWNE, spozniony wynik NIE przerywa czytnikowi pracy w innym
    /// oknie: odswiezamy tylko widoczny tekst, zeby wracajacy uzytkownik
    /// przeczytal AKTUALNY stan zamiast nieaktualnego "Wysyłam...".
    /// </summary>
    internal void AnnounceForOwner(string message)
    {
        if (_closed) return;
        _ownerFeedbackVersion++;
        if (!IsActive)
        {
            StatusText.Text = message;
            return;
        }

        Announce(message);
    }

    private void Announce(string message)
    {
        if (_closed) return;
        AnnouncementsForTests++;
        StatusText.Announce(message);
    }

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

    private void Play_Click(object sender, RoutedEventArgs e) => StartPlaySelected();

    /// <summary>
    /// PRZYPISANIE PRESETU z TEJ listy: TYPOWANA playlista Z WIERSZA, nigdy po
    /// tytule. Nic nie wysyla do Sonosa.
    /// </summary>
    private void RequestPresetAssignment()
    {
        if (_closed) return;
        if (_assignPreset is null)
        {
            Announce("Tu nie można przypisać presetu.");
            return;
        }
        if (PlaylistsList.SelectedItem is not PlaylistRow row)
        {
            Announce("Najpierw wybierz playlistę z listy.");
            return;
        }
        _assignPreset(row.Playlist);
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Handled) return;
        // CTRL+CYFRA: PRZELACZENIE SESJI BEZ RECZNEGO ZAMYKANIA LISTY.
        // Modal wylacza okno glowne, wiec jego router skrotow tego gestu nie
        // zobaczy - przechwytujemy go tu, tak samo jak Ctrl+Alt+Shift+P ponizej.
        if (SonosSublistSessionSwitch.TryHandle(
            this,
            e,
            SonosLibraryPresentation.PlaylistsCategoryId,
            () => HighlightedPlaylistIdForTests))
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
            // SWIADOME wyjscie: nie zostawiamy zadania powrotu.
            (Owner as MainWindow)?.ClearSonosSublistReturn();
            CloseSelf();
            e.Handled = true;
            return;
        }

        // CTRL+ALT+SHIFT+P: PRZYPISANIE PRESETU - glowne okno jest wylaczone jako
        // Owner, wiec przechwytujemy skrot tutaj.
        // Z ALTEM WPF podaje Key.System, a litera siedzi w SystemKey.
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.P
            && (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift))
                == (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift))
        {
            e.Handled = true;
            if (e.IsRepeat) return;
            RequestPresetAssignment();
            return;
        }

        if (e.Key != Key.Enter || !PlaylistsList.IsKeyboardFocusWithin) return;

        e.Handled = true;
        // AUTOPOWTARZANIE: JEDNA proba, nie seria POST - sprawdzane PRZED callbackiem.
        if (e.IsRepeat) return;
        StartPlaySelected();
    }

    /// <summary>
    /// JEDNA droga uruchomienia dla przycisku i dla Entera. Kolejnosc jest cala
    /// trescia bezpieczenstwa: wariant bez callbacka nie wola niczego, trwajaca
    /// proba odmawia, brak celu/zaznaczenia odmawia z WYJASNIENIEM, i tylko potem
    /// leci JEDEN callback z typowana playlista Z LISTY.
    /// </summary>
    private void StartPlaySelected()
    {
        if (_closed) return;
        if (_loadPlaylist is not { } load)
        {
            Announce(SonosPlaylistsLabels.PlayUnsupported);
            return;
        }

        if (_playInFlight)
        {
            Announce(SonosPlaylistsLabels.PlayAlreadyInFlight);
            return;
        }

        if (!PlayButton.IsEnabled)
        {
            Announce(string.IsNullOrWhiteSpace(GroupNameForTests)
                ? SonosPlaylistsLabels.PlayNeedsGroup
                : SonosPlaylistsLabels.PlayNothingSelected);
            return;
        }

        if (PlaylistsList.SelectedItem is not PlaylistRow row)
        {
            Announce(SonosPlaylistsLabels.PlayNothingSelected);
            return;
        }

        LastPlayTaskForTests = RunPlayAsync(load, row);
    }

    /// <summary>
    /// JEDEN przelot proby. Zaznaczenie i fokus sa ZACHOWANE: zapisujemy je PRZED
    /// await i przywracamy tylko wtedy, gdy okno nadal zyje, jest aktywne i fokus
    /// zszedl na samo okno. Gdy uzytkownik przeszedl na "Zamknij", NIE cofamy go.
    /// </summary>
    private async Task RunPlayAsync(Func<PlayRequest, Task> load, PlaylistRow row)
    {
        var index = PlaylistsList.SelectedIndex;
        var focusWasInList = PlaylistsList.IsKeyboardFocusWithin;
        // FOKUS NA PRZYCISKU zapamietany OSOBNO: wylaczenie SKUPIONEGO przycisku
        // oddaje fokus oknu, a czytnik czyta caly dialog od nowa.
        var focusWasOnPlay = PlayButton.IsKeyboardFocused;
        _playInFlight = true;
        // SKUPIONEGO przycisku NIE WYLACZAMY: drugie klikniecie odrzuca bramka
        // _playInFlight, wiec wylaczenie nic nie wnosi, a kosztuje fokus.
        if (!focusWasOnPlay) PlayButton.IsEnabled = false;
        try
        {
            LoadCallsForTests++;
            var feedbackBefore = _ownerFeedbackVersion;
            var operation = load(new PlayRequest(this, _lifetime.Token, row.Playlist));
            // KOMUNIKAT "Czekaj" TYLKO gdy jest PRAWDZIWE oczekiwanie: odmowa
            // synchroniczna nie ma fazy czekania, a jej wlasny wynik nie moze
            // zostac nadpisany.
            if (!operation.IsCompleted && _ownerFeedbackVersion == feedbackBefore)
                Announce(SonosPlaylistsLabels.PlayPending);
            await operation.ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // Wlasne zamkniecie okna albo anulowanie: CISZA.
        }
        finally
        {
            _playInFlight = false;
            if (!_closed)
            {
                PlayButton.IsEnabled = _rows.Count > 0 && !string.IsNullOrWhiteSpace(GroupNameForTests);
                if (index >= 0 && index < _rows.Count) PlaylistsList.SelectedIndex = index;
                if (focusWasInList && IsActive && !PlaylistsList.IsKeyboardFocusWithin
                    && ReferenceEquals(Keyboard.FocusedElement, this))
                {
                    FocusSelectedRow();
                }
                else if (focusWasOnPlay && IsActive && !PlayButton.IsKeyboardFocused
                    && PlayButton.IsEnabled && ReferenceEquals(Keyboard.FocusedElement, this))
                {
                    PlayButton.Focus();
                    Keyboard.Focus(PlayButton);
                }
            }
        }
    }

    private void FocusInitialElement()
    {
        PlaylistsList.UpdateLayout();
        if (_rows.Count > 0 && FocusSelectedRow()) return;

        PlaylistsList.Focus();
        Keyboard.Focus(PlaylistsList);
    }

    private bool FocusSelectedRow()
    {
        if (PlaylistsList.ItemContainerGenerator.ContainerFromIndex(PlaylistsList.SelectedIndex)
            is not ListBoxItem item)
        {
            return false;
        }

        item.Focus();
        Keyboard.Focus(item);
        return true;
    }

    /// <summary>
    /// NIEZMIENNE zlecenie uruchomienia. Niesie TOZSAMOSC okna, ktore je zlecilo,
    /// JEGO token zycia i TYPOWANA playliste z listy tego okna - wiec spozniona
    /// odpowiedz ma DOKLADNIE JEDEN adres.
    /// </summary>
    internal sealed record PlayRequest(
        SonosPlaylistsWindow Origin,
        CancellationToken Lifetime,
        SonosPlaylist Playlist);

    /// <summary>
    /// WIERSZ: typowana playlista WEWNETRZNIE, bezpieczna etykieta na widoku.
    /// Dzieki temu wlasciciel wysyla IDENTYFIKATOR, a nie tytul.
    /// </summary>
    internal sealed class PlaylistRow(SonosPlaylist playlist, string label)
    {
        internal SonosPlaylist Playlist { get; } = playlist;

        public string Label { get; } = label;

        public override string ToString() => Label;
    }
}

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AccessibleMediaController.Core.Sonos;

namespace AccessibleMediaController.Windows;

/// <summary>
/// OKNO KONTA SONOS na ODEBRANYM <see cref="SonosAccountCoordinator"/>.
///
/// Granice tego okna, swiadome i sprawdzane testami:
///   * WLASCICIELEM koordynatora jest aplikacja, nie okno. Zamkniecie okna NIE
///     wola Dispose ani Disconnect - zamyka wylacznie WLASNA probe logowania i
///     wlasne oczekiwanie. Tokeny w pamieci wspolnego koordynatora zostaja.
///   * Caller wola <see cref="SonosAccountCoordinator.RestoreOnce"/> PRZED
///     otwarciem. Okno korzysta z migawki i NIE czyta produkcyjnego magazynu z
///     bezparametrowego konstruktora.
///   * ZERO pol deweloperskich: client id, sekret i adres powrotu nalezą do
///     brokera AMC, uzytkownik nie ma tu czego wpisywac.
///   * Przegladarke otwiera WSTRZYKNIETY delegat. Domyslny otwiera WYLACZNIE
///     adres z wyniku Start, sprawdzony polityka <see cref="SonosAuthorizeUrlPolicy"/> -
///     nigdy adresu wpisanego przez uzytkownika.
///   * Zadnego pollingu, timera i sleepu. Jedno jawne sprawdzenie na klikniecie.
/// </summary>
public partial class SonosAccountWindow
{
    private readonly SonosAccountCoordinator coordinator;
    private readonly Func<Uri, bool> openBrowser;
    private readonly Func<bool> confirmDisconnect;
    private readonly Action<string>? announcementSink;

    /// <summary>Anuluje WYLASNIE operacje tego okna. Nie dotyka zycia koordynatora.</summary>
    private readonly CancellationTokenSource windowLifetime = new();

    private bool busy;
    private bool closed;
    private SonosAccountSnapshot snapshot;

    /// <summary>Czy okno rozpoczelo WLASNA probe logowania (jest co anulowac przy zamknieciu).</summary>
    private bool ownLoginAttempt;

    /// <summary>Tresc ostatniego komunikatu - zostaje w oknie do ponownego odczytu.</summary>
    public string LastAnnouncement { get; private set; } = string.Empty;

    /// <summary>Liczba komunikatow oglaszanych przez to okno (kwit dla testow i harnessu).</summary>
    public int AnnouncementCount { get; private set; }

    public SonosAccountWindow(
        SonosAccountCoordinator coordinator,
        Func<Uri, bool>? openBrowser = null,
        Func<bool>? confirmDisconnect = null,
        Action<string>? announcementSink = null)
    {
        ArgumentNullException.ThrowIfNull(coordinator);
        this.coordinator = coordinator;
        this.openBrowser = openBrowser ?? OpenTrustedAuthorizeUri;
        // Brak wstrzyknietego potwierdzenia = brak zgody. Wylogowanie jest
        // nieodwracalne, wiec domyslna odpowiedzia nie moze byc "tak".
        this.confirmDisconnect = confirmDisconnect ?? (() => false);
        this.announcementSink = announcementSink;

        InitializeComponent();
        snapshot = coordinator.Snapshot;
        RefreshView();

        // Fokus startowy ustawiony przy BUDOWIE okna, nie przez opoznienie ani
        // wiazanie rozwiazywane dopiero przy pokazaniu. Dzieki temu da sie go
        // zmierzyc bez Show i bez odbierania fokusu czemukolwiek na pulpicie.
        FocusManager.SetFocusedElement(this, InstructionBox);
    }

    /// <summary>
    /// Domyslne otwarcie przegladarki: TYLKO zaufany adres autoryzacji Sonos z
    /// wyniku Start. Adres spoza polityki nie jest otwierany i nie trafia do
    /// komunikatu (zero wycieku URI).
    /// </summary>
    private static bool OpenTrustedAuthorizeUri(Uri authorizeUri)
    {
        if (authorizeUri is null || !SonosAuthorizeUrlPolicy.IsTrustedAuthorizeUrl(authorizeUri.AbsoluteUri))
        {
            return false;
        }

        try
        {
            using var started = Process.Start(new ProcessStartInfo(authorizeUri.AbsoluteUri)
            {
                UseShellExecute = true
            });
            return true;
        }
        catch (Exception)
        {
            // Bez echa adresu i bez tresci wyjatku w komunikacie uzytkownika.
            return false;
        }
    }

    // ================= widok =================

    /// <summary>
    /// Buduje CALA widoczna tresc z migawki. Kolejnosc odczytu wynika z budowy
    /// okna; tu nie ma zadnego opoznienia ustawiajacego mowe.
    /// </summary>
    internal void RefreshView()
    {
        InstructionBox.Text = BuildInstructionText(snapshot);
        ApplyButtonAvailability(snapshot);
    }

    internal static string BuildInstructionText(SonosAccountSnapshot current)
    {
        var text = current.Message;
        var issue = current.IssueMessage;
        if (!string.IsNullOrEmpty(issue))
        {
            text += Environment.NewLine + issue;
        }

        if (current.HasCredentials && !current.IsPersisted)
        {
            text += Environment.NewLine + "Działa, ale nie zapisane. Możesz ponowić sam zapis.";
        }

        text += Environment.NewLine + Environment.NewLine
            + "Logujesz się zwykłym kontem Sonos. Hasło wpisujesz wyłącznie w oficjalnej "
            + "przeglądarce na stronie Sonos; AMC go nie widzi i nie zapisuje. "
            + "To okno nie wymaga żadnych danych aplikacji ani kluczy.";

        return text;
    }

    /// <summary>
    /// Przycisk bez warunkow NIE jest oferowany - martwy klawisz to czysty koszt
    /// przy tabulacji. Zajetosc wylacza duplikaty operacji, ale Zamknij i Anuluj
    /// logowanie dzialaja zawsze.
    /// </summary>
    private void ApplyButtonAvailability(SonosAccountSnapshot current)
    {
        SetAvailability(LoginButton, applicable: true);
        SetAvailability(CheckLoginButton, current.IsAwaitingBrowser);
        SetAvailability(CancelLoginButton, current.IsAwaitingBrowser);
        SetAvailability(RefreshButton, current.HasCredentials && current.HasRefreshToken);
        SetAvailability(RetryPersistButton, current.CanRetryPersist);
        SetAvailability(DisconnectButton, current.HasCredentials || current.PersistedRecordMayRemain);

        // Klawisze wyjscia i odwolania nie moga zniknac przy zajetosci.
        CancelLoginButton.IsEnabled = CancelLoginButton.Visibility == Visibility.Visible;
        CloseButton.IsEnabled = true;
        CloseButton.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// Ukrycie przycisku, ktory WLASNIE ma fokus, zostawia fokus na samym oknie:
    /// czytnik ekranu oglasza wtedy rolne "okno dialogowe" i czyta CALY dialog od
    /// nowa, a klawiatura traci punkt zaczepienia. Dlatego przed schowaniem
    /// takiego przycisku fokus przechodzi na sasiednia UZYWALNA kontrolke.
    ///
    /// Zamierzone granice: ruszamy fokus TYLKO wtedy, gdy znikajacy przycisk sam
    /// go trzyma (kiedy uzytkownik odszedl gdzie indziej - nie dotykamy), i tylko
    /// wewnatrz tego okna, bez Activate, bez Focus() na oknie i bez opoznien.
    /// Gdy fokus jest poza tym oknem, poprawiamy WYLACZNIE punkt powrotu okna,
    /// wiec nieaktywne okno niczego nie zabiera pierwszemu planowi.
    /// </summary>
    private void SetAvailability(Button button, bool applicable)
    {
        var hides = !applicable && button.Visibility == Visibility.Visible;
        if (hides)
        {
            RescueFocusBefore(button);
        }

        button.Visibility = applicable ? Visibility.Visible : Visibility.Collapsed;
        button.IsEnabled = applicable && !busy;
    }

    /// <summary>
    /// Przenosi punkt fokusu z przycisku, ktory zaraz zniknie, na pierwsza
    /// UZYWALNA kontrolke tego okna. Nie wymusza aktywacji okna: gdy fokus
    /// klawiatury jest gdzie indziej, zmieniany jest tylko punkt powrotu.
    /// </summary>
    private void RescueFocusBefore(Button vanishing)
    {
        var keyboardHere = ReferenceEquals(Keyboard.FocusedElement, vanishing);
        var logicalHere = ReferenceEquals(FocusManager.GetFocusedElement(this), vanishing);
        if (!keyboardHere && !logicalHere)
        {
            return;
        }

        var fallback = FocusFallbackFor(vanishing);
        if (fallback is null)
        {
            return;
        }

        // Punkt powrotu okna ustawiany zawsze - to on decyduje, gdzie wroci
        // uzytkownik, i nie przejmuje pierwszego planu.
        FocusManager.SetFocusedElement(this, fallback);

        // Prawdziwy fokus klawiatury ruszamy WYLACZNIE wtedy, gdy trzymal go
        // znikajacy przycisk, czyli gdy i tak zaraz by go stracil.
        if (keyboardHere)
        {
            Keyboard.Focus(fallback);
        }
    }

    /// <summary>
    /// Wybiera stabilny punkt zapasowy: najpierw widoczny i wlaczony przycisk
    /// SASIEDNI w tej samej kolejnosci tabulacji, a gdy takiego nie ma - pole
    /// instrukcji, ktore jest dostepne i zawsze obecne.
    /// </summary>
    private IInputElement? FocusFallbackFor(Button vanishing)
    {
        // Kolejnosc odpowiada kolejnosci tabulacji w oknie - punkt zapasowy jest
        // wiec przewidywalny, a nie zalezny od chwilowego stanu.
        Button[] order =
        {
            LoginButton,
            CheckLoginButton,
            CancelLoginButton,
            RefreshButton,
            RetryPersistButton,
            DisconnectButton,
            CloseButton,
        };

        var index = Array.IndexOf(order, vanishing);
        if (index >= 0)
        {
            for (var next = index + 1; next < order.Length; next++)
            {
                if (IsUsableFocusTarget(order[next]))
                {
                    return order[next];
                }
            }

            for (var previous = index - 1; previous >= 0; previous--)
            {
                if (IsUsableFocusTarget(order[previous]))
                {
                    return order[previous];
                }
            }
        }

        return InstructionBox;
    }

    private static bool IsUsableFocusTarget(Button candidate) =>
        candidate.Visibility == Visibility.Visible && candidate.IsEnabled;

    private void Apply(SonosAccountSnapshot updated)
    {
        snapshot = updated;
        RefreshView();
    }

    /// <summary>
    /// JEDEN komunikat na zdarzenie. W produkcji idzie przez istniejacy
    /// <see cref="Controls.AccessibleStatusTextBlock.Announce"/>
    /// (RaiseNotificationEvent, bez dodatkowego LiveRegionChanged), w testach do
    /// wstrzyknietego odbiornika. Tresc ZOSTAJE w dostepnym polu do ponownego odczytu.
    /// </summary>
    private void Announce(string message)
    {
        LastAnnouncement = message;
        AnnouncementCount++;
        if (announcementSink is not null)
        {
            OperationStatusText.Text = message;
            announcementSink(message);
            return;
        }

        OperationStatusText.Announce(message);
    }

    // ================= operacje =================

    /// <summary>
    /// Wspolna oslona operacji: zajetosc blokuje duplikaty, KAZDY wyjatek jest
    /// obsluzony (zaden async void nie wypuszcza go do petli komunikatow), a
    /// spozniona kontynuacja po zamknieciu okna nie rusza interfejsu i niczego
    /// nie wskrzesza.
    /// </summary>
    private async Task RunAsync(Func<CancellationToken, Task> operation, string failureMessage)
    {
        if (busy || closed)
        {
            return;
        }

        busy = true;
        RefreshView();
        try
        {
            await operation(windowLifetime.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            if (!closed)
            {
                Announce("Operacja konta Sonos została przerwana.");
            }
        }
        catch (Exception)
        {
            // Komunikat STALY: zaden wyjatek transportu nie cytuje adresu, sesji
            // ani tokenu.
            if (!closed)
            {
                Announce(failureMessage);
            }
        }
        finally
        {
            busy = false;
            if (!closed)
            {
                // Odswiezenie wlasciwosci, NIGDY nie odbieranie fokusu: uzytkownik
                // moze w tym czasie stac w przegladarce.
                Apply(coordinator.Snapshot);
            }
        }
    }

    /// <summary>
    /// Te same drogi, ktorymi ida klikniecia, ale jako Task - zeby test mogl je
    /// DOCZEKAC bez pokazywania okna i bez wysylania klawiszy. Klikniecie woła
    /// dokladnie to samo, wiec test nie mierzy osobnej sciezki.
    /// </summary>
    internal Task InvokeLoginAsync() =>
        RunAsync(BeginLoginAsync, "Nie udało się rozpocząć logowania Sonos.");

    internal Task InvokeCheckLoginAsync() =>
        RunAsync(CheckLoginAsync, "Nie udało się sprawdzić logowania Sonos.");

    internal Task InvokeRefreshAsync() =>
        RunAsync(RefreshAccessAsync, "Nie udało się odnowić dostępu Sonos.");

    internal void InvokeCancelLogin() => CancelLogin_Click(this, new RoutedEventArgs());

    internal void InvokeRetryPersist() => RetryPersist_Click(this, new RoutedEventArgs());

    internal void InvokeDisconnect() => Disconnect_Click(this, new RoutedEventArgs());

    private async void Login_Click(object sender, RoutedEventArgs e) =>
        await RunAsync(BeginLoginAsync, "Nie udało się rozpocząć logowania Sonos.");

    private async Task BeginLoginAsync(CancellationToken cancellationToken)
    {
        var result = await coordinator.BeginLoginAsync(cancellationToken).ConfigureAwait(true);
        if (closed)
        {
            return;
        }

        Apply(result.Snapshot);
        if (!result.Started || result.AuthorizeUri is null)
        {
            Announce(result.Message);
            return;
        }

        ownLoginAttempt = true;
        if (openBrowser(result.AuthorizeUri))
        {
            Announce("Dokończ logowanie Sonos w przeglądarce, potem wybierz Sprawdź logowanie.");
            return;
        }

        Announce("Nie udało się otworzyć przeglądarki z logowaniem Sonos. Spróbuj ponownie.");
    }

    private async void CheckLogin_Click(object sender, RoutedEventArgs e) =>
        await RunAsync(CheckLoginAsync, "Nie udało się sprawdzić logowania Sonos.");

    private async Task CheckLoginAsync(CancellationToken cancellationToken)
    {
        var result = await coordinator.CheckLoginAsync(cancellationToken).ConfigureAwait(true);
        if (closed)
        {
            return;
        }

        Apply(result.Snapshot);
        Announce(DescribeCheck(result));
        if (!result.StillWaiting)
        {
            ownLoginAttempt = false;
        }
    }

    /// <summary>
    /// StillWaiting NIE znaczy zawsze "dokoncz w przegladarce": po ProofMismatch
    /// albo niezgodnej odpowiedzi proba zostaje czynna tylko po to, by mozna bylo
    /// ponowic, a uzytkownik musi uslyszec RZECZYWISTA przyczyne, nie samo czekanie.
    /// </summary>
    internal static string DescribeCheck(SonosAccountLoginCheckResult result)
    {
        if (!result.HadPendingLogin)
        {
            return "Nie ma rozpoczętego logowania Sonos. Wybierz Zaloguj w przeglądarce.";
        }

        if (result.Discarded)
        {
            return "Ten wynik logowania Sonos jest już nieaktualny; rozpocznij logowanie na nowo.";
        }

        if (result.Connected)
        {
            return result.WriteStatus == SonosCredentialWriteStatus.Success
                ? "Konto Sonos jest połączone i zapisane."
                : "Konto Sonos działa, ale nie zostało zapisane. Możesz ponowić sam zapis.";
        }

        if (result.LoginStatus == SonosLoginStatus.Pending)
        {
            return "Logowanie Sonos jeszcze trwa. Dokończ je w przeglądarce i sprawdź ponownie.";
        }

        // Kazdy inny wynik (odmowa, wygasniecie, ProofMismatch, niezgodna
        // odpowiedz, blad brokera) opisuje SWOJ status.
        var text = result.Message;
        return result.StillWaiting
            ? text + " Możesz sprawdzić ponownie albo rozpocząć logowanie na nowo."
            : text + " Rozpocznij logowanie na nowo.";
    }

    private void CancelLogin_Click(object sender, RoutedEventArgs e)
    {
        if (closed)
        {
            return;
        }

        // Anulowanie MUSI dzialac takze w trakcie zajetosci - inaczej uzytkownik
        // zostaje uwieziony w czekaniu.
        Apply(coordinator.CancelPendingLogin());
        ownLoginAttempt = false;
        Announce("Logowanie Sonos zostało anulowane.");
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) =>
        await RunAsync(RefreshAccessAsync, "Nie udało się odnowić dostępu Sonos.");

    private async Task RefreshAccessAsync(CancellationToken cancellationToken)
    {
        var result = await coordinator.RefreshAsync(cancellationToken).ConfigureAwait(true);
        if (closed)
        {
            return;
        }

        Apply(result.Snapshot);
        if (result.Renewed)
        {
            Announce(result.WriteStatus == SonosCredentialWriteStatus.Success
                ? "Dostęp Sonos został odnowiony i zapisany."
                : "Dostęp Sonos działa, ale nie został zapisany. Możesz ponowić sam zapis.");
            return;
        }

        Announce(result.Message);
    }

    private void RetryPersist_Click(object sender, RoutedEventArgs e)
    {
        if (busy || closed)
        {
            return;
        }

        var result = coordinator.RetryPersist();
        Apply(result.Snapshot);
        if (!result.Attempted)
        {
            Announce("Nie ma czego zapisywać.");
            return;
        }

        Announce(result.Succeeded
            ? "Logowanie Sonos zostało zapisane."
            : SonosCredentialMessages.Describe(result.WriteStatus ?? SonosCredentialWriteStatus.WriteFailure));
    }

    private void Disconnect_Click(object sender, RoutedEventArgs e)
    {
        if (busy || closed)
        {
            return;
        }

        if (!confirmDisconnect())
        {
            Announce("Wylogowanie z Sonos zostało odwołane.");
            return;
        }

        var result = coordinator.Disconnect();
        Apply(result.Snapshot);
        ownLoginAttempt = false;
        Announce(result.Disconnected
            ? "Wylogowano z Sonos; zapisane logowanie zostało usunięte."
            : "Wylogowanie z Sonos nie zostało dokończone: zapisane logowanie mogło pozostać. Spróbuj ponownie.");
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    /// <summary>
    /// Zamkniecie konczy WLASNA prace okna: anuluje wlasna probe logowania i
    /// wlasne oczekiwanie. NIE wola Dispose ani Disconnect na wspolnym
    /// koordynatorze - wlascicielem jest aplikacja, a tokeny w pamieci sa
    /// potrzebne dalej.
    /// </summary>
    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) => ShutdownOwnWork();

    internal void ShutdownOwnWork()
    {
        if (closed)
        {
            return;
        }

        closed = true;
        if (ownLoginAttempt)
        {
            try
            {
                coordinator.CancelPendingLogin();
            }
            catch (ObjectDisposedException)
            {
                // Wlasciciel zwolnil koordynator wczesniej - to nie blad zamykania.
            }

            ownLoginAttempt = false;
        }

        try
        {
            windowLifetime.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Juz zwolniony token nie jest bledem zamykania.
        }

        // ROZMYSLNIE bez Dispose: trwajaca operacja koordynatora trzyma token
        // POWIAZANY z tym zrodlem. Zwolnienie zrodla teraz wywalaloby jej
        // wlasne, poprawne zwolnienie linku juz po zamknieciu okna. Anulowanie
        // wystarcza, a zrodlo zbierze GC razem z oknem.
    }

    // ================= kwity dla testow i harnessu =================

    internal bool IsClosedForWork => closed;

    internal bool IsBusy => busy;

    internal SonosAccountSnapshot CurrentSnapshot => snapshot;
}

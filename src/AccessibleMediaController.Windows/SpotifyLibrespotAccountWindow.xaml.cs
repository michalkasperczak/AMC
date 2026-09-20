using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using AccessibleMediaController.Windows.Services;

namespace AccessibleMediaController.Windows;

public partial class SpotifyLibrespotAccountWindow : Controls.AccessibleWindow
{
    private readonly Func<bool> hasLogin;
    private readonly Func<Action<string, Uri>, CancellationToken, Task> pair;
    private readonly Action disconnect;
    private readonly Action<Uri> openBrowser;
    private CancellationTokenSource? pairingCancellation;
    private Uri? pairingAddress;
    private bool closed;

    internal SpotifyLibrespotAccountWindow(
        Func<bool> hasLogin,
        Func<Action<string, Uri>, CancellationToken, Task> pair,
        Action disconnect,
        Action<Uri>? openBrowser = null)
    {
        InitializeComponent();
        this.hasLogin = hasLogin;
        this.pair = pair;
        this.disconnect = disconnect;
        this.openBrowser = openBrowser ?? (uri => Process.Start(
            new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true }));
        UpdateStatus();
        Loaded += (_, _) => Dispatcher.BeginInvoke(() => AccountText.Focus(), DispatcherPriority.ContextIdle);
        Closing += (_, _) =>
        {
            closed = true;
            if (pairingCancellation is not null)
            {
                CompletionAnnouncement = "Anulowano parowanie Spotify — Librespot";
                pairingCancellation.Cancel();
            }
        };
    }

    public string? CompletionAnnouncement { get; private set; }
    public bool Disconnected { get; private set; }
    public bool OpenCatalogRequested { get; private set; }
    internal Task PendingPairing { get; private set; } = Task.CompletedTask;

    private void UpdateStatus()
    {
        var paired = hasLogin();
        AccountText.Text = (paired
            ? "Parowanie odtwarzacza Spotify — Librespot jest zapisane. "
            : "Odtwarzacz Spotify — Librespot wymaga jednorazowego parowania. ")
            + "Biblioteka i wyszukiwanie korzystają z oddzielnego logowania. "
            + "Aby zalogować bibliotekę lub rozszerzyć zgodę na jej zmienianie, wybierz "
            + "Konto katalogu i biblioteka, a następnie Zaloguj w przeglądarce. "
            + "Parowanie odtwarzacza nie zmienia zgody na zapis biblioteki. "
            + "Odtwarzacz i biblioteka powinny korzystać z tego samego konta Premium. "
            + "Hasło wpisujesz wyłącznie na stronie Spotify; AMC go nie otrzymuje. "
            + "Librespot pozwala wybrać wyjście dźwięku, ale nie obsługuje Spotify Lossless.";
        DisconnectButton.IsEnabled = paired && pairingCancellation is null;
    }

    private void Pair_Click(object sender, RoutedEventArgs e)
    {
        if (pairingCancellation is not null) return;
        PendingPairing = PairAsync();
    }

    private async Task PairAsync()
    {
        using var cancellation = new CancellationTokenSource();
        pairingCancellation = cancellation;
        PairButton.IsEnabled = false;
        CatalogAccountButton.IsEnabled = false;
        DisconnectButton.IsEnabled = false;
        ClearPairing();
        OperationStatusText.Announce("Pobieranie jednorazowego kodu parowania Spotify");
        try
        {
            await pair((code, address) => Dispatcher.Invoke(() =>
            {
                if (closed || cancellation.IsCancellationRequested) return;
                if (!string.IsNullOrEmpty(address.UserInfo) || !address.IsDefaultPort)
                    throw new InvalidOperationException("Nieprawidłowa strona parowania.");
                pairingAddress = SpotifyLibrespotAuthenticationService.ResolveVerificationUri(
                    address.AbsoluteUri, null);
                PairingCodeBox.Text = code;
                PairingAddressBox.Text = address.AbsoluteUri;
                OpenBrowserButton.IsEnabled = true;
                CopyCodeButton.IsEnabled = true;
                OperationStatusText.Announce("Kod jest gotowy. Otwórz stronę Spotify przyciskiem "
                    + "albo skopiuj adres na telefon. Po zatwierdzeniu AMC samo potwierdzi połączenie. "
                    + "Zamknięcie tego okna anuluje oczekiwanie.");
                PairingCodeBox.Focus();
            }), cancellation.Token);
            if (closed || cancellation.IsCancellationRequested) return;
            CompletionAnnouncement = "Sparowano Spotify — Librespot. Połączenie zostało zapamiętane";
            OperationStatusText.Announce(CompletionAnnouncement);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            CompletionAnnouncement = "Anulowano parowanie Spotify — Librespot";
            DiagnosticLog.Info("spotify-librespot", CompletionAnnouncement);
            if (!closed) OperationStatusText.Announce(CompletionAnnouncement);
        }
        catch (Exception exception)
        {
            // Odpowiedzi OAuth mogą zawierać poświadczenia: żadnej surowej treści w logu ani UI.
            DiagnosticLog.Warning("spotify-librespot", $"Parowanie nie powiodło się; typ: {exception.GetType().Name}.");
            CompletionAnnouncement = "Nie udało się zakończyć parowania Spotify — Librespot. "
                + "Kod mógł wygasnąć albo zgoda nie została udzielona. Sprawdź połączenie i spróbuj ponownie.";
            if (!closed) OperationStatusText.Announce(CompletionAnnouncement);
        }
        finally
        {
            pairingCancellation = null;
            if (!closed)
            {
                ClearPairing();
                PairButton.IsEnabled = true;
                CatalogAccountButton.IsEnabled = true;
                UpdateStatus();
            }
        }
    }

    private void ClearPairing()
    {
        pairingAddress = null;
        PairingCodeBox.Clear();
        PairingAddressBox.Clear();
        OpenBrowserButton.IsEnabled = false;
        CopyCodeButton.IsEnabled = false;
    }

    private void OpenBrowser_Click(object sender, RoutedEventArgs e)
    {
        if (pairingAddress is null) return;
        try { openBrowser(pairingAddress); }
        catch (Exception)
        {
            OperationStatusText.Announce("Nie udało się otworzyć przeglądarki. Skopiuj adres z pola strony parowania.");
        }
    }

    private void CopyCode_Click(object sender, RoutedEventArgs e)
    {
        if (pairingAddress is null) return;
        try
        {
            Clipboard.SetText(PairingCodeBox.Text);
            OperationStatusText.Announce("Skopiowano kod parowania Spotify");
        }
        catch (Exception)
        {
            OperationStatusText.Announce("Schowek jest niedostępny. Kod można odczytać w polu kodu parowania.");
        }
    }

    private void Disconnect_Click(object sender, RoutedEventArgs e)
    {
        if (pairingCancellation is not null) return;
        if (AccessibleDialog.Show(this,
            "Odłączyć tylko odtwarzanie Spotify — Librespot? Ta sesja zostanie zatrzymana, "
            + "a jej zapis parowania usunięty. Dotychczasowa sesja Spotify i biblioteka pozostaną bez zmian.",
            "Odłącz Spotify — Librespot", MessageBoxButton.YesNo, MessageBoxImage.Warning,
            MessageBoxResult.No) != MessageBoxResult.Yes) return;
        try
        {
            disconnect();
            Disconnected = true;
            CompletionAnnouncement = "Odłączono tylko Spotify — Librespot";
            UpdateStatus();
            OperationStatusText.Announce(CompletionAnnouncement);
        }
        catch (Exception)
        {
            OperationStatusText.Announce("Nie udało się usunąć parowania Spotify — Librespot");
        }
    }

    private void CatalogAccount_Click(object sender, RoutedEventArgs e)
    {
        if (pairingCancellation is not null) return;
        OpenCatalogRequested = true;
        Close();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}

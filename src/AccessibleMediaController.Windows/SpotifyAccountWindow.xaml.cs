using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Core.Spotify;
using AccessibleMediaController.Windows.Services;

namespace AccessibleMediaController.Windows;

public partial class SpotifyAccountWindow : Controls.AccessibleWindow
{
    private readonly SpotifySettings settings;
    private readonly SpotifyIntegrationService integration;
    private readonly CancellationTokenSource cancellation = new();
    private bool busy;

    internal SpotifyAccountWindow(SpotifySettings settings, SpotifyIntegrationService integration)
    {
        InitializeComponent();
        this.settings = settings;
        this.integration = integration;
        ClientIdBox.Text = settings.ClientId;
        RedirectUriBox.Text = settings.RedirectUri;
        CountryCodeBox.Text = settings.CountryCode;
        UpdateStatus(announce: false);
        Loaded += (_, _) => Dispatcher.BeginInvoke(
            () =>
            {
                // Bez identyfikatora nic się nie uda, więc kursor staje tam,
                // gdzie trzeba coś wpisać. Gdy konto już działa, staje na
                // przycisku logowania, a nie w polu, które ma zostać bez zmian.
                FrameworkElement target = string.IsNullOrWhiteSpace(ClientIdBox.Text)
                    ? ClientIdBox
                    : LoginButton;
                target.Focus();
            },
            DispatcherPriority.ContextIdle);
    }

    public bool Changed { get; private set; }
    public bool Disconnected { get; private set; }
    public string? CompletionAnnouncement { get; private set; }
    // Pobrana biblioteka. Null znaczy "nie pobierano", a nie "pusta" - okno
    // wywolujace musi te dwa przypadki rozroznic, inaczej zamkniecie okna
    // wyczyscilo by sesje.
    internal IReadOnlyList<MediaItem>? SynchronizedItems { get; private set; }
    public bool SynchronizedCatalogComplete { get; private set; }
    public event EventHandler? SettingsApplied;
    public event EventHandler? PlaybackAccountRequested;

    internal void ConfigurePlaybackPairing(bool available) =>
        PlaybackAccountButton.Visibility = available ? Visibility.Visible : Visibility.Collapsed;

    internal void AnnouncePlaybackAccountResult(string message) => OperationStatusText.Announce(message);

    private void PlaybackAccount_Click(object sender, RoutedEventArgs e)
    {
        if (!busy) PlaybackAccountRequested?.Invoke(this, EventArgs.Empty);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!TryApplySettings(out var message))
        {
            OperationStatusText.Announce(message);
            return;
        }
        MarkSettingsApplied();
        CompletionAnnouncement = "Zapisano ustawienia Spotify";
        OperationStatusText.Announce(CompletionAnnouncement);
    }

    private async void Login_Click(object sender, RoutedEventArgs e)
    {
        if (!TryApplySettings(out var message))
        {
            OperationStatusText.Announce(message);
            return;
        }
        MarkSettingsApplied();
        await RunAsync(async () =>
        {
            OperationStatusText.Announce("Otwieranie bezpiecznego logowania Spotify w przeglądarce");
            var profile = await integration.LoginAsync(cancellation.Token);
            Changed = true;
            CompletionAnnouncement = DescribeProfile(profile, loggedInNow: true);
            OperationStatusText.Announce(CompletionAnnouncement);
            // Zaraz po zalogowaniu pobieramy biblioteke od razu. Samo
            // "zalogowano" przy pustej sesji wyglada jak awaria - i tak
            // wygladalo, dopoki tego nie bylo.
            OperationStatusText.Announce("Zalogowano. Pobieranie biblioteki Spotify");
            await SynchronizeCoreAsync();
        });
    }

    private async void Sync_Click(object sender, RoutedEventArgs e)
    {
        if (!TryApplySettings(out var message))
        {
            OperationStatusText.Announce(message);
            return;
        }
        MarkSettingsApplied();
        await RunAsync(SynchronizeCoreAsync);
    }

    private async Task SynchronizeCoreAsync()
    {
        OperationStatusText.Announce(
            "Pobieranie biblioteki Spotify. Przy pierwszym razie może to potrwać kilka minut; nie zamykaj tego okna.");
        var result = await integration.SynchronizeAsync(cancellation.Token);
        SynchronizedItems = result.Items;
        SynchronizedCatalogComplete = result.IsComplete;
        Changed = true;
        var account = string.IsNullOrWhiteSpace(result.AccountDisplayName)
            ? string.Empty
            : $" Konto: {result.AccountDisplayName}.";
        var warnings = result.Warnings.Count == 0
            ? string.Empty
            : $" Pobrano niepełnie: {string.Join("; ", result.Warnings)}";
        CompletionAnnouncement = $"Pobrano bibliotekę Spotify: {result.Items.Count} pozycji.{account}{warnings}";
        OperationStatusText.Announce(CompletionAnnouncement);
    }

    private async void CheckAccount_Click(object sender, RoutedEventArgs e)
    {
        if (!TryApplySettings(out var message))
        {
            OperationStatusText.Announce(message);
            return;
        }
        await RunAsync(async () =>
        {
            OperationStatusText.Announce("Sprawdzanie konta Spotify");
            var profile = await integration.GetProfileAsync(cancellation.Token);
            Changed = true;
            CompletionAnnouncement = DescribeProfile(profile, loggedInNow: false);
            OperationStatusText.Announce(CompletionAnnouncement);
        });
    }

    /// <summary>
    /// Mówi wprost, czy odtwarzanie w AMC będzie możliwe. Rodzaj konta
    /// decyduje o tym bardziej niż samo udane logowanie, a "zalogowano" bez
    /// tej informacji byłoby obietnicą, której program może nie spełnić.
    /// </summary>
    private string DescribeProfile(SpotifyAccountProfile profile, bool loggedInNow)
    {
        var opening = loggedInNow ? "Zalogowano konto Spotify" : "Konto Spotify";
        var name = string.IsNullOrWhiteSpace(profile.DisplayName)
            ? string.Empty
            : $": {profile.DisplayName}";
        var plan = profile.IsPremium
            ? " Konto Premium, więc odtwarzanie wewnątrz AMC jest możliwe."
            : string.IsNullOrWhiteSpace(profile.Product)
                ? " Nie udało się ustalić rodzaju konta, więc nie wiadomo, czy odtwarzanie w AMC zadziała."
                : $" Rodzaj konta: {profile.Product}. Odtwarzanie wewnątrz AMC wymaga Premium i na tym koncie nie zagra;"
                    + " zostaje biblioteka i sterowanie oficjalną aplikacją Spotify.";
        // Zakres przyznany przez Spotify sprawdzamy osobno od rodzaju konta:
        // konto Premium, któremu użytkownik odmówił prawa do odtwarzania na
        // stronie zgody, też nie zagra, a wyglądałoby na gotowe.
        var scopeWarning = SpotifyScopes.AllowsPlayback(settings.GrantedScope)
            ? string.Empty
            : " Spotify nie przyznał prawa do odtwarzania, więc wbudowany odtwarzacz nie wstanie.";
        return $"{opening}{name}.{plan}{scopeWarning}";
    }

    private void Disconnect_Click(object sender, RoutedEventArgs e)
    {
        if (Services.AccessibleDialog.Show(
                this,
                "Odłączyć konto Spotify? Bezpiecznie zapisany token zostanie usunięty z Menedżera "
                    + "poświadczeń Windows. Sesja Spotify wróci do trybu demonstracyjnego, dopóki nie "
                    + "zalogujesz się ponownie. Pliki lokalne, radio, podcasty i TIDAL pozostaną bez zmian.",
                "Odłącz konto Spotify",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No) != MessageBoxResult.Yes) return;
        try
        {
            integration.Disconnect();
            Disconnected = true;
            Changed = true;
            CompletionAnnouncement = "Odłączono konto Spotify. Przywrócono tryb demonstracyjny";
            UpdateStatus(announce: true);
        }
        catch (Exception exception)
        {
            DiagnosticLog.Error("spotify-auth", "Nie udało się odłączyć konta Spotify.", exception);
            OperationStatusText.Announce("Nie udało się bezpiecznie usunąć logowania Spotify");
        }
    }

    private void DeveloperPanel_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo("https://developer.spotify.com/dashboard") { UseShellExecute = true });
    }

    private void Instructions_Click(object sender, RoutedEventArgs e)
    {
        new InformationWindow(SpotifySetupInstructions.Text, windowTitle: "Pierwsze logowanie Spotify")
        {
            Owner = this
        }.ShowDialog();
        if (IsVisible && IsActive) InstructionsButton.Focus();
    }

    private async Task RunAsync(Func<Task> action)
    {
        if (busy) return;
        busy = true;
        SetButtonsEnabled(false);
        try
        {
            await action();
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            DiagnosticLog.Warning(
                "spotify-auth",
                "Operacja Spotify przerwana zamknięciem okna konta.");
            CompletionAnnouncement =
                "Operacja Spotify przerwana zamknięciem okna. Otwórz okno konta Spotify i spróbuj ponownie, "
                    + "a okno zostaw otwarte do końca.";
        }
        catch (Exception exception)
        {
            DiagnosticLog.Error("spotify", "Operacja Spotify nie powiodła się.", exception);
            CompletionAnnouncement = exception.Message;
            OperationStatusText.Announce(exception.Message);
        }
        finally
        {
            busy = false;
            SetButtonsEnabled(true);
            UpdateStatus(announce: false, preserveOperationText: true);
        }
    }

    private bool TryApplySettings(out string message)
    {
        var clientId = ClientIdBox.Text.Trim();
        var country = CountryCodeBox.Text.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(clientId))
        {
            message = "Wpisz identyfikator aplikacji Spotify";
            ClientIdBox.Focus();
            return false;
        }
        if (!Uri.TryCreate(RedirectUriBox.Text.Trim(), UriKind.Absolute, out var redirect)
            || !redirect.IsLoopback
            || redirect.Scheme != Uri.UriSchemeHttp)
        {
            message = "Adres powrotu musi być lokalnym adresem HTTP";
            RedirectUriBox.Focus();
            return false;
        }
        // Powiedziane tutaj, a nie dopiero na stronie Spotify: ten warunek
        // łatwo naruszyć, a błąd Spotify nie wyjaśnia przyczyny.
        if (redirect.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            message = "Spotify nie przyjmuje adresu z nazwą localhost. Wpisz 127.0.0.1 zamiast localhost";
            RedirectUriBox.Focus();
            return false;
        }
        if (country.Length != 2 || country.Any(character => character is < 'A' or > 'Z'))
        {
            message = "Kod kraju musi mieć dwie litery, na przykład PL";
            CountryCodeBox.Focus();
            return false;
        }
        if (!redirect.AbsolutePath.EndsWith("/", StringComparison.Ordinal))
        {
            var builder = new UriBuilder(redirect) { Path = redirect.AbsolutePath + "/" };
            redirect = builder.Uri;
            RedirectUriBox.Text = redirect.AbsoluteUri;
        }
        settings.ClientId = clientId;
        settings.RedirectUri = redirect.AbsoluteUri;
        settings.CountryCode = country;
        message = string.Empty;
        return true;
    }

    private void MarkSettingsApplied()
    {
        Changed = true;
        SettingsApplied?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateStatus(bool announce, bool preserveOperationText = false)
    {
        var status = integration.HasStoredLogin
            ? string.IsNullOrWhiteSpace(settings.AccountDisplayName)
                ? "Logowanie Spotify jest zapisane"
                : $"Połączono z kontem {settings.AccountDisplayName}"
            : "Konto Spotify nie jest połączone";
        if (integration.HasStoredLogin && !string.IsNullOrWhiteSpace(settings.AccountProduct))
        {
            status += settings.AccountProduct.Equals("premium", StringComparison.OrdinalIgnoreCase)
                ? ". Konto Premium"
                : $". Rodzaj konta: {settings.AccountProduct}, bez odtwarzania wewnątrz AMC";
        }
        if (preserveOperationText && !string.IsNullOrWhiteSpace(OperationStatusText.Text)) return;
        if (announce) OperationStatusText.Announce(status);
        else OperationStatusText.Text = status;
    }

    private void SetButtonsEnabled(bool enabled)
    {
        SaveButton.IsEnabled = enabled;
        LoginButton.IsEnabled = enabled;
        PlaybackAccountButton.IsEnabled = enabled;
        CheckAccountButton.IsEnabled = enabled;
        DisconnectButton.IsEnabled = enabled;
        DeveloperPanelButton.IsEnabled = enabled;
        InstructionsButton.IsEnabled = enabled;
    }

    /// <summary>
    /// Nie pozwala zamknąć okna w trakcie logowania. Zamknięcie anulowałoby
    /// operację, a przy czytniku ekranu łatwo nacisnąć Escape, nie wiedząc,
    /// że coś jeszcze trwa.
    /// </summary>
    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!busy) return;
        e.Cancel = true;
        OperationStatusText.Announce(
            "Trwa operacja Spotify. Poczekaj na jej zakończenie; zamknięcie teraz przerwałoby logowanie.");
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        cancellation.Cancel();
        cancellation.Dispose();
    }
}

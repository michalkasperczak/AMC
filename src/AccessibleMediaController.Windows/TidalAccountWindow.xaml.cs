using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Windows.Services;

namespace AccessibleMediaController.Windows;

public partial class TidalAccountWindow : Controls.AccessibleWindow
{
    private readonly TidalSettings settings;
    private readonly TidalIntegrationService integration;
    private readonly CancellationTokenSource cancellation = new();
    private bool busy;

    internal TidalAccountWindow(TidalSettings settings, TidalIntegrationService integration)
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
                FrameworkElement target = string.IsNullOrWhiteSpace(ClientIdBox.Text)
                    ? ClientIdBox
                    : SyncButton;
                target.Focus();
            },
            DispatcherPriority.ContextIdle);
    }

    public bool Changed { get; private set; }
    public bool Disconnected { get; private set; }
    public IReadOnlyList<MediaItem>? SynchronizedItems { get; private set; }
    public bool SynchronizedCatalogComplete { get; private set; }
    public string? CompletionAnnouncement { get; private set; }
    public event EventHandler? SettingsApplied;

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!TryApplySettings(out var message))
        {
            OperationStatusText.Announce(message);
            return;
        }
        MarkSettingsApplied();
        CompletionAnnouncement = "Zapisano ustawienia TIDAL";
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
            OperationStatusText.Announce("Otwieranie bezpiecznego logowania TIDAL w przeglądarce");
            await integration.LoginAsync(cancellation.Token);
            OperationStatusText.Announce("Zalogowano. Synchronizowanie kolekcji TIDAL");
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
        var result = await integration.SynchronizeAsync(cancellation.Token);
        SynchronizedItems = result.Items;
        SynchronizedCatalogComplete = result.IsComplete;
        Changed = true;
        var account = string.IsNullOrWhiteSpace(result.AccountDisplayName)
            ? string.Empty
            : $" Konto: {result.AccountDisplayName}.";
        var warnings = result.Warnings.Count == 0
            ? string.Empty
            : $" Niepełna synchronizacja: {string.Join("; ", result.Warnings)}.";
        CompletionAnnouncement = $"Zsynchronizowano TIDAL: {result.Items.Count} elementów.{account}{warnings}";
        OperationStatusText.Announce(CompletionAnnouncement);
    }

    private void Disconnect_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(
                this,
                "Odłączyć konto TIDAL? Bezpiecznie zapisane tokeny zostaną usunięte z Menedżera poświadczeń Windows. Lokalne dane AMC pozostaną bez zmian.",
                "Odłącz konto TIDAL",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No) != MessageBoxResult.Yes) return;
        try
        {
            integration.Disconnect();
            Disconnected = true;
            Changed = true;
            SynchronizedItems = null;
            CompletionAnnouncement = "Odłączono konto TIDAL. Przywrócono tryb demonstracyjny";
            UpdateStatus(announce: true);
        }
        catch (Exception exception)
        {
            DiagnosticLog.Error("tidal-auth", "Nie udało się odłączyć konta TIDAL.", exception);
            OperationStatusText.Announce("Nie udało się bezpiecznie usunąć logowania TIDAL");
        }
    }

    private void DeveloperPanel_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo("https://developer.tidal.com/") { UseShellExecute = true });
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
        }
        catch (Exception exception)
        {
            DiagnosticLog.Error("tidal", "Operacja TIDAL nie powiodła się.", exception);
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
            message = "Wpisz identyfikator aplikacji TIDAL";
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
                ? "Logowanie TIDAL jest zapisane"
                : $"Połączono z kontem {settings.AccountDisplayName}"
            : "Konto TIDAL nie jest połączone";
        if (settings.LastSuccessfulSyncUtcTicks > 0)
        {
            var local = new DateTime(settings.LastSuccessfulSyncUtcTicks, DateTimeKind.Utc).ToLocalTime();
            status += $". Ostatnia synchronizacja: {local:g}";
        }
        if (preserveOperationText && !string.IsNullOrWhiteSpace(OperationStatusText.Text)) return;
        if (announce) OperationStatusText.Announce(status);
        else OperationStatusText.Text = status;
    }

    private void SetButtonsEnabled(bool enabled)
    {
        SaveButton.IsEnabled = enabled;
        LoginButton.IsEnabled = enabled;
        SyncButton.IsEnabled = enabled;
        DisconnectButton.IsEnabled = enabled;
        DeveloperPanelButton.IsEnabled = enabled;
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        cancellation.Cancel();
        cancellation.Dispose();
    }
}

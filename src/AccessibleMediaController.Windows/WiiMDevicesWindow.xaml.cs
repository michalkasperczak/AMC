using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Devices.WiiM;
using AccessibleMediaController.Windows.Services;

namespace AccessibleMediaController.Windows;

public partial class WiiMDevicesWindow : Window
{
    private readonly WiiMSettings settings;
    private readonly WiiMDeviceClient client;
    private readonly WiiMDiscoveryService discovery;
    private readonly Dictionary<string, WiiMDeviceSnapshot> snapshots =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> errors =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly CancellationTokenSource cancellation = new();
    private bool busy;

    public WiiMDevicesWindow(
        WiiMSettings settings,
        WiiMDeviceClient client,
        WiiMDiscoveryService discovery,
        IReadOnlyDictionary<string, WiiMDeviceSnapshot>? knownSnapshots = null)
    {
        InitializeComponent();
        this.settings = settings;
        this.client = client;
        this.discovery = discovery;
        if (knownSnapshots is not null)
        {
            foreach (var (id, snapshot) in knownSnapshots) snapshots[id] = snapshot;
        }
        ReloadRows(settings.SelectedDeviceId);
        Loaded += (_, _) => Dispatcher.BeginInvoke(FocusInitialControl, DispatcherPriority.ContextIdle);
    }

    public bool Changed { get; private set; }
    public IReadOnlyDictionary<string, WiiMDeviceSnapshot> Snapshots => snapshots;

    private WiiMDeviceRow? SelectedRow => DevicesList.SelectedItem as WiiMDeviceRow;

    private async void Discover_Click(object sender, RoutedEventArgs e)
    {
        await RunAsync(async () =>
        {
            OperationStatusText.Text = "Wykrywanie urządzeń WiiM w sieci lokalnej…";
            var addresses = await discovery.DiscoverAddressesAsync(cancellationToken: cancellation.Token);
            var found = 0;
            foreach (var address in addresses)
            {
                try
                {
                    var snapshot = await client.ReadSnapshotAsync(address, cancellation.Token);
                    Upsert(snapshot);
                    found++;
                }
                catch (Exception exception) when (IsConnectionFailure(exception))
                {
                    DiagnosticLog.Warning("wiim-discovery", $"Odrzucono urządzenie przy {address}: {exception.GetType().Name}.");
                }
            }
            ReloadRows(settings.SelectedDeviceId);
            OperationStatusText.Text = found switch
            {
                0 => "Nie wykryto urządzenia WiiM. Wpisz jego adres IP z aplikacji WiiM Home.",
                1 => "Wykryto jedno urządzenie WiiM.",
                _ => $"Wykryto urządzenia WiiM: {found}."
            };
            if (found > 0)
                _ = Dispatcher.BeginInvoke(FocusSelectedDevice, DispatcherPriority.ContextIdle);
        });
    }

    private async void Add_Click(object sender, RoutedEventArgs e)
    {
        var value = AddressBox.Text;
        if (!WiiMAddressPolicy.TryNormalize(value, out var address))
        {
            OperationStatusText.Text = "Podaj lokalny adres IP, na przykład 192.168.1.25.";
            AddressBox.Focus();
            Keyboard.Focus(AddressBox);
            return;
        }

        await RunAsync(async () =>
        {
            OperationStatusText.Text = $"Sprawdzanie urządzenia pod adresem {address}…";
            try
            {
                var snapshot = await client.ReadSnapshotAsync(address, cancellation.Token);
                Upsert(snapshot);
                AddressBox.Clear();
                ReloadRows(snapshot.Device.Id);
                OperationStatusText.Text = $"Dodano urządzenie {snapshot.Device.Name}.";
            }
            catch (Exception exception) when (IsConnectionFailure(exception))
            {
                OperationStatusText.Text = "Pod tym adresem nie udało się potwierdzić urządzenia WiiM. Sprawdź adres i połączenie z tą samą siecią.";
                DiagnosticLog.Warning("wiim-add", $"Nie udało się zweryfikować {address}: {exception.GetType().Name}.");
                AddressBox.Focus();
                Keyboard.Focus(AddressBox);
            }
        });
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedRow is not { } row) return;
        await RunAsync(async () =>
        {
            OperationStatusText.Text = $"Odświeżanie urządzenia {row.Device.DisplayName}…";
            try
            {
                var snapshot = await client.ReadSnapshotAsync(row.Device.Address, cancellation.Token);
                Upsert(snapshot);
                errors.Remove(row.Device.Id);
                ReloadRows(snapshot.Device.Id);
                OperationStatusText.Text = $"Odświeżono urządzenie {snapshot.Device.Name}.";
            }
            catch (Exception exception) when (IsConnectionFailure(exception))
            {
                errors[row.Device.Id] = "urządzenie obecnie niedostępne";
                ReloadRows(row.Device.Id);
                OperationStatusText.Text = $"Brak odpowiedzi urządzenia {row.Device.DisplayName}. Zapamiętany wpis pozostał bez zmian.";
                DiagnosticLog.Warning("wiim-refresh", $"Brak odpowiedzi {row.Device.Address}: {exception.GetType().Name}.");
            }
        });
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedRow is not { } row) return;
        var answer = MessageBox.Show(
            this,
            $"Usunąć urządzenie „{row.Device.DisplayName}” z AMC?\n\nNie zmieni to ustawień samego urządzenia ani aplikacji WiiM Home.",
            "Usuń urządzenie WiiM",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes) return;
        settings.Devices.RemoveAll(device => string.Equals(device.Id, row.Device.Id, StringComparison.OrdinalIgnoreCase));
        snapshots.Remove(row.Device.Id);
        errors.Remove(row.Device.Id);
        if (string.Equals(settings.SelectedDeviceId, row.Device.Id, StringComparison.OrdinalIgnoreCase))
            settings.SelectedDeviceId = settings.Devices.FirstOrDefault()?.Id;
        Changed = true;
        ReloadRows(settings.SelectedDeviceId);
        OperationStatusText.Text = $"Usunięto urządzenie {row.Device.DisplayName} z AMC.";
    }

    private void Activate_Click(object sender, RoutedEventArgs e) => ActivateSelectedDevice();

    private void ActivateSelectedDevice()
    {
        if (SelectedRow is not { } row)
        {
            OperationStatusText.Text = "Najpierw wybierz urządzenie WiiM.";
            return;
        }
        settings.SelectedDeviceId = row.Device.Id;
        Changed = true;
        ReloadRows(row.Device.Id);
        OperationStatusText.Text = $"Aktywne urządzenie: {row.Device.DisplayName}.";
        Dispatcher.BeginInvoke(FocusSelectedDevice, DispatcherPriority.ContextIdle);
    }

    private void Upsert(WiiMDeviceSnapshot snapshot)
    {
        var existing = settings.Devices.FirstOrDefault(device =>
            string.Equals(device.Id, snapshot.Device.Id, StringComparison.OrdinalIgnoreCase)
            || string.Equals(device.Address, snapshot.Device.Address, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            existing = new WiiMDeviceSettings();
            settings.Devices.Add(existing);
        }
        var previousId = existing.Id;
        existing.Id = snapshot.Device.Id;
        existing.Address = snapshot.Device.Address;
        existing.DisplayName = snapshot.Device.Name;
        existing.Model = snapshot.Device.Model;
        existing.Firmware = snapshot.Device.Firmware;
        existing.LastSeenUtcTicks = DateTime.UtcNow.Ticks;
        if (!string.IsNullOrWhiteSpace(previousId) && !string.Equals(previousId, existing.Id, StringComparison.OrdinalIgnoreCase))
        {
            snapshots.Remove(previousId);
            errors.Remove(previousId);
        }
        snapshots[existing.Id] = snapshot;
        errors.Remove(existing.Id);
        settings.SelectedDeviceId ??= existing.Id;
        Changed = true;
    }

    private void ReloadRows(string? preferredId)
    {
        preferredId ??= SelectedRow?.Device.Id;
        var rows = settings.Devices
            .OrderBy(device => device.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(device => new WiiMDeviceRow(
                device,
                snapshots.GetValueOrDefault(device.Id),
                errors.GetValueOrDefault(device.Id),
                string.Equals(device.Id, settings.SelectedDeviceId, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        DevicesList.ItemsSource = rows;
        DevicesList.SelectedItem = rows.FirstOrDefault(row =>
            string.Equals(row.Device.Id, preferredId, StringComparison.OrdinalIgnoreCase));
        if (DevicesList.SelectedItem is null && rows.Length > 0) DevicesList.SelectedIndex = 0;
        UpdateSelection();
    }

    private async Task RunAsync(Func<Task> action)
    {
        if (busy) return;
        SetBusy(true);
        try
        {
            await action();
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            OperationStatusText.Text = "Operacja WiiM nie powiodła się. Sprawdź połączenie z siecią lokalną i spróbuj ponownie.";
            DiagnosticLog.Warning("wiim-manager", $"Operacja nie powiodła się: {exception.GetType().Name}.");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool value)
    {
        busy = value;
        DiscoverButton.IsEnabled = !value;
        AddButton.IsEnabled = !value;
        AddressBox.IsEnabled = !value;
        ActivateButton.IsEnabled = !value && SelectedRow is not null;
        RefreshButton.IsEnabled = !value && SelectedRow is not null;
        RemoveButton.IsEnabled = !value && SelectedRow is not null;
    }

    private void DevicesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateSelection();
    }

    private void UpdateSelection()
    {
        RefreshButton.IsEnabled = !busy && SelectedRow is not null;
        RemoveButton.IsEnabled = !busy && SelectedRow is not null;
        ActivateButton.IsEnabled = !busy && SelectedRow is not null;
        DeviceDetailsText.Text = SelectedRow?.Details
            ?? "Brak zapisanych urządzeń. Wykryj urządzenie w sieci albo wpisz jego adres IP.";
    }

    private void FocusInitialControl()
    {
        if (DevicesList.SelectedItem is not null)
        {
            DevicesList.ScrollIntoView(DevicesList.SelectedItem);
            DevicesList.UpdateLayout();
            if (DevicesList.ItemContainerGenerator.ContainerFromItem(DevicesList.SelectedItem) is ListBoxItem item)
            {
                item.Focus();
                Keyboard.Focus(item);
                return;
            }
        }
        DiscoverButton.Focus();
        Keyboard.Focus(DiscoverButton);
    }

    private void FocusSelectedDevice()
    {
        if (DevicesList.SelectedItem is null) return;
        DevicesList.ScrollIntoView(DevicesList.SelectedItem);
        DevicesList.UpdateLayout();
        if (DevicesList.ItemContainerGenerator.ContainerFromItem(DevicesList.SelectedItem) is ListBoxItem item)
        {
            item.Focus();
            Keyboard.Focus(item);
            return;
        }
        Keyboard.Focus(DevicesList);
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F5 && Keyboard.Modifiers == ModifierKeys.None)
        {
            if (SelectedRow is not null) Refresh_Click(this, new RoutedEventArgs());
            else Discover_Click(this, new RoutedEventArgs());
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Enter
            && Keyboard.Modifiers == ModifierKeys.None
            && DevicesList.IsKeyboardFocusWithin)
        {
            ActivateSelectedDevice();
            e.Handled = true;
            return;
        }
        if (e.Key != Key.Escape || busy) return;
        Close();
        e.Handled = true;
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        cancellation.Cancel();
        cancellation.Dispose();
    }

    private static bool IsConnectionFailure(Exception exception) => exception is
        HttpRequestException or TaskCanceledException or IOException or SocketException or FormatException or ArgumentException;
}

public sealed record WiiMDeviceRow(
    WiiMDeviceSettings Device,
    WiiMDeviceSnapshot? Snapshot,
    string? Error,
    bool IsActive = false)
{
    public string NavigationText => Device.DisplayName;

    public string Label
    {
        get
        {
            var active = IsActive ? ", aktywne" : string.Empty;
            if (!string.IsNullOrWhiteSpace(Error)) return $"{Device.DisplayName}{active}, {Error}";
            if (Snapshot is null) return $"{Device.DisplayName}{active}, zapisane, stan jeszcze nieodświeżony";
            return $"{Device.DisplayName}{active}, dostępne, {Snapshot.PlaybackSummary}";
        }
    }

    public string Details
    {
        get
        {
            var parts = new List<string>
            {
                $"Nazwa: {Device.DisplayName}.",
                $"Model: {(string.IsNullOrWhiteSpace(Device.Model) ? "nierozpoznany" : Device.Model)}.",
                $"Adres IP: {Device.Address}."
            };
            if (!string.IsNullOrWhiteSpace(Device.Firmware)) parts.Add($"Oprogramowanie urządzenia: {Device.Firmware}.");
            if (Snapshot is not null)
            {
                parts.Add($"Stan: {Snapshot.PlaybackSummary}.");
                if (!string.IsNullOrWhiteSpace(Snapshot.Track.Album)) parts.Add($"Album: {Snapshot.Track.Album}.");
                if (Snapshot.Track.SampleRateHz is { } sampleRate)
                    parts.Add($"Częstotliwość próbkowania: {sampleRate / 1000d:0.#} kHz.");
                if (Snapshot.Track.BitDepth is { } bitDepth) parts.Add($"Głębia: {bitDepth} bit.");
                parts.Add($"Zajęte presety urządzenia: {Snapshot.Presets.Count} z 12.");
                parts.Add($"Multiroom: {WiiMGroupPresentation.Summary(Snapshot.Group)}.");
            }
            else if (!string.IsNullOrWhiteSpace(Error)) parts.Add($"Stan: {Error}.");
            return string.Join(' ', parts);
        }
    }

    public override string ToString() => Label;
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace AccessibleMediaController.Core.Devices.WiiM;

public sealed class WiiMDeviceClient : IDisposable
{
    private const int MaximumResponseBytes = 1024 * 1024;
    private readonly HttpClient client;

    public WiiMDeviceClient()
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            ConnectTimeout = TimeSpan.FromSeconds(3),
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            SslOptions =
            {
                // WiiM exposes its documented API over a certificate issued by
                // the device itself. This dedicated client can contact only a
                // validated local IP and never follows redirects.
                RemoteCertificateValidationCallback = (_, _, _, _) => true
            }
        };
        client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(6),
            MaxResponseContentBufferSize = MaximumResponseBytes
        };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("AMC", "0.1"));
    }

    public async Task<WiiMDeviceSnapshot> ReadSnapshotAsync(
        string address,
        CancellationToken cancellationToken = default)
    {
        if (!WiiMAddressPolicy.TryNormalize(address, out var normalized))
            throw new ArgumentException("Podaj lokalny adres IP urządzenia WiiM.", nameof(address));

        var deviceJson = await ReadTextAsync(normalized, "getStatusEx", cancellationToken)
            .ConfigureAwait(false);
        var device = WiiMApiParser.ParseDeviceInformation(deviceJson, normalized);
        var playbackTask = TryReadTextAsync(normalized, "getPlayerStatus", cancellationToken);
        var metadataTask = TryReadTextAsync(normalized, "getMetaInfo", cancellationToken);
        var presetsTask = TryReadTextAsync(normalized, "getPresetInfo", cancellationToken);
        await Task.WhenAll(playbackTask, metadataTask, presetsTask).ConfigureAwait(false);
        return new WiiMDeviceSnapshot(
            device,
            WiiMApiParser.ParsePlaybackInformation(playbackTask.Result),
            WiiMApiParser.ParseTrackInformation(metadataTask.Result),
            WiiMApiParser.ParsePresets(presetsTask.Result));
    }

    public Task TogglePlayPauseAsync(string address, CancellationToken cancellationToken = default) =>
        SendCommandAsync(address, WiiMCommands.TogglePlayPause, cancellationToken);

    public Task PreviousAsync(string address, CancellationToken cancellationToken = default) =>
        SendCommandAsync(address, WiiMCommands.Previous, cancellationToken);

    public Task NextAsync(string address, CancellationToken cancellationToken = default) =>
        SendCommandAsync(address, WiiMCommands.Next, cancellationToken);

    public Task StopAsync(string address, CancellationToken cancellationToken = default) =>
        SendCommandAsync(address, WiiMCommands.Stop, cancellationToken);

    public Task SetVolumeAsync(string address, int volume, CancellationToken cancellationToken = default) =>
        SendCommandAsync(address, WiiMCommands.SetVolume(volume), cancellationToken);

    public Task SetMutedAsync(string address, bool muted, CancellationToken cancellationToken = default) =>
        SendCommandAsync(address, WiiMCommands.SetMuted(muted), cancellationToken);

    public Task SeekAsync(string address, TimeSpan position, CancellationToken cancellationToken = default) =>
        SendCommandAsync(address, WiiMCommands.Seek(position), cancellationToken);

    public Task ActivatePresetAsync(string address, int presetNumber, CancellationToken cancellationToken = default) =>
        SendCommandAsync(address, WiiMCommands.ActivatePreset(presetNumber), cancellationToken);

    private async Task SendCommandAsync(
        string address,
        string command,
        CancellationToken cancellationToken)
    {
        var response = await ReadTextAsync(address, command, cancellationToken).ConfigureAwait(false);
        if (WiiMCommands.ResponseIndicatesFailure(response))
            throw new IOException("Urządzenie WiiM odrzuciło polecenie.");
    }

    private async Task<string?> TryReadTextAsync(
        string address,
        string command,
        CancellationToken cancellationToken)
    {
        try
        {
            return await ReadTextAsync(address, command, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is HttpRequestException
            or TaskCanceledException
            or IOException
            or FormatException)
        {
            return null;
        }
    }

    private async Task<string> ReadTextAsync(
        string address,
        string command,
        CancellationToken cancellationToken)
    {
        var uri = WiiMAddressPolicy.CreateApiUri(address, command);
        using var request = new HttpRequestMessage(HttpMethod.Get, uri)
        {
            Version = HttpVersion.Version11,
            VersionPolicy = HttpVersionPolicy.RequestVersionOrLower
        };
        using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is > MaximumResponseBytes)
            throw new IOException("Odpowiedź urządzenia jest zbyt duża.");
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        using var memory = new MemoryStream();
        var buffer = new byte[8192];
        while (true)
        {
            var count = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (count == 0) break;
            if (memory.Length + count > MaximumResponseBytes)
                throw new IOException("Odpowiedź urządzenia jest zbyt duża.");
            memory.Write(buffer, 0, count);
        }
        return Encoding.UTF8.GetString(memory.ToArray()).TrimStart('\uFEFF');
    }

    public void Dispose() => client.Dispose();
}

public static class WiiMCommands
{
    public const string TogglePlayPause = "setPlayerCmd:onepause";
    public const string Previous = "setPlayerCmd:prev";
    public const string Next = "setPlayerCmd:next";
    public const string Stop = "setPlayerCmd:stop";

    public static string SetVolume(int volume) =>
        $"setPlayerCmd:vol:{Math.Clamp(volume, 0, 100)}";

    public static string SetMuted(bool muted) =>
        $"setPlayerCmd:mute:{(muted ? 1 : 0)}";

    public static string Seek(TimeSpan position)
    {
        var seconds = Math.Clamp((long)Math.Round(position.TotalSeconds), 0, int.MaxValue);
        return $"setPlayerCmd:seek:{seconds}";
    }

    public static string ActivatePreset(int presetNumber)
    {
        if (presetNumber is < 1 or > 12)
            throw new ArgumentOutOfRangeException(nameof(presetNumber));
        return $"MCUKeyShortClick:{presetNumber}";
    }

    public static bool ResponseIndicatesFailure(string? response)
    {
        if (string.IsNullOrWhiteSpace(response)) return false;
        var value = response.Trim();
        if (value.Equals("FAIL", StringComparison.OrdinalIgnoreCase)
            || value.Equals("FAILED", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return value.Contains("\"status\"", StringComparison.OrdinalIgnoreCase)
            && value.Contains("failed", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class WiiMDiscoveryService
{
    private static readonly IPEndPoint MulticastEndpoint =
        new(IPAddress.Parse("239.255.255.250"), 1900);

    public async Task<IReadOnlyList<string>> DiscoverAddressesAsync(
        TimeSpan? duration = null,
        CancellationToken cancellationToken = default)
    {
        var addresses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var localAddresses = NetworkInterface.GetAllNetworkInterfaces()
            .Where(adapter => adapter.OperationalStatus == OperationalStatus.Up
                && adapter.NetworkInterfaceType is not NetworkInterfaceType.Loopback
                && adapter.NetworkInterfaceType is not NetworkInterfaceType.Tunnel)
            .SelectMany(adapter => adapter.GetIPProperties().UnicastAddresses)
            .Select(entry => entry.Address)
            .Where(address => address.AddressFamily == AddressFamily.InterNetwork)
            .Distinct()
            .ToArray();
        if (localAddresses.Length == 0) localAddresses = [IPAddress.Any];

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(duration ?? TimeSpan.FromSeconds(3));
        var tasks = localAddresses.Select(address => DiscoverOnInterfaceAsync(address, addresses, timeout.Token));
        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Normal end of the bounded discovery window.
        }
        return addresses.OrderBy(address => address, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static bool TryParseResponse(string response, out string address)
    {
        address = string.Empty;
        foreach (var line in response.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = line.IndexOf(':');
            if (separator <= 0
                || !line[..separator].Trim().Equals("LOCATION", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            var location = line[(separator + 1)..].Trim();
            if (!Uri.TryCreate(location, UriKind.Absolute, out var uri)) return false;
            return WiiMAddressPolicy.TryNormalize(uri.Host, out address);
        }
        return false;
    }

    private static async Task DiscoverOnInterfaceAsync(
        IPAddress localAddress,
        HashSet<string> addresses,
        CancellationToken cancellationToken)
    {
        using var socket = new UdpClient(new IPEndPoint(localAddress, 0));
        socket.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 2);
        var message = Encoding.ASCII.GetBytes(
            "M-SEARCH * HTTP/1.1\r\n" +
            "HOST: 239.255.255.250:1900\r\n" +
            "MAN: \"ssdp:discover\"\r\n" +
            "MX: 2\r\n" +
            "ST: urn:schemas-upnp-org:device:MediaRenderer:1\r\n\r\n");
        await socket.SendAsync(message, MulticastEndpoint, cancellationToken).ConfigureAwait(false);
        while (!cancellationToken.IsCancellationRequested)
        {
            UdpReceiveResult result;
            try
            {
                result = await socket.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            var response = Encoding.UTF8.GetString(result.Buffer);
            if (!TryParseResponse(response, out var address)) continue;
            lock (addresses) addresses.Add(address);
        }
    }
}

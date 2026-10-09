using System.Text.Json;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Devices.WiiM;

namespace AccessibleMediaController.LiteHost.Protocol;

/// <summary>
/// Waski kontrakt zdalnego sterowania WiiM. Python przekazuje wylacznie
/// stabilne Id zapisanego urzadzenia; adres lokalny pozostaje w hoście C#.
/// </summary>
public static class LiteWiiMContract
{
    public const string DevicesOperation = "wiim.devices";
    public const string SnapshotOperation = "wiim.snapshot";
    public const string TransportOperation = "wiim.transport";

    public static string ReadDeviceId(JsonElement args) =>
        LiteArgs.RequireText(args, "deviceId").Trim();

    public static LiteWiiMTransportRequest ReadTransportRequest(JsonElement args)
    {
        var deviceId = ReadDeviceId(args);
        var command = LiteArgs.RequireText(args, "command").Trim();
        if (command is not (
                "toggle" or "previous" or "next"
                or "setVolume"
                or "volumeUp5" or "volumeDown5"
                or "volumeUp1" or "volumeDown1"))
        {
            throw new LiteRequestException("Nieznane polecenie sterowania WiiM.");
        }
        int? volume = null;
        if (command == "setVolume")
        {
            if (!args.TryGetProperty("volume", out var rawVolume)
                || rawVolume.ValueKind != JsonValueKind.Number
                || !rawVolume.TryGetInt32(out var requestedVolume)
                || requestedVolume is < 0 or > 100)
            {
                throw new LiteRequestException(
                    "Głośność WiiM musi być liczbą od 0 do 100.");
            }
            volume = requestedVolume;
        }
        return new LiteWiiMTransportRequest(deviceId, command, volume);
    }

    public static object CreateDevicesResult(WiiMSettings settings)
    {
        var selected = settings.SelectedDeviceId;
        var items = (settings.Devices ?? [])
            .Where(device => !string.IsNullOrWhiteSpace(device.Id)
                && WiiMAddressPolicy.TryNormalize(device.Address, out _))
            .Select(device => new
            {
                itemId = "wiim:" + device.Id.Trim(),
                deviceId = device.Id.Trim(),
                title = UserTitle(device),
                detail = UserDetail(device),
                selected = string.Equals(
                    device.Id, selected, StringComparison.OrdinalIgnoreCase)
            })
            .ToArray();
        return new { items };
    }

    public static object CreateSnapshotResult(
        WiiMDeviceSettings device,
        WiiMDeviceSnapshot snapshot,
        string? message = null)
    {
        var track = snapshot.Track;
        var playback = snapshot.Playback;
        return new
        {
            itemId = "wiim:" + device.Id.Trim(),
            deviceId = device.Id.Trim(),
            deviceTitle = UserTitle(device),
            title = Useful(track.Title),
            subtitle = Useful(track.Subtitle),
            artist = Useful(track.Artist),
            album = Useful(track.Album),
            source = Useful(playback.Source),
            playbackState = Useful(playback.State),
            volume = Math.Clamp(playback.Volume, 0, 100),
            muted = playback.Muted,
            positionSeconds = Math.Max(0, playback.Position.TotalSeconds),
            durationSeconds = playback.Duration > TimeSpan.Zero
                ? playback.Duration.TotalSeconds
                : (double?)null,
            message = string.IsNullOrWhiteSpace(message)
                ? SnapshotAnnouncement(device, snapshot)
                : message.Trim()
        };
    }

    private static string SnapshotAnnouncement(
        WiiMDeviceSettings device,
        WiiMDeviceSnapshot snapshot)
    {
        var parts = new List<string>();
        AddDistinct(parts, Useful(snapshot.Track.Title));
        AddDistinct(parts, Useful(snapshot.Track.Subtitle));
        AddDistinct(parts, Useful(snapshot.Track.Artist));
        AddDistinct(parts, Useful(snapshot.Track.Album));
        if (parts.Count == 0) AddDistinct(parts, UserTitle(device));
        AddDistinct(parts, Useful(snapshot.Playback.State));
        if (snapshot.Playback.Muted) parts.Add("wyciszone");
        parts.Add($"głośność {Math.Clamp(snapshot.Playback.Volume, 0, 100)}%");
        return string.Join(", ", parts);
    }

    private static void AddDistinct(List<string> parts, string value)
    {
        if (value.Length == 0 || parts.Any(part => string.Equals(
                part, value, StringComparison.CurrentCultureIgnoreCase))) return;
        parts.Add(value);
    }

    private static string UserTitle(WiiMDeviceSettings device)
    {
        var title = Useful(device.DisplayName);
        // Starsza normalizacja profilu potrafiła utworzyć nazwę
        // "WiiM 192.168...". Adres jest szczegółem połączenia, nie nazwą
        // dostępną pozycji listy.
        return title.Length == 0
            || (!string.IsNullOrWhiteSpace(device.Address)
                && title.Contains(device.Address.Trim(), StringComparison.OrdinalIgnoreCase))
            ? "Urządzenie WiiM"
            : title;
    }

    private static string UserDetail(WiiMDeviceSettings device)
    {
        var parts = new[] { device.Model, device.Firmware }
            .Select(Useful)
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.CurrentCultureIgnoreCase);
        return string.Join(", ", parts);
    }

    private static string Useful(string? value)
    {
        var text = value?.Trim() ?? string.Empty;
        return text.Equals("unknown", StringComparison.OrdinalIgnoreCase)
            || text.Equals("unknow", StringComparison.OrdinalIgnoreCase)
            || text.Equals("null", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : text;
    }
}

public sealed record LiteWiiMTransportRequest(
    string DeviceId,
    string Command,
    int? Volume);

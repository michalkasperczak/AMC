using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace AccessibleMediaController.Windows.Services;

internal sealed record AudioOutputDeviceChoice(
    string? Id,
    string Label,
    bool IsAvailable = true)
{
    public override string ToString() => Label;
}

internal sealed class AudioOutputDeviceLease(
    WasapiOut output,
    MMDevice? device) : IDisposable
{
    private int _disposed;

    public WasapiOut Output { get; } = output;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        try
        {
            Output.Dispose();
        }
        finally
        {
            device?.Dispose();
        }
    }
}

internal static class AudioOutputDeviceCatalog
{
    private const string DefaultDeviceLabel = "Domyślne urządzenie systemowe";

    public static IReadOnlyList<AudioOutputDeviceChoice> Enumerate(string? selectedDeviceId)
    {
        var choices = new List<AudioOutputDeviceChoice>
        {
            new(null, DefaultDeviceLabel)
        };
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            string? defaultDeviceId = null;
            try
            {
                using var defaultDevice = enumerator.GetDefaultAudioEndpoint(
                    DataFlow.Render,
                    Role.Multimedia);
                defaultDeviceId = defaultDevice.ID;
            }
            catch (Exception exception) when (IsDeviceEnumerationFailure(exception))
            {
                DiagnosticLog.Warning(
                    "audio-device",
                    $"Nie można ustalić domyślnego wyjścia audio; błąd {exception.GetType().Name}.");
            }

            var endpointSnapshots = new List<(string Id, string Name, bool IsDefault)>();
            foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                using (device)
                {
                    endpointSnapshots.Add((
                        device.ID,
                        string.IsNullOrWhiteSpace(device.FriendlyName)
                            ? "Urządzenie audio bez nazwy"
                            : device.FriendlyName.Trim(),
                        string.Equals(device.ID, defaultDeviceId, StringComparison.Ordinal)));
                }
            }
            var endpoints = endpointSnapshots
                .OrderBy(device => device.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenByDescending(device => device.IsDefault)
                .ToArray();

            var duplicateNames = endpoints
                .GroupBy(device => device.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.CurrentCultureIgnoreCase);
            var nameIndexes = new Dictionary<string, int>(StringComparer.CurrentCultureIgnoreCase);
            foreach (var endpoint in endpoints)
            {
                var label = endpoint.Name;
                if (duplicateNames[label] > 1)
                {
                    var index = nameIndexes.GetValueOrDefault(label) + 1;
                    nameIndexes[label] = index;
                    label = $"{label}, urządzenie {index}";
                }
                if (endpoint.IsDefault) label += " — obecnie domyślne w Windows";
                choices.Add(new AudioOutputDeviceChoice(endpoint.Id, label));
            }
        }
        catch (Exception exception) when (IsDeviceEnumerationFailure(exception))
        {
            DiagnosticLog.Error(
                "audio-device",
                "Nie udało się odczytać listy urządzeń audio.",
                exception);
        }

        if (!string.IsNullOrWhiteSpace(selectedDeviceId)
            && choices.All(choice => !string.Equals(
                choice.Id,
                selectedDeviceId,
                StringComparison.Ordinal)))
        {
            choices.Add(new AudioOutputDeviceChoice(
                selectedDeviceId,
                "Poprzednio wybrane urządzenie jest teraz niedostępne; tymczasowo zostanie użyte urządzenie domyślne",
                false));
        }
        return choices;
    }

    public static bool IsAvailable(string? deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId)) return true;
        return Enumerate(deviceId).Any(choice =>
            choice.IsAvailable
            && string.Equals(choice.Id, deviceId, StringComparison.Ordinal));
    }

    public static AudioOutputDeviceLease CreateOutput(string? deviceId, int latencyMilliseconds)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return new AudioOutputDeviceLease(
                new WasapiOut(AudioClientShareMode.Shared, true, latencyMilliseconds),
                null);
        }

        MMDevice? selectedDevice = null;
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            selectedDevice = enumerator.GetDevice(deviceId);
            if (selectedDevice.State != DeviceState.Active)
            {
                throw new InvalidOperationException("Wybrane urządzenie audio nie jest aktywne.");
            }
            var output = new WasapiOut(
                selectedDevice,
                AudioClientShareMode.Shared,
                true,
                latencyMilliseconds);
            return new AudioOutputDeviceLease(output, selectedDevice);
        }
        catch (Exception exception) when (IsDeviceEnumerationFailure(exception))
        {
            selectedDevice?.Dispose();
            DiagnosticLog.Warning(
                "audio-device",
                $"Wybrane urządzenie jest niedostępne; użyto domyślnego. Błąd {exception.GetType().Name}.");
            return new AudioOutputDeviceLease(
                new WasapiOut(AudioClientShareMode.Shared, true, latencyMilliseconds),
                null);
        }
    }

    private static bool IsDeviceEnumerationFailure(Exception exception) =>
        exception is InvalidOperationException
            or ArgumentException
            or COMException
            or InvalidComObjectException
            or UnauthorizedAccessException;
}

using AccessibleMediaController.Core.Configuration;

namespace AccessibleMediaController.Core.Playback;

/// <summary>
/// Stores only explicit user adjustments. An empty output-device identifier
/// means the Windows system-default output, not every physical device.
/// </summary>
public static class PlaybackVolumeMemory
{
    public static int? Find(
        PlaybackVolumeMemorySettings settings,
        string sessionId,
        string contextId,
        string? outputDeviceId)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var normalizedSession = NormalizeRequired(sessionId);
        var normalizedContext = NormalizeRequired(contextId);
        if (normalizedSession.Length == 0 || normalizedContext.Length == 0) return null;
        var normalizedDevice = NormalizeDevice(outputDeviceId);
        return settings.Entries.LastOrDefault(entry =>
            string.Equals(entry.SessionId, normalizedSession, StringComparison.OrdinalIgnoreCase)
            && string.Equals(entry.ContextId, normalizedContext, StringComparison.Ordinal)
            && string.Equals(entry.OutputDeviceId, normalizedDevice, StringComparison.OrdinalIgnoreCase))?.Volume;
    }

    public static void Remember(
        PlaybackVolumeMemorySettings settings,
        string sessionId,
        string contextId,
        string? outputDeviceId,
        int volume)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var normalizedSession = NormalizeRequired(sessionId);
        var normalizedContext = NormalizeRequired(contextId);
        if (normalizedSession.Length == 0)
            throw new ArgumentException("Identyfikator sesji nie może być pusty.", nameof(sessionId));
        if (normalizedContext.Length == 0)
            throw new ArgumentException("Identyfikator kontekstu nie może być pusty.", nameof(contextId));
        var normalizedDevice = NormalizeDevice(outputDeviceId);
        settings.Entries.RemoveAll(entry =>
            string.Equals(entry.SessionId, normalizedSession, StringComparison.OrdinalIgnoreCase)
            && string.Equals(entry.ContextId, normalizedContext, StringComparison.Ordinal)
            && string.Equals(entry.OutputDeviceId, normalizedDevice, StringComparison.OrdinalIgnoreCase));
        settings.Entries.Add(new PlaybackVolumeMemoryEntry
        {
            SessionId = normalizedSession,
            ContextId = normalizedContext,
            OutputDeviceId = normalizedDevice,
            Volume = Math.Clamp(volume, 0, 100)
        });
    }

    public static void Normalize(PlaybackVolumeMemorySettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Entries = (settings.Entries ?? [])
            .Where(entry => entry is not null)
            .Select(entry => new PlaybackVolumeMemoryEntry
            {
                SessionId = NormalizeRequired(entry.SessionId),
                ContextId = NormalizeRequired(entry.ContextId),
                OutputDeviceId = NormalizeDevice(entry.OutputDeviceId),
                Volume = Math.Clamp(entry.Volume, 0, 100)
            })
            .Where(entry => entry.SessionId.Length > 0 && entry.ContextId.Length > 0)
            .GroupBy(
                entry => new VolumeKey(entry.SessionId, entry.ContextId, entry.OutputDeviceId),
                VolumeKeyComparer.Instance)
            .Select(group => group.Last())
            .ToList();
    }

    private static string NormalizeRequired(string? value) => value?.Trim() ?? string.Empty;
    private static string NormalizeDevice(string? value) => value?.Trim() ?? string.Empty;

    private readonly record struct VolumeKey(string SessionId, string ContextId, string OutputDeviceId);

    private sealed class VolumeKeyComparer : IEqualityComparer<VolumeKey>
    {
        public static VolumeKeyComparer Instance { get; } = new();

        public bool Equals(VolumeKey x, VolumeKey y) =>
            string.Equals(x.SessionId, y.SessionId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.ContextId, y.ContextId, StringComparison.Ordinal)
            && string.Equals(x.OutputDeviceId, y.OutputDeviceId, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode(VolumeKey value) => HashCode.Combine(
            StringComparer.OrdinalIgnoreCase.GetHashCode(value.SessionId),
            StringComparer.Ordinal.GetHashCode(value.ContextId),
            StringComparer.OrdinalIgnoreCase.GetHashCode(value.OutputDeviceId));
    }
}

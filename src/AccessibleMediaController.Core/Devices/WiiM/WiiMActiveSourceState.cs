namespace AccessibleMediaController.Core.Devices.WiiM;

public static class WiiMActiveSourceState
{
    public static string? ResolveNetworkStreamId(
        string? rememberedStreamId,
        string? streamIdMatchedFromSnapshot,
        bool nativePresetResolved,
        IEnumerable<string> availableStreamIds)
    {
        ArgumentNullException.ThrowIfNull(availableStreamIds);
        if (nativePresetResolved) return null;
        if (!string.IsNullOrWhiteSpace(streamIdMatchedFromSnapshot))
            return streamIdMatchedFromSnapshot;
        if (string.IsNullOrWhiteSpace(rememberedStreamId)) return null;

        // Some WiiM firmware returns a redirected manifest, an internal proxy
        // address or no content URI after Play URL. That must not erase the
        // explicit stream selected in AMC, otherwise Alt+Page Up/Down silently
        // falls back to hardware presets immediately after playback starts.
        return availableStreamIds.Contains(rememberedStreamId, StringComparer.OrdinalIgnoreCase)
            ? rememberedStreamId
            : null;
    }
}

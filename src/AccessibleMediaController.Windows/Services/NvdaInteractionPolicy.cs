using System.Diagnostics;
using System.Windows.Threading;
using AccessibleMediaController.Core.Sessions;

namespace AccessibleMediaController.Windows.Services;

internal static class NvdaPresetPolicy
{
    internal static bool NeedsBrowser(string? kind) => kind?.ToLowerInvariant() is
        "folder" or "localalbum" or "amcplaylist" or "playlist" or "album" or "artist" or "podcast";
}

// Flow the origin through awaits; a late provider response must not steal focus.
// This is execution-local, not a global flag which could block ordinary UI work.
internal sealed class NvdaBackgroundScope : IDisposable
{
    private static readonly AsyncLocal<bool> Active = new();
    private readonly bool _previous = Active.Value;
    internal static bool IsActive => Active.Value;
    internal NvdaBackgroundScope() => Active.Value = true;
    public void Dispose() => Active.Value = _previous;
}

internal sealed class NvdaUiHandoff
{
    internal bool Pending { get; private set; }

    // Do not run a modal dialog on the pipe request's stack. A second request
    // cannot enqueue another dialog, and stale work expires instead of surprising
    // the user when a blocked dispatcher eventually recovers.
    internal bool TrySchedule(Dispatcher dispatcher, Func<bool> canOpen, Action open,
        TimeSpan? lifetime = null)
    {
        if (Pending) return false;
        Pending = true;
        var started = Stopwatch.GetTimestamp();
        dispatcher.BeginInvoke(() =>
        {
            try
            {
                if (Stopwatch.GetElapsedTime(started) <= (lifetime ?? TimeSpan.FromSeconds(1.5)) && canOpen())
                    open();
            }
            catch (Exception exception)
            {
                DiagnosticLog.Error("nvda-bridge", "Nie udało się otworzyć widoku z dodatku NVDA.", exception);
            }
            finally { Pending = false; }
        }, DispatcherPriority.Background);
        return true;
    }
}

internal static class NvdaPlaybackContext
{
    internal static string Describe(DemoMediaSession session, string? savedViewLabel)
    {
        if (!session.HasCurrentItem) return $"{session.DisplayName}, brak otwartego nagrania.";
        if (session.Id == "wiim")
            return "WiiM: lewo i prawo sterują kolejnością urządzenia; Alt+Page Up i Alt+Page Down presetami lub zapisanymi strumieniami, z Ctrl+Windows.";
        var ids = session.QueueNavigationActive ? session.QueueNavigationItemIds : session.PlaybackContextItemIds;
        var index = ids.ToList().FindIndex(id => id == session.CurrentItem.Id);
        var label = session.QueueNavigationActive ? "Kolejka"
            : string.IsNullOrWhiteSpace(savedViewLabel) ? "Bieżąca lista odtwarzania" : savedViewLabel;
        return index >= 0
            ? $"{label}, {session.DisplayName}, {index + 1} z {ids.Count}."
            : $"{label}, {session.DisplayName}, bieżącego nagrania nie ma już na tej liście.";
    }
}

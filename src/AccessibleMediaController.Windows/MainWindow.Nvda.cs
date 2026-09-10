using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using AccessibleMediaController.Core.Commands;
using AccessibleMediaController.Windows.Services;

namespace AccessibleMediaController.Windows;

public partial class MainWindow
{
    private NvdaCommandServer? _nvdaCommandServer;
    private bool _nvdaRemoteCommandActive;
    private bool _nvdaRemoteReadRequested;

    private Task<NvdaReply> ExecuteNvdaCommandAsync(string command, CancellationToken cancellation) =>
        Dispatcher.InvokeAsync(() => ExecuteNvdaCommand(command), DispatcherPriority.Input, cancellation).Task;

    private NvdaReply ExecuteNvdaCommand(string command)
    {
        if (_isClosing || !_initialFocusApplied || !IsEnabled
            || OwnedWindows.OfType<Window>().Any(window => window.IsVisible)
            || IsMenuInteractionActive(Keyboard.FocusedElement))
            return new(false, "AMC jest zajęty. Zamknij otwarte okno dialogowe lub spróbuj za chwilę.");

        if (!NvdaCommands.IsAllowed(command)) return new(false, "Nieobsługiwane polecenie dodatku AMC.");
        if (command == "status")
        {
            var session = _sessions.Current;
            if (!session.HasCurrentItem) return new(true, $"{session.DisplayName}, brak otwartego nagrania.");
            var state = session.IsPlaying ? "odtwarzanie" : "pauza";
            var mute = session.IsMuted ? ", wyciszone" : string.Empty;
            return new(true, $"{session.CurrentItem.Title}, {session.DisplayName}, {state}{mute}, głośność {session.Volume}%.");
        }

        // Capture immediate feedback for NVDA speech/braille, without a second UIA
        // announcement. Asynchronous provider errors keep their normal AMC path.
        var previousCapture = _captureAnnouncements;
        var previousMessage = _capturedAnnouncement;
        _captureAnnouncements = true;
        _capturedAnnouncement = null;
        _nvdaRemoteCommandActive = true;
        _nvdaRemoteReadRequested = command is "elapsed" or "remaining" or "total";
        try
        {
            var id = NvdaCommands.Resolve(command)!;
            var result = ExecuteCommand(id);
            if (command is "sessionPrevious" or "sessionNext")
                return new(true, _sessions.Current.DisplayName);
            return new(result.Handled, _capturedAnnouncement ??
                (result.Handled ? "" : "Nie można wykonać tego polecenia w bieżącej sesji."));
        }
        finally
        {
            _nvdaRemoteCommandActive = false;
            _nvdaRemoteReadRequested = false;
            _captureAnnouncements = previousCapture;
            _capturedAnnouncement = previousMessage;
        }
    }
}

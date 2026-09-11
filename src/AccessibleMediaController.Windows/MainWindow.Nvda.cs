using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using AccessibleMediaController.Core.Commands;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Windows.Services;

namespace AccessibleMediaController.Windows;

public partial class MainWindow
{
    private NvdaCommandServer? _nvdaCommandServer;
    private bool _nvdaRemoteCommandActive;
    private bool _nvdaRemoteReadRequested;
    private bool _nvdaCurrentItemTarget;
    private readonly NvdaUiHandoff _nvdaUiHandoff = new();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint handle);

    private Task<NvdaReply> ExecuteNvdaCommandAsync(string command, CancellationToken cancellation) =>
        Dispatcher.InvokeAsync(() => ExecuteNvdaCommand(command), DispatcherPriority.Input, cancellation).Task;

    private NvdaReply ExecuteNvdaCommand(string command)
    {
        if (_isClosing || !_initialFocusApplied || !IsEnabled || _nvdaUiHandoff.Pending
            || OwnedWindows.OfType<Window>().Any(window => window.IsVisible)
            || IsMenuInteractionActive(Keyboard.FocusedElement))
            return new(false, "AMC jest zajęty. Zamknij otwarte okno dialogowe lub spróbuj za chwilę.");

        if (!NvdaCommands.IsAllowed(command)) return new(false, "Nieobsługiwane polecenie dodatku AMC.");
        DiagnosticLog.Info("nvda-bridge", $"Polecenie: {command}; sesja: {_sessions.Current.Id}; otwiera okno: {NvdaCommands.OpensWindow(command)}.");
        if (command == "context")
        {
            // Persisted view identifiers can include IDs or paths. Resolve their
            // intentional display labels before exposing anything through NVDA.
            var view = GetSessionNavigationState(_sessions.Current.Id).PlaybackContextView;
            var label = NvdaPlaybackViewLabel(view);
            return new(true, NvdaPlaybackContext.Describe(_sessions.Current, label));
        }
        if (command == "status")
        {
            var session = _sessions.Current;
            if (!session.HasCurrentItem) return new(true, $"{session.DisplayName}, brak otwartego nagrania.");
            var state = session.IsPlaying ? "odtwarzanie" : "pauza";
            var mute = session.IsMuted ? ", wyciszone" : string.Empty;
            return new(true, $"{session.CurrentItem.Title}, {session.DisplayName}, {state}{mute}, głośność {session.Volume}%.");
        }

        // Playable presets stay in the background; containers need a visible browser.
        var presetCommand = NvdaCommands.Resolve(command);
        var presetNeedsWindow = presetCommand is not null
            && CommandIds.TryParseRadioPreset(presetCommand, out var presetSlot)
            && NvdaPresetNeedsWindow(presetSlot);
        if (NvdaCommands.OpensWindow(command) || presetNeedsWindow)
        {
            if (!IsVisible) Show();
            if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
            var handle = new WindowInteropHelper(this).Handle;
            if (!IsActive && !SetForegroundWindow(handle))
                return new(false, "Przejdź do AMC klawiszami Alt+Tab i ponów skrót otwierający widok.");
            var sessionId = _sessions.Current.Id;
            var scheduled = _nvdaUiHandoff.TrySchedule(Dispatcher,
                () => !_isClosing && IsActive && IsEnabled && _sessions.Current.Id == sessionId
                    && !OwnedWindows.OfType<Window>().Any(window => window.IsVisible),
                () =>
                {
                    // Chapters always refer to the current recording in the add-on.
                    if (command == "showChapters") ShowPlayerView();
                    ExecuteCommand(NvdaCommands.Resolve(command)!);
                });
            return new(scheduled, scheduled ? "" : "Poczekaj na otwarcie widoku AMC.");
        }

        if (NvdaCommands.TargetsCurrentItem(command) && !_sessions.Current.HasCurrentItem)
            return new(false, "Brak otwartego nagrania w bieżącej sesji.");

        // Capture immediate feedback for NVDA speech/braille, without a second UIA
        // announcement. Asynchronous provider errors keep their normal AMC path.
        var previousCapture = _captureAnnouncements;
        var previousMessage = _capturedAnnouncement;
        _captureAnnouncements = true;
        _capturedAnnouncement = null;
        _nvdaRemoteCommandActive = true;
        _nvdaRemoteReadRequested = true; // Explicit user requests, not automatic announcements.
        _nvdaCurrentItemTarget = NvdaCommands.TargetsCurrentItem(command)
            || presetCommand is not null && CommandIds.TryParseRadioPreset(presetCommand, out _);
        using var background = new NvdaBackgroundScope();
        try
        {
            if (command is "presetPrevious" or "presetNext")
                return NavigateNvdaPreset(command == "presetNext" ? 1 : -1);
            // 2026-09-11: w odtwarzaczu WiiM strzalki przelaczaja presety, tak jak w
            // oryginalnej aplikacji WiiM, do ktorej uzytkownik jest przyzwyczajony.
            // W pozostalych sesjach zostaja zmiana nagrania. Ctrl+Windows+Alt+PageUp
            // i PageDown przelaczaja presety zawsze, niezaleznie od sesji.
            if ((command is "previous" or "next") && _sessions.Current.Id == "wiim")
                return NavigateNvdaPreset(command == "next" ? 1 : -1);
            var id = NvdaCommands.Resolve(command)!;
            if (CommandIds.TryParseRadioPreset(id, out var directSlot))
            {
                var preset = SessionPresetEntries(_sessions.Current.Id).FirstOrDefault(entry => entry.Slot == directSlot);
                var resultPreset = ExecuteCommand(id);
                // Always acknowledge the requested slot, including 0/-/=. Async
                // provider confirmation/errors retain their usual reporting path.
                return new(resultPreset.Handled, _capturedAnnouncement
                    ?? (preset is null ? $"Preset {directSlot}" : $"Preset {directSlot}, {preset.TargetTitle}"));
            }
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
            _nvdaCurrentItemTarget = false;
            _captureAnnouncements = previousCapture;
            _capturedAnnouncement = previousMessage;
        }
    }

    private static string NvdaPlaybackViewLabel(string? view) => view switch
    {
        "Biblioteka" or "Ulubione" or "Kolejka" or "Presety" or "Historia" or "Foldery"
            or "Wszystkie stacje" or "Nowe odcinki" or "Wyszukiwanie" or "Pliki" => view,
        null or "" => "Bieżąca lista odtwarzania",
        _ when new[] { "Album — ", "Playlista — ", "Podcast — ", "Kanał YouTube — ", "Utwory — ", "Albumy — " }
            .Any(prefix => view.StartsWith(prefix, StringComparison.Ordinal)) => view.Length <= 240 ? view : view[..240],
        _ when view.StartsWith("playlist:", StringComparison.OrdinalIgnoreCase) => "Playlista",
        _ when view.StartsWith("podcast", StringComparison.OrdinalIgnoreCase) => "Podcasty",
        _ when view.StartsWith("tidal", StringComparison.OrdinalIgnoreCase) => "Kolekcja TIDAL",
        _ => "Bieżąca lista odtwarzania"
    };

    private bool NvdaPresetNeedsWindow(int slot)
    {
        var session = _sessions.Current;
        if (session.Id == "wiim") return false;
        var preset = SessionPresetEntries(session.Id).FirstOrDefault(entry => entry.Slot == slot);
        return preset is not null && NvdaPresetPolicy.NeedsBrowser(preset.TargetKind);
    }

    private NvdaReply NavigateNvdaPreset(int direction)
    {
        var session = _sessions.Current;
        if (session.Id == "wiim")
        {
            ExecuteCommand(direction > 0 ? CommandIds.NextWiiMDevicePreset : CommandIds.PreviousWiiMDevicePreset);
            return new(true, _capturedAnnouncement ?? "");
        }
        var presets = SessionPresetEntries(session.Id).OrderBy(entry => entry.Slot).ToArray();
        if (presets.Length == 0) return new(false, $"Brak zapisanych presetów: {session.DisplayName}.");
        var index = Array.FindIndex(presets, entry => session.HasCurrentItem && entry.TargetId == session.CurrentItem.Id);
        var next = index < 0 ? (direction > 0 ? 0 : presets.Length - 1)
            : (index + direction + presets.Length) % presets.Length;
        var target = presets[next];
        var item = session.Items.FirstOrDefault(candidate => candidate.Id == target.TargetId);
        // Folder/playlist presets need a visible browser, not a hidden change of
        // list. The explicit list-of-presets command offers that workflow.
        if (item?.Kind is not (MediaItemKind.Track or MediaItemKind.Station or MediaItemKind.Episode))
            return new(false, $"Preset {target.Slot}: {target.TargetTitle}. Otwórz listę presetów Ctrl+Windows+Alt+P, aby wybrać zawartość.");
        ActivatePreset(target.Slot);
        return new(true, $"{item.Title}, preset {target.Slot}.");
    }
}

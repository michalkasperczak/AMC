using System.Windows.Input;
using System.Windows.Threading;
using AccessibleMediaController.Core.Commands;
using AccessibleMediaController.Core.Presentation;

namespace AccessibleMediaController.Windows;

/// <summary>
/// Wspolne podglady AMC: Alt+R (nagrywane stacje), Alt+Shift+R (historia
/// nagrywania) i Ctrl+I (nowe odcinki podcastow). Przy wlaczonym przelaczniku
/// „wspólne podglądy” dzialaja ze WSZYSTKICH sesji, a Escape wraca dokladnie do
/// miejsca wywolania: tej samej sesji, widoku, filtru, zaznaczonego elementu
/// oraz odtwarzacza, jezeli podglad wywolano wlasnie z odtwarzacza.
///
/// To nie sa systemowe skroty RegisterHotKey - dzialaja w obrebie okna AMC.
/// Sam podglad nigdy nie rusza transportu: nie zatrzymuje, nie pauzuje i nie
/// wznawia odtwarzania.
/// </summary>
public partial class MainWindow
{
    private readonly TransientPreviewNavigator _transientPreviews = new();

    private bool GlobalTransientPreviewsEnabled =>
        _state?.Settings.GlobalTransientPreviews ?? true;

    /// <summary>
    /// Czy polecenie podgladu wolno wykonac w biezacej sesji. Uzywane przez
    /// warstwe dostepnosci polecen (menu, paleta, pomoc kontekstowa).
    /// </summary>
    private bool IsTransientPreviewAvailable(string commandId) =>
        TransientPreviewPolicy.IsAvailable(
            commandId,
            _sessions?.Current.Id,
            GlobalTransientPreviewsEnabled);

    private static bool IsTransientPreviewCommand(string commandId) =>
        TransientPreviewPolicy.FromCommandId(commandId) != TransientPreviewKind.None;

    /// <summary>
    /// Routing skrotow podgladow. Zwraca <c>true</c>, gdy skrot nalezy do
    /// podgladow i ma zostac obsluzony w biezacej sesji.
    /// </summary>
    private bool TryResolveTransientPreviewShortcut(
        Key key,
        ModifierKeys modifiers,
        out string commandId)
    {
        commandId = string.Empty;
        var kind = (modifiers, key) switch
        {
            (ModifierKeys.Alt, Key.R) => TransientPreviewKind.ActiveRadioRecordings,
            (ModifierKeys.Alt | ModifierKeys.Shift, Key.R) => TransientPreviewKind.RecordedRadioFiles,
            (ModifierKeys.Control, Key.I) => TransientPreviewKind.PodcastInbox,
            _ => TransientPreviewKind.None
        };
        if (kind == TransientPreviewKind.None) return false;
        if (!TransientPreviewPolicy.IsAvailable(kind, _sessions?.Current.Id, GlobalTransientPreviewsEnabled))
        {
            // Skrot nadal nalezy do podgladow: chcemy wypowiedziec przyczyne,
            // zamiast oddawac go innej, przypadkowej funkcji sesji.
            commandId = TransientPreviewPolicy.ToCommandId(kind)!;
            return true;
        }

        commandId = TransientPreviewPolicy.ToCommandId(kind)!;
        return true;
    }

    /// <summary>
    /// Zapamietuje miejsce wywolania PRZED przejsciem do podgladu. Wolane z
    /// <c>ExecuteCommand</c> dla trzech polecen podgladow.
    /// </summary>
    private void BeginTransientPreview(TransientPreviewKind kind)
    {
        if (_sessions is null) return;
        _transientPreviews.BeginPreview(
            kind,
            new TransientPreviewLocation(
                SessionId: _sessions.Current.Id,
                ViewName: _currentView,
                Filter: FilterBox?.Text ?? string.Empty,
                SelectedItemId: SelectedItem?.Id,
                PlayerActive: _playerViewActive,
                FilterFocused: FilterBox?.IsKeyboardFocusWithin == true,
                FilterSelectionStart: FilterBox?.SelectionStart ?? 0,
                FilterSelectionLength: FilterBox?.SelectionLength ?? 0));
    }

    /// <summary>
    /// Swiadomy Enter w podgladzie. Od tej chwili Escape z odtwarzacza wraca
    /// najpierw do podgladu, a dopiero potem do zrodla - i NIE wskrzesza
    /// dawnego sluchania w sesji zrodlowej.
    /// </summary>
    private void NoteTransientPreviewPlayback()
    {
        if (_transientPreviews.ActivePreview == TransientPreviewKind.None) return;
        _transientPreviews.NotePlaybackStartedFromPreview();
    }

    /// <summary>
    /// Jawna nawigacja uzytkownika (Ctrl+cyfra, lista sesji, inny widok)
    /// uniewaznia cel powrotu, zeby Escape nie skakal w nieaktualne miejsce.
    /// </summary>
    private void NoteExplicitNavigationAwayFromPreview()
    {
        if (!_transientPreviews.HasPendingReturn
            && _transientPreviews.ActivePreview == TransientPreviewKind.None)
        {
            return;
        }
        _transientPreviews.NoteExplicitNavigation();
    }

    /// <summary>
    /// Escape w podgladzie albo w odtwarzaczu otwartym z podgladu.
    /// Zwraca <c>true</c>, gdy powrot zostal obsluzony tutaj.
    /// </summary>
    private bool TryHandleTransientPreviewEscape()
    {
        var escape = _transientPreviews.HandleEscape();
        switch (escape.Kind)
        {
            case TransientPreviewEscapeKind.ReturnToPreview:
                // Wracamy do listy podgladu BEZ dotykania transportu: samo
                // opuszczenie odtwarzacza nie moze zatrzymywac nagrania.
                ReturnFromPlayerToPreviewList(escape.Preview);
                return true;

            case TransientPreviewEscapeKind.ReturnToOrigin when escape.Origin is { } origin:
                RestoreTransientPreviewOrigin(origin, escape.RestorePlayer);
                return true;

            default:
                return false;
        }
    }

    private void ReturnFromPlayerToPreviewList(TransientPreviewKind preview)
    {
        if (_playerViewActive)
        {
            HidePlayerForBrowserNavigation();
        }
        var commandId = TransientPreviewPolicy.ToCommandId(preview);
        if (commandId is null) return;
        // Podglad odbudowujemy tym samym poleceniem, ktorego uzywa skrot, wiec
        // widok, naglowek i fokus sa identyczne jak przy wejsciu. Pierwotne
        // miejsce powrotu zostaje: BeginPreview nie nadpisuje juz zapamietanego
        // zrodla, dopoki podglad jest aktywny.
        ExecuteCommand(commandId);
    }

    private void RestoreTransientPreviewOrigin(TransientPreviewLocation origin, bool restorePlayer)
    {
        if (_sessions is null) return;

        CaptureCurrentSessionNavigationState();
        if (_playerViewActive)
        {
            HidePlayerForBrowserNavigation();
        }

        if (!string.Equals(_sessions.Current.Id, origin.SessionId, StringComparison.Ordinal))
        {
            var session = _sessions.FindSession(origin.SessionId);
            if (session is null)
            {
                Announce("Sesja, z której otwarto podgląd, nie jest już dostępna");
                return;
            }
            _sessions.SelectSession(session.Id);
        }

        var navigation = GetSessionNavigationState(origin.SessionId);
        _currentView = origin.ViewName;
        navigation.CurrentView = origin.ViewName;
        navigation.PlayerActive = false;
        navigation.Filters[origin.ViewName] = origin.Filter;
        if (origin.SelectedItemId is { Length: > 0 } selectedId)
        {
            navigation.SelectedItemIds[origin.ViewName] = selectedId;
        }

        RestoreFilterForCurrentView(navigation);
        RefreshCurrentView(preferredItemId: origin.SelectedItemId);
        UpdateFileMenuForCurrentSession();
        UpdateWindowTitle();

        if (restorePlayer && _sessions.Current.HasCurrentItem)
        {
            // Przywracamy tylko widok, nigdy wcześniejszy cel ani stan audio.
            // Również jawnie uruchomione nagranie ma nadal grać.
            ShowPlayerView();
            return;
        }

        if (origin.FilterFocused)
        {
            FilterBox.Focus();
            Keyboard.Focus(FilterBox);
            FilterBox.Select(origin.FilterSelectionStart, origin.FilterSelectionLength);
        }
        else
        {
            PrepareViewFocusContext(origin.ViewName);
            RestoreMediaListFocusAfterRefresh();
        }
        QueueStateSave();
    }

    /// <summary>
    /// Wykonuje polecenie podgladu z zapamietaniem (albo bez) miejsca powrotu.
    /// </summary>
    private void ExecuteTransientPreviewCommand(string commandId, bool rememberOrigin = true)
    {
        var kind = TransientPreviewPolicy.FromCommandId(commandId);
        if (kind == TransientPreviewKind.None) return;
        if (rememberOrigin)
        {
            BeginTransientPreview(kind);
        }
        ExecuteCommand(commandId);
    }

    /// <summary>
    /// Alt+R - aktualnie nagrywane stacje. Widok mieszka w sesji Radio, wiec z
    /// innych sesji najpierw przechodzimy do Radia, zapamietawszy miejsce powrotu.
    /// Transportu nie ruszamy.
    /// </summary>
    private void ShowActiveRadioRecordingsPreview()
    {
        BeginTransientPreview(TransientPreviewKind.ActiveRadioRecordings);
        if (!SwitchToPreviewHostSession(TransientPreviewPolicy.ActiveRadioRecordingsHomeSession)) return;
        NavigateTo(ActiveRadioRecordingsViewName);
        PrepareViewFocusContext(ActiveRadioRecordingsViewName);
        RestoreMediaListFocusAfterRefresh();
    }

    /// <summary>
    /// Ctrl+I - nowe odcinki i materialy. Jak wyzej: widok zyje w sesji
    /// Podcasty i YouTube, a Escape wraca do miejsca wywolania.
    /// </summary>
    private void ShowPodcastInboxPreview()
    {
        BeginTransientPreview(TransientPreviewKind.PodcastInbox);
        if (!SwitchToPreviewHostSession(TransientPreviewPolicy.PodcastInboxHomeSession)) return;
        // RefreshCurrentView materializuje widoczną stronę ze świeżych danych
        // odcinków. Nie zastępuj katalogu sesji: może właśnie grać podgląd,
        // którego nie ma w zapisanej Bibliotece.
        NavigateTo(PodcastInboxViewName);
        PrepareViewFocusContext(MediaList.Items.Count == 0
            ? $"{PodcastInboxDisplayName}, brak nowych materiałów"
            : PodcastInboxDisplayName);
        RestoreMediaListFocusAfterRefresh();
        if (MediaList.Items.Count == 0)
        {
            Dispatcher.BeginInvoke(
                () => Announce("Brak nowych odcinków i materiałów. F5 odświeża wszystkie źródła z Biblioteki"),
                DispatcherPriority.ContextIdle);
        }
    }

    /// <summary>
    /// Przelacza na sesje, w ktorej fizycznie zyje widok podgladu. Nie dotyka
    /// transportu: samo ogladanie niczego nie zatrzymuje ani nie wznawia.
    /// </summary>
    private bool SwitchToPreviewHostSession(string sessionId)
    {
        if (string.Equals(_sessions.Current.Id, sessionId, StringComparison.Ordinal))
        {
            return true;
        }

        var session = _sessions.FindSession(sessionId);
        if (session is null)
        {
            Announce($"Sesja {sessionId} nie jest dostępna");
            return false;
        }

        CaptureCurrentSessionNavigationState();
        HidePlayerForBrowserNavigation();
        _sessions.SelectSession(session.Id);
        var navigation = GetSessionNavigationState(session.Id);
        navigation.PlayerActive = false;
        UpdateFileMenuForCurrentSession();
        UpdateWindowTitle();
        return true;
    }

    /// <summary>
    /// Opis skrotu podgladu dla pomocy kontekstowej - bez podwojnego skrotu w
    /// nazwie, zgodnie z zasada pojedynczego oznajmiania.
    /// </summary>
    private bool TryDescribeTransientPreviewShortcut(Key key, ModifierKeys modifiers, out string description)
    {
        description = string.Empty;
        if (!TryResolveTransientPreviewShortcut(key, modifiers, out var commandId)) return false;
        var kind = TransientPreviewPolicy.FromCommandId(commandId);
        if (!TransientPreviewPolicy.IsAvailable(kind, _sessions?.Current.Id, GlobalTransientPreviewsEnabled))
        {
            description = TransientPreviewPolicy.DescribeUnavailable(kind);
            return true;
        }

        description = kind switch
        {
            TransientPreviewKind.ActiveRadioRecordings =>
                "pokaż aktualnie nagrywane stacje; Escape wraca tutaj",
            TransientPreviewKind.RecordedRadioFiles =>
                "pokaż historię nagrywania; Escape wraca tutaj",
            TransientPreviewKind.PodcastInbox =>
                "pokaż nowe odcinki i materiały; Escape wraca tutaj",
            _ => string.Empty
        };
        return description.Length > 0;
    }
}

namespace AccessibleMediaController.Core.Presentation;

using AccessibleMediaController.Core.Commands;

/// <summary>
/// Trzy wspolne podglady AMC: nagrywane stacje (Alt+R), historia nagrywania
/// (Alt+Shift+R) i nowe odcinki podcastow (Ctrl+I).
/// </summary>
public enum TransientPreviewKind
{
    None,
    ActiveRadioRecordings,
    RecordedRadioFiles,
    PodcastInbox
}

/// <summary>
/// Miejsce, z ktorego uzytkownik wywolal podglad. Escape ma wrocic dokladnie
/// tutaj: ta sama sesja, ten sam widok, ten sam filtr, ten sam element listy
/// oraz odtwarzacz, jezeli podglad wywolano wlasnie z odtwarzacza.
/// </summary>
public sealed record TransientPreviewLocation(
    string SessionId,
    string ViewName,
    string Filter = "",
    string? SelectedItemId = null,
    bool PlayerActive = false,
    bool FilterFocused = false,
    int FilterSelectionStart = 0,
    int FilterSelectionLength = 0);

public enum TransientPreviewEscapeKind
{
    /// <summary>Escape nie dotyczy podgladow - obowiazuje zwykle zachowanie okna.</summary>
    NotHandled,

    /// <summary>Escape z odtwarzacza otwartego z podgladu wraca najpierw do podgladu.</summary>
    ReturnToPreview,

    /// <summary>Escape z podgladu wraca do miejsca wywolania.</summary>
    ReturnToOrigin
}

public readonly record struct TransientPreviewEscape(
    TransientPreviewEscapeKind Kind,
    TransientPreviewKind Preview,
    TransientPreviewLocation? Origin,
    bool RestorePlayer)
{
    public static readonly TransientPreviewEscape NotHandled =
        new(TransientPreviewEscapeKind.NotHandled, TransientPreviewKind.None, null, false);
}

/// <summary>
/// Czysta polityka dostepnosci trzech wspolnych podgladow. Wlaczony przelacznik
/// „wspolne podglady” (domyslnie) udostepnia je we WSZYSTKICH sesjach AMC;
/// wylaczony zostawia je w sesjach macierzystych, tak jak przed zmiana.
/// </summary>
public static class TransientPreviewPolicy
{
    public const string ActiveRadioRecordingsHomeSession = "radio";
    public const string PodcastInboxHomeSession = "podcasts";

    public static TransientPreviewKind FromCommandId(string? commandId) => commandId switch
    {
        CommandIds.ViewActiveRadioRecordings => TransientPreviewKind.ActiveRadioRecordings,
        CommandIds.ViewRecordedRadioFiles => TransientPreviewKind.RecordedRadioFiles,
        CommandIds.ViewPodcastInbox => TransientPreviewKind.PodcastInbox,
        _ => TransientPreviewKind.None
    };

    public static string? ToCommandId(TransientPreviewKind kind) => kind switch
    {
        TransientPreviewKind.ActiveRadioRecordings => CommandIds.ViewActiveRadioRecordings,
        TransientPreviewKind.RecordedRadioFiles => CommandIds.ViewRecordedRadioFiles,
        TransientPreviewKind.PodcastInbox => CommandIds.ViewPodcastInbox,
        _ => null
    };

    /// <summary>
    /// Sesje, w ktorych podglad dziala przy WYLACZONYM przelaczniku. Historia
    /// nagrywania mieszka w dwoch sesjach, bo pliki trafiaja do Plikow lokalnych.
    /// </summary>
    public static bool IsHomeSession(TransientPreviewKind kind, string? sessionId) => kind switch
    {
        TransientPreviewKind.ActiveRadioRecordings =>
            string.Equals(sessionId, ActiveRadioRecordingsHomeSession, StringComparison.Ordinal),
        TransientPreviewKind.RecordedRadioFiles =>
            string.Equals(sessionId, "radio", StringComparison.Ordinal)
            || string.Equals(sessionId, "local", StringComparison.Ordinal),
        TransientPreviewKind.PodcastInbox =>
            string.Equals(sessionId, PodcastInboxHomeSession, StringComparison.Ordinal),
        _ => false
    };

    public static bool IsAvailable(TransientPreviewKind kind, string? sessionId, bool globalPreviews)
    {
        if (kind == TransientPreviewKind.None) return false;
        return globalPreviews || IsHomeSession(kind, sessionId);
    }

    public static bool IsAvailable(string? commandId, string? sessionId, bool globalPreviews) =>
        IsAvailable(FromCommandId(commandId), sessionId, globalPreviews);

    /// <summary>
    /// Komunikat dla wylaczonego przelacznika - mowi, gdzie podglad nadal dziala.
    /// </summary>
    public static string DescribeUnavailable(TransientPreviewKind kind) => kind switch
    {
        TransientPreviewKind.ActiveRadioRecordings =>
            "Widok nagrywanych stacji jest dostępny w sesji Radio internetowe. "
            + "Wspólne podglądy ze wszystkich sesji włączysz w Ustawieniach",
        TransientPreviewKind.RecordedRadioFiles =>
            "Historia nagrywania jest dostępna w sesjach Radio internetowe i Pliki lokalne. "
            + "Wspólne podglądy ze wszystkich sesji włączysz w Ustawieniach",
        TransientPreviewKind.PodcastInbox =>
            "Nowe odcinki i materiały są dostępne w sesji Podcasty i YouTube. "
            + "Wspólne podglądy ze wszystkich sesji włączysz w Ustawieniach",
        _ => string.Empty
    };
}

/// <summary>
/// Pamiec powrotu ze wspolnych podgladow. Trzyma JEDNO pierwotne miejsce
/// wywolania, zeby przelaczanie miedzy trzema podgladami i powtorne nacisniecie
/// tego samego skrotu nie gubily celu Escape.
/// </summary>
public sealed class TransientPreviewNavigator
{
    public TransientPreviewKind ActivePreview { get; private set; }

    public TransientPreviewLocation? Origin { get; private set; }

    /// <summary>Odtwarzacz otwarty swiadomym Enterem z podgladu.</summary>
    public bool PlaybackStartedFromPreview { get; private set; }

    public bool HasPendingReturn => ActivePreview != TransientPreviewKind.None && Origin is not null;

    /// <summary>
    /// Wejscie w podglad. Pierwotne miejsce zapamietujemy tylko przy PIERWSZYM
    /// wejsciu; kolejne skroty jedynie zmieniaja aktywny podglad.
    /// </summary>
    public void BeginPreview(TransientPreviewKind kind, TransientPreviewLocation current)
    {
        if (kind == TransientPreviewKind.None) return;
        if (ActivePreview == TransientPreviewKind.None)
        {
            Origin = current;
        }

        ActivePreview = kind;
        PlaybackStartedFromPreview = false;
    }

    /// <summary>
    /// Swiadomy Enter w podgladzie. Od tej chwili Escape z odtwarzacza wraca
    /// najpierw do podgladu, a powrot do zrodla NIE wskrzesza dawnego sluchania.
    /// </summary>
    public void NotePlaybackStartedFromPreview()
    {
        if (ActivePreview == TransientPreviewKind.None) return;
        PlaybackStartedFromPreview = true;
    }

    /// <summary>
    /// Jawna nawigacja uzytkownika (Ctrl+cyfra, wybor sesji, inny widok) czyni
    /// cel powrotu nieaktualnym - kasujemy go, zeby Escape nie skakal wstecz.
    /// </summary>
    public void NoteExplicitNavigation() => Reset();

    public TransientPreviewEscape HandleEscape()
    {
        if (ActivePreview == TransientPreviewKind.None) return TransientPreviewEscape.NotHandled;

        if (PlaybackStartedFromPreview)
        {
            PlaybackStartedFromPreview = false;
            return new TransientPreviewEscape(
                TransientPreviewEscapeKind.ReturnToPreview,
                ActivePreview,
                Origin,
                RestorePlayer: false);
        }

        if (Origin is not { } origin) return TransientPreviewEscape.NotHandled;

        var restorePlayer = origin.PlayerActive;
        var preview = ActivePreview;
        Reset();
        return new TransientPreviewEscape(
            TransientPreviewEscapeKind.ReturnToOrigin,
            preview,
            origin,
            restorePlayer);
    }

    public void Reset()
    {
        ActivePreview = TransientPreviewKind.None;
        Origin = null;
        PlaybackStartedFromPreview = false;
    }
}

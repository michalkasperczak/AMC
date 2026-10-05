using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.LocalMedia;

namespace AccessibleMediaController.Core.Playback;

/// <summary>
/// Rozstrzyga, czy dana sesja ma pamiętać pozycję odtwarzania.
///
/// Kolejność rozstrzygania dla plików lokalnych, od najbardziej szczegółowej:
/// opcja folderu, źródło folderu, ustawienie SESJI, ustawienie globalne.
/// Ta klasa odpowiada wyłącznie za poziom sesji i globalny, żeby dało się ją
/// przetestować bez interfejsu okna.
/// </summary>
public static class ResumePositionPolicy
{
    /// <summary>
    /// Zwraca tryb ustawiony jawnie dla sesji albo <see cref="ResumePositionMode.Inherit"/>,
    /// gdy sesja nie ma własnego ustawienia i obowiązuje ustawienie globalne.
    /// </summary>
    public static ResumePositionMode GetSessionMode(AppSettings settings, string? sessionId)
    {
        if (settings is null || string.IsNullOrWhiteSpace(sessionId)) return ResumePositionMode.Inherit;
        return settings.ResumePositionModeBySession.TryGetValue(sessionId, out var mode)
            ? mode
            : ResumePositionMode.Inherit;
    }

    /// <summary>
    /// Czy sesja ma pamiętać pozycję odtwarzania. Sesja bez własnego ustawienia
    /// dziedziczy ustawienie globalne, więc istniejące konfiguracje nie zmieniają
    /// zachowania po aktualizacji.
    /// </summary>
    public static bool ShouldRemember(AppSettings settings, string? sessionId)
    {
        if (settings is null) return true;
        return GetSessionMode(settings, sessionId) switch
        {
            ResumePositionMode.Remember => true,
            ResumePositionMode.StartFromBeginning => false,
            _ => settings.RememberLocalPlaybackPositions
        };
    }

    /// <summary>
    /// Ustawia tryb dla sesji. <see cref="ResumePositionMode.Inherit"/> usuwa
    /// wpis, żeby w zapisanych ustawieniach nie zostawały puste wartości
    /// nieodróżnialne od braku decyzji użytkownika.
    /// </summary>
    public static void SetSessionMode(AppSettings settings, string sessionId, ResumePositionMode mode)
    {
        if (settings is null || string.IsNullOrWhiteSpace(sessionId)) return;
        if (mode == ResumePositionMode.Inherit)
        {
            settings.ResumePositionModeBySession.Remove(sessionId);
            return;
        }

        settings.ResumePositionModeBySession[sessionId] = mode;
    }

    /// <summary>
    /// PELNA hierarchia AMC dla pliku lokalnego, w JEDNYM miejscu.
    ///
    /// Kolejnosc, od najbardziej szczegolowej: jawny tryb POZYCJI, najblizsza
    /// jawna OPCJA FOLDERU (<c>folder_playback_options</c>), najblizsze jawne
    /// ZRODLO FOLDERU (<c>folder_sources</c>), tryb SESJI, ustawienie GLOBALNE.
    /// „Najblizszy" znaczy o najdluzszej sciezce: podfolder bije folder nadrzedny.
    /// <see cref="ResumePositionMode.Inherit"/> na kazdym poziomie oznacza
    /// „pytaj dalszej warstwy", a jawne <see cref="ResumePositionMode.Remember"/>
    /// na pozycji nadpisuje wylaczone tlo.
    ///
    /// Ta metoda jest JEDNA BRAMKA dla obu silnikow (okno WPF i host Lite), zeby
    /// nie liczyly polityki niezaleznie i nie rozjechaly sie na granicy folderu
    /// ani sesji. <paramref name="localPath"/> rowne <c>null</c>/puste znaczy
    /// „brak sciezki lokalnej": warstwy folderow sa wtedy pomijane, a decyzja
    /// spada na sesje i globalne — dokladnie jak dotad.
    /// </summary>
    public static bool ShouldRememberLocalPosition(
        ResumePositionMode? itemMode,
        string? localPath,
        IEnumerable<LocalFolderPlaybackSettings>? folderPlaybackOptions,
        IEnumerable<LocalFolderSourceSettings>? folderSources,
        ResumePositionMode sessionMode,
        bool rememberGlobally,
        LocalFolderPathNormalizer? normalizer = null)
    {
        // Jawny tryb POZYCJI konczy sprawe: uzytkownik zdecydowal o tym pliku.
        if (itemMode is ResumePositionMode.Remember) return true;
        if (itemMode is ResumePositionMode.StartFromBeginning) return false;

        var sessionFallback = sessionMode switch
        {
            ResumePositionMode.Remember => true,
            ResumePositionMode.StartFromBeginning => false,
            _ => rememberGlobally
        };

        if (string.IsNullOrWhiteSpace(localPath)) return sessionFallback;

        var folderMode = (folderPlaybackOptions ?? [])
            .Where(option => option.ResumePositionMode != ResumePositionMode.Inherit
                && LocalFolderSourcePolicy.IsSameOrDescendant(localPath, option.Path, normalizer))
            .OrderByDescending(option => option.Path.Length)
            .Select(option => (ResumePositionMode?)option.ResumePositionMode)
            .FirstOrDefault();
        if (folderMode.HasValue) return folderMode.Value == ResumePositionMode.Remember;

        // ZRODLO folderu bierzemy NAJBLIZSZE, a jego Inherit schodzi dalej --
        // tak samo jak opcja folderu. Blizsze zrodlo z Inherit NIE przeslania
        // dalszego jawnego; o granicy decyduje pierwszy JAWNY tryb.
        var sourceMode = (folderSources ?? [])
            .Where(source => source.ResumePositionMode != ResumePositionMode.Inherit
                && LocalFolderSourcePolicy.IsSameOrDescendant(localPath, source.Path, normalizer))
            .OrderByDescending(source => source.Path.Length)
            .Select(source => (ResumePositionMode?)source.ResumePositionMode)
            .FirstOrDefault();
        if (sourceMode.HasValue) return sourceMode.Value == ResumePositionMode.Remember;

        return sessionFallback;
    }

    /// <summary>
    /// Etykieta dla czytnika ekranu. Wariant dziedziczony mówi wprost, co z
    /// niego wynika, żeby użytkownik nie musiał sprawdzać ustawień globalnych.
    /// </summary>
    public static string DescribeSessionMode(AppSettings settings, string? sessionId)
    {
        return GetSessionMode(settings, sessionId) switch
        {
            ResumePositionMode.Remember => "Pamiętaj pozycję odtwarzania",
            ResumePositionMode.StartFromBeginning => "Zawsze od początku",
            _ => settings is not null && settings.RememberLocalPlaybackPositions
                ? "Jak ustawienie ogólne: pamiętaj pozycję odtwarzania"
                : "Jak ustawienie ogólne: zawsze od początku"
        };
    }
}

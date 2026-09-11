using AccessibleMediaController.Core.Configuration;

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

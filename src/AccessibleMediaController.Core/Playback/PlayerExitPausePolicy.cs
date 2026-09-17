using AccessibleMediaController.Core.Configuration;

namespace AccessibleMediaController.Core.Playback;

/// <summary>
/// Rozstrzyga, czy wyjscie z odtwarzacza (Escape, przejscie do listy) ma
/// wstrzymac odtwarzanie W TEJ SESJI.
///
/// Kolejnosc: ustawienie SESJI, potem ustawienie ogolne. Sesja bez wlasnego
/// ustawienia dziedziczy ogolne, wiec dotychczasowe konfiguracje po
/// aktualizacji zachowuja sie tak samo.
///
/// ZGLOSZENIE Michala: w radiu wyjscie z odtwarzacza nie powinno przerywac
/// transmisji, a w plikach lokalnych powinno - jedno pole globalne nie umialo
/// obsluzyc obu przypadkow naraz.
/// </summary>
public static class PlayerExitPausePolicy
{
    /// <summary>
    /// Zwraca ustawienie jawnie wybrane dla sesji albo null, gdy sesja
    /// dziedziczy ustawienie ogolne.
    /// </summary>
    public static bool? GetSessionOverride(AppSettings settings, string? sessionId)
    {
        if (settings is null || string.IsNullOrWhiteSpace(sessionId)) return null;
        return settings.Audio.OverridesBySession.TryGetValue(sessionId, out var overrides)
            ? overrides?.PausePlaybackWhenLeavingPlayerOverride
            : null;
    }

    /// <summary>
    /// Czy wyjscie z odtwarzacza ma wstrzymac odtwarzanie w tej sesji.
    /// </summary>
    public static bool ShouldPause(AppSettings settings, string? sessionId)
    {
        if (settings is null) return true;
        return GetSessionOverride(settings, sessionId)
            ?? settings.PausePlaybackWhenLeavingPlayer;
    }

    /// <summary>
    /// Ustawia zachowanie dla sesji. null usuwa odstepstwo (powrot do
    /// ustawienia ogolnego); pusty wpis sesji jest usuwany calkowicie, zeby w
    /// zapisanych ustawieniach nie zostawaly wartosci nieodroznialne od braku
    /// decyzji uzytkownika.
    /// </summary>
    public static void SetSessionOverride(AppSettings settings, string sessionId, bool? value)
    {
        if (settings is null || string.IsNullOrWhiteSpace(sessionId)) return;
        var wpisy = settings.Audio.OverridesBySession;
        if (!wpisy.TryGetValue(sessionId, out var overrides) || overrides is null)
        {
            if (value is null) return;
            wpisy[sessionId] = new SessionPlaybackAudioOverrides
            {
                PausePlaybackWhenLeavingPlayerOverride = value
            };
            return;
        }

        overrides.PausePlaybackWhenLeavingPlayerOverride = value;
        if (overrides.IsEmpty) wpisy.Remove(sessionId);
    }

    /// <summary>
    /// Etykieta dla czytnika ekranu. Wariant dziedziczony mowi wprost, co z
    /// niego wynika, zeby uzytkownik nie musial sprawdzac ustawien ogolnych.
    /// </summary>
    public static string DescribeSessionMode(AppSettings settings, string? sessionId)
    {
        var wybor = GetSessionOverride(settings, sessionId);
        if (wybor == true) return "Wstrzymuj odtwarzanie po wyjściu z odtwarzacza";
        if (wybor == false) return "Odtwarzaj dalej po wyjściu z odtwarzacza";
        return settings is not null && settings.PausePlaybackWhenLeavingPlayer
            ? "Jak ustawienie ogólne: wstrzymuj po wyjściu z odtwarzacza"
            : "Jak ustawienie ogólne: odtwarzaj dalej po wyjściu";
    }
}

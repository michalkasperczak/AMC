using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Playback;
using AccessibleMediaController.Core.Sessions;

namespace AccessibleMediaController.Core.Spotify;

/// <summary>
/// Rzeczywiste ustawienia odtwarzania pozycji Spotify: pamiec pozycji dla
/// utworu/odcinka, dla albumu/podcastu, dla calej sesji i ustawienie ogolne.
///
/// ZGLOSZENIE Michala 18.09.2026: "Spotify ALT+SHIFT+ENTER jeszcze nie
/// podlaczone". Okno opcji elementu nie otwieralo sie w ogole, a sesja Spotify
/// pamietala pozycje ZAWSZE, bo <see cref="SessionManager"/> tworzyl ja bez
/// funkcji rozstrzygajacej pamiec pozycji i obowiazywalo domyslne "_ => true".
///
/// Kolejnosc rozstrzygania, od najbardziej szczegolowej:
/// utwor/odcinek, album/podcast, sesja Spotify, ustawienie ogolne.
///
/// Spotify NIE ma zmiany tempa (Web Playback SDK nie udostepnia predkosci),
/// nie przechodzi przez nasz lancuch DSP i polityka uslugi zabrania crossfade,
/// wiec ta klasa celowo NIE przechowuje zadnych ustawien przetwarzania dzwieku.
/// Zapis "normalizacja wlaczona", ktorego nikt nie odczyta, byl by martwa
/// kontrolka - dokladnie tym, co uzytkownik czytnika zglasza jako blad.
/// </summary>
public static class SpotifyPlaybackSettingsResolver
{
    /// <summary>Identyfikator sesji Spotify w ustawieniach.</summary>
    public const string SessionId = "spotify";
    public const string LibrespotSessionId = "spotifyLibrespot";

    public static bool IsSpotifySession(string? sessionId) => sessionId is SessionId or LibrespotSessionId;

    private static SpotifyPlaybackSettings Profile(AppSettings settings, string sessionId) => sessionId switch
    {
        SessionId => settings.SpotifyPlayback,
        LibrespotSessionId => settings.SpotifyLibrespotPlayback,
        _ => throw new ArgumentException("Nieznana sesja Spotify.", nameof(sessionId))
    };

    /// <summary>
    /// Stabilny klucz zapisu dla pozycji Spotify.
    ///
    /// MUSI byc stabilny miedzy uruchomieniami i miedzy odswiezeniami
    /// biblioteki. <see cref="MediaItem.Id"/> NIE nadaje sie na klucz:
    /// domyslnie jest to <c>Guid.NewGuid()</c> nadawany przy czytaniu
    /// odpowiedzi API, wiec po ponownym pobraniu biblioteki ten sam utwor ma
    /// inny Id i zapisana pozycja byla by nieosiagalna.
    ///
    /// Kolejnosc: adres uslugi (spotify:track:...), potem identyfikator
    /// zewnetrzny z rodzajem pozycji, a na koncu - tylko dla pozycji, ktore nie
    /// maja ani jednego, ani drugiego (np. sesja demonstracyjna) - lokalne Id.
    /// </summary>
    public static string StorageKey(MediaItem? item)
    {
        if (item is null) return string.Empty;
        if (!string.IsNullOrWhiteSpace(item.Source)
            && item.Source!.StartsWith("spotify:", StringComparison.Ordinal))
        {
            return item.Source!;
        }
        if (!string.IsNullOrWhiteSpace(item.ExternalId))
        {
            return $"spotify:{KindSegment(item.Kind)}:{item.ExternalId}";
        }
        return string.IsNullOrWhiteSpace(item.Id) ? string.Empty : $"local:{item.Id}";
    }

    /// <summary>
    /// Klucz albumu albo podcastu, z ktorego pochodzi pozycja, albo null gdy
    /// pozycja nie nalezy do zadnego pojemnika. Dla samego albumu/podcastu
    /// zwraca jego wlasny klucz, zeby ustawienie zrobione na albumie obejmowalo
    /// jego utwory.
    /// </summary>
    public static string? ContainerKey(MediaItem? item)
    {
        if (item is null) return null;
        if (item.Kind is MediaItemKind.Album or MediaItemKind.Podcast)
        {
            var wlasny = StorageKey(item);
            return wlasny.Length == 0 ? null : wlasny;
        }
        if (!string.IsNullOrWhiteSpace(item.RelatedAlbumExternalId))
        {
            var segment = item.Kind == MediaItemKind.Episode ? "show" : "album";
            return $"spotify:{segment}:{item.RelatedAlbumExternalId}";
        }
        return null;
    }

    /// <summary>
    /// Wybor uzytkownika dla POJEDYNCZEJ pozycji. <see cref="ResumePositionMode.Inherit"/>
    /// oznacza brak wlasnej decyzji - wtedy obowiazuje album/podcast, sesja
    /// albo ustawienie ogolne. Tej wartosci uzywa okno opcji, zeby pokazac to,
    /// co uzytkownik faktycznie wybral, a nie wynik dziedziczenia.
    /// </summary>
    public static ResumePositionMode ResolveItemMode(AppSettings? settings, MediaItem? item, string sessionId = SessionId)
    {
        var key = StorageKey(item);
        if (settings is null || key.Length == 0) return ResumePositionMode.Inherit;
        return Profile(settings, sessionId).ItemsByKey.TryGetValue(key, out var entry)
            ? entry.ResumePositionMode
            : ResumePositionMode.Inherit;
    }

    /// <summary>Wybor uzytkownika dla albumu albo podcastu.</summary>
    public static ResumePositionMode ResolveContainerMode(AppSettings? settings, MediaItem? item, string sessionId = SessionId)
    {
        var key = ContainerKey(item);
        if (settings is null || string.IsNullOrEmpty(key)) return ResumePositionMode.Inherit;
        return Profile(settings, sessionId).ContainersByKey.TryGetValue(key, out var mode)
            ? mode
            : ResumePositionMode.Inherit;
    }

    /// <summary>Wybor dla calej sesji Spotify (Ctrl+Alt+Enter).</summary>
    public static ResumePositionMode ResolveSessionMode(AppSettings? settings, string sessionId = SessionId) =>
        settings is null
            ? ResumePositionMode.Inherit
            : ResumePositionPolicy.GetSessionMode(settings, sessionId);

    public static void SetItemMode(AppSettings? settings, MediaItem? item, ResumePositionMode mode, string sessionId = SessionId)
    {
        var key = StorageKey(item);
        if (settings is null || key.Length == 0) return;
        var store = Profile(settings, sessionId).ItemsByKey;
        if (mode == ResumePositionMode.Inherit)
        {
            // Brak decyzji zapisujemy jako BRAK wpisu, zeby w pliku ustawien nie
            // zostawaly wartosci nieodrozninalne od niewybranych. Zapisana
            // pozycja musi jednak przezyc powrot do dziedziczenia.
            if (store.TryGetValue(key, out var istniejacy))
            {
                if (istniejacy.PositionTicks == 0) store.Remove(key);
                else istniejacy.ResumePositionMode = ResumePositionMode.Inherit;
            }
            return;
        }

        var entry = store.TryGetValue(key, out var found)
            ? found
            : store[key] = new SpotifyItemPlaybackSettings();
        entry.ResumePositionMode = mode;
        // "Zawsze od poczatku" musi od razu wyczyscic zapamietana pozycje.
        // Inaczej stara wartosc wrocilaby po ponownym wlaczeniu pamietania,
        // a uzytkownik nie ma zadnego sposobu, zeby sie jej domyslic.
        if (mode == ResumePositionMode.StartFromBeginning) entry.PositionTicks = 0;
    }

    public static void SetContainerMode(AppSettings? settings, MediaItem? item, ResumePositionMode mode, string sessionId = SessionId)
    {
        var key = ContainerKey(item);
        if (settings is null || string.IsNullOrEmpty(key)) return;
        if (mode == ResumePositionMode.Inherit)
        {
            Profile(settings, sessionId).ContainersByKey.Remove(key);
            return;
        }
        Profile(settings, sessionId).ContainersByKey[key] = mode;
    }

    public static void SetSessionMode(AppSettings? settings, ResumePositionMode mode, string sessionId = SessionId)
    {
        if (settings is null) return;
        ResumePositionPolicy.SetSessionMode(settings, sessionId, mode);
    }

    /// <summary>
    /// Rozstrzyga, czy pozycja Spotify ma pamietac miejsce odtwarzania.
    /// To jest funkcja, ktorej brakowalo sesji Spotify - bez niej
    /// <see cref="DemoMediaSession"/> pamietal pozycje zawsze.
    /// </summary>
    public static bool ShouldRemember(AppSettings? settings, MediaItem? item, string sessionId = SessionId)
    {
        if (settings is null) return true;
        return ResolveItemMode(settings, item, sessionId) switch
        {
            ResumePositionMode.Remember => true,
            ResumePositionMode.StartFromBeginning => false,
            _ => ResolveContainerMode(settings, item, sessionId) switch
            {
                ResumePositionMode.Remember => true,
                ResumePositionMode.StartFromBeginning => false,
                _ => ResumePositionPolicy.ShouldRemember(settings, sessionId)
            }
        };
    }

    /// <summary>
    /// Zapisuje pozycje odtwarzania do TRWALEGO stanu. Pozycja zapisana tutaj
    /// przezywa zamkniecie programu i odswiezenie biblioteki, bo klucz nie
    /// zalezy od losowego <see cref="MediaItem.Id"/>.
    /// </summary>
    public static void StorePosition(AppSettings? settings, MediaItem? item, TimeSpan position, string sessionId = SessionId)
    {
        var key = StorageKey(item);
        if (settings is null || key.Length == 0) return;
        var store = Profile(settings, sessionId).ItemsByKey;
        if (!ShouldRemember(settings, item, sessionId))
        {
            if (store.TryGetValue(key, out var wylaczony)) wylaczony.PositionTicks = 0;
            return;
        }

        var ticks = position < TimeSpan.Zero ? 0 : position.Ticks;
        if (ticks == 0)
        {
            // Zero to brak pozycji. Nie zostawiamy wpisu utworzonego wylacznie
            // dla zera, zeby plik ustawien nie rosl o kazdy dotkniety utwor.
            if (store.TryGetValue(key, out var zerowany))
            {
                zerowany.PositionTicks = 0;
                if (zerowany.ResumePositionMode == ResumePositionMode.Inherit) store.Remove(key);
            }
            return;
        }

        var entry = store.TryGetValue(key, out var found)
            ? found
            : store[key] = new SpotifyItemPlaybackSettings();
        entry.PositionTicks = ticks;
    }

    /// <summary>
    /// Zapisana pozycja albo zero, gdy pamietanie jest wylaczone na dowolnym
    /// poziomie. Sprawdzenie polityki tutaj chroni przed sytuacja, w ktorej
    /// wylaczenie pamieci pozycji nie dziala do pierwszego zapisu.
    /// </summary>
    public static TimeSpan ResolvePosition(AppSettings? settings, MediaItem? item, string sessionId = SessionId)
    {
        var key = StorageKey(item);
        if (settings is null || key.Length == 0) return TimeSpan.Zero;
        if (!ShouldRemember(settings, item, sessionId)) return TimeSpan.Zero;
        return Profile(settings, sessionId).ItemsByKey.TryGetValue(key, out var entry)
            ? TimeSpan.FromTicks(entry.PositionTicks < 0 ? 0 : entry.PositionTicks)
            : TimeSpan.Zero;
    }

    /// <summary>
    /// Etykieta dla czytnika ekranu. Wariant dziedziczony mowi wprost, co z
    /// niego wynika, zeby uzytkownik nie musial otwierac kolejnych okien.
    /// </summary>
    public static string DescribeItemMode(AppSettings? settings, MediaItem? item, string sessionId = SessionId)
    {
        return ResolveItemMode(settings, item, sessionId) switch
        {
            ResumePositionMode.Remember => "Pamiętaj pozycję odtwarzania",
            ResumePositionMode.StartFromBeginning => "Zawsze od początku",
            _ => ShouldRemember(settings, item, sessionId)
                ? "Jak ustawienie nadrzędne: pamiętaj pozycję odtwarzania"
                : "Jak ustawienie nadrzędne: zawsze od początku"
        };
    }

    private static string KindSegment(MediaItemKind kind) => kind switch
    {
        MediaItemKind.Album => "album",
        MediaItemKind.Playlist => "playlist",
        MediaItemKind.Artist => "artist",
        MediaItemKind.Podcast => "show",
        MediaItemKind.Episode => "episode",
        _ => "track"
    };
}

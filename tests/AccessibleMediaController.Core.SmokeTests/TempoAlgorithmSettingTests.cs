using System.Text.Json;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.LocalMedia;
using AccessibleMediaController.Core.Podcasts;

/// <summary>
/// Wybor algorytmu tempa jako ustawienie: kontrakt dla przyszlego lekkiego
/// hosta (enum + wlasnosc), domysl ZGODNY ze starym zachowaniem oraz
/// przenoszenie wyboru przez wszystkie istniejace rozstrzygniecia
/// (pliki lokalne, podcasty) i odstepstwa per sesja.
///
/// Celowo NIE sprawdza samego istnienia pola: martwe pole, ktorego nie widzi
/// rozstrzygniecie ustawien, byloby wlasnie bledem, ktory ten test ma wykryc.
/// </summary>
internal static class TempoAlgorithmSettingTests
{
    // Dopasowanie folderow idzie separatorem systemu, wiec sciezki w fixture
    // skladamy przez Path.Combine - inaczej test przechodzi tylko na Windows.
    private static readonly string FolderPath =
        Path.Combine(Path.GetTempPath(), "amc-tempo-fixture", "Audio");
    private static readonly string ItemPath = Path.Combine(FolderPath, "mowa.mp3");

    public static void Run()
    {
        var errors = new List<string>();

        // 1. Kontrakt nazw i wartosci dla lekkiego hosta.
        var contract = new[]
        {
            ("SoundTouch", PlaybackTempoAlgorithm.SoundTouch, 0),
            ("Speech", PlaybackTempoAlgorithm.Speech, 1),
            ("Music", PlaybackTempoAlgorithm.Music, 2),
        };
        foreach (var (name, value, expected) in contract)
        {
            if (Convert.ToInt32(value) != expected)
                errors.Add($"PlaybackTempoAlgorithm.{name} ma numer {Convert.ToInt32(value)}, oczekiwano {expected}.");
        }

        // 2. Stary profil bez tego ustawienia zachowuje DOTYCHCZASOWE zachowanie.
        var fresh = new PlaybackAudioSettings();
        if (fresh.TempoAlgorithm != PlaybackTempoAlgorithm.SoundTouch)
            errors.Add("Domysl nowego ustawienia nie jest dotychczasowym SoundTouch.");

        var legacyJson = """
            {"LoudnessNormalizationEnabled":true,"InterTrackSilenceMilliseconds":500}
            """;
        var legacy = JsonSerializer.Deserialize<PlaybackAudioSettings>(legacyJson)!;
        if (legacy.TempoAlgorithm != PlaybackTempoAlgorithm.SoundTouch)
            errors.Add("Odczyt starego zapisu bez klucza tempa zmienil algorytm.");
        if (!legacy.LoudnessNormalizationEnabled || legacy.InterTrackSilenceMilliseconds != 500)
            errors.Add("Odczyt starego zapisu zgubil dotychczasowe ustawienia.");

        // 3. Pliki lokalne: plik -> folder -> sesja -> ustawienie ogolne.
        var global = new PlaybackAudioSettings { TempoAlgorithm = PlaybackTempoAlgorithm.Music };
        var resolvedGlobal = LocalPlaybackAudioSettingsResolver.Resolve(global, null, null, null);
        if (resolvedGlobal.TempoAlgorithm != PlaybackTempoAlgorithm.Music)
            errors.Add("Rozstrzygniecie plikow lokalnych nie przeniosło ustawienia ogolnego.");

        var session = new SessionPlaybackAudioOverrides
        {
            TempoAlgorithmOverride = PlaybackTempoAlgorithm.Speech
        };
        if (session.IsEmpty) errors.Add("Odstepstwo z samym algorytmem tempa raportuje sie jako puste.");
        var resolvedSession = LocalPlaybackAudioSettingsResolver.Resolve(
            global, ItemPath, null, null, session);
        if (resolvedSession.TempoAlgorithm != PlaybackTempoAlgorithm.Speech)
            errors.Add("Odstepstwo sesji nie przeslonilo ustawienia ogolnego.");

        var folders = new[]
        {
            new LocalFolderPlaybackSettings
            {
                Path = FolderPath,
                TempoAlgorithmOverride = PlaybackTempoAlgorithm.SoundTouch
            }
        };
        var resolvedFolder = LocalPlaybackAudioSettingsResolver.Resolve(
            global, ItemPath, null, folders, session);
        if (resolvedFolder.TempoAlgorithm != PlaybackTempoAlgorithm.SoundTouch)
            errors.Add("Folder nie przeslonil odstepstwa sesji.");

        var item = new LocalMediaItemSettings
        {
            TempoAlgorithmOverride = PlaybackTempoAlgorithm.Speech
        };
        var resolvedItem = LocalPlaybackAudioSettingsResolver.Resolve(
            global, ItemPath, item, folders, session);
        if (resolvedItem.TempoAlgorithm != PlaybackTempoAlgorithm.Speech)
            errors.Add("Ustawienie pojedynczego pliku nie przeslonilo folderu.");

        var withSources = LocalPlaybackAudioSettingsResolver.ResolveWithSources(
            global, ItemPath, item, folders, session);
        if (withSources.TempoAlgorithmSource != LocalPlaybackAudioSettingSource.Item)
            errors.Add("Zrodlo wyboru algorytmu nie wskazuje pliku.");
        var fromFolder = LocalPlaybackAudioSettingsResolver.ResolveWithSources(
            global, ItemPath, null, folders, session);
        if (fromFolder.TempoAlgorithmSource != LocalPlaybackAudioSettingSource.Folder)
            errors.Add("Zrodlo wyboru algorytmu nie wskazuje folderu.");
        var fromGlobal = LocalPlaybackAudioSettingsResolver.ResolveWithSources(
            global, ItemPath, null, null, null);
        if (fromGlobal.TempoAlgorithmSource != LocalPlaybackAudioSettingSource.Global)
            errors.Add("Zrodlo wyboru algorytmu nie wskazuje ustawienia ogolnego.");

        // 4. Podcasty: odcinek -> podcast -> sesja -> ustawienie ogolne.
        var podcastGlobal = new PlaybackAudioSettings
        {
            TempoAlgorithm = PlaybackTempoAlgorithm.Music
        };
        podcastGlobal.OverridesBySession["podcasts"] = new SessionPlaybackAudioOverrides
        {
            TempoAlgorithmOverride = PlaybackTempoAlgorithm.Speech
        };
        var podcastSession = PodcastPlaybackSettingsResolver.ResolveAudio(podcastGlobal, null, null);
        if (podcastSession.TempoAlgorithm != PlaybackTempoAlgorithm.Speech)
            errors.Add("Podcasty nie przenosza odstepstwa sesji dla algorytmu tempa.");
        var podcastEpisode = PodcastPlaybackSettingsResolver.ResolveAudio(
            podcastGlobal,
            new PodcastEpisodeSettings { TempoAlgorithmOverride = PlaybackTempoAlgorithm.SoundTouch },
            new PodcastSubscriptionSettings { TempoAlgorithmOverride = PlaybackTempoAlgorithm.Music });
        if (podcastEpisode.TempoAlgorithm != PlaybackTempoAlgorithm.SoundTouch)
            errors.Add("Ustawienie odcinka nie przeslonilo ustawienia podcastu.");

        if (errors.Count != 0) throw new Exception(string.Join(" | ", errors));
        Console.WriteLine(
            "OK: wybor algorytmu tempa ma kontrakt 0/1/2, domysl SoundTouch dla starych profili "
            + "i przechodzi przez rozstrzygniecia plikow, folderow, sesji i podcastow");
    }
}

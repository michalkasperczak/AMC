using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Xml;
using AccessibleMediaController.Core.Commands;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Devices.WiiM;
using AccessibleMediaController.Core.Input;
using AccessibleMediaController.Core.LocalMedia;
using AccessibleMediaController.Core.Playback;
using AccessibleMediaController.Core.Podcasts;
using AccessibleMediaController.Core.Presentation;
using AccessibleMediaController.Core.Sessions;

var tests = new (string Name, Action Test)[]
{
    ("Normalizacja skrótów", TestKeyChords),
    ("Domyślny profil", TestDefaultProfile),
    ("Odświeżanie profilu wbudowanego", TestBuiltInProfileRefresh),
    ("Czytelne nazwy poleceń", TestCommandCatalog),
    ("Migracja presetów radia do wspólnego magazynu", TestRadioPresetPersistence),
    ("Trwały i odporny harmonogram radia", TestRadioRecordingSchedule),
    ("Szablony nazw zaplanowanych nagrań", TestRadioRecordingFileNameTemplate),
    ("Trwałe ustawienia i historia rozpoznawania utworów", TestRadioRecognitionHistoryPersistence),
    ("Trwałe presety wszystkich sesji", TestSessionPresetPersistence),
    ("Bezpieczny klient i parser urządzeń WiiM", TestWiiMApiParsing),
    ("Bezpieczne parsowanie kanałów podcastów", TestPodcastFeedParsing),
    ("Rozdziały dostawcy podcastu", TestPodcastProviderChapters),
    ("Zwięzłe autorstwo podcastów", TestPodcastMetadataPresentation),
    ("Sortowanie skrzynki Podcastów", TestPodcastInboxOrdering),
    ("Stronicowanie dużych list Podcastów", TestPodcastEpisodePaging),
    ("Kopiowanie opisów i adresów Podcastów", TestPodcastClipboardPresentation),
    ("Bezpieczne nazwy pobranych odcinków Podcastów", TestPodcastDownloadNaming),
    ("Bezpieczny import list podcastów OPML", TestPodcastOpmlParsing),
    ("Aktualizacja biblioteki Podcastów", TestPodcastLibraryUpdate),
    ("Migracja skrzynki Podcastów po starszym imporcie", TestPodcastLegacyInboxMigration),
    ("Stany odsłuchania odcinków Podcastów", TestPodcastEpisodeProgress),
    ("Dziedziczenie opcji odtwarzania Podcastów", TestPodcastPlaybackSettings),
    ("Odwracanie Biblioteki i ulubionych z katalogu Podcastów", TestPodcastMembershipToggle),
    ("Trwały model Podcastów", TestPodcastStatePersistence),
    ("Bezpieczna migracja Podcastów do SQLite", TestPodcastSqliteMigration),
    ("Konfigurowana kolejność odczytu", TestMediaItemFormatting),
    ("Zwięzłe parametry audio", TestAudioParametersFormatting),
    ("Trwałe opcje przetwarzania dźwięku", TestPlaybackAudioSettingsPersistence),
    ("Głośność materiału zależna od wyjścia audio", TestPlaybackVolumeMemory),
    ("Trwałe wyciszenia sesji", TestSessionMutePersistence),
    ("Możliwości przetwarzania dźwięku adaptera", TestPlaybackAudioProcessingCapabilities),
    ("Dziedziczenie przetwarzania dźwięku plików lokalnych", TestLocalPlaybackAudioSettingsInheritance),
    ("Migracja starszych ustawień", TestLegacyStateMigration),
    ("Migracja ustawień alpha.4", TestVersion2StateMigration),
    ("Migracja komunikatów alpha.5", TestVersion3MessageMigration),
    ("Migracja krótkich komunikatów alpha.7", TestVersion4MessageMigration),
    ("Migracja komunikatów z nazwą elementu alpha.8", TestVersion5MessageMigration),
    ("Migracja nazw w komunikatach Ulubionych alpha.18", TestVersion6FavoriteMessageMigration),
    ("Migracja wspólnego wyciszenia alpha.40", TestVersion9PlayerMessageMigration),
    ("Migracja kategorii komunikatów alpha.41", TestVersion10PlayerMessageMigration),
    ("Przełączanie sesji", TestSessions),
    ("Doładowywanie odcinków według stabilnego identyfikatora", TestSessionAddItemsById),
    ("Konfigurowana kolejność sesji", TestSessionOrder),
    ("Pusta sesja lokalna", TestEmptyLocalSession),
    ("Oddzielony tor lokalnego odtwarzania", TestLocalPlaybackBoundary),
    ("Kontekst listy odtwarzania", TestPlaybackContext),
    ("Pamięć domyślnej prędkości po ponownym otwarciu", TestPlaybackRateDefaultPersistence),
    ("Trwała kolejność Kolejki", TestQueueOrder),
    ("Nawigacja Page Up i Page Down w Kolejce", TestQueuePlaybackNavigation),
    ("Zniknięcie bieżącego pliku zachowuje kontekst odtwarzania", TestMissingCurrentItemRecovery),
    ("Polityka pamiętania pozycji", TestResumePositionPolicy),
    ("Odkrywanie lokalnych plików audio", TestLocalAudioFileDiscovery),
    ("Ograniczone sprawdzanie struktury MP3", TestMp3StructureProbe),
    ("Strumień ogromnego pliku bez kopiowania", TestBoundedSubrangeStream),
    ("Ograniczone rozpoznawanie kontenerów multimedialnych", TestMediaContainerProbe),
    ("Ponowne włączanie folderu do biblioteki", TestLocalLibraryImport),
    ("Synchronizacja źródeł lokalnej biblioteki", TestLocalLibrarySynchronization),
    ("Bezpieczna zmiana nazwy lokalnego pliku", TestLocalFileRenamePolicy),
    ("Integracyjny cykl zmian folderu", TestLocalFolderSynchronizationCycle),
    ("Bezpieczne zarządzanie Folderami Biblioteki", TestLocalFolderSourcePolicy),
    ("Ręczne zapominanie niedostępnych rekordów", TestUnavailableLocalItemPolicy),
    ("Trwała kolejność własna Biblioteki", TestLocalLibraryManualOrder),
    ("Albumy rozpoznawane ze struktury folderów", TestLocalAlbumInference),
    ("Migracja i trwałość Biblioteki SQLite", TestSqliteLibraryMigration),
    ("Serializacja równoległych zapisów stanu", TestConcurrentConfigurationSaves),
    ("Migracja biblioteki alpha.79", TestVersion17LocalLibraryMigration),
    ("Naprawa pustego źródła po alpha.80", TestVersion18EmptySourceMigration),
    ("Wyszukiwanie w katalogu", TestCatalogSearch),
    ("Historia wyszukiwania", TestSearchHistory),
    ("Historia odtwarzania", TestPlaybackHistory),
    ("Trwałe zakładki", TestBookmarks),
    ("Trwałe i chronologiczne rozdziały", TestChapters),
    ("Pamięć widoków sesji", TestSessionNavigationPersistence),
    ("Pamięć lokalnej biblioteki", TestLocalMediaPersistence),
    ("Trwałe playlisty", TestPlaylists),
    ("Paleta poleceń", TestCommandPalette),
    ("Dostępny spis skrótów", TestShortcutHelpCatalog),
    ("Cofanie zmian przynależności", TestMembershipHistory),
    ("Zbiorowe zmiany przynależności", TestBatchMembershipCommands),
    ("Folder nie staje się fałszywym elementem kolekcji", TestFolderMembershipGuard),
    ("Częściowy stan folderu w kolekcjach", TestFolderContentsMembership),
    ("Krótkie komunikaty czasu", TestTimeCommands),
    ("Skok wpisanym czasem i procentem", TestSeekInputParser),
    ("Niedestrukcyjne zaznaczanie fragmentu audio", TestAudioClipSelection),
    ("Trzy rodzaje eksportu", TestExports)
};

static void TestWiiMApiParsing()
{
    True(WiiMAddressPolicy.TryNormalize("192.168.1.25", out var address),
        "Prywatny adres IPv4 powinien być dozwolony dla WiiM.");
    Equal("192.168.1.25", address);

    Equal("setPlayerCmd:onepause", WiiMCommands.TogglePlayPause);
    Equal("setPlayerCmd:prev", WiiMCommands.Previous);
    Equal("setPlayerCmd:next", WiiMCommands.Next);
    Equal("setPlayerCmd:vol:100", WiiMCommands.SetVolume(125));
    Equal("setPlayerCmd:vol:0", WiiMCommands.SetVolume(-5));
    Equal("setPlayerCmd:mute:1", WiiMCommands.SetMuted(true));
    Equal("setPlayerCmd:seek:95", WiiMCommands.Seek(TimeSpan.FromSeconds(94.6)));
    Equal("MCUKeyShortClick:12", WiiMCommands.ActivatePreset(12));
    Equal(
        "setPlayerCmd:play:https://example.test/live/stream.m3u8?quality=high",
        WiiMCommands.PlayUrl(" https://example.test/live/stream.m3u8?quality=high#player "));
    Equal(
        $"setPlayerCmd:hex_playlist:{Convert.ToHexString(System.Text.Encoding.UTF8.GetBytes("https://example.test/lista.m3u"))}:0",
        WiiMCommands.PlayNetworkResource("https://example.test/lista.m3u"));
    Equal(
        "setPlayerCmd:play:https://example.test/live/playlist.m3u8",
        WiiMCommands.PlayNetworkResource("https://example.test/live/playlist.m3u8"));
    True(WiiMPlaybackUriPolicy.TryNormalize(
            "https://example.test/audio.mp3#fragment",
            out var playableUri),
        "Publiczny adres HTTPS powinien być dozwolony dla odtwarzania na WiiM.");
    Equal("https://example.test/audio.mp3", playableUri);
    True(!WiiMPlaybackUriPolicy.TryNormalize("file:///D:/Muzyka/test.mp3", out _),
        "Lokalna ścieżka nie może zostać wysłana do WiiM jako adres sieciowy.");
    True(!WiiMPlaybackUriPolicy.TryNormalize("https://user:secret@example.test/audio.mp3", out _),
        "Adres zawierający dane logowania nie może zostać wysłany do WiiM.");
    Equal("setPlayerCmd:switchmode:optical", WiiMCommands.SwitchInput("optical"));
    Equal("setAudioOutputHardwareMode:2", WiiMCommands.SetAudioOutputHardwareMode(2));
    Equal("EQLoad:Spoken Word", WiiMCommands.LoadEqualizerPreset("Spoken Word"));
    Equal("setPlayerCmd:loopmode:-1", WiiMCommands.SetLoopMode(-1));
    Equal("setShutdown:3600", WiiMCommands.SetSleepTimer(3600));
    True(WiiMCommands.ResponseIndicatesFailure("{\"status\":\"Failed\"}"),
        "Odrzucone polecenie urządzenia powinno zostać wykryte.");
    True(!WiiMCommands.ResponseIndicatesFailure("OK"),
        "Odpowiedź OK urządzenia nie może być traktowana jako błąd.");
    True(WiiMAddressPolicy.TryNormalize("https://10.0.0.8/httpapi.asp", out address),
        "Adres urządzenia podany jako HTTPS powinien zostać znormalizowany.");
    Equal("10.0.0.8", address);
    True(!WiiMAddressPolicy.TryNormalize("8.8.8.8", out _),
        "Klient WiiM nie może łączyć się z publicznym adresem IP.");
    True(!WiiMAddressPolicy.TryNormalize("127.0.0.1", out _),
        "Klient WiiM nie może łączyć się z adresem zwrotnym.");

    const string statusJson = """
        {"DeviceName":"Salon","uuid":"wiim-1","project":"WiiM_Pro","firmware":"4.8.7000","preset_key":"12"}
        """;
    var device = WiiMApiParser.ParseDeviceInformation(statusJson, "192.168.1.25");
    Equal("Salon", device.Name);
    Equal("wiim-1", device.Id);
    Equal("WiiM Pro", device.Model);
    Equal(12, device.PresetButtonCount);

    var playback = WiiMApiParser.ParsePlaybackInformation(
        "{\"status\":\"play\",\"mode\":\"32\",\"curpos\":\"184919\",\"totlen\":\"300000\",\"vol\":\"39\",\"mute\":\"0\",\"loop\":\"2\",\"eq\":\"7\",\"uri\":\"https://example.test/current\"}");
    Equal("odtwarzanie", playback.State);
    Equal("TIDAL Connect", playback.Source);
    Equal(39, playback.Volume);
    Equal(TimeSpan.FromMilliseconds(184919), playback.Position);
    Equal(2, playback.LoopMode);
    Equal(7, playback.EqualizerPresetNumber);
    Equal(32, playback.RawMode);
    Equal("https://example.test/current", playback.ContentUri);

    True(WiiMApiParser.ParseEqualizerEnabled("{\"EQStat\":\"On\"}"),
        "Włączony korektor WiiM powinien zostać rozpoznany.");
    var equalizerPresets = WiiMApiParser.ParseEqualizerPresets("[\"Flat\",\"Spoken Word\",\"Flat\"]");
    Equal(2, equalizerPresets.Count);
    Equal("Spoken Word", equalizerPresets[1]);
    Equal(2, WiiMApiParser.ParseAudioOutputHardwareMode("{\"hardware\":\"2\",\"source\":\"0\"}"));

    var metadata = WiiMApiParser.ParseTrackInformation(
        "{\"metaData\":{\"title\":\"Utwór\",\"artist\":\"Wykonawca\",\"album\":\"Album\",\"sampleRate \":\"48000\",\"bitDepth\":\"24\"}}");
    Equal("Utwór", metadata.Title);
    Equal(48000, metadata.SampleRateHz);
    Equal(24, metadata.BitDepth);

    var radioMetadata = WiiMApiParser.ParseTrackInformation(
        "{\"metaData\":{\"title\":\"playlist.m3u8\",\"subtitle\":\"Poranny gość\",\"artist\":\"unknow\",\"bitRate\":\"196\"}}");
    Equal("Poranny gość", radioMetadata.Subtitle);
    Equal(string.Empty, radioMetadata.Artist);
    Equal(196, radioMetadata.BitrateKbps);
    var statusMetadata = WiiMApiParser.ParsePlayerTrackInformation(
        "{\"Title\":\"526164696F20527A65737A6F77\",\"Artist\":\"4A616E204B6F77616C736B69\",\"Album\":\"\"}");
    Equal("Radio Rzeszow", statusMetadata.Title);
    Equal("Jan Kowalski", statusMetadata.Artist);
    var mergedMetadata = WiiMApiParser.MergeTrackInformation(
        radioMetadata with { Title = string.Empty },
        statusMetadata);
    Equal("Radio Rzeszow", mergedMetadata.Title);
    Equal("Poranny gość", mergedMetadata.Subtitle);

    const string upnpXml = """
        <s:Envelope xmlns:s="http://schemas.xmlsoap.org/soap/envelope/"><s:Body>
        <u:GetInfoExResponse xmlns:u="urn:schemas-upnp-org:service:AVTransport:1">
        <TrackMetaData>&lt;DIDL-Lite xmlns:dc="http://purl.org/dc/elements/1.1/" xmlns:upnp="urn:schemas-upnp-org:metadata-1-0/upnp/" xmlns:song="www.wiimu.com/song/" xmlns="urn:schemas-upnp-org:metadata-1-0/DIDL-Lite/"&gt;&lt;item&gt;&lt;dc:title&gt;Audycja dnia&lt;/dc:title&gt;&lt;upnp:artist&gt;Prowadzący&lt;/upnp:artist&gt;&lt;song:bitrate&gt;192000&lt;/song:bitrate&gt;&lt;song:rate_hz&gt;48000&lt;/song:rate_hz&gt;&lt;/item&gt;&lt;/DIDL-Lite&gt;</TrackMetaData>
        <TrackURI>https://example.test/radio.m3u8</TrackURI>
        </u:GetInfoExResponse></s:Body></s:Envelope>
        """;
    var upnp = WiiMApiParser.ParseUpnpPlaybackInformation(upnpXml);
    Equal("https://example.test/radio.m3u8", upnp.ContentUri);
    Equal("Audycja dnia", upnp.Track.Title);
    Equal("Prowadzący", upnp.Track.Artist);
    Equal(192, upnp.Track.BitrateKbps);

    var presets = WiiMApiParser.ParsePresets(
        "{\"preset_list\":[{\"number\":\"2\",\"name\":\"Radio\",\"source\":\"TuneIn\",\"url\":\"https://example.test/radio\"},{\"number\":\"13\",\"name\":\"Poza zakresem\"}]}");
    Equal(1, presets.Count);
    Equal(2, presets[0].Number);
    Equal("Radio", presets[0].Name);

    var presetSnapshot = new WiiMDeviceSnapshot(
        device,
        playback with { RawMode = 12, ContentUri = "https://example.test/radio" },
        WiiMApiParser.EmptyTrack(),
        presets);
    Equal(2, WiiMPresetStateResolver.ResolveCurrentPreset(presetSnapshot, 0));
    Equal(2, WiiMPresetStateResolver.ResolveCurrentPreset(
        presetSnapshot with { Playback = presetSnapshot.Playback with { ContentUri = null } },
        2));
    Equal(2, WiiMPresetStateResolver.ResolveCurrentPreset(
        presetSnapshot with { Playback = presetSnapshot.Playback with { RawMode = 20, ContentUri = null } },
        2));
    True(WiiMPresetStateResolver.ResolveCurrentPreset(
            presetSnapshot with { Playback = presetSnapshot.Playback with { RawMode = 32, ContentUri = null } },
            2) is null,
        "Odtwarzanie przez TIDAL Connect nie może udawać ostatniego presetu WiiM.");
    Equal(2, WiiMPresetStateResolver.ResolveCurrentPreset(
        presetSnapshot with { Playback = presetSnapshot.Playback with { RawMode = 32, ContentUri = null } },
        2,
        trustRememberedNetworkPreset: true));
    True(WiiMPresetStateResolver.ResolveCurrentPreset(
            presetSnapshot with { Playback = presetSnapshot.Playback with { RawMode = 43, ContentUri = null } },
            2,
            trustRememberedNetworkPreset: true) is null,
        "Wejście optyczne nie może udawać zapamiętanego presetu WiiM.");

    const string ssdp = "HTTP/1.1 200 OK\r\nLOCATION: http://192.168.1.25:49152/description.xml\r\nST: urn:schemas-upnp-org:device:MediaRenderer:1\r\n\r\n";
    True(WiiMDiscoveryService.TryParseResponse(ssdp, out address),
        "Odpowiedź SSDP urządzenia w sieci lokalnej powinna zostać rozpoznana.");
    Equal("192.168.1.25", address);

    var directory = Path.Combine(Path.GetTempPath(), $"amc-wiim-tests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        var store = new ConfigurationStore(Path.Combine(directory, "state.json"));
        var state = ConfigurationStore.CreateDefaultState();
        state.WiiM.Devices.Add(new WiiMDeviceSettings
        {
            Id = "wiim-1",
            Address = "192.168.1.25",
            DisplayName = "Salon",
            Model = "WiiM Pro",
            Firmware = "4.8.7000",
            LastActivatedPresetNumber = 2
        });
        state.WiiM.SelectedDeviceId = "wiim-1";
        state.WiiM.NetworkStreams.Add(new WiiMNetworkStreamSettings
        {
            Id = "wiim:stream:1",
            Name = "Radio testowe",
            StreamUrl = "https://radio.example/live.m3u8",
            AddedUtcTicks = 123456
        });
        state.WiiM.NetworkStreams.Add(new WiiMNetworkStreamSettings
        {
            Id = "wiim:stream:duplicate",
            Name = "Duplikat",
            StreamUrl = "https://radio.example/live.m3u8"
        });
        state.WiiM.NetworkStreams.Add(new WiiMNetworkStreamSettings
        {
            Id = "wiim:stream:unsafe",
            Name = "Niedozwolony",
            StreamUrl = "file:///C:/private.mp3"
        });
        store.Save(state);
        var loaded = store.LoadOrCreate();
        Equal(ConfigurationStore.CurrentSchemaVersion, loaded.SchemaVersion);
        Equal(1, loaded.WiiM.Devices.Count);
        Equal("Salon", loaded.WiiM.Devices[0].DisplayName);
        Equal("wiim-1", loaded.WiiM.SelectedDeviceId);
        Equal(2, loaded.WiiM.Devices[0].LastActivatedPresetNumber);
        True(loaded.WiiM.Devices[0].LastActivatedNetworkStreamId is null,
            "Preset urządzenia nie może jednocześnie wskazywać lokalnego strumienia WiiM.");
        Equal(1, loaded.WiiM.NetworkStreams.Count);
        Equal("Radio testowe", loaded.WiiM.NetworkStreams[0].Name);
        Equal("https://radio.example/live.m3u8", loaded.WiiM.NetworkStreams[0].StreamUrl);
        loaded.WiiM.Devices[0].LastActivatedNetworkStreamId = "wiim:stream:1";
        loaded.WiiM.Devices[0].LastActivatedPresetNumber = 2;
        store.Save(loaded);
        loaded = store.LoadOrCreate();
        Equal("wiim:stream:1", loaded.WiiM.Devices[0].LastActivatedNetworkStreamId);
        Equal(0, loaded.WiiM.Devices[0].LastActivatedPresetNumber);
        var playlist = Encoding.UTF8.GetString(WiiMNetworkStreamPlaylistWriter.Write([
            loaded.WiiM.NetworkStreams[0],
            new WiiMNetworkStreamSettings
            {
                Name = "Radio\r\nDrugie",
                StreamUrl = "https://example.test/live#fragment"
            },
            new WiiMNetworkStreamSettings
            {
                Name = "Prywatny adres",
                StreamUrl = "https://login:haslo@example.test/live"
            }
        ]));
        True(playlist.StartsWith("#EXTM3U\r\n", StringComparison.Ordinal),
            "Eksport strumieni WiiM nie tworzy rozszerzonej playlisty M3U.");
        True(playlist.Contains("#EXTINF:-1,Radio Drugie\r\nhttps://example.test/live", StringComparison.Ordinal),
            "Eksport nie oczyścił nazwy albo fragmentu adresu strumienia.");
        True(playlist.IndexOf("Radio Drugie", StringComparison.Ordinal)
                < playlist.IndexOf("Radio testowe", StringComparison.Ordinal),
            "Eksport dla WiiM Home musi kompensować dodawanie importowanych wpisów na początek listy.");
        True(!playlist.Contains("haslo", StringComparison.Ordinal),
            "Eksport nie może zapisać adresu z osadzonymi danymi logowania.");
    }
    finally
    {
        Directory.Delete(directory, true);
    }
}

static void TestPodcastFeedParsing()
{
    const string rss = """
        <?xml version="1.0" encoding="UTF-8"?>
        <rss version="2.0" xmlns:itunes="http://www.itunes.com/dtds/podcast-1.0.dtd" xmlns:podcast="https://podcastindex.org/namespace/1.0">
          <channel>
            <title>Próba &amp; Podcast</title>
            <link>https://example.test/podcast</link>
            <description><![CDATA[<p>Opis <strong>audycji</strong>.</p>]]></description>
            <itunes:author>Autor kanału</itunes:author>
            <item>
              <guid>odcinek-1</guid>
              <title>Odcinek pierwszy</title>
              <pubDate>Tue, 01 Sep 2026 18:30:00 GMT</pubDate>
              <itunes:duration>01:02:03</itunes:duration>
              <description><![CDATA[<p>Pierwszy akapit.</p><p><a href="https://example.test/material">Materiały do odcinka</a></p>]]></description>
              <enclosure url="https://cdn.example.test/audio/1.mp3" length="123456" type="audio/mpeg" />
              <podcast:chapters url="https://cdn.example.test/chapters/1.json" type="application/json+chapters" />
              <link>/podcast/1</link>
            </item>
            <item>
              <title>Wpis bez dźwięku</title>
              <link>https://example.test/text</link>
            </item>
            <item>
              <guid>odcinek-z-obrazem</guid>
              <title>Odcinek z ilustracją przed dźwiękiem</title>
              <enclosure url="https://cdn.example.test/artwork.jpg" type="image/jpeg" />
              <media:content xmlns:media="http://search.yahoo.com/mrss/" url="https://cdn.example.test/audio/2.mp3" type="audio/mpeg" />
            </item>
            <item>
              <guid>sam-obraz</guid>
              <title>Artykuł udający odcinek</title>
              <enclosure url="https://cdn.example.test/photo.jpg" type="image/jpeg" />
            </item>
          </channel>
        </rss>
        """;
    var feedUri = new Uri("https://example.test/feed.xml");
    var feed = PodcastFeedParser.Parse(rss, feedUri);
    Equal("Próba & Podcast", feed.Title);
    Equal("Autor kanału", feed.Author);
    Equal("Opis audycji.", feed.Description);
    Equal(new Uri("https://example.test/podcast"), feed.HomepageUri);
    Equal(2, feed.Episodes.Count);
    var episode = feed.Episodes[0];
    Equal("Odcinek pierwszy", episode.Title);
    Equal(
        $"Pierwszy akapit.{Environment.NewLine}Materiały do odcinka: https://example.test/material",
        episode.Description);
    Equal(TimeSpan.FromHours(1) + TimeSpan.FromMinutes(2) + TimeSpan.FromSeconds(3), episode.Duration);
    Equal(new Uri("https://cdn.example.test/audio/1.mp3"), episode.MediaUri);
    Equal(new Uri("https://example.test/podcast/1"), episode.PageUri);
    Equal("audio/mpeg", episode.MediaType);
    Equal(123456L, episode.MediaLength);
    Equal(new Uri("https://cdn.example.test/chapters/1.json"), episode.ChaptersUri);
    Equal(0, episode.Chapters?.Count ?? -1);
    Equal(episode.Id, PodcastFeedParser.Parse(rss, feedUri).Episodes[0].Id);
    Equal(
        new Uri("https://cdn.example.test/audio/2.mp3"),
        feed.Episodes.Single(item => item.SourceIdentifier == "odcinek-z-obrazem").MediaUri);
    True(feed.Episodes.All(item => !item.MediaUri.AbsolutePath.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)),
        "Ilustracja RSS nie może zostać potraktowana jako audio podcastu.");

    const string atom = """
        <feed xmlns="http://www.w3.org/2005/Atom">
          <id>urn:test:podcast</id>
          <title>Atom Podcast</title>
          <author><name>Anna</name></author>
          <link rel="alternate" href="https://example.test/atom" />
            <entry>
              <id>urn:test:episode:1</id>
              <title>Atom odcinek</title>
              <updated>2026-09-01T12:00:00Z</updated>
              <itunes:duration xmlns:itunes="http://www.itunes.com/dtds/podcast-1.0.dtd">3723</itunes:duration>
              <link rel="enclosure" href="media/atom.m4a" type="audio/mp4" length="42" />
            </entry>
        </feed>
        """;
    var atomFeed = PodcastFeedParser.Parse(atom, new Uri("https://example.test/feed/atom.xml"));
    Equal("Atom Podcast", atomFeed.Title);
    Equal("Anna", atomFeed.Author);
    Equal(1, atomFeed.Episodes.Count);
    Equal("Anna", atomFeed.Episodes[0].Author);
    Equal(TimeSpan.FromSeconds(3723), atomFeed.Episodes[0].Duration);
    Equal(new Uri("https://example.test/feed/media/atom.m4a"), atomFeed.Episodes[0].MediaUri);

    var rejectedDtd = false;
    try
    {
        PodcastFeedParser.Parse(
            "<!DOCTYPE rss [<!ENTITY xxe SYSTEM 'file:///c:/windows/win.ini'>]><rss><channel><title>&xxe;</title></channel></rss>",
            feedUri);
    }
    catch (XmlException)
    {
        rejectedDtd = true;
    }
    True(rejectedDtd, "Parser podcastów musi odrzucać DTD i encje zewnętrzne.");
}

static void TestPodcastProviderChapters()
{
    const string json = """
        {
          "version": "1.2.0",
          "chapters": [
            { "startTime": 65.5, "title": "Rozmowa" },
            { "startTime": 0, "title": "Wstęp" },
            { "startTime": 30, "title": "Ukryty", "toc": false },
            { "startTime": -1, "title": "Błędny" }
          ]
        }
        """;
    var jsonChapters = PodcastJsonChapterParser.Parse(json);
    Equal(2, jsonChapters.Count);
    Equal("Wstęp", jsonChapters[0].Name);
    Equal(TimeSpan.FromSeconds(65.5), jsonChapters[1].Start);

    var descriptionChapters = PodcastDescriptionChapterParser.Parse(
        $"00:00 Wprowadzenie{Environment.NewLine}12:34 - Temat główny{Environment.NewLine}1:02:03 Zakończenie",
        TimeSpan.FromHours(2));
    Equal(3, descriptionChapters.Count);
    Equal(TimeSpan.FromMinutes(12) + TimeSpan.FromSeconds(34), descriptionChapters[1].Start);
    Equal(0, PodcastDescriptionChapterParser.Parse("12:34 tylko jeden znacznik", TimeSpan.FromHours(1)).Count);
    var trailingTimeChapters = PodcastDescriptionChapterParser.Parse(
        $"Intro 00:00:00{Environment.NewLine}Temat główny 00:12:34{Environment.NewLine}Zakończenie 01:02:03",
        TimeSpan.FromHours(2));
    Equal(3, trailingTimeChapters.Count);
    Equal("Temat główny", trailingTimeChapters[1].Name);

    const string episodePage = """
        <html><head><style>.time{display:none}</style><script>const fake = '09:09';</script></head><body>
        <p>Opis zawierający godzinę 12:00, który nie jest rozdziałem.</p>
        <h3>Znaczniki czasu:</h3>
        <p>Intro 00:00:00<br>Rozmowa 00:12:34<br>Zakończenie 01:02:03</p>
        </body></html>
        """;
    var pageChapters = PodcastEpisodePageChapterParser.Parse(episodePage, TimeSpan.FromHours(2));
    Equal(3, pageChapters.Count);
    Equal("Rozmowa", pageChapters[1].Name);
    const string chapterCommentPage = """
        <html><body><div class="comment"><strong>TyfloPodcast pisze:</strong>
        znaczniki czasu:<br>Intro 00:00:00<br>Temat 00:02:42</div></body></html>
        """;
    var commentChapters = PodcastEpisodePageChapterParser.Parse(
        chapterCommentPage,
        TimeSpan.FromHours(1));
    Equal(2, commentChapters.Count);
    Equal("Temat", commentChapters[1].Name);
    Equal(0, PodcastEpisodePageChapterParser.Parse(
        "<p>Odtwarzacz 01:23:45</p><p>Komentarz o 12:30</p>",
        TimeSpan.FromHours(2)).Count);

    const string psc = """
        <rss xmlns:psc="http://podlove.org/simple-chapters" xmlns:itunes="http://www.itunes.com/dtds/podcast-1.0.dtd"><channel><title>PSC</title><item>
          <guid>psc-1</guid><title>Odcinek</title><enclosure url="https://example.test/a.mp3" />
          <itunes:duration>10:00</itunes:duration>
          <psc:chapters><psc:chapter start="00:00:00" title="Początek"/><psc:chapter start="00:03:20" title="Drugi"/></psc:chapters>
        </item></channel></rss>
        """;
    var parsed = PodcastFeedParser.Parse(psc, new Uri("https://example.test/feed.xml"));
    Equal(2, parsed.Episodes[0].Chapters?.Count ?? -1);
    Equal("Drugi", parsed.Episodes[0].Chapters?[1].Name);

    var podcastSettings = new PodcastSettings();
    var bookmarks = new BookmarkSettings();
    var update = PodcastLibraryUpdater.Apply(
        podcastSettings,
        parsed,
        null,
        DateTime.UtcNow,
        bookmarks);
    var importedEpisode = podcastSettings.Episodes.Single();
    Equal(2, new ChapterIndex(bookmarks).GetForItem(
        "podcasts",
        importedEpisode.Id,
        TimeSpan.FromTicks(importedEpisode.DurationTicks)).Count);
    Equal(update.Subscription.Id, importedEpisode.SubscriptionId);

    var chapterIndex = new ChapterIndex(bookmarks);
    importedEpisode.ProviderChaptersUrl = "https://example.test/chapters/psc-1.json";
    importedEpisode.ProviderChaptersLoadedUrl = importedEpisode.ProviderChaptersUrl;
    chapterIndex.ReplaceProviderChapters(
        "podcasts",
        "Podcasty",
        new MediaItem
        {
            Id = importedEpisode.Id,
            Title = importedEpisode.Title,
            Artist = parsed.Title,
            Kind = MediaItemKind.Episode,
            Duration = TimeSpan.FromTicks(importedEpisode.DurationTicks),
            Source = importedEpisode.MediaUrl
        },
        "podcast-json",
        [new ProviderChapterPoint("json-1", "Dodatkowy rozdział", TimeSpan.FromMinutes(8))],
        DateTime.UtcNow);

    // A temporary, incomplete RSS response must not erase chapters that AMC
    // has already stored. Some publishers intermittently omit both PSC and
    // Podcasting 2.0 chapter metadata from otherwise valid feed responses.
    var incomplete = parsed with
    {
        Episodes =
        [
            parsed.Episodes[0] with
            {
                ChaptersUri = null,
                Chapters = null
            }
        ]
    };
    PodcastLibraryUpdater.Apply(
        podcastSettings,
        incomplete,
        null,
        DateTime.UtcNow.AddMinutes(1),
        bookmarks);
    Equal(true, importedEpisode.HasFeedChapters);
    Equal("https://example.test/chapters/psc-1.json", importedEpisode.ProviderChaptersUrl);
    Equal("https://example.test/chapters/psc-1.json", importedEpisode.ProviderChaptersLoadedUrl);
    Equal(3, chapterIndex.GetForItem(
        "podcasts",
        importedEpisode.Id,
        TimeSpan.FromTicks(importedEpisode.DurationTicks)).Count);
}

static void TestPodcastMetadataPresentation()
{
    Equal("Polskie Radio PiK", PodcastMetadataPresentation.FormatAuthor("℗&© Polskie Radio PiK"));
    Equal("Radio", PodcastMetadataPresentation.FormatAuthor(" © / ℗ | Radio "));
    Equal("2026 Wydawca", PodcastMetadataPresentation.FormatAuthor("(c) 2026 Wydawca"));
    Equal("Simon & Schuster", PodcastMetadataPresentation.FormatAuthor("Simon & Schuster"));
    Equal(string.Empty, PodcastMetadataPresentation.FormatAuthor("© & ℗"));
}

static void TestPodcastInboxOrdering()
{
    var subscriptions = new[]
    {
        new PodcastSubscriptionSettings { Id = "b", Title = "Beta" },
        new PodcastSubscriptionSettings { Id = "a", Title = "Alfa" }
    };
    var episodes = new[]
    {
        new PodcastEpisodeSettings { Id = "3", SubscriptionId = "b", Title = "Adam", PublishedUtcTicks = 30 },
        new PodcastEpisodeSettings { Id = "2", SubscriptionId = "a", Title = "Zenon", PublishedUtcTicks = 20 },
        new PodcastEpisodeSettings { Id = "1", SubscriptionId = "a", Title = "Beata", PublishedUtcTicks = 10 }
    };

    Equal(
        "3,2,1",
        string.Join(',', PodcastInboxOrdering.Order(
            episodes,
            subscriptions,
            CollectionSortMode.AddedNewest).Select(episode => episode.Id)));
    Equal(
        "3,1,2",
        string.Join(',', PodcastInboxOrdering.Order(
            episodes,
            subscriptions,
            CollectionSortMode.Alphabetical).Select(episode => episode.Id)));
    Equal(
        "2,1,3",
        string.Join(',', PodcastInboxOrdering.Order(
            episodes,
            subscriptions,
            CollectionSortMode.Custom).Select(episode => episode.Id)));
}

static void TestPodcastEpisodePaging()
{
    Equal(0, PodcastEpisodePaging.ResolveLoadedCount(0, 0));
    Equal(120, PodcastEpisodePaging.ResolveLoadedCount(120, 0));
    Equal(150, PodcastEpisodePaging.ResolveLoadedCount(2_000, 0));
    Equal(450, PodcastEpisodePaging.ResolveLoadedCount(2_000, 150, 300));
    Equal(900, PodcastEpisodePaging.ResolveLoadedCount(2_000, 900, 10));
    Equal(300, PodcastEpisodePaging.ResolveNextLoadedCount(2_000, 150));
    Equal(2_000, PodcastEpisodePaging.ResolveNextLoadedCount(2_000, 1_900));

    try
    {
        PodcastEpisodePaging.ResolveLoadedCount(10, 0, pageSize: 0);
        throw new InvalidOperationException("Zerowy rozmiar strony powinien zostać odrzucony.");
    }
    catch (ArgumentOutOfRangeException)
    {
    }
}

static void TestSessionAddItemsById()
{
    var sharedAddress = "https://example.test/shared-audio.mp3";
    var session = new DemoMediaSession(
        "podcasts",
        "Podcasty",
        [new MediaItem { Id = "episode-1", Title = "Pierwszy", Source = sharedAddress }]);

    session.AddItemsById(
    [
        new MediaItem { Id = "episode-2", Title = "Drugi", Source = sharedAddress },
        new MediaItem { Id = "episode-1", Title = "Duplikat", Source = "https://example.test/other.mp3" }
    ]);

    Equal(2, session.Items.Count);
    Equal("episode-2", session.Items[1].Id);
}

static void TestPodcastClipboardPresentation()
{
    var entries = new[]
    {
        new PodcastClipboardEntry(
            "Odcinek pierwszy",
            "Opis pierwszego odcinka.",
            "https://example.com/episodes/1",
            "https://cdn.example.com/audio/1.mp3"),
        new PodcastClipboardEntry(
            "Odcinek drugi",
            "Opis drugi.\nDrugi akapit.",
            "https://example.com/episodes/2",
            "https://cdn.example.com/audio/2.mp3")
    };

    var publicText = PodcastClipboardPresentation.FormatPublicDetails(entries);
    True(publicText.Contains(string.Join(Environment.NewLine,
            "Odcinek pierwszy",
            "Opis pierwszego odcinka.",
            "https://example.com/episodes/1")),
        "Ctrl+C powinien łączyć nazwę, opis i publiczną stronę odcinka.");
    True(publicText.Contains(Environment.NewLine + Environment.NewLine + "Odcinek drugi"),
        "Wiele odcinków powinno tworzyć osobne, czytelne bloki.");
    Equal(
        "https://cdn.example.com/audio/1.mp3" + Environment.NewLine +
        "https://cdn.example.com/audio/2.mp3",
        PodcastClipboardPresentation.FormatDirectUrls(entries));
}

static void TestPodcastStatePersistence()
{
    var directory = Path.Combine(Path.GetTempPath(), $"amc-podcast-tests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        var store = new ConfigurationStore(
            Path.Combine(directory, "state.json"),
            Path.Combine(directory, "library.db"));
        var state = ConfigurationStore.CreateDefaultState();
        state.Podcasts.DownloadsFolder = Path.Combine(directory, "pobrane");
        state.Podcasts.Volume = 44;
        state.Podcasts.PlaybackRate = 1.25d;
        state.Podcasts.Subscriptions.Add(new PodcastSubscriptionSettings
        {
            Id = "podcast-a",
            Title = "Podcast A",
            FeedUrl = "https://example.test/feed.xml",
            HomepageUrl = "javascript:alert(1)",
            LastRefreshUtcTicks = DateTime.UtcNow.Ticks,
            RefreshIntervalMinutes = 60,
            DownloadsFolder = Path.Combine(directory, "podcast-a"),
            ResumePositionMode = ResumePositionMode.StartFromBeginning,
            PlaybackRateOverride = 1.5d,
            LoudnessNormalizationOverride = true
        });
        state.Podcasts.Episodes.Add(new PodcastEpisodeSettings
        {
            Id = "episode-a",
            SubscriptionId = "podcast-a",
            SourceIdentifier = "guid-a",
            Title = "Odcinek A",
            MediaUrl = "https://cdn.example.test/a.mp3",
            ProviderChaptersUrl = "https://cdn.example.test/a.chapters.json",
            ProviderChaptersLoadedUrl = "https://cdn.example.test/a.chapters.json",
            EmbeddedChaptersSignature = "C:\\Podcasty\\a.mp3|123|456",
            HasFeedChapters = true,
            DurationTicks = TimeSpan.FromMinutes(30).Ticks,
            ResumePositionTicks = TimeSpan.FromMinutes(5).Ticks,
            ResumePositionMode = ResumePositionMode.Remember,
            PlaybackRateOverride = 0.75d,
            SmoothTrackTransitionsOverride = true,
            IsNew = true,
            IsFavorite = true
        });
        state.Podcasts.CurrentItemId = "episode-a";
        store.Save(state);

        var loaded = store.LoadOrCreate();
        Equal(ConfigurationStore.CurrentSchemaVersion, loaded.SchemaVersion);
        Equal("podcasts", loaded.Settings.SessionSlots[6]);
        Equal(44, loaded.Podcasts.Volume);
        Equal(1.25d, loaded.Podcasts.PlaybackRate);
        Equal(Path.Combine(directory, "pobrane"), loaded.Podcasts.DownloadsFolder);
        Equal(1, loaded.Podcasts.Subscriptions.Count);
        Equal(null, loaded.Podcasts.Subscriptions[0].HomepageUrl);
        Equal(60, loaded.Podcasts.Subscriptions[0].RefreshIntervalMinutes);
        Equal(Path.Combine(directory, "podcast-a"), loaded.Podcasts.Subscriptions[0].DownloadsFolder);
        Equal(ResumePositionMode.StartFromBeginning, loaded.Podcasts.Subscriptions[0].ResumePositionMode);
        Equal(1.5d, loaded.Podcasts.Subscriptions[0].PlaybackRateOverride);
        Equal(true, loaded.Podcasts.Subscriptions[0].LoudnessNormalizationOverride);
        Equal(1, loaded.Podcasts.Episodes.Count);
        Equal(TimeSpan.FromMinutes(5).Ticks, loaded.Podcasts.Episodes[0].ResumePositionTicks);
        Equal(ResumePositionMode.Remember, loaded.Podcasts.Episodes[0].ResumePositionMode);
        Equal(0.75d, loaded.Podcasts.Episodes[0].PlaybackRateOverride);
        Equal(true, loaded.Podcasts.Episodes[0].SmoothTrackTransitionsOverride);
        Equal("https://cdn.example.test/a.chapters.json", loaded.Podcasts.Episodes[0].ProviderChaptersUrl);
        Equal("https://cdn.example.test/a.chapters.json", loaded.Podcasts.Episodes[0].ProviderChaptersLoadedUrl);
        Equal("C:\\Podcasty\\a.mp3|123|456", loaded.Podcasts.Episodes[0].EmbeddedChaptersSignature);
        Equal(true, loaded.Podcasts.Episodes[0].HasFeedChapters);
        Equal(false, loaded.Podcasts.Episodes[0].IsNew);
        Equal(true, loaded.Podcasts.Episodes[0].IsStarted);
        Equal("episode-a", loaded.Podcasts.CurrentItemId);
    }
    finally
    {
        Directory.Delete(directory, true);
    }
}

static void TestPodcastSqliteMigration()
{
    var directory = Path.Combine(Path.GetTempPath(), $"amc-podcast-sqlite-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        var statePath = Path.Combine(directory, "state.json");
        var databasePath = Path.Combine(directory, "podcasts.db");
        var state = ConfigurationStore.CreateDefaultState();
        state.Podcasts.Subscriptions.Add(new PodcastSubscriptionSettings
        {
            Id = "migration-podcast",
            Title = "Podcast migracyjny",
            Description = new string('o', 2048),
            FeedUrl = "https://example.test/migration.xml"
        });
        for (var index = 0; index < 200; index++)
        {
            state.Podcasts.Episodes.Add(new PodcastEpisodeSettings
            {
                Id = $"migration-episode-{index}",
                SubscriptionId = "migration-podcast",
                Title = $"Odcinek {index}",
                Description = new string('x', 1024),
                MediaUrl = $"https://cdn.example.test/{index}.mp3",
                PublishedUtcTicks = DateTime.UtcNow.AddDays(-index).Ticks,
                IsFavorite = index == 17,
                ResumePositionTicks = index == 17 ? TimeSpan.FromMinutes(3).Ticks : 0
            });
        }
        state.Podcasts.Episodes.Add(new PodcastEpisodeSettings
        {
            Id = "migration-artwork",
            SubscriptionId = "migration-podcast",
            Title = "Błędna ilustracja RMF",
            MediaUrl = "https://cdn.example.test/attachment.jpg",
            MediaType = "image/jpeg"
        });
        File.WriteAllText(
            statePath,
            JsonSerializer.Serialize(state, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters = { new JsonStringEnumConverter() }
            }));

        var store = new ConfigurationStore(
            statePath,
            Path.Combine(directory, "library.db"),
            databasePath);
        var migrated = store.LoadOrCreate();
        Equal(1, migrated.Podcasts.Subscriptions.Count);
        Equal(200, migrated.Podcasts.Episodes.Count);
        Equal(true, migrated.Podcasts.Episodes.Single(item => item.Id == "migration-episode-17").IsFavorite);
        Equal(TimeSpan.FromMinutes(3).Ticks,
            migrated.Podcasts.Episodes.Single(item => item.Id == "migration-episode-17").ResumePositionTicks);
        True(File.Exists(databasePath), "Nie utworzono bazy Podcastów.");
        True(File.Exists(Path.Combine(directory, "state.pre-podcast-sqlite-migration.json")),
            "Nie zachowano kopii stanu sprzed migracji Podcastów.");
        var compactJson = File.ReadAllText(statePath);
        True(!compactJson.Contains("migration-episode-17", StringComparison.Ordinal),
            "Lekki state.json nadal zawiera archiwum odcinków.");

        var loadedAgain = new ConfigurationStore(
            statePath,
            Path.Combine(directory, "library.db"),
            databasePath).LoadOrCreate();
        Equal(200, loadedAgain.Podcasts.Episodes.Count);
        Equal("Podcast migracyjny", loadedAgain.Podcasts.Subscriptions[0].Title);
    }
    finally
    {
        Directory.Delete(directory, true);
    }
}

static void TestPodcastDownloadNaming()
{
    Equal(
        "Odcinek - specjalny.m4a",
        PodcastDownloadNaming.SuggestedFileName(
            "Odcinek : specjalny?",
            "https://cdn.example.test/audio?id=1",
            "audio/mp4; charset=binary"));
    Equal(
        "Rozmowa - której nie było - gość OConnor.mp3",
        PodcastDownloadNaming.SuggestedFileName(
            "„Rozmowa”, której nie było: gość O'Connor",
            "https://cdn.example.test/audio.mp3",
            "audio/mpeg"));
    Equal(
        "_CON.mp3",
        PodcastDownloadNaming.SuggestedFileName(
            "CON",
            "https://cdn.example.test/audio.mp3",
            "audio/mpeg"));
    Equal(
        "_CON.notatki.mp3",
        PodcastDownloadNaming.SuggestedFileName(
            "CON.notatki",
            "https://cdn.example.test/audio.mp3",
            "audio/mpeg"));
    Equal(
        "Odcinek podcastu.mp3",
        PodcastDownloadNaming.SuggestedFileName(
            "\"'...",
            "https://cdn.example.test/audio.mp3",
            "audio/mpeg"));
    var longName = PodcastDownloadNaming.SuggestedFileName(
        new string('ą', 200),
        "https://cdn.example.test/audio.mp3",
        "audio/mpeg");
    True(longName.Length <= 144, "Przenośna nazwa odcinka przekracza bezpieczny limit.");
    True(longName.EndsWith(".mp3", StringComparison.Ordinal), "Skracanie usunęło rozszerzenie pliku.");
    Equal(
        ".ogg",
        PodcastDownloadNaming.ResolveExtension(
            "https://cdn.example.test/media/episode.OGG?token=abc",
            "audio/mpeg"));

    var directory = Path.Combine(Path.GetTempPath(), $"amc-podcast-name-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        File.WriteAllText(Path.Combine(directory, "Odcinek.mp3"), "pierwszy");
        Equal(
            Path.Combine(directory, "Odcinek (2).mp3"),
            PodcastDownloadNaming.UniquePath(directory, "Odcinek.mp3"));
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static void TestPodcastOpmlParsing()
{
    const string opml = """
        <?xml version="1.0" encoding="utf-8"?>
        <opml version="2.0"><body>
          <outline text="Folder">
            <outline text="Podcast A" xmlUrl="https://example.test/a.xml" htmlUrl="https://example.test/a" />
            <outline text="Duplikat" xmlUrl="https://example.test/a.xml" />
            <outline text="Niebezpieczny" xmlUrl="file:///c:/plik.xml" />
          </outline>
        </body></opml>
        """;
    var entries = PodcastOpmlParser.Parse(opml);
    Equal(1, entries.Count);
    Equal("Podcast A", entries[0].Title);
    Equal(new Uri("https://example.test/a.xml"), entries[0].FeedUri);
    Equal(new Uri("https://example.test/a"), entries[0].HomepageUri);
    Equal("Podcast A, example.test", entries[0].ToString());

    var rejectedDtd = false;
    try
    {
        PodcastOpmlParser.Parse("<!DOCTYPE opml [<!ENTITY xxe SYSTEM 'file:///c:/windows/win.ini'>]><opml><body><outline text='&xxe;' xmlUrl='https://example.test/a'/></body></opml>");
    }
    catch (XmlException)
    {
        rejectedDtd = true;
    }
    True(rejectedDtd, "Parser OPML musi odrzucać DTD i encje zewnętrzne.");

    var exported = Encoding.UTF8.GetString(PodcastOpmlWriter.Write(
    [
        new PodcastOpmlEntry("Życie & dźwięk", new Uri("https://example.test/b.xml"), null),
        entries[0],
        new PodcastOpmlEntry("Duplikat", new Uri("https://example.test/a.xml"), null)
    ]));
    True(exported.StartsWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>", StringComparison.Ordinal),
        "Eksport OPML powinien deklarować rzeczywiste kodowanie UTF-8.");
    var roundTrip = PodcastOpmlParser.Parse(exported);
    Equal(2, roundTrip.Count);
    Equal("Podcast A", roundTrip[0].Title);
    Equal("Życie & dźwięk", roundTrip[1].Title);
    Equal(new Uri("https://example.test/a"), roundTrip[0].HomepageUri);
}

static void TestPodcastLibraryUpdate()
{
    var settings = new PodcastSettings();
    var feedUri = new Uri("https://example.test/feed.xml");
    var first = new PodcastFeedDocument(
        "podcast-a",
        "Podcast z kanału",
        "Autor",
        "Opis",
        feedUri,
        new Uri("https://example.test/podcast"),
        [new PodcastFeedEpisode(
            "episode-1",
            "guid-1",
            "Starszy odcinek",
            "Autor",
            "Opis odcinka",
            new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero),
            TimeSpan.FromMinutes(20),
            new Uri("https://cdn.example.test/1.mp3"),
            null,
            "audio/mpeg",
            100)]);
    var initial = PodcastLibraryUpdater.Apply(settings, first, "Moja nazwa", DateTime.UtcNow);
    Equal(true, initial.AddedSubscription);
    Equal(true, settings.Episodes[0].IsNew);
    settings.Episodes[0].ResumePositionTicks = TimeSpan.FromMinutes(5).Ticks;
    settings.Episodes[0].IsPlayed = true;

    // The publisher now exposes only a new episode. The old one must remain
    // in AMC together with its listening state even though it vanished from
    // the current RSS window.
    var second = first with
    {
        Title = "Nowa nazwa z kanału",
        Episodes =
        [
            first.Episodes[0] with
            {
                Id = "episode-2",
                SourceIdentifier = "guid-2",
                Title = "Nowy odcinek",
                MediaUri = new Uri("https://cdn.example.test/2.mp3")
            }
        ]
    };
    var update = PodcastLibraryUpdater.Apply(settings, second, null, DateTime.UtcNow.AddMinutes(1));
    Equal(1, update.AddedEpisodes);
    Equal(1, update.RetainedEpisodesAbsentFromFeed);
    Equal(2, settings.Episodes.Count);
    Equal("Moja nazwa", settings.Subscriptions[0].Title);
    Equal(true, settings.Subscriptions[0].HasCustomTitle);
    Equal(TimeSpan.FromMinutes(5).Ticks, settings.Episodes.Single(item => item.Id == "episode-1").ResumePositionTicks);
    Equal(true, settings.Episodes.Single(item => item.Id == "episode-1").IsPlayed);
    Equal(null, settings.Episodes.Single(item => item.Id == "episode-1").DownloadPath);
    Equal(true, settings.Episodes.Single(item => item.Id == "episode-2").IsNew);

    // If an archived episode reappears, update its metadata in place without
    // duplicating it or losing locally remembered progress.
    var third = second with
    {
        Episodes =
        [
            first.Episodes[0] with { Title = "Zmieniony starszy odcinek" },
            second.Episodes[0]
        ]
    };
    var reappeared = PodcastLibraryUpdater.Apply(settings, third, null, DateTime.UtcNow.AddMinutes(2));
    Equal(0, reappeared.AddedEpisodes);
    Equal(0, reappeared.RetainedEpisodesAbsentFromFeed);
    Equal(2, settings.Episodes.Count);
    Equal("Zmieniony starszy odcinek", settings.Episodes.Single(item => item.Id == "episode-1").Title);
    Equal(TimeSpan.FromMinutes(5).Ticks, settings.Episodes.Single(item => item.Id == "episode-1").ResumePositionTicks);
    Equal(true, settings.Episodes.Single(item => item.Id == "episode-1").IsPlayed);
}

static void TestPodcastLegacyInboxMigration()
{
    var directory = Path.Combine(Path.GetTempPath(), $"amc-podcast-inbox-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        var store = new ConfigurationStore(
            Path.Combine(directory, "state.json"),
            Path.Combine(directory, "library.db"));
        var state = ConfigurationStore.CreateDefaultState();
        state.SchemaVersion = 42;
        state.Podcasts.Subscriptions =
        [
            new PodcastSubscriptionSettings { Id = "legacy", Title = "Starszy import", FeedUrl = "https://example.test/legacy.xml" },
            new PodcastSubscriptionSettings { Id = "started", Title = "Rozpoczęty", FeedUrl = "https://example.test/started.xml" },
            new PodcastSubscriptionSettings { Id = "has-new", Title = "Ma nowy", FeedUrl = "https://example.test/has-new.xml" },
            new PodcastSubscriptionSettings { Id = "removed", Title = "Usunięty", FeedUrl = "https://example.test/removed.xml", IsInLibrary = false }
        ];
        state.Podcasts.Episodes =
        [
            new PodcastEpisodeSettings { Id = "legacy-old", SubscriptionId = "legacy", Title = "Starszy", MediaUrl = "https://cdn.example.test/legacy-old.mp3", PublishedUtcTicks = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc).Ticks, IsNew = false },
            new PodcastEpisodeSettings { Id = "legacy-latest", SubscriptionId = "legacy", Title = "Najnowszy", MediaUrl = "https://cdn.example.test/legacy-latest.mp3", PublishedUtcTicks = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc).Ticks, IsNew = false },
            new PodcastEpisodeSettings { Id = "started-latest", SubscriptionId = "started", Title = "Już rozpoczęty", MediaUrl = "https://cdn.example.test/started.mp3", PublishedUtcTicks = new DateTime(2026, 9, 2, 0, 0, 0, DateTimeKind.Utc).Ticks, ResumePositionTicks = TimeSpan.FromMinutes(2).Ticks, IsNew = false },
            new PodcastEpisodeSettings { Id = "existing-new", SubscriptionId = "has-new", Title = "Już nowy", MediaUrl = "https://cdn.example.test/existing-new.mp3", PublishedUtcTicks = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc).Ticks, IsNew = true },
            new PodcastEpisodeSettings { Id = "has-new-latest", SubscriptionId = "has-new", Title = "Nowszy, ale archiwalny", MediaUrl = "https://cdn.example.test/has-new-latest.mp3", PublishedUtcTicks = new DateTime(2026, 9, 2, 0, 0, 0, DateTimeKind.Utc).Ticks, IsNew = false },
            new PodcastEpisodeSettings { Id = "removed-latest", SubscriptionId = "removed", Title = "Poza Biblioteką", MediaUrl = "https://cdn.example.test/removed.mp3", PublishedUtcTicks = new DateTime(2026, 9, 2, 0, 0, 0, DateTimeKind.Utc).Ticks, IsNew = false }
        ];
        store.Save(state);

        var migrated = store.LoadOrCreate();
        Equal(ConfigurationStore.CurrentSchemaVersion, migrated.SchemaVersion);
        Equal(false, migrated.Podcasts.Episodes.Single(item => item.Id == "legacy-old").IsNew);
        Equal(true, migrated.Podcasts.Episodes.Single(item => item.Id == "legacy-latest").IsNew);
        Equal(false, migrated.Podcasts.Episodes.Single(item => item.Id == "started-latest").IsNew);
        Equal(true, migrated.Podcasts.Episodes.Single(item => item.Id == "existing-new").IsNew);
        Equal(false, migrated.Podcasts.Episodes.Single(item => item.Id == "has-new-latest").IsNew);
        Equal(false, migrated.Podcasts.Episodes.Single(item => item.Id == "removed-latest").IsNew);

        store.Save(migrated);
        var loadedAgain = store.LoadOrCreate();
        Equal(2, loadedAgain.Podcasts.Episodes.Count(item => item.IsNew));
    }
    finally
    {
        Directory.Delete(directory, true);
    }
}

static void TestPodcastEpisodeProgress()
{
    var episode = new PodcastEpisodeSettings { IsNew = true };
    Equal(PodcastEpisodeListeningState.New, PodcastEpisodeProgress.GetState(episode));
    Equal(false, PodcastEpisodeProgress.UpdateFromPosition(episode, TimeSpan.FromSeconds(59)));
    Equal(true, episode.IsNew);
    Equal(true, PodcastEpisodeProgress.UpdateFromPosition(episode, TimeSpan.FromMinutes(1)));
    Equal(false, episode.IsNew);
    Equal(true, episode.IsStarted);
    Equal(PodcastEpisodeListeningState.InProgress, PodcastEpisodeProgress.GetState(episode));
    Equal("w trakcie", PodcastEpisodeProgress.GetLabel(episode));
    PodcastEpisodeProgress.MarkPlayed(episode);
    Equal(PodcastEpisodeListeningState.Played, PodcastEpisodeProgress.GetState(episode));
    Equal("odtworzony", PodcastEpisodeProgress.GetLabel(episode));

    var archived = new PodcastEpisodeSettings { IsNew = false };
    Equal(PodcastEpisodeListeningState.Unplayed, PodcastEpisodeProgress.GetState(archived));
    Equal("nieodtworzony", PodcastEpisodeProgress.GetLabel(archived));
}

static void TestPodcastPlaybackSettings()
{
    var global = new PlaybackAudioSettings
    {
        LoudnessNormalizationEnabled = false,
        SmoothTrackTransitionsEnabled = false,
        InterTrackSilenceMilliseconds = 500
    };
    var podcast = new PodcastSubscriptionSettings
    {
        ResumePositionMode = ResumePositionMode.StartFromBeginning,
        PlaybackRateOverride = 1.25d,
        LoudnessNormalizationOverride = true,
        InterTrackSilenceMillisecondsOverride = 2000,
        DownloadsFolder = @"D:\Podcasty\Audycja"
    };
    var episode = new PodcastEpisodeSettings
    {
        ResumePositionMode = ResumePositionMode.Inherit,
        SmoothTrackTransitionsOverride = true,
        InterTrackSilenceMillisecondsOverride = 0
    };

    Equal(false, PodcastPlaybackSettingsResolver.ShouldRememberPosition(episode, podcast));
    Equal(1.25d, PodcastPlaybackSettingsResolver.PlaybackRateOverride(episode, podcast));
    var audio = PodcastPlaybackSettingsResolver.ResolveAudio(global, episode, podcast);
    Equal(true, audio.LoudnessNormalizationEnabled);
    Equal(true, audio.SmoothTrackTransitionsEnabled);
    Equal(0, audio.InterTrackSilenceMilliseconds);
    Equal(
        @"D:\Podcasty\Audycja",
        PodcastPlaybackSettingsResolver.ConfiguredDownloadFolder(@"D:\Podcasty", podcast));
    podcast.DownloadsFolder = null;
    Equal(
        @"D:\Podcasty",
        PodcastPlaybackSettingsResolver.ConfiguredDownloadFolder(@"D:\Podcasty", podcast));
}

static void TestPodcastMembershipToggle()
{
    PodcastSubscriptionSettings[] subscriptions =
    [
        new() { IsInLibrary = true, IsFavorite = false },
        new() { IsInLibrary = true, IsFavorite = false }
    ];
    Equal(true, PodcastMembershipToggle.Apply(subscriptions, PodcastMembershipCollection.Favorites));
    True(subscriptions.All(subscription => subscription.IsFavorite && subscription.IsInLibrary),
        "Dodanie do ulubionych musi zachować podcast w Bibliotece.");
    Equal(false, PodcastMembershipToggle.Apply(subscriptions, PodcastMembershipCollection.Favorites));
    True(subscriptions.All(subscription => !subscription.IsFavorite && subscription.IsInLibrary),
        "Drugie użycie ulubionych musi usunąć wyłącznie stan ulubionego.");
    Equal(false, PodcastMembershipToggle.Apply(subscriptions, PodcastMembershipCollection.Library));
    True(subscriptions.All(subscription => !subscription.IsInLibrary && !subscription.IsFavorite),
        "Drugie użycie Biblioteki musi usunąć zapisany podcast i stan ulubionego.");
    Equal(true, PodcastMembershipToggle.Apply(subscriptions, PodcastMembershipCollection.Library));
    True(subscriptions.All(subscription => subscription.IsInLibrary),
        "Kolejne użycie Biblioteki musi przywrócić podcast.");
}

static void TestAudioClipSelection()
{
    var selection = new AudioClipSelection();
    selection.SetStart("item-1", @"D:\Audio\plik.mp3", TimeSpan.FromSeconds(20), TimeSpan.FromMinutes(3));
    Equal(TimeSpan.FromSeconds(20), selection.Start);
    Equal(false, selection.IsComplete);
    Equal(false, selection.TrySetEnd(
        "item-1",
        @"D:\Audio\plik.mp3",
        TimeSpan.FromSeconds(10),
        TimeSpan.FromMinutes(3)));
    Equal(true, selection.TrySetEnd(
        "item-1",
        @"D:\Audio\plik.mp3",
        TimeSpan.FromSeconds(45),
        TimeSpan.FromMinutes(3)));
    Equal(true, selection.IsComplete);
    Equal(TimeSpan.FromSeconds(20), selection.FindRelativeBoundary(TimeSpan.FromSeconds(30), -1));
    Equal(TimeSpan.FromSeconds(45), selection.FindRelativeBoundary(TimeSpan.FromSeconds(30), 1));
    Equal(TimeSpan.FromSeconds(20), selection.FindRelativeBoundary(TimeSpan.FromSeconds(45), -1));
    Equal(TimeSpan.FromSeconds(45), selection.FindRelativeBoundary(TimeSpan.FromSeconds(20), 1));
    Equal(null, selection.FindRelativeBoundary(TimeSpan.FromSeconds(20), -1));
    Equal(null, selection.FindRelativeBoundary(TimeSpan.FromSeconds(45), 1));

    selection.SetStart("item-1", @"D:\Audio\plik.mp3", TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(3));
    Equal(null, selection.End);
    selection.SetStart("item-2", @"D:\Audio\inny.mp3", TimeSpan.FromSeconds(-2), TimeSpan.FromMinutes(1));
    Equal(TimeSpan.Zero, selection.Start);
    Equal("item-2", selection.ItemId);
    selection.Clear();
    Equal(null, selection.ItemId);
    Equal(false, selection.IsComplete);
}

static void TestPlaybackAudioSettingsPersistence()
{
    var directory = Path.Combine(Path.GetTempPath(), $"amc-audio-settings-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        var store = new ConfigurationStore(
            Path.Combine(directory, "state.json"),
            Path.Combine(directory, "library.db"));
        var state = ConfigurationStore.CreateDefaultState();
        Equal(false, state.Settings.Audio.LoudnessNormalizationEnabled);
        Equal(false, state.Settings.Audio.SmoothTrackTransitionsEnabled);
        Equal(0, state.Settings.Audio.InterTrackSilenceMilliseconds);

        state.Settings.Audio.LoudnessNormalizationEnabled = true;
        state.Settings.Audio.SmoothTrackTransitionsEnabled = true;
        state.Settings.Audio.InterTrackSilenceMilliseconds = 2000;
        state.Settings.Audio.AllSessionsMuted = true;
        state.Settings.Audio.SessionMutedById["radio"] = true;
        state.Settings.Audio.OutputDeviceIdsBySession["radio"] = "test-device-id";
        state.LocalMedia.Items.Add(new LocalMediaItemSettings
        {
            Id = "audio-override-item",
            Title = "Utwór z własnymi opcjami",
            Path = Path.Combine(directory, "Album", "utwor.mp3"),
            LoudnessNormalizationOverride = false,
            SmoothTrackTransitionsOverride = true,
            InterTrackSilenceMillisecondsOverride = 500
        });
        state.LocalMedia.FolderPlaybackOptions.Add(new LocalFolderPlaybackSettings
        {
            Path = Path.Combine(directory, "Album"),
            LoudnessNormalizationOverride = true,
            SmoothTrackTransitionsOverride = false,
            InterTrackSilenceMillisecondsOverride = 3000
        });
        store.Save(state);

        var loaded = store.LoadOrCreate();
        Equal(true, loaded.Settings.Audio.LoudnessNormalizationEnabled);
        Equal(true, loaded.Settings.Audio.SmoothTrackTransitionsEnabled);
        Equal(2000, loaded.Settings.Audio.InterTrackSilenceMilliseconds);
        Equal(true, loaded.Settings.Audio.AllSessionsMuted);
        Equal(true, loaded.Settings.Audio.SessionMutedById["RADIO"]);
        Equal("test-device-id", loaded.Settings.Audio.OutputDeviceIdsBySession["RADIO"]);
        var loadedItem = loaded.LocalMedia.Items.Single(item => item.Id == "audio-override-item");
        Equal(false, loadedItem.LoudnessNormalizationOverride);
        Equal(true, loadedItem.SmoothTrackTransitionsOverride);
        Equal(500, loadedItem.InterTrackSilenceMillisecondsOverride);
        var loadedFolder = loaded.LocalMedia.FolderPlaybackOptions.Single();
        Equal(true, loadedFolder.LoudnessNormalizationOverride);
        Equal(false, loadedFolder.SmoothTrackTransitionsOverride);
        Equal(3000, loadedFolder.InterTrackSilenceMillisecondsOverride);
        True(
            PlaybackAudioSettingsRules.SupportedInterTrackSilenceMilliseconds
                .SequenceEqual([0, 500, 1000, 2000, 3000, 5000]),
            "Lista obsługiwanych czasów ciszy jest nieprawidłowa.");
        Equal("bez dodatkowej ciszy", PlaybackAudioSettingsRules.GetInterTrackSilenceLabel(0));
        Equal("pół sekundy", PlaybackAudioSettingsRules.GetInterTrackSilenceLabel(500));
        Equal("2 sekundy", PlaybackAudioSettingsRules.GetInterTrackSilenceLabel(2000));
    }
    finally
    {
        Directory.Delete(directory, true);
    }
}

static void TestPlaybackVolumeMemory()
{
    var settings = new PlaybackVolumeMemorySettings();
    Equal(null, PlaybackVolumeMemory.Find(settings, "radio", "stacja-1", null));

    PlaybackVolumeMemory.Remember(settings, "radio", "stacja-1", null, 18);
    PlaybackVolumeMemory.Remember(settings, "radio", "stacja-1", "wyjscie-a", 47);
    PlaybackVolumeMemory.Remember(settings, "podcasts", "podcast-1", "wyjscie-a", 130);
    Equal(18, PlaybackVolumeMemory.Find(settings, "RADIO", "stacja-1", null));
    Equal(47, PlaybackVolumeMemory.Find(settings, "radio", "stacja-1", "WYJSCIE-A"));
    Equal(null, PlaybackVolumeMemory.Find(settings, "radio", "stacja-1", "wyjscie-b"));
    Equal(100, PlaybackVolumeMemory.Find(settings, "podcasts", "podcast-1", "wyjscie-a"));

    PlaybackVolumeMemory.Remember(settings, "radio", "stacja-1", "wyjscie-a", 39);
    PlaybackVolumeMemory.Normalize(settings);
    Equal(3, settings.Entries.Count);
    Equal(39, PlaybackVolumeMemory.Find(settings, "radio", "stacja-1", "wyjscie-a"));

    var directory = Path.Combine(Path.GetTempPath(), $"amc-volume-memory-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        var store = new ConfigurationStore(
            Path.Combine(directory, "state.json"),
            Path.Combine(directory, "library.db"));
        var state = ConfigurationStore.CreateDefaultState();
        state.PlaybackVolumes = settings;
        store.Save(state);
        var loaded = store.LoadOrCreate();
        Equal(ConfigurationStore.CurrentSchemaVersion, loaded.SchemaVersion);
        Equal(18, PlaybackVolumeMemory.Find(loaded.PlaybackVolumes, "radio", "stacja-1", null));
        Equal(39, PlaybackVolumeMemory.Find(loaded.PlaybackVolumes, "radio", "stacja-1", "wyjscie-a"));
    }
    finally
    {
        Directory.Delete(directory, true);
    }
}

static void TestSessionMutePersistence()
{
    var settings = new AppSettings();
    var manager = new SessionManager(settings);
    var output = new FakeMediaOutput();
    var (local, _) = manager.AddOrUpdateTransientSession(
        "local",
        "Pliki lokalne",
        [new MediaItem { Id = "mute-item", Title = "Test wyciszenia" }],
        output,
        1);
    manager.SelectSession("local");

    Equal(true, manager.ToggleCurrentSessionMute());
    Equal(true, manager.ToggleAllSessionsMute());
    Equal(true, settings.Audio.SessionMutedById["LOCAL"]);
    Equal(true, settings.Audio.AllSessionsMuted);

    var restoredManager = new SessionManager(settings);
    var (restoredLocal, _) = restoredManager.AddOrUpdateTransientSession(
        "local",
        "Pliki lokalne",
        [new MediaItem { Id = "mute-item", Title = "Test wyciszenia" }],
        new FakeMediaOutput(),
        1);
    Equal(true, restoredManager.AllSessionsMuted);
    Equal(true, restoredLocal.IsGloballyMuted);
    Equal(true, restoredLocal.IsSessionMuted);

    restoredManager.SelectSession("local");
    Equal(false, restoredManager.ToggleAllSessionsMute());
    Equal(false, restoredLocal.IsGloballyMuted);
    Equal(true, restoredLocal.IsSessionMuted);
    Equal(false, restoredManager.ToggleCurrentSessionMute());
    Equal(false, settings.Audio.SessionMutedById.ContainsKey("local"));
}

static void TestPlaybackAudioProcessingCapabilities()
{
    var plainSession = new DemoMediaSession(
        "plain-output",
        "Zewnętrzne sterowanie",
        [new MediaItem { Id = "plain", Title = "Element" }],
        new FakeMediaOutput());
    Equal(PlaybackAudioProcessingCapabilities.None, plainSession.AudioProcessingCapabilities);

    var output = new FakeAudioProcessingMediaOutput();
    var processingSession = new DemoMediaSession(
        "processing-output",
        "Odtwarzanie przez AMC",
        [new MediaItem { Id = "processed", Title = "Element" }],
        output);
    Equal(PlaybackAudioProcessingCapabilities.All, processingSession.AudioProcessingCapabilities);
    var settings = new PlaybackAudioSettings
    {
        LoudnessNormalizationEnabled = true,
        SmoothTrackTransitionsEnabled = true,
        InterTrackSilenceMilliseconds = 2000
    };
    processingSession.ConfigureAudioProcessing(settings);
    Equal(settings, output.LastAudioProcessingSettings);
}

static void TestLocalPlaybackAudioSettingsInheritance()
{
    var root = Path.Combine(Path.GetTempPath(), $"amc-audio-inheritance-{Guid.NewGuid():N}");
    var child = Path.Combine(root, "Podcasty");
    var itemPath = Path.Combine(child, "odcinek.mp3");
    var global = new PlaybackAudioSettings
    {
        LoudnessNormalizationEnabled = false,
        SmoothTrackTransitionsEnabled = false,
        InterTrackSilenceMilliseconds = 0
    };
    LocalFolderPlaybackSettings[] folders =
    [
        new()
        {
            Path = root,
            LoudnessNormalizationOverride = true,
            InterTrackSilenceMillisecondsOverride = 2000
        },
        new()
        {
            Path = child,
            SmoothTrackTransitionsOverride = true
        }
    ];
    var item = new LocalMediaItemSettings
    {
        Path = itemPath,
        LoudnessNormalizationOverride = false,
        InterTrackSilenceMillisecondsOverride = 500
    };

    var effectiveResolution = LocalPlaybackAudioSettingsResolver.ResolveWithSources(
        global,
        itemPath,
        item,
        folders);
    var effective = effectiveResolution.Settings;
    Equal(false, effective.LoudnessNormalizationEnabled);
    Equal(true, effective.SmoothTrackTransitionsEnabled);
    Equal(500, effective.InterTrackSilenceMilliseconds);
    Equal(LocalPlaybackAudioSettingSource.Item, effectiveResolution.LoudnessNormalizationSource);
    Equal(LocalPlaybackAudioSettingSource.Folder, effectiveResolution.SmoothTrackTransitionsSource);
    Equal(LocalPlaybackAudioSettingSource.Item, effectiveResolution.InterTrackSilenceSource);
    Equal(
        "normalizacja wyłączona, ustawienie pliku; przejścia włączone, ustawienie folderu; "
        + "cisza pół sekundy, ustawienie pliku",
        LocalPlaybackAudioSettingsPresentation.FormatEffective(effectiveResolution));

    var inheritedResolution = LocalPlaybackAudioSettingsResolver.ResolveWithSources(
        global,
        itemPath,
        itemSettings: null,
        folders);
    var inherited = inheritedResolution.Settings;
    Equal(true, inherited.LoudnessNormalizationEnabled);
    Equal(true, inherited.SmoothTrackTransitionsEnabled);
    Equal(2000, inherited.InterTrackSilenceMilliseconds);
    Equal(LocalPlaybackAudioSettingSource.Folder, inheritedResolution.LoudnessNormalizationSource);
    Equal(LocalPlaybackAudioSettingSource.Folder, inheritedResolution.SmoothTrackTransitionsSource);
    Equal(LocalPlaybackAudioSettingSource.Folder, inheritedResolution.InterTrackSilenceSource);

    var globalResolution = LocalPlaybackAudioSettingsResolver.ResolveWithSources(
        global,
        itemPath,
        itemSettings: null,
        folderSettings: []);
    Equal(LocalPlaybackAudioSettingSource.Global, globalResolution.LoudnessNormalizationSource);
    Equal(LocalPlaybackAudioSettingSource.Global, globalResolution.SmoothTrackTransitionsSource);
    Equal(LocalPlaybackAudioSettingSource.Global, globalResolution.InterTrackSilenceSource);
}

var failures = new List<string>();
foreach (var (name, test) in tests)
{
    try
    {
        test();
        Console.WriteLine($"OK: {name}");
    }
    catch (Exception exception)
    {
        failures.Add($"BŁĄD: {name}: {exception.Message}");
    }
}

var externalPodcastStatePath = Environment.GetEnvironmentVariable("AMC_PODCAST_MIGRATION_STATE");
if (!string.IsNullOrWhiteSpace(externalPodcastStatePath))
{
    try
    {
        TestPodcastMigrationOnCopy(externalPodcastStatePath);
        Console.WriteLine("OK: Migracja kopii rzeczywistej Biblioteki Podcastów");
    }
    catch (Exception exception)
    {
        failures.Add($"BŁĄD: Migracja kopii rzeczywistej Biblioteki Podcastów: {exception.Message}");
    }
}

foreach (var failure in failures) Console.Error.WriteLine(failure);
return failures.Count == 0 ? 0 : 1;

static void TestPodcastMigrationOnCopy(string sourceStatePath)
{
    if (!File.Exists(sourceStatePath))
        throw new FileNotFoundException("Nie znaleziono wskazanego stanu do diagnostyki.", sourceStatePath);

    var directory = Path.Combine(Path.GetTempPath(), $"amc-real-podcast-migration-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        var statePath = Path.Combine(directory, "state.json");
        var libraryPath = Path.Combine(directory, "library.db");
        var podcastsPath = Path.Combine(directory, "podcasts.db");
        File.Copy(sourceStatePath, statePath);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var state = new ConfigurationStore(statePath, libraryPath, podcastsPath).LoadOrCreate();
        stopwatch.Stop();
        var subscriptionCount = state.Podcasts.Subscriptions.Count;
        var episodeCount = state.Podcasts.Episodes.Count;
        True(subscriptionCount > 0, "Kopia rzeczywistego stanu nie zawiera subskrypcji Podcastów.");
        True(episodeCount > 0, "Kopia rzeczywistego stanu nie zawiera odcinków Podcastów.");
        True(File.Exists(podcastsPath), "Migracja kopii nie utworzyła podcasts.db.");
        var compactJson = JsonNode.Parse(File.ReadAllText(statePath))?.AsObject()
            ?? throw new InvalidDataException("Nie można odczytać kompaktowego state.json.");
        Equal(0, compactJson["podcasts"]?["episodes"]?.AsArray().Count ?? -1);

        var reload = new ConfigurationStore(statePath, libraryPath, podcastsPath).LoadOrCreate();
        Equal(subscriptionCount, reload.Podcasts.Subscriptions.Count);
        Equal(episodeCount, reload.Podcasts.Episodes.Count);
        Console.WriteLine(
            $"DIAGNOSTYKA: {subscriptionCount} podcastów, {episodeCount} odcinków, migracja {stopwatch.Elapsed.TotalSeconds:0.00} s.");
    }
    finally
    {
        Directory.Delete(directory, true);
    }
}

static void TestKeyChords()
{
    Equal("Ctrl+Shift+F", KeyChord.Parse("shift+ctrl+f").Canonical);
    Equal("Ctrl+Alt+Space", KeyChord.Parse("Control+Alt+Spacja").Canonical);
    Equal("Ctrl+Alt+Windows+Enter", KeyChord.Parse("Alt+Win+Control+Enter").Canonical);
    Equal("Ctrl+NumpadEnter", KeyChord.Parse("Control+Enter numeryczny").Canonical);
    Equal("Ctrl+NumpadEnter", KeyChord.Parse("Ctrl+Numpad Enter").Canonical);
    Equal("NumpadAdd", KeyChord.Parse("plus numeryczny").Canonical);
    Equal("NumpadSubtract", KeyChord.Parse("minus numeryczny").Canonical);
    Equal("NumpadDecimal", KeyChord.Parse("kropka numeryczna").Canonical);
    Equal("Numpad7", KeyChord.Parse("7 numeryczny").Canonical);
    Equal("NumpadInsert", KeyChord.Parse("Insert numeryczny").Canonical);
    Equal("NumpadNumLock", KeyChord.Parse("Num Lock").Canonical);
    Equal("Windows+NumpadDivide", KeyChord.Parse("Win+dzielenie numeryczne").Canonical);
    Equal("Ctrl+Alt+Windows+F12", KeyChord.Parse("Windows+Alt+Control+F12").Canonical);
    Equal("Ctrl+Alt+Windows+F12", KeyChord.Parse("CTRL-Alt-Win-F12").Canonical);
    Equal("PageDown", KeyChord.Parse("PgDn").Canonical);
}

static void TestDefaultProfile()
{
    var settings = new AppSettings();
    Equal("Ctrl+Alt+Windows+F12", settings.PrefixChord);
    Equal(true, settings.Messages.Enabled);
    Equal(false, settings.Messages.DetailedHints);
    Equal(true, settings.Messages.SeekMessages);
    Equal(true, settings.Messages.ArrowSeekMessages);
    Equal(true, settings.Messages.PercentageSeekMessages);
    Equal(true, settings.Messages.BookmarkNavigationMessages);
    Equal(true, settings.Messages.VolumeMessages);
    Equal(true, settings.Messages.PlaybackMessages);
    Equal(PercentageSeekAnnouncementMode.Percent, settings.Messages.PercentageSeekAnnouncement);
    Equal(StartupTarget.MediaList, settings.StartupTarget);
    Equal(true, settings.PausePlaybackWhenLeavingPlayer);
    Equal(false, settings.OpenPlayerWhenActivatingPreset);
    Equal(true, settings.RememberLocalPlaybackPositions);

    var profile = KeyboardProfile.CreateDefault();
    Equal(CommandIds.SessionSlot(1), profile.Resolve(KeyChord.Parse("1")));
    True(profile.Resolve(KeyChord.Parse("Ctrl+1")) is null, "Po prefiksie cyfra nie powinna wymagać Control.");
    Equal(CommandIds.ViewFavorites, profile.Resolve(KeyChord.Parse("U")));
    Equal(CommandIds.ToggleFavorite, profile.Resolve(KeyChord.Parse("Shift+U")));
    Equal(CommandIds.ViewAlbums, profile.Resolve(KeyChord.Parse("A")));
    Equal(CommandIds.ViewBookmarks, profile.Resolve(KeyChord.Parse("B")));
    Equal(CommandIds.AddBookmark, profile.Resolve(KeyChord.Parse("Shift+B")));
    True(profile.Resolve(KeyChord.Parse("Shift+A")) is null, "Shift+A pozostaje nieprzypisane.");
    Equal(CommandIds.ToggleLoudnessNormalization, profile.Resolve(KeyChord.Parse("Shift+N")));
    Equal(CommandIds.ToggleSmoothTrackTransitions, profile.Resolve(KeyChord.Parse("T")));
    Equal(CommandIds.CycleInterTrackSilence, profile.Resolve(KeyChord.Parse("C")));
    Equal(CommandIds.FilterCurrent, profile.Resolve(KeyChord.Parse("K")));
    Equal(CommandIds.CommandPalette, profile.Resolve(KeyChord.Parse("Shift+K")));
    Equal(CommandIds.SearchCurrent, profile.Resolve(KeyChord.Parse("F")));
    Equal(CommandIds.SearchAll, profile.Resolve(KeyChord.Parse("Shift+F")));
    Equal(CommandIds.DownloadInService, profile.Resolve(KeyChord.Parse("D")));
    Equal(CommandIds.DownloadToDisk, profile.Resolve(KeyChord.Parse("Shift+D")));
    True(profile.Resolve(KeyChord.Parse("I")) is null, "I nie powinno mieć polecenia informacyjnego po prefiksie.");
    True(profile.Resolve(KeyChord.Parse("Shift+I")) is null, "Shift+I nie powinno mieć polecenia informacyjnego po prefiksie.");
    Equal(CommandIds.TimeElapsed, profile.Resolve(KeyChord.Parse("Ctrl+E")));
    Equal(CommandIds.TimeRemaining, profile.Resolve(KeyChord.Parse("Ctrl+R")));
    Equal(CommandIds.TimeTotal, profile.Resolve(KeyChord.Parse("Ctrl+T")));
}

static void TestBuiltInProfileRefresh()
{
    var directory = Path.Combine(Path.GetTempPath(), $"amc-profile-tests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        var statePath = Path.Combine(directory, "state.json");
        var store = new ConfigurationStore(statePath);
        var state = ConfigurationStore.CreateDefaultState();
        var oldBuiltIn = state.KeyboardProfiles.Single(profile => profile.Id == "default");
        oldBuiltIn.Bindings.Clear();
        oldBuiltIn.Bindings[KeyChord.Parse("Ctrl+1").Canonical] = CommandIds.SessionSlot(1);

        var custom = oldBuiltIn.CreateEditableCopy("Własny stary profil");
        custom.Bindings[KeyChord.Parse("Ctrl+Enter").Canonical] = "transport.playSelected";
        custom.Bindings[KeyChord.Parse("I").Canonical] = "view.itemInformation";
        custom.Bindings[KeyChord.Parse("Shift+I").Canonical] = "information.playbackStatus";
        state.KeyboardProfiles.Add(custom);
        state.Settings.ActiveKeyboardProfileId = custom.Id;
        store.Save(state);

        var loaded = store.LoadOrCreate();
        var refreshedBuiltIn = loaded.KeyboardProfiles.Single(profile => profile.Id == "default");
        var retainedCustom = loaded.KeyboardProfiles.Single(profile => profile.Id == custom.Id);
        Equal(CommandIds.SessionSlot(1), refreshedBuiltIn.Resolve(KeyChord.Parse("1")));
        Equal(CommandIds.ToggleLoudnessNormalization, refreshedBuiltIn.Resolve(KeyChord.Parse("Shift+N")));
        Equal(CommandIds.ToggleSmoothTrackTransitions, refreshedBuiltIn.Resolve(KeyChord.Parse("T")));
        Equal(CommandIds.CycleInterTrackSilence, refreshedBuiltIn.Resolve(KeyChord.Parse("C")));
        True(refreshedBuiltIn.Resolve(KeyChord.Parse("Ctrl+1")) is null, "Profil wbudowany powinien otrzymać nową mapę.");
        Equal(CommandIds.SessionSlot(1), retainedCustom.Resolve(KeyChord.Parse("Ctrl+1")));
        Equal(CommandIds.ActivateSelected, retainedCustom.Resolve(KeyChord.Parse("Ctrl+Enter")));
        True(retainedCustom.Resolve(KeyChord.Parse("I")) is null, "Usunięte polecenie informacji nie może pozostać w profilu.");
        True(retainedCustom.Resolve(KeyChord.Parse("Shift+I")) is null, "Usunięty odczyt stanu nie może pozostać w profilu.");
        Equal(custom.Id, loaded.Settings.ActiveKeyboardProfileId);
    }
    finally
    {
        Directory.Delete(directory, true);
    }
}

static void TestCommandCatalog()
{
    Equal("Odtwórz lub wstrzymaj", CommandCatalog.GetDisplayName(CommandIds.ActivateSelected));
    Equal("Dodaj lub usuń z ulubionych", CommandCatalog.GetDisplayName(CommandIds.ToggleFavorite));
    Equal("Dodaj lub usuń z kolejki", CommandCatalog.GetDisplayName(CommandIds.AddQueue));
    Equal("Ustawienia: szablony komunikatów", CommandCatalog.GetDisplayName(CommandIds.SettingsMessageTemplates));
    Equal("Ustawienia: komunikat po skoku cyfrą", CommandCatalog.GetDisplayName(CommandIds.SettingsPercentageSeekAnnouncement));
    Equal("Przełącz automatyczne komunikaty odtwarzacza", CommandCatalog.GetDisplayName(CommandIds.SettingsToggleSeekMessages));
    Equal(
        "Włącz lub wyłącz oznajmianie rozpoznanych utworów",
        CommandCatalog.GetDisplayName(CommandIds.ToggleRadioRecognitionAnnouncements));
    Equal("Otwórz lokalne pliki multimedialne", CommandCatalog.GetDisplayName(CommandIds.OpenLocalFiles));
    Equal("Otwórz folder z plikami multimedialnymi", CommandCatalog.GetDisplayName(CommandIds.OpenLocalFolder));
    Equal("Importuj stacje radiowe z playlisty", CommandCatalog.GetDisplayName(CommandIds.ImportRadioPlaylist));
    Equal("Pokaż nowe odcinki podcastów", CommandCatalog.GetDisplayName(CommandIds.ViewPodcastInbox));
    Equal("Pokaż rozpoczęte odcinki podcastów", CommandCatalog.GetDisplayName(CommandIds.ViewPodcastInProgress));
    Equal("Pokaż aktualnie nagrywane stacje", CommandCatalog.GetDisplayName(CommandIds.ViewActiveRadioRecordings));
    Equal("Rozpocznij nową część ręcznego nagrania radia", CommandCatalog.GetDisplayName(CommandIds.SplitRadioRecording));
    Equal("Zatrzymaj wszystkie trwające nagrania", CommandCatalog.GetDisplayName(CommandIds.StopAllRadioRecordings));
    Equal("Pokaż presety aktywnej sesji", CommandCatalog.GetDisplayName(CommandIds.ViewRadioPresets));
    Equal("Utwórz preset lub przypisz skrót aktywnej sesji", CommandCatalog.GetDisplayName(CommandIds.AssignRadioPreset));
    Equal("Biblioteka lokalna: pokaż foldery", CommandCatalog.GetDisplayName(CommandIds.ViewFolders));
    Equal("Biblioteka lokalna: pokaż wszystkie pliki", CommandCatalog.GetDisplayName(CommandIds.ViewAllLocalFiles));
    Equal("Biblioteka lokalna: pokaż kolejność własną", CommandCatalog.GetDisplayName(CommandIds.ViewCustomLocalOrder));
    Equal("Odśwież foldery Biblioteki", CommandCatalog.GetDisplayName(CommandIds.RefreshLocalLibrary));
    Equal("Foldery Biblioteki", CommandCatalog.GetDisplayName(CommandIds.ManageLocalSources));
    Equal("Otwórz element w WiiM", CommandCatalog.GetDisplayName(CommandIds.OpenOnWiiM));
    Equal("Dodaj strumień sieciowy WiiM", CommandCatalog.GetDisplayName(CommandIds.AddWiiMNetworkStream));
    Equal("Importuj strumienie WiiM z playlisty", CommandCatalog.GetDisplayName(CommandIds.ImportWiiMNetworkStreams));
    Equal("Eksportuj strumienie do WiiM Home", CommandCatalog.GetDisplayName(CommandIds.ExportWiiMNetworkStreams));
    Equal("Poprzedni strumień lub zajęty preset urządzenia WiiM", CommandCatalog.GetDisplayName(CommandIds.PreviousWiiMDevicePreset));
    Equal("Następny strumień lub zajęty preset urządzenia WiiM", CommandCatalog.GetDisplayName(CommandIds.NextWiiMDevicePreset));
    Equal("Zmień nazwę w Bibliotece", CommandCatalog.GetDisplayName(CommandIds.RenameLibraryItem));
    Equal("Zmień nazwę pliku na dysku", CommandCatalog.GetDisplayName(CommandIds.RenameLocalFile));
    Equal("Przenieś wyżej na bieżącej liście", CommandCatalog.GetDisplayName(CommandIds.MoveLocalLibraryItemUp));
    Equal("Przenieś niżej na bieżącej liście", CommandCatalog.GetDisplayName(CommandIds.MoveLocalLibraryItemDown));
    Equal("Ustawienia: kolejność sesji i skrótów Ctrl+1–9", CommandCatalog.GetDisplayName(CommandIds.SettingsSessionOrder));
    Equal("Ustawienia: wstrzymuj po wyjściu z odtwarzacza", CommandCatalog.GetDisplayName(CommandIds.SettingsPausePlaybackWhenLeavingPlayer));
    Equal("Ustawienia: fokus podąża za odtwarzaniem", CommandCatalog.GetDisplayName(CommandIds.SettingsFollowPlaybackOnPlayerExit));
    Equal("Ustawienia: otwieraj odtwarzacz po uruchomieniu presetu", CommandCatalog.GetDisplayName(CommandIds.SettingsOpenPlayerWhenActivatingPreset));
    Equal("Ustawienia: pamiętaj pozycję odtwarzania lokalnych plików", CommandCatalog.GetDisplayName(CommandIds.SettingsRememberLocalPlaybackPositions));
    Equal("Skocz do czasu", CommandCatalog.GetDisplayName(CommandIds.SeekToTime));
    Equal("Skocz do procentu", CommandCatalog.GetDisplayName(CommandIds.SeekToPercentage));
    Equal("Właściwości i informacje", CommandCatalog.GetDisplayName(CommandIds.ItemProperties));
    Equal("Zwiększ prędkość odtwarzania", CommandCatalog.GetDisplayName(CommandIds.PlaybackRateUp));
    Equal("Przywróć normalną prędkość odtwarzania", CommandCatalog.GetDisplayName(CommandIds.PlaybackRateReset));
    Equal("Wycisz lub przywróć dźwięk bieżącej sesji", CommandCatalog.GetDisplayName(CommandIds.ToggleMuteCurrentSession));
    Equal("Wycisz lub przywróć dźwięk wszystkich sesji AMC", CommandCatalog.GetDisplayName(CommandIds.ToggleMuteAllSessions));
    Equal("Wybierz sesję 7", CommandCatalog.GetDisplayName(CommandIds.SessionSlot(7)));
    Equal("Przejdź do 50% utworu", CommandCatalog.GetDisplayName(CommandIds.SeekPercent(50)));
    True(CommandIds.TryParseSeekPercent(CommandIds.SeekPercent(90), out var percent), "Identyfikator skoku procentowego powinien być rozpoznawany.");
    Equal(90, percent);
    Equal(10, CommandCatalog.GetAllCommandIds().Count(commandId => CommandIds.TryParseSeekPercent(commandId, out _)));
    Equal(12, CommandCatalog.GetAllCommandIds().Count(commandId => CommandIds.TryParseRadioPreset(commandId, out _)));
    True(CommandIds.TryParseRadioPreset(CommandIds.RadioPreset(12), out var presetSlot), "Identyfikator presetu radiowego powinien być rozpoznawany.");
    Equal(12, presetSlot);
    Equal("12", RadioPresetSlots.Label(12));
    Equal("=", RadioPresetSlots.ShortcutLabel(12));
    Equal("znak równości", RadioPresetSlots.SpokenShortcutLabel(12));
    Equal("nieznane.polecenie", CommandCatalog.GetDisplayName("nieznane.polecenie"));
}

static void TestRadioPresetPersistence()
{
    var directory = Path.Combine(Path.GetTempPath(), $"amc-radio-preset-tests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        var store = new ConfigurationStore(Path.Combine(directory, "state.json"));
        var state = ConfigurationStore.CreateDefaultState();
        state.SchemaVersion = 29;
        state.Radio.Stations =
        [
            new RadioStationSettings { Id = "station-a", Name = "Stacja A", StreamUrl = "https://example.test/a" },
            new RadioStationSettings { Id = "station-b", Name = "Stacja B", StreamUrl = "https://example.test/b" }
        ];
        state.Radio.Presets =
        [
            new RadioPresetSettings { Slot = 1, StationId = "station-a" },
            new RadioPresetSettings { Slot = 1, StationId = "station-b" },
            new RadioPresetSettings { Slot = 12, StationId = "station-b" },
            new RadioPresetSettings { Slot = 13, StationId = "station-a" },
            new RadioPresetSettings { Slot = 2, StationId = "missing-station" }
        ];
        state.SessionPresets.EntriesBySession["radio"] =
        [
            new SessionPresetEntry
            {
                Slot = 12,
                TargetId = "existing-station",
                TargetKind = "station",
                TargetTitle = "Preset zachowany",
                TargetLocation = "https://example.test/existing"
            }
        ];
        state.SessionPresets.EntriesBySession["wiim-device:wiim-salon"] =
        [
            new SessionPresetEntry
            {
                Slot = 2,
                TargetId = "native-preset:12",
                TargetKind = "wiimNativePreset",
                TargetTitle = "Radio Rzeszów",
                TargetLocation = "https://example.test/rzeszow"
            }
        ];

        store.Save(state);
        var loaded = store.LoadOrCreate();

        Equal(0, loaded.Radio.Presets.Count);
        var presets = loaded.SessionPresets.EntriesBySession["radio"];
        Equal(2, presets.Count);
        Equal(1, presets[0].Slot);
        Equal("station-a", presets[0].TargetId);
        Equal("station", presets[0].TargetKind);
        Equal("Stacja A", presets[0].TargetTitle);
        Equal("https://example.test/a", presets[0].TargetLocation);
        Equal(12, presets[1].Slot);
        Equal("existing-station", presets[1].TargetId);
        var wiiMShortcut = loaded.SessionPresets.EntriesBySession["wiim-device:wiim-salon"].Single();
        Equal(2, wiiMShortcut.Slot);
        Equal("native-preset:12", wiiMShortcut.TargetId);
        Equal("wiimNativePreset", wiiMShortcut.TargetKind);
        Equal("Radio Rzeszów", wiiMShortcut.TargetTitle);
        Equal(ConfigurationStore.CurrentSchemaVersion, loaded.SchemaVersion);
    }
    finally
    {
        Directory.Delete(directory, true);
    }
}

static void TestRadioRecordingSchedule()
{
    var start = new DateTime(2026, 8, 29, 20, 0, 0, DateTimeKind.Utc);
    var schedule = new RadioRecordingScheduleSettings
    {
        Id = "schedule-a",
        StationId = "station-a",
        StationName = "Stacja A",
        StreamUrl = "https://example.test/live",
        NextStartUtcTicks = start.Ticks,
        TimeZoneId = "UTC",
        DurationMinutes = 60,
        Recurrence = RadioScheduleRecurrence.Daily
    };
    Equal(RadioScheduleDueKind.Future,
        RadioScheduleCalculator.Evaluate(schedule, start.AddMinutes(-1)).Kind);
    var active = RadioScheduleCalculator.Evaluate(schedule, start.AddMinutes(15));
    Equal(RadioScheduleDueKind.StartRemaining, active.Kind);
    Equal(TimeSpan.FromMinutes(45), active.Remaining);
    Equal(RadioScheduleDueKind.Missed,
        RadioScheduleCalculator.Evaluate(schedule, start.AddMinutes(60)).Kind);
    Equal(start.AddDays(1), RadioScheduleCalculator.FindNextStartUtc(schedule, start.AddMinutes(60)));

    schedule.Recurrence = RadioScheduleRecurrence.SelectedDays;
    schedule.ActiveDays = [DayOfWeek.Monday, DayOfWeek.Friday];
    Equal(
        new DateTime(2026, 8, 31, 20, 0, 0, DateTimeKind.Utc),
        RadioScheduleCalculator.FindNextStartUtc(schedule, start));

    var directory = Path.Combine(Path.GetTempPath(), $"amc-radio-schedule-tests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        var store = new ConfigurationStore(Path.Combine(directory, "state.json"));
        var state = ConfigurationStore.CreateDefaultState();
        state.Radio.RecordingsFolder = @"D:\Nagrania radia";
        state.Radio.UsePodcastDownloadsFolderForRecordings = true;
        state.Radio.RecordingFormat = RadioRecordingFormat.Original;
        state.Radio.RecordingBitrateKbps = 173;
        state.Radio.WakeScheduledRecordings = true;
        state.Radio.Stations =
        [
            new RadioStationSettings
            {
                Id = "station-a",
                Name = "Stacja A",
                StreamUrl = "https://example.test/live",
                Volume = 140
            }
        ];
        state.Radio.RecordingSchedules =
        [
            new RadioRecordingScheduleSettings
            {
                Id = "schedule-a",
                StationId = "station-a",
                StationName = "Stacja A",
                StreamUrl = "https://example.test/live",
                NextStartUtcTicks = start.Ticks,
                TimeZoneId = "UTC",
                DurationMinutes = 0,
                SegmentMinutes = 30,
                Recurrence = RadioScheduleRecurrence.SelectedDays,
                ActiveDays = [],
                FileNameTemplate = "Audycja - {data-polska}",
                RecordingFormat = RadioRecordingFormat.Aac,
                RecordingBitrateKbps = 222,
                WakeComputer = true
            },
            new RadioRecordingScheduleSettings
            {
                Id = "schedule-segmented",
                StationId = "station-b",
                StationName = "Stacja dzielona",
                StreamUrl = "https://example.test/segmented",
                NextStartUtcTicks = start.AddHours(1).Ticks,
                TimeZoneId = "UTC",
                DurationMinutes = 120,
                SegmentMinutes = 30,
                Recurrence = RadioScheduleRecurrence.Once
            }
        ];
        store.Save(state);
        var loaded = store.LoadOrCreate();
        Equal(2, loaded.Radio.RecordingSchedules.Count);
        var normalized = loaded.Radio.RecordingSchedules.Single(item => item.Id == "schedule-a");
        Equal(1, normalized.DurationMinutes);
        Equal(0, normalized.SegmentMinutes);
        Equal(1, normalized.ActiveDays.Count);
        Equal("Audycja - {data-polska}", normalized.FileNameTemplate);
        Equal(RadioRecordingFormat.Aac, normalized.RecordingFormat);
        Equal(192, normalized.RecordingBitrateKbps);
        Equal(true, normalized.WakeComputer);
        var segmented = loaded.Radio.RecordingSchedules.Single(item => item.Id == "schedule-segmented");
        Equal(120, segmented.DurationMinutes);
        Equal(30, segmented.SegmentMinutes);
        Equal(RadioRecordingFileNameTemplate.DefaultTemplate, segmented.FileNameTemplate);
        Equal(@"D:\Nagrania radia", loaded.Radio.RecordingsFolder);
        Equal(true, loaded.Radio.UsePodcastDownloadsFolderForRecordings);
        Equal(RadioRecordingFormat.Original, loaded.Radio.RecordingFormat);
        Equal(160, loaded.Radio.RecordingBitrateKbps);
        Equal(true, loaded.Radio.WakeScheduledRecordings);
        Equal(100, loaded.Radio.Stations.Single().Volume);
        Equal(ConfigurationStore.CurrentSchemaVersion, loaded.SchemaVersion);
    }
    finally
    {
        Directory.Delete(directory, true);
    }
}

static void TestRadioRecordingFileNameTemplate()
{
    var occurrence = new DateTime(2026, 8, 31, 7, 5, 0, DateTimeKind.Unspecified);
    var expanded = RadioRecordingFileNameTemplate.Expand(
        "Audycja: {stacja} - {data-polska} - {dzień-tygodnia} - {czas} - {część}",
        "Radio/Łódź",
        occurrence,
        partNumber: 3);
    Equal(
        "Audycja_ Radio_Łódź - 31.08.2026 - poniedziałek - 07-05 - 03",
        expanded);
    Equal(
        "Radio Łódź - 2026-08-31 07-05",
        RadioRecordingFileNameTemplate.Expand(null, "Radio Łódź", occurrence));
    Equal("_CON", RadioRecordingFileNameTemplate.SanitizeBaseName("CON"));
    True(!RadioRecordingFileNameTemplate.TryValidate("Audycja - {nieznany}", out var error)
         && error.Contains("Nieznany token", StringComparison.Ordinal),
        "Nieznany token nazwy pliku nie został odrzucony czytelnym błędem.");
    True(!RadioRecordingFileNameTemplate.TryValidate("Audycja - {data", out error)
         && error.Contains("nawias", StringComparison.Ordinal),
        "Niepełny token nazwy pliku nie został odrzucony.");
}

static void TestRadioRecognitionHistoryPersistence()
{
    var directory = Path.Combine(Path.GetTempPath(), $"amc-radio-recognition-tests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        var store = new ConfigurationStore(Path.Combine(directory, "state.json"));
        var state = ConfigurationStore.CreateDefaultState();
        Equal(false, state.Radio.AutomaticTrackRecognitionEnabled);
        state.Radio.AutomaticTrackRecognitionEnabled = true;
        state.Radio.AutomaticTrackRecognitionScope =
            RadioRecognitionScope.CurrentAndRecordingStations;
        state.Radio.RecognizedTracks =
        [
            new RadioRecognizedTrackSettings
            {
                Id = "recognized-a",
                StationId = "station-a",
                StationName = "Radio A",
                Title = "Utwór",
                Artist = "Wykonawca",
                Album = "Album",
                ReleaseDate = "2025",
                ProviderUri = "https://example.test/result",
                RecognizedUtcTicks = new DateTime(2026, 8, 30, 18, 0, 0, DateTimeKind.Utc).Ticks
            },
            new RadioRecognizedTrackSettings
            {
                Id = "empty",
                StationName = "Pusty wpis"
            }
        ];
        store.Save(state);

        var loaded = store.LoadOrCreate();
        Equal(true, loaded.Radio.AutomaticTrackRecognitionEnabled);
        Equal(RadioRecognitionScope.CurrentAndRecordingStations,
            loaded.Radio.AutomaticTrackRecognitionScope);
        Equal(true, RadioRecognitionScopeRules.IncludesCurrentStation(
            loaded.Radio.AutomaticTrackRecognitionScope));
        Equal(true, RadioRecognitionScopeRules.IncludesRecordingStations(
            loaded.Radio.AutomaticTrackRecognitionScope));
        Equal("aktualnie odtwarzana stacja i wszystkie stacje nagrywane w tle",
            RadioRecognitionScopeRules.GetLabel(loaded.Radio.AutomaticTrackRecognitionScope));
        Equal(1, loaded.Radio.RecognizedTracks.Count);
        var entry = loaded.Radio.RecognizedTracks[0];
        Equal("recognized-a", entry.Id);
        Equal("Radio A", entry.StationName);
        Equal("Utwór", entry.Title);
        Equal("Wykonawca", entry.Artist);
        Equal("Album", entry.Album);
        Equal("2025", entry.ReleaseDate);
        Equal("https://example.test/result", entry.ProviderUri);
        Equal(ConfigurationStore.CurrentSchemaVersion, loaded.SchemaVersion);
    }
    finally
    {
        Directory.Delete(directory, true);
    }
}

static void TestSessionPresetPersistence()
{
    var directory = Path.Combine(Path.GetTempPath(), $"amc-local-preset-tests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        var store = new ConfigurationStore(Path.Combine(directory, "state.json"));
        var state = ConfigurationStore.CreateDefaultState();
        state.SessionPresets.EntriesBySession["local"] =
        [
            new SessionPresetEntry
            {
                Slot = 1,
                TargetId = "folder:C:\\MUZYKA",
                TargetKind = "folder",
                TargetTitle = "Muzyka",
                TargetLocation = "C:\\Muzyka"
            },
            new SessionPresetEntry
            {
                Slot = 2,
                TargetId = "track-a",
                TargetKind = "item",
                TargetTitle = "Audycja",
                TargetLocation = "C:\\Audio\\Audycja.mp3"
            },
            new SessionPresetEntry
            {
                Slot = 2,
                TargetId = "duplicate",
                TargetKind = "item",
                TargetTitle = "Duplikat"
            },
            new SessionPresetEntry { Slot = 13, TargetId = "invalid", TargetKind = "item" }
        ];
        state.SessionPresets.EntriesBySession["tidal"] =
        [
            new SessionPresetEntry
            {
                Slot = 3,
                TargetId = "tidal-album",
                TargetKind = "album",
                TargetTitle = "Album TIDAL",
                TargetLocation = "https://tidal.example/album"
            }
        ];
        state.SessionPresets.EntriesBySession["appleMusic"] =
        [
            new SessionPresetEntry
            {
                Slot = 4,
                TargetId = "apple-playlist",
                TargetKind = "playlist",
                TargetTitle = "Playlista Apple Music"
            }
        ];
        state.SessionPresets.EntriesBySession["wiim"] =
        [
            new SessionPresetEntry
            {
                Slot = 5,
                TargetId = "wiim-preset",
                TargetKind = "device",
                TargetTitle = "Preset urządzenia WiiM"
            }
        ];

        store.Save(state);
        var loaded = store.LoadOrCreate();
        var presets = loaded.SessionPresets.EntriesBySession["local"];
        Equal(2, presets.Count);
        Equal("folder", presets[0].TargetKind);
        Equal("Muzyka", presets[0].TargetTitle);
        Equal("track-a", presets[1].TargetId);
        Equal("C:\\Audio\\Audycja.mp3", presets[1].TargetLocation);
        Equal("tidal-album", loaded.SessionPresets.EntriesBySession["tidal"].Single().TargetId);
        Equal("apple-playlist", loaded.SessionPresets.EntriesBySession["appleMusic"].Single().TargetId);
        Equal("wiim-preset", loaded.SessionPresets.EntriesBySession["wiim"].Single().TargetId);
    }
    finally
    {
        Directory.Delete(directory, true);
    }
}

static void TestMediaItemFormatting()
{
    var item = new MediaItem
    {
        Title = "Przykładowy utwór",
        Artist = "Przykładowy wykonawca",
        Duration = TimeSpan.FromSeconds(222),
        Kind = MediaItemKind.Track
    };
    var artistFirst = new[]
    {
        MediaItemField.Artist,
        MediaItemField.Title,
        MediaItemField.Duration,
        MediaItemField.Kind
    };
    Equal(
        "Przykładowy wykonawca, Przykładowy utwór, 3:42, utwór",
        MediaItemFormatter.Format(item, artistFirst));

    var withoutArtist = new MediaItem
    {
        Title = "Do odsłuchu",
        Duration = TimeSpan.FromMinutes(166),
        Kind = MediaItemKind.Playlist
    };
    Equal(
        "Do odsłuchu, 2:46:00, playlista",
        MediaItemFormatter.Format(withoutArtist, ListDisplaySettings.CreateDefaultFieldOrder()));

    var homogeneousFields = ListDisplaySettings.CreateDefaultFieldOrder()
        .Where(field => field != MediaItemField.Kind);
    Equal(
        "Do odsłuchu, 2:46:00",
        MediaItemFormatter.Format(withoutArtist, homogeneousFields));

    var repeatedPodcastAuthor = new MediaItem
    {
        Title = "ZACZYTAJ SIĘ Z RADIEM POZNAŃ",
        Artist = "zaczytaj się z radiem poznań",
        Kind = MediaItemKind.Podcast
    };
    Equal(
        "ZACZYTAJ SIĘ Z RADIEM POZNAŃ, podcast",
        MediaItemFormatter.Format(repeatedPodcastAuthor, ListDisplaySettings.CreateDefaultFieldOrder()));
}

static void TestLocalLibraryImport()
{
    var existingPath = @"D:\Nagrania\istniejący.mp3";
    var newPath = @"D:\Nagrania\nowy.ogg";
    var existing = new MediaItem
    {
        Id = "local-existing",
        Title = "Istniejący",
        Source = existingPath,
        IsInLibrary = false
    };
    var catalog = new List<MediaItem> { existing };

    var first = LocalLibraryImporter.Import(catalog, [existingPath, newPath, existingPath]);
    Equal(2, first.ImportedItems.Count);
    Equal(1, first.AddedItems.Count);
    Equal(1, first.RestoredItems.Count);
    Equal(true, existing.IsInLibrary);
    Equal(2, catalog.Count);
    Equal(true, catalog.Single(item => item.Source == newPath).IsInLibrary);

    var second = LocalLibraryImporter.Import(catalog, [existingPath, newPath]);
    Equal(2, second.ImportedItems.Count);
    Equal(0, second.AddedItems.Count);
    Equal(0, second.RestoredItems.Count);
    Equal(2, catalog.Count);
}

static void TestLocalLibrarySynchronization()
{
    var root = Path.Combine(Path.GetTempPath(), $"amc-sync-root-{Guid.NewGuid():N}");
    var unavailableRoot = Path.Combine(Path.GetTempPath(), $"amc-sync-offline-{Guid.NewGuid():N}");
    var keepPath = Path.Combine(root, "Album", "zostaje.mp3");
    var missingPath = Path.Combine(root, "znika.flac");
    var excludedPath = Path.Combine(root, "pomijany.ogg");
    var newPath = Path.Combine(root, "nowy.wav");
    var manualPath = Path.Combine(Path.GetTempPath(), "pojedynczy.aac");
    var offlinePath = Path.Combine(unavailableRoot, "offline.mp3");
    var keep = new MediaItem
    {
        Id = "keep",
        Title = "Własna nazwa AMC",
        HasCustomTitle = true,
        Source = keepPath,
        IsInLibrary = true
    };
    var missing = new MediaItem { Id = "missing", Title = "Znika", Source = missingPath, IsInLibrary = true };
    var excluded = new MediaItem { Id = "excluded", Title = "Pomijany", Source = excludedPath, IsInLibrary = true };
    var manual = new MediaItem { Id = "manual", Title = "Pojedynczy", Source = manualPath, IsInLibrary = true };
    var offline = new MediaItem { Id = "offline", Title = "Offline", Source = offlinePath, IsInLibrary = true };
    var catalog = new List<MediaItem> { keep, missing, excluded, manual, offline };

    var first = LocalLibrarySynchronizer.Synchronize(
        catalog,
        [root],
        [keepPath, excludedPath, newPath],
        [excludedPath]);
    Equal(1, first.AddedItems.Count);
    Equal(false, missing.IsAvailable);
    Equal(false, excluded.IsInLibrary);
    Equal(true, excluded.IsAvailable);
    Equal(true, manual.IsAvailable);
    Equal(true, offline.IsAvailable);
    Equal(6, catalog.Count);
    Equal(true, catalog.Single(item => item.Source == newPath).IsInLibrary);
    Equal("Własna nazwa AMC", keep.Title);
    Equal(true, keep.HasCustomTitle);

    var second = LocalLibrarySynchronizer.Synchronize(
        catalog,
        [root],
        [keepPath, missingPath, excludedPath, newPath],
        [excludedPath]);
    Equal(0, second.AddedItems.Count);
    Equal(true, missing.IsAvailable);
    Equal(true, missing.IsInLibrary);
    Equal(false, excluded.IsInLibrary);
    Equal(6, catalog.Count);
}

static void TestLocalFileRenamePolicy()
{
    var directory = Path.Combine(Path.GetTempPath(), $"amc-rename-tests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        var current = Path.Combine(directory, "stara nazwa.mp3");
        File.WriteAllBytes(current, [1, 2, 3]);
        True(
            LocalFileRenamePolicy.TryBuildTargetPath(current, "nowa nazwa", out var target, out var error),
            $"Poprawna nazwa powinna zostać przyjęta: {error}");
        Equal(Path.Combine(directory, "nowa nazwa.mp3"), target);
        True(
            LocalFileRenamePolicy.TryBuildTargetPath(current, "nowa nazwa.mp3", out var targetWithExtension, out _),
            "Wpisanie dotychczasowego rozszerzenia nie powinno go dublować.");
        Equal(target, targetWithExtension);
        True(!LocalFileRenamePolicy.TryBuildTargetPath(current, "CON", out _, out _),
            "Nazwa zarezerwowana przez Windows musi zostać odrzucona.");
        True(!LocalFileRenamePolicy.TryBuildTargetPath(current, "folder\\plik", out _, out _),
            "Nazwa nie może zawierać ścieżki.");

        File.WriteAllBytes(target, [4, 5, 6]);
        True(!LocalFileRenamePolicy.TryBuildTargetPath(current, "nowa nazwa", out _, out _),
            "Istniejący plik docelowy musi zostać ochroniony przed nadpisaniem.");
    }
    finally
    {
        Directory.Delete(directory, true);
    }
}

static void TestLocalFolderSynchronizationCycle()
{
    var root = Path.Combine(Path.GetTempPath(), $"amc-sync-cycle-{Guid.NewGuid():N}");
    var nested = Path.Combine(root, "Audycje");
    Directory.CreateDirectory(nested);
    try
    {
        var firstPath = Path.Combine(root, "pierwszy.mp3");
        var secondPath = Path.Combine(nested, "drugi.flac");
        File.WriteAllBytes(firstPath, [1, 2, 3]);
        var catalog = new List<MediaItem>();

        var firstScan = LocalAudioFileDiscovery.FindFiles(root);
        var first = LocalLibrarySynchronizer.Synchronize(catalog, [root], firstScan, []);
        Equal(1, first.AddedItems.Count);
        Equal(true, catalog.Single().IsAvailable);

        File.WriteAllBytes(secondPath, [4, 5, 6]);
        var secondScan = LocalAudioFileDiscovery.FindFiles(root);
        var second = LocalLibrarySynchronizer.Synchronize(catalog, [root], secondScan, []);
        Equal(1, second.AddedItems.Count);
        Equal(2, catalog.Count(item => item.IsAvailable && item.IsInLibrary));

        File.Delete(firstPath);
        var thirdScan = LocalAudioFileDiscovery.FindFiles(root);
        var third = LocalLibrarySynchronizer.Synchronize(catalog, [root], thirdScan, []);
        Equal(1, third.BecameUnavailableItems.Count);
        Equal(false, catalog.Single(item => item.Source == firstPath).IsAvailable);
        Equal(true, catalog.Single(item => item.Source == secondPath).IsAvailable);
    }
    finally
    {
        Directory.Delete(root, true);
    }
}

static void TestVersion17LocalLibraryMigration()
{
    var directory = Path.Combine(Path.GetTempPath(), $"amc-v17-library-tests-{Guid.NewGuid():N}");
    var source = Path.Combine(directory, "Muzyka");
    Directory.CreateDirectory(source);
    try
    {
        var statePath = Path.Combine(directory, "state.json");
        var store = new ConfigurationStore(statePath);
        var state = ConfigurationStore.CreateDefaultState();
        state.SchemaVersion = 17;
        var path = Path.Combine(source, "wykluczony.mp3");
        state.LocalMedia.FolderSources.Add(new LocalFolderSourceSettings
        {
            Id = "source",
            Path = source,
            DisplayName = "Muzyka"
        });
        state.LocalMedia.Items.Add(new LocalMediaItemSettings
        {
            Id = "excluded",
            Title = "Wykluczony",
            Path = path,
            IsInLibrary = false
        });
        state.LocalMedia.Items.Add(new LocalMediaItemSettings
        {
            Id = "included",
            Title = "Pozostawiony",
            Path = Path.Combine(source, "pozostawiony.mp3"),
            IsInLibrary = true
        });
        state.SessionNavigation.Sessions["local"] = new SessionNavigationState
        {
            CurrentView = "Biblioteka"
        };
        WriteLegacyState(statePath, state);
        var document = JsonNode.Parse(File.ReadAllText(statePath))!.AsObject();
        var localMedia = document["localMedia"]!.AsObject();
        localMedia.Remove("excludedPaths");
        localMedia.Remove("libraryView");
        foreach (var item in localMedia["items"]!.AsArray()) item!.AsObject().Remove("isAvailable");
        File.WriteAllText(statePath, document.ToJsonString());

        var loaded = store.LoadOrCreate();
        Equal(ConfigurationStore.CurrentSchemaVersion, loaded.SchemaVersion);
        Equal("Wszystkie pliki", loaded.LocalMedia.LibraryView);
        Equal("Wszystkie pliki", loaded.SessionNavigation.Sessions["local"].CurrentView);
        Equal(1, loaded.LocalMedia.ExcludedPaths.Count);
        Equal(Path.GetFullPath(path), loaded.LocalMedia.ExcludedPaths[0]);
        Equal(true, loaded.LocalMedia.Items[0].IsAvailable);
        True(File.Exists(Path.Combine(directory, "library.db")), "Migracja powinna utworzyć bazę SQLite.");
        True(
            File.Exists(Path.Combine(directory, "state.pre-sqlite-migration.json")),
            "Migracja powinna zachować źródłowy JSON.");
        var settingsOnly = JsonNode.Parse(File.ReadAllText(statePath))!.AsObject();
        Equal(0, settingsOnly["localMedia"]!["items"]!.AsArray().Count);
    }
    finally
    {
        Directory.Delete(directory, true);
    }
}

static void TestVersion18EmptySourceMigration()
{
    var directory = Path.Combine(Path.GetTempPath(), $"amc-v18-empty-source-{Guid.NewGuid():N}");
    var source = Path.Combine(directory, "iCloud");
    Directory.CreateDirectory(source);
    try
    {
        var statePath = Path.Combine(directory, "state.json");
        var store = new ConfigurationStore(statePath);
        var state = ConfigurationStore.CreateDefaultState();
        state.SchemaVersion = 18;
        state.LocalMedia.FolderSources.Add(new LocalFolderSourceSettings
        {
            Id = "cloud-source",
            Path = source,
            DisplayName = "iCloud"
        });
        foreach (var name in new[] { "pierwszy.mp3", "drugi.m4a" })
        {
            var path = Path.Combine(source, name);
            state.LocalMedia.Items.Add(new LocalMediaItemSettings
            {
                Id = name,
                Title = Path.GetFileNameWithoutExtension(name),
                Path = path,
                IsInLibrary = false,
                IsAvailable = true
            });
            state.LocalMedia.ExcludedPaths.Add(path);
        }
        WriteLegacyState(statePath, state);

        var loaded = store.LoadOrCreate();
        Equal(ConfigurationStore.CurrentSchemaVersion, loaded.SchemaVersion);
        Equal(0, loaded.LocalMedia.ExcludedPaths.Count);
        Equal(true, loaded.LocalMedia.Items.All(item => item.IsInLibrary));
    }
    finally
    {
        Directory.Delete(directory, true);
    }
}

static void WriteLegacyState(string path, PersistedState state)
{
    var options = new JsonSerializerOptions
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };
    File.WriteAllText(path, JsonSerializer.Serialize(state, options));
}

static void TestSqliteLibraryMigration()
{
    var directory = Path.Combine(Path.GetTempPath(), $"amc-sqlite-tests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        var statePath = Path.Combine(directory, "state.json");
        var databasePath = Path.Combine(directory, "library.db");
        var state = ConfigurationStore.CreateDefaultState();
        state.SchemaVersion = 24;
        state.LocalMedia.FolderSources.Add(new LocalFolderSourceSettings
        {
            Id = "source",
            DisplayName = "Duża biblioteka",
            Path = Path.Combine(directory, "Muzyka")
        });
        for (var index = 0; index < 1500; index++)
        {
            state.LocalMedia.Items.Add(new LocalMediaItemSettings
            {
                Id = $"item-{index}",
                Title = $"Utwór {index}",
                Path = Path.Combine(directory, "Muzyka", $"Utwór {index}.mp3"),
                IsFavorite = index % 10 == 0,
                IsInLibrary = true,
                IsAvailable = true,
                ResumePositionTicks = index
            });
        }
        state.LocalMedia.CurrentItemId = "item-1499";
        state.LocalMedia.Items[1498].IsInQueue = true;
        state.LocalMedia.Items[1499].IsInQueue = true;
        state.CollectionOrders.QueueItemIdsBySession["local"] = ["item-1499", "item-1498"];
        state.PlaybackHistory.ItemIdsBySession["local"] = ["item-1499", "item-1498"];
        state.Bookmarks.Entries.Add(new BookmarkEntry
        {
            Id = "bookmark",
            SessionId = "local",
            SessionName = "Pliki lokalne",
            ItemId = "item-1499",
            ItemTitle = "Utwór 1499",
            PositionTicks = 1234
        });
        WriteLegacyState(statePath, state);

        var store = new ConfigurationStore(statePath, databasePath);
        var migrated = store.LoadOrCreate();
        Equal(1500, migrated.LocalMedia.Items.Count);
        Equal("item-1499", migrated.LocalMedia.CurrentItemId);
        Equal(2, migrated.PlaybackHistory.ItemIdsBySession["local"].Count);
        Equal(1, migrated.Bookmarks.Entries.Count);
        True(migrated.CollectionOrders.QueueItemIdsBySession["LOCAL"].SequenceEqual(
                ["item-1499", "item-1498"]),
            "Migracja SQLite powinna zachować ręczny porządek Kolejki.");
        True(File.Exists(databasePath), "Brak pliku Biblioteki SQLite.");

        migrated.LocalMedia.Items[1499].Title = "Zmieniony tytuł";
        migrated.LocalMedia.Items[1499].HasCustomTitle = true;
        store.Save(migrated);
        var reloaded = new ConfigurationStore(statePath, databasePath).LoadOrCreate();
        Equal(1500, reloaded.LocalMedia.Items.Count);
        Equal("Zmieniony tytuł", reloaded.LocalMedia.Items.Single(item => item.Id == "item-1499").Title);
        Equal(true, reloaded.LocalMedia.Items.Single(item => item.Id == "item-1499").HasCustomTitle);
        True(reloaded.CollectionOrders.QueueItemIdsBySession["local"].SequenceEqual(
                ["item-1499", "item-1498"]),
            "Ponowny odczyt SQLite powinien zachować ręczny porządek Kolejki.");
    }
    finally
    {
        Directory.Delete(directory, true);
    }
}

static void TestAudioParametersFormatting()
{
    var polish = CultureInfo.GetCultureInfo("pl-PL");
    var exact = new MediaItem
    {
        BitrateKbps = 192,
        SampleRateHz = 48_000
    };
    Equal("192 kb/s, 48 kHz", AudioParametersFormatter.Format(exact, polish));

    var estimated = new MediaItem
    {
        BitrateKbps = 322,
        IsBitrateEstimated = true,
        SampleRateHz = 44_100
    };
    Equal("około 322 kb/s, 44,1 kHz", AudioParametersFormatter.Format(estimated, polish));
    Equal("322 kb/s, 44,1 kHz", AudioParametersFormatter.FormatCompact(estimated, polish));
    Equal(
        "128 kb/s, 22,05 kHz",
        AudioParametersFormatter.Format(
            new MediaItem { BitrateKbps = 128, SampleRateHz = 22_050 },
            polish));

    Equal(
        "96 kHz",
        AudioParametersFormatter.Format(new MediaItem { SampleRateHz = 96_000 }, polish));
    Equal("brak danych audio", AudioParametersFormatter.Format(new MediaItem(), polish));
    Equal(string.Empty, AudioParametersFormatter.FormatCompact(new MediaItem(), polish));
}

static void TestLegacyStateMigration()
{
    var directory = Path.Combine(Path.GetTempPath(), $"amc-legacy-tests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        var statePath = Path.Combine(directory, "state.json");
        var store = new ConfigurationStore(statePath);
        store.Save(ConfigurationStore.CreateDefaultState());

        var document = JsonNode.Parse(File.ReadAllText(statePath))?.AsObject()
            ?? throw new InvalidOperationException("Nie udało się utworzyć testowej konfiguracji.");
        document["schemaVersion"] = 1;
        var settings = document["settings"]?.AsObject()
            ?? throw new InvalidOperationException("Brak ustawień w testowej konfiguracji.");
        settings["prefixChord"] = "Ctrl+Alt+Space";
        settings["messages"]!["enabled"] = false;
        settings.Remove("lists");
        settings.Remove("startupTarget");
        File.WriteAllText(statePath, document.ToJsonString());

        var loaded = store.LoadOrCreate();
        Equal(ConfigurationStore.CurrentSchemaVersion, loaded.SchemaVersion);
        Equal("Ctrl+Alt+Windows+F12", loaded.Settings.PrefixChord);
        Equal(true, loaded.Settings.Messages.Enabled);
        Equal(StartupTarget.MediaList, loaded.Settings.StartupTarget);
        Equal(true, loaded.Settings.PausePlaybackWhenLeavingPlayer);
        Equal(true, loaded.Settings.RememberLocalPlaybackPositions);
        Equal(MediaItemField.Title, loaded.Settings.Lists.FieldOrder[0]);
        Equal(4, loaded.Settings.Lists.FieldOrder.Count);

        document = JsonNode.Parse(File.ReadAllText(statePath))?.AsObject()
            ?? throw new InvalidOperationException("Nie udało się ponownie odczytać konfiguracji.");
        document["schemaVersion"] = ConfigurationStore.CurrentSchemaVersion;
        settings = document["settings"]?.AsObject()
            ?? throw new InvalidOperationException("Brak ustawień w bieżącej konfiguracji.");
        settings["prefixChord"] = "CTRL-Alt-Win-F12";
        File.WriteAllText(statePath, document.ToJsonString());

        loaded = store.LoadOrCreate();
        Equal("Ctrl+Alt+Windows+F12", loaded.Settings.PrefixChord);
        store.Save(loaded);
        document = JsonNode.Parse(File.ReadAllText(statePath))?.AsObject()
            ?? throw new InvalidOperationException("Nie udało się sprawdzić zapisu konfiguracji.");
        settings = document["settings"]?.AsObject()
            ?? throw new InvalidOperationException("Brak ustawień po zapisie konfiguracji.");
        Equal("Ctrl+Alt+Windows+F12", settings["prefixChord"]?.GetValue<string>());

        settings["prefixChord"] = string.Empty;
        File.WriteAllText(statePath, document.ToJsonString());
        loaded = store.LoadOrCreate();
        Equal("Ctrl+Alt+Windows+F12", loaded.Settings.PrefixChord);
    }
    finally
    {
        Directory.Delete(directory, true);
    }
}

static void TestVersion2StateMigration()
{
    var directory = Path.Combine(Path.GetTempPath(), $"amc-v2-tests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        var statePath = Path.Combine(directory, "state.json");
        var store = new ConfigurationStore(statePath);
        store.Save(ConfigurationStore.CreateDefaultState());

        var document = JsonNode.Parse(File.ReadAllText(statePath))?.AsObject()
            ?? throw new InvalidOperationException("Nie udało się utworzyć testowej konfiguracji alpha.4.");
        document["schemaVersion"] = 2;
        var settings = document["settings"]?.AsObject()
            ?? throw new InvalidOperationException("Brak ustawień w testowej konfiguracji alpha.4.");
        settings["prefixChord"] = "Ctrl+Alt+Windows+Enter";
        settings["messages"]!["enabled"] = false;
        File.WriteAllText(statePath, document.ToJsonString());

        var loaded = store.LoadOrCreate();
        Equal(ConfigurationStore.CurrentSchemaVersion, loaded.SchemaVersion);
        Equal("Ctrl+Alt+Windows+F12", loaded.Settings.PrefixChord);
        Equal(true, loaded.Settings.Messages.Enabled);

        settings["prefixChord"] = "Ctrl+Alt+Shift+F11";
        File.WriteAllText(statePath, document.ToJsonString());
        loaded = store.LoadOrCreate();
        Equal("Ctrl+Alt+Shift+F11", loaded.Settings.PrefixChord);
    }
    finally
    {
        Directory.Delete(directory, true);
    }
}

static void TestVersion3MessageMigration()
{
    var directory = Path.Combine(Path.GetTempPath(), $"amc-v3-tests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        var statePath = Path.Combine(directory, "state.json");
        var store = new ConfigurationStore(statePath);
        store.Save(ConfigurationStore.CreateDefaultState());

        var document = JsonNode.Parse(File.ReadAllText(statePath))?.AsObject()
            ?? throw new InvalidOperationException("Nie udało się utworzyć testowej konfiguracji alpha.5.");
        document["schemaVersion"] = 3;
        var templates = document["settings"]?["messages"]?["templates"]?.AsObject()
            ?? throw new InvalidOperationException("Brak szablonów komunikatów w konfiguracji alpha.5.");
        templates.Remove("queue.added");
        templates.Remove("queue.removed");
        templates.Remove("playNext.added");
        templates.Remove("playNext.removed");
        File.WriteAllText(statePath, document.ToJsonString());

        var loaded = store.LoadOrCreate();
        Equal(ConfigurationStore.CurrentSchemaVersion, loaded.SchemaVersion);
        Equal("Dodano do kolejki: {item}", loaded.Settings.Messages.Templates["queue.added"]);
        Equal("Usunięto z następnych: {item}", loaded.Settings.Messages.Templates["playNext.removed"]);
    }
    finally
    {
        Directory.Delete(directory, true);
    }
}

static void TestVersion4MessageMigration()
{
    var directory = Path.Combine(Path.GetTempPath(), $"amc-v4-tests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        var statePath = Path.Combine(directory, "state.json");
        var store = new ConfigurationStore(statePath);
        store.Save(ConfigurationStore.CreateDefaultState());

        var document = JsonNode.Parse(File.ReadAllText(statePath))?.AsObject()
            ?? throw new InvalidOperationException("Nie udało się utworzyć testowej konfiguracji alpha.6.");
        document["schemaVersion"] = 4;
        var templates = document["settings"]?["messages"]?["templates"]?.AsObject()
            ?? throw new InvalidOperationException("Brak szablonów komunikatów w konfiguracji alpha.6.");
        templates["queue.added"] = "Dodano do kolejki — demonstracja";
        templates["queue.removed"] = "Usunięto z kolejki — demonstracja";
        templates["playNext.added"] = "Ustawiono do odtworzenia jako następne — demonstracja";
        templates["playNext.removed"] = "Usunięto z odtwarzanych jako następne — demonstracja";
        File.WriteAllText(statePath, document.ToJsonString());

        var loaded = store.LoadOrCreate();
        Equal(ConfigurationStore.CurrentSchemaVersion, loaded.SchemaVersion);
        Equal("Dodano do kolejki: {item}", loaded.Settings.Messages.Templates["queue.added"]);
        Equal("Usunięto z kolejki: {item}", loaded.Settings.Messages.Templates["queue.removed"]);
        Equal("Odtwarzaj jako następne: {item}", loaded.Settings.Messages.Templates["playNext.added"]);
        Equal("Usunięto z następnych: {item}", loaded.Settings.Messages.Templates["playNext.removed"]);

        document["schemaVersion"] = 4;
        templates["queue.added"] = "Mój własny komunikat";
        File.WriteAllText(statePath, document.ToJsonString());
        loaded = store.LoadOrCreate();
        Equal("Mój własny komunikat", loaded.Settings.Messages.Templates["queue.added"]);
    }
    finally
    {
        Directory.Delete(directory, true);
    }
}

static void TestVersion5MessageMigration()
{
    var directory = Path.Combine(Path.GetTempPath(), $"amc-v5-tests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        var statePath = Path.Combine(directory, "state.json");
        var store = new ConfigurationStore(statePath);
        store.Save(ConfigurationStore.CreateDefaultState());

        var document = JsonNode.Parse(File.ReadAllText(statePath))?.AsObject()
            ?? throw new InvalidOperationException("Nie udało się utworzyć testowej konfiguracji alpha.7.");
        document["schemaVersion"] = 5;
        var templates = document["settings"]?["messages"]?["templates"]?.AsObject()
            ?? throw new InvalidOperationException("Brak szablonów komunikatów w konfiguracji alpha.7.");
        templates["queue.added"] = "Dodano do kolejki";
        templates["queue.removed"] = "Usunięto z kolejki";
        templates["playNext.added"] = "Odtwarzaj jako następne";
        templates["playNext.removed"] = "Usunięto z następnych";
        File.WriteAllText(statePath, document.ToJsonString());

        var loaded = store.LoadOrCreate();
        Equal(ConfigurationStore.CurrentSchemaVersion, loaded.SchemaVersion);
        Equal("Dodano do kolejki: {item}", loaded.Settings.Messages.Templates["queue.added"]);
        Equal("Usunięto z następnych: {item}", loaded.Settings.Messages.Templates["playNext.removed"]);

        document["schemaVersion"] = 5;
        templates["queue.added"] = "Własny tekst kolejki";
        File.WriteAllText(statePath, document.ToJsonString());
        loaded = store.LoadOrCreate();
        Equal("Własny tekst kolejki", loaded.Settings.Messages.Templates["queue.added"]);
    }
    finally
    {
        Directory.Delete(directory, true);
    }
}

static void TestSessions()
{
    var settings = new AppSettings();
    var manager = new SessionManager(settings);
    Equal("TIDAL", manager.Current.DisplayName);
    Equal("WiiM", manager.SelectSlot(2)?.DisplayName);
    Equal("Apple Music", manager.MoveSession(-1).DisplayName);
    Equal("TIDAL", manager.SelectSession("tidal")?.DisplayName);
    Equal("tidal", settings.LastSessionId);
    Equal(17, manager.Current.Items.Count);
    Equal(2, manager.Current.Items.Count(item => item.IsInLibrary));
    True(manager.Current.Items.Count(item => item.Title.StartsWith('B')) >= 2, "Dane demonstracyjne powinny umożliwiać powtarzanie litery B.");
    True(manager.Current.Items.Count(item => item.Title.StartsWith('C')) >= 2, "Dane demonstracyjne powinny umożliwiać powtarzanie litery C.");
    True(manager.Current.Items.Any(item => item.IsInQueue), "Kolejka demonstracyjna nie powinna być pusta.");
    var tidalQueueIds = manager.Current.Items
        .Where(item => item.IsInQueue || item.IsPlayNext)
        .Select(item => item.Id)
        .ToArray();
    Equal("Apple Music", manager.SelectSession("appleMusic")?.DisplayName);
    Equal("TIDAL", manager.SelectSession("tidal")?.DisplayName);
    Equal(
        string.Join('|', tidalQueueIds),
        string.Join('|', manager.Current.Items
            .Where(item => item.IsInQueue || item.IsPlayNext)
            .Select(item => item.Id)));
    Equal(true, manager.Current.ToggleQueue(manager.Current.CurrentItem));
    Equal(false, manager.Current.ToggleQueue(manager.Current.CurrentItem));
    Equal(true, manager.Current.TogglePlayNext(manager.Current.CurrentItem));
    Equal(false, manager.Current.TogglePlayNext(manager.Current.CurrentItem));
    Equal(true, manager.Current.TogglePlayNext(manager.Current.CurrentItem));
    Equal(false, manager.Current.ToggleQueue(manager.Current.CurrentItem));
    Equal(false, manager.Current.CurrentItem.IsPlayNext);
    var selected = manager.Current.Items.First(item => item.Title == "Brzeg ciszy");
    True(manager.Current.Play(selected), "Wybrany element powinien dać się odtworzyć.");
    Equal(selected, manager.Current.CurrentItem);
    True(manager.Current.IsPlaying, "Odtwarzanie wybranego elementu powinno uruchomić sesję.");
    True(manager.Current.Play(selected), "Ponowne polecenie odtwarzania nie powinno przełączać na pauzę.");
    True(manager.Current.IsPlaying, "Polecenie odtwarzania ma pozostać jednoznaczne także dla bieżącego elementu.");
    True(manager.Current.Activate(selected), "Ponowne otwarcie bieżącego elementu powinno być obsłużone.");
    Equal(false, manager.Current.IsPlaying);
    True(manager.Current.Activate(selected), "Kolejne otwarcie bieżącego elementu powinno wznowić odtwarzanie.");
    Equal(true, manager.Current.IsPlaying);
    var album = manager.Current.Items.First(item => item.Kind == MediaItemKind.Album);
    Equal("Album demonstracyjny", album.PrimaryText);
    var playlist = manager.Current.Items.First(item => item.Kind == MediaItemKind.Playlist);
    Equal("Do odsłuchu", playlist.PrimaryText);

    settings.LastSessionId = "nieistniejąca";
    manager = new SessionManager(settings);
    Equal("wiim", settings.LastSessionId);
}

static void TestSessionOrder()
{
    var defaults = SessionSlotOrder.CreateDefault();
    Equal("local", defaults[1]);
    Equal("wiim", defaults[2]);
    Equal("tidal", defaults[3]);
    Equal("appleMusic", defaults[4]);
    Equal("radio", defaults[5]);
    Equal("podcasts", defaults[6]);

    var settings = new AppSettings
    {
        LastSessionId = "tidal",
        SessionSlots = new Dictionary<int, string>
        {
            [1] = "appleMusic",
            [2] = "tidal",
            [3] = "wiim",
            [4] = "local"
        }
    };
    var manager = new SessionManager(settings);
    Equal("TIDAL", manager.Current.DisplayName);
    Equal("Apple Music", manager.MoveSession(-1).DisplayName);
    Equal("TIDAL", manager.MoveSession(1).DisplayName);

    var local = new MediaItem { Id = "local-order", Title = "Lokalny", Source = @"C:\Muzyka\lokalny.mp3" };
    var (localSession, slot) = manager.AddOrUpdateTransientSession(
        "local", "Pliki lokalne", [local], new FakeMediaOutput(), 1);
    Equal(4, slot);
    Equal(localSession, manager.SelectSlot(4));
    Equal("Pliki lokalne", manager.Sessions[^1].DisplayName);
}

static void TestLocalPlaybackBoundary()
{
    var output = new FakeMediaOutput();
    var item = new MediaItem
    {
        Id = "local-1",
        Title = "Plik testowy",
        Source = @"C:\Muzyka\plik-testowy.mp3"
    };
    var manager = new SessionManager(new AppSettings());
    var (session, slot) = manager.AddOrUpdateTransientSession(
        "local",
        "Pliki lokalne",
        [item],
        output,
        4);

    Equal(1, slot);
    Equal(session, manager.SelectSlot(1));
    Equal(TimeSpan.Zero, session.Position);
    True(session.Activate(item), "Lokalny element powinien uruchamiać wyjście dźwięku.");
    Equal(1, output.PlayCount);
    Equal(item, output.LastItem);
    Equal(35, output.Volume);
    Equal(1d, output.PlaybackRate);
    True(session.SupportsPlaybackRate, "Lokalne wyjście powinno udostępniać regulację prędkości.");

    True(session.ToggleMute(), "Pierwsze przełączenie powinno wyciszyć bieżącą sesję.");
    Equal(0, output.Volume);
    Equal(true, session.IsMuted);
    Equal(false, session.ToggleMute());
    Equal(35, output.Volume);
    Equal(false, session.IsMuted);
    Equal(true, manager.ToggleAllSessionsMute());
    Equal(0, output.Volume);
    Equal(true, session.IsGloballyMuted);
    True(session.ToggleMute(), "Indywidualne wyciszenie powinno pozostać niezależną warstwą.");
    Equal(false, manager.ToggleAllSessionsMute());
    Equal(0, output.Volume);
    Equal(true, session.IsSessionMuted);
    Equal(false, session.ToggleMute());
    Equal(35, output.Volume);

    session.TogglePlayback();
    Equal(1, output.PauseCount);
    Equal(false, session.IsPlaying);
    session.TogglePlayback();
    Equal(2, output.PlayCount);
    Equal(true, session.IsPlaying);
    session.StopPlayback();
    Equal(1, output.StopCount);
    Equal(false, session.IsPlaying);
    session.TogglePlayback();

    output.Position = TimeSpan.FromSeconds(27);
    True(session.RestartPlaybackOutput(), "Zmiana urządzenia powinna ponownie uruchomić aktywne wyjście.");
    Equal(TimeSpan.FromSeconds(27), output.Position);
    Equal(true, session.IsPlaying);
    Equal(2, output.StopCount);
    Equal(4, output.PlayCount);

    output.Position = TimeSpan.FromSeconds(31);
    session.MarkPlaybackFailed(output.Position);
    Equal(false, session.IsPlaying);
    Equal(TimeSpan.FromSeconds(31), session.Position);
    True(!session.RestartPlaybackOutput(),
        "Zwykła zmiana urządzenia nie może samoczynnie uruchamiać świadomie zatrzymanej sesji.");
    True(session.RestartPlaybackOutput(
            TimeSpan.FromSeconds(31),
            resumeIfStopped: true),
        "Po zniknięciu urządzenia ręczny wybór sprawnego wyjścia powinien wznowić sesję.");
    Equal(true, session.IsPlaying);
    Equal(TimeSpan.FromSeconds(31), output.Position);

    output.Position = TimeSpan.FromSeconds(30);
    session.Seek(TimeSpan.FromSeconds(10));
    Equal(TimeSpan.FromSeconds(40), output.Position);
    session.ChangeVolume(5);
    Equal(40, output.Volume);
    True(session.ChangePlaybackRate(1), "Przyspieszenie powinno zostać przekazane do wyjścia audio.");
    Equal(1.25d, session.PlaybackRate);
    Equal(1.25d, output.PlaybackRate);
    True(session.ChangePlaybackRate(-1), "Zwolnienie powinno zostać przekazane do wyjścia audio.");
    Equal(1d, output.PlaybackRate);
    True(session.SetPlaybackRate(2d), "Ustawienie najwyższej prędkości powinno być obsłużone.");
    Equal(2d, output.PlaybackRate);
    True(session.SetPlaybackRate(1d), "Przywrócenie normalnej prędkości powinno być obsłużone.");

    var stationA = new MediaItem { Id = "radio-a", Title = "Radio A", Kind = MediaItemKind.Station };
    var stationB = new MediaItem { Id = "radio-b", Title = "Radio B", Kind = MediaItemKind.Station };
    var radioOutput = new FakeMediaOutput();
    var radioSession = new DemoMediaSession(
        "radio",
        "Radio internetowe",
        [stationA, stationB],
        radioOutput,
        volumeOverride: item => item.Id == stationA.Id ? 20 : 75);
    True(radioSession.Play(stationA), "Pierwsza stacja powinna się uruchomić.");
    Equal(20, radioOutput.Volume);
    True(radioSession.PlayRelative(1), "Druga stacja powinna się uruchomić.");
    Equal(75, radioOutput.Volume);

    session.AddItems([item]);
    Equal(1, session.Items.Count);
    var nextItem = new MediaItem
    {
        Id = "local-2",
        Title = "Następny plik",
        Source = @"C:\Muzyka\następny-plik.mp3"
    };
    session.AddItems([nextItem]);
    Equal(2, session.Items.Count);
    var removed = session.RemoveItems([item.Id]);
    Equal(1, removed.Count);
    Equal(nextItem, session.CurrentItem);
    Equal(1, session.Items.Count);
    Equal(0, session.RemoveItems([nextItem.Id]).Count);
    session.RestoreItems(removed);
    Equal(2, session.Items.Count);
    Equal(item, session.Items[0]);
    True(session.SelectItem(item), "Przywrócony plik powinien dać się ponownie wybrać.");
    True(session.PlayRelative(1), "Page Down powinien uruchomić następny plik.");
    Equal(nextItem, session.CurrentItem);
    Equal(true, session.IsPlaying);
    True(!session.PlayRelative(1), "Następny plik nie powinien zapętlać końca listy.");
    True(session.PlayRelative(-1), "Page Up powinien uruchomić poprzedni plik.");
    Equal(item, session.CurrentItem);
    True(!session.PlayRelative(-1), "Poprzedni plik nie powinien zapętlać początku listy.");
    True(session.Play(item), "Pierwszy plik powinien ponownie rozpocząć odtwarzanie.");
    session.SetRememberedPosition(nextItem.Id, TimeSpan.FromMinutes(17));
    Equal(nextItem, session.ContinueAfterPlaybackEnded(item));
    Equal(nextItem, session.CurrentItem);
    Equal(true, session.IsPlaying);
    Equal(nextItem, output.LastItem);
    Equal(TimeSpan.FromMinutes(17), output.Position);
    True(session.ContinueAfterPlaybackEnded(nextItem) is null, "Ostatni plik nie powinien zapętlać listy.");
    Equal(false, session.IsPlaying);
    Equal(TimeSpan.Zero, session.Position);

    var natural = new MediaItem { Id = "natural", Title = "Naturalny następny", Source = @"C:\Muzyka\naturalny.mp3" };
    var queued = new MediaItem { Id = "queued", Title = "Z kolejki", Source = @"C:\Muzyka\kolejka.mp3", IsInQueue = true };
    var playNext = new MediaItem { Id = "play-next", Title = "Jako następny", Source = @"C:\Muzyka\jako-następny.mp3", IsPlayNext = true };
    var prioritySession = new DemoMediaSession(
        "priority",
        "Priorytety",
        [item, natural, queued, playNext],
        output);
    True(prioritySession.Play(item), "Test priorytetów powinien rozpocząć pierwszy element.");
    Equal(playNext, prioritySession.ContinueAfterPlaybackEnded(item));
    Equal(queued, prioritySession.ContinueAfterPlaybackEnded(playNext));
    Equal(natural, prioritySession.ContinueAfterPlaybackEnded(queued));
    True(prioritySession.ContinueAfterPlaybackEnded(natural) is null, "Po powrocie do naturalnej listy wykorzystana kolejka nie powinna zagrać drugi raz.");
    True(!playNext.IsPlayNext && !queued.IsInQueue, "Wykorzystane stany kolejki powinny zostać wyczyszczone.");

    var detached = manager.RemoveTransientSession("local");
    True(detached is not null, "Pusta lokalna sesja powinna dać się odłączyć od menedżera.");
    True(manager.FindSession("local") is null, "Odłączona sesja nie może pozostać na liście.");
    Equal(session, manager.RestoreTransientSession(detached!, makeCurrent: true));
    Equal(session, manager.Current);
}

static void TestEmptyLocalSession()
{
    var output = new FakeMediaOutput();
    var manager = new SessionManager(new AppSettings());
    var (session, slot) = manager.AddOrUpdateTransientSession(
        "local",
        "Pliki lokalne",
        [],
        output,
        1);

    Equal(1, slot);
    Equal(session, manager.SelectSlot(1));
    Equal(false, session.HasItems);
    Equal("Brak elementów w sesji Pliki lokalne", session.CurrentItem.Title);
    session.TogglePlayback();
    session.Move(1);
    session.Seek(TimeSpan.FromSeconds(10));
    Equal(0, output.PlayCount);
    Equal(false, session.IsPlaying);

    var item = new MediaItem
    {
        Id = "local-after-empty",
        Title = "Dodany po uruchomieniu",
        Source = @"C:\Muzyka\dodany.mp3"
    };
    session.AddItems([item]);
    Equal(true, session.HasItems);
    Equal(item, session.CurrentItem);
    True(session.Activate(item), "Plik dodany do pustej sesji powinien dać się odtworzyć.");
    Equal(1, output.PlayCount);
    session.ReplaceItems([]);
    Equal(false, session.HasItems);
    Equal(false, session.IsPlaying);
    Equal(1, output.StopCount);
    session.ReplaceItems([item]);
    Equal(true, session.HasItems);
    Equal(item, session.CurrentItem);
}

static void TestPlaybackContext()
{
    var output = new FakeMediaOutput();
    var first = new MediaItem { Id = "first", Title = "Pierwszy" };
    var second = new MediaItem { Id = "second", Title = "Drugi" };
    var third = new MediaItem { Id = "third", Title = "Trzeci" };
    var fourth = new MediaItem { Id = "fourth", Title = "Czwarty" };
    var rateOverrides = new Dictionary<string, double?>
    {
        [third.Id] = 1.50d
    };
    var session = new DemoMediaSession(
        "context",
        "Kontekst",
        [first, second, third, fourth],
        output,
        playbackRateOverride: item => rateOverrides.GetValueOrDefault(item.Id));
    session.SetDefaultPlaybackRate(1.25d);
    session.SetPlaybackContext([second.Id, fourth.Id]);

    True(session.Play(second), "Element kontekstu powinien się uruchomić.");
    Equal(1.25d, session.PlaybackRate);
    True(session.PlayRelative(1), "Page Down powinien użyć kolejności bieżącego kontekstu.");
    Equal(fourth, session.CurrentItem);
    True(!session.PlayRelative(1), "Kontekst nie może przejść do elementu spoza listy.");
    True(session.PlayRelative(-1), "Page Up powinien wrócić w tym samym kontekście.");
    Equal(second, session.CurrentItem);
    Equal(fourth, session.ContinueAfterPlaybackEnded(second));
    True(session.ContinueAfterPlaybackEnded(fourth) is null,
        "Automatyczna kontynuacja powinna zakończyć się wraz z kontekstem.");

    second.IsInQueue = true;
    session.SetPlaybackContext([first.Id, third.Id, fourth.Id]);
    True(session.Play(first), "Pierwszy element nowego kontekstu powinien się uruchomić.");
    Equal(second, session.ContinueAfterPlaybackEnded(first));
    Equal(third, session.ContinueAfterPlaybackEnded(second));
    Equal(1.50d, session.PlaybackRate);
    Equal(fourth, session.ContinueAfterPlaybackEnded(third));
    Equal(1.25d, session.PlaybackRate);
}

static void TestPlaybackRateDefaultPersistence()
{
    var output = new FakeMediaOutput();
    var episode = new MediaItem
    {
        Id = "podcast-episode",
        Title = "Odcinek",
        Kind = MediaItemKind.Episode
    };
    var session = new DemoMediaSession(
        "podcasts",
        "Podcasty",
        [episode],
        output,
        rememberPosition: _ => true);

    True(session.Play(episode), "Odcinek powinien rozpocząć odtwarzanie.");
    True(session.ChangePlaybackRate(1), "Podcast powinien pozwalać na zmianę prędkości.");
    Equal(1.25d, session.PlaybackRate);

    // To samo robi warstwa okna po zmianie prędkości Podcastów: zapisuje
    // wybraną wartość i ustawia ją jako domyślną dla następnego otwarcia.
    session.SetDefaultPlaybackRate(session.PlaybackRate);
    session.StopPlayback();
    True(session.Play(episode), "Odcinek powinien dać się ponownie otworzyć po Escape.");
    Equal(1.25d, session.PlaybackRate);
    Equal(1.25d, output.PlaybackRate);
}

static void TestQueueOrder()
{
    var output = new FakeMediaOutput();
    var start = new MediaItem { Id = "start", Title = "Początek" };
    var firstQueued = new MediaItem { Id = "queue-1", Title = "Kolejka pierwsza", IsInQueue = true };
    var secondQueued = new MediaItem { Id = "queue-2", Title = "Kolejka druga", IsInQueue = true };
    var firstNext = new MediaItem { Id = "next-1", Title = "Następny pierwszy", IsPlayNext = true };
    var secondNext = new MediaItem { Id = "next-2", Title = "Następny drugi", IsPlayNext = true };
    var session = new DemoMediaSession(
        "queue-order",
        "Kolejność kolejki",
        [start, firstQueued, secondQueued, firstNext, secondNext],
        output);
    session.SetQueueOrder([secondQueued.Id, "missing", secondNext.Id, firstQueued.Id, firstNext.Id]);
    True(session.QueueItemIds.SequenceEqual(
            [secondQueued.Id, secondNext.Id, firstQueued.Id, firstNext.Id]),
        "Kolejność powinna odrzucić brakujące identyfikatory i zachować zapisane pozycje.");

    True(session.Play(start), "Test kolejki powinien rozpocząć element źródłowy.");
    Equal(secondNext, session.ContinueAfterPlaybackEnded(start));
    Equal(firstNext, session.ContinueAfterPlaybackEnded(secondNext));
    Equal(secondQueued, session.ContinueAfterPlaybackEnded(firstNext));
    Equal(firstQueued, session.ContinueAfterPlaybackEnded(secondQueued));
    True(session.ContinueAfterPlaybackEnded(firstQueued) is null,
        "Po wykorzystaniu uporządkowanej kolejki odtwarzanie nie może powtarzać jej elementów.");
    Equal(0, session.QueueItemIds.Count);
}

static void TestQueuePlaybackNavigation()
{
    var output = new FakeMediaOutput();
    var first = new MediaItem { Id = "queue-a", Title = "Kolejka A", IsPlayNext = true };
    var second = new MediaItem { Id = "queue-b", Title = "Kolejka B", IsInQueue = true };
    var third = new MediaItem { Id = "queue-c", Title = "Kolejka C", IsInQueue = true };
    var explicitQueue = new DemoMediaSession(
        "explicit-queue",
        "Jawna Kolejka",
        [first, second, third],
        output);
    explicitQueue.SetQueueOrder([first.Id, second.Id, third.Id]);
    explicitQueue.SetPlaybackContext([first.Id, second.Id, third.Id], isQueueContext: true);

    True(explicitQueue.Play(first), "Pierwsza pozycja jawnej Kolejki powinna się uruchomić.");
    True(first.IsPlayNext,
        "Bieżący element powinien pozostać widoczny w Kolejce do zakończenia albo przejścia dalej.");
    True(explicitQueue.QueueNavigationActive, "Odtwarzacz powinien pamiętać aktywny kontekst Kolejki.");
    True(explicitQueue.PlayRelative(1), "Page Down powinien przejść do następnej pozycji Kolejki.");
    Equal(second, explicitQueue.CurrentItem);
    True(!first.IsPlayNext && second.IsInQueue,
        "Dopiero opuszczony element powinien zniknąć, a bieżący pozostać w Kolejce.");
    True(explicitQueue.PlayRelative(-1), "Page Up powinien wrócić do poprzedniej pozycji tej samej Kolejki.");
    Equal(first, explicitQueue.CurrentItem);
    True(explicitQueue.PlayRelative(1), "Ponowny Page Down powinien wrócić do drugiej pozycji.");
    Equal(second, explicitQueue.CurrentItem);
    Equal(third, explicitQueue.ContinueAfterPlaybackEnded(second));
    True(explicitQueue.ContinueAfterPlaybackEnded(third) is null,
        "Jawna Kolejka nie może po wyczerpaniu odtworzyć zużytej pozycji ponownie.");
    True(explicitQueue.PlayRelative(-1),
        "Po dojściu do końca Page Up powinien nadal pozwolić wrócić w historii Kolejki.");
    Equal(second, explicitQueue.CurrentItem);

    var source = new MediaItem { Id = "source", Title = "Źródło" };
    var natural = new MediaItem { Id = "natural", Title = "Dalszy element Biblioteki" };
    var next = new MediaItem { Id = "next", Title = "Następny", IsPlayNext = true };
    var queued = new MediaItem { Id = "queued", Title = "Zwykła Kolejka", IsInQueue = true };
    var diversion = new DemoMediaSession(
        "queue-diversion",
        "Wejście automatyczne",
        [source, natural, next, queued],
        output);
    diversion.SetPlaybackContext([source.Id, next.Id, queued.Id, natural.Id]);
    diversion.SetQueueOrder([queued.Id, next.Id]);
    True(diversion.Play(source), "Źródłowy plik powinien się uruchomić.");
    Equal(next, diversion.ContinueAfterPlaybackEnded(source));
    True(diversion.PlayRelative(1),
        "Page Down po automatycznym wejściu do Kolejki powinien wybrać jej kolejną pozycję.");
    Equal(queued, diversion.CurrentItem);
    True(diversion.PlayRelative(-1),
        "Page Up po automatycznym wejściu powinien wrócić w Kolejce, a nie w Bibliotece.");
    Equal(next, diversion.CurrentItem);
    Equal(natural, diversion.ContinueAfterPlaybackEnded(next));

    var adopted = new MediaItem { Id = "adopted", Title = "Już odtwarzany", IsPlayNext = true };
    var adoptedLater = new MediaItem { Id = "adopted-later", Title = "Później", IsInQueue = true };
    var adoption = new DemoMediaSession(
        "queue-adoption",
        "Przejęcie bieżącego",
        [adopted, adoptedLater],
        output);
    True(adoption.Play(adopted), "Element powinien najpierw grać poza kontekstem Kolejki.");
    True(adopted.IsPlayNext, "Samo odtworzenie poza widokiem Kolejki nie powinno zmienić przynależności.");
    adoption.SetPlaybackContext([adopted.Id, adoptedLater.Id], isQueueContext: true);
    True(adopted.IsPlayNext && adoption.QueueNavigationActive,
        "Otwarcie już odtwarzanego elementu z Kolejki nie powinno przedwcześnie usuwać go z listy.");
}

static void TestMissingCurrentItemRecovery()
{
    var output = new FakeMediaOutput();
    var unrelated = new MediaItem { Id = "unrelated", Title = "Pierwszy w całej Bibliotece" };
    var first = new MediaItem { Id = "first", Title = "Bieżący" };
    var second = new MediaItem { Id = "second", Title = "Następny z folderu" };
    var folderSession = new DemoMediaSession(
        "folder-recovery",
        "Folder",
        [unrelated, first, second],
        output);
    folderSession.SetPlaybackContext([first.Id, second.Id]);
    True(folderSession.Play(first), "Bieżący plik folderu powinien się uruchomić.");

    var folderResult = folderSession.ReplaceItems([unrelated, second]);
    True(folderResult.CurrentItemRemoved, "Odświeżenie powinno rozpoznać zniknięcie bieżącego pliku.");
    Equal(second, folderResult.SelectedSuccessor);
    Equal(second, folderSession.CurrentItem);
    Equal(false, folderSession.IsPlaying);
    folderSession.TogglePlayback();
    Equal(second, output.LastItem);

    var queuedFirst = new MediaItem { Id = "queue-first", Title = "Pierwszy z Kolejki", IsInQueue = true };
    var queuedSecond = new MediaItem { Id = "queue-second", Title = "Drugi z Kolejki", IsInQueue = true };
    var queueSession = new DemoMediaSession(
        "queue-recovery",
        "Kolejka",
        [unrelated, queuedFirst, queuedSecond],
        output);
    queueSession.SetQueueOrder([queuedFirst.Id, queuedSecond.Id]);
    queueSession.SetPlaybackContext([queuedFirst.Id, queuedSecond.Id], isQueueContext: true);
    True(queueSession.Play(queuedFirst), "Pierwszy element Kolejki powinien się uruchomić.");

    var queueResult = queueSession.ReplaceItems([unrelated, queuedSecond]);
    Equal(queuedSecond, queueResult.SelectedSuccessor);
    Equal(queuedSecond, queueSession.CurrentItem);
    Equal(false, queueSession.IsPlaying);
    Equal(true, queuedSecond.IsInQueue);
    queueSession.TogglePlayback();
    Equal(queuedSecond, output.LastItem);
    Equal(true, queuedSecond.IsInQueue);

    var onlyQueued = new MediaItem { Id = "queue-only", Title = "Jedyny z Kolejki", IsInQueue = true };
    var exhaustedQueue = new DemoMediaSession(
        "queue-exhausted",
        "Pusta Kolejka",
        [unrelated, onlyQueued],
        output);
    exhaustedQueue.SetPlaybackContext([onlyQueued.Id], isQueueContext: true);
    True(exhaustedQueue.Play(onlyQueued), "Jedyny element Kolejki powinien się uruchomić.");

    var exhaustedResult = exhaustedQueue.ReplaceItems([unrelated]);
    True(exhaustedResult.CurrentItemRemoved, "Usunięcie jedynego elementu powinno zostać rozpoznane.");
    Equal<MediaItem?>(null, exhaustedResult.SelectedSuccessor);
    Equal(false, exhaustedQueue.HasCurrentItem);
    exhaustedQueue.TogglePlayback();
    Equal(false, exhaustedQueue.IsPlaying);

    var source = new MediaItem { Id = "source", Title = "Źródło" };
    var queued = new MediaItem { Id = "diverted", Title = "Pozycja z Kolejki", IsPlayNext = true };
    var natural = new MediaItem { Id = "natural", Title = "Dalszy plik folderu" };
    var divertedSession = new DemoMediaSession(
        "diversion-recovery",
        "Powrót z Kolejki",
        [source, queued, natural],
        output);
    divertedSession.SetPlaybackContext([source.Id, natural.Id]);
    True(divertedSession.Play(source), "Plik źródłowy powinien się uruchomić.");
    Equal(queued, divertedSession.ContinueAfterPlaybackEnded(source));

    var diversionResult = divertedSession.ReplaceItems([source, natural]);
    Equal(natural, diversionResult.SelectedSuccessor);
    Equal(natural, divertedSession.CurrentItem);
    Equal(false, divertedSession.QueueNavigationActive);

    var deletedFirst = new MediaItem { Id = "deleted-first", Title = "Usuwany z widoku" };
    var deletedNext = new MediaItem { Id = "deleted-next", Title = "Następny z tego samego widoku" };
    var deletionSession = new DemoMediaSession(
        "deletion-recovery",
        "Dowolny widok",
        [unrelated, deletedFirst, deletedNext],
        output);
    deletionSession.SetPlaybackContext([deletedFirst.Id, deletedNext.Id]);
    True(deletionSession.Play(deletedFirst), "Usuwany element powinien się uruchomić.");
    Equal(1, deletionSession.RemoveItems([deletedFirst.Id]).Count);
    Equal(deletedNext, deletionSession.CurrentItem);
    Equal(false, deletionSession.IsPlaying);
    deletionSession.TogglePlayback();
    Equal(deletedNext, output.LastItem);

    var deletedOnly = new MediaItem { Id = "deleted-only", Title = "Jedyny w widoku" };
    var exhaustedDeletion = new DemoMediaSession(
        "deletion-exhausted",
        "Widok bez następcy",
        [unrelated, deletedOnly],
        output);
    exhaustedDeletion.SetPlaybackContext([deletedOnly.Id]);
    True(exhaustedDeletion.Play(deletedOnly), "Jedyny element widoku powinien się uruchomić.");
    Equal(1, exhaustedDeletion.RemoveItems([deletedOnly.Id]).Count);
    Equal(false, exhaustedDeletion.HasCurrentItem);
    exhaustedDeletion.TogglePlayback();
    Equal(false, exhaustedDeletion.IsPlaying);
}

static void TestResumePositionPolicy()
{
    var output = new FakeMediaOutput();
    var music = new MediaItem
    {
        Id = "music",
        Title = "Muzyka od początku",
        Source = @"C:\Muzyka\utwor.mp3"
    };
    var podcast = new MediaItem
    {
        Id = "podcast",
        Title = "Podcast ze wznowieniem",
        Source = @"C:\Podcasty\odcinek.mp3"
    };
    var session = new DemoMediaSession(
        "local",
        "Pliki lokalne",
        [music, podcast],
        output,
        item => string.Equals(item.Id, podcast.Id, StringComparison.Ordinal));

    True(session.Play(music), "Plik muzyczny powinien się uruchomić.");
    output.Position = TimeSpan.FromSeconds(25);
    session.TogglePlayback();
    Equal(false, session.IsPlaying);
    session.TogglePlayback();
    Equal(TimeSpan.FromSeconds(25), output.Position);
    True(!session.RememberedPositions.ContainsKey(music.Id),
        "Pauza ma zachować bieżące miejsce tylko w sesji, bez trwałego zapisu muzyki.");

    True(session.Play(podcast), "Podcast powinien dać się wybrać.");
    session.SetPosition(TimeSpan.FromMinutes(12));
    True(session.Play(music), "Powrót do muzyki powinien być możliwy.");
    Equal(TimeSpan.Zero, session.Position);
    True(session.Play(podcast), "Powrót do podcastu powinien być możliwy.");
    Equal(TimeSpan.FromMinutes(12), session.Position);
    Equal(TimeSpan.FromMinutes(12), session.RememberedPositions[podcast.Id]);
}

static void TestConcurrentConfigurationSaves()
{
    var directory = Path.Combine(
        Path.GetTempPath(),
        $"amc-concurrent-state-tests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        var statePath = Path.Combine(directory, "state.json");
        var databasePath = Path.Combine(directory, "library.db");
        var store = new ConfigurationStore(statePath, databasePath);
        var tasks = Enumerable.Range(0, 16)
            .Select(index => Task.Run(() =>
            {
                var state = ConfigurationStore.CreateDefaultState();
                state.Settings.PrefixTimeoutMilliseconds = 2_000 + index;
                state.LocalMedia.Volume = 10 + index;
                store.Save(state);
            }))
            .ToArray();
        Task.WaitAll(tasks);

        var loaded = new ConfigurationStore(statePath, databasePath).LoadOrCreate();
        Equal(
            loaded.Settings.PrefixTimeoutMilliseconds - 1_990,
            loaded.LocalMedia.Volume);
    }
    finally
    {
        Directory.Delete(directory, true);
    }
}

static void TestLocalAudioFileDiscovery()
{
    var directory = Path.Combine(Path.GetTempPath(), $"amc-local-folder-tests-{Guid.NewGuid():N}");
    var nested = Path.Combine(directory, "Z Album 2");
    Directory.CreateDirectory(nested);
    try
    {
        File.WriteAllText(Path.Combine(directory, "Utwór 10.mp3"), string.Empty);
        File.WriteAllText(Path.Combine(directory, "Utwór 2.FLAC"), string.Empty);
        File.WriteAllText(Path.Combine(directory, "okładka.jpg"), string.Empty);
        File.WriteAllText(Path.Combine(nested, "01 Intro.opus"), string.Empty);

        var files = LocalAudioFileDiscovery.FindFiles(directory);
        Equal(3, files.Count);
        Equal("Utwór 2.FLAC", Path.GetFileName(files[0]));
        Equal("Utwór 10.mp3", Path.GetFileName(files[1]));
        Equal("01 Intro.opus", Path.GetFileName(files[2]));
        var lockedPath = Path.Combine(directory, "Zablokowany.mp3");
        File.WriteAllBytes(lockedPath, [1, 2, 3]);
        using (File.Open(lockedPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var indexedWithoutOpeningPayload = LocalAudioFileDiscovery.FindFiles(directory);
            True(
                indexedWithoutOpeningPayload.Contains(lockedPath, StringComparer.OrdinalIgnoreCase),
                "Indeksowanie folderu nie może wymagać otwarcia danych pliku.");
        }
        Equal(CloudFileState.Local, CloudFileAvailability.GetState(lockedPath));
        True(
            CloudFileAvailability.ClassifyMetadata(FileAttributes.Normal) == CloudFileState.Local,
            "Brak stanu Cloud Files nie może zostać pomylony z placeholderem.");
        True(
            CloudFileAvailability.ClassifyMetadata(FileAttributes.Normal, 0x10) == CloudFileState.Placeholder,
            "Częściowy placeholder Cloud Files powinien zostać wykryty niezależnie od dostawcy.");
        var placeholderPath = Path.Combine(directory, "Tylko online.mp3");
        File.WriteAllBytes(placeholderPath, [1]);
        try
        {
            File.SetAttributes(
                placeholderPath,
                File.GetAttributes(placeholderPath) | FileAttributes.Offline);
            Equal(CloudFileState.Placeholder, CloudFileAvailability.GetState(placeholderPath));
            True(
                CloudFileAvailability.RequiresHydration(placeholderPath),
                "Plik oznaczony jako Offline powinien zostać rozpoznany bez otwierania zawartości.");
        }
        finally
        {
            File.SetAttributes(placeholderPath, FileAttributes.Normal);
        }
        Equal(
            CloudFileState.Unavailable,
            CloudFileAvailability.GetState(Path.Combine(directory, "brak.mp3")));
        True(
            CloudFileAvailability.MayRequireRemoteAccess(
                @"G:\Dyski współdzielone\Archiwum M\Radio Centrum\audycja.mp3"),
            "Strumieniowany Dysk Google powinien być rozpoznany bez otwierania pliku.");
        True(
            CloudFileAvailability.MayRequireRemoteAccess(
                @"D:\iCloudDrive\iCloud~co~example\nagranie.mp3"),
            "iCloud powinien być rozpoznany na podstawie bezpiecznej ścieżki.");
        var hydratedICloudDirectory = Path.Combine(directory, "iCloudDrive", "iCloud~co~example");
        Directory.CreateDirectory(hydratedICloudDirectory);
        var hydratedICloudFile = Path.Combine(hydratedICloudDirectory, "pobrane.mp3");
        File.WriteAllBytes(hydratedICloudFile, [1, 2, 3]);
        True(
            !CloudFileAvailability.MayRequireRemoteAccess(hydratedICloudFile),
            "Pobrany lokalnie plik iCloud nie powinien otrzymać limitów dla pliku zdalnego.");
        Equal(
            CloudFileState.Local,
            CloudFileAvailability.ClassifyMetadata((FileAttributes)0x00080420));
        True(
            CloudFileAvailability.MayRequireRemoteAccess(
                @"C:\Users\Test\OneDrive - Firma\nagranie.mp3"),
            "OneDrive powinien być rozpoznany na podstawie bezpiecznej ścieżki.");
        True(
            CloudFileAvailability.MayRequireRemoteAccess(
                @"X:\Box Drive\Archiwum\nagranie.mp3"),
            "Box Drive powinien być rozpoznany na podstawie bezpiecznej ścieżki.");
        True(
            CloudFileAvailability.MayRequireRemoteAccess(
                @"P:\Nextcloud\Archiwum\nagranie.mp3"),
            "Nextcloud powinien być rozpoznany na podstawie bezpiecznej ścieżki.");
        True(
            CloudFileAvailability.MayRequireRemoteAccess(
                @"\\serwer\udzial\Archiwum\nagranie.mp3"),
            "Udział sieciowy powinien otrzymać bezpieczne limity dostępu zdalnego.");
        True(
            !CloudFileAvailability.MayRequireRemoteAccess(lockedPath),
            "Zwykły lokalny plik nie powinien być uznany za chmurowy.");
        Equal(
            MediaSourceAccessKind.LocalFile,
            MediaSourceAccessPolicy.Classify(lockedPath).Kind);
        Equal(
            MediaSourceAccessKind.RemoteFile,
            MediaSourceAccessPolicy.Classify(
                @"G:\Dyski współdzielone\Archiwum M\Radio Centrum\audycja.mp3").Kind);
        var streamPolicy = MediaSourceAccessPolicy.Classify(
            "https://radio.example.invalid/live.mp3");
        Equal(MediaSourceAccessKind.NetworkStream, streamPolicy.Kind);
        True(
            streamPolicy.RequiresRemoteAccess && streamPolicy.RequiresBackgroundIo,
            "Strumień i przyszłe pobieranie z adresu sieciowego muszą używać wspólnej polityki pracy w tle.");
        Equal(
            MediaSourceAccessKind.NetworkStream,
            MediaSourceAccessPolicy.Classify("spotify:track:test").Kind);
        True(
            MediaSourceAccessPolicy.Classify(lockedPath).RequiresBackgroundIo,
            "Także odczyt zwykłego pliku musi pozostać poza wątkiem interfejsu.");
        True(LocalAudioFileDiscovery.IsAudioFile("nagranie.aiff"), "AIFF powinien być rozpoznawany.");
        True(LocalAudioFileDiscovery.IsAudioFile("film.mp4"), "MP4 powinien trafić do lokalnych multimediów.");
        True(LocalAudioFileDiscovery.IsAudioFile("film.mkv"), "MKV powinien trafić do lokalnych multimediów.");
        True(LocalAudioFileDiscovery.IsAudioFile("film.m2ts"), "M2TS powinien trafić do lokalnych multimediów.");
        True(LocalAudioFileDiscovery.IsVideoFile("film.mp4"), "MP4 powinien być oznaczony jako kontener wideo.");
        True(!LocalAudioFileDiscovery.IsVideoFile("nagranie.m4a"), "M4A nie jest kontenerem wideo.");
        True(LocalAudioFileDiscovery.IsRecoverablePartialFile("nagranie.PART"), "PART powinien być dostępny do ręcznego odzyskania.");
        True(LocalAudioFileDiscovery.IsRecoverablePartialFile("nagranie.mp3.amc-partial"), "AMC-PARTIAL powinien być dostępny do ręcznego odzyskania.");
        True(!LocalAudioFileDiscovery.IsAudioFile("nagranie.part"), "Aktywne PART nie może automatycznie trafiać do Biblioteki.");
        True(LocalAudioFileDiscovery.DialogFilter.Contains("*.amc-partial", StringComparison.Ordinal), "Okno Otwórz nie udostępnia niedokończonych nagrań.");
        True(!LocalAudioFileDiscovery.IsAudioFile("okładka.jpg"), "Obraz nie może trafić na listę audio.");
        Equal(320, LocalAudioFileDiscovery.EstimateBitrateKbps(4_000_000, TimeSpan.FromSeconds(100)));
        True(LocalAudioFileDiscovery.EstimateBitrateKbps(0, TimeSpan.FromSeconds(100)) is null, "Pusty plik nie ma wiarygodnej przepływności.");
    }
    finally
    {
        Directory.Delete(directory, true);
    }
}

static void TestMp3StructureProbe()
{
    var directory = Path.Combine(
        Path.GetTempPath(),
        $"amc-mp3-probe-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        const int frameLength = 417;
        byte[] frameHeader = [0xFF, 0xFB, 0x90, 0x00];

        var plainPath = Path.Combine(directory, "plain.mp3");
        var plain = new byte[frameLength * 2];
        frameHeader.CopyTo(plain, 0);
        frameHeader.CopyTo(plain, frameLength);
        File.WriteAllBytes(plainPath, plain);

        var plainResult = Mp3StructureProbe.Probe(plainPath);
        True(plainResult.HasConsecutiveFrames, "Nie rozpoznano dwóch kolejnych ramek MP3.");
        Equal(0L, plainResult.AudioStartOffset);
        Equal(44_100, plainResult.SampleRateHz);
        Equal(128, plainResult.BitrateKbps);

        var id3Path = Path.Combine(directory, "id3.mp3");
        var withId3 = new byte[30 + frameLength * 2];
        withId3[0] = (byte)'I';
        withId3[1] = (byte)'D';
        withId3[2] = (byte)'3';
        withId3[3] = 4;
        withId3[9] = 20;
        frameHeader.CopyTo(withId3, 30);
        frameHeader.CopyTo(withId3, 30 + frameLength);
        File.WriteAllBytes(id3Path, withId3);

        var id3Result = Mp3StructureProbe.Probe(id3Path);
        True(id3Result.HasConsecutiveFrames, "Nie pominięto prawidłowego znacznika ID3.");
        Equal(30L, id3Result.DeclaredAudioStartOffset);
        Equal(30L, id3Result.AudioStartOffset);

        var leadingJunkPath = Path.Combine(directory, "leading-junk.mp3");
        var withLeadingJunk = new byte[37 + frameLength * 2];
        Array.Fill<byte>(withLeadingJunk, 0x55, 0, 37);
        frameHeader.CopyTo(withLeadingJunk, 37);
        frameHeader.CopyTo(withLeadingJunk, 37 + frameLength);
        File.WriteAllBytes(leadingJunkPath, withLeadingJunk);
        var leadingJunkResult = Mp3StructureProbe.Probe(leadingJunkPath);
        True(leadingJunkResult.HasConsecutiveFrames, "Nie odnaleziono MP3 po danych poprzedzających audio.");
        Equal(37L, leadingJunkResult.AudioStartOffset);
        True(leadingJunkResult.ShouldUseSanitizedStream, "Nietypowy początek powinien uruchamiać oczyszczony strumień.");

        var invalidVersionPath = Path.Combine(directory, "invalid-id3-version.mp3");
        var invalidVersion = new byte[10 + frameLength * 2];
        invalidVersion[0] = (byte)'I';
        invalidVersion[1] = (byte)'D';
        invalidVersion[2] = (byte)'3';
        invalidVersion[3] = 99;
        frameHeader.CopyTo(invalidVersion, 10);
        frameHeader.CopyTo(invalidVersion, 10 + frameLength);
        File.WriteAllBytes(invalidVersionPath, invalidVersion);
        var invalidVersionResult = Mp3StructureProbe.Probe(invalidVersionPath);
        True(invalidVersionResult.HasConsecutiveFrames, "Nie odzyskano audio po nieprawidłowej wersji ID3.");
        Equal(10L, invalidVersionResult.AudioStartOffset);
        True(!string.IsNullOrWhiteSpace(invalidVersionResult.Warning), "Brakuje ostrzeżenia o wersji ID3.");

        var freeFormatPath = Path.Combine(directory, "free-format.mp3");
        byte[] freeFormatHeader = [0xFF, 0xFB, 0x00, 0x00];
        var freeFormat = new byte[frameLength * 3];
        freeFormatHeader.CopyTo(freeFormat, 0);
        freeFormatHeader.CopyTo(freeFormat, frameLength);
        freeFormatHeader.CopyTo(freeFormat, frameLength * 2);
        File.WriteAllBytes(freeFormatPath, freeFormat);
        var freeFormatResult = Mp3StructureProbe.Probe(freeFormatPath);
        True(freeFormatResult.HasConsecutiveFrames, "Nie rozpoznano trzech ramek MP3 free-format.");
        Equal(128, freeFormatResult.BitrateKbps);

        const int largeId3Payload = 20 * 1024 * 1024;
        var largeTagPath = Path.Combine(directory, "large-tag.mp3");
        using (var largeTag = new FileStream(largeTagPath, FileMode.CreateNew, FileAccess.Write))
        {
            Span<byte> header = stackalloc byte[10];
            "ID3"u8.CopyTo(header);
            header[3] = 4;
            header[6] = (byte)((largeId3Payload >> 21) & 0x7F);
            header[7] = (byte)((largeId3Payload >> 14) & 0x7F);
            header[8] = (byte)((largeId3Payload >> 7) & 0x7F);
            header[9] = (byte)(largeId3Payload & 0x7F);
            largeTag.Write(header);
            largeTag.Position = 10L + largeId3Payload;
            largeTag.Write(frameHeader);
            largeTag.Position += frameLength - frameHeader.Length;
            largeTag.Write(frameHeader);
            largeTag.SetLength(10L + largeId3Payload + frameLength * 2L);
        }
        var largeTagResult = Mp3StructureProbe.Probe(largeTagPath);
        True(largeTagResult.HasConsecutiveFrames, "Nie odczytano ramek za dużym, ale prawidłowym ID3.");
        True(largeTagResult.HasLargeLeadingTag, "Duży ID3 nie został oznaczony do bezpiecznego pominięcia.");

        var damagedPath = Path.Combine(directory, "damaged.mp3");
        File.WriteAllBytes(
            damagedPath,
            [(byte)'I', (byte)'D', (byte)'3', 4, 0, 0, 0, 0, 1, 0, 1, 2, 3]);
        var damagedResult = Mp3StructureProbe.Probe(damagedPath);
        True(!damagedResult.HasConsecutiveFrames, "Uszkodzony znacznik ID3 uznano za dźwięk.");
        True(!string.IsNullOrWhiteSpace(damagedResult.Warning), "Brakuje bezpiecznej diagnozy uszkodzenia.");

        var junkPath = Path.Combine(directory, "junk.mp3");
        File.WriteAllBytes(junkPath, [1, 2, 3, 4, 5, 6, 7, 8]);
        True(
            !Mp3StructureProbe.Probe(junkPath).HasConsecutiveFrames,
            "Losowe dane uznano za MP3.");
    }
    finally
    {
        Directory.Delete(directory, true);
    }
}

static void TestBoundedSubrangeStream()
{
    const long sourceLength = 8L * 1024 * 1024 * 1024;
    const long origin = 5L * 1024 * 1024 * 1024 + 17;
    using var source = new VirtualLargeReadStream(sourceLength);
    using var window = new BoundedSubrangeStream(source, origin, 1024 * 1024);
    Equal(1024L * 1024, window.Length);

    Span<byte> first = stackalloc byte[8];
    Equal(first.Length, window.Read(first));
    for (var index = 0; index < first.Length; index++)
    {
        Equal((byte)((origin + index) & 0xFF), first[index]);
    }

    Equal(window.Length - 4, window.Seek(-4, SeekOrigin.End));
    Span<byte> ending = stackalloc byte[16];
    Equal(4, window.Read(ending));
    Equal(window.Length, window.Position);
}

static void TestMediaContainerProbe()
{
    var directory = Path.Combine(
        Path.GetTempPath(),
        $"amc-container-probe-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        void Verify(string fileName, byte[] data, MediaContainerKind expected)
        {
            var path = Path.Combine(directory, fileName);
            File.WriteAllBytes(path, data);
            var result = MediaContainerProbe.Probe(path);
            Equal(expected, result.Kind);
            True(result.HeaderRecognized, $"Nie rozpoznano kontenera {expected}.");
        }

        Verify("wave.wav", "RIFF\0\0\0\0WAVEfmt "u8.ToArray(), MediaContainerKind.Wave);
        Verify("broadcast.wav", "BW64\0\0\0\0WAVEds64"u8.ToArray(), MediaContainerKind.Wave);
        Verify("audio.aiff", "FORM\0\0\0\0AIFFCOMM"u8.ToArray(), MediaContainerKind.Aiff);
        Verify("audio.flac", "fLaC\0\0\0\0"u8.ToArray(), MediaContainerKind.Flac);

        var vorbis = new byte[64];
        "OggS"u8.CopyTo(vorbis);
        "\x01vorbis"u8.CopyTo(vorbis.AsSpan(32));
        Verify("audio.ogg", vorbis, MediaContainerKind.OggVorbis);

        var opus = new byte[64];
        "OggS"u8.CopyTo(opus);
        "OpusHead"u8.CopyTo(opus.AsSpan(32));
        Verify("audio.opus", opus, MediaContainerKind.OggOpus);

        var mp4 = new byte[24];
        mp4[3] = 24;
        "ftypM4A "u8.CopyTo(mp4.AsSpan(4));
        Verify("audio.m4a", mp4, MediaContainerKind.Mp4);
        Verify(
            "audio.wma",
            [0x30, 0x26, 0xB2, 0x75, 0x8E, 0x66, 0xCF, 0x11, 0xA6, 0xD9, 0x00, 0xAA, 0x00, 0x62, 0xCE, 0x6C],
            MediaContainerKind.Asf);
        Verify("audio.webm", [0x1A, 0x45, 0xDF, 0xA3, 0, 0, 0, 0], MediaContainerKind.Matroska);
        Verify("audio.aac", [0xFF, 0xF1, 0x50, 0x80, 0, 0, 0, 0], MediaContainerKind.AdtsAac);

        var taggedFlac = new byte[34];
        taggedFlac[0] = (byte)'I';
        taggedFlac[1] = (byte)'D';
        taggedFlac[2] = (byte)'3';
        taggedFlac[3] = 4;
        taggedFlac[9] = 20;
        "fLaC"u8.CopyTo(taggedFlac.AsSpan(30));
        var taggedPath = Path.Combine(directory, "tagged.flac");
        File.WriteAllBytes(taggedPath, taggedFlac);
        var taggedResult = MediaContainerProbe.Probe(taggedPath);
        Equal(MediaContainerKind.Flac, taggedResult.Kind);
        Equal(30L, taggedResult.ContentOffset);

        var mismatchPath = Path.Combine(directory, "wrong.wav");
        File.WriteAllBytes(mismatchPath, "fLaC\0\0\0\0"u8.ToArray());
        var mismatch = MediaContainerProbe.Probe(mismatchPath);
        Equal(MediaContainerKind.Flac, mismatch.Kind);
        True(!string.IsNullOrWhiteSpace(mismatch.Warning), "Nie wykryto niezgodnego rozszerzenia.");

        var oversizedRiffPath = Path.Combine(directory, "oversized.wav");
        var oversizedRiff = "RIFF\0\0\0\0WAVEfmt "u8.ToArray();
        BitConverter.GetBytes(10_000u).CopyTo(oversizedRiff, 4);
        File.WriteAllBytes(oversizedRiffPath, oversizedRiff);
        True(
            MediaContainerProbe.Probe(oversizedRiffPath).Warning?.Contains("poza końcem", StringComparison.Ordinal) == true,
            "Nie wykryto rozmiaru RIFF poza końcem pliku.");

        var oversizedFlacPath = Path.Combine(directory, "oversized.flac");
        File.WriteAllBytes(oversizedFlacPath, [(byte)'f', (byte)'L', (byte)'a', (byte)'C', 0, 0, 1, 0]);
        True(
            MediaContainerProbe.Probe(oversizedFlacPath).Warning?.Contains("poza koniec", StringComparison.Ordinal) == true,
            "Nie wykryto bloku FLAC poza końcem pliku.");

        var truncatedOggPath = Path.Combine(directory, "truncated.ogg");
        var truncatedOgg = new byte[27];
        "OggS"u8.CopyTo(truncatedOgg);
        truncatedOgg[26] = 3;
        File.WriteAllBytes(truncatedOggPath, truncatedOgg);
        True(
            MediaContainerProbe.Probe(truncatedOggPath).Warning?.Contains("segmentów", StringComparison.Ordinal) == true,
            "Nie wykryto uciętej tablicy segmentów OGG.");

        True(LocalAudioFileDiscovery.IsAudioFile("nagranie.oga"), "OGA powinno być rozpoznawane.");
        True(LocalAudioFileDiscovery.IsAudioFile("nagranie.webm"), "WebM powinno być rozpoznawane.");
        True(LocalAudioFileDiscovery.IsAudioFile("nagranie.adts"), "ADTS powinno być rozpoznawane.");
    }
    finally
    {
        Directory.Delete(directory, true);
    }
}

static void TestVersion6FavoriteMessageMigration()
{
    var directory = Path.Combine(Path.GetTempPath(), $"amc-v6-tests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        var statePath = Path.Combine(directory, "state.json");
        var store = new ConfigurationStore(statePath);
        store.Save(ConfigurationStore.CreateDefaultState());

        var document = JsonNode.Parse(File.ReadAllText(statePath))?.AsObject()
            ?? throw new InvalidOperationException("Nie udało się utworzyć testowej konfiguracji alpha.17.");
        document["schemaVersion"] = 6;
        var templates = document["settings"]?["messages"]?["templates"]?.AsObject()
            ?? throw new InvalidOperationException("Brak szablonów komunikatów w konfiguracji alpha.17.");
        templates["favorite.added"] = "Dodano do ulubionych";
        templates["favorite.removed"] = "Usunięto z ulubionych";
        File.WriteAllText(statePath, document.ToJsonString());

        var loaded = store.LoadOrCreate();
        Equal(ConfigurationStore.CurrentSchemaVersion, loaded.SchemaVersion);
        Equal("Dodano do ulubionych: {item}", loaded.Settings.Messages.Templates["favorite.added"]);
        Equal("Usunięto z ulubionych: {item}", loaded.Settings.Messages.Templates["favorite.removed"]);

        document["schemaVersion"] = 6;
        templates["favorite.added"] = "Moje ulubione: {item}";
        File.WriteAllText(statePath, document.ToJsonString());
        loaded = store.LoadOrCreate();
        Equal("Moje ulubione: {item}", loaded.Settings.Messages.Templates["favorite.added"]);
    }
    finally
    {
        Directory.Delete(directory, true);
    }
}

static void TestVersion9PlayerMessageMigration()
{
    var directory = Path.Combine(Path.GetTempPath(), $"amc-v9-tests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        var statePath = Path.Combine(directory, "state.json");
        var store = new ConfigurationStore(statePath);
        var state = ConfigurationStore.CreateDefaultState();
        state.SchemaVersion = 8;
        state.Settings.Messages.SeekMessages = false;
        state.Settings.Messages.VolumeMessages = true;
        store.Save(state);

        var loaded = store.LoadOrCreate();
        Equal(ConfigurationStore.CurrentSchemaVersion, loaded.SchemaVersion);
        Equal(false, loaded.Settings.Messages.SeekMessages);
        Equal(false, loaded.Settings.Messages.VolumeMessages);
        Equal(PercentageSeekAnnouncementMode.Percent, loaded.Settings.Messages.PercentageSeekAnnouncement);
    }
    finally
    {
        Directory.Delete(directory, true);
    }
}

static void TestVersion10PlayerMessageMigration()
{
    var directory = Path.Combine(Path.GetTempPath(), $"amc-v10-tests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        var statePath = Path.Combine(directory, "state.json");
        var store = new ConfigurationStore(statePath);
        var state = ConfigurationStore.CreateDefaultState();
        state.SchemaVersion = 9;
        state.Settings.Messages.SeekMessages = false;
        state.Settings.Messages.ArrowSeekMessages = false;
        state.Settings.Messages.PercentageSeekMessages = false;
        store.Save(state);

        var loaded = store.LoadOrCreate();
        Equal(ConfigurationStore.CurrentSchemaVersion, loaded.SchemaVersion);
        Equal(false, loaded.Settings.Messages.SeekMessages);
        Equal(true, loaded.Settings.Messages.ArrowSeekMessages);
        Equal(true, loaded.Settings.Messages.PercentageSeekMessages);
    }
    finally
    {
        Directory.Delete(directory, true);
    }
}

static void TestCatalogSearch()
{
    var manager = new SessionManager(new AppSettings());
    var currentResults = MediaCatalogSearch.Search([manager.Current], "brzeg ciszy");
    Equal(1, currentResults.Count);
    Equal("Brzeg ciszy", currentResults[0].Item.Title);
    Equal("TIDAL", currentResults[0].Session.DisplayName);

    var globalResults = MediaCatalogSearch.Search(manager.Sessions, "zielony horyzont");
    Equal(2, globalResults.Count);
    True(globalResults.Any(result => result.Session.DisplayName == "Apple Music"), "Wyniki globalne powinny zawierać Apple Music.");
    True(globalResults.All(result => result.Session.DisplayName != "WiiM"),
        "Sesja WiiM nie może zawierać fikcyjnego katalogu multimediów.");
    Equal(0, MediaCatalogSearch.Search(manager.Sessions, "nieistniejący wynik").Count);
    Equal(0, MediaCatalogSearch.Search(manager.Sessions, "   ").Count);
}

static void TestSearchHistory()
{
    var settings = new SearchHistorySettings();
    var history = new SearchQueryHistory(settings);

    True(history.Record("tidal", "  Brzeg ciszy  "), "Pierwsze zapytanie powinno zostać zapisane.");
    True(history.Record(SearchQueryHistory.GlobalScope, "zielony horyzont"), "Zakres globalny powinien mieć osobną historię.");
    Equal("Brzeg ciszy", history.GetEntries("TIDAL")[0]);
    Equal("zielony horyzont", history.GetEntries(SearchQueryHistory.GlobalScope)[0]);
    Equal(1, history.GetEntries("tidal").Count);

    True(history.Record("tidal", "BRZEG CISZY"), "Nowszy zapis powinien zaktualizować pisownię duplikatu.");
    Equal(1, history.GetEntries("tidal").Count);
    Equal("BRZEG CISZY", history.GetEntries("tidal")[0]);
    True(!history.Record("tidal", "BRZEG CISZY"), "Identyczne najnowsze zapytanie nie powinno zmieniać historii.");

    for (var index = 0; index < 25; index++)
    {
        history.Record("tidal", $"Zapytanie {index}");
    }
    Equal(SearchQueryHistory.MaxEntriesPerScope, history.GetEntries("tidal").Count);
    Equal("Zapytanie 24", history.GetEntries("tidal")[0]);
    Equal("Zapytanie 5", history.GetEntries("tidal")[^1]);

    var duplicateScopes = new SearchHistorySettings
    {
        Entries = new Dictionary<string, List<string>>
        {
            ["tidal"] = Enumerable.Range(0, 15).Select(index => $"Pierwsza {index}").ToList(),
            ["TIDAL"] = Enumerable.Range(0, 15).Select(index => $"Druga {index}").ToList()
        }
    };
    var normalizedHistory = new SearchQueryHistory(duplicateScopes);
    Equal(SearchQueryHistory.MaxEntriesPerScope, normalizedHistory.GetEntries("tidal").Count);

    var directory = Path.Combine(Path.GetTempPath(), $"amc-search-history-tests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        var statePath = Path.Combine(directory, "state.json");
        var store = new ConfigurationStore(statePath);
        var state = ConfigurationStore.CreateDefaultState();
        state.SearchHistory = settings;
        store.Save(state);

        var loaded = store.LoadOrCreate();
        var loadedHistory = new SearchQueryHistory(loaded.SearchHistory);
        Equal("Zapytanie 24", loadedHistory.GetEntries("tidal")[0]);
        Equal("zielony horyzont", loadedHistory.GetEntries(SearchQueryHistory.GlobalScope)[0]);

        var document = JsonNode.Parse(File.ReadAllText(statePath))?.AsObject()
            ?? throw new InvalidOperationException("Nie udało się odczytać testowej historii.");
        document["schemaVersion"] = 7;
        document.Remove("searchHistory");
        File.WriteAllText(statePath, document.ToJsonString());
        loaded = store.LoadOrCreate();
        Equal(ConfigurationStore.CurrentSchemaVersion, loaded.SchemaVersion);
        Equal(0, new SearchQueryHistory(loaded.SearchHistory).GetEntries("tidal").Count);
    }
    finally
    {
        Directory.Delete(directory, true);
    }
}

static void TestPlaybackHistory()
{
    var settings = new PlaybackHistorySettings();
    var history = new PlaybackHistory(settings);
    True(history.Record("local", "a"), "Pierwszy plik powinien trafić do historii.");
    True(history.Record("local", "b"), "Nowszy plik powinien trafić na początek.");
    True(history.Record("local", "a"), "Ponowne odtworzenie powinno przenieść plik na początek.");
    Equal("a", history.GetItemIds("local")[0]);
    Equal("b", history.GetItemIds("local")[1]);
    Equal(2, history.GetItemIds("local").Count);
    True(!history.Record("local", "a"), "Powtórzenie najnowszego wpisu nie powinno zmieniać historii.");

    history.Remove("local", ["a"]);
    Equal(1, history.GetItemIds("local").Count);
    Equal("b", history.GetItemIds("local")[0]);

    history.Record("local", "c");
    history.Record("local", "d");
    history.Record("tidal", "stream-1");
    history.Remove("local", ["b", "d"]);
    Equal(1, history.GetItemIds("local").Count);
    Equal("c", history.GetItemIds("local")[0]);
    Equal("stream-1", history.GetItemIds("tidal")[0]);
    history.Record("local", "b");

    var directory = Path.Combine(Path.GetTempPath(), $"amc-playback-history-tests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        var store = new ConfigurationStore(Path.Combine(directory, "state.json"));
        var state = ConfigurationStore.CreateDefaultState();
        state.LocalMedia.Items =
        [
            new LocalMediaItemSettings { Id = "b", Title = "B", Path = "B.mp3" }
        ];
        history.Record("local", "usunięty");
        state.PlaybackHistory = settings;
        store.Save(state);
        var loaded = store.LoadOrCreate();
        var loadedHistory = new PlaybackHistory(loaded.PlaybackHistory).GetItemIds("local");
        Equal(1, loadedHistory.Count);
        Equal("b", loadedHistory[0]);
    }
    finally
    {
        Directory.Delete(directory, true);
    }
}

static void TestBookmarks()
{
    var settings = new BookmarkSettings();
    var index = new BookmarkIndex(settings);
    var item = new MediaItem
    {
        Id = "local-1",
        Title = "Długie nagranie",
        Duration = TimeSpan.FromMinutes(90)
    };
    var now = new DateTime(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc);

    var first = index.Add("local", "Pliki lokalne", item, TimeSpan.FromMinutes(10), now);
    True(first.Added, "Pierwsza zakładka powinna zostać dodana.");
    var duplicate = index.Add("local", "Pliki lokalne", item, TimeSpan.FromMinutes(10).Add(TimeSpan.FromMilliseconds(400)), now.AddSeconds(1));
    True(!duplicate.Added, "Druga zakładka w tej samej sekundzie nie powinna tworzyć duplikatu.");
    var second = index.Add("local", "Pliki lokalne", item, TimeSpan.FromMinutes(25), now.AddMinutes(1));
    True(second.Added, "Zakładka w innym miejscu powinna zostać dodana.");
    var third = index.Add("local", "Pliki lokalne", item, TimeSpan.FromMinutes(40), now.AddMinutes(2));
    True(third.Added, "Trzecia zakładka powinna zostać dodana.");
    var named = index.Add("local", "Pliki lokalne", item, TimeSpan.FromMinutes(25), now.AddMinutes(3), "  Ważny   fragment  ");
    True(!named.Added, "Nazwanie istniejącej pozycji nie powinno tworzyć duplikatu.");
    True(named.NameChanged, "Istniejąca szybka zakładka powinna otrzymać nazwę.");
    Equal("Ważny fragment", named.Entry.Name);
    var sameName = index.Add("local", "Pliki lokalne", item, TimeSpan.FromMinutes(25), now.AddMinutes(4), "Ważny fragment");
    True(!sameName.NameChanged, "Ponowne zapisanie tej samej nazwy nie powinno zgłaszać zmiany.");
    Equal(3, index.GetForItem("local", item.Id).Count);
    Equal(first.Entry.Id, index.FindRelative("local", item.Id, TimeSpan.FromMinutes(20), -1)?.Id);
    Equal(second.Entry.Id, index.FindRelative("local", item.Id, TimeSpan.FromMinutes(20), 1)?.Id);
    Equal(first.Entry.Id, index.FindRelative("local", item.Id, TimeSpan.FromMinutes(25).Add(TimeSpan.FromMilliseconds(400)), -1)?.Id);
    Equal(second.Entry.Id, index.FindAdjacent("local", item.Id, third.Entry.Id, -1)?.Id);
    Equal(first.Entry.Id, index.FindAdjacent("local", item.Id, second.Entry.Id, -1)?.Id);
    Equal(third.Entry.Id, index.FindAdjacent("local", item.Id, second.Entry.Id, 1)?.Id);
    Equal(null, index.FindAdjacent("local", item.Id, first.Entry.Id, -1)?.Id);
    Equal(null, index.FindAdjacent("local", item.Id, third.Entry.Id, 1)?.Id);
    Equal(third.Entry.Id, index.GetAll()[0].Id);

    var early = index.Add("local", "Pliki lokalne", item, TimeSpan.FromMinutes(5), now.AddMinutes(5));
    var otherItem = new MediaItem
    {
        Id = "apple-1",
        Title = "Inny materiał",
        Duration = TimeSpan.FromMinutes(30)
    };
    var other = index.Add("appleMusic", "Apple Music", otherItem, TimeSpan.FromMinutes(1), now.AddMinutes(6));
    var display = index.GetForDisplay("local", item.Id);
    Equal(early.Entry.Id, display[0].Id);
    Equal(first.Entry.Id, display[1].Id);
    Equal(second.Entry.Id, display[2].Id);
    Equal(third.Entry.Id, display[3].Id);
    Equal(other.Entry.Id, display[4].Id);

    var directory = Path.Combine(Path.GetTempPath(), $"amc-bookmark-tests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        var store = new ConfigurationStore(Path.Combine(directory, "state.json"));
        var state = ConfigurationStore.CreateDefaultState();
        state.Bookmarks = settings;
        store.Save(state);
        var loaded = store.LoadOrCreate();
        Equal(5, new BookmarkIndex(loaded.Bookmarks).GetAll().Count);
        Equal("Długie nagranie", loaded.Bookmarks.Entries.Single(entry => entry.Id == first.Entry.Id).ItemTitle);
        Equal("Ważny fragment", loaded.Bookmarks.Entries.Single(entry => entry.Id == second.Entry.Id).Name);
    }
    finally
    {
        Directory.Delete(directory, true);
    }

    Equal(1, index.Remove([first.Entry.Id]));
    Equal(4, index.GetAll().Count);
}

static void TestChapters()
{
    var settings = new BookmarkSettings();
    var bookmarks = new BookmarkIndex(settings);
    var chapters = new ChapterIndex(settings);
    var item = new MediaItem
    {
        Id = "episode-1",
        Title = "Odcinek z rozdziałami",
        Kind = MediaItemKind.Episode,
        Duration = TimeSpan.FromMinutes(30)
    };
    var now = new DateTime(2026, 9, 4, 12, 0, 0, DateTimeKind.Utc);

    var bookmark = bookmarks.Add("podcasts", "Podcasty", item, TimeSpan.FromMinutes(5), now, "Temat pierwszy");
    var shared = chapters.AddUserChapter(
        "podcasts", "Podcasty", item, TimeSpan.FromMinutes(5), now.AddSeconds(1), "Rozdział pierwszy");
    True(shared.Added, "Zakładka powinna móc stać się również rozdziałem bez duplikowania punktu.");
    Equal(bookmark.Entry.Id, shared.Entry.Id);
    True((shared.Entry.Purpose & BookmarkPurpose.Bookmark) != 0
         && (shared.Entry.Purpose & BookmarkPurpose.Chapter) != 0,
        "Wspólny punkt powinien zachować obie funkcje.");

    var last = chapters.AddUserChapter(
        "podcasts", "Podcasty", item, TimeSpan.FromMinutes(20), now.AddMinutes(2), "Zakończenie");
    var middle = chapters.AddUserChapter(
        "podcasts", "Podcasty", item, TimeSpan.FromMinutes(12), now.AddMinutes(3), "Rozmowa");
    var ordered = chapters.GetForItem("podcasts", item.Id, item.Duration);
    Equal(3, ordered.Count);
    Equal("Rozdział pierwszy", ordered[0].Name);
    Equal("Rozmowa", ordered[1].Name);
    Equal("Zakończenie", ordered[2].Name);
    Equal(TimeSpan.FromMinutes(7), ordered[0].Duration);
    Equal(TimeSpan.FromMinutes(8), ordered[1].Duration);
    Equal(TimeSpan.FromMinutes(10), ordered[2].Duration);
    Equal(middle.Entry.Id, chapters.FindRelative(
        "podcasts", item.Id, item.Duration, TimeSpan.FromMinutes(6), 1)?.Entry.Id);
    Equal(shared.Entry.Id, chapters.FindRelative(
        "podcasts", item.Id, item.Duration, TimeSpan.FromMinutes(12).Add(TimeSpan.FromSeconds(1)), -1)?.Entry.Id);
    Equal(middle.Entry.Id, chapters.FindRelative(
        "podcasts", item.Id, item.Duration, TimeSpan.FromMinutes(12).Add(TimeSpan.FromSeconds(4)), -1)?.Entry.Id);
    Equal(null, chapters.FindRelative(
        "podcasts", item.Id, item.Duration, TimeSpan.FromMinutes(5).Add(TimeSpan.FromSeconds(1)), -1));

    var selectedSequence = new[] { ordered[0], ordered[2] };
    var selectedAlignment = ChapterPlaybackSelection.Align(
        selectedSequence,
        ordered,
        TimeSpan.FromMinutes(7),
        item.Duration);
    Equal(0, selectedAlignment.SelectedIndex);
    True(!selectedAlignment.IsInsideUnselectedRange,
        "Ręczny skok w wybranym rozdziale nie powinien tworzyć zakresu tymczasowego.");

    var manualAlignment = ChapterPlaybackSelection.Align(
        selectedSequence,
        ordered,
        TimeSpan.FromMinutes(13),
        item.Duration);
    Equal(middle.Entry.Id, manualAlignment.ManualChapter?.Entry.Id);
    Equal(TimeSpan.FromMinutes(20), manualAlignment.ManualBoundary);
    Equal(1, ChapterPlaybackSelection.FindNextSelectedIndex(
        selectedSequence,
        manualAlignment.ManualBoundary!.Value));
    True(manualAlignment.IsInsideUnselectedRange,
        "Ręczny skok w niewybranym rozdziale powinien pozwolić odsłuchać go do końca.");

    var lastAlignment = ChapterPlaybackSelection.Align(
        selectedSequence,
        ordered,
        TimeSpan.FromMinutes(22),
        item.Duration);
    Equal(1, lastAlignment.SelectedIndex);
    True(!lastAlignment.IsInsideUnselectedRange,
        "Ręczny skok do ostatniego wybranego rozdziału powinien zachować kolejność zestawu.");

    var directory = Path.Combine(Path.GetTempPath(), $"amc-chapter-tests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        var store = new ConfigurationStore(Path.Combine(directory, "state.json"));
        var state = ConfigurationStore.CreateDefaultState();
        state.Bookmarks = settings;
        store.Save(state);
        var loaded = store.LoadOrCreate();
        var loadedChapters = new ChapterIndex(loaded.Bookmarks).GetForItem("podcasts", item.Id, item.Duration);
        Equal(3, loadedChapters.Count);
        Equal(ChapterOrigin.User, loadedChapters[0].Entry.ChapterOrigin);
        Equal(BookmarkPurpose.Bookmark | BookmarkPurpose.Chapter, loadedChapters[0].Entry.Purpose);
    }
    finally
    {
        Directory.Delete(directory, true);
    }

    Equal(1, chapters.RemoveUserChapters([shared.Entry.Id]));
    Equal(2, chapters.GetForItem("podcasts", item.Id, item.Duration).Count);
    Equal(1, bookmarks.GetForItem("podcasts", item.Id).Count);
    Equal(bookmark.Entry.Id, bookmarks.GetForItem("podcasts", item.Id)[0].Id);
    True(settings.Entries.Contains(last.Entry), "Usunięcie innego rozdziału nie może naruszyć pozostałych.");

    var providerCount = chapters.ReplaceProviderChapters(
        "podcasts",
        "Podcasty",
        item,
        "podcast-json",
        [
            new ProviderChapterPoint("start", "Początek dostawcy", TimeSpan.Zero),
            new ProviderChapterPoint("override", "Nadpisywany", TimeSpan.FromMinutes(5)),
            new ProviderChapterPoint("end", "Końcówka dostawcy", TimeSpan.FromMinutes(25))
        ],
        now.AddMinutes(4));
    Equal(3, providerCount);
    True(chapters.GetForItem("podcasts", item.Id, item.Duration).Any(chapter =>
            chapter.Entry.ChapterOrigin == ChapterOrigin.Provider
            && chapter.Name == "Końcówka dostawcy"),
        "Rozdziały dostawcy powinny wejść do wspólnej osi czasu.");
    chapters.ReplaceProviderChapters(
        "podcasts",
        "Podcasty",
        item,
        "podcast-json",
        [new ProviderChapterPoint("replacement", "Nowy spis", TimeSpan.FromMinutes(2))],
        now.AddMinutes(5));
    Equal(1, settings.Entries.Count(entry =>
        entry.ChapterOrigin == ChapterOrigin.Provider
        && entry.ChapterSourceId?.StartsWith("podcast-json:", StringComparison.Ordinal) == true));

    var sharedAgain = chapters.AddUserChapter(
        "podcasts", "Podcasty", item, TimeSpan.FromMinutes(5), now.AddMinutes(4), "Rozdział ponownie");
    Equal(bookmark.Entry.Id, sharedAgain.Entry.Id);
    Equal(1, bookmarks.Remove([bookmark.Entry.Id]));
    Equal(0, bookmarks.GetForItem("podcasts", item.Id).Count);
    True(chapters.GetForItem("podcasts", item.Id, item.Duration).Any(chapter =>
            string.Equals(chapter.Entry.Id, sharedAgain.Entry.Id, StringComparison.Ordinal)),
        "Usunięcie zakładki nie może usuwać rozdziału współdzielącego ten sam punkt.");
}

static void TestSessionNavigationPersistence()
{
    var directory = Path.Combine(Path.GetTempPath(), $"amc-navigation-tests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        var store = new ConfigurationStore(Path.Combine(directory, "state.json"));
        var state = ConfigurationStore.CreateDefaultState();
        state.SessionNavigation.Sessions["tidal"] = new SessionNavigationState
        {
            CurrentView = "Ulubione",
            PlayerActive = true,
            SelectedItemIds = new Dictionary<string, string?>
            {
                ["Ulubione"] = "tidal-14"
            },
            Filters = new Dictionary<string, string>
            {
                ["Ulubione"] = "północ"
            },
            CollectionSortModes = new Dictionary<string, CollectionSortMode>
            {
                ["Ulubione"] = CollectionSortMode.Alphabetical
            },
            PlaybackContextView = "Ulubione",
            PlaybackContextItemIds = ["tidal-1", "tidal-14"]
        };
        state.SessionNavigation.Sessions["appleMusic"] = new SessionNavigationState
        {
            CurrentView = "Albumy",
            PlayerActive = false
        };
        state.SessionNavigation.Sessions["podcasts"] = new SessionNavigationState
        {
            CurrentView = "Kolejka",
            LastLibraryView = "Podcast:audycja-1",
            SelectedItemIds = new Dictionary<string, string?>
            {
                ["Podcast:audycja-1"] = "odcinek-7"
            }
        };

        store.Save(state);
        var loaded = store.LoadOrCreate();
        var tidal = loaded.SessionNavigation.Sessions["TIDAL"];
        Equal("Ulubione", tidal.CurrentView);
        Equal(true, tidal.PlayerActive);
        Equal("tidal-14", tidal.SelectedItemIds["ulubione"]);
        Equal("północ", tidal.Filters["ULUBIONE"]);
        Equal(CollectionSortMode.Alphabetical, tidal.CollectionSortModes["ULUBIONE"]);
        Equal("Ulubione", tidal.PlaybackContextView);
        True(tidal.PlaybackContextItemIds.SequenceEqual(["tidal-1", "tidal-14"]),
            "Kontekst odtwarzania powinien przetrwać ponowne uruchomienie.");
        Equal("Albumy", loaded.SessionNavigation.Sessions["appleMusic"].CurrentView);
        Equal(false, loaded.SessionNavigation.Sessions["appleMusic"].PlayerActive);
        var podcasts = loaded.SessionNavigation.Sessions["podcasts"];
        Equal("Kolejka", podcasts.CurrentView);
        Equal("Podcast:audycja-1", podcasts.LastLibraryView);
        Equal("odcinek-7", podcasts.SelectedItemIds["Podcast:audycja-1"]);
    }
    finally
    {
        Directory.Delete(directory, true);
    }
}

static void TestLocalMediaPersistence()
{
    var directory = Path.Combine(Path.GetTempPath(), $"amc-local-state-tests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        var store = new ConfigurationStore(Path.Combine(directory, "state.json"));
        var state = ConfigurationStore.CreateDefaultState();
        state.LocalMedia.CurrentItemId = "local-1";
        state.LocalMedia.Volume = 47;
        state.LocalMedia.PlaybackRate = 1.50d;
        state.LocalMedia.FolderSources.Add(new LocalFolderSourceSettings
        {
            Id = "folder-1",
            Path = directory,
            DisplayName = "Nagrania"
        });
        state.LocalMedia.FolderPlaybackOptions.Add(new LocalFolderPlaybackSettings
        {
            Path = Path.Combine(directory, "Podcasty"),
            ResumePositionMode = ResumePositionMode.Remember,
            PlaybackRateOverride = 1.75d,
            LoudnessNormalizationOverride = true,
            SmoothTrackTransitionsOverride = false,
            InterTrackSilenceMillisecondsOverride = 2000
        });
        state.LocalMedia.CurrentFolderPath = directory;
        state.LocalMedia.LibraryView = "Foldery";
        state.LocalMedia.CustomOrderItemIds.Add("local-1");
        state.LocalMedia.ExcludedPaths.Add(@"C:\Muzyka\pomijany.mp3");
        state.CollectionOrders.FavoriteItemIdsBySession["local"] = ["local-1"];
        state.CollectionOrders.FavoriteAddedItemIdsBySession["local"] = ["local-2", "local-1"];
        state.CollectionOrders.LibraryAddedItemIdsBySession["radio"] = ["radio-2", "radio-1"];
        state.CollectionOrders.LibraryItemIdsBySession["radio"] = ["radio-1", "radio-2"];
        state.CollectionOrders.QueueItemIdsBySession["local"] = ["local-1"];
        state.LocalMedia.Items.Add(new LocalMediaItemSettings
        {
            Id = "local-1",
            Title = "Długie nagranie",
            HasCustomTitle = true,
            Path = @"C:\Muzyka\długie.aac",
            DurationTicks = TimeSpan.FromMinutes(90).Ticks,
            ResumePositionTicks = TimeSpan.FromMinutes(17).Ticks,
            ClipStartTicks = TimeSpan.FromMinutes(2).Ticks,
            ClipEndTicks = TimeSpan.FromMinutes(4).Ticks,
            FileLength = 123456,
            LastWriteUtcTicks = 987654,
            IsFavorite = true,
            IsInLibrary = true,
            IsInQueue = true,
            ResumePositionMode = ResumePositionMode.Remember,
            PlaybackRateOverride = 1.75d,
            OutputDeviceId = "default",
            LoudnessNormalizationOverride = false,
            SmoothTrackTransitionsOverride = true,
            InterTrackSilenceMillisecondsOverride = 500
        });
        state.LocalMedia.Items.Add(new LocalMediaItemSettings
        {
            Id = "local-2",
            Title = "Drugie nagranie",
            Path = @"C:\Muzyka\drugie.flac",
            DurationTicks = TimeSpan.FromMinutes(10).Ticks,
            ClipStartTicks = TimeSpan.FromSeconds(10).Ticks,
            ClipEndTicks = TimeSpan.FromSeconds(20).Ticks
        });

        store.Save(state);
        var loaded = store.LoadOrCreate();
        Equal(ConfigurationStore.CurrentSchemaVersion, loaded.SchemaVersion);
        Equal("local-1", loaded.LocalMedia.CurrentItemId);
        Equal(47, loaded.LocalMedia.Volume);
        Equal(1.50d, loaded.LocalMedia.PlaybackRate);
        Equal(2, loaded.LocalMedia.Items.Count);
        Equal(1, loaded.LocalMedia.FolderSources.Count);
        Equal(1, loaded.LocalMedia.FolderPlaybackOptions.Count);
        Equal("Foldery", loaded.LocalMedia.LibraryView);
        Equal(2, loaded.LocalMedia.CustomOrderItemIds.Count);
        Equal("local-1", loaded.LocalMedia.CustomOrderItemIds[0]);
        Equal(Path.GetFullPath(@"C:\Muzyka\pomijany.mp3"), loaded.LocalMedia.ExcludedPaths[0]);
        Equal("Nagrania", loaded.LocalMedia.FolderSources[0].DisplayName);
        var folderOptions = loaded.LocalMedia.FolderPlaybackOptions[0];
        Equal(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.Combine(directory, "Podcasty"))),
            folderOptions.Path);
        Equal(ResumePositionMode.Remember, folderOptions.ResumePositionMode);
        Equal(1.75d, folderOptions.PlaybackRateOverride);
        Equal(true, folderOptions.LoudnessNormalizationOverride);
        Equal(false, folderOptions.SmoothTrackTransitionsOverride);
        Equal(2000, folderOptions.InterTrackSilenceMillisecondsOverride);
        Equal(Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory)), loaded.LocalMedia.CurrentFolderPath);
        Equal("local-1", new PlaybackHistory(loaded.PlaybackHistory).GetItemIds("local")[0]);
        var item = loaded.LocalMedia.Items.Single(entry => entry.Id == "local-1");
        Equal("Długie nagranie", item.Title);
        Equal(true, item.HasCustomTitle);
        Equal(TimeSpan.FromMinutes(17).Ticks, item.ResumePositionTicks);
        Equal(TimeSpan.FromMinutes(2).Ticks, item.ClipStartTicks);
        Equal(TimeSpan.FromMinutes(4).Ticks, item.ClipEndTicks);
        Equal(true, item.IsFavorite);
        Equal(true, item.IsInQueue);
        Equal(true, item.IsAvailable);
        Equal(ResumePositionMode.Remember, item.ResumePositionMode);
        Equal(1.75d, item.PlaybackRateOverride);
        Equal("default", item.OutputDeviceId);
        Equal(false, item.LoudnessNormalizationOverride);
        Equal(true, item.SmoothTrackTransitionsOverride);
        Equal(500, item.InterTrackSilenceMillisecondsOverride);
        var secondItem = loaded.LocalMedia.Items.Single(entry => entry.Id == "local-2");
        Equal(TimeSpan.FromSeconds(10).Ticks, secondItem.ClipStartTicks);
        Equal(TimeSpan.FromSeconds(20).Ticks, secondItem.ClipEndTicks);
        Equal("local-1", loaded.CollectionOrders.FavoriteItemIdsBySession["LOCAL"].Single());
        True(
            loaded.CollectionOrders.FavoriteAddedItemIdsBySession["LOCAL"].SequenceEqual(["local-2", "local-1"]),
            "Kolejność dodawania ulubionych powinna przetrwać zapis w lokalnej bazie.");
        True(
            loaded.CollectionOrders.LibraryAddedItemIdsBySession["RADIO"].SequenceEqual(["radio-2", "radio-1"]),
            "Kolejność dodawania do biblioteki powinna przetrwać zapis w lokalnej bazie.");
        True(
            loaded.CollectionOrders.LibraryItemIdsBySession["RADIO"].SequenceEqual(["radio-1", "radio-2"]),
            "Kolejność własna biblioteki powinna przetrwać zapis w lokalnej bazie.");
        Equal("local-1", loaded.CollectionOrders.QueueItemIdsBySession["LOCAL"].Single());

        var output = new FakeMediaOutput();
        var media = new MediaItem { Id = item.Id, Title = item.Title, Source = item.Path };
        var session = new DemoMediaSession("local", "Pliki lokalne", [media], output);
        session.SetRememberedPosition(item.Id, TimeSpan.FromTicks(item.ResumePositionTicks));
        Equal(TimeSpan.FromMinutes(17), session.Position);
        session.TogglePlayback();
        Equal(TimeSpan.FromMinutes(17), output.Position);
    }
    finally
    {
        Directory.Delete(directory, true);
    }
}

static void TestPlaylists()
{
    var directory = Path.Combine(Path.GetTempPath(), $"amc-playlist-tests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        var state = ConfigurationStore.CreateDefaultState();
        state.LocalMedia.Items.AddRange(
        [
            new LocalMediaItemSettings
            {
                Id = "track-a",
                Title = "Alfa",
                Path = Path.Combine(directory, "a.mp3"),
                IsInLibrary = true,
                IsAvailable = true
            },
            new LocalMediaItemSettings
            {
                Id = "track-b",
                Title = "Bravo",
                Path = Path.Combine(directory, "b.mp3"),
                IsInLibrary = true,
                IsAvailable = true
            },
            new LocalMediaItemSettings
            {
                Id = "track-c",
                Title = "Charlie",
                Path = Path.Combine(directory, "c.mp3"),
                IsInLibrary = true,
                IsAvailable = true
            }
        ]);
        var playlists = new PlaylistIndex(state.Playlists);
        var first = playlists.Create("local", "Do odsłuchu");
        playlists.SetMembership(first.Id, ["track-a", "track-b"], true);
        Equal(PlaylistMembershipState.Some, playlists.GetMembership(first.Id, ["track-b", "track-c"]));
        playlists.SetMembership(first.Id, ["track-b", "track-c"], true);
        True(first.ItemIds.SequenceEqual(["track-a", "track-b", "track-c"]),
            "Dodanie zbiorowe powinno zachować dotychczasową kolejność i dopisać tylko brakujący element.");
        Equal(
            ManualOrderMoveResult.Moved,
            playlists.MoveItems(first.Id, ["track-a", "track-b", "track-c"], ["track-c"], -1));
        True(first.ItemIds.SequenceEqual(["track-a", "track-c", "track-b"]),
            "Playlista powinna pozwalać przenieść element bez zmiany katalogu.");
        playlists.SetMembership(first.Id, ["track-c"], false);
        True(first.ItemIds.SequenceEqual(["track-a", "track-b"]),
            "Usunięcie z playlisty nie powinno naruszyć pozostałych pozycji.");
        playlists.Rename(first.Id, "Audycje");
        var second = playlists.Create("local", "Muzyka");
        playlists.SetMembership(second.Id, ["track-c"], true);
        var radioPlaylist = playlists.Create("radio", "Stacje informacyjne");
        playlists.SetMembership(radioPlaylist.Id, ["station-a", "station-b"], true);
        var duplicateRejected = false;
        try
        {
            playlists.Create("LOCAL", "muzyka");
        }
        catch (InvalidOperationException)
        {
            duplicateRejected = true;
        }
        True(duplicateRejected, "Nazwy playlist powinny być unikatowe w obrębie sesji.");

        var statePath = Path.Combine(directory, "state.json");
        var databasePath = Path.Combine(directory, "library.db");
        var store = new ConfigurationStore(statePath, databasePath);
        store.Save(state);
        var loaded = new ConfigurationStore(statePath, databasePath).LoadOrCreate();
        var loadedPlaylists = new PlaylistIndex(loaded.Playlists).GetForSession("LOCAL");
        Equal(2, loadedPlaylists.Count);
        Equal("Audycje", loadedPlaylists[0].Name);
        True(loadedPlaylists[0].ItemIds.SequenceEqual(["track-a", "track-b"]),
            "SQLite powinien zachować kolejność elementów pierwszej playlisty.");
        Equal("track-c", loadedPlaylists[1].ItemIds.Single());
        var loadedRadioPlaylist = new PlaylistIndex(loaded.Playlists).GetForSession("RADIO").Single();
        Equal("Stacje informacyjne", loadedRadioPlaylist.Name);
        True(loadedRadioPlaylist.ItemIds.SequenceEqual(["station-a", "station-b"]),
            "SQLite powinien zachować playlistę stacji jako osobną kolekcję sesji Radio.");

        var clone = new PlaylistIndex(loaded.Playlists).CloneSettings();
        new PlaylistIndex(loaded.Playlists).Remove(loadedPlaylists[0].Id);
        new PlaylistIndex(loaded.Playlists).ReplaceSession(
            "local",
            clone.Entries.Where(entry => string.Equals(
                entry.SessionId,
                "local",
                StringComparison.OrdinalIgnoreCase)));
        Equal(2, new PlaylistIndex(loaded.Playlists).GetForSession("local").Count);
        Equal(1, new PlaylistIndex(loaded.Playlists).GetForSession("radio").Count);
    }
    finally
    {
        Directory.Delete(directory, true);
    }
}

static void TestLocalLibraryManualOrder()
{
    var alpha = new MediaItem { Id = "a", Title = "Alfa", Source = @"C:\Muzyka\a.mp3" };
    var bravo = new MediaItem { Id = "b", Title = "Brawo", Source = @"C:\Muzyka\b.mp3" };
    var charlie = new MediaItem { Id = "c", Title = "Charlie", Source = @"C:\Muzyka\c.mp3" };
    var delta = new MediaItem { Id = "d", Title = "Delta", Source = @"C:\Muzyka\d.mp3" };

    var initialized = LocalLibraryManualOrder.Normalize(
        [],
        [charlie, alpha, bravo],
        initializeAlphabetically: true);
    True(initialized.SequenceEqual(["a", "b", "c"]), "Pierwsze otwarcie powinno utworzyć porządek alfabetyczny.");

    var normalized = LocalLibraryManualOrder.Normalize(
        ["c", "brak", "c"],
        [alpha, bravo, charlie, delta]);
    True(normalized.SequenceEqual(["c", "a", "b", "d"]), "Należy zachować znaną kolejność, usunąć duplikaty i dopisać nowe elementy.");
    True(
        LocalLibraryManualOrder.Order([alpha, bravo, charlie, delta], normalized)
            .Select(item => item.Id)
            .SequenceEqual(["c", "a", "b", "d"]),
        "Widok powinien respektować zapisaną kolejność.");

    var singleMove = new List<string> { "a", "b", "c", "d" };
    Equal(
        ManualOrderMoveResult.Moved,
        LocalLibraryManualOrder.MoveVisibleBlock(singleMove, ["a", "b", "c", "d"], ["b"], 1));
    True(singleMove.SequenceEqual(["a", "c", "b", "d"]), "Pojedynczy element powinien przesunąć się o jeden wiersz.");

    var blockMove = new List<string> { "a", "b", "c", "d" };
    Equal(
        ManualOrderMoveResult.Moved,
        LocalLibraryManualOrder.MoveVisibleBlock(blockMove, ["a", "b", "c", "d"], ["b", "c"], -1));
    True(blockMove.SequenceEqual(["b", "c", "a", "d"]), "Ciągłe zaznaczenie powinno przenieść się jako jeden blok.");
    Equal(
        ManualOrderMoveResult.Boundary,
        LocalLibraryManualOrder.MoveVisibleBlock(blockMove, ["b", "c", "a", "d"], ["b", "c"], -1));
    Equal(
        ManualOrderMoveResult.NonContiguousSelection,
        LocalLibraryManualOrder.MoveVisibleBlock(blockMove, ["b", "c", "a", "d"], ["b", "a"], 1));

    var orderWithHiddenItems = new List<string> { "a", "ukryty-1", "b", "ukryty-2", "c" };
    Equal(
        ManualOrderMoveResult.Moved,
        LocalLibraryManualOrder.MoveVisibleBlock(orderWithHiddenItems, ["a", "b", "c"], ["b"], 1));
    True(
        orderWithHiddenItems.SequenceEqual(["a", "ukryty-1", "c", "ukryty-2", "b"]),
        "Przenoszenie nie powinno gubić pozycji chwilowo niewidocznych plików.");

    var placedBeforeTarget = new List<string> { "a", "b", "c", "d", "e" };
    Equal(
        ManualOrderPlacementResult.Moved,
        LocalLibraryManualOrder.PlaceItemsBefore(placedBeforeTarget, ["b", "d"], "e"));
    True(
        placedBeforeTarget.SequenceEqual(["a", "c", "b", "d", "e"]),
        "Ctrl+X i Ctrl+V powinny przenieść zaznaczone pozycje jako blok przed celem.");
    Equal(
        ManualOrderPlacementResult.TargetInSelection,
        LocalLibraryManualOrder.PlaceItemsBefore(placedBeforeTarget, ["b", "d"], "b"));

    var orderBeforeRemoval = new List<string> { "a", "b", "c", "d", "e" };
    var removedPositions = LocalLibraryManualOrder.CapturePositions(
        orderBeforeRemoval,
        ["b", "d"]);
    var orderAfterSaveWithoutRemovedItems = LocalLibraryManualOrder.Normalize(
        orderBeforeRemoval,
        [alpha, charlie, new MediaItem { Id = "e", Title = "Echo" }]);
    True(
        orderAfterSaveWithoutRemovedItems.SequenceEqual(["a", "c", "e"]),
        "Trwałe usunięcie rekordów powinno usunąć ich identyfikatory z bieżącego porządku.");
    LocalLibraryManualOrder.RestorePositions(orderAfterSaveWithoutRemovedItems, removedPositions);
    True(
        orderAfterSaveWithoutRemovedItems.SequenceEqual(["a", "b", "c", "d", "e"]),
        "Ctrl+Z powinno odtworzyć dokładne pozycje kilku usuniętych elementów.");

    var withGenuinelyNewItem = LocalLibraryManualOrder.Normalize(
        orderAfterSaveWithoutRemovedItems,
        [alpha, bravo, charlie, delta, new MediaItem { Id = "e", Title = "Echo" }, new MediaItem { Id = "f", Title = "Foxtrot" }]);
    True(
        withGenuinelyNewItem.SequenceEqual(["a", "b", "c", "d", "e", "f"]),
        "Rzeczywiście nowy plik powinien nadal trafić na koniec kolejności własnej.");
}

static void TestLocalAlbumInference()
{
    var root = Path.GetFullPath(@"C:\Muzyka");
    var albumFolder = Path.Combine(root, "Anna Kowalska", "Pierwszy album");
    var first = new MediaItem
    {
        Id = "track-1",
        Title = "01 Początek",
        Source = Path.Combine(albumFolder, "01 - Początek.mp3"),
        Duration = TimeSpan.FromMinutes(2)
    };
    var second = new MediaItem
    {
        Id = "track-2",
        Title = "02 Środek",
        Source = Path.Combine(albumFolder, "02. Środek.flac"),
        Duration = TimeSpan.FromMinutes(3)
    };
    var tenth = new MediaItem
    {
        Id = "track-10",
        Title = "10 Koniec",
        Source = Path.Combine(albumFolder, "10_Koniec.ogg"),
        Duration = TimeSpan.FromMinutes(4)
    };

    var albums = LocalAlbumInference.Infer([tenth, second, first], [root]);
    Equal(1, albums.Count);
    var album = albums[0];
    Equal("Pierwszy album", album.Title);
    Equal("Anna Kowalska", album.Artist);
    Equal(albumFolder, album.FolderPath);
    Equal(TimeSpan.FromMinutes(9), album.Duration);
    True(
        album.Tracks.Select(track => track.Id).SequenceEqual(["track-1", "track-2", "track-10"]),
        "Ścieżki powinny być uporządkowane według numerów z nazw.");

    True(LocalAlbumInference.TryGetTrackNumber(first.Source, out var firstNumber) && firstNumber == 1,
        "Należy rozpoznać numer z początku nazwy.");
    True(!LocalAlbumInference.TryGetTrackNumber(@"C:\Muzyka\2026-08-24 nagranie.mp3", out _),
        "Rok i data nie mogą udawać numeru ścieżki.");
    True(!LocalAlbumInference.TryGetTrackNumber(@"C:\Muzyka\01Początek.mp3", out _),
        "Cyfry bez separatora nie powinny klasyfikować zwykłej nazwy.");

    var looseFolder = Path.Combine(root, "Nagrania");
    var loose = LocalAlbumInference.Infer(
        [
            new MediaItem { Id = "loose-1", Title = "Poranek", Source = Path.Combine(looseFolder, "Poranek.mp3") },
            new MediaItem { Id = "loose-2", Title = "Wieczór", Source = Path.Combine(looseFolder, "Wieczór.mp3") }
        ],
        [root]);
    Equal(0, loose.Count);

    var single = LocalAlbumInference.Infer([first], [root]);
    Equal(0, single.Count);
    var directAlbum = LocalAlbumInference.Infer([first, second], [albumFolder]);
    Equal(string.Empty, directAlbum.Single().Artist);
}

static void TestCommandPalette()
{
    var profile = KeyboardProfile.CreateDefault();
    var settings = new AppSettings();
    settings.Messages.Enabled = true;
    settings.Messages.DetailedHints = false;
    var radioSettings = new RadioSettings
    {
        AutomaticTrackRecognitionScope = RadioRecognitionScope.CurrentAndRecordingStations
    };
    var entries = CommandPaletteSearch.CreateEntries(
        profile,
        settings,
        radioSettings: radioSettings);

    True(entries.Count >= 40, "Paleta powinna zawierać pełny katalog poleceń.");
    True(entries.All(entry => entry.CommandId != CommandIds.CommandPalette), "Paleta nie powinna uruchamiać samej siebie.");
    Equal(9, entries.Count(entry => entry.CommandId.StartsWith("session.slot.", StringComparison.Ordinal)));
    Equal(
        "Wybierz sesję 1: Pliki lokalne",
        entries.Single(entry => entry.CommandId == CommandIds.SessionSlot(1)).DisplayName);

    var favorites = entries.Single(entry => entry.CommandId == CommandIds.ViewFavorites);
    Equal("Ctrl+U", favorites.LocalShortcut);
    Equal("U", favorites.PrefixShortcut);
    True(favorites.Label.Contains("Ctrl+U", StringComparison.Ordinal), "Etykieta powinna podawać skrót działający w oknie.");
    True(favorites.Label.Contains("prefiks U", StringComparison.Ordinal), "Etykieta powinna podawać aktywny skrót po prefiksie.");
    Equal(favorites.Label, favorites.ToString());
    True(!favorites.ToString().Contains("CommandId", StringComparison.Ordinal), "Lista nie może ujawniać technicznych nazw pól obiektu.");
    Equal("Ctrl+O", entries.Single(entry => entry.CommandId == CommandIds.OpenLocalFiles).LocalShortcut);
    Equal("Ctrl+Shift+O", entries.Single(entry => entry.CommandId == CommandIds.OpenLocalFolder).LocalShortcut);
    Equal("Ctrl+P", entries.Single(entry => entry.CommandId == CommandIds.ViewPlaylists).LocalShortcut);
    Equal("Ctrl+Shift+P", entries.Single(entry => entry.CommandId == CommandIds.ManagePlaylists).LocalShortcut);
    Equal("Ctrl+L", entries.Single(entry => entry.CommandId == CommandIds.ViewLibrary).LocalShortcut);
    Equal("Ctrl+Alt+P (Pliki lokalne lub Radio internetowe)", entries.Single(entry => entry.CommandId == CommandIds.ViewRadioPresets).LocalShortcut);
    Equal("Ctrl+Alt+Shift+P (sesja obsługująca presety)", entries.Single(entry => entry.CommandId == CommandIds.AssignRadioPreset).LocalShortcut);
    Equal("T (odtwarzacz radia lub widok Nagrywane)", entries.Single(entry => entry.CommandId == CommandIds.SplitRadioRecording).LocalShortcut);
    Equal("Ctrl+Alt+Shift+R", entries.Single(entry => entry.CommandId == CommandIds.StopAllRadioRecordings).LocalShortcut);
    Equal("Ctrl+Shift+H (Radio internetowe)", entries.Single(entry => entry.CommandId == CommandIds.ManageRadioSchedules).LocalShortcut);
    Equal("Ctrl+N (Podcasty)", entries.Single(entry => entry.CommandId == CommandIds.AddPodcast).LocalShortcut);
    Equal("Ctrl+O (Podcasty)", entries.Single(entry => entry.CommandId == CommandIds.ImportPodcastOpml).LocalShortcut);
    Equal("Ctrl+Alt+W", entries.Single(entry => entry.CommandId == CommandIds.OpenOnWiiM).LocalShortcut);
    Equal("Ctrl+N (WiiM)", entries.Single(entry => entry.CommandId == CommandIds.AddWiiMNetworkStream).LocalShortcut);
    Equal("Ctrl+O (WiiM)", entries.Single(entry => entry.CommandId == CommandIds.ImportWiiMNetworkStreams).LocalShortcut);
    Equal("Ctrl+Shift+O (WiiM)", entries.Single(entry => entry.CommandId == CommandIds.ExportWiiMNetworkStreams).LocalShortcut);
    Equal("F5 (Podcasty)", entries.Single(entry => entry.CommandId == CommandIds.RefreshPodcast).LocalShortcut);
    Equal("Ctrl+F5 (Podcasty)", entries.Single(entry => entry.CommandId == CommandIds.RefreshPodcastLibrary).LocalShortcut);
    Equal("Ctrl+I (Podcasty)", entries.Single(entry => entry.CommandId == CommandIds.ViewPodcastInbox).LocalShortcut);
    Equal("Ctrl+Shift+I (Podcasty)", entries.Single(entry => entry.CommandId == CommandIds.ViewPodcastInProgress).LocalShortcut);
    True(entries.Any(entry => entry.CommandId == CommandIds.ViewFolders), "Paleta powinna zawierać widok folderów.");
    True(entries.Any(entry => entry.CommandId == CommandIds.SettingsSessionOrder), "Paleta powinna zawierać ustawienia kolejności sesji.");
    Equal(
        "Zakres automatycznego rozpoznawania radia: aktualnie odtwarzana stacja i wszystkie stacje nagrywane w tle. Enter: ustawienia",
        entries.Single(entry => entry.CommandId == CommandIds.SettingsRadioRecognitionScope).DisplayName);
    Equal("Ctrl+H", entries.Single(entry => entry.CommandId == CommandIds.ViewHistory).LocalShortcut);
    var bookmarks = entries.Single(entry => entry.CommandId == CommandIds.ViewBookmarks);
    Equal("Ctrl+B", bookmarks.LocalShortcut);
    Equal("B", bookmarks.PrefixShortcut);
    Equal("B (odtwarzacz)", entries.Single(entry => entry.CommandId == CommandIds.AddBookmark).LocalShortcut);
    Equal("Ctrl+Shift+B (odtwarzacz)", entries.Single(entry => entry.CommandId == CommandIds.AddNamedBookmark).LocalShortcut);
    Equal("Shift+PageUp (odtwarzacz)", entries.Single(entry => entry.CommandId == CommandIds.PreviousBookmark).LocalShortcut);
    Equal("Shift+PageDown (odtwarzacz)", entries.Single(entry => entry.CommandId == CommandIds.NextBookmark).LocalShortcut);
    Equal("Alt+PageUp (odtwarzacz lokalnego pliku)", entries.Single(entry => entry.CommandId == CommandIds.PreviousClipBoundary).LocalShortcut);
    Equal("Alt+PageDown (odtwarzacz lokalnego pliku)", entries.Single(entry => entry.CommandId == CommandIds.NextClipBoundary).LocalShortcut);
    Equal("Ctrl+X (odtwarzacz lokalnego pliku)", entries.Single(entry => entry.CommandId == CommandIds.RemoveClipFromOriginal).LocalShortcut);
    Equal("Ctrl+J (odtwarzacz)", entries.Single(entry => entry.CommandId == CommandIds.SeekToTime).LocalShortcut);
    Equal("Ctrl+Shift+J (odtwarzacz)", entries.Single(entry => entry.CommandId == CommandIds.SeekToPercentage).LocalShortcut);
    Equal("PageUp (odtwarzacz)", entries.Single(entry => entry.CommandId == CommandIds.Previous).LocalShortcut);
    Equal("PageDown (odtwarzacz)", entries.Single(entry => entry.CommandId == CommandIds.Next).LocalShortcut);
    var itemProperties = entries.Single(entry => entry.CommandId == CommandIds.ItemProperties);
    Equal("Alt+Enter", itemProperties.LocalShortcut);
    True(itemProperties.PrefixShortcut is null, "Właściwości nie mają skrótu prefiksowego.");
    Equal("Alt+D (Podcasty)", entries.Single(entry => entry.CommandId == CommandIds.PodcastDescription).LocalShortcut);
    Equal("Alt+D (Radio internetowe i WiiM)", entries.Single(entry => entry.CommandId == CommandIds.CurrentBroadcastInformation).LocalShortcut);
    var goToPodcast = entries.Single(entry => entry.CommandId == CommandIds.GoToPodcast);
    Equal("Przejdź do podcastu tego odcinka", goToPodcast.DisplayName);
    True(goToPodcast.LocalShortcut is null, "Przejście do podcastu nie powinno zajmować nowego skrótu domyślnego.");
    True(entries.All(entry => entry.CommandId != "view.itemInformation"), "Stare polecenie informacji nie może być w palecie.");
    True(entries.All(entry => entry.CommandId != "information.playbackStatus"), "Stary odczyt stanu nie może być w palecie.");
    Equal("Left (odtwarzacz)", entries.Single(entry => entry.CommandId == CommandIds.SeekBackward10).LocalShortcut);
    Equal("Shift+Left (odtwarzacz)", entries.Single(entry => entry.CommandId == CommandIds.SeekBackward30).LocalShortcut);
    Equal("Ctrl+Left (odtwarzacz)", entries.Single(entry => entry.CommandId == CommandIds.SeekBackward60).LocalShortcut);
    Equal("Up (odtwarzacz)", entries.Single(entry => entry.CommandId == CommandIds.VolumeUp5).LocalShortcut);
    Equal("Ctrl+M", entries.Single(entry => entry.CommandId == CommandIds.ToggleMuteCurrentSession).LocalShortcut);
    Equal("Ctrl+Shift+M", entries.Single(entry => entry.CommandId == CommandIds.ToggleMuteAllSessions).LocalShortcut);
    Equal("Shift+N (odtwarzacz obsługujący przetwarzanie dźwięku)",
        entries.Single(entry => entry.CommandId == CommandIds.ToggleLoudnessNormalization).LocalShortcut);
    Equal("Shift+T (odtwarzacz obsługujący przetwarzanie dźwięku)",
        entries.Single(entry => entry.CommandId == CommandIds.ToggleSmoothTrackTransitions).LocalShortcut);
    Equal("Shift+C (odtwarzacz obsługujący przetwarzanie dźwięku)",
        entries.Single(entry => entry.CommandId == CommandIds.CycleInterTrackSilence).LocalShortcut);
    Equal("Shift+N", entries.Single(entry => entry.CommandId == CommandIds.ToggleLoudnessNormalization).PrefixShortcut);
    Equal("T", entries.Single(entry => entry.CommandId == CommandIds.ToggleSmoothTrackTransitions).PrefixShortcut);
    Equal("C", entries.Single(entry => entry.CommandId == CommandIds.CycleInterTrackSilence).PrefixShortcut);
    Equal("Shift+, (odtwarzacz)", entries.Single(entry => entry.CommandId == CommandIds.PlaybackRateDown).LocalShortcut);
    Equal("Shift+. (odtwarzacz)", entries.Single(entry => entry.CommandId == CommandIds.PlaybackRateUp).LocalShortcut);
    Equal("Ctrl+. (odtwarzacz)", entries.Single(entry => entry.CommandId == CommandIds.PlaybackRateReset).LocalShortcut);
    Equal("Ctrl+Shift+E", entries.Single(entry => entry.CommandId == CommandIds.TimeElapsed).LocalShortcut);
    Equal("Ctrl+Shift+S lub Ctrl+0",
        entries.Single(entry => entry.CommandId == CommandIds.SessionList).LocalShortcut);
    Equal("F6", entries.Single(entry => entry.CommandId == CommandIds.ViewNowPlaying).LocalShortcut);
    Equal("0 (odtwarzacz)", entries.Single(entry => entry.CommandId == CommandIds.SeekPercent(0)).LocalShortcut);
    Equal("9 (odtwarzacz)", entries.Single(entry => entry.CommandId == CommandIds.SeekPercent(90)).LocalShortcut);
    Equal(10, entries.Count(entry => CommandIds.TryParseSeekPercent(entry.CommandId, out _)));
    True(
        entries.Single(entry => entry.CommandId == CommandIds.OpenOfficialApp).LocalShortcut is null,
        "Otwieranie strony elementu nie powinno kolidować ze skrótem folderu.");
    Equal("Alt+1 (lista lokalna)", entries.Single(entry => entry.CommandId == CommandIds.ViewFolders).LocalShortcut);
    Equal("Alt+2 (lista lokalna)", entries.Single(entry => entry.CommandId == CommandIds.ViewAllLocalFiles).LocalShortcut);
    Equal("Alt+3 (lista lokalna)", entries.Single(entry => entry.CommandId == CommandIds.ViewCustomLocalOrder).LocalShortcut);
    Equal("Alt+Up (kolejność własna lub Ulubione)", entries.Single(entry => entry.CommandId == CommandIds.MoveLocalLibraryItemUp).LocalShortcut);
    Equal("Alt+Down (kolejność własna lub Ulubione)", entries.Single(entry => entry.CommandId == CommandIds.MoveLocalLibraryItemDown).LocalShortcut);
    Equal("F5 (lista lokalna)", entries.Single(entry => entry.CommandId == CommandIds.RefreshLocalLibrary).LocalShortcut);
    Equal("Ctrl+F5", entries.Single(entry => entry.CommandId == CommandIds.ManageLocalSources).LocalShortcut);
    Equal("F2 (Biblioteka lokalna, Radio lub Podcasty)", entries.Single(entry => entry.CommandId == CommandIds.RenameLibraryItem).LocalShortcut);
    Equal("Shift+F2 (lista lokalna)", entries.Single(entry => entry.CommandId == CommandIds.RenameLocalFile).LocalShortcut);
    Equal("Ctrl+F1", entries.Single(entry => entry.CommandId == CommandIds.KeyboardHelp).LocalShortcut);

    var remaining = CommandPaletteSearch.Filter(entries, "czas pozostaly");
    Equal(1, remaining.Count);
    Equal(CommandIds.TimeRemaining, remaining[0].CommandId);

    var shiftedFavorite = CommandPaletteSearch.Filter(entries, "shift u");
    True(shiftedFavorite.Any(entry => entry.CommandId == CommandIds.ToggleFavorite), "Powinno dać się filtrować także po skrócie.");
    Equal(3, CommandPaletteSearch.Filter(entries, "predkosc").Count);
    Equal(0, CommandPaletteSearch.Filter(entries, "polecenie-którego-nie-ma").Count);

    Equal("p", CommandPaletteSearch.ContinueOrRestartListQuery(entries, "sesja", "p"));
    Equal(string.Empty, CommandPaletteSearch.ContinueOrRestartListQuery(entries, "sesja", "§"));

    var messages = entries.Single(entry => entry.CommandId == CommandIds.SettingsToggleMessages);
    Equal("Komunikaty dostępności: włączone. Enter: wyłącz", messages.DisplayName);
    var hints = entries.Single(entry => entry.CommandId == CommandIds.SettingsToggleDetailedHints);
    Equal("Szczegółowe podpowiedzi klawiatury: wyłączone. Enter: włącz", hints.DisplayName);
    var seekMessages = entries.Single(entry => entry.CommandId == CommandIds.SettingsToggleSeekMessages);
    Equal("Automatyczne komunikaty odtwarzacza: włączone. Enter: wyłącz", seekMessages.DisplayName);
    Equal("Ctrl+Shift+G", seekMessages.LocalShortcut);
    Equal(
        "Komunikaty przewijania strzałkami: włączone. Enter: ustawienia",
        entries.Single(entry => entry.CommandId == CommandIds.SettingsArrowSeekMessages).DisplayName);
    Equal(
        "Komunikaty skoków cyframi: włączone. Enter: ustawienia",
        entries.Single(entry => entry.CommandId == CommandIds.SettingsPercentageSeekMessages).DisplayName);
    Equal(
        "Komunikaty nawigacji po zakładkach: włączone. Enter: ustawienia",
        entries.Single(entry => entry.CommandId == CommandIds.SettingsBookmarkNavigationMessages).DisplayName);
    Equal(
        "Komunikaty zmian głośności: włączone. Enter: ustawienia",
        entries.Single(entry => entry.CommandId == CommandIds.SettingsVolumeMessages).DisplayName);
    Equal(
        "Komunikaty odtwarzania i pauzy: włączone. Enter: ustawienia",
        entries.Single(entry => entry.CommandId == CommandIds.SettingsPlaybackMessages).DisplayName);
    Equal(
        "Oznajmianie automatycznie rozpoznanych utworów: włączone. Enter: ustawienia",
        entries.Single(entry => entry.CommandId == CommandIds.SettingsAutomaticRecognitionMessages).DisplayName);
    Equal(
        "Oznajmianie rozpoznanych utworów: włączone. Enter: przełącz",
        entries.Single(entry => entry.CommandId == CommandIds.ToggleRadioRecognitionAnnouncements).DisplayName);
    Equal(
        "Ctrl+Alt+Shift+S (Radio internetowe)",
        entries.Single(entry => entry.CommandId == CommandIds.ToggleRadioRecognitionAnnouncements).LocalShortcut);
    Equal(
        "Globalna normalizacja głośności: wyłączone. Enter: ustawienia",
        entries.Single(entry => entry.CommandId == CommandIds.SettingsLoudnessNormalization).DisplayName);
    Equal(
        "Globalne łagodne przejścia między utworami: wyłączone. Enter: ustawienia",
        entries.Single(entry => entry.CommandId == CommandIds.SettingsSmoothTrackTransitions).DisplayName);
    Equal(
        "Globalna cisza między utworami: bez dodatkowej ciszy. Enter: ustawienia",
        entries.Single(entry => entry.CommandId == CommandIds.SettingsInterTrackSilence).DisplayName);
    Equal(
        "Globalna normalizacja głośności: wyłączone. Enter: przełącz",
        entries.Single(entry => entry.CommandId == CommandIds.ToggleLoudnessNormalization).DisplayName);
    Equal(
        "Globalne łagodne przejścia między utworami: wyłączone. Enter: przełącz",
        entries.Single(entry => entry.CommandId == CommandIds.ToggleSmoothTrackTransitions).DisplayName);
    Equal(
        "Globalna cisza między utworami: bez dodatkowej ciszy. Enter: następna wartość",
        entries.Single(entry => entry.CommandId == CommandIds.CycleInterTrackSilence).DisplayName);
    Equal(
        "Komunikat po skoku cyfrą: tylko procent",
        entries.Single(entry => entry.CommandId == CommandIds.SettingsPercentageSeekAnnouncement).DisplayName);
    Equal("Ctrl+,", entries.Single(entry => entry.CommandId == CommandIds.SettingsGeneral).LocalShortcut);
    True(
        entries.Any(entry => entry.CommandId == CommandIds.SettingsImportFullBackup),
        "Paleta powinna udostępniać wszystkie bezpieczne wejścia do ustawień.");
    True(
        CommandPaletteSearch.Filter(entries, "szablony komunikatow")
            .Any(entry => entry.CommandId == CommandIds.SettingsMessageTemplates),
        "Ustawienia powinny być wyszukiwalne bez polskich znaków.");
    True(
        CommandPaletteSearch.Filter(entries, "ustawienia")
            .Any(entry => entry.CommandId == CommandIds.SettingsToggleMessages),
        "Wspólne wyszukiwanie ustawień powinno obejmować także bezpośrednie przełączniki.");

    settings.Messages.Enabled = false;
    settings.Messages.DetailedHints = true;
    settings.Messages.SeekMessages = false;
    settings.Messages.ArrowSeekMessages = false;
    settings.Messages.PercentageSeekMessages = false;
    settings.Messages.BookmarkNavigationMessages = false;
    settings.Messages.VolumeMessages = false;
    settings.Messages.PlaybackMessages = false;
    settings.Messages.AutomaticRecognitionMessages = false;
    settings.Messages.PercentageSeekAnnouncement = PercentageSeekAnnouncementMode.PercentAndTime;
    settings.Audio.LoudnessNormalizationEnabled = true;
    settings.Audio.SmoothTrackTransitionsEnabled = true;
    settings.Audio.InterTrackSilenceMilliseconds = 2000;
    var changedEntries = CommandPaletteSearch.CreateEntries(profile, settings);
    Equal(
        "Komunikaty dostępności: wyłączone. Enter: włącz",
        changedEntries.Single(entry => entry.CommandId == CommandIds.SettingsToggleMessages).DisplayName);
    Equal(
        "Szczegółowe podpowiedzi klawiatury: włączone. Enter: wyłącz",
        changedEntries.Single(entry => entry.CommandId == CommandIds.SettingsToggleDetailedHints).DisplayName);
    Equal(
        "Automatyczne komunikaty odtwarzacza: wyłączone. Enter: włącz",
        changedEntries.Single(entry => entry.CommandId == CommandIds.SettingsToggleSeekMessages).DisplayName);
    Equal(
        "Komunikaty przewijania strzałkami: wyłączone. Enter: ustawienia",
        changedEntries.Single(entry => entry.CommandId == CommandIds.SettingsArrowSeekMessages).DisplayName);
    Equal(
        "Komunikaty nawigacji po zakładkach: wyłączone. Enter: ustawienia",
        changedEntries.Single(entry => entry.CommandId == CommandIds.SettingsBookmarkNavigationMessages).DisplayName);
    Equal(
        "Oznajmianie automatycznie rozpoznanych utworów: wyłączone. Enter: ustawienia",
        changedEntries.Single(entry => entry.CommandId == CommandIds.SettingsAutomaticRecognitionMessages).DisplayName);
    Equal(
        "Oznajmianie rozpoznanych utworów: wyłączone. Enter: przełącz",
        changedEntries.Single(entry => entry.CommandId == CommandIds.ToggleRadioRecognitionAnnouncements).DisplayName);
    Equal(
        "Globalna normalizacja głośności: włączone. Enter: ustawienia",
        changedEntries.Single(entry => entry.CommandId == CommandIds.SettingsLoudnessNormalization).DisplayName);
    Equal(
        "Globalne łagodne przejścia między utworami: włączone. Enter: ustawienia",
        changedEntries.Single(entry => entry.CommandId == CommandIds.SettingsSmoothTrackTransitions).DisplayName);
    Equal(
        "Globalna cisza między utworami: 2 sekundy. Enter: ustawienia",
        changedEntries.Single(entry => entry.CommandId == CommandIds.SettingsInterTrackSilence).DisplayName);
    Equal(
        "Globalna normalizacja głośności: włączone. Enter: przełącz",
        changedEntries.Single(entry => entry.CommandId == CommandIds.ToggleLoudnessNormalization).DisplayName);
    Equal(
        "Globalne łagodne przejścia między utworami: włączone. Enter: przełącz",
        changedEntries.Single(entry => entry.CommandId == CommandIds.ToggleSmoothTrackTransitions).DisplayName);
    Equal(
        "Globalna cisza między utworami: 2 sekundy. Enter: następna wartość",
        changedEntries.Single(entry => entry.CommandId == CommandIds.CycleInterTrackSilence).DisplayName);
    Equal(
        "Komunikat po skoku cyfrą: procent i czas",
        changedEntries.Single(entry => entry.CommandId == CommandIds.SettingsPercentageSeekAnnouncement).DisplayName);
}

static void TestShortcutHelpCatalog()
{
    var sections = ShortcutHelpCatalog.Create(KeyboardProfile.CreateDefault(), new AppSettings());
    True(sections.Count >= 8, "Pomoc powinna zachować pełną hierarchię sekcji.");
    True(sections.All(section => section.Entries.Count > 0), "Puste sekcje nie powinny trafiać do okna Pomocy.");
    Equal(sections[0].Label, sections[0].ToString());

    var entries = sections.SelectMany(section => section.Entries).ToArray();
    var favorites = entries.Single(entry => entry.CommandId == CommandIds.ViewFavorites);
    True(favorites.Label.Contains("Pokaż ulubione", StringComparison.Ordinal), "Etykieta musi zawierać nazwę polecenia.");
    True(favorites.Label.Contains("Ctrl+U", StringComparison.Ordinal), "Etykieta musi zawierać skrót okna.");
    True(favorites.Label.Contains("po prefiksie U", StringComparison.Ordinal), "Etykieta musi zawierać aktywny skrót prefiksowy.");
    Equal(favorites.Label, favorites.ToString());

    var keyboardHelp = entries.Single(entry => entry.CommandId == CommandIds.KeyboardHelp);
    True(keyboardHelp.CanExecute, "Pomoc klawiatury powinna być uruchamiana z listy skrótów.");
    True(keyboardHelp.Label.Contains("Ctrl+F1", StringComparison.Ordinal), "Pomoc klawiatury musi podawać Ctrl+F1.");
    True(entries.Single(entry => entry.CommandId == CommandIds.Help).CanExecute == false,
        "Okno Pomocy nie powinno otwierać samo siebie.");
    True(entries.Any(entry => entry.Shortcut == "Ctrl+C"), "Spis powinien obejmować bezpieczne kopiowanie nazw.");
    True(entries.Any(entry => entry.Shortcut == "Shift+Delete"), "Spis powinien wyjaśniać osobną operację Kosza.");
    Equal("Ctrl+Shift+S lub Ctrl+0; po prefiksie 0",
        entries.Single(entry => entry.CommandId == CommandIds.SessionList).Shortcut);
    var normalizationHelp = entries.Single(entry => entry.CommandId == CommandIds.ToggleLoudnessNormalization);
    True(normalizationHelp.Shortcut.StartsWith("Shift+N;", StringComparison.Ordinal),
        "Spis powinien podawać lokalny skrót normalizacji przed wariantem prefiksowym.");
    True(normalizationHelp.Context.Contains("Odtwarzacz obsługujący przetwarzanie dźwięku", StringComparison.Ordinal),
        "Spis powinien oddzielnie podawać kontekst obsługiwanej normalizacji.");
    True(normalizationHelp.Label
            .Contains("po prefiksie Shift+N", StringComparison.Ordinal),
        "Spis powinien podawać prefiksowy skrót normalizacji.");
    var recognitionAnnouncementsHelp = entries.Single(
        entry => entry.CommandId == CommandIds.ToggleRadioRecognitionAnnouncements);
    Equal("Ctrl+Alt+Shift+S", recognitionAnnouncementsHelp.Shortcut);
    True(recognitionAnnouncementsHelp.Context.Contains("Radio internetowe", StringComparison.Ordinal),
        "Skrót oznajmiania rozpoznań musi mieć jednoznaczny kontekst Radia.");
    var transitionsHelp = entries.Single(entry => entry.CommandId == CommandIds.ToggleSmoothTrackTransitions);
    True(transitionsHelp.Shortcut.StartsWith("Shift+T;", StringComparison.Ordinal),
        "Spis powinien podawać lokalny skrót przejść przed wariantem prefiksowym.");
    True(transitionsHelp.Context.Contains("Odtwarzacz obsługujący przetwarzanie dźwięku", StringComparison.Ordinal),
        "Spis powinien oddzielnie podawać kontekst obsługiwanych przejść.");
    True(transitionsHelp.Label
            .Contains("po prefiksie T", StringComparison.Ordinal),
        "Spis powinien podawać prefiksowy skrót przejść.");
    var silenceHelp = entries.Single(entry => entry.CommandId == CommandIds.CycleInterTrackSilence);
    True(silenceHelp.Shortcut.StartsWith("Shift+C;", StringComparison.Ordinal),
        "Spis powinien podawać lokalny skrót ciszy przed wariantem prefiksowym.");
    True(silenceHelp.Context.Contains("Odtwarzacz obsługujący przetwarzanie dźwięku", StringComparison.Ordinal),
        "Spis powinien oddzielnie podawać kontekst obsługiwanej ciszy.");
    True(silenceHelp.Label
            .Contains("po prefiksie C", StringComparison.Ordinal),
        "Spis powinien podawać prefiksowy skrót ciszy.");
    True(entries.All(entry => !entry.Label.Contains("CommandId", StringComparison.Ordinal)
        && !entry.Label.Contains("{", StringComparison.Ordinal)),
        "Dostępne etykiety nie mogą ujawniać technicznego zapisu obiektów.");

    var filtered = ShortcutHelpCatalog.Filter(sections, "pomoc klawiatury");
    Equal(1, filtered.Count(entry => entry.CommandId == CommandIds.KeyboardHelp));
    True(ShortcutHelpCatalog.Filter(sections, "kosza").Any(entry => entry.Shortcut == "Shift+Delete"),
        "Wyszukiwanie musi obejmować opisy informacyjne.");

    var customProfile = KeyboardProfile.CreateDefault().CreateEditableCopy("Test");
    customProfile.Bindings.Remove("U");
    customProfile.Bindings["Y"] = CommandIds.ViewFavorites;
    var customFavorites = ShortcutHelpCatalog.Create(customProfile, new AppSettings())
        .SelectMany(section => section.Entries)
        .Single(entry => entry.CommandId == CommandIds.ViewFavorites);
    True(customFavorites.Label.Contains("po prefiksie Y", StringComparison.Ordinal),
        "Spis powinien od razu odzwierciedlać aktywny profil klawiatury.");
}

static void TestMembershipHistory()
{
    var item = new MediaItem
    {
        Title = "Element do przywrócenia",
        IsFavorite = true,
        IsInLibrary = true,
        IsInQueue = true,
        IsPlayNext = true
    };
    var history = new MediaMembershipHistory();
    var originalState = MediaMembershipState.From(item);

    item.IsInLibrary = false;
    item.IsInQueue = false;
    item.IsPlayNext = false;
    history.Record("tidal", item, originalState, "Przywrócono element");
    Equal(1, history.Count);

    var undo = history.Undo();
    True(undo is not null, "Historia powinna zwrócić ostatnią zmianę.");
    Equal("tidal", undo!.SessionId);
    Equal("Przywrócono element", undo.Announcement);
    Equal(originalState, MediaMembershipState.From(item));
    Equal(0, history.Count);
    True(history.Undo() is null, "Pusta historia nie powinna zwracać zmiany.");

    history.Record("tidal", item, MediaMembershipState.From(item), "Bez zmiany");
    Equal(0, history.Count);

    var second = new MediaItem { Title = "Drugi element", IsFavorite = true };
    var firstBeforeBatch = MediaMembershipState.From(item);
    var secondBeforeBatch = MediaMembershipState.From(second);
    item.IsFavorite = false;
    second.IsFavorite = false;
    history.RecordBatch(
        "tidal",
        [(item, firstBeforeBatch), (second, secondBeforeBatch)],
        "Przywrócono dwa elementy");
    Equal(1, history.Count);
    var batchUndo = history.Undo();
    Equal(2, batchUndo!.Items.Count);
    True(item.IsFavorite && second.IsFavorite, "Jedno cofnięcie powinno przywrócić całą zmianę zbiorową.");

    var stalePodcast = new MediaItem
    {
        Id = "podcast-restored-after-refresh",
        Title = "Podcast po przebudowie listy",
        Kind = MediaItemKind.Podcast,
        IsInLibrary = true
    };
    var podcastBeforeRemoval = MediaMembershipState.From(stalePodcast);
    stalePodcast.IsInLibrary = false;
    history.Record("podcasts", stalePodcast, podcastBeforeRemoval, "Przywrócono podcast");
    var currentPodcast = new MediaItem
    {
        Id = stalePodcast.Id,
        Title = stalePodcast.Title,
        Kind = MediaItemKind.Podcast,
        IsInLibrary = false
    };
    var podcastUndo = history.Undo((sessionId, itemId) =>
        string.Equals(sessionId, "podcasts", StringComparison.Ordinal)
        && string.Equals(itemId, currentPodcast.Id, StringComparison.Ordinal)
            ? currentPodcast
            : null);
    True(podcastUndo is not null, "Cofnięcie podcastu powinno zostać znalezione po przebudowie listy.");
    True(currentPodcast.IsInLibrary,
        "Ctrl+Z powinno przywrócić bieżący obiekt podcastu, a nie tylko nieaktualny element starej listy.");
    True(stalePodcast.IsInLibrary,
        "Obiekt zapisany w historii powinien pozostać zgodny z bieżącym elementem.");

    foreach (var sessionId in new[] { "local", "radio", "tidal", "appleMusic", "wiim" })
    {
        var stale = new MediaItem
        {
            Id = $"{sessionId}-rebuilt-item",
            Title = $"Przebudowany element {sessionId}",
            IsFavorite = true,
            IsInLibrary = true,
            IsInQueue = true
        };
        var before = MediaMembershipState.From(stale);
        stale.IsFavorite = false;
        stale.IsInLibrary = false;
        stale.IsInQueue = false;
        history.Record(sessionId, stale, before, $"Przywrócono {sessionId}");
        var current = new MediaItem
        {
            Id = stale.Id,
            Title = stale.Title
        };
        var serviceUndo = history.Undo((resolvedSessionId, itemId) =>
            string.Equals(resolvedSessionId, sessionId, StringComparison.Ordinal)
            && string.Equals(itemId, current.Id, StringComparison.Ordinal)
                ? current
                : null);
        True(serviceUndo is not null && MediaMembershipState.From(current) == before,
            $"Ctrl+Z nie przywrócił aktualnego rekordu po przebudowie sesji {sessionId}.");
    }

    var firstFavorite = new MediaItem { Id = "favorite-a", Title = "Pierwszy", IsFavorite = true };
    var middleFavorite = new MediaItem { Id = "favorite-b", Title = "Środkowy", IsFavorite = true };
    var lastFavorite = new MediaItem { Id = "favorite-c", Title = "Ostatni", IsFavorite = true };
    var favoriteOrder = new List<string> { firstFavorite.Id, middleFavorite.Id, lastFavorite.Id };
    var middlePosition = LocalLibraryManualOrder.CapturePositions(favoriteOrder, [middleFavorite.Id])
        .Select(entry => new MediaMembershipOrderPosition(entry.ItemId, entry.Index))
        .ToArray();
    var middleBeforeRemoval = MediaMembershipState.From(middleFavorite);
    middleFavorite.IsFavorite = false;
    history.Record(
        "local",
        middleFavorite,
        middleBeforeRemoval,
        "Przywrócono ulubiony",
        orderSnapshot: new MediaMembershipOrderSnapshot("favorites", middlePosition));
    favoriteOrder = LocalLibraryManualOrder.Normalize(
        favoriteOrder,
        [firstFavorite, lastFavorite]);
    True(
        favoriteOrder.SequenceEqual([firstFavorite.Id, lastFavorite.Id]),
        "Usunięty ulubiony znika z bieżącego porządku kolekcji.");
    var favoriteUndo = history.Undo();
    True(favoriteUndo?.OrderSnapshot is not null, "Historia powinna zachować pozycję w kolekcji.");
    LocalLibraryManualOrder.RestorePositions(
        favoriteOrder,
        favoriteUndo!.OrderSnapshot!.Positions.Select(entry =>
            new ManualOrderPosition(entry.ItemId, entry.Index)));
    True(
        favoriteOrder.SequenceEqual([firstFavorite.Id, middleFavorite.Id, lastFavorite.Id]),
        "Ctrl+Z powinno przywrócić ulubiony dokładnie w środkowym miejscu, a nie na końcu.");
}

static void TestBatchMembershipCommands()
{
    var settings = new AppSettings();
    var sessions = new SessionManager(settings);
    var first = sessions.Current.Items[0];
    var second = sessions.Current.Items[1];
    first.IsFavorite = false;
    second.IsFavorite = false;
    first.IsInQueue = false;
    second.IsInQueue = false;
    first.IsPlayNext = false;
    second.IsPlayNext = false;
    var sink = new FakeSink();
    var actions = new FakeActions(first, [first, second]);
    var router = new CommandRouter(sessions, settings, sink, actions);

    router.Execute(CommandIds.ToggleFavorite);
    Equal(true, first.IsFavorite);
    Equal(true, second.IsFavorite);
    True(sink.LastMessage.Contains("2 elementy", StringComparison.Ordinal), "Komunikat powinien podawać liczbę elementów.");
    router.Execute(CommandIds.ToggleFavorite);
    Equal(false, first.IsFavorite);
    Equal(false, second.IsFavorite);

    router.Execute(CommandIds.AddQueue);
    Equal(true, first.IsInQueue);
    Equal(true, second.IsInQueue);
    router.Execute(CommandIds.AddQueue);
    Equal(false, first.IsInQueue);
    Equal(false, second.IsInQueue);

    var duplicateLogicalItem = new MediaItem
    {
        Id = first.Id,
        Title = "Powtórzony wiersz tej samej stacji"
    };
    var duplicateSink = new FakeSink();
    var duplicateRouter = new CommandRouter(
        sessions,
        settings,
        duplicateSink,
        new FakeActions(first, [first, duplicateLogicalItem]));
    duplicateRouter.Execute(CommandIds.ToggleFavorite);
    Equal(true, first.IsFavorite);
    Equal(false, duplicateLogicalItem.IsFavorite);
    True(
        !duplicateSink.LastMessage.Contains("2 elementy", StringComparison.Ordinal),
        "Dwa wiersze o tym samym identyfikatorze nie mogą zostać policzone jako dwa elementy.");
}

static void TestFolderMembershipGuard()
{
    var settings = new AppSettings();
    var sessions = new SessionManager(settings);
    var folder = new MediaItem
    {
        Id = "folder:test",
        Title = "Testowy folder",
        Kind = MediaItemKind.Folder,
        Source = @"C:\Muzyka\Test"
    };
    var sink = new FakeSink();
    var router = new CommandRouter(sessions, settings, sink, new FakeActions(folder));

    foreach (var commandId in new[]
             {
                 CommandIds.ToggleFavorite,
                 CommandIds.ToggleLibrary,
                 CommandIds.AddQueue,
                 CommandIds.TogglePlayNext
             })
    {
        var result = router.Execute(commandId);
        True(result.Handled, "Polecenie folderu powinno zostać bezpiecznie obsłużone.");
        True(sink.LastMessage.Contains("folder", StringComparison.OrdinalIgnoreCase),
            "Komunikat powinien wyjaśniać semantykę folderu.");
        True(!folder.IsFavorite && !folder.IsInLibrary && !folder.IsInQueue && !folder.IsPlayNext,
            "Sztuczny wiersz folderu nie może otrzymać stanu kolekcji.");
    }
}

static void TestFolderContentsMembership()
{
    var first = new MediaItem
    {
        Id = "folder-first",
        Title = "Pierwszy",
        IsInQueue = true,
        IsPlayNext = true,
        IsFavorite = true
    };
    var second = new MediaItem
    {
        Id = "folder-second",
        Title = "Drugi"
    };
    MediaItem[] items = [first, second];

    Equal(false, FolderContentsMembership.ToggleQueue(items));
    True(items.All(item => !item.IsInQueue && !item.IsPlayNext),
        "Częściowo wykorzystana Kolejka folderu powinna zostać całkowicie wyczyszczona.");
    Equal(true, FolderContentsMembership.ToggleQueue(items));
    True(items.All(item => item.IsInQueue),
        "Całkowicie pusty stan powinien ponownie dodać zawartość folderu do Kolejki.");

    first.IsPlayNext = true;
    second.IsPlayNext = false;
    Equal(false, FolderContentsMembership.TogglePlayNext(items));
    True(items.All(item => !item.IsPlayNext),
        "Częściowy stan odtwarzania jako następne powinien zostać wyczyszczony.");

    Equal(false, FolderContentsMembership.ToggleFavorites(items));
    True(items.All(item => !item.IsFavorite),
        "Częściowy stan Ulubionych folderu powinien zostać całkowicie wyczyszczony.");
    Equal(true, FolderContentsMembership.ToggleFavorites(items));
    True(items.All(item => item.IsFavorite),
        "Pusty stan powinien dodać całą zawartość folderu do Ulubionych.");
}

static void TestTimeCommands()
{
    var settings = new AppSettings();
    var sessions = new SessionManager(settings);
    var sink = new FakeSink();
    var actions = new FakeActions(sessions.Current.CurrentItem);
    var router = new CommandRouter(sessions, settings, sink, actions);

    router.Execute(CommandIds.TimeElapsed);
    Equal("1:23", sink.LastMessage);
    router.Execute(CommandIds.TimeTotal);
    True(!sink.LastMessage.Contains("czas", StringComparison.OrdinalIgnoreCase), "Komunikat czasu powinien zawierać tylko wartość.");
    settings.Messages.SeekMessages = false;
    settings.Messages.VolumeMessages = false;
    var messageBeforeSilentSeek = sink.LastMessage;
    router.Execute(CommandIds.VolumeUp5);
    Equal(40, sessions.Current.Volume);
    Equal(messageBeforeSilentSeek, sink.LastMessage);
    router.Execute(CommandIds.SeekForward30);
    Equal(TimeSpan.FromSeconds(113), sessions.Current.Position);
    Equal(messageBeforeSilentSeek, sink.LastMessage);
    router.Execute(CommandIds.SeekBackward30);
    Equal(TimeSpan.FromSeconds(83), sessions.Current.Position);
    router.Execute(CommandIds.TrackEnd);
    Equal(sessions.Current.CurrentItem.Duration - TimeSpan.FromSeconds(10), sessions.Current.Position);
    Equal(messageBeforeSilentSeek, sink.LastMessage);
    router.Execute(CommandIds.TimeElapsed);
    Equal(CommandRouter.FormatTime(sessions.Current.Position), sink.LastMessage);
    var messageBeforePercentSeek = sink.LastMessage;
    router.Execute(CommandIds.SeekPercent(50));
    Equal(
        TimeSpan.FromTicks((long)Math.Round(sessions.Current.CurrentItem.Duration.Ticks * 0.5d)),
        sessions.Current.Position);
    Equal(messageBeforePercentSeek, sink.LastMessage);
    router.Execute(CommandIds.TimeElapsed);
    Equal(CommandRouter.FormatTime(sessions.Current.Position), sink.LastMessage);
    settings.Messages.SeekMessages = true;
    settings.Messages.VolumeMessages = true;
    router.Execute(CommandIds.SeekPercent(90));
    Equal(
        TimeSpan.FromTicks((long)Math.Round(sessions.Current.CurrentItem.Duration.Ticks * 0.9d)),
        sessions.Current.Position);
    Equal("90%", sink.LastMessage);
    router.Execute(CommandIds.SeekPercent(0));
    Equal(TimeSpan.Zero, sessions.Current.Position);
    Equal("0%", sink.LastMessage);
    settings.Messages.PercentageSeekAnnouncement = PercentageSeekAnnouncementMode.Time;
    router.Execute(CommandIds.SeekPercent(50));
    Equal(CommandRouter.FormatTime(sessions.Current.Position), sink.LastMessage);
    settings.Messages.PercentageSeekAnnouncement = PercentageSeekAnnouncementMode.PercentAndTime;
    router.Execute(CommandIds.SeekPercent(20));
    Equal($"20%, {CommandRouter.FormatTime(sessions.Current.Position)}", sink.LastMessage);
    settings.Messages.ArrowSeekMessages = false;
    var messageBeforeSilentArrowSeek = sink.LastMessage;
    router.Execute(CommandIds.SeekForward10);
    Equal(messageBeforeSilentArrowSeek, sink.LastMessage);
    settings.Messages.ArrowSeekMessages = true;
    settings.Messages.PercentageSeekMessages = false;
    var messageBeforeSilentPercentageSeek = sink.LastMessage;
    router.Execute(CommandIds.SeekPercent(30));
    Equal(messageBeforeSilentPercentageSeek, sink.LastMessage);
    settings.Messages.PercentageSeekMessages = true;
    settings.Messages.VolumeMessages = false;
    var messageBeforeSilentVolume = sink.LastMessage;
    router.Execute(CommandIds.VolumeUp5);
    Equal(messageBeforeSilentVolume, sink.LastMessage);
    settings.Messages.VolumeMessages = true;
    router.Execute(CommandIds.VolumeDown5);
    Equal("40%", sink.LastMessage);
    router.Execute(CommandIds.ToggleMuteCurrentSession);
    Equal("Wyciszono: TIDAL", sink.LastMessage);
    Equal(true, sessions.Current.IsSessionMuted);
    router.Execute(CommandIds.ToggleMuteAllSessions);
    Equal("Wyciszono wszystkie sesje AMC", sink.LastMessage);
    Equal(true, sessions.AllSessionsMuted);
    router.Execute(CommandIds.ToggleMuteAllSessions);
    Equal("Wyłączono wyciszenie wszystkich sesji AMC. Indywidualnie wyciszonych: 1", sink.LastMessage);
    Equal(true, sessions.Current.IsMuted);
    router.Execute(CommandIds.ToggleMuteCurrentSession);
    Equal("Przywrócono dźwięk: TIDAL", sink.LastMessage);
    Equal(false, sessions.Current.IsMuted);
    settings.Messages.PlaybackMessages = false;
    var messageBeforeSilentPlayback = sink.LastMessage;
    router.Execute(CommandIds.PlayPause);
    Equal(messageBeforeSilentPlayback, sink.LastMessage);
    router.Execute(CommandIds.PlayPause);
    Equal(messageBeforeSilentPlayback, sink.LastMessage);
    settings.Messages.PlaybackMessages = true;
    router.Execute(CommandIds.ActivateSelected);
    Equal("Odtwarzanie: Pierwszy utwór demonstracyjny", sink.LastMessage);
    router.Execute(CommandIds.ActivateSelected);
    Equal("Pauza: Pierwszy utwór demonstracyjny", sink.LastMessage);
    router.Execute(CommandIds.ActivateSelected);
    Equal("Odtwarzanie: Pierwszy utwór demonstracyjny", sink.LastMessage);
    router.Execute(CommandIds.PlayPause);
    Equal("Pauza: Pierwszy utwór demonstracyjny", sink.LastMessage);
    router.Execute(CommandIds.PlayPause);
    Equal("Odtwarzanie: Pierwszy utwór demonstracyjny", sink.LastMessage);
    router.Execute(CommandIds.ToggleFavorite);
    Equal("Usunięto z ulubionych: Pierwszy utwór demonstracyjny", sink.LastMessage);
    router.Execute(CommandIds.ToggleFavorite);
    Equal("Dodano do ulubionych: Pierwszy utwór demonstracyjny", sink.LastMessage);
    router.Execute(CommandIds.AddQueue);
    Equal("Dodano do kolejki: Pierwszy utwór demonstracyjny", sink.LastMessage);
    router.Execute(CommandIds.AddQueue);
    Equal("Usunięto z kolejki: Pierwszy utwór demonstracyjny", sink.LastMessage);
    router.Execute(CommandIds.TogglePlayNext);
    Equal("Odtwarzaj jako następne: Pierwszy utwór demonstracyjny", sink.LastMessage);
    router.Execute(CommandIds.TogglePlayNext);
    Equal("Usunięto z następnych: Pierwszy utwór demonstracyjny", sink.LastMessage);
    router.Execute(CommandIds.CommandPalette);
    True(actions.CommandPaletteShown, "Router powinien otworzyć paletę poleceń przez interfejs aplikacji.");
    router.Execute(CommandIds.RefreshLocalLibrary);
    True(actions.LocalLibraryRefreshed, "Router powinien przekazać ręczne odświeżenie lokalnej biblioteki.");
    router.Execute(CommandIds.ManageLocalSources);
    True(actions.LocalSourceManagerShown, "Router powinien otworzyć menedżer źródeł Biblioteki.");
    router.Execute(CommandIds.RenameLibraryItem);
    True(actions.LibraryItemRenameShown, "Router powinien otworzyć zmianę nazwy w Bibliotece.");
    router.Execute(CommandIds.RenameLocalFile);
    True(actions.LocalFileRenameShown, "Router powinien otworzyć zmianę nazwy pliku na dysku.");
    router.Execute(CommandIds.MoveLocalLibraryItemUp);
    Equal(-1, actions.LocalLibraryMoveDirection);
    router.Execute(CommandIds.MoveLocalLibraryItemDown);
    Equal(1, actions.LocalLibraryMoveDirection);
    router.Execute(CommandIds.SeekToTime);
    True(actions.SeekToTimeShown, "Router powinien otworzyć okno skoku do czasu.");
    router.Execute(CommandIds.SeekToPercentage);
    True(actions.SeekToPercentageShown, "Router powinien otworzyć okno skoku do procentu.");
    router.Execute(CommandIds.ItemProperties);
    True(actions.ItemPropertiesShown, "Router powinien otworzyć jedno okno właściwości i informacji.");
    router.Execute(CommandIds.PodcastDescription);
    True(actions.PodcastDescriptionShown, "Router powinien przekazać otwarcie pełnego opisu podcastu.");
    router.Execute(CommandIds.CurrentBroadcastInformation);
    True(actions.CurrentBroadcastInformationAnnounced, "Router powinien przekazać szybki odczyt bieżącej audycji.");
    router.Execute(CommandIds.GoToPodcast);
    True(actions.RelatedPodcastShown, "Router powinien przekazać przejście do podcastu nadrzędnego.");
    router.Execute(CommandIds.AddNamedBookmark);
    True(actions.NamedBookmarkAdded, "Router powinien przekazać dodanie nazwanej zakładki do aplikacji.");
    router.Execute(CommandIds.SettingsMessageTemplates);
    Equal(SettingsTarget.MessageTemplates, actions.LastSettingsTarget);
    router.Execute(CommandIds.SettingsPercentageSeekAnnouncement);
    Equal(SettingsTarget.PercentageSeekAnnouncement, actions.LastSettingsTarget);
    router.Execute(CommandIds.SettingsArrowSeekMessages);
    Equal(SettingsTarget.ArrowSeekMessages, actions.LastSettingsTarget);
    router.Execute(CommandIds.SettingsPercentageSeekMessages);
    Equal(SettingsTarget.PercentageSeekMessages, actions.LastSettingsTarget);
    router.Execute(CommandIds.SettingsBookmarkNavigationMessages);
    Equal(SettingsTarget.BookmarkNavigationMessages, actions.LastSettingsTarget);
    router.Execute(CommandIds.SettingsVolumeMessages);
    Equal(SettingsTarget.VolumeMessages, actions.LastSettingsTarget);
    router.Execute(CommandIds.SettingsPlaybackMessages);
    Equal(SettingsTarget.PlaybackMessages, actions.LastSettingsTarget);
    router.Execute(CommandIds.SettingsAutomaticRecognitionMessages);
    Equal(SettingsTarget.AutomaticRecognitionMessages, actions.LastSettingsTarget);
    router.Execute(CommandIds.SettingsRadioRecognitionScope);
    Equal(SettingsTarget.RadioRecognitionScope, actions.LastSettingsTarget);
    router.Execute(CommandIds.SettingsPausePlaybackWhenLeavingPlayer);
    Equal(SettingsTarget.PausePlaybackWhenLeavingPlayer, actions.LastSettingsTarget);
    router.Execute(CommandIds.SettingsFollowPlaybackOnPlayerExit);
    Equal(SettingsTarget.FollowPlaybackOnPlayerExit, actions.LastSettingsTarget);
    router.Execute(CommandIds.SettingsOpenPlayerWhenActivatingPreset);
    Equal(SettingsTarget.OpenPlayerWhenActivatingPreset, actions.LastSettingsTarget);
    router.Execute(CommandIds.SettingsRememberLocalPlaybackPositions);
    Equal(SettingsTarget.RememberLocalPlaybackPositions, actions.LastSettingsTarget);
    router.Execute(CommandIds.SettingsToggleMessages);
    True(actions.MessagesToggled, "Router powinien przekazać przełączenie komunikatów do aplikacji.");
    router.Execute(CommandIds.SettingsToggleDetailedHints);
    True(actions.DetailedHintsToggled, "Router powinien przekazać przełączenie szczegółowych podpowiedzi do aplikacji.");
    router.Execute(CommandIds.SettingsToggleSeekMessages);
    True(actions.SeekMessagesToggled, "Router powinien przekazać przełączenie odczytu przewijania do aplikacji.");
}

static void TestSeekInputParser()
{
    True(SeekInputParser.TryParseTime("35", out var minutes, out _), "Sama liczba powinna oznaczać minuty.");
    Equal(TimeSpan.FromMinutes(35), minutes);
    True(SeekInputParser.TryParseTime("1:35", out var minuteSeconds, out _), "Format minuty:sekundy powinien działać.");
    Equal(TimeSpan.FromSeconds(95), minuteSeconds);
    True(SeekInputParser.TryParseTime("1:02:30", out var hourTime, out _), "Format godziny:minuty:sekundy powinien działać.");
    Equal(new TimeSpan(1, 2, 30), hourTime);
    True(!SeekInputParser.TryParseTime("1:60", out _, out _), "Sekundy 60 nie mogą być przyjęte.");
    True(!SeekInputParser.TryParseTime("-1", out _, out _), "Czas ujemny nie może być przyjęty.");

    True(SeekInputParser.TryParsePercentage("35", out var percent, out _), "Procent bez znaku powinien działać.");
    Equal(35, percent);
    True(SeekInputParser.TryParsePercentage("100%", out percent, out _), "Procent ze znakiem powinien działać.");
    Equal(100, percent);
    True(!SeekInputParser.TryParsePercentage("101", out _, out _), "Procent ponad 100 nie może być przyjęty.");
}

static void TestLocalFolderSourcePolicy()
{
    var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "AMC-library"));
    var child = Path.Combine(root, "Podcasty");
    var parent = Directory.GetParent(root)?.FullName ?? Path.GetPathRoot(root)!;
    var sources = new List<LocalFolderSourceSettings>
    {
        new()
        {
            Id = "source-1",
            DisplayName = "Biblioteka",
            Path = root,
            ResumePositionMode = ResumePositionMode.StartFromBeginning
        }
    };

    Equal(
        LocalFolderSourceConflictKind.SameSource,
        LocalFolderSourcePolicy.FindConflict(sources, root)!.Kind);
    Equal(
        LocalFolderSourceConflictKind.CoveredByExistingSource,
        LocalFolderSourcePolicy.FindConflict(sources, child)!.Kind);
    Equal(
        LocalFolderSourceConflictKind.ContainsExistingSource,
        LocalFolderSourcePolicy.FindConflict(sources, parent)!.Kind);

    var items = new List<LocalMediaItemSettings>
    {
        new() { Id = "one", Path = Path.Combine(root, "one.mp3"), IsInLibrary = true, IsAvailable = true },
        new() { Id = "two", Path = Path.Combine(root, "two.ogg"), IsInLibrary = true, IsAvailable = false },
        new() { Id = "three", Path = Path.Combine(root, "three.wav"), IsInLibrary = false, IsAvailable = true }
    };
    var statuses = LocalFolderSourcePolicy.BuildStatuses(
        sources,
        items,
        [items[2].Path],
        _ => false);
    Equal(1, statuses.Count);
    Equal(false, statuses[0].IsReachable);
    Equal(1, statuses[0].ActiveItemCount);
    Equal(1, statuses[0].UnavailableItemCount);
    Equal(1, statuses[0].ExcludedItemCount);
    Equal(ResumePositionMode.StartFromBeginning, statuses[0].ResumePositionMode);
    True(statuses[0].Label.Contains("folder niedostępny", StringComparison.Ordinal),
        "Etykieta powinna jednoznacznie odróżniać stan folderu od liczników plików.");
    True(statuses[0].Label.Contains("Zawsze od początku", StringComparison.Ordinal),
        "Lista Folderów Biblioteki powinna podawać politykę pamiętania pozycji.");
    Equal(statuses[0].Label, statuses[0].ToString());
    True(!statuses[0].ToString().Contains(nameof(LocalFolderSourceStatus), StringComparison.Ordinal),
        "Nazwa dostępnościowa folderu nie może ujawniać technicznego zapisu rekordu.");

    True(LocalFolderSourcePolicy.DetachSource(sources, "source-1"), "Folder powinien dać się odłączyć.");
    Equal(0, sources.Count);
    Equal(3, items.Count);
    Equal(true, items[0].IsInLibrary);
}

static void TestUnavailableLocalItemPolicy()
{
    var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "AMC-unavailable"));
    var otherRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "AMC-other"));
    var state = ConfigurationStore.CreateDefaultState();
    state.LocalMedia.FolderSources.Add(new LocalFolderSourceSettings
    {
        Id = "folder-1",
        DisplayName = "Nagrania",
        Path = root
    });
    state.LocalMedia.FolderSources.Add(new LocalFolderSourceSettings
    {
        Id = "folder-2",
        DisplayName = "Inne",
        Path = otherRoot
    });
    state.LocalMedia.Items.AddRange(
    [
        new LocalMediaItemSettings
        {
            Id = "missing",
            Title = "Brakujący",
            Path = Path.Combine(root, "missing.mp3"),
            IsInLibrary = true,
            IsAvailable = false
        },
        new LocalMediaItemSettings
        {
            Id = "available",
            Title = "Dostępny",
            Path = Path.Combine(root, "available.mp3"),
            IsInLibrary = true,
            IsAvailable = true
        },
        new LocalMediaItemSettings
        {
            Id = "other-missing",
            Title = "Inny brakujący",
            Path = Path.Combine(otherRoot, "missing.mp3"),
            IsInLibrary = true,
            IsAvailable = false
        }
    ]);
    state.LocalMedia.CurrentItemId = "missing";
    state.LocalMedia.CustomOrderItemIds.AddRange(["available", "missing"]);
    state.Bookmarks.Entries.Add(new BookmarkEntry
    {
        Id = "bookmark",
        SessionId = "local",
        ItemId = "missing",
        ItemTitle = "Brakujący"
    });
    state.PlaybackHistory.ItemIdsBySession["local"] = ["missing", "available"];
    state.CollectionOrders.FavoriteItemIdsBySession["local"] = ["missing"];
    state.CollectionOrders.QueueItemIdsBySession["local"] = ["missing", "available"];
    state.Playlists.Entries.Add(new PlaylistEntry
    {
        Id = "playlist",
        SessionId = "local",
        Name = "Test",
        ItemIds = ["missing", "available"]
    });
    state.SessionNavigation.Sessions["local"] = new SessionNavigationState
    {
        SelectedItemIds = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Biblioteka"] = "missing"
        },
        PlaybackContextItemIds = ["available", "missing"]
    };

    var unavailable = UnavailableLocalItemPolicy.GetForFolder(state, "folder-1");
    Equal(1, unavailable.Count);
    Equal("missing", unavailable[0].Id);

    var removed = UnavailableLocalItemPolicy.Forget(
        state,
        "folder-1",
        ["missing", "available", "other-missing"]);
    Equal(1, removed.Count);
    Equal("missing", removed[0].Id);
    True(state.LocalMedia.Items.All(item => item.Id != "missing"),
        "Wybrany niedostępny rekord powinien zostać zapomniany.");
    True(state.LocalMedia.Items.Any(item => item.Id == "available")
        && state.LocalMedia.Items.Any(item => item.Id == "other-missing"),
        "Dostępny plik i rekord z innego folderu muszą pozostać.");
    Equal(null, state.LocalMedia.CurrentItemId);
    True(state.Bookmarks.Entries.All(entry => entry.ItemId != "missing"),
        "Zakładki zapomnianego rekordu powinny zostać usunięte.");
    True(state.PlaybackHistory.ItemIdsBySession["local"].SequenceEqual(["available"]),
        "Historia powinna usunąć tylko zapomniany identyfikator.");
    True(!state.CollectionOrders.FavoriteItemIdsBySession.ContainsKey("local"),
        "Pusty porządek Ulubionych powinien zostać usunięty.");
    True(state.CollectionOrders.QueueItemIdsBySession["local"].SequenceEqual(["available"]),
        "Kolejka powinna zachować pozostały identyfikator.");
    True(state.Playlists.Entries[0].ItemIds.SequenceEqual(["available"]),
        "Playlista powinna zachować pozostały element.");
    Equal(null, state.SessionNavigation.Sessions["local"].SelectedItemIds["Biblioteka"]);
    True(state.SessionNavigation.Sessions["local"].PlaybackContextItemIds.SequenceEqual(["available"]),
        "Kontekst odtwarzania nie może przechowywać zapomnianego identyfikatora.");
}

static void TestExports()
{
    var directory = Path.Combine(Path.GetTempPath(), $"amc-tests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        var store = new ConfigurationStore(Path.Combine(directory, "state.json"));
        var state = ConfigurationStore.CreateDefaultState();
        state.Settings.Messages.DetailedHints = true;
        state.Settings.Messages.SeekMessages = false;
        state.Settings.Messages.ArrowSeekMessages = false;
        state.Settings.Messages.PercentageSeekMessages = true;
        state.Settings.Messages.BookmarkNavigationMessages = false;
        state.Settings.Messages.VolumeMessages = false;
        state.Settings.Messages.PlaybackMessages = false;
        state.Settings.Messages.PercentageSeekAnnouncement = PercentageSeekAnnouncementMode.PercentAndTime;
        state.Settings.PausePlaybackWhenLeavingPlayer = false;
        state.Settings.OpenPlayerWhenActivatingPreset = true;
        state.Settings.RememberLocalPlaybackPositions = false;
        state.Bookmarks.Entries.Add(new BookmarkEntry
        {
            Id = "bookmark-1",
            SessionId = "tidal",
            SessionName = "TIDAL",
            ItemId = "tidal-1",
            ItemTitle = "Pierwszy utwór demonstracyjny",
            PositionTicks = TimeSpan.FromMinutes(2).Ticks,
            CreatedUtcTicks = DateTime.UtcNow.Ticks
        });
        state.LocalMedia.FolderSources.Add(new LocalFolderSourceSettings
        {
            Id = "local-source",
            DisplayName = "Nagrania",
            Path = Path.Combine(directory, "Nagrania"),
            ResumePositionMode = ResumePositionMode.Remember
        });
        state.LocalMedia.Items.Add(new LocalMediaItemSettings
        {
            Id = "local-item",
            Title = "Audycja",
            Path = Path.Combine(directory, "Nagrania", "audycja.mp3"),
            IsInLibrary = true,
            IsAvailable = false,
            ResumePositionTicks = TimeSpan.FromMinutes(12).Ticks
        });
        state.Playlists.Entries.Add(new PlaylistEntry
        {
            Id = "playlist-1",
            SessionId = "local",
            Name = "Do odsłuchu",
            CreatedUtcTicks = DateTime.UtcNow.Ticks,
            ItemIds = ["local-item"]
        });
        state.CollectionOrders.QueueItemIdsBySession["local"] = ["local-item"];
        var mapPath = Path.Combine(directory, "map.amckeys.json");
        var settingsPath = Path.Combine(directory, "settings.amcsettings.json");
        var backupPath = Path.Combine(directory, "all.amcbackup.json");

        store.Save(state);
        Equal(false, store.LoadOrCreate().Settings.Messages.SeekMessages);
        Equal(false, store.LoadOrCreate().Settings.Messages.ArrowSeekMessages);
        Equal(true, store.LoadOrCreate().Settings.Messages.PercentageSeekMessages);
        Equal(false, store.LoadOrCreate().Settings.Messages.BookmarkNavigationMessages);
        Equal(false, store.LoadOrCreate().Settings.Messages.VolumeMessages);
        Equal(false, store.LoadOrCreate().Settings.Messages.PlaybackMessages);
        Equal(PercentageSeekAnnouncementMode.PercentAndTime, store.LoadOrCreate().Settings.Messages.PercentageSeekAnnouncement);
        Equal(false, store.LoadOrCreate().Settings.PausePlaybackWhenLeavingPlayer);
        Equal(true, store.LoadOrCreate().Settings.OpenPlayerWhenActivatingPreset);
        Equal(false, store.LoadOrCreate().Settings.RememberLocalPlaybackPositions);

        store.ExportKeyboardMap(mapPath, state.KeyboardProfiles[0]);
        var importedProfile = store.ImportKeyboardMap(mapPath);
        True(!importedProfile.IsBuiltIn, "Importowana mapa musi być edytowalna.");

        store.ExportConfiguration(settingsPath, state.Settings);
        var importedSettings = store.ImportConfiguration(settingsPath, "default");
        Equal("default", importedSettings.ActiveKeyboardProfileId);
        Equal(MediaItemField.Title, importedSettings.Lists.FieldOrder[0]);
        Equal(true, importedSettings.Messages.DetailedHints);
        Equal(false, importedSettings.Messages.SeekMessages);
        Equal(false, importedSettings.Messages.ArrowSeekMessages);
        Equal(true, importedSettings.Messages.PercentageSeekMessages);
        Equal(false, importedSettings.Messages.BookmarkNavigationMessages);
        Equal(false, importedSettings.Messages.VolumeMessages);
        Equal(false, importedSettings.Messages.PlaybackMessages);
        Equal(PercentageSeekAnnouncementMode.PercentAndTime, importedSettings.Messages.PercentageSeekAnnouncement);
        Equal(false, importedSettings.PausePlaybackWhenLeavingPlayer);
        Equal(true, importedSettings.OpenPlayerWhenActivatingPreset);
        Equal(false, importedSettings.RememberLocalPlaybackPositions);

        store.ExportFullBackup(backupPath, state);
        var importedBackup = store.ImportFullBackup(backupPath);
        Equal(6, importedBackup.Settings.SessionSlots.Count);
        Equal(1, importedBackup.KeyboardProfiles.Count);
        Equal(false, importedBackup.Settings.Messages.SeekMessages);
        Equal(false, importedBackup.Settings.Messages.ArrowSeekMessages);
        Equal(true, importedBackup.Settings.Messages.PercentageSeekMessages);
        Equal(false, importedBackup.Settings.Messages.BookmarkNavigationMessages);
        Equal(false, importedBackup.Settings.Messages.VolumeMessages);
        Equal(false, importedBackup.Settings.Messages.PlaybackMessages);
        Equal(false, importedBackup.Settings.PausePlaybackWhenLeavingPlayer);
        Equal(true, importedBackup.Settings.OpenPlayerWhenActivatingPreset);
        Equal(false, importedBackup.Settings.RememberLocalPlaybackPositions);
        Equal(ResumePositionMode.Remember, importedBackup.LocalMedia.FolderSources[0].ResumePositionMode);
        Equal(PercentageSeekAnnouncementMode.PercentAndTime, importedBackup.Settings.Messages.PercentageSeekAnnouncement);
        Equal(1, importedBackup.Bookmarks.Entries.Count);
        Equal("tidal-1", importedBackup.Bookmarks.Entries[0].ItemId);
        Equal(1, importedBackup.LocalMedia.FolderSources.Count);
        Equal("local-source", importedBackup.LocalMedia.FolderSources[0].Id);
        Equal(1, importedBackup.LocalMedia.Items.Count);
        Equal(TimeSpan.FromMinutes(12).Ticks, importedBackup.LocalMedia.Items[0].ResumePositionTicks);
        Equal(1, importedBackup.Playlists.Entries.Count);
        Equal("Do odsłuchu", importedBackup.Playlists.Entries[0].Name);
        Equal("local-item", importedBackup.Playlists.Entries[0].ItemIds.Single());
        Equal("local-item", importedBackup.CollectionOrders.QueueItemIdsBySession["LOCAL"].Single());
    }
    finally
    {
        Directory.Delete(directory, true);
    }
}

static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Oczekiwano „{expected}”, otrzymano „{actual}”.");
    }
}

static void True(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

sealed class VirtualLargeReadStream(long length) : Stream
{
    private long _position;
    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length { get; } = length;
    public override long Position
    {
        get => _position;
        set => _position = value is >= 0 && value <= Length
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value));
    }
    public override void Flush() { }
    public override int Read(byte[] buffer, int offset, int count) =>
        Read(buffer.AsSpan(offset, count));
    public override int Read(Span<byte> buffer)
    {
        var count = (int)Math.Min(buffer.Length, Length - Position);
        for (var index = 0; index < count; index++)
        {
            buffer[index] = (byte)((Position + index) & 0xFF);
        }
        Position += count;
        return count;
    }
    public override long Seek(long offset, SeekOrigin origin)
    {
        Position = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => checked(Position + offset),
            SeekOrigin.End => checked(Length + offset),
            _ => throw new ArgumentOutOfRangeException(nameof(origin))
        };
        return Position;
    }
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

sealed class FakeSink : IAnnouncementSink
{
    public string LastMessage { get; private set; } = string.Empty;
    public void Announce(string message) => LastMessage = message;
}

sealed class FakeActions(MediaItem selectedItem, IReadOnlyList<MediaItem>? actionItems = null) : IApplicationActions
{
    public MediaItem? SelectedItem { get; } = selectedItem;
    public MediaItem? ActionItem => SelectedItem;
    public IReadOnlyList<MediaItem> ActionItems => actionItems ?? (SelectedItem is null ? [] : [SelectedItem]);
    public bool CommandPaletteShown { get; private set; }
    public bool KeyboardHelpToggled { get; private set; }
    public SettingsTarget? LastSettingsTarget { get; private set; }
    public bool MessagesToggled { get; private set; }
    public bool DetailedHintsToggled { get; private set; }
    public bool SeekMessagesToggled { get; private set; }
    public bool SeekToTimeShown { get; private set; }
    public bool SeekToPercentageShown { get; private set; }
    public bool ItemPropertiesShown { get; private set; }
    public bool PodcastDescriptionShown { get; private set; }
    public bool CurrentBroadcastInformationAnnounced { get; private set; }
    public bool RelatedPodcastShown { get; private set; }
    public bool ItemPlaybackOptionsShown { get; private set; }
    public bool BookmarkAdded { get; private set; }
    public bool NamedBookmarkAdded { get; private set; }
    public bool LocalLibraryRefreshed { get; private set; }
    public bool LocalSourceManagerShown { get; private set; }
    public bool LibraryItemRenameShown { get; private set; }
    public bool LocalFileRenameShown { get; private set; }
    public int LocalLibraryMoveDirection { get; private set; }
    public int BookmarkNavigationDirection { get; private set; }
    public bool ChaptersShown { get; private set; }
    public bool NamedChapterAdded { get; private set; }
    public int ChapterNavigationDirection { get; private set; }
    public void ShowCurrentSession(string viewName) { }
    public void ShowFilter() { }
    public void ShowSessionList() { }
    public void ShowPlaylistManager() { }
    public void ShowCommandPalette() => CommandPaletteShown = true;
    public void ShowItemProperties() => ItemPropertiesShown = true;
    public void ShowPodcastDescription() => PodcastDescriptionShown = true;
    public void AnnounceCurrentBroadcastInformation() => CurrentBroadcastInformationAnnounced = true;
    public void GoToRelatedPodcast() => RelatedPodcastShown = true;
    public void ShowItemPlaybackOptions() => ItemPlaybackOptionsShown = true;
    public void OpenOfficialApplication() { }
    public void ShowHelp() { }
    public void ToggleKeyboardHelp() => KeyboardHelpToggled = true;
    public void ShowSettings(SettingsTarget target) => LastSettingsTarget = target;
    public void ToggleAccessibilityMessages() => MessagesToggled = true;
    public void ToggleDetailedHints() => DetailedHintsToggled = true;
    public void ToggleSeekMessages() => SeekMessagesToggled = true;
    public void OpenLocalFiles() { }
    public void OpenLocalFolder() { }
    public void ImportRadioPlaylist() { }
    public void RefreshLocalLibrary() => LocalLibraryRefreshed = true;
    public void ShowLocalSourceManager() => LocalSourceManagerShown = true;
    public void ShowWiiMDeviceManager() { }
    public void RefreshWiiMDevices() { }
    public void RenameLibraryItem() => LibraryItemRenameShown = true;
    public void RenameLocalFile() => LocalFileRenameShown = true;
    public void MoveLocalLibrarySelection(int direction) => LocalLibraryMoveDirection = direction;
    public void ShowSeekToTime() => SeekToTimeShown = true;
    public void ShowSeekToPercentage() => SeekToPercentageShown = true;
    public void AddBookmark() => BookmarkAdded = true;
    public void AddNamedBookmark() => NamedBookmarkAdded = true;
    public void NavigateBookmark(int direction) => BookmarkNavigationDirection = direction;
    public void ShowChapters() => ChaptersShown = true;
    public void AddNamedChapter() => NamedChapterAdded = true;
    public void NavigateChapter(int direction) => ChapterNavigationDirection = direction;
}

class FakeMediaOutput : IMediaOutput
{
    public string? LoadedItemId => LastItem?.Id;
    public TimeSpan Position { get; set; }
    public bool SupportsPlaybackRate => true;
    public int PlayCount { get; private set; }
    public int PauseCount { get; private set; }
    public int StopCount { get; private set; }
    public int Volume { get; private set; }
    public double PlaybackRate { get; private set; } = 1d;
    public MediaItem? LastItem { get; private set; }

    public void Play(MediaItem item, TimeSpan position, int volume, double playbackRate)
    {
        LastItem = item;
        Position = position;
        Volume = volume;
        PlaybackRate = playbackRate;
        PlayCount++;
    }

    public void Pause() => PauseCount++;
    public void Stop() => StopCount++;
    public void Seek(TimeSpan position) => Position = position;
    public void SetVolume(int volume) => Volume = volume;
    public void SetPlaybackRate(double playbackRate) => PlaybackRate = playbackRate;
}

sealed class FakeAudioProcessingMediaOutput : FakeMediaOutput, IPlaybackAudioProcessingOutput
{
    public PlaybackAudioProcessingCapabilities AudioProcessingCapabilities =>
        PlaybackAudioProcessingCapabilities.All;
    public PlaybackAudioSettings? LastAudioProcessingSettings { get; private set; }

    public void ConfigureAudioProcessing(PlaybackAudioSettings settings) =>
        LastAudioProcessingSettings = settings;
}

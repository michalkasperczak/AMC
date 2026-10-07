using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sonos;

/// <summary>
/// IMPORT PLAYLISTY DO MOICH STACJI SONOSA - sama logika scalania, bez okien,
/// bez dysku i bez sieci.
///
/// WEJSCIE to wpisy ODCZYTANE PRZEZ ISTNIEJACY parser radia/WiiM
/// (<c>RadioPlaylistImporter</c> z warstwy Windows): pary nazwa + adres. Tu NIE
/// MA drugiego parsera M3U/PLS/XSPF/JSON i nie wolno go dopisywac.
///
/// Fixtures formatow nie sa wymyslone: to DOSLOWNE wyjscie prawdziwego parsera
/// zmierzone na tym samym zrodle (log:
/// amc_pomoc/sonos-after416/import-core/parser-fixtures.log). Testy Core nie
/// uruchamiaja tego parsera, bo zyje on w projekcie Windows (WPF) i nie
/// kompiluje sie w tym runnerze - rozdzial jest jawny, nie udawany.
/// </summary>
internal static class SonosOwnStreamsImportTests
{
    private static int _checks;

    internal static void Run()
    {
        _checks = 0;
        FormatsFromRealParserBecomeStations();
        UnsupportedAddressesNeverBecomeStations();
        EmptyImportKeepsLibraryUntouched();
        DuplicateAddressKeepsSavedIdAndName();
        AddressesAreStoredLiterally();
        ResultIsDetachedFromInput();
        NewIdsAreUniqueEvenWithBadGenerator();
        Console.WriteLine($"OK: import playlisty do Moich stacji Sonosa ({_checks} sprawdzeń)");
    }

    /// <summary>
    /// Przypadki z wyjścia prawdziwego parsera: te pary zmierzono osobną sondą
    /// RadioPlaylistImporter dla M3U, PLS, XSPF i VRadio JSON. Ta metoda testuje scalanie.
    /// </summary>
    private static void FormatsFromRealParserBecomeStations()
    {
        // M3U: "#EXTINF:-1,Radio Pierwsze" + adres, dwa wpisy.
        var m3u = Merge([], [
            new SonosOwnStreamsImportEntry("Radio Pierwsze", "https://radio.example/one.mp3"),
            new SonosOwnStreamsImportEntry("Radio Drugie", "http://radio.example/two.aac")
        ]);
        Check(m3u.Added == 2 && m3u.Stations.Count == 2, "M3U: nie dodano obu stacji");
        Check(m3u.Stations[0].Name == "Radio Pierwsze" && m3u.Stations[0].StreamUrl == "https://radio.example/one.mp3",
            "M3U: zgubiono nazwę albo adres pierwszej stacji");
        Check(m3u.Stations[1].StreamUrl == "http://radio.example/two.aac", "M3U: odrzucono poprawny adres http");
        Check(m3u.SkippedInvalidAddresses == 0 && m3u.SkippedDuplicates == 0, "M3U: zły bilans pominięć");

        // PLS: File1 + Title1.
        var pls = Merge([], [new SonosOwnStreamsImportEntry("Radio PLS", "https://radio.example/live")]);
        Check(pls.Added == 1 && pls.Stations.Single().Name == "Radio PLS", "PLS: nie dodano stacji z tytułem");

        // XSPF: <title> + <location>.
        var xspf = Merge([], [new SonosOwnStreamsImportEntry("Radio XSPF", "https://radio.example/xspf")]);
        Check(xspf.Added == 1 && xspf.Stations.Single().Name == "Radio XSPF", "XSPF: nie dodano stacji");

        // VRadio JSON: parser oddaje JUZ odsiane wpisy (zly adres i duplikat
        // zliczyl u siebie) - my dostajemy jedna stacje.
        var json = Merge([], [new SonosOwnStreamsImportEntry("Radio VRadio", "https://radio.example/vradio")]);
        Check(json.Added == 1 && json.Stations.Single().StreamUrl == "https://radio.example/vradio",
            "VRadio JSON: nie dodano stacji");

        // Parser potrafi oddac wpis bez nazwy (sam podstawia host); gdy mimo to
        // nazwa jest pusta, stacja musi miec czytelna etykiete, nie pusty wiersz.
        var noName = Merge([], [new SonosOwnStreamsImportEntry("   ", "https://radio.example/nienazwana")]);
        Check(noName.Stations.Single().Name == "radio.example", "Pusta nazwa nie dostała etykiety z hosta");
    }

    /// <summary>
    /// Tylko zaakceptowane HTTP/HTTPS. Parser radia jest LUZNIEJSZY od Sonosa
    /// (dopuszcza adresy do 4096 znakow i znaki, ktorych loadStreamUrl nie
    /// przyjmie), wiec bramka SonosStreamUrlPolicy musi tu zadzialac.
    /// </summary>
    private static void UnsupportedAddressesNeverBecomeStations()
    {
        var tooLong = "https://radio.example/" + new string('a', 1025 - "https://radio.example/".Length);
        string?[] rejected =
        [
            null, "", "   ", "nie-adres", @"C:\muzyka\utwor.mp3", "/home/michal/muzyka.mp3",
            "file:///C:/muzyka.mp3", "ftp://radio.example/x", "spotify:track:abc", "tidal://album/1",
            "javascript:alert(1)", "//radio.example/x", "http://", "https://zly host/x",
            "https://radio.example/zly\"adres", "https://radio.example/zly|adres", tooLong
        ];
        foreach (var address in rejected)
        {
            var result = Merge([], [new SonosOwnStreamsImportEntry("Cokolwiek", address)]);
            Check(result.Stations.Count == 0 && result.Added == 0,
                $"Niedozwolony adres trafił na listę stacji: {address ?? "<null>"}");
            Check(result.SkippedInvalidAddresses == 1 && result.SkippedDuplicates == 0,
                $"Zły bilans pominięć dla adresu: {address ?? "<null>"}");
        }

        var atLimit = "https://radio.example/" + new string('a', 1024 - "https://radio.example/".Length);
        Check(Merge([], [new SonosOwnStreamsImportEntry("Granica", atLimit)]).Added == 1,
            "Adres o długości 1024 powinien wejść");
    }

    private static void EmptyImportKeepsLibraryUntouched()
    {
        var saved = new[] { Station("id-1", "Moja stacja", "https://radio.example/live") };
        var result = Merge(saved, []);
        Check(result.Added == 0 && result.SkippedInvalidAddresses == 0 && result.SkippedDuplicates == 0,
            "Pusty import nie powinien nic zliczyć");
        Check(result.Stations.Count == 1 && result.Stations[0].Id == "id-1"
            && result.Stations[0].Name == "Moja stacja"
            && result.Stations[0].StreamUrl == "https://radio.example/live",
            "Pusty import zmienił zapisaną stację");

        // Import nie czysci starych stacji takze wtedy, gdy CALA playlista jest zla.
        var allBad = Merge(saved, [new SonosOwnStreamsImportEntry("Zła", "nie-adres")]);
        Check(allBad.Stations.Count == 1 && allBad.Stations[0].Id == "id-1",
            "Nieudany import usunął zapisaną stację");
    }

    private static void DuplicateAddressKeepsSavedIdAndName()
    {
        var saved = new[]
        {
            Station("id-1", "Nazwa nadana przez Michała", "https://radio.example/live"),
            Station("id-2", "Druga", "https://radio.example/two")
        };
        var result = Merge(saved, [
            new SonosOwnStreamsImportEntry("Nazwa z playlisty", "HTTPS://RADIO.EXAMPLE/live"),
            new SonosOwnStreamsImportEntry("Nowa", "https://radio.example/new"),
            new SonosOwnStreamsImportEntry("Ta sama nowa", "https://radio.example/new")
        ]);
        Check(result.Added == 1, "Duplikaty powinny dać dokładnie jedną nową stację");
        Check(result.SkippedDuplicates == 2, "Zła liczba duplikatów (zapisany + wewnątrz playlisty)");
        Check(result.Stations.Count == 3, "Import zmienił liczbę stacji inaczej niż o jedną nową");
        Check(result.Stations[0].Id == "id-1" && result.Stations[0].Name == "Nazwa nadana przez Michała"
            && result.Stations[0].StreamUrl == "https://radio.example/live",
            "Duplikat nadpisał nazwę, ID albo adres zapisanej stacji");
        Check(result.Stations[1].Id == "id-2" && result.Stations[1].Name == "Druga",
            "Import ruszył stację, której playlista nie dotyczy");
        Check(result.Stations[2].Name == "Nowa" && result.Stations[2].StreamUrl == "https://radio.example/new",
            "Nowa stacja nie zachowała nazwy z playlisty");
        Check(result.Stations[0].Id != result.Stations[2].Id && result.Stations[1].Id != result.Stations[2].Id,
            "Nowe ID zderzyło się z zapisanym");
    }

    /// <summary>
    /// Adres idzie DOSLOWNIE: bez trimowania, bez dopisywania ukosnika, bez
    /// gubienia zapytania i fragmentu. Cicha normalizacja zmieniłaby material,
    /// ktory uzytkownik slyszy.
    /// </summary>
    private static void AddressesAreStoredLiterally()
    {
        const string withQuery = "https://radio.example/live?Key=AbC%2Fdef&n=1#tag";
        const string hostOnly = "https://radio.example";
        var result = Merge([], [
            new SonosOwnStreamsImportEntry("Z zapytaniem", withQuery),
            new SonosOwnStreamsImportEntry("Sam host", hostOnly)
        ]);
        Check(result.Stations[0].StreamUrl == withQuery, "Zmieniono adres z zapytaniem i fragmentem");
        Check(result.Stations[1].StreamUrl == hostOnly, "Dopisano ukośnik do adresu bez ścieżki");

        // Zapisany adres tez zostaje doslowny po imporcie czegos innego.
        var saved = new[] { Station("id-1", "Stara", withQuery) };
        Check(Merge(saved, [new SonosOwnStreamsImportEntry("Nowa", "https://radio.example/new")])
            .Stations[0].StreamUrl == withQuery, "Import znormalizował adres zapisanej stacji");
    }

    /// <summary>
    /// Wynik jest ODLACZONY: zadna strona nie trzyma referencji do obiektow
    /// drugiej, wiec zapis listy nie moze po cichu zmienic stanu wejscia.
    /// </summary>
    private static void ResultIsDetachedFromInput()
    {
        var saved = new List<SonosOwnStreamSettings>
        {
            Station("id-1", "Moja stacja", "https://radio.example/live")
        };
        var result = Merge(saved, [new SonosOwnStreamsImportEntry("Nowa", "https://radio.example/new")]);
        Check(!ReferenceEquals(result.Stations[0], saved[0]), "Wynik aliasuje obiekt wejściowej stacji");

        result.Stations[0].Name = "Zmienione w wyniku";
        result.Stations[0].StreamUrl = "https://radio.example/zmienione";
        Check(saved[0].Name == "Moja stacja" && saved[0].StreamUrl == "https://radio.example/live",
            "Zmiana w wyniku zmieniła zapisaną stację");

        saved[0].Name = "Zmienione w wejściu";
        saved.Add(Station("id-9", "Dodane po scaleniu", "https://radio.example/late"));
        Check(result.Stations.Count == 2 && result.Stations[0].Name == "Zmienione w wyniku",
            "Zmiana wejścia po scaleniu zmieniła wynik");

        // Wpisy importu tez zostaja nietkniete.
        var entry = new SonosOwnStreamsImportEntry("Nazwa wpisu", "https://radio.example/entry");
        var second = Merge([], [entry]);
        second.Stations[0].Name = "Inna";
        Check(entry.Name == "Nazwa wpisu" && entry.StreamUrl == "https://radio.example/entry",
            "Scalanie zmieniło wpis importu");
    }

    private static void NewIdsAreUniqueEvenWithBadGenerator()
    {
        // Generator celowo zepsuty: oddaje ZAWSZE identyfikator juz zajety.
        var saved = new[] { Station("stale-id", "Zapisana", "https://radio.example/live") };
        var result = SonosOwnStreamsImport.Merge(
            saved,
            [
                new SonosOwnStreamsImportEntry("Pierwsza", "https://radio.example/a"),
                new SonosOwnStreamsImportEntry("Druga", "https://radio.example/b")
            ],
            () => "stale-id");
        Check(result.Added == 2, "Zepsuty generator ID zablokował import");
        var ids = result.Stations.Select(station => station.Id).ToArray();
        Check(ids.Distinct(StringComparer.Ordinal).Count() == ids.Length, "Powtórzone ID stacji");
        Check(ids[0] == "stale-id", "Zmieniono ID zapisanej stacji");
        Check(ids.All(id => !string.IsNullOrWhiteSpace(id)), "Puste ID nowej stacji");
    }

    private static SonosOwnStreamsImportResult Merge(
        IEnumerable<SonosOwnStreamSettings> existing,
        IEnumerable<SonosOwnStreamsImportEntry> entries) =>
        SonosOwnStreamsImport.Merge(existing, entries);

    private static SonosOwnStreamSettings Station(string id, string name, string streamUrl) =>
        new() { Id = id, Name = name, StreamUrl = streamUrl };

    private static void Check(bool value, string message)
    {
        _checks++;
        if (!value) throw new Exception(message);
    }
}

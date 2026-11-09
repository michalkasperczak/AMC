using System.Text.Json.Nodes;
using AccessibleMediaController.LiteHost;

namespace AccessibleMediaController.LiteHost.ProtocolTests;

internal static class RadioLibraryMutationTests
{
    public static void Run()
    {
        ManualAddUpdatesDuplicateAndCreatesNewStation();
        ImportPromotesCatalogStationAndPreservesItsName();
        ImportReplacesHostOnlyNameButKeepsUserName();
        FavoritesExportUsesSelectedSortAndPortableFields();
        Console.WriteLine("RadioLibraryMutationTests: OK");
    }

    private static void ManualAddUpdatesDuplicateAndCreatesNewStation()
    {
        var root = Profile(
            Station("radio:old", "Stara nazwa", "https://radio.invalid/live", inLibrary: false));

        var updated = LiteRadioLibraryMutation.Apply(
            root,
            [new LiteRadioStationDraft("Nowa nazwa", "https://radio.invalid/live")],
            parserSkipped: 0,
            updateExistingTitles: true);

        var station = Stations(root).Single();
        Assert.True(updated.UpdatedCount == 1 && updated.AddedCount == 0
                    && updated.PromotedCount == 1,
            "ręczne dodanie tego samego adresu aktualizuje i przywraca stację");
        Assert.Equal("radio:old", updated.PreferredStationId,
            "fokus po aktualizacji wraca na istniejącą stację");
        Assert.Equal("Nowa nazwa", station["name"]?.GetValue<string>(),
            "ręczne dodanie zapisuje nową nazwę");
        Assert.True(station["hasCustomTitle"]?.GetValue<bool>() == true
                    && station["isInLibrary"]?.GetValue<bool>() == true,
            "zaktualizowana stacja ma własną nazwę i wraca do Biblioteki");

        var added = LiteRadioLibraryMutation.Apply(
            root,
            [new LiteRadioStationDraft("Nowe radio", "https://radio.invalid/new")],
            parserSkipped: 0,
            updateExistingTitles: true);
        var created = Stations(root).Single(value =>
            value["streamUrl"]?.GetValue<string>() == "https://radio.invalid/new");
        Assert.True(added.AddedCount == 1
                    && added.PreferredStationId?.StartsWith("radio:custom:", StringComparison.Ordinal) == true,
            "nowy ręczny wpis otrzymuje identyfikator stacji własnej");
        Assert.True(created["isCustom"]?.GetValue<bool>() == true
                    && created["isInLibrary"]?.GetValue<bool>() == true,
            "nowa stacja jest własna i widoczna w Bibliotece");
    }

    private static void ImportReplacesHostOnlyNameButKeepsUserName()
    {
        var root = Profile(
            Station("radio:host", "prem4.di.fm", "http://prem4.di.fm/trance", inLibrary: true),
            Station("radio:user", "Moje radio", "http://radio.invalid/live", inLibrary: true));

        var result = LiteRadioLibraryMutation.Apply(
            root,
            [
                new LiteRadioStationDraft("Trance - DI.FM Premium", "http://prem4.di.fm/trance"),
                new LiteRadioStationDraft("Nazwa z serwera", "http://radio.invalid/live")
            ],
            parserSkipped: 0,
            updateExistingTitles: false);

        var names = Stations(root).Select(value => value["name"]?.GetValue<string>()).ToArray();
        Assert.True(names[0] == "Trance - DI.FM Premium" && names[1] == "Moje radio",
            "ponowny import uzupełnia nazwę-host, ale nie nadpisuje nazwy użytkownika");
        Assert.True(result.UpdatedCount == 1 && result.SkippedCount == 1 && result.Changed,
            "uzupełniona nazwa jest liczona i zapisywana, a nie pomijana");
    }

    private static void ImportPromotesCatalogStationAndPreservesItsName()
    {
        var root = Profile(
            Station("radio:catalog", "Bogata nazwa katalogowa", "https://radio.invalid/catalog", inLibrary: false),
            Station("radio:saved", "Już zapisana", "https://radio.invalid/saved", inLibrary: true));

        var result = LiteRadioLibraryMutation.Apply(
            root,
            [
                new LiteRadioStationDraft("Nowa", "https://radio.invalid/new"),
                new LiteRadioStationDraft("Nazwa z playlisty", "https://radio.invalid/catalog"),
                new LiteRadioStationDraft("Duplikat", "https://radio.invalid/saved"),
                new LiteRadioStationDraft("Nowa drugi raz", "https://radio.invalid/new")
            ],
            parserSkipped: 2,
            updateExistingTitles: false);

        var catalog = Stations(root).Single(value =>
            value["id"]?.GetValue<string>() == "radio:catalog");
        Assert.True(result.PromotedCount == 1 && result.AddedCount == 1,
            "import przywraca katalogową stację i dodaje tylko jeden nowy adres");
        Assert.True(result.ChangedStationIds.Count == 2
                    && result.ChangedStationIds[0].StartsWith(
                        "radio:imported:", StringComparison.Ordinal)
                    && result.ChangedStationIds[1] == "radio:catalog",
            "zmienione stacje zachowują kolejność playlisty między wpisem istniejącym i nowym");
        Assert.True(result.SourceStationIds.Count == 3
                    && result.SourceStationIds[0] == result.ChangedStationIds[0]
                    && result.SourceStationIds[1] == "radio:catalog"
                    && result.SourceStationIds[2] == "radio:saved",
            "ponowny import obejmuje też już zapisane stacje i może naprawić ich kolejność");
        var customOrder = new List<string> { "radio:starsza" };
        var addedOrder = new List<string> { "radio:starsza" };
        LiteRadioLibraryMutation.ApplyChangedOrder(
            customOrder,
            addedOrder,
            result.SourceStationIds);
        Assert.True(customOrder.Skip(1).SequenceEqual(result.SourceStationIds),
            "kolejność własna zachowuje kolejność importowanej playlisty");
        Assert.True(addedOrder.AsEnumerable().Reverse().Take(3)
                .SequenceEqual(result.SourceStationIds),
            "widok najnowszych nie odwraca kolejności wewnątrz importowanej playlisty");
        Assert.True(result.SkippedCount == 4,
            "wynik sumuje błędy parsera i duplikaty Biblioteki oraz playlisty");
        Assert.Equal(result.SourceStationIds[0], result.PreferredStationId,
            "po imporcie fokus wraca na pierwszy wpis playlisty");
        Assert.Equal("Bogata nazwa katalogowa", catalog["name"]?.GetValue<string>(),
            "import nie zastępuje bogatej nazwy katalogowej nazwą z pliku");
        Assert.True(catalog["isInLibrary"]?.GetValue<bool>() == true,
            "istniejąca stacja katalogowa zostaje włączona do Biblioteki");
    }

    private static void FavoritesExportUsesSelectedSortAndPortableFields()
    {
        var root = Profile(
            Favorite("radio:older", "Żuraw", "https://radio.invalid/older"),
            Favorite("radio:newer", "Alfa", "https://radio.invalid/newer"),
            Favorite("radio:private", "Prywatne", "https://login:haslo@radio.invalid/live"),
            Station("radio:ordinary", "Zwykłe", "https://radio.invalid/ordinary", inLibrary: true));
        root["sessionNavigation"] = new JsonObject
        {
            ["sessions"] = new JsonObject
            {
                ["radio"] = new JsonObject
                {
                    ["collectionSortModes"] = new JsonObject { ["Ulubione"] = "AddedNewest" }
                }
            }
        };

        var entries = LiteRadioFavoritesExport.SelectEntries(
            root,
            customOrder: ["radio:newer", "radio:older"],
            addedOrder: ["radio:older", "radio:newer"]);

        Assert.True(entries.Count == 2, "eksport pomija wpisy nieulubione i adresy z logowaniem");
        Assert.Equal("Alfa", entries[0].Name,
            "kolejność według dodania pokazuje najnowszą stację u góry");
        var playlist = System.Text.Encoding.UTF8.GetString(
            AccessibleMediaController.Core.Radio.RadioFavoritesPlaylistWriter.Write(entries));
        Assert.True(!playlist.Contains("radio:newer", StringComparison.Ordinal)
                    && playlist.Contains("https://radio.invalid/newer", StringComparison.Ordinal),
            "przenośny plik zawiera adres, ale nie techniczny identyfikator AMC");

        root["sessionNavigation"]!["sessions"]!["radio"]!["collectionSortModes"]!["Ulubione"] =
            "Alphabetical";
        entries = LiteRadioFavoritesExport.SelectEntries(root, [], []);
        Assert.Equal("Alfa", entries[0].Name, "sortowanie alfabetyczne profilu jest zachowane");

        var directory = Path.Combine(
            Path.GetTempPath(),
            "amc-radio-export-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var destination = Path.Combine(directory, "ulubione.json");
            var result = LiteRadioFavoritesExport.Write(root, [], [], destination);
            var json = File.ReadAllText(destination);
            Assert.True(result.ExportedCount == 2
                        && json.Contains("\"stations\"", StringComparison.Ordinal)
                        && !json.Contains("radio:newer", StringComparison.Ordinal),
                "atomowy eksport JSON zapisuje wyłącznie przenośne pola dwóch stacji");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static JsonObject Profile(params JsonObject[] stations) => new()
    {
        ["radio"] = new JsonObject
        {
            ["stations"] = new JsonArray(stations.Select(station => (JsonNode)station).ToArray())
        }
    };

    private static JsonObject Station(string id, string name, string url, bool inLibrary) => new()
    {
        ["id"] = id,
        ["name"] = name,
        ["streamUrl"] = url,
        ["hasCustomTitle"] = false,
        ["isInLibrary"] = inLibrary
    };

    private static JsonObject Favorite(string id, string name, string url)
    {
        var station = Station(id, name, url, inLibrary: true);
        station["isFavorite"] = true;
        return station;
    }

    private static IEnumerable<JsonObject> Stations(JsonObject root) =>
        ((JsonArray)root["radio"]!["stations"]!).OfType<JsonObject>();
}

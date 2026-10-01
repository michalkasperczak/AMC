using System.Text.Json;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sonos;

internal static class SonosOwnStreamsPersistenceTests
{
    internal static void Run()
    {
        var root = Path.Combine(Path.GetTempPath(), "amc-own-streams-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "settings.json");
            var store = new ConfigurationStore(path);
            var state = store.LoadOrCreate();
            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            const string url = "https://radio.example.invalid/live?Key=AbC%2Fdef&n=1";
            state.Sonos = JsonSerializer.Deserialize<SonosSessionSettings>(JsonSerializer.Serialize(new
            {
                selectedHouseholdId = "Sonos_test.home1",
                selectedGroupId = "GROUP:1",
                ownStreams = new[] { new { id = "station-1", name = "Moja stacja", streamUrl = url } }
            }, options), options)!;
            store.Save(state);
            var restored = new ConfigurationStore(path).LoadOrCreate();
            using var json = JsonDocument.Parse(JsonSerializer.Serialize(restored.Sonos, options));
            Check(json.RootElement.TryGetProperty("ownStreams", out var list) && list.GetArrayLength() == 1,
                "Zapis i ponowny odczyt zgubiły własną stację Sonosa");
            Check(list[0].GetProperty("id").GetString() == "station-1", "Zmieniono trwałe ID stacji");
            Check(list[0].GetProperty("streamUrl").GetString() == url, "Zmieniono dosłowny adres stacji");
            Check(restored.Sonos.SelectedGroupId == "GROUP:1", "Zapis stacji zmienił wybór grupy");
            Check(SonosLibraryPresentation.DescribeCategories().Any(x => x.CategoryId == "sonos.library.ownstreams"),
                "Biblioteka nie ma wejścia do własnych stacji");
            restored.Sonos.OwnStreams[0].Name = "Po zmianie";
            restored.Sonos.OwnStreams[0].StreamUrl = url + "&edited=1";
            store.Save(restored);
            var edited = new ConfigurationStore(path).LoadOrCreate();
            Check(edited.Sonos.OwnStreams.Single().Id == "station-1"
                && edited.Sonos.OwnStreams.Single().Name == "Po zmianie", "Edycja zgubiła nazwę albo ID");
            Check(edited.Sonos.OwnStreams.Single().StreamUrl == url + "&edited=1", "Edycja nie zachowała URL");
            edited.Sonos.OwnStreams.Clear();
            store.Save(edited);
            Check(new ConfigurationStore(path).LoadOrCreate().Sonos.OwnStreams.Count == 0,
                "Usunięta stacja wróciła po ponownym odczycie");
            Console.WriteLine("OK: własne stacje Sonosa - zapis, edycja, usunięcie, dosłowny URL, cel i kategoria (8 sprawdzeń)");
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
    }
}

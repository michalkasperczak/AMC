using System.Text.Json;

namespace AccessibleMediaController.LiteHost.ProtocolTests;

internal static class ObservedMaterialFixtureExportTests
{
    public static void Run()
    {
        var path = Path.Combine(
            Path.GetTempPath(), "amc-observed-material-fixture-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            ObservedMaterialFixtureExport.Write(path);
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var cases = document.RootElement.GetProperty("cases");
            Check(cases.GetArrayLength() == 10,
                "eksport kontraktu ma zawierać pięć scenariuszy WiiM i pięć Sonos");

            foreach (var entry in cases.EnumerateArray())
            {
                var name = entry.GetProperty("name").GetString() ?? string.Empty;
                var session = entry.GetProperty("session").GetString() ?? string.Empty;
                var expected = entry.GetProperty("expectedMaterialId");
                var snapshot = entry.GetProperty("snapshot");
                var listPayload = entry.GetProperty("listPayload");

                Check(session is "WIIM" or "SONOS", $"nieznana sesja exportu: {name}");
                Check(listPayload.GetProperty("items").GetArrayLength() > 0,
                    $"brak rzeczywistego payloadu listy: {name}");
                Check(snapshot.TryGetProperty("observedMaterialId", out var actual),
                    $"migawka nie ma observedMaterialId: {name}");
                Check(snapshot.TryGetProperty("identityProvenance", out _),
                    $"migawka nie ma identityProvenance: {name}");
                Check(actual.ValueKind == expected.ValueKind
                      && (actual.ValueKind == JsonValueKind.Null
                          || actual.GetString() == expected.GetString()),
                    $"wynik mappera nie zgadza się z niezależnym oraklem: {name}");

                if (session == "WIIM")
                {
                    Check(entry.GetProperty("deviceId").GetString() == "device-1",
                        $"fixture WiiM nie zachował deviceId: {name}");
                    Check(entry.GetProperty("householdId").ValueKind == JsonValueKind.Null
                          && entry.GetProperty("groupId").ValueKind == JsonValueKind.Null,
                        $"fixture WiiM nie może udawać kontekstu grupy Sonos: {name}");
                }
                else
                {
                    Check(entry.GetProperty("deviceId").ValueKind == JsonValueKind.Null,
                        $"fixture Sonos nie może udawać deviceId WiiM: {name}");
                    Check(entry.GetProperty("householdId").GetString() == "home-1"
                          && entry.GetProperty("groupId").GetString() == "group-1",
                        $"fixture Sonos nie zachował celu gospodarstwo/grupa: {name}");
                }
            }

            var json = File.ReadAllText(path);
            Check(!json.Contains("radio.example/", StringComparison.Ordinal)
                  && json.Contains(".invalid", StringComparison.Ordinal),
                "fixture musi używać wyłącznie jawnie fikcyjnych adresów .invalid");
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

// Pomiar PRZED/PO dla CaptureLocalMediaState na rzeczywistym oknie WPF.
//
// Mierzy naprzemiennie (stare, nowe, stare, nowe, ...) DOKLADNIE te same dane, zeby
// dryf maszyny (inne kompilacje w tle) rozlozyl sie po obu wariantach zamiast
// obciazyc jeden z nich.
//
//   STARE  = droga sprzed poprawki: dla kazdej pozycji pelny skan liniowy
//            FindLocalItemSettings + ShouldRememberLocalPosition(string) bez memo.
//   NOWE   = CaptureLocalMediaState po poprawce.
//
// Raportowane sa mediany, nie srednie: pojedyncze przebiegi na obciazonej maszynie
// maja dlugi ogon. Wynik nie jest pomiarem opoznienia klawiatury ani czytnika.
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Windows;

internal static class CaptureLocalMediaStateBench
{
    private const BindingFlags Members =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    internal static int Run(string dataDirectory, int repetitions)
    {
        var statePath = Path.Combine(dataDirectory, "state.json");
        if (!File.Exists(statePath))
        {
            Console.Error.WriteLine($"Brak pliku stanu: {statePath}");
            return 2;
        }

        var store = new ConfigurationStore(statePath);
        var state = store.LoadOrCreate();
        var window = new MainWindow(state, store);

        var localItems = (List<MediaItem>)typeof(MainWindow)
            .GetField("_localItems", Members)!.GetValue(window)!;
        var itemCount = localItems.Count;
        var savedCount = state.LocalMedia.Items.Count;

        var capture = typeof(MainWindow).GetMethod("CaptureLocalMediaState", Members)!;
        var findSettings = typeof(MainWindow).GetMethod("FindLocalItemSettings", Members)!;
        var shouldRememberByPath = typeof(MainWindow).GetMethod(
            "ShouldRememberLocalPosition", Members, null, new[] { typeof(string) }, null)!;

        // Rozgrzewka: JIT i pierwsze dotkniecie sciezek nie moga wejsc do pomiaru.
        capture.Invoke(window, null);
        LegacyPass(window, localItems, findSettings, shouldRememberByPath);

        var referenceSnapshot = JsonSerializer.Serialize(state.LocalMedia.Items);

        var oldMs = new List<double>();
        var newMs = new List<double>();
        for (var pass = 0; pass < repetitions; pass++)
        {
            var legacyWatch = Stopwatch.StartNew();
            LegacyPass(window, localItems, findSettings, shouldRememberByPath);
            legacyWatch.Stop();
            oldMs.Add(legacyWatch.Elapsed.TotalMilliseconds);

            var newWatch = Stopwatch.StartNew();
            capture.Invoke(window, null);
            newWatch.Stop();
            newMs.Add(newWatch.Elapsed.TotalMilliseconds);

            // Kazdy przebieg musi dac IDENTYCZNY stan: pomiar bez zgodnosci jest bezuzyteczny.
            var snapshot = JsonSerializer.Serialize(state.LocalMedia.Items);
            if (!string.Equals(snapshot, referenceSnapshot, StringComparison.Ordinal))
            {
                Console.Error.WriteLine($"Przebieg {pass}: zapisany stan rozni sie od odniesienia.");
                return 3;
            }
        }

        var scans = (int)typeof(MainWindow)
            .GetField("_captureFindSettingsScans", Members)!.GetValue(window)!;
        var normalizations = (int)typeof(MainWindow)
            .GetField("_captureFolderNormalizations", Members)!.GetValue(window)!;

        var result = new
        {
            dataDirectory,
            repetitions,
            localItems = itemCount,
            savedItems = savedCount,
            folderSources = state.LocalMedia.FolderSources.Count,
            folderPlaybackOptions = state.LocalMedia.FolderPlaybackOptions.Count,
            oldMedianMs = Median(oldMs),
            newMedianMs = Median(newMs),
            oldMinMs = oldMs.Min(),
            newMinMs = newMs.Min(),
            oldMaxMs = oldMs.Max(),
            newMaxMs = newMs.Max(),
            scansInLastPass = scans,
            normalizationsInLastPass = normalizations,
            stateHash = Hash(referenceSnapshot),
            note = "mediany; maszyna obciazona innymi kompilacjami; nie jest to pomiar UI ani czytnika"
        };
        Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }

    /// <summary>
    /// Droga sprzed poprawki: powtorny skan liniowy i normalizacja bez pamieci przebiegu.
    /// </summary>
    private static void LegacyPass(
        MainWindow window,
        List<MediaItem> localItems,
        MethodInfo findSettings,
        MethodInfo shouldRememberByPath)
    {
        foreach (var item in localItems)
        {
            var found = (LocalMediaItemSettings?)findSettings.Invoke(window, new object?[] { item });
            _ = found?.ResumePositionMode switch
            {
                ResumePositionMode.Remember => true,
                ResumePositionMode.StartFromBeginning => false,
                _ => (bool)shouldRememberByPath.Invoke(window, new object?[] { item.Source })!
            };
        }
    }

    private static double Median(List<double> values)
    {
        var sorted = values.OrderBy(value => value).ToList();
        var middle = sorted.Count / 2;
        return sorted.Count % 2 == 1
            ? sorted[middle]
            : (sorted[middle - 1] + sorted[middle]) / 2.0;
    }

    private static string Hash(string text) =>
        Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(text)))[..16];
}

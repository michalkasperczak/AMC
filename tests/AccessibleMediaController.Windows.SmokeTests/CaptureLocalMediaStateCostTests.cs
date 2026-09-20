// Regresja KOSZTU I ZGODNOSCI dla CaptureLocalMediaState.
//
// Test pilnuje DWOCH wlasnosci naraz (test pilnujacy tylko zgodnosci przechodzilby
// takze na kodzie przed poprawka):
//   1. zapisany stan jest IDENTYCZNY we WSZYSTKICH polach i w tej samej kolejnosci,
//      co wynik wyliczony droga sprzed poprawki (FindLocalItemSettings +
//      ShouldRememberLocalPosition(string));
//   2. jeden przebieg nie wykonuje ani powtornego skanu liniowego ustawien elementu,
//      ani powtorzonej normalizacji tych samych korzeni folderow.
//
// Zakres przypadkow: Remember / StartFromBeginning / Inherit dla pliku, dla folderu
// (FolderPlaybackOptions oraz FolderSources) i dla sesji; brak rekordu `previous`;
// dopasowanie po sciezce o innej wielkosci liter i z segmentem "."; sciezka, ktorej
// nie da sie znormalizowac (wyjatek w normalizacji nie przerywa przebiegu);
// brak przeciekania pamieci normalizacji miedzy wywolaniami.
using System.Reflection;
using System.Text;
using System.Text.Json;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.LocalMedia;
using AccessibleMediaController.Core.Playback;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Windows;

internal static class CaptureLocalMediaStateCostTests
{
    private const BindingFlags Members =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    internal static void Run()
    {
        RunOnStaThread(() =>
        {
            AllFieldsMatchReferenceAcrossResumeModes();
            SinglePassDoesNotRescanOrRenormalize();
        });
        Console.WriteLine(
            "OK: zapis lokalnego stanu zachowuje wszystkie pola i nie powtarza wyszukiwania ani normalizacji");
    }

    // ---------- Przypadek 1: pelna zgodnosc pol dla calej macierzy trybow ----------

    private static void AllFieldsMatchReferenceAcrossResumeModes()
    {
        foreach (var sessionMode in new[]
                 {
                     ResumePositionMode.Inherit,
                     ResumePositionMode.Remember,
                     ResumePositionMode.StartFromBeginning
                 })
        foreach (var globalRemembers in new[] { true, false })
        {
            using var fixture = Fixture.Create(sessionMode, globalRemembers);
            var window = fixture.Window;
            var label = $"sesja={sessionMode}, globalnie={globalRemembers}";

            // Werdykt ODNIESIENIA policzony droga sprzed poprawki: pelny skan
            // liniowy przez FindLocalItemSettings i ShouldRememberLocalPosition(string).
            var expected = new Dictionary<string, bool>(StringComparer.Ordinal);
            var referenceRecords = new Dictionary<string, LocalMediaItemSettings?>(StringComparer.Ordinal);
            foreach (var item in fixture.LocalItems)
            {
                var found = (LocalMediaItemSettings?)typeof(MainWindow)
                    .GetMethod("FindLocalItemSettings", Members)!
                    .Invoke(window, new object?[] { item });
                referenceRecords[item.Id] = found;
                expected[item.Id] = found?.ResumePositionMode switch
                {
                    ResumePositionMode.Remember => true,
                    ResumePositionMode.StartFromBeginning => false,
                    _ => (bool)typeof(MainWindow)
                        .GetMethod(
                            "ShouldRememberLocalPosition",
                            Members,
                            null,
                            new[] { typeof(string) },
                            null)!
                        .Invoke(window, new object?[] { item.Source })!
                };
            }

            Invoke(window, "CaptureLocalMediaState");
            var produced = Snapshot(fixture.State.LocalMedia.Items);
            var producedOrder = Order(fixture.State.LocalMedia.Items);

            Check(
                fixture.State.LocalMedia.Items.Count == fixture.LocalItems.Count,
                $"Przebieg musi zapisac wszystkie pozycje ({label}): "
                + $"{fixture.State.LocalMedia.Items.Count} z {fixture.LocalItems.Count}.");
            Check(
                producedOrder == Order(fixture.LocalItems),
                $"Kolejnosc zapisanych pozycji musi odpowiadac kolejnosci listy ({label}).");

            foreach (var saved in fixture.State.LocalMedia.Items)
            {
                // Wartosc ODNIESIENIA liczona DOKLADNIE wzorem produkcyjnym, ale na
                // rekordzie znalezionym pelnym skanem liniowym (droga sprzed poprawki).
                var referenceRecord = referenceRecords[saved.Id];
                var expectedTicks = expected[saved.Id]
                    ? (fixture.RememberedPositions is { } remembered
                        ? remembered.GetValueOrDefault(saved.Id)
                        : TimeSpan.FromTicks(referenceRecord?.ResumePositionTicks ?? 0))
                    : TimeSpan.Zero;
                Check(
                    saved.ResumePositionTicks == Math.Max(0, expectedTicks.Ticks),
                    $"Zapisana pozycja „{saved.Id}” musi zgadzac sie z przebiegiem odniesienia "
                    + $"({label}): jest {saved.ResumePositionTicks}, "
                    + $"ma byc {Math.Max(0, expectedTicks.Ticks)}.");
                if (!expected[saved.Id])
                {
                    Check(
                        saved.ResumePositionTicks == 0,
                        $"„{saved.Id}” nie pamieta pozycji, wiec zapis musi miec zero ({label}).");
                }
            }

            // Drugi przebieg na tym samym stanie: pamiec normalizacji nie przecieka
            // miedzy wywolaniami i nie zmienia ani pol, ani kolejnosci.
            Invoke(window, "CaptureLocalMediaState");
            Check(
                Snapshot(fixture.State.LocalMedia.Items) == produced,
                $"Drugi przebieg zmienil zapisane pola ({label}).");
            Check(
                Order(fixture.State.LocalMedia.Items) == producedOrder,
                $"Drugi przebieg zmienil kolejnosc zapisanych pozycji ({label}).");

            // Pozycja BEZ wczesniejszego rekordu dostaje wartosci domyslne, nie pola obcego rekordu.
            var fresh = Single(fixture.State.LocalMedia.Items, "nowy-bez-rekordu");
            Check(
                fresh.ResumePositionMode == ResumePositionMode.Inherit
                && fresh.ClipStartTicks is null && fresh.ClipEndTicks is null
                && fresh.PlaybackRateOverride is null && fresh.OutputDeviceId is null
                && fresh.LoudnessNormalizationOverride is null
                && fresh.SmoothTrackTransitionsOverride is null
                && fresh.InterTrackSilenceMillisecondsOverride is null
                && !fresh.IsRadioRecording && fresh.RadioRecordingCompletedUtcTicks == 0,
                $"Pozycja bez wczesniejszego rekordu nie moze odziedziczyc pol obcego rekordu ({label}).");

            // Dopasowanie po sciezce (inne Id, inna wielkosc liter, segment ".")
            // musi odtworzyc WSZYSTKIE pola rekordu dopasowanego po sciezce.
            var byPath = Single(fixture.State.LocalMedia.Items, "inne-id-ta-sama-sciezka");
            Check(
                byPath.ResumePositionMode == ResumePositionMode.Remember
                && byPath.PlaybackRateOverride == 1.25
                && byPath.OutputDeviceId == "urzadzenie-x"
                && byPath.LoudnessNormalizationOverride == true
                && byPath.SmoothTrackTransitionsOverride == false
                && byPath.InterTrackSilenceMillisecondsOverride == 750
                && byPath.ClipStartTicks == 111 && byPath.ClipEndTicks == 222
                && byPath.FileLength == 4321 && byPath.LastWriteUtcTicks == 8765
                && byPath.IsRadioRecording
                && byPath.RadioRecordingCompletedUtcTicks == 999,
                $"Dopasowanie po znormalizowanej sciezce musi odtworzyc wszystkie pola ({label}).");

            // Sciezka niemozliwa do znormalizowania nie przerywa przebiegu.
            var broken = Single(fixture.State.LocalMedia.Items, "zla-sciezka");
            Check(
                broken.ResumePositionTicks >= 0,
                $"Pozycja o niepoprawnej sciezce musi zostac zapisana ({label}).");
        }
    }

    // ---------- Przypadek 2: koszt jednego przebiegu ----------

    private static void SinglePassDoesNotRescanOrRenormalize()
    {
        using var fixture = Fixture.Create(ResumePositionMode.Inherit, globalRemembers: true);
        var window = fixture.Window;

        SetField(window, "_captureFindSettingsScans", 0);
        SetField(window, "_captureFolderNormalizations", 0);
        Invoke(window, "CaptureLocalMediaState");

        var scans = GetField<int>(window, "_captureFindSettingsScans");
        var normalizations = GetField<int>(window, "_captureFolderNormalizations");
        var items = fixture.LocalItems.Count;

        // Przed poprawka petla wolala FindLocalItemSettings raz na KAZDA pozycje.
        Check(
            scans < items,
            $"Petla zapisu nie moze ponownie skanowac listy dla kazdej pozycji: "
            + $"{scans} skanow na {items} pozycji.");
        // Poza petla zostaja dokladnie dwa wyszukania niezalezne od liczby pozycji:
        // delegat _rememberPosition sesji (RememberCurrentPosition) oraz sprawdzenie
        // wlasnej predkosci biezacego elementu. Oba sa stale, nie rosna z lista.
        Check(
            scans <= 2,
            $"Poza petla moga zostac najwyzej dwa wyszukania ustawien, jest {scans}.");

        // Korzeni folderow jest garsc, pozycji tysiace: liczba realnych normalizacji
        // nie moze rosnac z liczba pozycji.
        Check(
            normalizations < items,
            $"Normalizacja korzeni nie moze powtarzac sie dla kazdej pozycji: "
            + $"{normalizations} na {items} pozycji.");
        Check(
            normalizations > 0,
            "Przebieg z folderami musi realnie policzyc choc jedna normalizacje korzenia.");
    }

    // ---------- Aparatura ----------

    private sealed class Fixture : IDisposable
    {
        public required string Root { get; init; }
        public required PersistedState State { get; init; }
        public required MainWindow Window { get; init; }
        public required List<MediaItem> LocalItems { get; init; }
        public required IReadOnlyDictionary<string, TimeSpan>? RememberedPositions { get; init; }

        public static Fixture Create(ResumePositionMode sessionMode, bool globalRemembers)
        {
            var root = Path.Combine(
                Path.GetTempPath(),
                "amc-capture-cost-" + Guid.NewGuid().ToString("N"));
            var libraryRoot = Path.Combine(root, "Muzyka");
            var rememberFolder = Path.Combine(libraryRoot, "Pamietane");
            var beginningFolder = Path.Combine(libraryRoot, "OdPoczatku");
            Directory.CreateDirectory(rememberFolder);
            Directory.CreateDirectory(beginningFolder);

            var state = new PersistedState();
            state.Settings.RememberLocalPlaybackPositions = globalRemembers;
            ResumePositionPolicy.SetSessionMode(state.Settings, "local", sessionMode);

            state.LocalMedia.FolderSources.Add(new LocalFolderSourceSettings
            {
                Id = "zrodlo-glowne",
                Path = libraryRoot,
                DisplayName = "Muzyka",
                ResumePositionMode = ResumePositionMode.Inherit
            });
            state.LocalMedia.FolderSources.Add(new LocalFolderSourceSettings
            {
                Id = "zrodlo-od-poczatku",
                Path = beginningFolder,
                DisplayName = "Od poczatku",
                ResumePositionMode = ResumePositionMode.StartFromBeginning
            });
            state.LocalMedia.FolderPlaybackOptions.Add(new LocalFolderPlaybackSettings
            {
                Path = rememberFolder,
                ResumePositionMode = ResumePositionMode.Remember
            });

            var files = new List<(string Id, string Path, ResumePositionMode Mode)>
            {
                ("plik-remember", Path.Combine(libraryRoot, "remember.mp3"), ResumePositionMode.Remember),
                ("plik-od-poczatku", Path.Combine(libraryRoot, "start.mp3"), ResumePositionMode.StartFromBeginning),
                ("plik-inherit", Path.Combine(libraryRoot, "inherit.mp3"), ResumePositionMode.Inherit),
                ("folder-remember", Path.Combine(rememberFolder, "a.mp3"), ResumePositionMode.Inherit),
                ("folder-od-poczatku", Path.Combine(beginningFolder, "b.mp3"), ResumePositionMode.Inherit),
                ("zrodlo-inherit", Path.Combine(libraryRoot, "c.mp3"), ResumePositionMode.Inherit)
            };

            foreach (var (id, path, mode) in files)
            {
                File.WriteAllBytes(path, new byte[] { 1, 2, 3, 4 });
                state.LocalMedia.Items.Add(new LocalMediaItemSettings
                {
                    Id = id,
                    Title = id,
                    Path = path,
                    ResumePositionMode = mode,
                    ResumePositionTicks = TimeSpan.FromSeconds(30).Ticks,
                    DurationTicks = TimeSpan.FromMinutes(5).Ticks
                });
            }

            // Rekord dopasowywany PO SCIEZCE: pelny zestaw pol do sprawdzenia.
            var sharedPath = Path.Combine(libraryRoot, "po-sciezce.mp3");
            File.WriteAllBytes(sharedPath, new byte[] { 9, 9, 9 });
            state.LocalMedia.Items.Add(new LocalMediaItemSettings
            {
                Id = "stare-id",
                Title = "Po sciezce",
                HasCustomTitle = true,
                Path = sharedPath,
                ResumePositionMode = ResumePositionMode.Remember,
                ResumePositionTicks = TimeSpan.FromSeconds(42).Ticks,
                PlaybackRateOverride = 1.25,
                OutputDeviceId = "urzadzenie-x",
                LoudnessNormalizationOverride = true,
                SmoothTrackTransitionsOverride = false,
                InterTrackSilenceMillisecondsOverride = 750,
                ClipStartTicks = 111,
                ClipEndTicks = 222,
                FileLength = 4321,
                LastWriteUtcTicks = 8765,
                IsRadioRecording = true,
                RadioRecordingCompletedUtcTicks = 999,
                DurationTicks = TimeSpan.FromMinutes(3).Ticks
            });

            var statePath = Path.Combine(root, "state.json");
            var window = new MainWindow(state, new ConfigurationStore(statePath));

            var localItems = GetField<List<MediaItem>>(window, "_localItems");
            localItems.Clear();
            foreach (var (id, path, _) in files) localItems.Add(NewItem(id, path));

            // Ta sama sciezka, INNA wielkosc liter i segment "." w srodku.
            var variantPath = Path.Combine(libraryRoot, ".", "po-sciezce.mp3").ToUpperInvariant();
            localItems.Add(NewItem("inne-id-ta-sama-sciezka", variantPath));

            // Pozycja BEZ rekordu w zapisie.
            var freshPath = Path.Combine(libraryRoot, "nowy.mp3");
            File.WriteAllBytes(freshPath, new byte[] { 7 });
            localItems.Add(NewItem("nowy-bez-rekordu", freshPath));

            // Sciezka, ktorej nie da sie znormalizowac: wyjatek w normalizacji nie
            // moze przerwac przebiegu ani zmienic pozostalych pol.
            localItems.Add(NewItem("zla-sciezka", "|||nie:jest*sciezka?.mp3"));

            return new Fixture
            {
                Root = root,
                State = state,
                Window = window,
                LocalItems = localItems,
                RememberedPositions = GetField<SessionManager>(window, "_sessions")
                    .FindSession("local")?.RememberedPositions
            };
        }

        public void Dispose()
        {
            try { Window.Close(); } catch { }
            try { Directory.Delete(Root, true); } catch { }
        }
    }

    private static MediaItem NewItem(string id, string path) => new()
    {
        Id = id,
        Title = id,
        Source = path,
        Duration = TimeSpan.FromMinutes(5)
    };

    private static LocalMediaItemSettings Single(List<LocalMediaItemSettings> items, string id) =>
        items.Single(item => string.Equals(item.Id, id, StringComparison.Ordinal));

    private static string Snapshot(List<LocalMediaItemSettings> items) =>
        JsonSerializer.Serialize(items);

    private static string Order(List<LocalMediaItemSettings> items)
    {
        var builder = new StringBuilder();
        foreach (var item in items) builder.Append(item.Id).Append('\n');
        return builder.ToString();
    }

    private static string Order(List<MediaItem> items)
    {
        var builder = new StringBuilder();
        foreach (var item in items) builder.Append(item.Id).Append('\n');
        return builder.ToString();
    }

    private static void Invoke(MainWindow window, string method) =>
        typeof(MainWindow).GetMethod(method, Members)!.Invoke(window, null);

    private static T GetField<T>(object target, string name) =>
        (T)target.GetType().GetField(name, Members)!.GetValue(target)!;

    private static void SetField(object target, string name, object? value) =>
        target.GetType().GetField(name, Members)!.SetValue(target, value);

    private static void RunOnStaThread(Action body)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { body(); }
            catch (Exception exception) { failure = exception; }
            finally
            {
                System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(TimeSpan.FromMinutes(5)))
        {
            throw new Exception("Proba zapisu lokalnego stanu przekroczyla limit czasu.");
        }
        if (failure is not null) throw failure;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}

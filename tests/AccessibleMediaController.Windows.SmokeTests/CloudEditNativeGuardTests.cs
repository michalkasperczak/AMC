using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using AccessibleMediaController.Core.LocalMedia;
using AccessibleMediaController.Windows.Services;

/// <summary>
/// NATYWNE testy Windows wspólnej decyzji o edycji pliku chmurowego, po korekcie
/// commitu 6a2b914. Uruchamiają PRAWDZIWE usługi produktu
/// (<see cref="AudioClipOriginalEditor"/> i <see cref="AudioClipAppender"/>) na
/// własnych, wygenerowanych plikach WAV. Nigdy nie dotykają nagrań użytkownika,
/// nie zmieniają uprawnień poza własną atrapą i nie włączają trybu dewelopera.
///
/// Dowód odmowy jest MIERZONY, nie zakładany: dla każdego odrzucenia zapisujemy
/// SHA-256 i czas modyfikacji obu plików oraz pełny spis katalogu przed próbą i
/// po niej, a dodatkowo liczymy procesy FFmpeg uruchomione w trakcie próby.
/// </summary>
internal static class CloudEditNativeGuardTests
{
    internal static int Run()
    {
        var cases = new (string Name, Action<string> Body)[]
        {
            ("Kompletny plik lokalny: ciecie i dopisanie naprawde dzialaja", RealCutAndAppendOnLocalFile),
            ("Plik w folderze o nazwie chmury: ciecie dziala", CutWorksUnderCloudNamedFolder),
            ("Offline: odmowa ciecia bez zmiany plikow i bez FFmpeg", OfflineRefusesCutWithoutTouchingFiles),
            ("Offline: odmowa dopisania dla zrodla i dla celu", OfflineRefusesAppendBothDirections),
            ("RECALL_ON_DATA_ACCESS: odmowa bez zmiany plikow", RecallOnDataAccessRefuses),
            ("Przypiecie samo nie wystarcza jako dowod danych", PinnedAloneIsNotEvidence),
            ("Brak pliku to Missing, a nie brak pobrania", MissingIsNotNeedsDownload),
            ("Brak uprawnien jest rozpoznany, nie zamieniony na Missing", AccessDeniedIsNotMissing),
            ("Dowiazanie symboliczne: decyduje metadana celu", SymbolicLinkFollowsTargetMetadata),
            ("Zlacze katalogu: decyduje metadana prawdziwego celu", JunctionParentFollowsRealTarget),
            ("Sprawdzenie dostepnosci nie otwiera tresci pliku", ProbeDoesNotOpenPayload)
        };

        var failed = 0;
        foreach (var (name, body) in cases)
        {
            var root = Path.Combine(Path.GetTempPath(), "amc-cloud-edit-native-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                body(root);
                Console.WriteLine("OK: " + name);
            }
            catch (Exception error)
            {
                failed++;
                Console.Error.WriteLine("BLAD: " + name + ": " + error);
            }
            finally
            {
                TryDeleteDirectory(root);
            }
        }

        Console.WriteLine(
            $"EDYCJA PLIKU CHMUROWEGO (NATYWNIE): {cases.Length - failed} OK / {failed} BLAD / razem {cases.Length}");
        return failed == 0 ? 0 : 1;
    }

    // ---------- pozytywne: produkt nadal naprawde edytuje ----------

    private static void RealCutAndAppendOnLocalFile(string root)
    {
        RequireFfmpeg();
        var cut = Path.Combine(root, "ciecie.wav");
        WriteTone(cut, TimeSpan.FromSeconds(4));
        var cutResult = Wait(AudioClipOriginalEditor.RemoveAsync(
            new AudioClipRemovalRequest(cut, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4)),
            null,
            CancellationToken.None));
        Check(
            Math.Abs(cutResult.Duration.TotalSeconds - 3d) < 0.15d,
            $"Po cieciu 1 s z 4 s oczekiwano ~3 s, jest {cutResult.Duration}.");

        var target = Path.Combine(root, "cel.wav");
        var source = Path.Combine(root, "zrodlo.wav");
        WriteTone(target, TimeSpan.FromSeconds(2));
        WriteTone(source, TimeSpan.FromSeconds(3));
        var sourceHash = HashOf(source);
        var appendResult = Wait(AudioClipAppender.AppendAsync(
            new AudioClipAppendRequest(source, target, TimeSpan.Zero, TimeSpan.FromSeconds(1)),
            null,
            CancellationToken.None));
        Check(HashOf(source) == sourceHash, "Dopisanie zmienilo plik zrodlowy.");
        Check(
            appendResult.TargetDurationAfter > TimeSpan.FromSeconds(2.7),
            $"Po dopisaniu 1 s do 2 s oczekiwano ~3 s, jest {appendResult.TargetDurationAfter}.");
    }

    private static void CutWorksUnderCloudNamedFolder(string root)
    {
        RequireFfmpeg();
        // Nazwa folderu NIE jest dowodem braku danych. To byla tresc falszywej
        // odmowy, ktora ta zmiana usuwa. Fixture, nie prawdziwy klient chmury.
        // Kontrolka na tej samej glebokosci, ale z neutralna nazwa: oddziela
        // wplyw NAZWY folderu od wplywu samej glebokosci sciezki.
        foreach (var provider in new[] { "ZwyklyFolder", "OneDrive", "OneDrive - Firma", "Dropbox", "Google Drive", "iCloudDrive" })
        {
            var directory = Path.Combine(root, provider, "Nagrania");
            Directory.CreateDirectory(directory);
            var file = Path.Combine(directory, "nagranie.wav");
            WriteTone(file, TimeSpan.FromSeconds(3));
            try
            {
                var result = Wait(AudioClipOriginalEditor.RemoveAsync(
                    new AudioClipRemovalRequest(file, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3)),
                    null,
                    CancellationToken.None));
                Check(
                    Math.Abs(result.Duration.TotalSeconds - 2d) < 0.15d,
                    provider + $": dlugosc po cieciu {result.Duration} zamiast ~2 s.");
                Console.WriteLine($"  POMIAR: {provider} -> ciecie wykonane, dlugosc {result.Duration}");
            }
            catch (Exception error)
            {
                throw new InvalidOperationException(
                    $"Ciecie nie powiodlo sie dla folderu '{provider}' (sciezka {file}): {error.Message}", error);
            }
        }
    }

    // ---------- negatywne: odmowa MIERZONA na plikach i procesach ----------

    private static void OfflineRefusesCutWithoutTouchingFiles(string root)
    {
        var file = Path.Combine(root, "tylko-online.wav");
        WriteTone(file, TimeSpan.FromSeconds(4));
        var refusal = MeasureRefusal(
            root,
            [file],
            () => Wait(AudioClipOriginalEditor.RemoveAsync(
                new AudioClipRemovalRequest(file, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4)),
                null,
                CancellationToken.None)),
            () => SetAttribute(file, FileAttributes.Offline),
            () => ClearAttribute(file, FileAttributes.Offline));
        refusal.AssertRefusedWithoutSideEffects("w pełni dostępny lokalnie");
        Check(
            refusal.Error is InvalidOperationException,
            "Odmowa ciecia z powodu braku danych ma byc InvalidOperationException, a jest " + refusal.Error?.GetType().Name);
    }

    private static void OfflineRefusesAppendBothDirections(string root)
    {
        foreach (var offlineIsTarget in new[] { true, false })
        {
            var directory = Path.Combine(root, offlineIsTarget ? "cel" : "zrodlo");
            Directory.CreateDirectory(directory);
            var source = Path.Combine(directory, "zrodlo.wav");
            var target = Path.Combine(directory, "cel.wav");
            WriteTone(source, TimeSpan.FromSeconds(4));
            WriteTone(target, TimeSpan.FromSeconds(3));
            var offlinePath = offlineIsTarget ? target : source;
            var refusal = MeasureRefusal(
                directory,
                [source, target],
                () => Wait(AudioClipAppender.AppendAsync(
                    new AudioClipAppendRequest(source, target, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3)),
                    null,
                    CancellationToken.None)),
                () => SetAttribute(offlinePath, FileAttributes.Offline),
                () => ClearAttribute(offlinePath, FileAttributes.Offline));
            refusal.AssertRefusedWithoutSideEffects("w pełni dostępny lokalnie");
        }
    }

    private static void RecallOnDataAccessRefuses(string root)
    {
        // FILE_ATTRIBUTE_RECALL_ON_DATA_ACCESS (0x00400000) ustawiany przez
        // silnik synchronizacji. Jesli system nie pozwoli go ustawic z poziomu
        // uzytkownika, mowimy o tym wprost zamiast udawac PASS.
        const FileAttributes recallOnDataAccess = (FileAttributes)0x00400000;
        var file = Path.Combine(root, "recall.wav");
        WriteTone(file, TimeSpan.FromSeconds(3));
        if (!TrySetAttribute(file, recallOnDataAccess))
        {
            Console.WriteLine(
                "  GRANICA: systemu nie da sie poprosic o RECALL_ON_DATA_ACCESS z poziomu uzytkownika; "
                + "ten stan zmierzony tylko na poziomie klasyfikacji metadanych (ClassifyMetadataForEdit).");
            Check(
                CloudFileAvailability.ClassifyMetadataForEdit(
                    FileAttributes.Archive | recallOnDataAccess, 0xFFFFFFFFu, true)
                    == CloudEditOutcome.NeedsDownload,
                "RECALL_ON_DATA_ACCESS musi dawac odmowe na poziomie klasyfikacji.");
            return;
        }

        try
        {
            var availability = CloudFileAvailability.GetEditAvailability(file);
            Check(!availability.CanEdit, "Plik z RECALL_ON_DATA_ACCESS zostal przyjety do edycji.");
            Check(
                availability.Outcome == CloudEditOutcome.NeedsDownload,
                "RECALL_ON_DATA_ACCESS dal wynik " + availability.Outcome);
        }
        finally
        {
            ClearAttribute(file, recallOnDataAccess);
        }
    }

    private static void PinnedAloneIsNotEvidence(string root)
    {
        // FILE_ATTRIBUTE_PINNED (0x00080000) to intencja przechowywania, nie
        // dowod kompletnosci. Na prawdziwym, w calosci lokalnym pliku NTFS samo
        // przypiecie nie moze zmienic odpowiedzi w zadna strone: plik jest
        // edytowalny dzieki dowodowi nosnika, nie dzieki przypieciu.
        const FileAttributes pinned = (FileAttributes)0x00080000;
        var file = Path.Combine(root, "przypiety.wav");
        WriteTone(file, TimeSpan.FromSeconds(3));
        var withoutPin = CloudFileAvailability.GetEditAvailability(file).Outcome;
        Check(withoutPin == CloudEditOutcome.Editable, "Kontrolka: zwykly plik lokalny ma byc edytowalny.");
        if (TrySetAttribute(file, pinned))
        {
            try
            {
                var withPin = CloudFileAvailability.GetEditAvailability(file).Outcome;
                // Przypiecie nie moze zmienic odpowiedzi w ZADNA strone: ani
                // dodac zgody tam, gdzie brak dowodu, ani odebrac jej plikowi,
                // ktory naprawde lezy na dysku. Drugi kierunek zostal zmierzony
                // natywnie i byl falszywa odmowa we wczesniejszej wersji tej
                // poprawki.
                Check(
                    withPin == withoutPin,
                    $"Samo przypiecie zmienilo wynik dla zwyklego pliku lokalnego: {withoutPin} -> {withPin}.");
                Console.WriteLine(
                    "  POMIAR: FILE_ATTRIBUTE_PINNED na prawdziwym pliku NTFS nie zmienil wyniku (" + withPin + ")");
            }
            finally { ClearAttribute(file, pinned); }
        }
        else
        {
            Console.WriteLine("  GRANICA: systemu nie da sie poprosic o FILE_ATTRIBUTE_PINNED na zwyklym pliku.");
        }

        // Przypiecie na niezidentyfikowanym punkcie ponownej analizy ani
        // przypiecie bez dowodu nosnika nie moga dawac zgody na przepisanie.
        const FileAttributes reparse = FileAttributes.Archive | FileAttributes.ReparsePoint;
        Check(
            CloudFileAvailability.ClassifyMetadataForEdit(FileAttributes.Archive | pinned, 0xFFFFFFFFu, false)
                == CloudEditOutcome.Unknown,
            "Przypiecie bez dowodu nosnika lokalnego nie moze dowodzic danych lokalnych.");
        Check(
            CloudFileAvailability.ClassifyMetadataForEdit(reparse | pinned, 0xFFFFFFFFu, true)
                == CloudEditOutcome.Unknown,
            "Przypiecie na nieznanym punkcie ponownej analizy nie moze dowodzic danych lokalnych.");
        Check(
            CloudFileAvailability.ClassifyMetadataForEdit(reparse | pinned, 0x11u, true)
                == CloudEditOutcome.NeedsDownload,
            "Przypiecie z PARTIAL musi zostac odmowa.");
        Check(
            CloudFileAvailability.ClassifyMetadataForEdit(
                FileAttributes.Archive | pinned | FileAttributes.Offline, 0xFFFFFFFFu, true)
                == CloudEditOutcome.NeedsDownload,
            "Przypiecie z OFFLINE musi zostac odmowa.");
        // Kompletny placeholder Cloud Files nadal przechodzi, przypiety czy nie.
        foreach (var attributes in new[] { reparse, reparse | pinned, reparse | (FileAttributes)0x00100000 })
        {
            Check(
                CloudFileAvailability.ClassifyMetadataForEdit(attributes, 0x9u, true) == CloudEditOutcome.Editable,
                "Kompletny placeholder Cloud Files musi pozostac edytowalny.");
        }
    }

    private static void MissingIsNotNeedsDownload(string root)
    {
        var availability = CloudFileAvailability.GetEditAvailability(Path.Combine(root, "nie-ma-mnie.wav"));
        Check(availability.Outcome == CloudEditOutcome.Missing, "Brak pliku dal wynik " + availability.Outcome);
        Check(
            !availability.Message.Contains("Pobierz", StringComparison.OrdinalIgnoreCase),
            "Brak pliku zglaszany jako brak pobrania z chmury: " + availability.Message);
        var thrown = CatchFrom(() => Wait(AudioClipOriginalEditor.RemoveAsync(
            new AudioClipRemovalRequest(
                Path.Combine(root, "nie-ma-mnie.wav"), TimeSpan.Zero, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3)),
            null,
            CancellationToken.None)));
        Check(
            thrown is FileNotFoundException or ArgumentException,
            "Brakujacy plik dal wyjatek " + thrown?.GetType().Name);
    }

    private static void AccessDeniedIsNotMissing(string root)
    {
        // ACL zmieniany TYLKO na wlasnej atrapie i przywracany w finally.
        //
        // ZMIERZONA GRANICA: odmowa prawa CZYTANIA DANYCH (RD) i rozszerzonych
        // atrybutow (REA) NIE blokuje otwarcia samych metadanych, bo sonda
        // otwiera uchwyt z dostepem 0 i czyta FileAttributeTagInfo. To jest
        // poprawne i pozadane: sprawdzenie dostepnosci ma NIE siegac do tresci.
        // Dlatego zeby zmierzyc sciezke AccessDenied, trzeba odmowic prawa do
        // odczytu ATRYBUTOW (RA) oraz samego otwarcia.
        var file = Path.Combine(root, "bez-dostepu.wav");
        WriteTone(file, TimeSpan.FromSeconds(2));
        var icacls = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System), "icacls.exe");
        if (!File.Exists(icacls))
        {
            Console.WriteLine("  GRANICA: brak icacls.exe, nie da sie zmierzyc odmowy dostepu na prawdziwym pliku.");
            return;
        }

        // Najpierw kontrola: sam zakaz czytania DANYCH nie moze niczego zepsuc,
        // bo sonda tresci nie czyta.
        var user = Environment.UserName;
        if (RunTool(icacls, $"\"{file}\" /deny \"{user}\":(RD,REA)") == 0)
        {
            try
            {
                var dataDenied = CloudFileAvailability.GetEditAvailability(file);
                Check(
                    dataDenied.Outcome == CloudEditOutcome.Editable,
                    "Zakaz czytania DANYCH zmienil wynik sprawdzenia metadanych na " + dataDenied.Outcome
                    + " — sonda najwyrazniej siega do tresci pliku.");
                Console.WriteLine(
                    "  POMIAR: zakaz czytania danych (RD,REA) -> " + dataDenied.Outcome
                    + " (sonda nie siega do tresci, zgodnie z zalozeniem)");
            }
            finally { RunTool(icacls, $"\"{file}\" /remove:d \"{user}\""); }
        }

        // Teraz prawdziwa odmowa dostepu: zakaz odczytu atrybutow i uprawnien.
        if (RunTool(icacls, $"\"{file}\" /inheritance:r /deny \"{user}\":(RA,RD,REA,RC)") != 0)
        {
            Console.WriteLine("  GRANICA: nie udalo sie zalozyc wlasnego zakazu ACL na atrapie; pomiar pominiety.");
            RunTool(icacls, $"\"{file}\" /reset");
            return;
        }

        try
        {
            var availability = CloudFileAvailability.GetEditAvailability(file);
            Console.WriteLine("  POMIAR: plik bez prawa odczytu atrybutow -> " + availability.Outcome);
            Check(!availability.CanEdit, "Plik bez uprawnien zostal przyjety do edycji.");
            Check(
                availability.Outcome != CloudEditOutcome.Missing,
                "Brak uprawnien zostal zgloszony jako brak pliku — wlasnie to naprawia ta zmiana.");
            Check(
                availability.Outcome is CloudEditOutcome.AccessDenied or CloudEditOutcome.Unknown,
                "Brak uprawnien dal nieoczekiwany wynik " + availability.Outcome);
            // Kontrola przeciwna: File.Exists, na ktorym opieral sie 6a2b914,
            // w tym stanie klamie — i to jest dowod, ze nie wolno z niego
            // wnioskowac nieobecnosci pliku.
            Console.WriteLine(
                "  POMIAR: File.Exists dla tego samego pliku = " + File.Exists(file)
                + " (plik istnieje, wiec wnioskowanie nieobecnosci z File.Exists bylo bledne)");
        }
        finally
        {
            RunTool(icacls, $"\"{file}\" /reset");
            RunTool(icacls, $"\"{file}\" /remove:d \"{user}\"");
        }
    }

    private static void SymbolicLinkFollowsTargetMetadata(string root)
    {
        var targetDirectory = Path.Combine(root, "prawdziwy");
        Directory.CreateDirectory(targetDirectory);
        var real = Path.Combine(targetDirectory, "prawdziwy.wav");
        WriteTone(real, TimeSpan.FromSeconds(3));
        var link = Path.Combine(root, "dowiazanie.wav");
        try
        {
            File.CreateSymbolicLink(link, real);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            // Bez trybu dewelopera i bez podniesienia uprawnien tworzenie
            // dowiazan jest zabronione. Nie wlaczamy go; mowimy o granicy.
            Console.WriteLine(
                "  GRANICA NIEZMIERZONA: system nie pozwala utworzyc dowiazania symbolicznego bez trybu dewelopera "
                + "(" + error.GetType().Name + "); przypadku dowiazania NIE zmierzono natywnie.");
            return;
        }

        try
        {
            Check(
                CloudFileAvailability.GetEditAvailability(link).CanEdit,
                "Dowiazanie do w calosci lokalnego pliku zostalo odrzucone.");
            // Cel bez danych => odmowa, mimo ze samo dowiazanie wyglada zwyczajnie.
            SetAttribute(real, FileAttributes.Offline);
            try
            {
                var availability = CloudFileAvailability.GetEditAvailability(link);
                Check(!availability.CanEdit, "Dowiazanie do pliku bez danych lokalnych zostalo przyjete.");
                Check(
                    availability.Outcome == CloudEditOutcome.NeedsDownload,
                    "Dowiazanie do pliku Offline dalo wynik " + availability.Outcome);
                Console.WriteLine("  POMIAR: dowiazanie -> metadana celu zadecydowala (" + availability.Outcome + ")");
            }
            finally { ClearAttribute(real, FileAttributes.Offline); }
        }
        finally
        {
            try { File.Delete(link); } catch (IOException) { }
        }
    }

    private static void JunctionParentFollowsRealTarget(string root)
    {
        // Zlacze katalogu (junction) tworzy mklink /J i NIE wymaga trybu
        // dewelopera. Plik widziany przez zlacze ma byc oceniony po prawdziwym
        // celu, bez otwierania jego tresci.
        var realDirectory = Path.Combine(root, "prawdziwy-folder");
        Directory.CreateDirectory(realDirectory);
        var real = Path.Combine(realDirectory, "nagranie.wav");
        WriteTone(real, TimeSpan.FromSeconds(3));
        var junction = Path.Combine(root, "przez-zlacze");
        var comspec = Environment.GetEnvironmentVariable("ComSpec")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
        if (!File.Exists(comspec)
            || RunTool(comspec, $"/c mklink /J \"{junction}\" \"{realDirectory}\"") != 0)
        {
            Console.WriteLine("  GRANICA: nie udalo sie utworzyc zlacza katalogu; przypadku nie zmierzono.");
            return;
        }

        var viaJunction = Path.Combine(junction, "nagranie.wav");
        try
        {
            Check(
                CloudFileAvailability.GetEditAvailability(viaJunction).CanEdit,
                "Plik widziany przez zlacze katalogu zostal odrzucony, choc jest w calosci lokalny.");
            SetAttribute(real, FileAttributes.Offline);
            try
            {
                var availability = CloudFileAvailability.GetEditAvailability(viaJunction);
                Check(
                    availability.Outcome == CloudEditOutcome.NeedsDownload,
                    "Plik bez danych widziany przez zlacze dal wynik " + availability.Outcome);
                Console.WriteLine("  POMIAR: zlacze katalogu -> " + availability.Outcome);
            }
            finally { ClearAttribute(real, FileAttributes.Offline); }
        }
        finally
        {
            try { Directory.Delete(junction); } catch (IOException) { }
        }
    }

    private static void ProbeDoesNotOpenPayload(string root)
    {
        // Samo sprawdzenie dostepnosci nie moze czytac tresci. Mierzymy to
        // wylacznym uchwytem na dane: gdyby sprawdzenie otwieralo payload,
        // dostalibysmy IOException albo zmieniony czas dostepu.
        var file = Path.Combine(root, "wylaczny.wav");
        WriteTone(file, TimeSpan.FromSeconds(2));
        var before = File.GetLastWriteTimeUtc(file);
        using (var exclusive = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var availability = CloudFileAvailability.GetEditAvailability(file);
            Check(
                availability.Outcome is CloudEditOutcome.Editable,
                "Sprawdzenie dostepnosci pliku z wylacznym uchwytem na dane dalo " + availability.Outcome
                + " — to znak, ze probowano otworzyc tresc.");
        }
        Check(File.GetLastWriteTimeUtc(file) == before, "Sprawdzenie dostepnosci zmienilo czas modyfikacji.");
    }

    // ---------- pomiar odmowy ----------

    private sealed record Refusal(
        Exception? Error,
        bool FilesIdentical,
        bool DirectoryIdentical,
        int FfmpegStarted,
        string Detail)
    {
        internal void AssertRefusedWithoutSideEffects(string expectedMessagePart)
        {
            Check(Error is not null, "Plik bez danych lokalnych zostal przyjety do edycji. " + Detail);
            Check(
                Error!.Message.Contains(expectedMessagePart, StringComparison.Ordinal),
                "Odmowa nie nazwala przyczyny: " + Error.Message);
            Check(FilesIdentical, "Odmowa zmienila zawartosc lub czas modyfikacji pliku. " + Detail);
            Check(DirectoryIdentical, "Odmowa zostawila dodatkowe pliki (kopie lub plik tymczasowy). " + Detail);
            Check(FfmpegStarted == 0, $"Odmowa uruchomila FFmpeg {FfmpegStarted} raz(y). " + Detail);
            Console.WriteLine("  POMIAR: " + Detail);
        }
    }

    private static Refusal MeasureRefusal(
        string directory,
        string[] watched,
        Action attempt,
        Action arrange,
        Action restore)
    {
        var ffmpeg = FfmpegRadioWaveProvider.FindExecutable();
        var ffmpegName = ffmpeg is null ? "ffmpeg" : Path.GetFileNameWithoutExtension(ffmpeg);
        var before = watched.ToDictionary(
            path => path,
            path => (Hash: HashOf(path), Stamp: File.GetLastWriteTimeUtc(path), Length: new FileInfo(path).Length));
        var entriesBefore = Snapshot(directory);
        var ffmpegBefore = CountProcesses(ffmpegName);

        arrange();
        Exception? error = null;
        int ffmpegPeak;
        try
        {
            try { attempt(); }
            catch (Exception caught) { error = caught; }
        }
        finally
        {
            ffmpegPeak = CountProcesses(ffmpegName);
            restore();
        }

        var identical = before.All(entry =>
            File.Exists(entry.Key)
            && HashOf(entry.Key) == entry.Value.Hash
            && File.GetLastWriteTimeUtc(entry.Key) == entry.Value.Stamp
            && new FileInfo(entry.Key).Length == entry.Value.Length);
        var entriesAfter = Snapshot(directory);
        var started = Math.Max(0, ffmpegPeak - ffmpegBefore);
        var detail =
            $"pliki={watched.Length} identyczne(SHA-256+mtime+dlugosc)={identical}, "
            + $"wpisy katalogu przed={entriesBefore.Length} po={entriesAfter.Length}, "
            + $"procesy {ffmpegName} przed={ffmpegBefore} po={ffmpegPeak}, "
            + $"wyjatek={error?.GetType().Name ?? "BRAK"}";
        return new Refusal(
            error,
            identical,
            entriesBefore.SequenceEqual(entriesAfter, StringComparer.OrdinalIgnoreCase),
            started,
            detail);
    }

    private static string[] Snapshot(string directory) =>
        Directory.Exists(directory)
            ? Directory.GetFileSystemEntries(directory, "*", SearchOption.AllDirectories)
                .Select(entry => entry + "|" + (File.Exists(entry) ? new FileInfo(entry).Length : -1L))
                .OrderBy(entry => entry, StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : [];

    private static int CountProcesses(string name)
    {
        try { return Process.GetProcessesByName(name).Length; }
        catch (Exception) { return 0; }
    }

    // ---------- narzedzia ----------

    private static void RequireFfmpeg()
    {
        if (FfmpegRadioWaveProvider.FindExecutable() is null)
            throw new InvalidOperationException("Ten pomiar wymaga skladnika FFmpeg, ktorego tu nie ma.");
    }

    private static void SetAttribute(string path, FileAttributes attribute)
    {
        if (!TrySetAttribute(path, attribute))
            throw new InvalidOperationException($"Nie udalo sie ustawic atrybutu {attribute} na {path}.");
    }

    private static bool TrySetAttribute(string path, FileAttributes attribute)
    {
        try
        {
            File.SetAttributes(path, File.GetAttributes(path) | attribute);
            return (File.GetAttributes(path) & attribute) != 0;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }
    }

    private static void ClearAttribute(string path, FileAttributes attribute)
    {
        try { File.SetAttributes(path, File.GetAttributes(path) & ~attribute); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException) { }
    }

    private static int RunTool(string fileName, string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(fileName, arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });
            if (process is null) return -1;
            process.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();
            return process.WaitForExit(TimeSpan.FromSeconds(30)) ? process.ExitCode : -1;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return -1;
        }
    }

    private static Exception? CatchFrom(Action action)
    {
        try { action(); return null; }
        catch (Exception error) { return error; }
    }

    private static string HashOf(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static void WriteTone(string path, TimeSpan duration)
    {
        const int rate = 44100;
        var samples = (int)(duration.TotalSeconds * rate);
        using var writer = new BinaryWriter(File.Create(path));
        var dataBytes = samples * 2;
        writer.Write("RIFF"u8.ToArray());
        writer.Write(36 + dataBytes);
        writer.Write("WAVE"u8.ToArray());
        writer.Write("fmt "u8.ToArray());
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(rate);
        writer.Write(rate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write("data"u8.ToArray());
        writer.Write(dataBytes);
        for (var index = 0; index < samples; index++)
        {
            writer.Write((short)(8000 * Math.Sin(2 * Math.PI * 440 * index / rate)));
        }
    }

    private static T Wait<T>(Task<T> task) => task.GetAwaiter().GetResult();

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            foreach (var entry in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                try { File.SetAttributes(entry, FileAttributes.Normal); } catch (Exception) { }
            }
            Directory.Delete(path, recursive: true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
    }
}

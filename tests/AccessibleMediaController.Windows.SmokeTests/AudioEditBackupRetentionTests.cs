using System.IO;
using System.Security.Cryptography;
using AccessibleMediaController.Windows.Services;
using NAudio.Wave;

/// <summary>
/// Real-file tests of the retention policy for the backup that an audio edit
/// creates for ITS OWN operation. The default is to remove that one backup after
/// the saved file has been checked; anything uncertain keeps it. Backups left by
/// earlier operations are never touched, and nothing here sweeps or globs.
/// </summary>
internal static class AudioEditBackupRetentionTests
{
    internal static int Run()
    {
        var cases = new (string Name, Action<string> Body)[]
        {
            ("Udane ciecie nie zostawia swojej nowej kopii", CutSuccessRemovesNewBackup),
            ("Udane dopisanie nie zostawia swojej nowej kopii", AppendSuccessRemovesNewBackup),
            ("Wybor zachowania kopii dziala przy cieciu", CutKeepsBackupOnRequest),
            ("Wybor zachowania kopii dziala przy dopisaniu", AppendKeepsBackupOnRequest),
            ("Starsza kopia z wczesniejszej edycji zostaje nietknieta", OlderBackupSurvivesNewEdit),
            ("Ostrzezenie o kodowaniu nie obiecuje usunietej kopii", ReencodeWarningMatchesRetention),
            ("Anulowanie ciecia nie gubi materialu i nie tworzy kopii", CutCancellationLosesNothing),
            ("Blad ciecia zostawia oryginal i nie tworzy kopii", CutFailureLeavesOriginal),
            ("Inna tresc po podmianie zachowuje kopie i nie klamie", CommitMismatchKeepsBackup),
            ("Nieczytelny plik po podmianie zachowuje kopie", CommitUnreadableKeepsBackup),
            ("Nieudane usuniecie kopii zwraca jej sciezke", DeleteFailureKeepsBackupPath)
        };

        var failed = 0;
        foreach (var (name, body) in cases)
        {
            var root = Path.Combine(Path.GetTempPath(), "amc-edit-backups-" + Guid.NewGuid().ToString("N"));
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
            $"KOPIE PO EDYCJI: {cases.Length - failed} OK / {failed} BLAD / razem {cases.Length}");
        return failed == 0 ? 0 : 1;
    }

    // ---- domyslny sukces: kopia tej operacji znika po sprawdzeniu ----

    private static void CutSuccessRemovesNewBackup(string root)
    {
        RequireFfmpeg();
        var source = Path.Combine(root, "ciecie.wav");
        WriteSilence(source, TimeSpan.FromSeconds(3));

        var result = Wait(AudioClipOriginalEditor.RemoveAsync(
            new AudioClipRemovalRequest(
                source,
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(3)),
            null,
            CancellationToken.None));

        Check(File.Exists(source), "Po cieciu zabraklo pliku zrodlowego.");
        Check(
            result.BackupPath.Length == 0,
            "Po sprawdzonym cieciu wynik nadal wskazuje kopie zapasowa: " + result.BackupPath);
        var leftovers = Backups(root);
        Check(
            leftovers.Length == 0,
            "Po sprawdzonym cieciu zostala nowa kopia zapasowa: " + string.Join(", ", leftovers));
        using var edited = new WaveFileReader(source);
        Check(
            Math.Abs(edited.TotalTime.TotalSeconds - 2d) < 0.08d,
            $"Nieprawidlowa dlugosc pliku po cieciu: {edited.TotalTime}.");
    }

    private static void AppendSuccessRemovesNewBackup(string root)
    {
        var target = Path.Combine(root, "cel.wav");
        var source = Path.Combine(root, "zrodlo.wav");
        WriteSilence(target, TimeSpan.FromSeconds(2));
        WriteSilence(source, TimeSpan.FromSeconds(3));
        var sourceHash = Hash(source);

        var result = Wait(AudioClipAppender.AppendAsync(
            new AudioClipAppendRequest(source, target, TimeSpan.Zero, TimeSpan.FromSeconds(1)),
            null,
            CancellationToken.None));

        Check(
            result.BackupPath.Length == 0,
            "Po sprawdzonym dopisaniu wynik nadal wskazuje kopie zapasowa: " + result.BackupPath);
        var leftovers = Backups(root);
        Check(
            leftovers.Length == 0,
            "Po sprawdzonym dopisaniu zostala nowa kopia zapasowa: " + string.Join(", ", leftovers));
        Check(Hash(source) == sourceHash, "Dopisanie zmienilo plik zrodlowy.");
        using var reader = new WaveFileReader(target);
        Check(
            (reader.TotalTime - TimeSpan.FromSeconds(3)).Duration() < TimeSpan.FromMilliseconds(60),
            $"Cel ma po dopisaniu {reader.TotalTime:c}, a oczekiwano okolo 3 s.");
    }

    // ---- opcja zachowania ----

    private static void CutKeepsBackupOnRequest(string root)
    {
        RequireFfmpeg();
        var source = Path.Combine(root, "ciecie.wav");
        WriteSilence(source, TimeSpan.FromSeconds(3));
        var before = File.ReadAllBytes(source);

        var result = Wait(AudioClipOriginalEditor.RemoveAsync(
            new AudioClipRemovalRequest(
                source,
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(3)),
            null,
            CancellationToken.None,
            keepBackup: true));

        Check(result.BackupPath.Length != 0, "Przy wybranym zachowaniu kopii wynik nie podal jej sciezki.");
        Check(File.Exists(result.BackupPath), "Wybrana kopia zapasowa nie istnieje: " + result.BackupPath);
        Check(
            before.SequenceEqual(File.ReadAllBytes(result.BackupPath)),
            "Zachowana kopia nie jest wierna poprzedniej wersji pliku.");
        Check(Backups(root).Length == 1, "Zachowanie kopii utworzylo inna liczbe kopii niz jedna.");
    }

    private static void AppendKeepsBackupOnRequest(string root)
    {
        var target = Path.Combine(root, "cel.wav");
        var source = Path.Combine(root, "zrodlo.wav");
        WriteSilence(target, TimeSpan.FromSeconds(2));
        WriteSilence(source, TimeSpan.FromSeconds(3));
        var before = File.ReadAllBytes(target);

        var result = Wait(AudioClipAppender.AppendAsync(
            new AudioClipAppendRequest(source, target, TimeSpan.Zero, TimeSpan.FromSeconds(1)),
            null,
            CancellationToken.None,
            keepBackup: true));

        Check(result.BackupPath.Length != 0, "Przy wybranym zachowaniu kopii wynik nie podal jej sciezki.");
        Check(File.Exists(result.BackupPath), "Wybrana kopia zapasowa nie istnieje: " + result.BackupPath);
        Check(
            before.SequenceEqual(File.ReadAllBytes(result.BackupPath)),
            "Zachowana kopia nie jest wierna poprzedniej wersji celu.");
        Check(Backups(root).Length == 1, "Zachowanie kopii utworzylo inna liczbe kopii niz jedna.");
    }

    // ---- stare kopie sa poza zasiegiem tej polityki ----

    private static void OlderBackupSurvivesNewEdit(string root)
    {
        var target = Path.Combine(root, "cel.wav");
        var source = Path.Combine(root, "zrodlo.wav");
        WriteSilence(target, TimeSpan.FromSeconds(2));
        WriteSilence(source, TimeSpan.FromSeconds(3));
        // Kopia z JAKIEJS wczesniejszej edycji, o ktorej ta operacja nic nie wie.
        var older = target + ".20200101-000000.amc-backup";
        File.WriteAllText(older, "starsza kopia uzytkownika");
        var olderHash = Hash(older);
        var unrelated = Path.Combine(root, "obcy.wav.20200101-000000.amc-backup");
        File.WriteAllText(unrelated, "kopia innego pliku");

        var result = Wait(AudioClipAppender.AppendAsync(
            new AudioClipAppendRequest(source, target, TimeSpan.Zero, TimeSpan.FromSeconds(1)),
            null,
            CancellationToken.None));

        Check(result.BackupPath.Length == 0, "Nowa kopia tej operacji nie zostala usunieta.");
        Check(File.Exists(older) && Hash(older) == olderHash, "Usunieto starsza kopie tego samego pliku.");
        Check(File.Exists(unrelated), "Usunieto kopie nalezaca do innego pliku.");
        Check(Backups(root).Length == 2, "Zmieniono liczbe starszych kopii: " + string.Join(", ", Backups(root)));
    }

    private static void ReencodeWarningMatchesRetention(string root)
    {
        RequireFfmpeg();
        var target = Path.Combine(root, "cel.mp3");
        var source = Path.Combine(root, "zrodlo.wav");
        WriteSilence(source, TimeSpan.FromSeconds(4));
        Wait(AudioClipAppender.EncodeRangeAsync(
            source, target, TimeSpan.Zero, TimeSpan.FromSeconds(3), CancellationToken.None));

        var removed = Wait(AudioClipAppender.AppendAsync(
            new AudioClipAppendRequest(source, target, TimeSpan.Zero, TimeSpan.FromSeconds(1)),
            null,
            CancellationToken.None));
        Check(removed.TargetWasReencoded, "Dopisanie do MP3 nie zglosilo ponownego kodowania.");
        Check(removed.BackupPath.Length == 0, "Kopia po dopisaniu do MP3 nie zostala usunieta.");
        Check(
            !string.IsNullOrWhiteSpace(removed.ReencodeWarning)
            && !removed.ReencodeWarning!.Contains("kopii", StringComparison.OrdinalIgnoreCase),
            "Ostrzezenie mowi o kopii zapasowej, ktorej juz nie ma: " + removed.ReencodeWarning);

        var kept = Wait(AudioClipAppender.AppendAsync(
            new AudioClipAppendRequest(source, target, TimeSpan.Zero, TimeSpan.FromSeconds(1)),
            null,
            CancellationToken.None,
            keepBackup: true));
        Check(kept.BackupPath.Length != 0 && File.Exists(kept.BackupPath), "Wybrana kopia MP3 nie istnieje.");
        Check(
            kept.ReencodeWarning!.Contains(Path.GetFileName(kept.BackupPath), StringComparison.Ordinal),
            "Ostrzezenie nie wskazuje zachowanej kopii: " + kept.ReencodeWarning);
    }

    // ---- blad i niepewnosc: material zostaje ----

    private static void CutCancellationLosesNothing(string root)
    {
        RequireFfmpeg();
        var source = Path.Combine(root, "ciecie.wav");
        WriteSilence(source, TimeSpan.FromSeconds(40));
        var before = Hash(source);

        using var cancellation = new CancellationTokenSource();
        var progress = new Progress<double>(_ => cancellation.Cancel());
        var canceled = false;
        try
        {
            Wait(AudioClipOriginalEditor.RemoveAsync(
                new AudioClipRemovalRequest(
                    source,
                    TimeSpan.FromSeconds(1),
                    TimeSpan.FromSeconds(20),
                    TimeSpan.FromSeconds(40)),
                progress,
                cancellation.Token));
        }
        catch (OperationCanceledException) { canceled = true; }
        catch (InvalidDataException) { canceled = true; }

        Check(canceled, "Anulowanie nie przerwalo ciecia.");
        Check(Hash(source) == before, "Anulowanie zmienilo plik zrodlowy.");
        Check(Backups(root).Length == 0, "Anulowanie zostawilo kopie zapasowa: " + string.Join(", ", Backups(root)));
    }

    private static void CutFailureLeavesOriginal(string root)
    {
        RequireFfmpeg();
        // Zaznaczenie poza osia czasu: blad ma wyjsc PRZED podmiana pliku.
        var source = Path.Combine(root, "ciecie.wav");
        WriteSilence(source, TimeSpan.FromSeconds(3));
        var before = Hash(source);
        var failed = false;
        try
        {
            Wait(AudioClipOriginalEditor.RemoveAsync(
                new AudioClipRemovalRequest(
                    source,
                    TimeSpan.FromSeconds(1),
                    TimeSpan.FromSeconds(9),
                    TimeSpan.FromSeconds(3)),
                null,
                CancellationToken.None));
        }
        catch (ArgumentException) { failed = true; }
        catch (InvalidDataException) { failed = true; }

        Check(failed, "Nieprawidlowe zaznaczenie nie zostalo odrzucone.");
        Check(Hash(source) == before, "Odrzucone ciecie zmienilo plik.");
        Check(Backups(root).Length == 0, "Odrzucone ciecie zostawilo kopie zapasowa.");
    }

    // ---- polityka po podmianie: mierzona wprost na wspolnym helperze ----

    private static void CommitMismatchKeepsBackup(string root)
    {
        var destination = Path.Combine(root, "cel.bin");
        var result = Path.Combine(root, "wynik.bin");
        var backup = destination + ".20260925-000000.amc-backup";
        File.WriteAllText(destination, "poprzednia wersja");
        File.WriteAllText(result, "sprawdzony wynik");

        // Podmiana, ktora zapisuje CO INNEGO niz sprawdzony wynik - dokladnie ta
        // rozbieznosc, ktorej kontrola przed podmiana nie moze zobaczyc.
        var failure = Catch(() => Wait(AudioEditBackupRetention.CommitAsync(
            result,
            destination,
            backup,
            keepBackup: false,
            (from, to, backupTo) =>
            {
                File.Move(to, backupTo);
                File.WriteAllText(to, "cos zupelnie innego");
                File.Delete(from);
            },
            "test",
            CancellationToken.None)));

        Check(failure is InvalidDataException, "Rozbieznosc po podmianie nie zglosila bledu: " + failure);
        Check(File.Exists(backup), "Rozbieznosc po podmianie usunela kopie zapasowa.");
        Check(File.ReadAllText(backup) == "poprzednia wersja", "Kopia po bledzie nie jest poprzednia wersja.");
        Check(
            !failure!.Message.Contains("nie został zmieniony", StringComparison.OrdinalIgnoreCase)
            && !failure.Message.Contains("nietknięty", StringComparison.OrdinalIgnoreCase),
            "Komunikat po podmianie obiecuje nietkniety oryginal: " + failure.Message);
        Check(
            failure.Message.Contains(Path.GetFileName(backup), StringComparison.Ordinal),
            "Komunikat nie wskazuje zachowanej kopii: " + failure.Message);
    }

    private static void CommitUnreadableKeepsBackup(string root)
    {
        var destination = Path.Combine(root, "cel.bin");
        var result = Path.Combine(root, "wynik.bin");
        var backup = destination + ".20260925-000000.amc-backup";
        File.WriteAllText(destination, "poprzednia wersja");
        File.WriteAllText(result, "sprawdzony wynik");

        var failure = Catch(() => Wait(AudioEditBackupRetention.CommitAsync(
            result,
            destination,
            backup,
            keepBackup: false,
            (from, to, backupTo) =>
            {
                File.Move(to, backupTo);
                File.Move(from, to);
                File.Delete(to);
            },
            "test",
            CancellationToken.None)));

        Check(failure is IOException, "Nieczytelny plik po podmianie nie zglosil bledu: " + failure);
        Check(File.Exists(backup), "Nieczytelny wynik po podmianie usunal kopie zapasowa.");
        Check(File.ReadAllText(backup) == "poprzednia wersja", "Kopia po bledzie nie jest poprzednia wersja.");
    }

    private static void DeleteFailureKeepsBackupPath(string root)
    {
        var destination = Path.Combine(root, "cel.bin");
        var backup = destination + ".20260925-000000.amc-backup";
        File.WriteAllText(destination, "zapisany plik");
        File.WriteAllText(backup, "poprzednia wersja");

        using (var hold = new FileStream(backup, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var kept = AudioEditBackupRetention.TryRemoveBackup(backup, destination, "test");
            Check(kept == backup, "Nieudane usuniecie nie zwrocilo sciezki kopii: '" + kept + "'");
        }

        Check(File.Exists(backup), "Kopia zniknela, choc usuniecie mialo sie nie udac.");
        Check(File.ReadAllText(destination) == "zapisany plik", "Nieudane usuniecie kopii tknelo zapisany plik.");
        var removed = AudioEditBackupRetention.TryRemoveBackup(backup, destination, "test");
        Check(removed.Length == 0, "Udane usuniecie nadal zwraca sciezke kopii: " + removed);
        Check(!File.Exists(backup), "Kopia zostala po udanym usunieciu.");
    }

    // ---- narzedzia proby ----

    private static string[] Backups(string root) => Directory
        .GetFiles(root)
        .Where(path => path.EndsWith(".amc-backup", StringComparison.OrdinalIgnoreCase))
        .Select(path => Path.GetFileName(path)!)
        .ToArray();

    private static void RequireFfmpeg()
    {
        if (!AudioClipOriginalEditor.IsAvailable)
            throw new InvalidOperationException("Proba wymaga skladnika FFmpeg; bez niego wynik nie jest zaliczony.");
    }

    private static void WriteSilence(string path, TimeSpan duration)
    {
        var format = new WaveFormat(8_000, 16, 1);
        using var writer = new WaveFileWriter(path, format);
        writer.Write(new byte[(int)(format.AverageBytesPerSecond * duration.TotalSeconds)]);
    }

    private static string Hash(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    private static Exception? Catch(Action body)
    {
        try
        {
            body();
            return null;
        }
        catch (Exception error)
        {
            return error;
        }
    }

    private static T Wait<T>(Task<T> task)
    {
        try
        {
            return task.GetAwaiter().GetResult();
        }
        catch (AggregateException error) when (error.InnerException is not null)
        {
            throw error.InnerException;
        }
    }

    private static void Wait(Task task)
    {
        try
        {
            task.GetAwaiter().GetResult();
        }
        catch (AggregateException error) when (error.InnerException is not null)
        {
            throw error.InnerException;
        }
    }

    private static void Check(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
    }

    private static void TryDeleteDirectory(string root)
    {
        for (var attempt = 1; attempt <= 10; attempt++)
        {
            try
            {
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
                return;
            }
            catch (IOException)
            {
                Thread.Sleep(100 * attempt);
            }
            catch (UnauthorizedAccessException)
            {
                Thread.Sleep(100 * attempt);
            }
        }
    }
}

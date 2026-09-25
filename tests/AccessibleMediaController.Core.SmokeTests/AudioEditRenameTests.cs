using System.Collections.Concurrent;
using System.Diagnostics;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.LocalMedia;
using AccessibleMediaController.Core.Presentation;

internal static class AudioEditRenameTests
{
    internal static void Run()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        var tests = new (string Name, Action Test)[]
        {
            ("kopia nie przejmuje historii", BackupDoesNotTakeHistory),
            ("nieistniejąca już kopia i wielkość liter", DeletedBackupDoesNotTakeHistory),
            ("plik roboczy cięcia nie przejmuje historii", () => WorkingFileDoesNotTakeHistory("cut")),
            ("plik roboczy dopisywania nie przejmuje historii", () => WorkingFileDoesNotTakeHistory("append")),
            ("zwykła zmiana nazwy pozostaje dozwolona", OrdinaryRename),
            ("zmiana folderu sprawdza końcową ścieżkę pliku", FolderRename),
            ("ręczne odzyskanie starego wpisu z kopii pozostaje dozwolone", RestoreOldBackupReference),
            ("dwie natywne podmiany, zmiana nazwy i zapis/odczyt", NativeReplacementSequence),
        };
        var failures = new List<string>();
        foreach (var (name, test) in tests)
        {
            try { test(); Console.WriteLine("OK: " + name); }
            catch (Exception exception) { failures.Add(name); Console.Error.WriteLine(name + ": " + exception); }
        }
        Console.WriteLine($"AUDIO EDIT RENAME: {tests.Length - failures.Count} OK / {failures.Count} BLAD / razem {tests.Length}");
        if (failures.Count != 0) throw new InvalidOperationException(string.Join("; ", failures));
    }

    private static string SourcePath() => Path.GetFullPath(Path.Combine(Path.GetTempPath(), "amc-edit-rename-test", "recording.mp3"));
    private static RadioRecordingHistorySettings Entry(string path) => new()
    {
        Id = "recording", Path = path, StationId = "test-station", StationName = "Stacja próby",
        Outcome = RadioRecordingOutcome.Completed, SavedFileCount = 1
    };

    private static void BackupDoesNotTakeHistory()
    {
        var source = SourcePath(); var entry = Entry(source);
        var changed = RadioRecordingHistoryPathRewriter.Rewrite(new[] { entry }, source, source + ".20260925-120000.amc-backup");
        Check(changed == 0 && entry.Path == source, "Podmiana po edycji przepięła historię na kopię bezpieczeństwa: " + entry.Path);
    }

    private static void DeletedBackupDoesNotTakeHistory()
    {
        var source = SourcePath(); var entry = Entry(source);
        var backup = source + "." + Guid.NewGuid().ToString("N") + ".AMC-BACKUP";
        Check(!File.Exists(backup), "Kontrolka kopii już usuniętej");
        var changed = RadioRecordingHistoryPathRewriter.Rewrite(new[] { entry }, source, backup);
        Check(changed == 0 && entry.Path == source, "Podmiana po edycji przepięła historię mimo usunięcia kopii");
    }

    private static void WorkingFileDoesNotTakeHistory(string operation)
    {
        var source = SourcePath(); var entry = Entry(source);
        var working = Path.Combine(Path.GetDirectoryName(source)!, $".recording.amc-{operation}-probe-result.mp3");
        var changed = RadioRecordingHistoryPathRewriter.Rewrite(new[] { entry }, source, working);
        Check(changed == 0 && entry.Path == source, "Historia wskazuje plik roboczy: " + working);
        Check(!LocalAudioFileDiscovery.IsAudioFile(working), "Biblioteka przyjmuje plik roboczy: " + working);
    }

    private static void OrdinaryRename()
    {
        var source = SourcePath(); var entry = Entry(source);
        var renamed = Path.Combine(Path.GetDirectoryName(source)!, "Mrówczyński recording.mp3");
        Check(RadioRecordingHistoryPathRewriter.Rewrite(new[] { entry }, source, renamed) == 1 && entry.Path == renamed,
            "Zwykła zmiana nazwy nie aktualizuje historii");
    }

    private static void FolderRename()
    {
        var source = SourcePath(); var entry = Entry(source);
        var oldFolder = Path.GetDirectoryName(source)!;
        var newFolder = oldFolder + ".amc-backup";
        var expected = Path.Combine(newFolder, Path.GetFileName(source));
        Check(RadioRecordingHistoryPathRewriter.Rewrite(new[] { entry }, oldFolder, newFolder) == 1 && entry.Path == expected,
            "Zmiana nazwy folderu została pomylona z plikiem kopii");
    }

    private static void RestoreOldBackupReference()
    {
        var source = SourcePath(); var backup = source + ".20260925-120000.amc-backup";
        var entry = Entry(backup);
        Check(RadioRecordingHistoryPathRewriter.Rewrite(new[] { entry }, backup, source) == 1 && entry.Path == source,
            "Jawne przywrócenie nagrania z kopii nie naprawia starego wpisu");
    }

    private static void NativeReplacementSequence()
    {
        var root = Path.Combine(Path.GetTempPath(), "amc-edit-native-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var source = Path.Combine(root, "nagranie.mp3");
            var result = Path.Combine(root, ".nagranie.amc-cut-probe-result.mp3");
            var renamed = Path.Combine(root, "Mrówczyński nagranie.mp3");
            var firstBackup = source + ".20260925-120000.amc-backup";
            var secondBackup = source + ".20260925-120001.amc-backup";
            File.WriteAllText(source, "SYNTHETIC FILE CONTENT; not audio: before");
            var entry = Entry(source);
            var events = new ConcurrentQueue<(string Old, string New)>();
            using var signal = new AutoResetEvent(false);
            using var watcher = new FileSystemWatcher(root) { NotifyFilter = NotifyFilters.FileName };
            watcher.Renamed += (_, e) => { events.Enqueue((e.OldFullPath, e.FullPath)); signal.Set(); };
            watcher.EnableRaisingEvents = true;
            void Drain()
            {
                var timer = Stopwatch.StartNew();
                while (timer.ElapsedMilliseconds < 5000 && signal.WaitOne(600)) { }
            }
            File.WriteAllText(result, "SYNTHETIC FILE CONTENT; not audio: first edit");
            File.Replace(result, source, firstBackup, ignoreMetadataErrors: true); Drain();
            File.WriteAllText(result, "SYNTHETIC FILE CONTENT; not audio: second edit");
            File.Replace(result, source, secondBackup, ignoreMetadataErrors: true); Drain();
            File.Move(source, renamed); Drain();
            watcher.EnableRaisingEvents = false;
            var observed = events.ToArray();
            Check(observed.Any(e => e.Old == source && e.New == firstBackup), "Sonda nie odebrała natywnego rename do pierwszej kopii");
            Check(observed.Any(e => e.Old == source && e.New == secondBackup), "Sonda nie odebrała natywnego rename do drugiej kopii");
            Check(observed.Any(e => e.Old == source && e.New == renamed), "Sonda nie odebrała zwykłej zmiany nazwy");
            // Emulate delayed delivery to the UI after successful cleanup.
            File.Delete(firstBackup); File.Delete(secondBackup);
            foreach (var e in observed) RadioRecordingHistoryPathRewriter.Rewrite(new[] { entry }, e.Old, e.New);
            Check(entry.Path == renamed && File.Exists(entry.Path), "Podmiana po edycji przepięła historię na nieistniejącą kopię");
            Check(entry.Id == "recording", "Zmieniła się tożsamość wpisu");
            var statePath = Path.Combine(root, "private-state", "state.json");
            var store = new ConfigurationStore(statePath);
            var state = store.LoadOrCreate();
            state.Radio.RecordingHistory.Clear(); state.Radio.RecordingHistory.Add(entry); store.Save(state);
            var restored = new ConfigurationStore(statePath).LoadOrCreate().Radio.RecordingHistory.Single();
            Check(restored.Path == renamed && restored.Id == entry.Id, "Zapis/odczyt przywrócił stare powiązanie historii");
        }
        finally { Directory.Delete(root, true); }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

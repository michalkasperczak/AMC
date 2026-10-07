using System.Windows.Threading;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Windows;

/// <summary>
/// POKAZ NA PULPICIE dla pomiaru ZYWYM NVDA: Shift+F2 na pliku „No cześć.wav”.
/// Ten sam PRODUKCYJNY <c>MainWindow</c> i ten sam handler skrotu, co u uzytkownika.
///
/// ZERO KONTA, ZERO SIECI, ZERO NAGRAN uzytkownika: wlasny katalog tymczasowy,
/// wlasny krotki plik WAV wygenerowany tutaj, wlasny state.json. Integracja z
/// pulpitem wylaczona (SuppressDesktopIntegrationForTests), a automatyczna
/// aktualizacja zablokowana przez _applicationUpdateStartOverride.
/// Twardy limit czasu zamyka okno, gdy pomiar sie urwie.
/// </summary>
internal static class RenameCaseNvdaGui
{
    internal static void Run(string[] args)
    {
        var seconds = int.TryParse(
            args.FirstOrDefault(a => a.StartsWith("--seconds=", StringComparison.Ordinal))
                ?.Split('=', 2)[1],
            out var parsed) ? parsed : 240;

        var root = Path.Combine(Path.GetTempPath(), "amc419-nvda-rename-" + Guid.NewGuid().ToString("N"));
        var folder = Path.Combine(root, "Nagrania próby");
        Directory.CreateDirectory(folder);
        var plik = Path.Combine(folder, "No cześć.wav");
        File.WriteAllBytes(plik, MinimalnyWav());
        // Drugi plik: zajeta nazwa, zeby dalo sie zmierzyc ODMOWE kolizji bez nadpisania.
        var obcy = Path.Combine(folder, "Zajete.wav");
        File.WriteAllBytes(obcy, MinimalnyWav());

        var store = new ConfigurationStore(Path.Combine(root, "state.json"));
        var state = store.LoadOrCreate();
        state.LocalMedia.FolderSources.Clear();
        state.LocalMedia.FolderSources.Add(new LocalFolderSourceSettings
        {
            Path = folder,
            DisplayName = "Nagrania próby"
        });
        state.Settings.LastSessionId = "local";
        state.LocalMedia.LibraryView = "Wszystkie pliki";

        state.LocalMedia.Items.Clear();
        state.LocalMedia.Items.Add(new LocalMediaItemSettings
        {
            Id = "proba-no-czesc",
            Title = "No cześć",
            Path = plik,
            IsInLibrary = true,
            IsAvailable = true,
            DurationTicks = TimeSpan.FromSeconds(1).Ticks
        });
        state.LocalMedia.Items.Add(new LocalMediaItemSettings
        {
            Id = "proba-zajete",
            Title = "Zajete",
            Path = obcy,
            IsInLibrary = true,
            IsAvailable = true,
            DurationTicks = TimeSpan.FromSeconds(1).Ticks
        });
        store.Save(state);

        Console.WriteLine("FOLDER: " + folder);
        Console.WriteLine("PLIK: " + plik);
        Console.WriteLine("OBCY: " + obcy);

        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var window = new MainWindow(store.LoadOrCreate(), store)
                {
                    SuppressDesktopIntegrationForTests = true
                };
                typeof(MainWindow)
                    .GetField("_applicationUpdateStartOverride",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .SetValue(window, (Func<bool, bool>)(_ => throw new Exception("Pomiar zabrania instalacji")));
                window.Title += "  [POMIAR NVDA 419 ZMIANA PISOWNI]";

                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(seconds) };
                timer.Tick += (_, _) => { timer.Stop(); if (window.IsVisible) window.Close(); };
                timer.Start();

                window.Show();
                window.Activate();
                Console.WriteLine($"POKAZANO MainWindow (limit {seconds} s)");
                Dispatcher.Run();
            }
            catch (Exception exception) { failure = exception; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Console.WriteLine("STAN KONCOWY FOLDERU:");
        foreach (var entry in Directory.GetFiles(folder).OrderBy(x => x, StringComparer.Ordinal))
        {
            Console.WriteLine("  " + Path.GetFileName(entry) + "  " + new FileInfo(entry).Length + " B");
        }
        var zapisany = new ConfigurationStore(Path.Combine(root, "state.json")).LoadOrCreate();
        foreach (var item in zapisany.LocalMedia.Items)
        {
            Console.WriteLine("BIBLIOTEKA: " + item.Path);
        }
        if (failure is not null) throw failure;
        Console.WriteLine("KONIEC POKAZU: rename-case");
    }

    /// <summary>Najkrotszy poprawny WAV PCM (cisza), zeby program przyjal plik.</summary>
    private static byte[] MinimalnyWav()
    {
        using var buffer = new MemoryStream();
        using var writer = new BinaryWriter(buffer);
        const int sampleRate = 8000;
        const int samples = 800;
        var dataBytes = samples * 2;
        writer.Write("RIFF".ToCharArray());
        writer.Write(36 + dataBytes);
        writer.Write("WAVE".ToCharArray());
        writer.Write("fmt ".ToCharArray());
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(sampleRate);
        writer.Write(sampleRate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write("data".ToCharArray());
        writer.Write(dataBytes);
        for (var i = 0; i < samples; i++) writer.Write((short)0);
        writer.Flush();
        return buffer.ToArray();
    }
}

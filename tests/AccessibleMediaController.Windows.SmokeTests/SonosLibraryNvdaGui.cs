using System.Windows;
using System.Windows.Threading;
using AccessibleMediaController.Core.Sonos;
using AccessibleMediaController.Windows;

/// <summary>
/// POKAZ NA PULPICIE dla pomiaru ZYWYM NVDA. Nie jest to drugi zestaw testow:
/// te same PRODUKCYJNE okna (<c>SonosLibraryWindow</c>,
/// <c>SonosPlaylistsWindow</c>, <c>SonosTargetSelectionWindow</c>) staja na
/// pulpicie, zeby czytnik ekranu mogl je PRZECZYTAC. Testy w kodzie dowodza, ze
/// program WYSYLA tekst; dopiero zywy NVDA mowi, czy go przeczyta.
///
/// ZERO SIECI I ZERO KONTA: playlisty sa syntetyczne, zaplecze uruchamiania to
/// atrapa w pamieci, a topologia jest zlozona z recznie podanych nazw. Zaden
/// prawdziwy magazyn poswiadczen, token ani Control API nie jest tu dotykany.
/// Nie powstaje tez produkcyjny MainWindow, wiec nie ma IPC ani globalnych
/// skrotow.
/// </summary>
internal static class SonosLibraryNvdaGui
{
    internal static void Run(string[] args)
    {
        // KTORE okno pokazac. Pomiar zywym czytnikiem prowadzi czlowiek/agent z
        // zewnatrz, wiec kazde okno dostaje wlasne uruchomienie - inaczej jedno
        // modalne okno zasloniloby nastepne.
        var stage = args.FirstOrDefault(a => a.StartsWith("--stage=", StringComparison.Ordinal))
            ?.Split('=', 2)[1] ?? "library";
        var seconds = int.TryParse(
            args.FirstOrDefault(a => a.StartsWith("--seconds=", StringComparison.Ordinal))
                ?.Split('=', 2)[1],
            out var parsed) ? parsed : 150;

        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var window = CreateWindow(stage);
                window.Title += "  [POMIAR NVDA]";
                window.WindowStartupLocation = WindowStartupLocation.CenterScreen;

                // TWARDY LIMIT: okno pomiarowe nie ma prawa zostac na pulpicie,
                // gdy pomiar sie urwie.
                var timer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(seconds)
                };
                timer.Tick += (_, _) =>
                {
                    timer.Stop();
                    if (window.IsVisible) window.Close();
                };
                timer.Start();

                window.Show();
                window.Activate();
                Console.WriteLine($"POKAZANO: {stage} / \"{window.Title}\" (limit {seconds} s)");
                Dispatcher.Run();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            finally
            {
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw failure;
        Console.WriteLine("KONIEC POKAZU: " + stage);
    }

    private static Window CreateWindow(string stage) => stage switch
    {
        "library" => new SonosLibraryWindow(null),
        "playlists" => CreatePlaylistsWindow(),
        "target" => CreateTargetWindow(),
        "target-empty" => new SonosTargetSelectionWindow(SonosTargetSelectionLabels.NotSignedIn),
        _ => throw new Exception(
            $"Nieznany etap pokazu \"{stage}\". Dozwolone: library, playlists, target, target-empty.")
    };

    /// <summary>
    /// Okno playlist z SYNTETYCZNA kolekcja i atrapa uruchamiania. Dwie pozycje
    /// o TYM SAMYM tytule i roznych identyfikatorach - zeby zywy czytnik
    /// pokazal, czy uzytkownik ma je czym rozroznic.
    /// </summary>
    private static Window CreatePlaylistsWindow()
    {
        var playlists = new[]
        {
            new SonosPlaylist("PL-1", "Poranek"),
            new SonosPlaylist("PL-2", "Do pracy"),
            new SonosPlaylist("PL-3", "Do pracy")
        };

        SonosPlaylistsWindow window = null!;
        window = new SonosPlaylistsWindow(
            playlists,
            "Salon i Kuchnia",
            request =>
            {
                // ATRAPA: zadnego HTTP. Odpowiadamy TYLKO wlasnemu, zyjacemu oknu
                // i mowimy o PRZYJECIU zlecenia, nie o potwierdzonym odtwarzaniu.
                if (!ReferenceEquals(request.Origin, window)) return Task.CompletedTask;
                if (!window.IsLiveOwnerTarget) return Task.CompletedTask;
                window.AnnounceForOwner(
                    $"Zlecono odtworzenie playlisty (atrapa pomiaru, identyfikator {request.Playlist.Id}). "
                    + "AMC nie potwierdza, że już gra.");
                return Task.CompletedTask;
            });
        return window;
    }

    /// <summary>Wybor celu na RECZNIE podanej topologii - pelne nazwy glosnikow.</summary>
    private static Window CreateTargetWindow()
    {
        SonosPlayer Player(string id, string name) => new(id, name, null, null, null);

        var topology = new SonosHouseholdTopology(
            [
                new SonosGroup("GRUPA-SALON", "Salon", "GLOSNIK-SALON",
                    ["GLOSNIK-SALON"], SonosPlaybackState.Unknown),
                new SonosGroup("GRUPA-BIURO", "Biuro + 1", "GLOSNIK-BIURO",
                    ["GLOSNIK-BIURO", "GLOSNIK-SYPIALNIA"], SonosPlaybackState.Unknown)
            ],
            [
                Player("GLOSNIK-SALON", "Salon"),
                Player("GLOSNIK-BIURO", "Biuro"),
                Player("GLOSNIK-SYPIALNIA", "Sypialnia")
            ],
            partial: false);

        return new SonosTargetSelectionWindow(topology, "GRUPA-SALON");
    }
}

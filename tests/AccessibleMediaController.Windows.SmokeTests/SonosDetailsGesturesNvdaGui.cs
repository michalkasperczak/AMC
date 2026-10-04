using System.Windows;
using System.Windows.Threading;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sonos;
using AccessibleMediaController.Windows;

/// <summary>
/// POKAZ NA PULPICIE dla pomiaru ZYWYM NVDA: Strzalka w lewo (parametry) oraz
/// Ctrl+C / Ctrl+Shift+C (nazwa / adres) w MOICH STACJACH i w ULUBIONYCH.
///
/// Testy w kodzie (<c>--sonos-details-gestures</c>) dowodza, ze produkcyjny
/// handler WIDZI gest i ze do schowka idzie wlasciwy napis. Dopiero zywy NVDA
/// mowi, czy uzytkownik to USLYSZY - dlatego tutaj staja TE SAME PRODUKCYJNE
/// okna, bez atrapy zapowiedzi.
///
/// ZERO SIECI, ZERO KONTA, ZERO IPC: ulubione i stacje sa podane recznie,
/// parametry oddaje atrapa W PAMIECI (zadnego Control API, zadnego tokenu),
/// a produkcyjny <c>MainWindow</c> NIE powstaje - wiec nie ma globalnych
/// skrotow ani potoku do dzialajacego AMC. Odsluchem glosnika to NIE JEST.
/// </summary>
internal static class SonosDetailsGesturesNvdaGui
{
    internal static void Run(string[] args)
    {
        var stage = args.FirstOrDefault(a => a.StartsWith("--stage=", StringComparison.Ordinal))
            ?.Split('=', 2)[1] ?? "stations";
        var seconds = int.TryParse(
            args.FirstOrDefault(a => a.StartsWith("--seconds=", StringComparison.Ordinal))
                ?.Split('=', 2)[1],
            out var parsed) ? parsed : 180;

        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var window = CreateWindow(stage);
                window.Title += "  [POMIAR NVDA: Strzałka w lewo, Ctrl+C, Ctrl+Shift+C]";
                window.WindowStartupLocation = WindowStartupLocation.CenterScreen;

                // TWARDY LIMIT: okno pomiarowe nie zostaje na pulpicie, gdy
                // pomiar sie urwie. Pulpit oddajemy JAWNIE.
                // ZMIERZONE: samo window.Close() NIE konczy Dispatcher.Run - petla
                // zostaje, finally z InvokeShutdown nigdy nie zostaje osiagniete i
                // CALY PROCES pokazu wisi po limicie. Dispatcher konczymy JAWNIE po
                // zdarzeniu Closed, zeby --seconds=1 naprawde oddalo pulpit.
                var dispatcher = Dispatcher.CurrentDispatcher;
                window.Closed += (_, _) => dispatcher.InvokeShutdown();

                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(seconds) };
                timer.Tick += (_, _) =>
                {
                    timer.Stop();
                    if (window.IsVisible) window.Close();
                    else dispatcher.InvokeShutdown();
                };
                timer.Start();

                window.Show();
                window.Activate();
                Console.WriteLine("POKAZANO: " + stage + " / PID=" + Environment.ProcessId
                    + " / HWND=" + new System.Windows.Interop.WindowInteropHelper(window).Handle
                    + " (limit " + seconds + " s)");
                Dispatcher.Run();
            }
            catch (Exception exception) { failure = exception; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw failure;
        Console.WriteLine("KONIEC POKAZU: " + stage);
    }

    private static Window CreateWindow(string stage) => stage switch
    {
        "stations" => CreateOwnStreamsWindow(),
        "favorites" => CreateFavoritesWindow(),
        _ => throw new Exception("Nieznany etap pokazu \"" + stage
            + "\". Dozwolone: stations, favorites.")
    };

    /// <summary>
    /// MOJE STACJE z trzema wpisami: dwa z adresem i JEDEN BEZ ADRESU - zeby
    /// zywy czytnik pokazal takze KROTKA ODMOWE przy Ctrl+Shift+C.
    /// Parametry oddaje atrapa w pamieci, ZAMIAST siegac do sieci.
    /// </summary>
    private static Window CreateOwnStreamsWindow()
    {
        SonosOwnStreamSettings[] stations =
        [
            new() { Id = "NVDA-1", Name = "Radio Pierwsze", StreamUrl = "http://przyklad.test/jeden" },
            new() { Id = "NVDA-2", Name = "Radio Drugie", StreamUrl = "http://przyklad.test/dwa" },
            new() { Id = "NVDA-3", Name = "Stacja bez adresu", StreamUrl = "" },
        ];

        // SAVE NIC NIE ZAPISUJE: pokaz nie ma prawa dotknac ustawien uzytkownika.
        return new SonosOwnStreamsWindow(stations, "Salon i Kuchnia", _ => { }, null)
        {
            // ATRAPA PARAMETROW: zadnego HTTP i zadnego BASS. Mowi WPROST, skad
            // pochodzi tekst, zeby pomiar nie zostal wziety za odczyt strumienia.
            DescribeStation = station => Task.FromResult(
                station.Name + ", audio/mpeg, mp3, 128 kb/s, 44,1 kHz "
                + "(atrapa pomiaru NVDA, nie odczyt ze strumienia)")
        };
    }

    /// <summary>
    /// ULUBIONE z OPISEM i USLUGA - zeby zywy czytnik pokazal, ze Ctrl+C mowi
    /// "Skopiowano nazwę" i do schowka idzie SAMA NAZWA, a Strzalka w lewo
    /// dokłada opis i usluge BEZ powtarzania nazwy. Adresu getFavorites NIE
    /// zwraca, wiec Ctrl+Shift+C ma tu powiedziec KROTKA ODMOWE.
    /// </summary>
    private static Window CreateFavoritesWindow()
    {
        var favorites = new[]
        {
            new SonosFavorite("ULU-1", "Radio Nasze", "Muzyka klasyczna bez przerw",
                new SonosFavoriteService("TuneIn", "254"), null),
            new SonosFavorite("ULU-2", "Nokturny Chopina", "Nagranie z 1999 roku",
                new SonosFavoriteService("Tidal", "38"), null),
        };

        SonosFavoritesWindow window = null!;
        window = new SonosFavoritesWindow(favorites, "Salon i Kuchnia", null)
        {
            DescribeFavorite = favorite => Task.FromResult(
                SonosFavoriteDetails.DescribeParameters(favorite, null)),
            // ADRESU NIE MA i nie wymyslamy go - taka jest prawda o getFavorites.
            ResolveFavoriteLocation = _ => Task.FromResult<string?>(null),
        };
        return window;
    }
}

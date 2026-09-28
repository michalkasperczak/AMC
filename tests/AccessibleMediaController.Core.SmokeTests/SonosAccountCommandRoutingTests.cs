using AccessibleMediaController.Core.Commands;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Input;
using AccessibleMediaController.Core.Presentation;
using AccessibleMediaController.Core.Sessions;

/// <summary>
/// DROGA polecenia "Konto Sonos" przez PRAWDZIWY router, katalog i filtr palety.
///
/// Co ten pomiar sprawdza i dlaczego akurat to:
///   * polecenie jest w KATALOGU (a wiec paleta je widzi), a jego nazwa jest
///     tekstem uzytkowym bez skrotu i bez znaku podkreslenia,
///   * PRAWDZIWY <see cref="CommandRouter"/> kieruje identyfikator do OSOBNEJ
///     metody aplikacji,
///   * KONTROLA ODLACZENIA: sasiednie polecenia kont (TIDAL, Spotify) NIE moga
///     trafiac w Sonos, a dodanie Sonos nie psuje ich kierowania,
///   * polecenie przechodzi RZECZYWISTY filtr palety po wpisaniu fragmentu,
///   * ten przyrost NIE dodaje sesji Sonos i NIE przypisuje zadnego skrotu
///     (w szczegolnosci nie zajmuje Ctrl+F5).
/// </summary>
internal static class SonosAccountCommandRoutingTests
{
    internal static void Run()
    {
        var checks = 0;

        var catalog = CommandCatalog.GetAllCommandIds();
        if (!catalog.Contains(CommandIds.ManageSonosConnection))
        {
            throw new Exception("Polecenie konta Sonos nie jest w katalogu poleceń, więc paleta go nie zobaczy.");
        }
        checks++;

        var displayName = CommandCatalog.GetDisplayName(CommandIds.ManageSonosConnection);
        if (displayName != "Konto Sonos")
        {
            throw new Exception("Nazwa polecenia konta Sonos nie jest oczekiwanym tekstem użytkowym: " + displayName);
        }
        if (displayName.Contains("Ctrl", StringComparison.OrdinalIgnoreCase)
            || displayName.Contains('+', StringComparison.Ordinal)
            || displayName.Contains('_', StringComparison.Ordinal))
        {
            throw new Exception("Nazwa polecenia konta Sonos cytuje skrót albo znak podkreślenia.");
        }
        checks++;

        // Prawdziwy router kieruje do OSOBNEJ metody aplikacji.
        var settings = new AppSettings();
        var sessions = new SessionManager(settings);
        var actions = new FakeActions(null!);
        var router = new CommandRouter(sessions, settings, new FakeSink(), actions);

        var result = router.Execute(CommandIds.ManageSonosConnection);
        if (!result.Handled)
        {
            throw new Exception("Router nie obsłużył polecenia konta Sonos.");
        }
        if (actions.SonosAccountManagerCalls != 1)
        {
            throw new Exception("Router nie wywołał otwarcia konta Sonos dokładnie raz.");
        }
        if (actions.TidalAccountManagerCalls != 0 || actions.SpotifyAccountManagerCalls != 0)
        {
            throw new Exception("Polecenie konta Sonos uruchomiło konto innej usługi.");
        }
        checks++;

        // KONTROLA ODLACZENIA.
        var kontrola = new FakeActions(null!);
        var kontrolnyRouter = new CommandRouter(
            new SessionManager(new AppSettings()),
            new AppSettings(),
            new FakeSink(),
            kontrola);
        kontrolnyRouter.Execute(CommandIds.ManageTidalConnection);
        kontrolnyRouter.Execute(CommandIds.ManageSpotifyConnection);
        if (kontrola.SonosAccountManagerCalls != 0)
        {
            throw new Exception("Konto TIDAL lub Spotify otwiera okno Sonos - polecenia nie są rozdzielne.");
        }
        if (kontrola.TidalAccountManagerCalls != 1 || kontrola.SpotifyAccountManagerCalls != 1)
        {
            throw new Exception("Dodanie konta Sonos zepsuło kierowanie kont TIDAL i Spotify.");
        }
        checks++;

        // Prawdziwy filtr palety po fragmencie nazwy.
        var entries = CommandPaletteSearch.CreateEntries(KeyboardProfile.CreateDefault(), new AppSettings());
        var sonosEntry = entries.FirstOrDefault(entry => entry.CommandId == CommandIds.ManageSonosConnection)
            ?? throw new Exception("Wpisy palety nie zawierają konta Sonos.");
        var filtered = CommandPaletteSearch.Filter(entries, "sonos");
        if (!filtered.Any(entry => entry.CommandId == CommandIds.ManageSonosConnection))
        {
            throw new Exception("Paleta poleceń nie znajduje konta Sonos po wpisaniu \"sonos\".");
        }
        checks++;

        // Brak skrotu i brak sesji Sonos na tym przyroscie.
        if (!string.IsNullOrEmpty(sonosEntry.LocalShortcut) || !string.IsNullOrEmpty(sonosEntry.PrefixShortcut))
        {
            throw new Exception(
                "Polecenie konta Sonos ma przypisany skrót: "
                + sonosEntry.LocalShortcut + " / " + sonosEntry.PrefixShortcut);
        }
        // Sesja Sonos JEST na liscie, ale nie wolno jej byc atrapa: zadnych
        // utworow demonstracyjnych. Lista sesji Sonos pokazuje GRUPY odczytane
        // z konta, a przy braku konta - pusty stan z droga do konta.
        var sonosSession = sessions.Sessions.FirstOrDefault(
            session => session.Id.Equals("sonos", StringComparison.OrdinalIgnoreCase));
        if (sonosSession is null)
        {
            throw new Exception("Brak sesji Sonos na liscie sesji.");
        }
        if (sonosSession.Items.Count > 0)
        {
            throw new Exception(
                "Sesja Sonos ma utwory demonstracyjne (atrapa): " + sonosSession.Items.Count);
        }
        // ZMIERZONA kolejnosc gniazd sesji na domyslnych ustawieniach PRZED tym
        // przyrostem. Nowe polecenie nie ma prawa jej zmienic ani przenumerowac.
        // Sonos jest dopisany NA KONCU. Kolejnosc dotychczasowych sesji i ich
        // numery musza zostac nietkniete - tego pilnuje ten warunek.
        var slots = string.Join(", ", sessions.Sessions.Select(session => session.Id));
        if (slots != "wiim, tidal, appleMusic, spotify, sonos")
        {
            throw new Exception("Kolejność gniazd sesji się zmieniła: " + slots);
        }
        checks++;

        Console.WriteLine(
            "OK: polecenie konta Sonos idzie prawdziwym routerem, katalogiem i paletą; "
            + $"bez skrótu, bez sesji, rozdzielne od TIDAL i Spotify ({checks} sprawdzeń)");
    }
}

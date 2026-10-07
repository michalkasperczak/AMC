using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Windows.Threading;
using AccessibleMediaController.Core.Sonos;
using AccessibleMediaController.Windows;

/// <summary>
/// ZGLOSZENIE PO 4.1.9, ZWYKLY AMC (nie okno zarzadzania): rutynowe zapowiedzi
/// postepu Sonosa. Doslownie: "po wejściu za każdym razem w sesji Sonos mówi
/// coś w rodzaju trwa odświeżanie głośników i chyba nawet nie kończy tego
/// komunikatu. I tak samo jak klikamy w stację ... Trwa odczytywanie czy coś
/// takiego ... One są zbędne."
///
/// CO MIERZYMY: czy przez PRODUKCYJNE ujscie mowy (<c>AnnouncementSinkForTests</c>,
/// to samo, ktorego uzywa <c>MainWindow.Announce</c>) przechodzi rutynowa
/// zapowiedz postepu. Mowa to Podglad mowy, nie nazwa kontrolki - dlatego
/// liczymy WYPOWIEDZI, nie tekst kontrolki statusu.
///
/// CO MUSI ZOSTAC (asercje pozytywne w TYM SAMYM pomiarze, zeby poprawka nie
/// byla "globalnym wyciszeniem"): wynik/podsumowanie odczytu oraz PRAWDZIWY
/// BLAD nadal wypowiadane.
///
/// Aparatura: ISTNIEJACY <c>RealHarness</c> z zestawu F3c (prawdziwe
/// <see cref="MainWindow"/>, produkcyjny wlasciciel konta, syntetyczny
/// transport HTTP). Zadnych tokenow, DPAPI, sieci ani prawdziwego Sonosa.
/// </summary>
internal static partial class SonosFavoritePlayRealOwnerTests
{
    /// <summary>Rutynowe zapowiedzi postepu - dokladnie te, ktore zglosil uzytkownik.</summary>
    private static readonly string[] RoutineProgressSpeech =
    [
        "Odświeżam grupy Sonos",
        SonosFavoritesLabels.Loading,
        SonosPlaylistsLabels.Loading,
        SonosTargetSelectionLabels.LoadingGroups,
    ];

    internal static void RunQuietProgress()
    {
        List<Verdict> verdicts = [];
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { verdicts = RunQuietProgressAll(); }
            catch (Exception exception) { failure = exception; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(180)))
        {
            throw new Exception("Limit czasu pomiaru cichego postępu Sonos.");
        }

        if (failure is not null) throw failure;

        foreach (var verdict in verdicts)
        {
            Console.WriteLine(
                (verdict.Passed ? "CICHY-POTWIERDZONY " : "CICHY-ODRZUCONY ")
                + verdict.Case + ": " + verdict.Hypothesis + " -> " + verdict.Detail);
        }

        var rejected = verdicts.Where(verdict => !verdict.Passed).ToArray();
        Console.WriteLine($"OK: ciche rutynowe postępy Sonos ({verdicts.Count} sprawdzeń)");
        if (rejected.Length != 0)
        {
            throw new Exception(
                "Ciche postępy Sonos: ODRZUCONO " + rejected.Length + " z " + verdicts.Count + ": "
                + string.Join(" || ", rejected.Select(verdict => verdict.Case + " " + verdict.Detail)));
        }
    }

    private static List<Verdict> RunQuietProgressAll() =>
    [
        Measure("Q1", "zwykle wejscie/odswiezenie topologii Sonos NIE wypowiada "
            + "rutynowego postepu", MeasureSessionEntryIsQuiet),
        Measure("Q2", "odczyt ulubionych NIE wypowiada postepu, ale WYNIK nadal mowi",
            MeasureFavoritesLoadIsQuietButResultSpeaks),
        Measure("Q3", "PRAWDZIWY BLAD odczytu nadal jest wypowiadany",
            MeasureRealErrorStillSpeaks),
    ];

    /// <summary>
    /// Q1. ZWYKLE WEJSCIE W SESJE SONOS (nie okno zarzadzania): produkcyjna
    /// droga odswiezenia topologii NIE MOZE wypowiedziec "Odświeżam grupy
    /// Sonos".
    /// </summary>
    private static string MeasureSessionEntryIsQuiet()
    {
        using var harness = RealHarness.Create();
        var window = harness.Window;
        harness.Enter();

        List<string> spoken = [];
        window.AnnouncementSinkForTests = message => spoken.Add(message);
        try
        {
            // PRODUKCYJNA droga zwyklego odswiezenia celu sterowania w sesji.
            harness.Pump(window.RefreshSonosTopologyAsync());
            harness.PumpQuietly(TimeSpan.FromMilliseconds(150));
        }
        finally
        {
            window.AnnouncementSinkForTests = null;
        }

        var routine = spoken
            .Where(message => RoutineProgressSpeech.Any(progress =>
                message.Contains(progress, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        if (routine.Length != 0)
        {
            throw new Exception("ZGŁOSZONY BŁĄD ODTWORZONY: zwykłe wejście w sesję Sonos "
                + "wypowiedziało rutynowy postęp: " + string.Join(" | ", routine)
                + " (wszystkie wypowiedzi: " + string.Join(" ; ", spoken) + ").");
        }

        return "zwykłe wejście nie wypowiedziało rutynowego postępu; "
            + $"wypowiedzi łącznie: {spoken.Count}"
            + (spoken.Count == 0 ? "" : " (" + string.Join(" ; ", spoken) + ")");
    }

    /// <summary>
    /// Q2. ODCZYT ULUBIONYCH produkcyjnym poleceniem: "Odczytuję ulubione" ma
    /// zniknac z MOWY, ale PODSUMOWANIE LICZBY ma nadal byc wypowiedziane.
    /// </summary>
    private static string MeasureFavoritesLoadIsQuietButResultSpeaks()
    {
        using var harness = RealHarness.Create();
        var window = harness.Window;
        harness.Enter();

        List<string> spoken = [];
        window.AnnouncementSinkForTests = message => spoken.Add(message);
        try
        {
            // PRODUKCYJNE polecenie uzytkownika - modal tylko podstawiony.
            harness.RunFavoritesModal(_ => { });
            harness.PumpQuietly(TimeSpan.FromMilliseconds(150));
        }
        finally
        {
            window.AnnouncementSinkForTests = null;
        }

        var routine = spoken
            .Where(message => message.Contains(SonosFavoritesLabels.Loading, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (routine.Length != 0)
        {
            throw new Exception("ZGŁOSZONY BŁĄD ODTWORZONY: odczyt ulubionych wypowiedział "
                + "rutynowy postęp: " + string.Join(" | ", routine)
                + " (wszystkie wypowiedzi: " + string.Join(" ; ", spoken) + ").");
        }

        // POPRAWKA NIE JEST WYCISZENIEM: wynik odczytu MUSI zostac powiedziany.
        if (spoken.Count == 0)
        {
            throw new Exception("Odczyt ulubionych zamilkł CAŁKOWICIE - to wyciszenie, "
                + "nie usunięcie zbędnej zapowiedzi. Wynik odczytu musi zostać wypowiedziany.");
        }

        return "odczyt ulubionych bez zapowiedzi postępu, wynik wypowiedziany: "
            + string.Join(" ; ", spoken);
    }

    /// <summary>
    /// Q3. PRAWDZIWY BLAD. Syntetyczna chmura odmawia; komunikat bledu MUSI
    /// przejsc przez mowe. To bezpiecznik przeciw "uciszylismy wszystko".
    /// </summary>
    private static string MeasureRealErrorStillSpeaks()
    {
        using var harness = RealHarness.Create();
        var window = harness.Window;
        harness.Enter();

        // CHMURA ODMAWIA - prawdziwy blad, nie postep.
        harness.Handler.RouteOverride = (request, _) =>
            request.RequestUri!.AbsolutePath.Contains("favorites", StringComparison.OrdinalIgnoreCase)
                ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
                : null;

        List<string> spoken = [];
        window.AnnouncementSinkForTests = message => spoken.Add(message);
        try
        {
            window.PresentSonosFavoritesOverrideForTests = dialog =>
            {
                throw new Exception("Odmowa chmury OTWORZYŁA okno ulubionych: " + dialog.GetType().Name);
            };
            harness.ExecuteCommand(AccessibleMediaController.Core.Commands.CommandIds.ViewFavorites);
            harness.Pump(window.LastSonosFavoritesTaskForTests
                ?? throw new Exception("Polecenie nie rozpoczęło odczytu ulubionych."));
            harness.PumpQuietly(TimeSpan.FromMilliseconds(150));
        }
        finally
        {
            window.AnnouncementSinkForTests = null;
            window.PresentSonosFavoritesOverrideForTests = null;
            harness.Handler.RouteOverride = null;
        }

        var routine = spoken
            .Where(message => message.Contains(SonosFavoritesLabels.Loading, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (routine.Length != 0)
        {
            throw new Exception("Ścieżka błędu nadal wypowiada rutynowy postęp: "
                + string.Join(" | ", routine) + ".");
        }

        if (spoken.Count == 0)
        {
            throw new Exception("PRAWDZIWY BŁĄD ODCZYTU ULUBIONYCH NIE ZOSTAŁ WYPOWIEDZIANY - "
                + "poprawka wyciszyła błędy, a miała usunąć tylko rutynowy postęp.");
        }

        return "prawdziwy błąd nadal wypowiadany: " + string.Join(" ; ", spoken);
    }
}

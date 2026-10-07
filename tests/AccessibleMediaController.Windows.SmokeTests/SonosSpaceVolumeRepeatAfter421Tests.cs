using System.Net.Http;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using AccessibleMediaController.Core.Commands;
using AccessibleMediaController.Core.Sonos;
using AccessibleMediaController.Windows;

/// <summary>
/// ZGLOSZENIE PO 4.2.0/4.2.1, DWIE CZESCI MIERZONE NA PRAWDZIWYM OKNIE.
///
/// CZESC A - SPACJA PO URUCHOMIENIU WLASNEJ STACJI:
///   "w Moich stacjach komunikat po wcisnieciu spacji podczas odtwarzania Sonos
///   nie zglasza mozliwosci zatrzymania tego materialu. Ale potem juz bylo OK."
///   Pomiar laduje w pamieci kopie z chwili BUFOROWANIA (bez canPause/canStop),
///   podstawia chmure, ktora NA SWIEZO zglasza PLAYING + canStop, i naciska
///   PRODUKCYJNA Spacje. Zgloszony blad = kategoryczna odmowa i ZERO POST.
///
/// CZESC B - SZYBKIE POWTORZENIA CTRL+WIN+DOL Z WTYCZKI NVDA:
///   "jak chce sciszyc Ctrl+Win+w dol - Poprzednie polecenie Sonos jeszcze sie
///   nie zakonczylo." Pomiar WSTRZYMUJE pierwszy POST glosnosci i wysyla seria
///   kolejnych intencji PRODUKCYJNA droga wtyczki. Zgloszony blad = nacisniecia
///   przepadaja (koncowa glosnosc nie odpowiada liczbie krokow).
///
/// CZEGO NIE DOWODZI: niczego o prawdziwym glosniku Michala. Konto i transport
/// sa syntetyczne; mowa NVDA idzie osobno na Windows.
/// </summary>
internal static partial class SonosFavoritePlayRealOwnerTests
{
    internal static void RunSpaceAndVolumeRepeatAfter421()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { MeasureSpaceAndVolumeRepeat(); }
            catch (Exception e) { failure = e; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(420)))
        {
            throw new Exception("Limit pomiaru Spacji i powtórzeń głośności");
        }

        if (failure is not null) throw failure;
        Console.WriteLine("OK: Spacja z aktualnego stanu i szybkie powtórzenia głośności (Sonos)");
    }

    private static void MeasureSpaceAndVolumeRepeat()
    {
        MeasureSpaceUsesFreshStateNotStaleBufferingCopy();
        MeasureSpaceStillRefusesWhenFreshStateReallyForbids();
        MeasureVolumeRepeatsAreNotDropped();
        MeasureVolumeRepeatsStopAtZeroWithoutExtraPosts();
        MeasureFailedFirstVolumePostStopsPendingGestures();
        MeasureDirectionReversalAtZeroIsNotLost();
        MeasureGroupChangeDoesNotCarryPendingGesture();
    }

    /// <summary>Odczyt stanu PRODUKCYJNA droga, z podana odpowiedzia chmury.</summary>
    private static void ReadGroupStateWith(RealHarness harness, string playbackJson)
    {
        harness.Handler.RouteOverride = (request, _) =>
            request.Method == HttpMethod.Get
            && request.RequestUri!.AbsolutePath.EndsWith("/playback", StringComparison.Ordinal)
                ? Json(playbackJson)
                : null;
        var read = typeof(MainWindow).GetMethod("ReadSonosGroupStateAsync", Instance)
            ?? throw new Exception("Nie ma prawdziwej metody ReadSonosGroupStateAsync.");
        harness.Pump((Task)read.Invoke(harness.Window, null)!);
    }

    private const string BufferingNoActions =
        "{\"playbackState\":\"PLAYBACK_STATE_BUFFERING\",\"itemId\":\"POZYCJA-1\","
        + "\"positionMillis\":0,\"availablePlaybackActions\":{\"canPause\":false,"
        + "\"canStop\":false,\"canSkip\":false,\"canSkipBack\":false,\"canSeek\":false}}";

    private const string PlayingCanStop =
        "{\"playbackState\":\"PLAYBACK_STATE_PLAYING\",\"itemId\":\"POZYCJA-1\","
        + "\"positionMillis\":9000,\"availablePlaybackActions\":{\"canPause\":false,"
        + "\"canStop\":true,\"canSkip\":false,\"canSkipBack\":false,\"canSeek\":false}}";

    /// <summary>
    /// CZESC A - ZGLOSZONY BLAD. Pamiec z chwili buforowania mowi "nie wolno",
    /// chmura NA SWIEZO mowi PLAYING + canStop. Spacja ma wyslac polecenie, a
    /// nie orzekac kategorycznie z nieaktualnej kopii.
    /// </summary>
    private static void MeasureSpaceUsesFreshStateNotStaleBufferingCopy()
    {
        using var harness = RealHarness.Create();
        var window = harness.Window;
        harness.EnterSonosSessionForMeasurement();

        // STAN W PAMIECI = dokladnie to, co lapie odczyt zaraz po uruchomieniu
        // wlasnej stacji: jeszcze BUFORUJE i nie zglasza zadnego uprawnienia.
        ReadGroupStateWith(harness, BufferingNoActions);
        if (window.SonosPlaybackForTests?.AvailablePlaybackActions is not { CanStop: false, CanPause: false })
        {
            throw new Exception("Aparatura nie załadowała kopii z chwili buforowania - "
                + "pomiar nie dotyczyłby zgłoszenia.");
        }

        // CHMURA TERAZ: material GRA i WOLNO go zatrzymac (radio: canPause=false,
        // canStop=true - tak opisuje to kontrakt Sonos playbackStatus). Plan
        // obejmuje TAKZE sam POST transportu: bez zaplanowanej odpowiedzi
        // aparatura przerywa pomiar, zanim zdazy ocenic zachowanie Spacji.
        harness.Handler.RouteOverride = (request, _) =>
        {
            if (request.Method == HttpMethod.Get
                && request.RequestUri!.AbsolutePath.EndsWith("/playback", StringComparison.Ordinal))
            {
                return Json(PlayingCanStop);
            }

            if (request.Method == HttpMethod.Post
                && request.RequestUri!.AbsolutePath.Contains("playback", StringComparison.Ordinal))
            {
                return Json("{}");
            }

            return null;
        };

        var announcedBefore = harness.Announcements.Count;
        var postsBefore = harness.Handler.Posts.Count;
        var wireBefore = harness.Handler.Requests.Count;

        harness.ExecuteSonosCommandForMeasurement(CommandIds.PlayPause);

        var said = string.Join(" | ", harness.Announcements.Skip(announcedBefore));
        var posts = harness.Handler.Posts.Skip(postsBefore).ToList();

        if (said.Contains("nie zgłasza możliwości zatrzymania", StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception("ZGŁOSZONY BŁĄD ODTWORZONY: Spacja orzekła kategorycznie "
                + "\"Sonos nie zgłasza możliwości zatrzymania tego materiału\" z nieaktualnej "
                + "kopii, choć świeży odczyt zgłasza PLAYING i canStop. Komunikat: " + said);
        }

        if (posts.Count != 1)
        {
            throw new Exception($"Spacja miała wysłać DOKŁADNIE JEDNO polecenie transportu, "
                + $"wysłała {posts.Count}. Komunikat: " + said);
        }

        if (!posts[0].Uri.AbsolutePath.Contains("playback", StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception("Spacja wysłała POST pod inny adres niż transport: "
                + posts[0].Uri.AbsolutePath);
        }

        // SWIEZY ODCZYT MUSI POPRZEDZAC POST - inaczej decyzja znow bylaby z kopii.
        // WAZNE: liczymy tylko ruch OD TEGO ZGLOSZENIA (wire od wireBefore), bo
        // GET-y z przygotowania aparatury maskowalyby brak swiezego odczytu.
        // List.FindLastIndex(startIndex, count, ...) szuka W TYL OD startIndex,
        // wiec wariant (0, postIndex, ...) ogladal wylacznie indeks 0 i rzucal
        // ArgumentOutOfRangeException dla postIndex > 1 - stad kierunek jest tu
        // zapisany jawnie, zakresem.
        var wire = harness.Handler.Requests.Skip(wireBefore).ToList();
        var postIndex = wire.FindIndex(w => w.Method == "POST");
        if (postIndex < 0)
        {
            throw new Exception("Aparatura nie zobaczyła POST-u Spacji w ruchu sieciowym.");
        }

        var freshGet = -1;
        for (var i = postIndex - 1; i >= 0; i--)
        {
            if (wire[i].Method == "GET"
                && wire[i].Uri.AbsolutePath.EndsWith("/playback", StringComparison.Ordinal))
            {
                freshGet = i;
                break;
            }
        }

        if (freshGet < 0)
        {
            throw new Exception("Spacja wysłała polecenie BEZ świeżego odczytu stanu przed nim.");
        }
    }

    /// <summary>
    /// GRANICA: gdy SWIEZY odczyt naprawde zabrania (material bez canPause i bez
    /// canStop, stan ustalony), odmowa ZOSTAJE i NIE leci zaden slepy POST.
    /// Usuniecie bramki zdalo by poprzedni pomiar i zepsulo ten.
    /// </summary>
    private static void MeasureSpaceStillRefusesWhenFreshStateReallyForbids()
    {
        using var harness = RealHarness.Create();
        var window = harness.Window;
        harness.EnterSonosSessionForMeasurement();

        const string idleNoActions =
            "{\"playbackState\":\"PLAYBACK_STATE_IDLE\",\"itemId\":null,\"positionMillis\":0,"
            + "\"availablePlaybackActions\":{\"canPause\":false,\"canStop\":false,"
            + "\"canSkip\":false,\"canSkipBack\":false,\"canSeek\":false}}";
        ReadGroupStateWith(harness, idleNoActions);

        var announcedBefore = harness.Announcements.Count;
        var postsBefore = harness.Handler.Posts.Count;
        harness.ExecuteSonosCommandForMeasurement(CommandIds.PlayPause);

        var said = string.Join(" | ", harness.Announcements.Skip(announcedBefore));
        var posts = harness.Handler.Posts.Skip(postsBefore).ToList();
        if (posts.Count != 0)
        {
            throw new Exception($"Spacja wysłała {posts.Count} POST, mimo że świeży stan "
                + "naprawdę nie pozwala na zatrzymanie. Komunikat: " + said);
        }

        if (string.IsNullOrWhiteSpace(said))
        {
            throw new Exception("Odmowa przeszła bez żadnego komunikatu - użytkownik nie wie, "
                + "co się stało.");
        }
    }

    /// <summary>
    /// PLAN TRASY GLOSNOSCI. Domyslna odpowiedz aparatury nie zna POST-u
    /// /groupVolume, wiec pomiar przerywal sie na pierwszym zapisie, zanim
    /// zdazyl ocenic kumulacje. GET oddaje poziom, ktory NAPRAWDE zostal
    /// zapisany - dzieki temu werdykt liczy sie z toru, a nie z zyczenia.
    /// </summary>
    private static void PlanVolumeRoute(RealHarness harness, int fallbackVolume)
    {
        // POZIOM ODCZYTU bierzemy z OSTATNIEGO ZAPISU na druciku, a gdy zadnego
        // jeszcze nie bylo - z wartosci wyjsciowej. LastVolumePostedForMeasurement
        // RZUCA bez zapisu, wiec probujemy go ostroznie: odczyt w aparaturze nie
        // moze wywrocic pomiaru przed pierwszym POST-em.
        int Current()
        {
            try { return harness.LastVolumePostedForMeasurement(); }
            catch (Exception) { return fallbackVolume; }
        }

        harness.Handler.RouteOverride = (request, body) =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Get
                && path.EndsWith("/playback", StringComparison.Ordinal))
            {
                return Json(PlayingCanStop);
            }

            if (request.Method == HttpMethod.Get
                && path.Contains("/groupVolume", StringComparison.Ordinal))
            {
                return Json("{\"volume\":" + Current().ToString(
                    System.Globalization.CultureInfo.InvariantCulture)
                    + ",\"muted\":false,\"fixed\":false}");
            }

            if (request.Method == HttpMethod.Post
                && path.Contains("/groupVolume", StringComparison.Ordinal))
            {
                return Json("{}");
            }

            return null;
        };
    }

    /// <summary>
    /// CZESC B - ZGLOSZONY BLAD. Pierwszy POST glosnosci WISI, a wtyczka NVDA
    /// dostarcza kolejne intencje. Zadna nie ma przepasc.
    /// </summary>
    private static void MeasureVolumeRepeatsAreNotDropped()
    {
        using var harness = RealHarness.Create();
        var window = harness.Window;
        harness.EnterSonosSessionForMeasurement();
        ReadGroupStateWith(harness, PlayingCanStop);
        // PLAN TRASY PRZED primingiem: priming NAKLADA swoj GET na biezacy
        // override i oddaje sterowanie dalej, wiec kolejnosc ma znaczenie.
        PlanVolumeRoute(harness, 50);
        harness.PrimeSonosVolumeForMeasurement(50);

        var step = harness.SonosVolumeStepForMeasurement();
        // ZNACZNIK OD TEGO ZGLOSZENIA: wejscie do sesji i priming same robia
        // pelne odczyty grupy. Liczenie ich jako "dolozonych przez powtorzenia"
        // mierzyloby przygotowanie, nie regulacje.
        var requestsBefore = harness.Handler.Requests.Count;
        var held = harness.Handler.HoldNextPost();

        // PRODUKCYJNA DROGA WTYCZKI: ten sam tor, ktorym leci nvda-bridge.
        harness.DispatchNvdaCommandForMeasurement("volumeDown");
        try
        {
            harness.PumpUntil(() => held.Arrived, TimeSpan.FromSeconds(10),
                "pierwszy POST głośności nie dotarł do transportu");
        }
        catch (Exception exception)
        {
            // DIAGNOSTYKA APARATURY: bez tresci wypowiedzi i ruchu sieciowego nie
            // da sie odroznic odmowy bramki od braku zaplanowanej trasy.
            throw new Exception(exception.Message
                + " | odpowiedź mostka: " + (harness.LastNvdaReplyForMeasurement ?? "(brak)")
                + " | mowa: " + string.Join(" / ", harness.Announcements)
                + " | ruch: " + string.Join(", ", harness.Handler.Requests
                    .Select(w => w.Method + " " + w.Uri.AbsolutePath)),
                exception);
        }

        const int repeats = 5;
        for (var i = 0; i < repeats; i++)
        {
            harness.DispatchNvdaCommandForMeasurement("volumeDown");
        }

        held.Release();
        harness.PumpQuietly(TimeSpan.FromMilliseconds(600));
        harness.PumpUntil(() => !harness.Window.SonosCommandInFlightForTests,
            TimeSpan.FromSeconds(15), "polecenia głośności nie domknęły się");

        var expected = Math.Max(0, 50 - ((repeats + 1) * step));
        var sent = harness.Handler.Posts
            .Where(w => w.Uri.AbsolutePath.Contains("volume", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var reached = harness.LastVolumePostedForMeasurement();

        if (reached != expected)
        {
            throw new Exception($"ZGŁOSZONY BŁĄD ODTWORZONY: {repeats + 1} naciśnięć Ctrl+Win+dół "
                + $"po {step} miało zejść z 50 na {expected}, a Sonos dostał {reached}. "
                + $"POST-ów głośności: {sent.Count}.");
        }

        // SERIALIZACJA i BRAK KOLEJEK NIEOGRANICZONYCH: skumulowana intencja ma
        // domknac sie JEDNYM dodatkowym zapisem, nie szescioma.
        if (sent.Count > 2)
        {
            throw new Exception($"Skumulowana intencja wysłała {sent.Count} zapisów głośności - "
                + "to kolejka, nie kumulacja.");
        }

        // BEZ MNOZENIA ODCZYTOW: powtorzenia nie dokladaja GET na kazdy krok.
        // Dozwolony jest JEDEN odczyt potwierdzajacy koncowy poziom.
        var volumeGets = harness.Handler.Requests.Skip(requestsBefore).Count(w =>
            w.Method == "GET" && w.Uri.AbsolutePath.EndsWith("/groupVolume", StringComparison.Ordinal));
        if (volumeGets > 1)
        {
            throw new Exception($"Powtórzenia dołożyły {volumeGets} odczytów głośności - "
                + "szybkość regulacji by spadła.");
        }
    }

    /// <summary>
    /// GRANICA 0: seria w dol przy niskiej glosnosci zatrzymuje sie na zerze i
    /// NIE wysyla zapisow bez zmiany.
    /// </summary>
    private static void MeasureVolumeRepeatsStopAtZeroWithoutExtraPosts()
    {
        using var harness = RealHarness.Create();
        harness.EnterSonosSessionForMeasurement();
        ReadGroupStateWith(harness, PlayingCanStop);
        var step = harness.SonosVolumeStepForMeasurement();
        PlanVolumeRoute(harness, step);
        harness.PrimeSonosVolumeForMeasurement(step);

        var held = harness.Handler.HoldNextPost();
        harness.DispatchNvdaCommandForMeasurement("volumeDown");
        harness.PumpUntil(() => held.Arrived, TimeSpan.FromSeconds(10),
            "POST głośności nie dotarł do transportu");
        for (var i = 0; i < 4; i++)
        {
            harness.DispatchNvdaCommandForMeasurement("volumeDown");
        }

        held.Release();
        harness.PumpQuietly(TimeSpan.FromMilliseconds(600));
        harness.PumpUntil(() => !harness.Window.SonosCommandInFlightForTests,
            TimeSpan.FromSeconds(15), "polecenia głośności nie domknęły się");

        if (harness.LastVolumePostedForMeasurement() != 0)
        {
            throw new Exception("Seria w dół nie zeszła do zera: "
                + harness.LastVolumePostedForMeasurement());
        }

        var sent = harness.Handler.Posts
            .Count(w => w.Uri.AbsolutePath.Contains("volume", StringComparison.OrdinalIgnoreCase));
        if (sent > 2)
        {
            throw new Exception($"Na granicy zera poszło {sent} zapisów głośności.");
        }
    }

    /// <summary>
    /// L1 Z ODBIORU: PIERWSZY zapis glosnosci PADA, a w tym czasie leca kolejne
    /// gesty. Zgloszony blad = petla dosylek startowala BEZ sprawdzenia wyniku
    /// pierwszego POST-u, wiec po bledzie leciały nastepne zapisy, a pozniejszy
    /// sukces PRZYKRYWAL blad w wypowiedzi.
    /// </summary>
    private static void MeasureFailedFirstVolumePostStopsPendingGestures()
    {
        using var harness = RealHarness.Create();
        harness.EnterSonosSessionForMeasurement();
        ReadGroupStateWith(harness, PlayingCanStop);
        harness.PrimeSonosVolumeForMeasurement(50);

        // PIERWSZY zapis glosnosci dostaje 500; GET-y nadal odpowiadaja.
        harness.Handler.RouteOverride = (request, _) =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Post
                && path.EndsWith("/groupVolume", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("{}"),
                };
            }

            if (request.Method == HttpMethod.Get
                && path.EndsWith("/groupVolume", StringComparison.Ordinal))
            {
                return Json("{\"volume\":50,\"muted\":false,\"fixed\":false}");
            }

            return request.Method == HttpMethod.Get
                && path.EndsWith("/playback", StringComparison.Ordinal)
                    ? Json(PlayingCanStop)
                    : null;
        };

        var announcedBefore = harness.Announcements.Count;
        var held = harness.Handler.HoldNextPost();
        harness.DispatchNvdaCommandForMeasurement("volumeDown");
        harness.PumpUntil(() => held.Arrived, TimeSpan.FromSeconds(10),
            "pierwszy (padający) POST głośności nie dotarł do transportu");

        // GESTY W TRAKCIE: nie mają prawa ruszyć po błędzie pierwszego zapisu.
        for (var i = 0; i < 3; i++)
        {
            harness.DispatchNvdaCommandForMeasurement("volumeDown");
        }

        held.Release();
        harness.PumpQuietly(TimeSpan.FromMilliseconds(600));
        harness.PumpUntil(() => !harness.Window.SonosCommandInFlightForTests,
            TimeSpan.FromSeconds(15), "polecenia głośności nie domknęły się");

        var volumePosts = harness.Handler.Posts
            .Count(w => w.Uri.AbsolutePath.EndsWith("/groupVolume", StringComparison.Ordinal));
        if (volumePosts != 1)
        {
            throw new Exception($"Po NIEUDANYM pierwszym zapisie poszło {volumePosts} "
                + "zapisów głośności - błąd został przykryty dosyłkami.");
        }

        // BLAD MUSI BYC NAZWANY: cisza albo \"ustawiono\" byloby klamstwem.
        var said = harness.Announcements.Skip(announcedBefore).ToList();
        if (said.Count == 0)
        {
            throw new Exception("Nieudany zapis głośności nic nie powiedział.");
        }
    }

    /// <summary>
    /// L3 Z ODBIORU: po dojsciu do 0 nastepuje ZMIANA KIERUNKU. Suma delt z
    /// clamp gubila taki obrot (-5 i +5 znosily sie do zera), wiec \"w gore\" po
    /// serii \"w dol\" nie podnosilo glosnosci.
    /// </summary>
    private static void MeasureDirectionReversalAtZeroIsNotLost()
    {
        using var harness = RealHarness.Create();
        harness.EnterSonosSessionForMeasurement();
        ReadGroupStateWith(harness, PlayingCanStop);
        var step = harness.SonosVolumeStepForMeasurement();
        PlanVolumeRoute(harness, step);
        harness.PrimeSonosVolumeForMeasurement(step);

        var held = harness.Handler.HoldNextPost();
        harness.DispatchNvdaCommandForMeasurement("volumeDown");
        harness.PumpUntil(() => held.Arrived, TimeSpan.FromSeconds(10),
            "POST głośności nie dotarł do transportu");

        // Jeszcze w dol (juz na zerze), a potem ZMIANA KIERUNKU w gore.
        harness.DispatchNvdaCommandForMeasurement("volumeDown");
        harness.DispatchNvdaCommandForMeasurement("volumeUp");

        held.Release();
        harness.PumpQuietly(TimeSpan.FromMilliseconds(600));
        harness.PumpUntil(() => !harness.Window.SonosCommandInFlightForTests,
            TimeSpan.FromSeconds(15), "polecenia głośności nie domknęły się");

        var reached = harness.LastVolumePostedForMeasurement();
        if (reached != step)
        {
            throw new Exception($"Po zejściu do 0 krok w górę miał dać {step}, "
                + $"a Sonos dostał {reached} - zmiana kierunku na brzegu przepadła.");
        }
    }

    /// <summary>
    /// L4 Z ODBIORU: zalegly gest NIE MOZE trafic do NOWEJ grupy. Inaczej
    /// sciszanie salonu konczyloby sie sciszeniem kuchni.
    /// </summary>
    private static void MeasureGroupChangeDoesNotCarryPendingGesture()
    {
        using var harness = RealHarness.Create();
        harness.EnterSonosSessionForMeasurement();
        ReadGroupStateWith(harness, PlayingCanStop);
        PlanVolumeRoute(harness, 50);
        harness.PrimeSonosVolumeForMeasurement(50);

        var firstGroup = harness.Window.SonosActiveGroup?.Id
            ?? throw new Exception("Brak aktywnej grupy w pomiarze.");

        var held = harness.Handler.HoldNextPost();
        harness.DispatchNvdaCommandForMeasurement("volumeDown");
        harness.PumpUntil(() => held.Arrived, TimeSpan.FromSeconds(10),
            "POST głośności nie dotarł do transportu");

        // Gesty w trakcie bramki, a potem ZMIANA CELU na druga grupe - tak jak
        // uzytkownik przelaczajacy pokoj w czasie trwajacego polecenia.
        harness.DispatchNvdaCommandForMeasurement("volumeDown");
        harness.DispatchNvdaCommandForMeasurement("volumeDown");
        harness.SelectOtherSonosGroupForMeasurement(firstGroup);

        held.Release();
        harness.PumpQuietly(TimeSpan.FromMilliseconds(600));
        harness.PumpUntil(() => !harness.Window.SonosCommandInFlightForTests,
            TimeSpan.FromSeconds(15), "polecenia głośności nie domknęły się");

        var toOther = harness.Handler.Posts.Count(w =>
            w.Uri.AbsolutePath.EndsWith("/groupVolume", StringComparison.Ordinal)
            && !w.Uri.AbsolutePath.Contains(firstGroup, StringComparison.Ordinal));
        if (toOther != 0)
        {
            throw new Exception($"Zaległy gest głośności poszedł do NOWEJ grupy "
                + $"({toOther} zapisów) - cel został podmieniony pod ręką.");
        }
    }
}

using System.Text.Json.Nodes;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Core.Spotify;

/// <summary>
/// Regresje z ODBIORU KODU cyklu zycia sesji "Spotify — Librespot". Kazdy test
/// tutaj odpowiada jednej znalezionej usterce i musi UPADAC na wersji przed
/// poprawka - inaczej nie dowodzi niczego.
///
/// Wspolna atrape hosta i scene bierzemy z
/// <see cref="SpotifyLibrespotLifecycleTests"/> (ta sama klasa czesciowa), zeby
/// nie powstala druga, rozjezdzajaca sie atrapa protokolu.
///
/// Konta tu nie ma: token jest jawnie fikcyjny. Ten zestaw NIE dowodzi
/// logowania do Spotify ani odsluchu.
/// </summary>
internal static partial class SpotifyLibrespotLifecycleTests
{
    /// <summary>
    /// Uruchamia WSZYSTKIE przypadki i dopiero na koncu zglasza porazke, zeby
    /// jeden przebieg pokazywal pelna liste regresji, a nie tylko pierwsza.
    /// </summary>
    internal static void RunReviewRegressions()
    {
        var bledy = new List<string>();
        Task.Run(async () =>
        {
            await Zmierz(bledy, "P1-1 limit przygotowania nie dopuszcza pozniejszego dzwieku",
                LimitPrzygotowaniaOdcinaSpoznionyDzwiek);
            await Zmierz(bledy, "P1-2 wyciszenie w trakcie logowania wchodzi do hosta",
                WyciszenieWTrakcieLogowaniaDochodzi);
            await Zmierz(bledy, "P2-6 przewiniecie w trakcie logowania wchodzi do play",
                PrzewiniecieWTrakcieLogowaniaDochodzi);
            await Zmierz(bledy, "P1-3 awaria dostawcy tokenu jest powiedziana",
                AwariaDostawcyTokenuJestPowiedziana);
            await Zmierz(bledy, "P1-4 pad hosta bez utworu jest powiedziany",
                PadHostaBezUtworuJestPowiedziany);
            await Zmierz(bledy, "P2-5 stary host dostaje shutdown przy zmianie wyjscia",
                ZmianaWyjsciaZamykaStaryHostPoDobremu);
        }).GetAwaiter().GetResult();

        if (bledy.Count > 0)
        {
            throw new InvalidOperationException(
                "Regresje odbioru kodu Librespot:" + Environment.NewLine
                + string.Join(Environment.NewLine, bledy));
        }
        Console.WriteLine(
            "OK: regresje odbioru Librespot (limit przygotowania odcina dźwięk, wyciszenie"
            + " i przewinięcie z czasu logowania dochodzą, awaria poświadczeń i pad hosta bez"
            + " utworu są powiedziane, stary host dostaje shutdown)");
    }

    private static async Task Zmierz(List<string> bledy, string nazwa, Func<Task> test)
    {
        try
        {
            await test();
        }
        catch (Exception exception)
        {
            bledy.Add($"- {nazwa}: {exception.Message}");
        }
    }

    /// <summary>
    /// P1-1. Po zgloszonym przekroczeniu limitu przygotowania host NIE MOZE
    /// pozniej zaczac grac. Wczesniej straz tylko mowila o bledzie, a spozniony
    /// stan isPlaying wznawial dzwiek po komunikacie o awarii - dla uzytkownika
    /// niewidomego to dzwiek, ktorego nikt nie zamawial.
    /// </summary>
    private static async Task LimitPrzygotowaniaOdcinaSpoznionyDzwiek()
    {
        using var scena = Scena.Nowa(preparationTimeout: TimeSpan.FromMilliseconds(250));
        scena.Output.Play(Utwor("A"), TimeSpan.Zero, 70, 1d);
        await scena.Output.PendingPlaybackStart.WaitAsync(TimeSpan.FromSeconds(5));
        var host = scena.Hosts.Single();
        Check(host.Commands("play").Count == 1, "Host musi przyjąć play");

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (scena.Failed.Count == 0 && DateTime.UtcNow < deadline) await Task.Delay(20);
        Check(scena.Failed.Count == 1, "Przekroczony limit przygotowania musi być powiedziany");

        // Host budzi sie PO limicie i melduje, ze gra.
        host.EmitState(host.LastPlayId, "spotify:track:A", 1_000, 200_000, isPlaying: true);
        await scena.Settle(host);
        await Task.Delay(120);

        Check(scena.Started.Count == 0,
            "Po zgłoszonym limicie przygotowania NIE WOLNO zgłosić startu spóźnionego dźwięku");
        Check(host.Names().Contains("stop"),
            "Limit przygotowania musi ZATRZYMAĆ hosta, żeby dźwięk nie włączył się po awarii");
        Check(!scena.Output.IsPreparing, "Przygotowanie musi być zakończone");
    }

    /// <summary>
    /// P1-2. Wyciszenie w czasie logowania nie moze zginac: pierwszy Play wysyla
    /// glosnosc po zalogowaniu, wiec musi wziac OSTATNIA wartosc, a nie
    /// zamrozona z chwili wywolania Play.
    /// </summary>
    private static async Task WyciszenieWTrakcieLogowaniaDochodzi()
    {
        using var scena = Scena.Nowa(withholdInitialize: true);
        scena.Output.Play(Utwor("A"), TimeSpan.Zero, 100, 1d);
        var czeka = scena.Output.PendingPlaybackStart;
        var host = await scena.WaitForHost();
        var initialize = await host.AwaitWithheld("initialize");

        scena.Output.SetVolume(0);
        host.Ack(initialize);
        await czeka.WaitAsync(TimeSpan.FromSeconds(5));
        await scena.Settle(host);

        var volume = host.Commands("volume");
        Check(volume[^1]["volume"]!.GetValue<int>() == 0,
            $"Wyciszenie z czasu logowania musi dojść do hosta; ostatnia głośność: "
            + $"{volume[^1]["volume"]!.GetValue<int>()}");
    }

    /// <summary>
    /// P2-6. Przewiniecie w czasie logowania nie moze zginac: play musi pojsc z
    /// pozycja z chwili wyslania polecenia, nie z chwili wywolania Play.
    /// </summary>
    private static async Task PrzewiniecieWTrakcieLogowaniaDochodzi()
    {
        using var scena = Scena.Nowa(withholdInitialize: true);
        scena.Output.Play(Utwor("A"), TimeSpan.Zero, 70, 1d);
        var czeka = scena.Output.PendingPlaybackStart;
        var host = await scena.WaitForHost();
        var initialize = await host.AwaitWithheld("initialize");

        scena.Output.Seek(TimeSpan.FromSeconds(90));
        host.Ack(initialize);
        await czeka.WaitAsync(TimeSpan.FromSeconds(5));

        var play = host.Commands("play").Single();
        Check(Liczba(play, "positionMs") == 90_000,
            $"Przewinięcie z czasu logowania musi dojść do play; wysłano "
            + $"{Liczba(play, "positionMs")} ms");
    }

    /// <summary>
    /// P1-3. Dostawca tokenu siega po siec i moze rzucic czymkolwiek. Cisza jest
    /// tu najgorsza: uzytkownik nacisnal odtwarzanie i musi uslyszec powod.
    /// </summary>
    private static async Task AwariaDostawcyTokenuJestPowiedziana()
    {
        using var scena = Scena.Nowa(credentials: _ =>
            throw new HttpRequestException("konto niedostępne"));
        scena.Output.Play(Utwor("A"), TimeSpan.Zero, 70, 1d);
        try
        {
            await scena.Output.PendingPlaybackStart.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (HttpRequestException)
        {
            throw new InvalidOperationException(
                "Awaria dostawcy poświadczeń nie może wychodzić wyjątkiem z przygotowania");
        }

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (scena.Failed.Count == 0 && DateTime.UtcNow < deadline) await Task.Delay(20);
        Check(scena.Failed.Count >= 1,
            "Awaria dostawcy poświadczeń MUSI być powiedziana, nie przemilczana");
        Check(!scena.Output.IsPreparing,
            "Po awarii poświadczeń przygotowanie nie może trwać dalej");
    }

    /// <summary>
    /// P1-4. Pad hosta PRZED jakimkolwiek utworem (np. po samej liście wyjść)
    /// tez musi byc powiedziany. Wcześniej komunikat szedł ze STARĄ generacją,
    /// a straż generacji w wywołaniu zwrotnym odrzucała go zawsze.
    /// </summary>
    private static async Task PadHostaBezUtworuJestPowiedziany()
    {
        using var scena = Scena.Nowa();
        var urzadzenia = await scena.Output.GetOutputDevicesAsync();
        Check(urzadzenia.Count == 2, "Lista wyjść musi dojść bez konta");
        var host = scena.Hosts.Single();

        host.Crash();
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (scena.Failed.Count == 0 && DateTime.UtcNow < deadline) await Task.Delay(20);

        Check(scena.Failed.Count == 1,
            "Pad hosta bez wczytanego utworu musi być powiedziany DOKŁADNIE raz");
        Check(scena.Failed[0].Item is null, "Komunikat o padzie hosta nie ma utworu");
        Check(scena.Failed[0].Message.Contains("Librespot"),
            $"Komunikat musi nazwać składnik; otrzymano: {scena.Blad}");
    }

    /// <summary>
    /// P2-5. Zmiana wyjscia konczy stary proces - ale po dobremu, poleceniem
    /// "shutdown". Ubity host nie oddaje urzadzenia audio, a nowe wyjscie ma je
    /// dostac wolne. Kolejnosc (stary przed nowym) pozostaje obowiazkowa.
    /// </summary>
    private static async Task ZmianaWyjsciaZamykaStaryHostPoDobremu()
    {
        using var scena = Scena.Nowa();
        scena.Output.Play(Utwor("A"), TimeSpan.Zero, 70, 1d);
        await scena.Output.PendingPlaybackStart.WaitAsync(TimeSpan.FromSeconds(5));
        var pierwszy = scena.Hosts.Single();
        pierwszy.EmitState(pierwszy.LastPlayId, "spotify:track:A", 15_000, 200_000, isPlaying: true);
        await scena.Settle(pierwszy);

        Check(await scena.Output.TrySetOutputDeviceAsync("Karta USB (2- Audio)"),
            $"Zmiana wyjścia musi się udać: {scena.Blad}");

        Check(pierwszy.Names().Contains("shutdown"),
            "STARY host musi dostać shutdown, nie sam Kill - inaczej nie oddaje wyjścia dźwięku");
        Check(pierwszy.WyszedlSam, "STARY host musi wyjść po swojemu, nie przez ubicie");
        Check(scena.Hosts.Count == 2, "Zmiana wyjścia w czasie grania stawia NOWY proces");
        Check(pierwszy.KilledAt < scena.Hosts[1].StartedAt,
            "Stary proces musi zniknąć PRZED startem nowego");
    }
}

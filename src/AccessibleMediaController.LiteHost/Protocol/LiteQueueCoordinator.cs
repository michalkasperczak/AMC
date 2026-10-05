using System.Text.Json;
using AccessibleMediaController.Core.Playback;
using AccessibleMediaController.Core.Sessions;

namespace AccessibleMediaController.LiteHost.Protocol;

/// <summary>
/// ZYWA kolejka odtwarzania hosta.
///
/// Zasada: kolejnosci NIE liczymy tutaj. Calym silnikiem kolejki jest
/// <see cref="DemoMediaSession"/> z Core -- ten sam obiekt, ktorego uzywa pelne
/// AMC. Koordynator robi trzy rzeczy i nic wiecej:
/// <list type="number">
/// <item>tlumaczy wiersze protokolu na <see cref="MediaItem"/> i oddaje je sesji,</item>
/// <item>woła kontraktowe metody sesji (<c>Play</c>, <c>PlayRelative</c>,
/// <c>ContinueAfterPlaybackEnded</c>, <c>TogglePlayback</c>),</item>
/// <item>odrzuca SPOZNIONE zdarzenia konca utworu, zeby kolejka nie przeskoczyla
/// dwoch pozycji.</item>
/// </list>
///
/// Czego tu NIE ma, swiadomie: zadnego zapisu do danych uzytkownika. Kolejka
/// zyje w pamieci TEGO procesu hosta. Trwalosc (kto i gdzie zapisuje wspolny
/// profil) zostaje osobnym etapem -- wlascicielem zapisu ma pozostac jeden
/// pisarz C#, a nie ta klasa.
/// </summary>
internal sealed class LiteQueueCoordinator
{
    /// <summary>Jeden wiersz kolejki w postaci oddawanej frontendowi.</summary>
    internal sealed record QueueRow(
        string Id,
        string Title,
        string? Path,
        bool IsPlayNext,
        bool IsCurrent);

    /// <summary>Stan kolejki oddawany na <c>queue.status</c>.</summary>
    internal sealed record QueueStatus(
        IReadOnlyList<QueueRow> Rows,
        string? CurrentId,
        string? CurrentTitle,
        bool Playing,
        bool Paused,
        double PositionSeconds,
        /// <summary>
        /// Czy kolejka ZOSTALA KIEDYKOLWIEK WCZYTANA w tym procesie hosta.
        /// Pusta lista wierszy ma DWA rozne znaczenia i frontend musi je
        /// rozroznic: "nigdy nie wczytano" (wolno przywrocic zapisany porzadek)
        /// kontra "wczytano i zuzyto" (NIE WOLNO, bo skonsumowane utwory
        /// wrocilyby do kolejki). Odmowa <c>queue.set</c> nie wczytuje niczego.
        /// </summary>
        bool Initialized);

    private readonly IMediaOutput _output;
    private readonly object _gate = new();

    private DemoMediaSession? _session;

    /// <summary>
    /// Czy kolejka PROWADZI teraz transport. To osobny stan od "ma material":
    /// po wyjsciu poza kolejke (bezposrednie <c>files.play</c>, radio) wiersze
    /// zostaja, ale transport nalezy do kogos innego. Samo Id biezacej pozycji
    /// NIE wystarcza do rozstrzygniecia, bo uzytkownik moze zagrac wprost TEN
    /// SAM plik, ktory stoi w kolejce.
    /// </summary>
    private bool _leading;

    /// <summary>
    /// Czy kolejka byla wczytana. Raz ustawione nie wraca do <c>false</c>:
    /// zuzycie wszystkich utworow nie jest "brakiem wczytania".
    /// </summary>
    private bool _initialized;

    /// <summary>
    /// Glosnosc i tempo ZADANE przez uzytkownika. Trzymamy je tutaj, bo nowa
    /// sesja Core startuje z domyslnym 35 i tempem 1: bez tego kazde
    /// <c>queue.set</c> cicho cofaloby nastawy.
    /// </summary>
    private int _volume = 35;
    private double _rate = 1d;

    /// <summary>
    /// Id materialu, ktorego koniec jeszcze MOZE przesunac kolejke. Ustawiane
    /// przy kazdym starcie. Spozniony <c>playback.ended</c> innego Id (albo
    /// powtorzony tego samego) zostaje odrzucony -- inaczej kolejka skakalaby o
    /// dwa utwory.
    /// </summary>
    private string? _advanceToken;

    public LiteQueueCoordinator(IMediaOutput output) =>
        _output = output ?? throw new ArgumentNullException(nameof(output));

    /// <summary>Czy kolejka ma material. Host pyta, zanim ruszy transportem.</summary>
    public bool HasRows
    {
        get { lock (_gate) return _session?.HasItems == true; }
    }

    /// <summary>
    /// Glosnosc, ktora kolejka FAKTYCZNIE oddala silnikowi. Host synchronizuje
    /// z niej swoj stan transportu, zeby <c>transport.status</c> i kolejne
    /// <c>setVolume</c> nie startowaly od domyslnej wartosci sesji.
    /// </summary>
    public int CurrentVolume
    {
        get { lock (_gate) return _volume; }
    }

    /// <summary>Tempo, ktore kolejka FAKTYCZNIE oddala silnikowi.</summary>
    public double CurrentRate
    {
        get { lock (_gate) return _rate; }
    }

    /// <summary>
    /// Wczytuje wiersze do zywej kolejki. SAM odczyt nie odtwarza niczego:
    /// zadna sciezka tej metody nie wola <c>Play</c>.
    /// </summary>
    public QueueStatus Set(JsonElement args)
    {
        if (args.ValueKind != JsonValueKind.Object
            || !args.TryGetProperty("items", out var items)
            || items.ValueKind != JsonValueKind.Array)
        {
            throw new LiteRequestException("Brak wymaganego argumentu \"items\" (tablica).");
        }
        if (items.GetArrayLength() > MaximumRows)
        {
            throw new LiteRequestException($"Kolejka przekracza {MaximumRows} pozycji.");
        }

        var parsed = new List<MediaItem>(items.GetArrayLength());
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in items.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                throw new LiteRequestException("Kazda pozycja kolejki musi byc obiektem.");
            }
            var id = LiteArgs.RequireText(element, "id");
            // Duplikat Id zlamalby wyszukiwanie po Id w sesji; odmawiamy jawnie.
            if (!seen.Add(id))
            {
                throw new LiteRequestException($"Powtorzone Id w kolejce: {id}");
            }
            var path = LiteArgs.ReadText(element, "path");
            parsed.Add(new MediaItem
            {
                Id = id,
                Title = LiteArgs.ReadText(element, "title") ?? id,
                Kind = MediaItemKind.Track,
                Source = path,
                // Flagi kolejnosci idą z KONTRAKTU wywołującego, nie z domysłu:
                // pozycja bez zadnej z nich nie jest czescia kolejki.
                IsInQueue = LiteArgs.ReadBool(element, "isInQueue", false),
                IsPlayNext = LiteArgs.ReadBool(element, "isPlayNext", false)
            });
        }

        // Kolejnosc ZAPISANA przez wolajacego. Brak "order" to kolejnosc wierszy.
        var order = new List<string>();
        if (args.TryGetProperty("order", out var orderElement)
            && orderElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in orderElement.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.String)
                {
                    throw new LiteRequestException("Kazde Id w \"order\" musi byc napisem.");
                }
                order.Add(element.GetString() ?? string.Empty);
            }
        }
        if (order.Count == 0) order.AddRange(parsed.Select(item => item.Id));

        lock (_gate)
        {
            var session = new DemoMediaSession(
                LiteArgs.ReadText(args, "sessionId") ?? "lite-queue",
                "Kolejka",
                parsed,
                _output);
            // Kolejnosc i kontekst oddajemy SESJI: ona rozstrzyga nastepstwo.
            session.SetQueueOrder(order);
            session.SetPlaybackContext(order, isQueueContext: true);
            // Nowa sesja startuje z domyslnym 35 i tempem 1. Przenosimy tu
            // ZADANE nastawy, inaczej samo wczytanie listy cicho zmienialoby
            // glosnosc i tempo uzytkownika.
            session.SetVolume(_volume);
            if (session.SupportsPlaybackRate) session.SetDefaultPlaybackRate(_rate);
            _session = session;
            // Nowa kolejka uniewaznia stare zdarzenia poprzedniej i NIE
            // przejmuje transportu: samo wczytanie nic nie gra.
            _advanceToken = null;
            _leading = false;
            // Poprawny wsad (takze pusty) to WCZYTANIE. Ta linia stoi PO
            // wszystkich odmowach powyzej: odrzucone zadanie nie wczytuje nic.
            _initialized = true;
            return BuildStatus(session);
        }
    }

    /// <summary>
    /// Enter z widoku kolejki: start od WSKAZANEGO wiersza. Opcjonalne
    /// <c>volume</c> i <c>rate</c> sa ZADANIEM uzytkownika i musza dojsc do
    /// silnika: oddajemy je sesji PRZED <c>Play</c>, bo to ona podaje
    /// <c>EffectiveVolume</c> i <c>PlaybackRate</c> w wywolaniu wyjscia.
    /// Swiadomy start ZAWSZE przywraca prowadzenie kolejki, takze po wyjsciu
    /// poza nia.
    /// </summary>
    public QueueStatus PlayAt(JsonElement args)
    {
        var itemId = LiteArgs.RequireText(args, "itemId");
        lock (_gate)
        {
            var session = RequireSession();
            var item = Find(session, itemId)
                ?? throw new LiteRequestException($"Nie ma takiej pozycji w kolejce: {itemId}");
            // Brakujacy plik odmawiamy PRZED zajeciem silnika: inaczej host
            // zglaszalby blad odtwarzania czegos, co nawet nie istnieje.
            if (!string.IsNullOrWhiteSpace(item.Source) && !File.Exists(item.Source))
            {
                throw new LiteRequestException($"Nie znaleziono pliku: {item.Source}");
            }
            // Nastawy z zadania (gdy podane) obowiazuja od TEGO startu i trwaja
            // przez cala sesje: naturalne nastepstwo, pauza i wznowienie czytaja
            // je z sesji, wiec nie trzeba ich powtarzac przy kazdym utworze.
            ApplyTransportSettingsLocked(
                session,
                LiteArgs.ReadInt(args, "volume", _volume, 0, 100),
                LiteArgs.ReadDouble(args, "rate", _rate, 0.5d, 2.0d));
            if (!session.Play(item))
            {
                throw new LiteRequestException($"Nie udalo sie rozpoczac pozycji: {itemId}");
            }
            _advanceToken = item.Id;
            _leading = true;
            return BuildStatus(session);
        }
    }

    /// <summary>
    /// Nastawy transportu na SESJI kolejki. Osobno, bo host ma dwie drogi:
    /// swiadomy start z argumentami i zmiane w trakcie gry.
    /// </summary>
    private void ApplyTransportSettingsLocked(DemoMediaSession session, int volume, double rate)
    {
        _volume = volume;
        _rate = rate;
        session.SetVolume(volume);
        // SetDefaultPlaybackRate, nie SetPlaybackRate: kazde Play w sesji wola
        // ApplyPlaybackRateForItem, ktore bez nadpisania dla utworu wraca
        // wlasnie do DefaultPlaybackRate. Ustawienie samego biezacego tempa
        // przetrwaloby do pierwszego przejscia i cicho wrocilo do 1.
        if (session.SupportsPlaybackRate) session.SetDefaultPlaybackRate(rate);
    }

    /// <summary>
    /// Glosnosc w trakcie gry kolejki. Idzie przez SESJE, wiec obowiazuje tez
    /// nastepny utwor; dotad host ruszal samo wyjscie i kolejny <c>Play</c>
    /// wracal do wartosci sesji. Zwraca <c>false</c>, gdy transportu nie
    /// prowadzi kolejka -- wtedy host obsluguje go po swojemu.
    /// </summary>
    public bool SetVolume(int volume)
    {
        lock (_gate)
        {
            var session = _session;
            if (!_leading || session is null || !session.HasCurrentItem) return false;
            _volume = Math.Clamp(volume, 0, 100);
            session.SetVolume(_volume);
            return true;
        }
    }

    /// <summary>Tempo w trakcie gry kolejki. Jak <see cref="SetVolume"/>.</summary>
    public bool SetRate(double rate)
    {
        lock (_gate)
        {
            var session = _session;
            if (!_leading || session is null || !session.HasCurrentItem) return false;
            if (!session.SupportsPlaybackRate) return false;
            _rate = Math.Clamp(rate, 0.5d, 2.0d);
            // Takze tutaj DEFAULT, zeby nastawa objela nastepny utwor sesji.
            session.SetDefaultPlaybackRate(_rate);
            return true;
        }
    }

    /// <summary>
    /// Nastepny/Poprzedni po RZECZYWISTEJ kolejce. Zwraca <c>false</c>, gdy nie
    /// ma gdzie isc -- bez zawijania, zgodnie z <c>PlayRelative</c> z Core.
    /// </summary>
    public bool PlayRelative(int direction)
    {
        lock (_gate)
        {
            var session = _session;
            if (session is null || !session.HasItems) return false;
            // Po wyjsciu poza kolejke Nastepny/Poprzedni NIE wznawia jej cicho:
            // uzytkownik slucha czegos innego i spodziewa sie, ze przycisk
            // dziala na TYM, co gra. Powrot nastepuje swiadomym queue.playAt.
            if (!_leading) return false;
            if (!session.PlayRelative(direction)) return false;
            _advanceToken = session.HasCurrentItem ? session.CurrentItem.Id : null;
            return true;
        }
    }

    /// <summary>Pauza/wznowienie na tej samej sesji, bez utraty pozycji.</summary>
    public QueueStatus PauseResume()
    {
        lock (_gate)
        {
            var session = RequireSession();
            session.TogglePlayback();
            // Wznowienie (takze po zatrzymaniu) przywraca prawo do przesuniecia
            // kolejki: inaczej naturalny koniec wznowionego utworu nie
            // poprowadzilby dalej. Pauza to prawo odbiera, zeby spozniony koniec
            // z czasu przed pauza nie przeskoczyl utworu.
            _advanceToken = session.IsPlaying && session.HasCurrentItem
                ? session.CurrentItem.Id
                : null;
            return BuildStatus(session);
        }
    }

    public QueueStatus Stop()
    {
        lock (_gate)
        {
            var session = RequireSession();
            // Zatrzymanie idzie przez SESJE, nie przez samo wyjscie: inaczej
            // sesja nadal twierdzi, ze gra (IsPlaying zostaje true), a okno i
            // skroty zachowuja sie, jakby dzwiek trwal. Sesja zapamietuje przy
            // tym pozycje, wiec wznowienie wraca tam, gdzie uzytkownik przerwal.
            session.StopPlayback();
            // Po zatrzymaniu stary koniec utworu nie ma prawa ruszyc kolejki.
            // Prowadzenie ZOSTAJE: zatrzymanie nie jest wyjsciem poza kolejke,
            // wiec wznowienie i Nastepny nadal dzialaja na niej. Elementy tez
            // zostaja -- to kontrakt Core, nie nasz wymysl.
            _advanceToken = null;
            return BuildStatus(session);
        }
    }

    /// <summary>
    /// NATURALNY koniec utworu. Nastepstwo liczy
    /// <see cref="DemoMediaSession.ContinueAfterPlaybackEnded"/>, nie ta metoda.
    /// Zwraca <c>null</c>, gdy kolejka sie skonczyla ALBO gdy zdarzenie bylo
    /// spoznione -- w obu przypadkach bez petli i bez przeskoku o dwa utwory.
    /// </summary>
    public MediaItem? HandlePlaybackEnded(string? endedItemId)
    {
        if (string.IsNullOrWhiteSpace(endedItemId)) return null;
        lock (_gate)
        {
            var session = _session;
            if (session is null || !session.HasItems) return null;
            // Odlaczona kolejka nie reaguje na koniec utworu w ogole -- takze
            // gdy bezposrednio zagrany plik ma TO SAMO Id, co jej biezaca pozycja.
            if (!_leading) return null;
            // Bramka na SPOZNIONE zdarzenia: tylko koniec materialu, ktory
            // faktycznie zaczelismy ostatnio, przesuwa kolejke. Token zdejmujemy
            // od razu, wiec powtorzone zdarzenie tego samego Id nie zadziala.
            if (!string.Equals(_advanceToken, endedItemId, StringComparison.Ordinal)) return null;
            _advanceToken = null;

            var ended = Find(session, endedItemId);
            if (ended is null) return null;

            var next = session.ContinueAfterPlaybackEnded(ended);
            if (next is not null) _advanceToken = next.Id;
            return next;
        }
    }

    /// <summary>
    /// Odciecie kolejki od BEZPOSREDNIEGO odtwarzania. Gdy uzytkownik gra
    /// pozycje poza kolejka (zwykle <c>files.play</c>, zakladka, radio), koniec
    /// tamtego materialu NIE ma prawa przesunac kolejki. Bez tego bezposrednie
    /// odtworzenie konczylo sie cichym skokiem do kolejki.
    /// </summary>
    public void DetachFromDirectPlay()
    {
        lock (_gate)
        {
            _advanceToken = null;
            // Samo wyczyszczenie tokenu NIE wystarcza: OwnsCurrent pytal tylko o
            // Id biezacej pozycji sesji, wiec bezposrednie odtworzenie TEGO
            // SAMEGO pliku, ktory stoi w kolejce, nadal oddawalo jej pauze,
            // Nastepny i koniec utworu. Prowadzenie gasimy jawnie.
            _leading = false;
        }
    }

    /// <summary>
    /// Czy transport prowadzi KOLEJKA i czy dotyczy on tego Id. Oba warunki sa
    /// konieczne: samo Id nie rozstrzyga, bo ten sam plik moze byc grany wprost.
    /// </summary>
    public bool OwnsCurrent(string? itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return false;
        lock (_gate)
        {
            var session = _session;
            return _leading
                && session is not null
                && session.HasCurrentItem
                && string.Equals(session.CurrentItem.Id, itemId, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// BIEZACA pozycja kolejki -- ta sama instancja <see cref="MediaItem"/>,
    /// ktora dostal silnik, ze sciezka zrodla. Host buforuje ja jako "co gra
    /// teraz"; obiekt zlozony z samego zadania klamalby o sciezce.
    /// </summary>
    public MediaItem? CurrentItem
    {
        get
        {
            lock (_gate)
            {
                var session = _session;
                return session is not null && session.HasCurrentItem ? session.CurrentItem : null;
            }
        }
    }

    /// <summary>
    /// Czy po TYM materiale kolejka poprowadzi dalej? Pytanie BEZ SKUTKOW:
    /// nie przesuwa kursora, nie zuzywa zdarzenia, nie startuje odtwarzania.
    /// Sluzy tylko do tego, by okno nie oglaszalo konca przed przejsciem.
    /// </summary>
    public bool WouldAdvanceAfter(string? endedItemId)
    {
        if (string.IsNullOrWhiteSpace(endedItemId)) return false;
        lock (_gate)
        {
            var session = _session;
            if (session is null || !session.HasItems) return false;
            // Odlaczona kolejka nie zapowiada przejscia, bo i nie przesunie sie.
            if (!_leading) return false;
            // Ta sama bramka, co w HandlePlaybackEnded: spoznione zdarzenie nie
            // zapowiada przejscia, bo i nie przesunie kolejki.
            if (!string.Equals(_advanceToken, endedItemId, StringComparison.Ordinal)) return false;

            // Kolejnosc nastepstwa bierzemy z KONTRAKTU sesji
            // (QueueNavigationItemIds), a nie z kolejnosci Items: to ona
            // rozstrzyga, co zagra po tym materiale.
            var order = session.QueueNavigationItemIds;
            var at = -1;
            for (var i = 0; i < order.Count; i++)
            {
                if (string.Equals(order[i], endedItemId, StringComparison.Ordinal)) { at = i; break; }
            }
            return at >= 0 && at + 1 < order.Count;
        }
    }

    public QueueStatus Status()
    {
        lock (_gate)
        {
            var session = _session;
            return session is null
                ? new QueueStatus([], null, null, false, false, 0d, _initialized)
                : BuildStatus(session);
        }
    }

    private const int MaximumRows = 50_000;

    private DemoMediaSession RequireSession()
    {
        var session = _session;
        if (session is null || !session.HasItems)
        {
            throw new LiteRequestException("Kolejka jest pusta.");
        }
        return session;
    }

    private static MediaItem? Find(DemoMediaSession session, string itemId) =>
        session.Items.FirstOrDefault(item =>
            string.Equals(item.Id, itemId, StringComparison.Ordinal));

    private QueueStatus BuildStatus(DemoMediaSession session)
    {
        var currentId = session.HasCurrentItem ? session.CurrentItem.Id : null;
        // Wiersze bierzemy w KOLEJNOSCI SESJI (QueueItemIds), nie w kolejnosci
        // Items: to sesja wie, co jeszcze zostalo do odtworzenia.
        var rows = session.QueueItemIds
            .Select(itemId => Find(session, itemId))
            .Where(item => item is not null)
            .Select(item => new QueueRow(
                item!.Id,
                item.Title,
                item.Source,
                item.IsPlayNext,
                string.Equals(item.Id, currentId, StringComparison.Ordinal)))
            .ToArray();

        return new QueueStatus(
            rows,
            currentId,
            session.HasCurrentItem ? session.CurrentItem.Title : null,
            session.IsPlaying,
            session.IsPaused,
            session.HasCurrentItem ? session.Position.TotalSeconds : 0d,
            _initialized);
    }
}

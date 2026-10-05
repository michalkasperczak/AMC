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
        double PositionSeconds);

    private readonly IMediaOutput _output;
    private readonly object _gate = new();

    private DemoMediaSession? _session;

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
            _session = session;
            // Nowa kolejka uniewaznia stare zdarzenia poprzedniej.
            _advanceToken = null;
            return BuildStatus(session);
        }
    }

    /// <summary>Enter z widoku kolejki: start od WSKAZANEGO wiersza.</summary>
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
            if (!session.Play(item))
            {
                throw new LiteRequestException($"Nie udalo sie rozpoczac pozycji: {itemId}");
            }
            _advanceToken = item.Id;
            return BuildStatus(session);
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
            return BuildStatus(session);
        }
    }

    public QueueStatus Stop()
    {
        lock (_gate)
        {
            var session = RequireSession();
            session.StopPlayback();
            // Po zatrzymaniu stary koniec utworu nie ma prawa ruszyc kolejki.
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
        lock (_gate) _advanceToken = null;
    }

    /// <summary>Czy kolejka prowadzi material o tym Id (czyli czy to jej transport).</summary>
    public bool OwnsCurrent(string? itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return false;
        lock (_gate)
        {
            var session = _session;
            return session is not null
                && session.HasCurrentItem
                && string.Equals(session.CurrentItem.Id, itemId, StringComparison.Ordinal);
        }
    }

    public QueueStatus Status()
    {
        lock (_gate)
        {
            var session = _session;
            return session is null
                ? new QueueStatus([], null, null, false, false, 0d)
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

    private static QueueStatus BuildStatus(DemoMediaSession session)
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
            session.HasCurrentItem ? session.Position.TotalSeconds : 0d);
    }
}

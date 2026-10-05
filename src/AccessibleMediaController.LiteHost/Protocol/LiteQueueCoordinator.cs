using System.Text.Json;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Playback;
using AccessibleMediaController.Core.Sessions;
using Microsoft.Data.Sqlite;

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
/// TRWALOSC: koordynator NIE pisze do bazy sam. Caly zapis idzie przez
/// <see cref="LiteQueueStore"/> -- JEDEN wlasciciel po stronie C#. Python nie
/// zapisuje plikow kolejki w ogole; przy braku magazynu (tryb odczytu albo
/// brak profilu) kolejka dziala tak jak dotad, tylko w pamieci procesu.
///
/// Migawke do zapisu liczy <see cref="TransientQueuePersistence.Capture"/> --
/// ten sam kod, ktorego uzywa pelne AMC. Nie powtarzamy tu reguly czlonkostwa.
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
        bool Initialized,
        /// <summary>
        /// Czy TEN host jest wlascicielem zapisu kolejki. <c>false</c> znaczy
        /// "kolejka zyje tylko w pamieci procesu" -- frontend nie moze wtedy
        /// obiecywac uzytkownikowi, ze stan przetrwa restart.
        /// </summary>
        bool Persistent,
        /// <summary>
        /// Komunikat ostatniej ODMOWY/bledu zapisu albo <c>null</c>. Odmowa
        /// MUSI byc widoczna: cichy brak trwalosci jest gorszy od bledu.
        /// </summary>
        string? PersistError,
        /// <summary>Ile zapisow FAKTYCZNIE poszlo do profilu w tym procesie.</summary>
        int PersistedWrites,
        /// <summary>Ile wierszy wczytano Z PROFILU przy starcie tego procesu.</summary>
        int RestoredRows,
        /// <summary>
        /// Czas, Z KTOREGO wystartuje biezaca pozycja, gdy uzytkownik swiadomie
        /// kaze jej grac (<c>queue.playAt</c>). Zero oznacza "od poczatku" --
        /// albo nie ma zapisanej pozycji, albo polityka AMC jej nie pozwala
        /// uzyc. Okno potrzebuje tej liczby, zeby nie obiecywac wznowienia,
        /// ktorego nie bedzie.
        /// </summary>
        double ResumeSeconds);

    private readonly IMediaOutput _output;
    private readonly object _gate = new();

    /// <summary>
    /// Magazyn trwalosci albo <c>null</c>, gdy host wystartowal bez profilu.
    /// <c>null</c> NIE jest bledem: kolejka dziala wtedy w pamieci procesu.
    /// </summary>
    private readonly LiteQueueStore? _store;

    private DemoMediaSession? _session;

    /// <summary>
    /// Sesja OSTATNIO wczytanej kolejki. Zapisujemy wylacznie sesje, ktora
    /// magazyn obsluguje (<c>local</c>) -- cudzej kolejki nie dotykamy.
    /// </summary>
    private string _sessionId = LiteQueueStore.LocalSessionId;

    /// <summary>
    /// Ostatni komunikat ODMOWY zapisu. Frontend musi go zobaczyc: odmowa nie
    /// moze wygladac jak zapisano.
    /// </summary>
    private string? _lastPersistError;

    /// <summary>Ile razy zapis FAKTYCZNIE poszedl do bazy w tym procesie.</summary>
    private int _persistedWrites;

    /// <summary>Ile razy zapis pominieto, bo stan sie nie zmienil.</summary>
    private int _persistSkipped;

    /// <summary>Ile wierszy wczytano Z ZAPISU przy starcie tego procesu.</summary>
    private int _restoredRows;

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

    /// <summary>
    /// POZYCJA ostatnio zanotowana przez hosta dla biezacego utworu kolejki.
    /// Host czyta czas z dekodera i podaje go tutaj; bez tego checkpoint musial
    /// by pytac wyjscia, ktore po zatrzymaniu juz nic nie wie.
    /// </summary>
    private TimeSpan _notedPosition;

    /// <summary>
    /// Id pozycji, do ktorej nalezy <see cref="_notedPosition"/>. Osobne pole,
    /// bo po wyjsciu poza kolejke (bezposredni plik, radio) czas naplywa dalej
    /// i NIE WOLNO przypisac go Id ostatniej pozycji kolejki.
    /// </summary>
    private string? _notedPositionItemId;

    /// <summary>
    /// Czasy pozycji wczytane Z PROFILU przy starcie hosta. Przy swiadomym
    /// starcie utworu oddajemy je sesji -- to jest cale "wznowienie z
    /// niezerowego czasu" widziane z zewnatrz.
    /// </summary>
    private LiteResumeState? _restoredResume;

    /// <summary>
    /// Zapamietane ustawienia wznawiania z profilu: GLOBALNE i tryb SESJI
    /// <c>local</c>. Czytamy je RAZ: to plik uzytkownika, a host nie ma powodu
    /// wracac do niego przy kazdym zapisie ani przy kazdej pozycji.
    /// </summary>
    private (bool RememberGlobally, ResumePositionMode SessionMode)? _profileResumeSettings;

    public LiteQueueCoordinator(IMediaOutput output, LiteQueueStore? store = null)
    {
        _output = output ?? throw new ArgumentNullException(nameof(output));
        _store = store;
        // ODCZYT przy starcie hosta. To jest cala "trwalosc" widziana z zewnatrz:
        // nowy proces hosta zastaje kolejke taka, jaka zapisal poprzedni.
        // Odczyt NIE odtwarza niczego i nie przejmuje transportu.
        RestoreFromStore();
    }

    /// <summary>
    /// Wczytuje ZAPISANA kolejke z profilu. Zwraca liczbe odtworzonych wierszy.
    ///
    /// Puste zapisane wiersze tez sa WYNIKIEM, nie brakiem: kolejka zuzyta do
    /// zera musi po restarcie zostac pusta, a nie odrodzic stare pozycje.
    /// Dlatego <c>_initialized</c> idzie na <c>true</c> takze dla zera -- byle
    /// magazyn faktycznie cos powiedzial (<c>queue_order</c> moglo byc puste,
    /// bo zapisalismy pustke).
    /// </summary>
    public int RestoreFromStore()
    {
        if (_store is null) return 0;
        LiteQueueStoredState stored;
        try
        {
            stored = _store.Read(LiteQueueStore.LocalSessionId);
        }
        catch (Exception exception) when (exception is LiteQueueStoreDenied or IOException)
        {
            // Nieudany ODCZYT nie moze udawac pustej kolejki: brak wczytania
            // zostaje brakiem wczytania, a powod idzie do stanu.
            lock (_gate) _lastPersistError = "Nie udalo sie odczytac zapisanej kolejki: " + exception.Message;
            return 0;
        }

        // Dane WZNOWIENIA czytamy z tego samego profilu, NIEZALEZNIE od tego,
        // czy kolejka byla juz zapisana: tryb pozycji (resume_mode) jest
        // POLITYKA uzytkownika i obowiazuje takze przy pierwszym zapisie, gdy
        // nie ma jeszcze czego wczytywac. Nieudany odczyt nie moze przewrocic
        // wczytania kolejki -- wznowienie jest dodatkiem i NIE MOZE blokowac
        // podstawy.
        LiteResumeState? resume = null;
        try
        {
            resume = _store.ReadResume(LiteQueueStore.LocalSessionId);
        }
        catch (Exception exception) when (exception is LiteQueueStoreDenied or IOException or InvalidOperationException or SqliteException)
        {
            lock (_gate)
            {
                _lastPersistError =
                    "Nie udalo sie odczytac zapisanej pozycji wznowienia: " + exception.Message;
            }
        }
        lock (_gate) _restoredResume = resume;

        // Brak ZAPISU to nie to samo, co zapisana pustka. Gdy nikt nic nie
        // zapisal, host zostaje NIEWCZYTANY -- frontend ma wtedy prawo wczytac
        // kolejke z ekranu. Zapisana pustka wczytuje sie jako pustka.
        if (!stored.Saved) return 0;

        var items = stored.Rows
            .Select(row => new MediaItem
            {
                Id = row.Id,
                Title = row.Title,
                Kind = MediaItemKind.Track,
                Source = row.Path,
                IsInQueue = row.IsInQueue,
                IsPlayNext = row.IsPlayNext
            })
            .ToList();

        lock (_gate)
        {
            var session = new DemoMediaSession(
                LiteQueueStore.LocalSessionId, "Kolejka", items, _output);
            var order = stored.Rows.Select(row => row.Id).ToArray();
            session.SetQueueOrder(order);
            session.SetPlaybackContext(order, isQueueContext: true);
            session.SetVolume(_volume);
            if (session.SupportsPlaybackRate) session.SetDefaultPlaybackRate(_rate);
            _session = session;
            _sessionId = LiteQueueStore.LocalSessionId;
            _advanceToken = null;
            _leading = false;
            _initialized = true;
            _restoredRows = items.Count;
            _restoredResume = resume;
            _notedPosition = TimeSpan.Zero;
            _notedPositionItemId = null;

            // Pozycje z profilu oddajemy SESJI, zeby swiadomy start wznowil z
            // zapisanego czasu. Polityka AMC decyduje o kazdej osobno, a sama
            // kolejka NIE jest przez to odtwarzana (zadnego Play tutaj).
            if (resume is not null && items.Count > 0)
            {
                foreach (var item in items)
                {
                    var position = ResolveRestorablePosition(resume, item);
                    if (position > TimeSpan.Zero) session.SetRememberedPosition(item.Id, position);
                }
                // BIEZACY utwor tylko wtedy, gdy nadal jest w KOLEJCE. Kolejka
                // zuzyta do zera nie ma biezacego utworu, choc profil moze
                // jeszcze pamietac ostatnie Id.
                if (resume.CurrentItemId is { Length: > 0 } currentId
                    && items.Any(item => string.Equals(item.Id, currentId, StringComparison.Ordinal)))
                {
                    var current = items.First(item =>
                        string.Equals(item.Id, currentId, StringComparison.Ordinal));
                    // SelectItem ustawia biezaca pozycje BEZ odtwarzania: host
                    // po starcie nie gra sam z siebie.
                    session.SelectItem(current);
                }
            }
            return items.Count;
        }
    }

    /// <summary>Czy ten host jest WLASCICIELEM zapisu kolejki.</summary>
    public bool CanPersist => _store?.IsWritable == true && _store.OwnsWriteLock;

    /// <summary>
    /// Notuje POZYCJE biezacego utworu kolejki. Host wola to z czasem z
    /// dekodera (np. przy pauzie, zatrzymaniu i przed zamknieciem), bo po
    /// zatrzymaniu wyjscie juz nie zna pozycji.
    ///
    /// Gdy kolejka NIE PROWADZI transportu (bezposredni plik, radio), czas jest
    /// ODRZUCANY: nalezy do cudzego materialu i przypisanie go Id kolejki
    /// byloby falszem.
    /// </summary>
    public bool NotePosition(TimeSpan position)
    {
        lock (_gate)
        {
            var session = _session;
            if (!_leading || session is null || !session.HasCurrentItem) return false;
            _notedPosition = position < TimeSpan.Zero ? TimeSpan.Zero : position;
            _notedPositionItemId = session.CurrentItem.Id;
            session.SetPosition(_notedPosition);
            return true;
        }
    }

    /// <summary>
    /// Utrwala PUNKT WZNOWIENIA: biezacy utwor i czasy pozycji kolejki.
    ///
    /// Polityke stosujemy TUTAJ, przed zapisem, dokladnie jak
    /// <c>MainWindow.SaveLocalPlaybackCheckpoint</c>: pozycja z trybem
    /// <c>StartFromBeginning</c> idzie do bazy jako ZERO, a nie jest pomijana
    /// -- inaczej po wylaczeniu wznawiania w bazie zostalby stary czas.
    ///
    /// Zwraca <c>true</c> tylko gdy zapis FAKTYCZNIE poszedl. Odmowa oddaje
    /// <c>false</c> i zostawia powod w stanie; nigdy nie udaje powodzenia.
    /// </summary>
    public bool SaveResumeCheckpoint()
    {
        lock (_gate)
        {
            var store = _store;
            var session = _session;
            if (store is null || session is null) return false;

            // Czas liczy sie TYLKO dla tej pozycji, do ktorej go zanotowano.
            if (_notedPositionItemId is { Length: > 0 } notedId
                && session.Items.Any(item => string.Equals(item.Id, notedId, StringComparison.Ordinal)))
            {
                session.SetRememberedPosition(notedId, _notedPosition);
            }

            var positions = new Dictionary<string, TimeSpan>(StringComparer.Ordinal);
            foreach (var item in session.Items)
            {
                var remembered = session.RememberedPositions.GetValueOrDefault(item.Id);
                // POLITYKA AMC: tryb pozycji rozstrzyga, czy czas ma byc
                // pamietany. Brak zgody = jawne ZERO w bazie.
                positions[item.Id] = ShouldRememberPosition(item) ? remembered : TimeSpan.Zero;
            }

            // Biezacy utwor tylko wtedy, gdy kolejka faktycznie go ma. Kolejka
            // zuzyta do zera zapisuje NULL, zeby nowy host nie uznal ostatniego
            // utworu za material do wznowienia.
            var currentId = session.HasCurrentItem && session.QueueItemIds.Count > 0
                ? session.CurrentItem.Id
                : null;

            try
            {
                var written = store.WriteResume(
                    _sessionId, new LiteResumeCheckpoint(currentId, positions));
                if (written)
                {
                    _persistedWrites++;
                    _lastPersistError = null;
                }
                else
                {
                    _persistSkipped++;
                }
                return written;
            }
            catch (Exception exception) when (exception is LiteQueueStoreDenied or IOException or SqliteException)
            {
                // Odmowa MUSI byc widoczna. Cichy brak trwalosci pozycji jest
                // gorszy od bledu: uzytkownik inaczej liczy na wznowienie.
                _lastPersistError = exception.Message;
                return false;
            }
        }
    }

    /// <summary>
    /// Czy pozycja ma PAMIETAC czas odtwarzania.
    ///
    /// Decyzje liczy JEDNA BRAMKA w Core
    /// (<c>ResumePositionPolicy.ShouldRememberLocalPosition</c>) -- ta sama,
    /// ktorej uzywa <c>MainWindow.ShouldRememberLocalPosition</c>. Host nie
    /// powtarza tu reguly, tylko dostarcza warstwy: jawny tryb POZYCJI
    /// (<c>local_items.resume_mode</c>), najblizsza jawna OPCJA FOLDERU
    /// (<c>folder_playback_options</c>), najblizsze jawne ZRODLO FOLDERU
    /// (<c>folder_sources</c>), tryb SESJI <c>local</c> oraz ustawienie
    /// GLOBALNE -- oba ostatnie z <c>state.json</c> tego samego profilu.
    ///
    /// Dzieki temu wylaczenie wznawiania w AMC -- globalne, sesyjne albo na
    /// folderze -- obowiazuje takze tutaj, a jawny <c>Remember</c> na pozycji
    /// nadal nadpisuje wylaczone tlo.
    /// </summary>
    private bool ShouldRememberPosition(MediaItem item) =>
        ShouldRememberPosition(_restoredResume, item);

    /// <summary>
    /// Wariant przyjmujacy stan wznowienia jawnie, zeby TA SAMA decyzja dzialala
    /// przy PRZYWRACANIU (gdy pole <c>_restoredResume</c> jeszcze nie jest
    /// ustawione) i przy ZAPISIE. Rozjazd miedzy tymi dwoma miejscami znaczylby,
    /// ze stary niezerowy czas w bazie wznawia sie mimo wylaczenia.
    /// </summary>
    private bool ShouldRememberPosition(LiteResumeState? resume, MediaItem item)
    {
        var entry = resume?.Entries.GetValueOrDefault(item.Id);
        var profile = ReadResumeProfileSettings();
        return ResumePositionPolicy.ShouldRememberLocalPosition(
            entry is null ? null : ToResumeMode(entry.ResumeMode),
            item.Source,
            resume?.FolderPlaybackOptions,
            resume?.FolderSources,
            profile.SessionMode,
            profile.RememberGlobally);
    }

    private static ResumePositionMode ToResumeMode(int value) =>
        Enum.IsDefined(typeof(ResumePositionMode), value)
            ? (ResumePositionMode)value
            : ResumePositionMode.Inherit;

    /// <summary>
    /// GLOBALNE <c>AppSettings.RememberLocalPlaybackPositions</c> oraz tryb
    /// SESJI <c>local</c> (<c>AppSettings.ResumePositionModeBySession</c>) z
    /// profilu.
    ///
    /// Czytamy DWIE wartosci wprost z <c>state.json</c>, bez
    /// <c>ConfigurationStore</c>: ten przy otwarciu MIGRUJE schemat i zapisuje
    /// caly <c>AppState</c>, a host ma tylko odczytac ustawienia -- nie wolno mu
    /// przepisac stanu uzytkownika z powodu malej listy. Brak pliku albo
    /// nieczytelny plik oznacza DOMYSLNE <c>true</c> i <c>Inherit</c>, dokladnie
    /// jak <c>AppSettings</c>.
    ///
    /// Tryb sesji w pliku AMC jest NAPISEM (<c>"StartFromBeginning"</c>), bo
    /// <c>ConfigurationStore</c> serializuje enumy przez
    /// <c>JsonStringEnumConverter</c>; liczbe tez przyjmujemy, zeby recznie
    /// zlozony profil nie wywrocil odczytu.
    /// </summary>
    private (bool RememberGlobally, ResumePositionMode SessionMode) ReadResumeProfileSettings()
    {
        if (_profileResumeSettings is { } cached) return cached;

        var remember = true;
        var sessionMode = ResumePositionMode.Inherit;
        var statePath = _store is null ? null : Path.Combine(_store.ProfileDirectory, "state.json");
        try
        {
            if (statePath is not null && File.Exists(statePath))
            {
                using var document = JsonDocument.Parse(File.ReadAllBytes(statePath));
                if (document.RootElement.TryGetProperty("settings", out var settings))
                {
                    if (settings.TryGetProperty("rememberLocalPlaybackPositions", out var flag)
                        && flag.ValueKind is JsonValueKind.True or JsonValueKind.False)
                    {
                        remember = flag.GetBoolean();
                    }

                    if (settings.TryGetProperty("resumePositionModeBySession", out var bySession)
                        && bySession.ValueKind is JsonValueKind.Object)
                    {
                        // Klucze sesji w AppSettings sa bez wzgledu na wielkosc
                        // liter (OrdinalIgnoreCase) -- szukamy tak samo, zeby
                        // "Local" w pliku nie zostal przeoczony.
                        foreach (var property in bySession.EnumerateObject())
                        {
                            if (!string.Equals(
                                    property.Name,
                                    LiteQueueStore.LocalSessionId,
                                    StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }
                            sessionMode = property.Value.ValueKind switch
                            {
                                JsonValueKind.String
                                    when Enum.TryParse<ResumePositionMode>(
                                        property.Value.GetString(), ignoreCase: true, out var parsed)
                                    => parsed,
                                JsonValueKind.Number when property.Value.TryGetInt32(out var number)
                                    => ToResumeMode(number),
                                _ => ResumePositionMode.Inherit
                            };
                            break;
                        }
                    }
                }
            }
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            // Nieczytelny state.json nie moze przewrocic kolejki. Zostaje
            // domyslne AMC (true, Inherit), a powod jest widoczny w stanie.
            _lastPersistError = "Nie udalo sie odczytac ustawienia wznawiania: " + exception.Message;
        }

        _profileResumeSettings = (remember, sessionMode);
        return _profileResumeSettings.Value;
    }

    /// <summary>
    /// Pozycja, z ktorej WOLNO wznowic dany utwor, albo zero.
    ///
    /// Powtarza regule <c>MainWindow.CanRestorePosition</c>: zerowy czas nie
    /// jest wznowieniem, a ODCISK PLIKU musi sie zgadzac. Podmieniony plik pod
    /// tym samym Id startuje od poczatku -- stary czas wskazywalby w nim inne
    /// miejsce.
    ///
    /// POLITYKA idzie PRZED odciskiem i jest DOKLADNIE ta sama, co przy zapisie
    /// (<see cref="ShouldRememberPosition(LiteResumeState?, MediaItem)"/>).
    /// Dlatego metoda nie jest statyczna: musi widziec ustawienia profilu.
    /// Bez tego STARY niezerowy czas w bazie wznawialby sie mimo wylaczenia
    /// wznawiania w AMC -- sam zapis zera po wylaczeniu tego nie zalatwia.
    /// </summary>
    private TimeSpan ResolveRestorablePosition(LiteResumeState resume, MediaItem item)
    {
        var entry = resume.Entries.GetValueOrDefault(item.Id);
        if (entry is null || entry.ResumePositionTicks <= 0) return TimeSpan.Zero;
        if (!ShouldRememberPosition(resume, item)) return TimeSpan.Zero;

        if (string.IsNullOrWhiteSpace(item.Source) || !File.Exists(item.Source))
        {
            // Bez pliku nie ma czego sprawdzac; zapisany czas zostaje, tak jak
            // w pelnym AMC przy nieodczytywalnym odcisku.
            return TimeSpan.FromTicks(entry.ResumePositionTicks);
        }

        try
        {
            var file = new FileInfo(item.Source);
            if (entry.FileLength.HasValue && entry.FileLength.Value != file.Length) return TimeSpan.Zero;
            if (entry.LastWriteUtcTicks.HasValue
                && entry.LastWriteUtcTicks.Value != file.LastWriteTimeUtc.Ticks)
            {
                return TimeSpan.Zero;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return TimeSpan.FromTicks(entry.ResumePositionTicks);
        }

        return TimeSpan.FromTicks(entry.ResumePositionTicks);
    }

    /// <summary>Komunikat ostatniej ODMOWY zapisu albo <c>null</c>.</summary>
    public string? LastPersistError
    {
        get { lock (_gate) return _lastPersistError; }
    }

    public int PersistedWrites
    {
        get { lock (_gate) return _persistedWrites; }
    }

    public int PersistSkipped
    {
        get { lock (_gate) return _persistSkipped; }
    }

    public int RestoredRows
    {
        get { lock (_gate) return _restoredRows; }
    }

    /// <summary>
    /// ZAPIS stanu kolejki do profilu. Wywolac TRZYMAJAC <see cref="_gate"/>.
    ///
    /// Trzy zasady, ktore ta metoda utrzymuje:
    /// <list type="bullet">
    /// <item>zapis idzie TYLKO po faktycznej zmianie -- podpis liczy magazyn,</item>
    /// <item>odmowa nie jest powodzeniem: komunikat ladauje w <c>_lastPersistError</c>,</item>
    /// <item>brak magazynu nie jest bledem -- host bez profilu dziala w pamieci.</item>
    /// </list>
    ///
    /// Migawke liczy <see cref="TransientQueuePersistence.Capture"/>: ten sam
    /// kod, ktory zapisuje kolejke w pelnym AMC. Kolejnosc bierzemy z sesji
    /// (<c>QueueItemIds</c>) -- to ona wie, co jeszcze zostalo.
    /// </summary>
    private void PersistLocked(DemoMediaSession session)
    {
        if (_store is null) return;
        // Sesje inne niz lokalna Biblioteka maja kopie kolejki w pelnym AMC i
        // nie sa tu zapisywane. Milczace pominiecie, nie blad: host Lite i tak
        // ich nie obsluguje.
        if (!string.Equals(_sessionId, LiteQueueStore.LocalSessionId, StringComparison.Ordinal)) return;

        try
        {
            var queued = session.Items.Where(item => item.IsInQueue || item.IsPlayNext).ToArray();
            var snapshot = TransientQueuePersistence.Capture(
                LiteQueueStore.LocalSessionId, queued, session.QueueItemIds);
            if (_store.Write(LiteQueueStore.LocalSessionId, snapshot)) _persistedWrites++;
            else _persistSkipped++;
            _lastPersistError = null;
        }
        catch (Exception exception) when (exception is LiteQueueStoreDenied or IOException or Microsoft.Data.Sqlite.SqliteException)
        {
            // Nieudany zapis NIE moze przejsc jako zapisano. Zywa kolejka dalej
            // dziala w pamieci -- odmowa trwalosci nie psuje odtwarzania.
            _lastPersistError = exception.Message;
        }
    }

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
            _sessionId = session.Id;
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
            // TRWALOSC: nowa czlonkostwo i kolejnosc to FAKTYCZNA zmiana stanu
            // kolejki, wiec ida do profilu. Magazyn sam pominie zapis, gdy stan
            // wyszedl identyczny (np. frontend wczytal to samo po restarcie).
            PersistLocked(session);
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
            // Nastepny/Poprzedni w kolejce ZUZYWA pozycje (ConsumeQueueItem w
            // Core), wiec czlonkostwo sie zmienilo i trzeba je utrwalic.
            PersistLocked(session);
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
            // NATURALNY koniec utworu zdejmuje go z kolejki (ContinueAfterPlaybackEnded
            // zeruje IsInQueue/IsPlayNext). To jest ta zmiana, ktora musi dojsc do
            // profilu, zeby po RESTARCIE PROCESU zuzyte pozycje nie wrocily --
            // takze gdy kolejka zeszla do zera (next == null).
            // RED-WITNESS sprawdzony: po zakomentowaniu tej linii
            // ZuzycieNaturalnymKoncemDoZeraNieOdradzaPozycji pada (zapisow 1,
            // oczekiwano >=2). Test mierzy wiec FAKTYCZNY zapis, nie etykiete.
            PersistLocked(session);
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
            // Notowana pozycja przestaje nalezec do kolejki. Bez tego czas
            // bezposrednio granego pliku trafilby pod Id pozycji kolejki.
            _notedPositionItemId = null;
            _notedPosition = TimeSpan.Zero;
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
                ? new QueueStatus([], null, null, false, false, 0d, _initialized,
                    CanPersist, _lastPersistError, _persistedWrites, _restoredRows, 0d)
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
            // Kolejka moze twierdzic, ze gra, TYLKO gdy nadal prowadzi
            // odtwarzanie. Po odlaczeniu (bezposredni files.play, radio,
            // zakladka) material sesji zostaje nietkniety -- wiersze i pozycja
            // sa wciaz prawdziwe -- ale to nie kolejka jest zrodlem dzwieku.
            // Bez tej bramki frontend stawia na przycisku "Wstrzymaj" dla
            // kolejki, ktora nic nie odtwarza.
            _leading && session.IsPlaying,
            _leading && session.IsPaused,
            session.HasCurrentItem ? session.Position.TotalSeconds : 0d,
            _initialized,
            CanPersist,
            _lastPersistError,
            _persistedWrites,
            _restoredRows,
            // Czas SWIADOMEGO wznowienia biezacej pozycji. Bierzemy go z TEJ
            // SAMEJ pamieci sesji, ktorej uzyje start (DemoMediaSession.Play),
            // wiec status nie moze obiecac czasu niezgodnego z gra.
            session.HasCurrentItem
                ? session.RememberedPositions
                    .GetValueOrDefault(session.CurrentItem.Id).TotalSeconds
                : 0d);
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AccessibleMediaController.Core.Commands;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Core.Sonos;
using AccessibleMediaController.Windows.Services;

namespace AccessibleMediaController.Windows;

/// <summary>
/// SESJA SONOS w AMC, obslugiwana jak istniejacy WiiM: lista GRUP, aktywna
/// grupa, odtwarzacz nad ODCZYTANYM stanem i te same globalne polecenia.
///
/// Swiadome granice tej sesji:
///   * Sonos jest URZADZENIEM AUTONOMICZNYM. AMC nie ma tu wlasnego toru audio,
///     nie buduje silnika HTTP i NIE zatrzymuje muzyki przy wyjsciu z
///     odtwarzacza, zmianie sesji ani zamknieciu programu,
///   * dom i grupa trzymane PO IDENTYFIKATORZE; zniknieta grupa nie jest po
///     cichu zastepowana inna, bo sterowalibysmy nie tym pokojem,
///   * kazde polecenie jest PRZEPUSZCZANE przez bramke z ODCZYTU
///     (availablePlaybackActions, volume.fixed) i nie ma tu zadnego ponawiania
///     POST - to zostaje w koordynatorze,
///   * po poleceniu robimy JAWNY GET w rodzaju polecenia i mowimy tylko to, co
///     odczyt POTWIERDZIL. HTTP 200 to przyjecie zlecenia, nie wykonanie,
///   * JEDEN przelot polecenia naraz dla aktywnego celu: drugie zadanie jest
///     JAWNIE odrzucane, nie kolejkowane (ukryta kolejka toggle dawalaby
///     przypadkowy koncowy stan),
///   * odczyt w tle jest POJEDYNCZY i oszczedny; interwal to POLITYKA AMC.
///     Bilet celu (dom+grupa) uniewaznia spoznione odpowiedzi, zeby odpowiedz
///     grupy A nie nadpisala widoku grupy B.
///
/// Poza etapem, celowo NIE MA tu martwych przyciskow: presety, ulubione, EQ,
/// wejscia, kolejka i webhooki Sonos.
/// </summary>
public partial class MainWindow
{
    internal const string SonosSessionId = SonosSessionListPresentation.SessionId;

    /// <summary>
    /// TESTOWE podstawienie zaplecza grup. Produkcyjnie null - wtedy uzywany
    /// jest adapter nad JEDNYM wlascicielem konta. To FAKTYCZNA granica
    /// API/transportu, wiec pomiar okna nie dotyka konta ani sieci.
    /// </summary>
    internal ISonosGroupSessionBackend? SonosBackendOverride { get; set; }

    private ISonosGroupSessionBackend? _sonosBackend;
    private SonosHouseholdTopology? _sonosTopology;
    private IReadOnlyList<SonosHousehold> _sonosHouseholds = [];
    private IReadOnlyList<SonosGroupRow> _sonosGroupRows = [];
    private SonosSessionEmptyReason _sonosEmptyReason = SonosSessionEmptyReason.NotRead;
    private SonosGroupPlaybackStatus? _sonosPlayback;
    private SonosGroupMetadata? _sonosMetadata;
    private SonosGroupVolume? _sonosVolume;
    private DateTime _sonosReadUtc;
    private bool _sonosOwnerInitialized;

    /// <summary>
    /// BILET aktywnego celu. Rosnie przy KAZDEJ zmianie domu, grupy i wyjsciu z
    /// sesji - odpowiedz ze starym biletem jest wyrzucana, nie publikowana.
    /// Jest PONAD generacja konta: konto moze zostac to samo, a cel inny.
    /// </summary>
    private int _sonosTargetTicket;

    private CancellationTokenSource? _sonosCancellation;

    /// <summary>JEDNO jawne odswiezenie naraz: druga proba nie mnozy GET.</summary>
    private bool _sonosRefreshInFlight;

    /// <summary>
    /// WLASCICIEL bramki odswiezenia. Bez niego spozniony przelot A zwalnialby
    /// bramke trwajacego przelotu B w swoim finally.
    /// </summary>
    private int _sonosRefreshGateTicket;

    /// <summary>JEDEN przelot polecenia naraz. Brak ukrytej kolejki.</summary>
    private bool _sonosCommandInFlight;

    /// <summary>
    /// BILET WLASCICIELA bramki polecenia. Bilet celu (<see cref="_sonosTargetTicket"/>)
    /// NIE nadaje sie na wlasciciela: zmiana grupy w trakcie polecenia A podnosi
    /// bilet celu, wiec spoznione <c>finally</c> A nie rozpoznawalo sie jako
    /// wlasciciel i zostawialo bramke ZAMKNIETA na zawsze - kolejne polecenie
    /// grupy B slyszalo "poprzednie jeszcze sie nie zakonczylo" i nie szlo do
    /// backendu. Rosnie przy KAZDYM wzieciu bramki, wiec spoznione finally
    /// starszego przelotu nie zwolni bramki nalezacej do NOWSZEGO polecenia.
    /// </summary>
    private int _sonosCommandGateTicket;

    /// <summary>
    /// ZADANIE odczytu w tle w locie albo null. PRAWDZIWA bariera, nie sam
    /// termin: dopoki pierwszy odczyt (stan + metadane + glosnosc) sie nie
    /// domknie, kolejne tykniecia licznika NIE wysylaja ani jednego GET.
    /// </summary>
    private Task? _sonosBackgroundRead;

    /// <summary>
    /// ZNACZNIK KOLEJNOSCI odczytu. Rosnie przy kazdym wejsciu do
    /// <see cref="ReadSonosGroupStateAsync"/>, a publikacja wynikow sprawdza, czy
    /// nadal jest NAJNOWSZA. Bilet celu tego NIE lapie: odczyt tla i odczyt po
    /// poleceniu TEJ SAMEJ grupy maja ten sam bilet celu, wiec starszy odczyt
    /// tla nadpisywal swiezszy wynik potwierdzajacy polecenie.
    /// </summary>
    private int _sonosReadSequence;

    private DateTime _sonosNextBackgroundReadUtc;

    /// <summary>
    /// POLITYKA AMC, nie rzekomy limit Sonosa: jeden odczyt na 10 sekund dla
    /// UZYWANEJ grupy. Po bledzie i po 429 wchodzi backoff, wiec polly sie nie
    /// nakladaja i nie zalewamy chmury.
    /// </summary>
    internal static readonly TimeSpan SonosBackgroundReadInterval = TimeSpan.FromSeconds(10);

    internal static readonly TimeSpan SonosBackoffAfterFailure = TimeSpan.FromSeconds(60);

    /// <summary>
    /// POMIAROWE ujscie komunikatow. Produkcyjnie null - wtedy mowi zwykly
    /// <c>Announce</c>. W testach pozwala SPRAWDZIC, co uslyszalby uzytkownik,
    /// bez uruchamiania mowy i bez zabierania fokusu.
    /// </summary>
    internal Action<string>? AnnouncementSinkForTests { get; set; }

    /// <summary>Prawdziwy SessionManager dla pomiaru - bez tworzenia atrapy sesji.</summary>
    internal SessionManager SessionsForTests => _sessions;

    /// <summary>
    /// POMIAR polityki wyjscia z odtwarzacza na PRAWDZIWEJ metodzie: sprawdza,
    /// ze Sonos nie dostaje zadnego zatrzymania.
    /// </summary>
    internal void ApplyPlaybackPolicyWhenLeavingPlayerForTests(
        DemoMediaSession session,
        PlayerDepartureReason reason) =>
        ApplyPlaybackPolicyWhenLeavingPlayer(session, reason);

    /// <summary>Pomiar: zaden instalator aktualizacji nie wystartuje.</summary>
    internal void DenyApplicationUpdateStartForTests() =>
        _applicationUpdateStartOverride = _ =>
            throw new InvalidOperationException("Instalator aktualizacji jest zabroniony w pomiarze.");

    internal static bool IsSonosSession(string? sessionId) =>
        string.Equals(sessionId, SonosSessionId, StringComparison.Ordinal);

    internal string? SonosSelectedHouseholdId => _state.Sonos.SelectedHouseholdId;

    internal string? SonosSelectedGroupId => _state.Sonos.SelectedGroupId;

    /// <summary>AKTYWNA grupa rozwiazana PO ID z aktualnej topologii albo null.</summary>
    internal SonosGroup? SonosActiveGroup =>
        SonosActiveGroupPolicy.Resolve(_state.Sonos.SelectedGroupId, _sonosTopology);

    internal IReadOnlyList<SonosGroupRow> SonosGroupRows => _sonosGroupRows;

    internal SonosSessionEmptyReason SonosEmptyReason => _sonosEmptyReason;

    internal int SonosTargetTicket => _sonosTargetTicket;

    /// <summary>
    /// Zaplecze grup. NIE jest tworzone na starcie AMC: dopiero JAWNE wejscie do
    /// sesji albo polecenie moze dotknac wlasciciela konta. RebuildCore i
    /// UpdateMenus nie wolaja tego, wiec zwykly start nie czyta konta ani sieci.
    /// </summary>
    internal ISonosGroupSessionBackend EnsureSonosBackend()
    {
        if (SonosBackendOverride is { } injected) return injected;
        _sonosOwnerInitialized = true;
        return _sonosBackend ??= new SonosAccountOwnerGroupBackend(_sonosAccount);
    }

    internal bool SonosOwnerInitialized => _sonosOwnerInitialized;

    /// <summary>
    /// ODNIESIENIE znacznika podlaczenia konta dla tej sesji. <c>null</c> znaczy
    /// NIEZNANE: nic jeszcze nie bylo powiazane, wiec PIERWSZY odczyt jest
    /// punktem odniesienia, a nie zdarzeniem zmiany. Nie porownujemy tego
    /// miedzy instancjami ani procesami.
    /// </summary>
    private long? _sonosBoundAccountGeneration;

    /// <summary>Ile razy sesja porzucila dane po RZECZYWISTEJ zmianie konta. Kwit pomiaru.</summary>
    internal int SonosAccountChangeDropsForTests { get; private set; }

    internal long? SonosBoundAccountGenerationForTests => _sonosBoundAccountGeneration;

    /// <summary>Kwity pomiaru L2: co zostalo z odczytow grupy po zmianie konta.</summary>
    internal SonosGroupPlaybackStatus? SonosPlaybackForTests => _sonosPlayback;

    internal SonosGroupMetadata? SonosMetadataForTests => _sonosMetadata;

    internal SonosGroupVolume? SonosVolumeForTests => _sonosVolume;

    /// <summary>Stan aplikacji tego okna - kwit pomiaru zapisanego wyboru.</summary>
    internal PersistedState StateForTests => _state;

    /// <summary>PRAWDZIWY prezenter okna konta tego okna. Tworzy go leniwie, jak produkcja.</summary>
    internal SonosAccountPresenter SonosAccountPresenterForTests =>
        _sonosAccountPresenter ??= new SonosAccountPresenter(_sonosAccount);

    /// <summary>
    /// Ustala ODNIESIENIE znacznika podlaczenia konta, gdy jeszcze go nie ma, i
    /// NIE porzuca przy tym niczego. Wolane tam, gdzie konto i tak zostanie
    /// zainicjowane (okno konta), zeby pozniejsza RZECZYWISTA zmiana miala z czym
    /// sie porownac. Zero znacznika to prawidlowa wartosc odniesienia, nie brak
    /// konta. Poza ta droga leniwosc zostaje nietknieta.
    /// </summary>
    internal void EstablishSonosAccountBindingReference()
    {
        if (_sonosBoundAccountGeneration is not null) return;
        var backend = EnsureSonosBackend() as ISonosAccountBoundBackend
            ?? SonosBackendOverride as ISonosAccountBoundBackend;
        if (backend?.AccountSnapshot is not { } snapshot) return;
        _sonosBoundAccountGeneration = snapshot.AccountBindingGeneration;
    }

    /// <summary>
    /// GRANICA konta przed odczytem albo poleceniem sesji: gdy obserwowane konto
    /// zostalo RZECZYWISCIE zastapione albo odlaczone, porzucamy dane i zadania
    /// STAREGO konta. Zwykla rotacja poswiadczen tego nie robi, bo znacznik
    /// podlaczenia sie nie zmienia.
    ///
    /// Migawka <c>null</c> (zaplecze bez konta, konto niezainicjowane) to
    /// NIEZNANE, a NIE odlaczenie: stare syntetyczne zaplecza dzialaja dalej.
    /// Sam odczyt NIE budzi konta - migawka jest lokalna, bez sieci.
    /// </summary>
    /// <returns><c>true</c>, gdy stan starego konta wlasnie porzucono.</returns>
    internal bool ApplySonosAccountBinding()
    {
        var backend = _sonosBackend as ISonosAccountBoundBackend
            ?? SonosBackendOverride as ISonosAccountBoundBackend;
        if (backend?.AccountSnapshot is not { } snapshot) return false;

        var generation = snapshot.AccountBindingGeneration;
        if (_sonosBoundAccountGeneration is not { } bound_generation)
        {
            // PIERWSZY odczyt jest ODNIESIENIEM. Zero nie znaczy "konta nie ma":
            // odtworzony zapis ma znacznik 0 i jego wybor musi przezyc.
            _sonosBoundAccountGeneration = generation;
            return false;
        }

        if (generation == bound_generation) return false;

        // RZECZYWISTA zmiana albo odlaczenie: dane i cele starego konta przestaja
        // cokolwiek znaczyc. Najpierw uniewazniamy operacje w locie odebrana
        // sciezka, potem czyscimy zapamietany stan - inaczej spozniony GET
        // odtworzylby stara liste.
        _sonosBoundAccountGeneration = generation;
        SonosAccountChangeDropsForTests++;
        CancelSonosPendingWork();
        _sonosHouseholds = [];
        _sonosTopology = null;
        _state.Sonos.SelectedHouseholdId = null;
        _state.Sonos.SelectedGroupId = null;
        _sonosPlayback = null;
        _sonosMetadata = null;
        _sonosVolume = null;
        _sonosReadUtc = default;
        _sonosNextBackgroundReadUtc = DateTime.MinValue;
        _sonosEmptyReason = SonosSessionEmptyReason.NotRead;
        // JEDNA istniejaca droga publikacji, nie druga kopia czyszczenia: samo
        // zerowanie prywatnego _sonosGroupRows nie ruszalo RZECZYWISTEJ
        // DemoMediaSession ani kontrolki listy, wiec czytnik dalej czytal pokoje
        // STAREGO konta, a HasCurrentItem dalej wskazywal jego wiersz.
        // ApplySonosGroupRows robi ReplaceItems i RefreshCurrentView - po
        // wyzerowanej topologii wierszy grup nie ma, zostaje uczciwy pusty stan.
        ApplySonosGroupRows();
        // Zadnego POST: nie zatrzymujemy muzyki i nie ruszamy innych sesji.
        QueueStateSave(announceFailure: false);
        return true;
    }

    /// <summary>
    /// JAWNE wejscie do sesji Sonos: wolno tu obudzic wlasciciela konta i
    /// odczytac domy oraz grupy. Wybor grupy NIE dotyka muzyki.
    /// </summary>
    internal async Task EnterSonosSessionAsync()
    {
        var backend = EnsureSonosBackend();
        // GRANICA konta PRZED uzyciem zapamietanej listy domow: po rzeczywistej
        // zmianie konta stare domy i wybor nie moga wrocic. Bilet bierzemy PO
        // niej, bo porzucenie podnosi bilet celu.
        ApplySonosAccountBinding();
        var ticket = _sonosTargetTicket;
        var token = EnsureSonosCancellation().Token;
        try
        {
            if (_sonosHouseholds.Count == 0)
            {
                var households = await backend.ReadHouseholdsAsync(token).ConfigureAwait(true);
                // GRANICA konta PO await (L2): wynik STAREGO konta odrzucamy, a
                // porzucenie podnosi bilet, wiec odczyt grup starego household
                // ID nizej w ogole nie wyjdzie.
                if (ApplySonosAccountBinding()) return;
                if (ticket != _sonosTargetTicket || _isClosing) return;
                if (!households.Succeeded || households.Households is null)
                {
                    _sonosEmptyReason = households.Status == SonosDeviceReadStatus.NoAccount
                        ? SonosSessionEmptyReason.NoAccount
                        : SonosSessionEmptyReason.NotRead;
                    ApplySonosGroupRows();
                    return;
                }

                _sonosHouseholds = households.Households;
            }

            // Zniknietego domu NIE podmieniamy po cichu. Gdy wybor jest pusty, a
            // dom dokladnie jeden, wybor jest jednoznaczny i wolno go przyjac.
            var household = SonosActiveGroupPolicy.ResolveHousehold(
                _state.Sonos.SelectedHouseholdId,
                _sonosHouseholds);
            if (household is null && _state.Sonos.SelectedHouseholdId is null && _sonosHouseholds.Count == 1)
            {
                household = _sonosHouseholds[0];
                _state.Sonos.SelectedHouseholdId = household.Id;
            }

            if (household is null)
            {
                // UDANY odczyt zero domow to BRAK DOMU, nie brak konta: inaczej
                // Enter na pustej liscie kazalby sie logowac na podlaczonym koncie.
                _sonosEmptyReason = _sonosHouseholds.Count == 0
                    ? SonosSessionEmptyReason.NoHouseholds
                    : SonosSessionEmptyReason.NotRead;
                ApplySonosGroupRows();
                return;
            }

            var groups = await backend.ReadGroupsAsync(household.Id, token).ConfigureAwait(true);
            if (ApplySonosAccountBinding()) return;
            if (ticket != _sonosTargetTicket || _isClosing) return;
            if (!groups.Succeeded || groups.Topology is null)
            {
                _sonosEmptyReason = groups.Status == SonosDeviceReadStatus.NoAccount
                    ? SonosSessionEmptyReason.NoAccount
                    : SonosSessionEmptyReason.NotRead;
                ApplySonosGroupRows();
                return;
            }

            ApplySonosTopology(groups.Topology);
        }
        catch (OperationCanceledException)
        {
        }
    }

    /// <summary>
    /// JAWNE odswiezenie topologii Sonos NA ZYCZENIE uzytkownika. Czyta domy i
    /// grupy Z ZAPLECZA, nie z cache, i publikuje POTWIERDZONY swiezy odczyt.
    ///
    /// JEDNO odswiezenie naraz: druga probra w trakcie pierwszej NIE mnozy GET.
    /// Bramka ma WLASCICIELA (bilet), zeby spozniony przelot A nie odblokowal ani
    /// nie podmienil trwajacego B.
    ///
    /// BLAD odczytu to NIE dowod zniknięcia: poprawnych identyfikatorow nie
    /// niszczymy i pustki nie publikujemy jako sukcesu. Zniknięcie domu albo
    /// grupy liczy sie WYLACZNIE po POTWIERDZONYM swiezym odczycie.
    /// </summary>
    internal async Task RefreshSonosTopologyAsync()
    {
        if (_sonosRefreshInFlight)
        {
            // SWIADOMA powtorka: krotka informacja, ZERO dodatkowych GET.
            Announce("Odświeżanie grup Sonos już trwa");
            return;
        }

        var backend = EnsureSonosBackend();
        // GRANICA konta PRZED wzieciem biletu bramki: ApplySonosAccountBinding
        // moze samo wywolac CancelSonosPendingWork, ktory PODNOSI bilet bramki.
        // Wziecie biletu wczesniej uniewaznialoby WLASNY przelot i po zmianie
        // konta odswiezenie nigdy by nie doszlo do publikacji.
        ApplySonosAccountBinding();
        var gate = ++_sonosRefreshGateTicket;
        _sonosRefreshInFlight = true;
        var ticket = _sonosTargetTicket;
        var token = EnsureSonosCancellation().Token;
        Announce("Odświeżam grupy Sonos");
        try
        {
            var households = await backend.ReadHouseholdsAsync(token).ConfigureAwait(true);
            if (ApplySonosAccountBinding()) return;
            if (ticket != _sonosTargetTicket || _isClosing || gate != _sonosRefreshGateTicket) return;
            if (!households.Succeeded || households.Households is null)
            {
                // Brak swiezosci, a NIE zniknięcie: wybor i lista zostaja.
                AnnounceSonosRefreshNotFresh(households.Status);
                return;
            }

            _sonosHouseholds = households.Households;
            var chosenHouseholdId = _state.Sonos.SelectedHouseholdId;
            var household = SonosActiveGroupPolicy.ResolveHousehold(
                chosenHouseholdId,
                _sonosHouseholds);
            if (household is null && chosenHouseholdId is null && _sonosHouseholds.Count == 1)
            {
                // Ta SAMA istniejaca regula poczatkowa: pusty wybor i dokladnie
                // jeden dom to wybor jednoznaczny. Wieloddomowy wybor to B2c2.
                household = _sonosHouseholds[0];
                _state.Sonos.SelectedHouseholdId = household.Id;
            }

            if (household is null && chosenHouseholdId is not null)
            {
                // SWIADOMY dom ZNIKNAL po potwierdzonym odczycie: nie podmieniamy
                // go po cichu na inny, uniewazniamy caly cel.
                InvalidateSonosTargetAfterConfirmedDisappearance(
                    _sonosHouseholds.Count == 0
                        ? SonosSessionEmptyReason.NoHouseholds
                        : SonosSessionEmptyReason.NotRead,
                    "Wybrany dom Sonos już nie istnieje. Wybór został wyczyszczony");
                return;
            }

            if (household is null)
            {
                // ZADEN dom nie byl wybrany: to NIE zniknięcie. Niczego nie
                // uniewazniamy (nie ma czego), nie wybieramy domu za uzytkownika
                // i nie podsuwamy nazwy - dostepne okno wyboru to B2c2. Zaden POST.
                _sonosEmptyReason = _sonosHouseholds.Count == 0
                    ? SonosSessionEmptyReason.NoHouseholds
                    : SonosSessionEmptyReason.NotRead;
                ApplySonosGroupRows();
                Announce(_sonosHouseholds.Count == 0
                    ? "Odświeżono: konto Sonos nie udostępnia żadnego domu"
                    : $"Odświeżono domy Sonos: {_sonosHouseholds.Count}. Nie wybrano domu, więc nie ma grup do pokazania");
                return;
            }

            var groups = await backend.ReadGroupsAsync(household.Id, token).ConfigureAwait(true);
            if (ApplySonosAccountBinding()) return;
            if (ticket != _sonosTargetTicket || _isClosing || gate != _sonosRefreshGateTicket) return;
            if (!groups.Succeeded || groups.Topology is null)
            {
                AnnounceSonosRefreshNotFresh(groups.Status);
                return;
            }

            var selected = _state.Sonos.SelectedGroupId;
            var vanished = selected is not null
                && SonosActiveGroupPolicy.Resolve(selected, groups.Topology) is null;
            if (vanished)
            {
                // POTWIERDZONE zniknięcie AKTYWNEJ grupy: najpierw uniewazniamy
                // wszystko w locie istniejaca droga, POTEM publikujemy swieza
                // topologie. Inaczej spozniony GET starej grupy odtworzylby jej
                // dane, a widok siedzialby w odtwarzaczu porzuconego celu.
                CancelSonosPendingWork();
                _sonosTopology = groups.Topology;
                ClearSonosTargetState();
                if (_playerViewActive && IsSonosSession(_sessions?.Current.Id)) ReturnFromPlayerToList();
                _sonosEmptyReason = groups.Topology.Groups.Count == 0
                    ? SonosSessionEmptyReason.NoGroups
                    : SonosSessionEmptyReason.NotRead;
                ApplySonosGroupRows();
                QueueStateSave(announceFailure: false);
                Announce("Aktywna grupa Sonos już nie istnieje. Odświeżono grupy, wybór wyczyszczony");
                return;
            }

            // NIEDESTRUKCYJNE odswiezenie: ApplySonosTopology zachowuje wybor po
            // IDENTYFIKATORZE mimo zmiany nazw i kolejnosci, a ApplySonosGroupRows
            // przywraca zaznaczony WIERSZ.
            ApplySonosTopology(groups.Topology);
            Announce(SonosRefreshSummary(groups.Topology.Groups.Count));
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            // Bramke zwalnia TYLKO jej wlasciciel: spozniony przelot A nie
            // odblokuje trwajacego B.
            if (gate == _sonosRefreshGateTicket) _sonosRefreshInFlight = false;
        }
    }

    private static string SonosRefreshSummary(int groupCount) => groupCount switch
    {
        0 => "Odświeżono: ten dom Sonos nie ma żadnych grup",
        1 => "Odświeżono grupy Sonos: 1 grupa",
        _ => $"Odświeżono grupy Sonos: {groupCount}"
    };

    /// <summary>
    /// Nieudany odczyt: mowimy o BRAKU SWIEZOSCI, a nie o zniknięciu. Zaden
    /// identyfikator ani wiersz nie ginie, pustki nie publikujemy.
    /// </summary>
    private void AnnounceSonosRefreshNotFresh(SonosDeviceReadStatus status) => Announce(
        status == SonosDeviceReadStatus.NoAccount
            ? "Nie odświeżono grup Sonos: brak podłączonego konta"
            : "Nie udało się odświeżyć grup Sonos. Pokazane grupy mogą być nieaktualne");

    /// <summary>
    /// Zniknięcie SWIADOMEGO domu po POTWIERDZONYM odczycie: uniewazniamy wyniki
    /// w locie i CALY cel. Zadnego POST - muzyki nie zatrzymujemy.
    /// </summary>
    private void InvalidateSonosTargetAfterConfirmedDisappearance(
        SonosSessionEmptyReason reason,
        string message)
    {
        CancelSonosPendingWork();
        _sonosTopology = null;
        _state.Sonos.SelectedHouseholdId = null;
        ClearSonosTargetState();
        if (_playerViewActive && IsSonosSession(_sessions?.Current.Id)) ReturnFromPlayerToList();
        _sonosEmptyReason = reason;
        ApplySonosGroupRows();
        QueueStateSave(announceFailure: false);
        Announce(message);
    }

    /// <summary>Sam CEL i jego dane, bez dotykania topologii i domow.</summary>
    private void ClearSonosTargetState()
    {
        _state.Sonos.SelectedGroupId = null;
        _sonosPlayback = null;
        _sonosMetadata = null;
        _sonosVolume = null;
        _sonosReadUtc = default;
        _sonosNextBackgroundReadUtc = DateTime.MinValue;
    }

    /// <summary>
    /// Nowa topologia. Zapamietany wybor grupy przezywa TYLKO wtedy, gdy grupa o
    /// tym IDENTYFIKATORZE nadal istnieje; inaczej wybor jest CZYSZCZONY, a nie
    /// przenoszony na sasiada.
    /// </summary>
    internal void ApplySonosTopology(SonosHouseholdTopology topology)
    {
        _sonosTopology = topology;
        var selected = _state.Sonos.SelectedGroupId;
        if (selected is not null && SonosActiveGroupPolicy.Resolve(selected, topology) is null)
        {
            _state.Sonos.SelectedGroupId = null;
            _sonosPlayback = null;
            _sonosMetadata = null;
            _sonosVolume = null;
        }

        _sonosEmptyReason = topology.Groups.Count == 0
            ? SonosSessionEmptyReason.NoGroups
            : SonosSessionEmptyReason.NotRead;
        ApplySonosGroupRows();
    }

    private void ApplySonosGroupRows()
    {
        _sonosGroupRows = SonosSessionListPresentation.DescribeGroups(_sonosTopology);
        var session = _sessions?.FindSession(SonosSessionId);
        if (session is null) return;
        // Wiersze listy to GRUPY, nie odtwarzalny material AMC: Kind.Device jak
        // urzadzenia WiiM, wiec zaden ogolny tor odtwarzania ich nie tknie.
        session.ReplaceItems(_sonosGroupRows
            .Select(row => new MediaItem
            {
                Id = row.GroupId,
                Title = row.Name,
                // ZWYKLY odczyt wiersza to KROTKA nazwa grupy (np. "Biuro").
                // Liczba glosnikow i stan powtarzaly nazwe w kazdym wierszu;
                // szczegoly sa w oknie Glosniki i grupy (Ctrl+F5) oraz w
                // odtwarzaczu po Enter, wiec nic sie nie gubi.
                Kind = MediaItemKind.Device
            })
            .ToList());

        // Wejscie do sesji jest ASYNCHRONICZNE: ReplaceItems konczy sie DLUGO po
        // tym, jak przelaczenie sesji odswiezylo widok. Bez odswiezenia TERAZ
        // kontrolka listy zostawala pusta az do ponownego wejscia - uzytkownik
        // slyszal "lista pusta" przy PIERWSZYM Ctrl+8, mimo odczytanych grup.
        // Odswiezamy WYLACZNIE gdy Sonos jest biezaca sesja i widac liste:
        // obcej sesji ani otwartego odtwarzacza nie ruszamy.
        if (_isClosing || !IsSonosSession(_sessions?.Current.Id) || _playerViewActive) return;
        // Zaznaczenie uzytkownika jest SWIADOME: zachowujemy je po identyfikatorze,
        // a nie po indeksie, bo topologia mogla sie przestawic.
        var selectedGroupId = (MediaList.SelectedItem as MediaItemRow)?.Item.Id
            ?? _state.Sonos.SelectedGroupId;
        RefreshCurrentView(preferredItemId: selectedGroupId);
    }

    /// <summary>
    /// Enter na grupie: grupa staje sie AKTYWNA i otwiera sie odtwarzacz. Sam
    /// wybor NIE wysyla zadnego POST, wiec muzyka w pokoju sie nie zmienia.
    /// </summary>
    /// <returns>
    /// Bilet WYSTAWIONY przez te aktywacje albo <c>null</c>, gdy grupy nie ma.
    /// Wolajacy po await musi porownac GO z biezacym biletem: wartosc sprzed
    /// await nalezy jeszcze do poprzedniego celu.
    /// </returns>
    internal async Task<int?> ActivateSonosGroupAsync(string groupId)
    {
        if (SonosActiveGroupPolicy.Resolve(groupId, _sonosTopology) is not { } group)
        {
            // Instrukcja odzyskania nazywa ISTNIEJACE polecenie z menu i palety.
            Announce("Ta grupa Sonos już nie istnieje. Użyj polecenia Odśwież grupy Sonos");
            return null;
        }

        // Zmiana celu: stary bilet przestaje byc wazny, spoznione odpowiedzi
        // poprzedniej grupy nie nadpisza tej.
        var ticket = ++_sonosTargetTicket;
        _state.Sonos.SelectedGroupId = group.Id;
        _sonosPlayback = null;
        _sonosMetadata = null;
        _sonosVolume = null;
        _sonosNextBackgroundReadUtc = DateTime.MinValue;
        QueueStateSave(announceFailure: true);
        await ReadSonosGroupStateAsync().ConfigureAwait(true);
        return ticket;
    }

    /// <summary>
    /// JAWNY odczyt stanu, metadanych i glosnosci aktywnej grupy. Nieudany
    /// odczyt NIE zostawia poprzednich danych jako biezacych - pola wracaja do
    /// braku informacji, bo stary tytul przy nowym utworze to klamstwo.
    /// </summary>
    internal async Task<bool> ReadSonosGroupStateAsync()
    {
        // GRANICA konta PRZED odczytem: po rzeczywistej zmianie konta nie ma
        // czego odczytywac, a stary cel nie moze pojsc przez nowe konto.
        if (ApplySonosAccountBinding()) return false;
        if (SonosActiveGroup is not { } group) return false;
        var ticket = _sonosTargetTicket;
        // KOLEJNOSC odczytow tej SAMEJ grupy: nowszy odczyt uniewaznia starszy.
        var sequence = ++_sonosReadSequence;
        var backend = EnsureSonosBackend();
        var token = EnsureSonosCancellation().Token;
        try
        {
            var playback = await backend.ReadGroupPlaybackAsync(group.Id, token).ConfigureAwait(true);
            if (IsSonosReadStaleOrAccountChanged(ticket, sequence)) return false;
            var metadata = await backend.ReadGroupMetadataAsync(group.Id, token).ConfigureAwait(true);
            if (IsSonosReadStaleOrAccountChanged(ticket, sequence)) return false;
            var volume = await backend.ReadGroupVolumeAsync(group.Id, token).ConfigureAwait(true);
            if (IsSonosReadStaleOrAccountChanged(ticket, sequence)) return false;

            _sonosPlayback = playback.Succeeded ? playback.Value : null;
            _sonosMetadata = metadata.Succeeded ? metadata.Value : null;
            _sonosVolume = volume.Succeeded ? volume.Value : null;
            _sonosReadUtc = DateTime.UtcNow;
            // PELNY odczyt to WSZYSTKIE trzy czesci. Metadane byly wczesniej
            // pominiete w tej decyzji, wiec brak tytulu przy udanym stanie
            // planowal zwykly termin i udawal potwierdzony odczyt. Czesciowe
            // dane nie potwierdzaja calosci; zera ani czasu nie wymyslamy.
            var ok = playback.Succeeded && metadata.Succeeded && volume.Succeeded;
            _sonosNextBackgroundReadUtc = _sonosReadUtc
                + (ok ? SonosBackgroundReadInterval : SonosBackoffAfterFailure);
            if (_playerViewActive && IsSonosSession(_sessions.Current.Id)) UpdatePlayerView();
            return ok;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception) when (RegisterSonosReadFailureBackoff(ticket, sequence))
        {
            // Wyjatek transportu (np. timeout) NIE moze przejsc cicho ani
            // zostawic starych danych jako biezacych: backoff jest zapisany w
            // filtrze powyzej, a wyjatek leci dalej do obserwujacego zadania.
            throw;
        }
    }

    /// <summary>
    /// Czy TEN odczyt jest juz NIEAKTUALNY: zmieniony cel, zamykanie okna albo
    /// NOWSZY odczyt tej samej grupy (np. potwierdzenie polecenia po odczycie
    /// tla). Sam bilet celu tego nie rozstrzygal.
    /// </summary>
    private bool IsSonosReadStale(int ticket, int sequence) =>
        ticket != _sonosTargetTicket || sequence != _sonosReadSequence || _isClosing;

    /// <summary>
    /// To samo co <see cref="IsSonosReadStale"/> PLUS granica konta PO await.
    /// L2 z przegladu 680: sprawdzenie tylko przed await przepuszczalo wynik
    /// GET-a, ktory wrocil juz po RZECZYWISTEJ zmianie konta, i ten wynik trafial
    /// do sesji. <see cref="ApplySonosAccountBinding"/> samo porzuca dane i
    /// podnosi bilet celu, wiec kolejny GET ze STARYM identyfikatorem nie wyjdzie
    /// - nie potrzeba dodatkowego odpytania ani polecenia.
    /// </summary>
    private bool IsSonosReadStaleOrAccountChanged(int ticket, int sequence) =>
        ApplySonosAccountBinding() || IsSonosReadStale(ticket, sequence);

    /// <summary>
    /// Zapisuje BACKOFF po wyjatku odczytu, nie tlumiac wyjatku (filtr zwraca
    /// false dla nieaktualnego przelotu, wiec cudzego terminu nie ruszamy).
    /// </summary>
    private bool RegisterSonosReadFailureBackoff(int ticket, int sequence)
    {
        if (IsSonosReadStale(ticket, sequence)) return false;
        _sonosPlayback = null;
        _sonosMetadata = null;
        _sonosVolume = null;
        _sonosNextBackgroundReadUtc = DateTime.UtcNow + SonosBackoffAfterFailure;
        return true;
    }

    /// <summary>Widok odtwarzacza dla aktywnej grupy, wylacznie z ODCZYTU.</summary>
    internal SonosPlayerView BuildSonosPlayerView(DateTime nowUtc)
    {
        var position = SonosPlayerPosition.Resolve(
            _sonosPlayback,
            _sonosMetadata?.CurrentTrack?.DurationMillis,
            _sonosReadUtc,
            nowUtc);
        return SonosPlayerPresentation.Describe(_sonosPlayback, _sonosMetadata, _sonosVolume, position);
    }

    private void UpdateSonosPlayerView(bool updateAccessibleName)
    {
        var view = BuildSonosPlayerView(DateTime.UtcNow);
        var group = SonosActiveGroup;
        PlayerTitleText.Text = view.Title;
        PlayerArtistText.Text = view.Source;
        PlayerSessionText.Text = group is null
            ? "Sonos, brak aktywnej grupy"
            : "Sonos, " + group.Name;
        PlayerStateText.Text = view.StateText + ". " + view.VolumeText + ". " + view.PositionText;
        // POMIAR B3: te kontrolki NIE byly tu ustawiane, wiec po wejsciu z
        // odtwarzacza innej sesji zostawal w nich JEJ czas, predkosc i etykieta
        // przycisku. Niewidomy uzytkownik slyszal wtedy dane cudzej sesji jako
        // stan Sonosa. Kazda kontrolka odtwarzacza musi pochodzic z ODCZYTU
        // grupy albo jawnie zniknac.
        PlayerTimeText.Text = view.PositionText;
        // Sonos nie ma predkosci odtwarzania ani zakladek AMC - te kontrolki
        // klamalyby o mozliwosciach urzadzenia, wiec sa ukryte, nie martwe.
        PlayerSpeedText.Visibility = System.Windows.Visibility.Collapsed;
        PlayerPreviousButton.Content = "Poprzedni element";
        PlayerNextButton.Content = "Następny element";
        foreach (var control in new System.Windows.FrameworkElement[]
                 {
                     PlayerRateDownButton,
                     PlayerRateUpButton,
                     PlayerRateResetButton,
                     PlayerAddBookmarkButton,
                     PlayerAddNamedBookmarkButton,
                     PlayerBookmarksButton
                 })
        {
            control.Visibility = System.Windows.Visibility.Collapsed;
        }
        // Przewijanie do MIEJSCA wymaga znanej dlugosci. Bez odczytanej dlugosci
        // te przyciski nie maja czego celowac.
        var position = SonosPlayerPosition.Resolve(
            _sonosPlayback,
            _sonosMetadata?.CurrentTrack?.DurationMillis,
            _sonosReadUtc,
            DateTime.UtcNow);
        var seekVisibility = position.Duration > TimeSpan.Zero
            ? System.Windows.Visibility.Visible
            : System.Windows.Visibility.Collapsed;
        PlayerSeekTimeButton.Visibility = seekVisibility;
        PlayerSeekPercentButton.Visibility = seekVisibility;
        RadioRecordingButton.Visibility = System.Windows.Visibility.Collapsed;
        PlayerHelpText.Text = PlayerKeyboardHelpText();
        // Tresc przycisku bierzemy z ODCZYTANEGO stanu grupy, nie z
        // DemoMediaSession i nie z Accepted 200 poprzedniego polecenia.
        var pausing = _sonosPlayback?.PlaybackState is SonosPlaybackState.Playing
            or SonosPlaybackState.Buffering;
        var action = pausing ? "Wstrzymaj" : "Odtwórz";
        PlayerPlayPauseButton.Content = action;
        // Dostepna nazwa NADPISUJE tresc dla czytnika, wiec nieaktualna nazwa
        // KLAMIE nawet przy poprawnej etykiecie. Przestawiamy ja przy KAZDEJ
        // zmianie odczytu, nie tylko przy fokusie - ale TYLKO gdy naprawde sie
        // zmienila, zeby odczyt w tle nie wywolywal zdarzen UIA co cykl.
        var buttonName = view.Title + ", " + PlayerSessionText.Text + ", " + view.StateText + ". " + action;
        if (!string.Equals(
                System.Windows.Automation.AutomationProperties.GetName(PlayerPlayPauseButton),
                buttonName,
                StringComparison.Ordinal))
        {
            System.Windows.Automation.AutomationProperties.SetName(PlayerPlayPauseButton, buttonName);
        }

        if (!updateAccessibleName) return;
        _playerFocusContextPrefix = null;
        PlayerPanel.SetValue(
            System.Windows.Automation.AutomationProperties.NameProperty,
            view.Title + ". " + PlayerSessionText.Text + ". " + PlayerStateText.Text);
        ApplyPlayerHelpTextToControl();
    }

    /// <summary>
    /// CZAS grupy Sonos dla polecen elapsed/remaining/total. Bez tego polecenia
    /// czasu spadaly do ogolnego routera i czytaly DemoMediaSession, czyli
    /// pozycje 0 z dlugosci 0 - dane, ktorych Sonos nigdy nie zglosil.
    /// BRAK pozycji albo dlugosci to BRAK INFORMACJI, nigdy zero.
    /// </summary>
    private void AnnounceSonosTime(string commandId)
    {
        var position = SonosPlayerPosition.Resolve(
            _sonosPlayback,
            _sonosMetadata?.CurrentTrack?.DurationMillis,
            _sonosReadUtc,
            DateTime.UtcNow);
        var stale = position.Stale ? ", z ostatniego odczytu" : string.Empty;
        if (commandId == CommandIds.TimeElapsed)
        {
            Announce(position.Position is { } elapsed
                ? "Czas od początku: " + FormatSonosTime(elapsed) + stale
                : "Czas od początku nie jest znany");
            return;
        }

        if (commandId == CommandIds.TimeRemaining)
        {
            if (position.Position is not { } current || position.Duration is not { } total)
            {
                Announce("Czas pozostały nie jest znany");
                return;
            }

            var remaining = current >= total ? TimeSpan.Zero : total - current;
            Announce("Czas pozostały: " + FormatSonosTime(remaining) + stale);
            return;
        }

        Announce(position.Duration is { } duration
            ? "Czas całkowity: " + FormatSonosTime(duration)
            : "Czas całkowity nie jest znany");
    }

    /// <summary>
    /// TEN SAM blad co w opisie pozycji w Core: wzorzec "h\:mm\:ss" czyta
    /// KOMPONENT godzin (0-23), wiec 25 h wracalo jako 1:00:00. Polecenia czasu
    /// maja podawac CALY czas, wiec delegujemy do istniejacego wspolnego
    /// <see cref="AccessibleMediaController.Core.Presentation.MediaItemFormatter.FormatDuration"/>.
    /// </summary>
    private static string FormatSonosTime(TimeSpan value) =>
        AccessibleMediaController.Core.Presentation.MediaItemFormatter.FormatDuration(value);


    /// <summary>
    /// INSTRUKCJA po rzeczywistej zmianie konta. L4 z przegladu 680: poprzednie
    /// brzmienie kazalo "wejść do sesji Sonos", a uzytkownik JUZ w niej byl -
    /// powtorne wybranie tej samej sesji nie przechodzi przez galaz zmiany sesji,
    /// wiec nic sie nie odswiezalo. Tu opisujemy ZMIERZONA dzialajaca droge.
    /// Pelne odswiezanie w miejscu nalezy do zakresu B2c.
    /// </summary>
    internal const string SonosAccountChangedInstruction =
        "Konto Sonos się zmieniło. Przejdź do innej sesji i wróć do sesji Sonos, "
        + "a potem wybierz grupę na nowo";

    /// <summary>
    /// PODSTAWOWE polecenia sesji Sonos ISTNIEJACA droga ExecuteCommand. Sposob
    /// mapowania przepisany z WiiM (te same identyfikatory, te same skroty), ale
    /// nie jego HTTP: tu ida wylacznie polecenia Control API grupy.
    /// </summary>
    internal async Task ExecuteSonosCommandAsync(string commandId)
    {
        // GRANICA konta PRZED poleceniem: stary groupId nie ma prawa pojsc przez
        // NOWE konto tylko dlatego, ze nastepny tick jeszcze nie odswiezyl UI.
        if (ApplySonosAccountBinding())
        {
            Announce(SonosAccountChangedInstruction);
            return;
        }

        if (SonosActiveGroup is not { } group)
        {
            Announce("Nie ma aktywnej grupy Sonos. Wybierz grupę na liście i potwierdź Enterem");
            return;
        }

        // JEDEN przelot naraz: jawna odmowa zamiast cichej kolejki.
        if (_sonosCommandInFlight)
        {
            Announce("Poprzednie polecenie Sonos jeszcze się nie zakończyło");
            return;
        }

        var state = _sonosPlayback?.PlaybackState ?? SonosPlaybackState.Unknown;
        var gate = SonosCommandGating.Evaluate(
            commandId,
            state,
            _sonosPlayback?.AvailablePlaybackActions,
            _sonosVolume);
        if (!gate.Allowed)
        {
            // Odmowa konczy droge: zaden POST nie idzie.
            Announce(gate.Refusal ?? "To polecenie nie jest dostępne w sesji Sonos");
            return;
        }

        var ticket = _sonosTargetTicket;
        // WLASNY bilet bramki: zmiana grupy podnosi bilet CELU, wiec on nie moze
        // decydowac o zwolnieniu bramki. Inaczej po zmianie celu spoznione
        // finally nie rozpoznawalo sie jako wlasciciel, bramka zostawala
        // zamknieta i nastepne polecenie nie doszlo do backendu.
        var gateTicket = ++_sonosCommandGateTicket;
        var backend = EnsureSonosBackend();
        var token = EnsureSonosCancellation().Token;
        var beforeState = state;
        var beforeItemId = _sonosPlayback?.ItemId;
        var beforeVolume = _sonosVolume;
        _sonosCommandInFlight = true;
        try
        {
            SonosGroupCommandResult result;
            int? requestedVolume = null;
            bool? requestedMute = null;
            switch (commandId)
            {
                case CommandIds.PlayPause:
                case CommandIds.ActivateSelected:
                    result = await backend.SendGroupCommandAsync(
                        group.Id, SonosGroupCommand.TogglePlayPause, token).ConfigureAwait(true);
                    break;
                case CommandIds.Next:
                    result = await backend.SendGroupCommandAsync(
                        group.Id, SonosGroupCommand.SkipToNextTrack, token).ConfigureAwait(true);
                    break;
                case CommandIds.Previous:
                    result = await backend.SendGroupCommandAsync(
                        group.Id, SonosGroupCommand.SkipToPreviousTrack, token).ConfigureAwait(true);
                    break;
                case CommandIds.ToggleMuteCurrentSession:
                    // Bramka przepuscila, wiec wyciszenie jest ZNANE: to nie jest
                    // zgadniety bool, tylko odwrotnosc ODCZYTU.
                    requestedMute = !(beforeVolume?.Muted ?? false);
                    result = await backend.SetGroupMuteAsync(
                        group.Id, requestedMute.Value, token).ConfigureAwait(true);
                    break;
                case CommandIds.VolumeUp5:
                case CommandIds.VolumeDown5:
                case CommandIds.VolumeUp1:
                case CommandIds.VolumeDown1:
                {
                    var delta = commandId switch
                    {
                        CommandIds.VolumeUp5 => 5,
                        CommandIds.VolumeDown5 => -5,
                        CommandIds.VolumeUp1 => 1,
                        _ => -1
                    };
                    requestedVolume = Math.Clamp((beforeVolume?.Volume ?? 0) + delta, 0, 100);
                    result = await backend.SetGroupVolumeAsync(
                        group.Id, requestedVolume.Value, token).ConfigureAwait(true);
                    break;
                }

                default:
                {
                    if (!SonosCommandGating.IsSeek(commandId))
                    {
                        Announce("To polecenie nie jest obsługiwane w sesji Sonos");
                        return;
                    }

                    var seconds = commandId switch
                    {
                        CommandIds.SeekBackward10 => -10,
                        CommandIds.SeekForward10 => 10,
                        CommandIds.SeekBackward30 => -30,
                        CommandIds.SeekForward30 => 30,
                        CommandIds.SeekBackward60 => -60,
                        CommandIds.SeekForward60 => 60,
                        // CUSTOM bierze dlugosc Z KONFIGURACJI przez TA SAMA
                        // regule normalizacji, ktorej uzywa CommandRouter dla
                        // pozostalych sesji - inaczej opcja w ustawieniach
                        // klamalaby akurat w Sonosie. Zadnego nowego klawisza.
                        CommandIds.SeekBackwardCustom =>
                            -PlaybackSeekRules.NormalizeCustomSeekSeconds(_state.Settings.CustomSeekSeconds),
                        CommandIds.SeekForwardCustom =>
                            PlaybackSeekRules.NormalizeCustomSeekSeconds(_state.Settings.CustomSeekSeconds),
                        _ => 0
                    };

                    int deltaMillis;
                    var seekItemId = beforeItemId;
                    if (seconds != 0)
                    {
                        // WZGLEDNE przewijanie nie potrzebuje naszej pozycji:
                        // delte liczy sam Sonos od swojego biezacego miejsca.
                        deltaMillis = seconds * 1000;
                    }
                    else if (CommandIds.TryParseSeekPercent(commandId, out var percent))
                    {
                        // CYFRY to cel BEZWZGLEDNY, wiec bez naszej pozycji i
                        // dlugosci nie ma z czego policzyc delty. Odczyt MUSI byc
                        // swiezy: stan z wejscia do odtwarzacza moze byc stary o
                        // cale minuty, a wtedy skok trafilby w inne miejsce.
                        var resolved = await ResolveSonosPercentSeekAsync(
                            percent, group, ticket).ConfigureAwait(true);
                        if (resolved is not { } plan) return;
                        deltaMillis = plan.DeltaMillis;
                        seekItemId = plan.ItemId;
                    }
                    else
                    {
                        Announce("Ten rodzaj przewijania nie jest obsługiwany w sesji Sonos");
                        return;
                    }

                    result = await backend.SeekRelativeAsync(
                        group.Id, deltaMillis, seekItemId, token).ConfigureAwait(true);
                    break;
                }
            }

            if (ticket != _sonosTargetTicket || _isClosing) return;

            // Accepted nie znaczy wykonane. Zawsze robimy JAWNY odczyt w rodzaju
            // polecenia i mowimy tylko to, co odczyt potwierdzil. Zadnego
            // ponowienia POST - nawet po 401.
            var accepted = result.Status == SonosGroupOperationStatus.Attempted
                && result.Outcome?.Status == SonosControlApiStatus.Success;
            // Wyjatek odczytu po obsludze polecenia wymaga wyjasnienia wyniku.
            // Sama automatyczna zapowiedz zmienionej nazwy przycisku nie wyjasnia
            // nieudanej operacji. Bez ponowienia polecenia i bez drugiego odczytu;
            // tresc wyjatku (np. adres lub naglowek autoryzacji) nie idzie do mowy.
            bool readOk;
            try
            {
                readOk = await ReadSonosGroupStateAsync().ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                // Nasze wlasne zamykanie albo zmiana celu: CISZA.
                return;
            }
            catch (Exception exception)
            {
                LogSonosSeekFailure("odczyt potwierdzajacy polecenie", exception);
                // CISZA po PORZUCENIU celu: miedzy naszym await a tym miejscem
                // uzytkownik mogl wyjsc z sesji albo zmienic grupe, a wtedy
                // komunikat odezwalby sie w CUDZYM widoku.
                if (_isClosing || ticket != _sonosTargetTicket) return;
                // OGOLNIE o POLECENIU, bo ten wspolny blok konczy takze skip,
                // glosnosc i wyciszenie - nie wmawiamy im skoku. Rozroznienie z
                // kontraktu SonosGroupCommandResult: RequestSent=false to ZERO
                // prob wyslania, wiec "wyslano" byloby klamstwem; true to tylko
                // PODJETA PROBA - nie dowod, ze zadanie opuscilo maszyne ani ze
                // dotarlo do glosnika.
                Announce(result.RequestSent
                    ? "Podjęto próbę wykonania polecenia, ale nie udało się odczytać stanu Sonosa, "
                        + "więc nie ma potwierdzenia jego wyniku"
                    : "Polecenie nie zostało wysłane, a odczytu stanu Sonosa też nie udało się wykonać");
                return;
            }
            if (ticket != _sonosTargetTicket || _isClosing) return;

            var verdict = requestedVolume is not null || requestedMute is not null
                ? SonosCommandVerdict.DescribeVolume(
                    accepted, readOk, beforeVolume, _sonosVolume, requestedVolume, requestedMute)
                : SonosCommandVerdict.Describe(
                    ResolveSonosVerdictCommand(commandId),
                    accepted,
                    readOk,
                    beforeState,
                    _sonosPlayback?.PlaybackState ?? SonosPlaybackState.Unknown,
                    beforeItemId,
                    _sonosPlayback?.ItemId);
            Announce(verdict.Text);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            ReleaseSonosCommandGate(gateTicket);
        }
    }

    /// <summary>
    /// Zamiana cyfry (procentu 0-90) na DELTE dla istniejacego
    /// <c>SeekRelativeAsync</c>. Zwraca <c>null</c>, gdy skok NIE ma sie odbyc -
    /// i wtedy odmowa jest juz powiedziana (albo swiadomie przemilczana, gdy cel
    /// zostal porzucony). Te same bezpieczniki co na drodze dialogu procentowego:
    /// SWIEZY odczyt, TEN SAM material, SWIEZA bramka CanSeek, zero ponowien.
    /// </summary>
    private async Task<(int DeltaMillis, string? ItemId)?> ResolveSonosPercentSeekAsync(
        int percent, SonosGroup group, long ticket)
    {
        var itemBefore = _sonosPlayback?.ItemId;
        bool readOk;
        try
        {
            readOk = await ReadSonosGroupStateAsync().ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // Nasze wlasne zamykanie albo zmiana celu: CISZA, zeby odmowa nie
            // odezwala sie w cudzym widoku.
            return null;
        }
        catch (Exception exception)
        {
            LogSonosSeekFailure("odczyt przed skokiem procentowym", exception);
            if (_isClosing || ticket != _sonosTargetTicket) return null;
            Announce("Skok pominięty: nie udało się odczytać aktualnej pozycji Sonos, "
                + "skok nie został wysłany");
            return null;
        }

        if (_isClosing || ticket != _sonosTargetTicket) return null;
        if (!readOk)
        {
            Announce("Skok pominięty: nie udało się odczytać aktualnej pozycji Sonos, "
                + "skok nie został wysłany");
            return null;
        }

        // GRUPA mogla sie zmienic w czasie odczytu: bezwzgledny cel policzony dla
        // salonu nie moze poleciec do kuchni.
        if (SonosActiveGroup is not { } groupNow
            || !string.Equals(groupNow.Id, group.Id, StringComparison.Ordinal))
        {
            return null;
        }

        var itemNow = _sonosPlayback?.ItemId;
        if (!string.Equals(itemBefore, itemNow, StringComparison.Ordinal))
        {
            Announce("Skok pominięty: Sonos zmienił odtwarzany materiał");
            return null;
        }

        // SWIEZA bramka: Sonos moze przestac zglaszac CanSeek miedzy wejsciem a
        // tym odczytem, a rownosc ItemId tego nie wychwytuje.
        if (!EvaluateSonosSeekGate()) return null;

        var resolved = SonosPlayerPosition.Resolve(
            _sonosPlayback, _sonosMetadata?.CurrentTrack?.DurationMillis, _sonosReadUtc, DateTime.UtcNow);
        if (resolved.Position is not { } from)
        {
            Announce("Skok pominięty: Sonos nie podał aktualnej pozycji");
            return null;
        }
        if (resolved.Duration is not { } total || total <= TimeSpan.Zero)
        {
            // Bez dlugosci procent nie ma do czego sie odniesc - i na pewno nie do
            // zera z sesji demonstracyjnej.
            Announce("Skok pominięty: Sonos nie podał długości materiału, "
                + "więc nie ma od czego liczyć procentu");
            return null;
        }

        var target = TimeSpan.FromTicks((long)Math.Round(total.Ticks * (percent / 100d)));
        return ((int)Math.Round((target - from).TotalMilliseconds), itemNow);
    }

    /// <summary>
    /// Zwolnienie bramki polecenia przez WLASCICIELA. Kryterium jest bilet
    /// BRAMKI, nie celu: po zmianie grupy bilet celu juz nie pasuje, a bramka i
    /// tak musi zostac zwolniona. Spozniony przelot z nieaktualnym biletem NIE
    /// zwalnia bramki nalezacej do nowszego polecenia.
    /// </summary>
    private void ReleaseSonosCommandGate(int gateTicket)
    {
        if (gateTicket == _sonosCommandGateTicket) _sonosCommandInFlight = false;
    }

    /// <summary>
    /// SKOK DO POZYCJI w grupie Sonos: czas albo procent. Ogolna droga
    /// <c>ShowSeekPositionDialog</c> czytala <c>_sessions.Current.CurrentItem.Duration</c>,
    /// a wiersz grupy powstaje jako <c>MediaItemKind.Device</c> BEZ dlugosci,
    /// wiec ZAWSZE odmawiala "czas trwania jest nieznany" - mimo ze odczyt grupy
    /// zna i pozycje, i dlugosc. Tutaj dlugosc i pozycja pochodza WYLACZNIE z
    /// odczytu Sonosa, a sam skok idzie ISTNIEJACA operacja
    /// <see cref="ISonosGroupSessionBackend.SeekRelativeAsync"/> - backend nie ma
    /// skoku absolutnego i nie dodajemy mu zadnego endpointu.
    /// </summary>
    private async Task SeekSonosToPositionAsync(string commandId)
    {
        if (!_playerViewActive)
        {
            Announce("Skok jest dostępny tylko w odtwarzaczu. Naciśnij F6");
            return;
        }
        if (SonosActiveGroup is not { } group)
        {
            Announce("Nie ma aktywnej grupy Sonos. Wybierz grupę na liście i potwierdź Enterem");
            return;
        }
        if (_sonosCommandInFlight)
        {
            Announce("Poprzednie polecenie Sonos jeszcze się nie zakończyło");
            return;
        }

        var byTime = commandId == CommandIds.SeekToTime;
        // TA SAMA bramka, co reszta przewijania: CanSeek z ODCZYTANYCH akcji.
        if (!EvaluateSonosSeekGate()) return;

        // DLUGOSC z odczytu grupy. Bez niej okno nie ma czego celowac, a zero
        // bylo BY wymyslone - wiec odmawiamy JAWNIE i nie wysylamy nic.
        var duration = SonosPlayerPosition
            .Resolve(_sonosPlayback, _sonosMetadata?.CurrentTrack?.DurationMillis, _sonosReadUtc, DateTime.UtcNow)
            .Duration;
        if (duration is not { } total || total <= TimeSpan.Zero)
        {
            Announce(byTime
                ? "Skok do czasu niedostępny: Sonos nie podał czasu trwania"
                : "Skok procentowy niedostępny: Sonos nie podał czasu trwania");
            return;
        }

        // TOZSAMOSC celu i materialu SPRZED modalu. Modal trwa dowolnie dlugo,
        // wiec po nim sprawdzamy to jeszcze raz - skok nie ma prawa trafic w
        // nowy cel ani w inny material.
        var ticketBefore = _sonosTargetTicket;
        var itemBefore = _sonosPlayback?.ItemId;
        // FOKUS SPRZED modalu, ZMIERZONY a nie zgadniety: Keyboard.FocusedElement
        // jest tym, z czego skok naprawde wyszedl (przycisk trybu przy klikniecu,
        // dowolny element odtwarzacza przy skrocie). Staly kandydat "przycisk
        // czasu" przenosil czytnik na obcy przycisk po Ctrl+Shift+J i po Escape.
        var focusBefore = Keyboard.FocusedElement as FrameworkElement;
        // REZERWACJA bramki PRZED pierwszym await tej drogi: w czasie
        // przedskokowego GET-a okno glowne jest w pelni interaktywne (modal nie
        // blokuje kolejki Dispatchera), a przy busy=false rownolegle polecenie
        // szlo do backendu i delta liczyla sie z pozycji sprzed cudzego POST-u.
        // Modal jest w tej rezerwacji, bo odmowa zajetosci w jego czasie jest
        // tym samym, czym po nim: jednym poleceniem naraz.
        var gateTicket = ++_sonosCommandGateTicket;
        _sonosCommandInFlight = true;
        try
        {
            var dialog = new SeekPositionWindow(
                byTime ? SeekInputMode.Time : SeekInputMode.Percentage, total) { Owner = this };
            var confirmed = dialog.ShowDialog() == true;
            // Fokus wraca do odtwarzacza NIEZALEZNIE od decyzji - takze po Escape.
            FocusSonosPlayerAfterSeek(focusBefore, byTime);
            if (!confirmed) return;
            if (_isClosing || ticketBefore != _sonosTargetTicket) return;
            if (SonosActiveGroup is not { } stillGroup
                || !string.Equals(stillGroup.Id, group.Id, StringComparison.Ordinal))
            {
                Announce("Skok pominięty: grupa Sonos zmieniła się w czasie wpisywania");
                return;
            }

            var target = byTime
                ? dialog.Position
                : TimeSpan.FromTicks((long)Math.Round(total.Ticks * (dialog.Percentage / 100d)));

            // AKTUALNA pozycja, nie ta z chwili otwarcia okna: po dlugim modalu
            // liczenie delty od starego miejsca trafiloby gdzie indziej.
            // WYJATEK transportu tego odczytu MUSI byc nazwany: wczesniej await
            // stal poza try, a wywolanie jest fire-and-forget, wiec timeout po
            // Enterze konczyl sie CISZA i uzytkownik nie wiedzial, ze skok nie
            // poszedl. Tresci wyjatku NIE powtarzamy - moze zawierac adres i
            // naglowek autoryzacji.
            bool readOkBefore;
            try
            {
                readOkBefore = await ReadSonosGroupStateAsync().ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                // Anulowanie to nasze wlasne zamykanie albo zmiana celu: nie
                // wchodzimy z tym w cudzy widok.
                return;
            }
            catch (Exception exception)
            {
                LogSonosSeekFailure("odczyt przed skokiem", exception);
                // CISZA po PORZUCENIU celu: miedzy naszym await a tym miejscem
                // uzytkownik mogl wyjsc z sesji albo zmienic grupe, a wtedy
                // odmowa skoku Sonosa odezwalaby sie w CUDZYM widoku.
                if (_isClosing || ticketBefore != _sonosTargetTicket) return;
                Announce("Skok pominięty: nie udało się odczytać aktualnej pozycji Sonos, "
                    + "skok nie został wysłany");
                return;
            }
            if (!readOkBefore)
            {
                // TA SAMA regula: false z ReadSonosGroupStateAsync znaczy TAKZE
                // anulowanie i nieaktualny cel, wiec najpierw aktualnosc.
                if (_isClosing || ticketBefore != _sonosTargetTicket) return;
                Announce("Skok pominięty: nie udało się odczytać aktualnej pozycji Sonos, "
                    + "skok nie został wysłany");
                return;
            }
            if (_isClosing || ticketBefore != _sonosTargetTicket) return;
            var itemNow = _sonosPlayback?.ItemId;
            if (!string.Equals(itemBefore, itemNow, StringComparison.Ordinal))
            {
                Announce("Skok pominięty: Sonos zmienił odtwarzany materiał");
                return;
            }
            // SWIEZA bramka dla TEGO SAMEGO materialu: Sonos moze przestac
            // zglaszac CanSeek w czasie modalu, a rownosc ItemId tego nie
            // wychwytuje. Odczyt jest juz zrobiony, wiec to zero dodatkowego
            // ruchu - tylko ocena flagi, ktora wlasnie przyszla.
            if (!EvaluateSonosSeekGate()) return;
            var current = SonosPlayerPosition
                .Resolve(_sonosPlayback, _sonosMetadata?.CurrentTrack?.DurationMillis, _sonosReadUtc, DateTime.UtcNow)
                .Position;
            if (current is not { } from)
            {
                Announce("Skok pominięty: Sonos nie podał aktualnej pozycji");
                return;
            }

            var deltaMillis = (int)Math.Round((target - from).TotalMilliseconds);
            var backend = EnsureSonosBackend();
            var token = EnsureSonosCancellation().Token;
            var beforeState = _sonosPlayback?.PlaybackState ?? SonosPlaybackState.Unknown;

            // DOKLADNIE JEDNO zadanie skoku, bez ponowien.
            var result = await backend
                .SeekRelativeAsync(stillGroup.Id, deltaMillis, itemNow, token).ConfigureAwait(true);
            if (ticketBefore != _sonosTargetTicket || _isClosing) return;

            var accepted = result.Status == SonosGroupOperationStatus.Attempted
                && result.Outcome?.Status == SonosControlApiStatus.Success;
            // JAWNY odczyt PO skoku: Accepted bez zmiany odczytu NIE jest dowodem
            // trafionej pozycji, wiec werdykt zostaje dotychczasowy i uczciwy.
            bool readOk;
            try
            {
                readOk = await ReadSonosGroupStateAsync().ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                LogSonosSeekFailure("odczyt po skoku", exception);
                if (_isClosing || ticketBefore != _sonosTargetTicket) return;
                // UCZCIWE rozroznienie z kontraktu SonosGroupCommandResult:
                // RequestSent=false to ZERO prob wyslania, wiec "wyslano"
                // byloby klamstwem; true to tylko PODJETA PROBA - nie dowod,
                // ze zadanie opuscilo maszyne ani ze dotarlo do glosnika.
                Announce(result.RequestSent
                    ? "Podjęto próbę skoku, ale nie udało się odczytać stanu Sonosa, "
                        + "więc nie ma potwierdzenia, czy pozycja się zmieniła"
                    : "Skok nie został wysłany, a odczytu stanu Sonosa też nie udało się wykonać");
                return;
            }
            if (ticketBefore != _sonosTargetTicket || _isClosing) return;
            var verdict = SonosCommandVerdict.Describe(
                SonosVerdictCommand.Seek,
                accepted,
                readOk,
                beforeState,
                _sonosPlayback?.PlaybackState ?? SonosPlaybackState.Unknown,
                itemNow,
                _sonosPlayback?.ItemId);
            Announce(verdict.Text);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            // KAZDE wyjscie - odmowa, anulowanie i wyjatek tez - zwalnia WLASNY
            // bilet. Cudzego nie tkniemy: warunek jest w ReleaseSonosCommandGate.
            ReleaseSonosCommandGate(gateTicket);
        }
    }

    /// <summary>
    /// TA SAMA bramka <c>CanSeek</c>, co reszta przewijania, oceniana na AKTUALNIE
    /// ODCZYTANYCH akcjach. Wydzielona, bo skok ocenia ja DWA razy: przed modalem
    /// i po swiezym odczycie przed wyslaniem - Sonos moze odebrac przewijanie w
    /// czasie modalu dla TEGO SAMEGO materialu.
    /// </summary>
    private bool EvaluateSonosSeekGate()
    {
        var gate = SonosCommandGating.Evaluate(
            CommandIds.SeekForward10,
            _sonosPlayback?.PlaybackState ?? SonosPlaybackState.Unknown,
            _sonosPlayback?.AvailablePlaybackActions,
            _sonosVolume);
        if (gate.Allowed) return true;
        Announce(gate.Refusal ?? "To polecenie nie jest dostępne w sesji Sonos");
        return false;
    }

    /// <summary>
    /// Zapis przyczyny nieudanego odczytu przy skoku BEZ tresci wyjatku w mowie:
    /// komunikat dla uzytkownika nazywa skutek, a szczegoly (mogace zawierac
    /// adres i naglowek autoryzacji) NIE ida do mowy. Kanalem jest tu
    /// <c>Debug.WriteLine</c>, wiec w kompilacji Release ten zapis NIE powstaje
    /// - zadnego trwalego dziennika to nie tworzy.
    /// </summary>
    private static void LogSonosSeekFailure(string stage, Exception exception) =>
        System.Diagnostics.Debug.WriteLine(
            $"Sonos: skok do pozycji - {stage} nie udal sie ({exception.GetType().Name}).");

    /// <summary>
    /// Fokus po zamknieciu okna skoku wraca tam, SKAD skok wyszedl: do elementu,
    /// ktory ZMIERZONO jako ogniskowany przed otwarciem modalu (przycisk trybu
    /// przy klikniecu, dowolny element odtwarzacza przy skrocie). Dopiero gdy ten
    /// element jest juz niewidoczny albo nieogniskowalny, siegamy po przycisk
    /// trybu, a na koniec po panel odtwarzacza. Wczesniej kolejnosc byla stala i
    /// zawsze celowala w przycisk czasu, wiec Ctrl+Shift+J i Escape przenosily
    /// czytnik na obcy przycisk.
    /// </summary>
    private void FocusSonosPlayerAfterSeek(FrameworkElement? focusBefore, bool byTime = true)
    {
        if (!_playerViewActive) return;
        // Nie kradniemy fokusu obcemu procesowi ani innej sesji: przywracamy go
        // tylko gdy nasze okno jest aktywne ALBO fokus klawiatury jest wciaz w
        // nim. Po zamknieciu modalu WPF oddaje fokus wlascicielowi, wiec zwykla
        // droga to spelnia; przelaczenie na obca aplikacje - nie.
        if (!IsActive && !IsKeyboardFocusWithin) return;
        var preferred = byTime ? PlayerSeekTimeButton : PlayerSeekPercentButton;
        foreach (var candidate in new FrameworkElement?[]
                 { focusBefore, preferred, PlayerPlayPauseButton, PlayerPanel })
        {
            if (candidate is { IsVisible: true, Focusable: true } && candidate.Focus()) return;
        }
    }

    /// <summary>WASKI hook pomiarowy: skok do pozycji PRAWDZIWA droga sesji Sonos.</summary>
    internal Task SeekSonosToPositionForTests(string commandId) => SeekSonosToPositionAsync(commandId);

    /// <summary>
    /// WASKI hook pomiarowy: ta sama PRODUKCYJNA droga zwolnienia bramki, zeby
    /// pomiar mogl sprawdzic spozniony przelot bez wlasnej kopii warunku.
    /// </summary>
    internal void ReleaseSonosCommandGateForTests(int gateTicket) => ReleaseSonosCommandGate(gateTicket);

    /// <summary>WASKI hook pomiarowy: polecenie PRAWDZIWA droga sesji Sonos.</summary>
    internal Task ExecuteSonosCommandForTests(string commandId) => ExecuteSonosCommandAsync(commandId);

    /// <summary>WASKI hook pomiarowy: aktywacja grupy PRAWDZIWA droga.</summary>
    internal Task ActivateSonosGroupForTests(string groupId) => ActivateSonosGroupAsync(groupId);

    /// <summary>
    /// WASKI hook pomiarowy: PRODUKCYJNA droga Entera na wierszu grupy, czyli
    /// aktywacja grupy i istniejacy widok odtwarzacza. Pomiar kontekstu Ctrl+F5
    /// w odtwarzaczu nie ma dzieki temu wlasnej kopii tej kolejnosci ani nie
    /// ustawia prywatnej flagi widoku.
    /// </summary>
    internal Task ActivateSonosGroupThenShowPlayerForTests(string groupId) =>
        ActivateSonosGroupThenShowPlayerAsync(groupId);

    /// <summary>
    /// OSTATNIA rozpoczeta aktywacja grupy Sonos. WASKA obserwowalnosc dla
    /// pomiaru: test moze poczekac na RZECZYWISTE zakonczenie zadania zamiast
    /// pompowac stala liczbe milisekund. Nic w logice produkcyjnej tego nie
    /// czyta i nie czeka na to zadanie.
    /// </summary>
    internal Task? LastSonosActivationTaskForTests { get; private set; }

    /// <summary>
    /// Enter na liscie: grupa aktywna, a POTEM istniejacy wzorzec widoku
    /// odtwarzacza. Odtwarzacz otwiera sie nawet gdy odczyt nie dal danych -
    /// pokazuje wtedy "Brak informacji", a nie wymyslony stan.
    /// </summary>
    private async Task ActivateSonosGroupThenShowPlayerAsync(string groupId)
    {
        var ticket = await ActivateSonosGroupAsync(groupId).ConfigureAwait(true);
        if (_isClosing || ticket is not { } issued) return;

        // SPOZNIONA aktywacja nie moze otworzyc odtwarzacza. Miedzy naszym
        // await a tym miejscem uzytkownik mogl SWIADOMIE wyjsc z sesji Sonos
        // albo wybrac inna grupe. Sam SonosActiveGroup tego NIE wykrywa:
        // po wyjsciu z sesji zapamietany wybor grupy dalej sie rozwiazuje,
        // wiec porzucony cel ukradlby fokus obcej sesji.
        if (issued != _sonosTargetTicket) return;
        if (_sessions is null || !IsSonosSession(_sessions.Current.Id)) return;
        if (SonosActiveGroup is not { } group
            || !string.Equals(group.Id, groupId, StringComparison.Ordinal))
        {
            return;
        }

        var session = _sessions.FindSession(SonosSessionId);
        var row = session?.Items.FirstOrDefault(item =>
            string.Equals(item.Id, group.Id, StringComparison.Ordinal));
        if (session is not null && row is not null) session.SelectItem(row);
        _playerFocusContextPrefix = "Wybrano grupę " + group.Name;
        ShowPlayerView();
    }

    /// <summary>
    /// Wejscie wolajacych: zapamietuje ZADANIE aktywacji, zeby pomiar mogl
    /// poczekac na jego rzeczywiste zakonczenie. Logika pozostaje "fire and
    /// forget" - nikt tego zadania nie awaituje w produkcji.
    /// </summary>
    private void StartSonosGroupActivationThenPlayer(string groupId) =>
        LastSonosActivationTaskForTests = ActivateSonosGroupThenShowPlayerAsync(groupId);

    private static SonosVerdictCommand ResolveSonosVerdictCommand(string commandId) => commandId switch
    {
        CommandIds.Next => SonosVerdictCommand.Next,
        CommandIds.Previous => SonosVerdictCommand.Previous,
        CommandIds.PlayPause or CommandIds.ActivateSelected => SonosVerdictCommand.Toggle,
        _ => SonosVerdictCommand.Seek
    };

    /// <summary>
    /// OSZCZEDNY, POJEDYNCZY odczyt w tle dla uzywanej grupy. Nie nakladamy
    /// pollow: gdy polecenie jest w locie albo termin nie minal, nic sie nie
    /// dzieje. Nic tu nie mowi do czytnika i nic nie zabiera fokusu.
    /// </summary>
    internal async Task PollSonosGroupIfDueAsync(DateTime nowUtc)
    {
        if (_isClosing
            || _sonosCommandInFlight
            || !IsSonosSession(_sessions.Current.Id)
            || SonosActiveGroup is null
            || nowUtc < _sonosNextBackgroundReadUtc)
        {
            return;
        }

        // PRAWDZIWA bariera, nie sam termin: dopoki poprzedni odczyt tla nie
        // domknal sie w CALOSCI (stan + metadane + glosnosc), nie wysylamy
        // drugiego GET-u. Bez tego wolna odpowiedz mnozyla ruch przy kazdym
        // tyknieciu licznika.
        if (_sonosBackgroundRead is { IsCompleted: false }) return;

        var read = ReadSonosGroupStateAsync();
        _sonosBackgroundRead = read;
        try
        {
            await read.ConfigureAwait(true);
        }
        finally
        {
            // Zwalniamy WLASNA bariere: nowszego odczytu nie ruszamy.
            if (ReferenceEquals(_sonosBackgroundRead, read)) _sonosBackgroundRead = null;
        }
    }

    /// <summary>
    /// Wejscie PRAWDZIWEGO licznika odtwarzacza. Zadanie jest ZAPAMIETANE, zeby
    /// pomiar mogl poczekac na jego rzeczywiste zakonczenie i ZOBACZYC wyjatek;
    /// zaden wyjatek nie ginie cicho w "fire and forget".
    /// </summary>
    private void PollSonosGroupFromPlayerTimer(DateTime nowUtc) =>
        LastSonosBackgroundPollTaskForTests = PollSonosGroupIfDueAsync(nowUtc);

    /// <summary>
    /// OSTATNI rozpoczety odczyt w tle. WASKA obserwowalnosc dla pomiaru;
    /// domyslnie neutralna - nic w produkcji tego nie czyta i nie awaituje.
    /// </summary>
    internal Task? LastSonosBackgroundPollTaskForTests { get; private set; }

    private CancellationTokenSource EnsureSonosCancellation() =>
        _sonosCancellation ??= new CancellationTokenSource();

    /// <summary>
    /// Wyjscie z sesji albo zamkniecie: uniewazniamy WLASNE oczekujace wyniki i
    /// koniec. Zadnego POST - nie zatrzymujemy muzyki i nie obiecujemy cofniecia
    /// polecen, ktore JUZ poszly.
    /// </summary>
    internal void CancelSonosPendingWork()
    {
        _sonosTargetTicket++;
        // Bramke polecenia zwalniamy JAWNIE i uniewazniamy jej wlasciciela, zeby
        // spoznione finally starego przelotu nie zamknelo bramki nowszego.
        _sonosCommandGateTicket++;
        _sonosCommandInFlight = false;
        // TA SAMA regula dla bramki ODSWIEZANIA: porzucony przelot A traci
        // wlasnosc bramki, wiec powrot do sesji moze od razu zaczac nowe
        // odswiezenie B. Spoznione finally A widzi juz CUDZY bilet i nie
        // ruszy zajetosci B (patrz finally w RefreshSonosTopologyAsync).
        _sonosRefreshGateTicket++;
        _sonosRefreshInFlight = false;
        // TA SAMA regula dla bramki WYBORU DOMU. Bez tego porzucony przelot A
        // trzymal bramke do konca swojego odczytu, a powrot do sesji odbijal sie
        // od "juz trwa" zamiast zaczac NOWY GET. Spoznione finally A widzi juz
        // CUDZY bilet i nie ruszy zajetosci B (patrz finally w
        // ChooseSonosHouseholdAsync i SwitchSonosHouseholdAsync).
        _sonosHouseholdChoiceGateTicket++;
        _sonosHouseholdChoiceInFlight = false;
        // TA SAMA regula dla bramki PODGLADU ULUBIONYCH. Bez tego porzucony
        // przelot A trzymal bramke do konca swojego odczytu, a powrot do sesji
        // odbijal sie od "juz trwa" zamiast zaczac NOWY GET. Spoznione finally A
        // widzi juz CUDZY bilet i nie ruszy zajetosci B (patrz finally w
        // ShowSonosFavoritesAsync).
        _sonosFavoritesGateTicket++;
        _sonosFavoritesInFlight = false;
        // Uniewazniamy tez WSZYSTKIE wyniki odczytow w locie i barierę tla:
        // po wyjsciu/zamknieciu zaden stary GET nie ma czego nadpisywac.
        _sonosReadSequence++;
        _sonosBackgroundRead = null;
        var cancellation = _sonosCancellation;
        _sonosCancellation = null;
        try
        {
            cancellation?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        cancellation?.Dispose();
    }
}

/// <summary>
/// PRODUKCYJNY adapter: sesja Sonos rozmawia z tym SAMYM, jedynym wlascicielem
/// konta. Nie ma tu wlasnego klienta HTTP, wlasnego magazynu ani zadnego gettera
/// tokenu - tylko przekazanie identyfikatora grupy.
/// </summary>
internal sealed class SonosAccountOwnerGroupBackend
    : ISonosGroupSessionBackend, ISonosAccountBoundBackend, ISonosFavoritesSessionBackend,
      ISonosFavoriteLoadSessionBackend, ISonosPlaylistsSessionBackend,
      ISonosPlaylistLoadSessionBackend, ISonosOwnStreamsSessionBackend,
      ISonosGroupMembershipSessionBackend
{
    private readonly SonosAccountOwner _owner;

    internal SonosAccountOwnerGroupBackend(SonosAccountOwner owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
    }

    /// <summary>
    /// BEZPIECZNA migawka konta albo <c>null</c>, gdy konta nie zainicjowano.
    /// Cienkie przekazanie do TEGO SAMEGO wlasciciela: zero tokenow, zero
    /// magazynu, zero sieci. <c>null</c> to NIEZNANE, nie odlaczenie.
    /// </summary>
    public SonosAccountSnapshot? AccountSnapshot => _owner.AccountSnapshot;

    public Task<SonosSessionCreateResult> CreateSessionAsync(string? groupId,
        SonosSessionRequest request, CancellationToken cancellationToken) =>
        _owner.CreateSessionAsync(groupId, request, cancellationToken);

    public Task<SonosStreamUrlLoadResult> LoadStreamUrlAsync(string? sessionId, string? streamUrl,
        bool playOnCompletion, string? itemId, CancellationToken cancellationToken) =>
        _owner.LoadStreamUrlAsync(sessionId, streamUrl, playOnCompletion, itemId, cancellationToken);


    public Task<SonosGroupReadResult<SonosGroupPlaybackStatus>> ReadGroupPlaybackAsync(
        string? groupId, CancellationToken cancellationToken) =>
        _owner.ReadGroupPlaybackAsync(groupId, cancellationToken);

    public Task<SonosGroupReadResult<SonosGroupMetadata>> ReadGroupMetadataAsync(
        string? groupId, CancellationToken cancellationToken) =>
        _owner.ReadGroupMetadataAsync(groupId, cancellationToken);

    public Task<SonosGroupReadResult<SonosGroupVolume>> ReadGroupVolumeAsync(
        string? groupId, CancellationToken cancellationToken) =>
        _owner.ReadGroupVolumeAsync(groupId, cancellationToken);

    public Task<SonosGroupCommandResult> SendGroupCommandAsync(
        string? groupId, SonosGroupCommand command, CancellationToken cancellationToken) =>
        _owner.SendGroupCommandAsync(groupId, command, cancellationToken);

    public Task<SonosGroupCommandResult> SeekRelativeAsync(
        string? groupId, int deltaMillis, string? itemId, CancellationToken cancellationToken) =>
        _owner.SeekRelativeAsync(groupId, deltaMillis, itemId, cancellationToken);

    public Task<SonosGroupCommandResult> SetGroupVolumeAsync(
        string? groupId, int volume, CancellationToken cancellationToken) =>
        _owner.SetGroupVolumeAsync(groupId, volume, cancellationToken);

    public Task<SonosGroupCommandResult> SetGroupMuteAsync(
        string? groupId, bool muted, CancellationToken cancellationToken) =>
        _owner.SetGroupMuteAsync(groupId, muted, cancellationToken);

    public Task<SonosHouseholdsReadResult> ReadHouseholdsAsync(CancellationToken cancellationToken) =>
        _owner.ReadHouseholdsAsync(cancellationToken);

    public Task<SonosGroupsReadResult> ReadGroupsAsync(
        string householdId, CancellationToken cancellationToken) =>
        _owner.ReadGroupsAsync(householdId, cancellationToken);

    /// <summary>ZMIANA SKLADU: createGroup. Cienkie przekazanie, jeden POST.</summary>
    public Task<SonosGroupMembershipResult> CreateGroupAsync(
        string? householdId, SonosCreateGroupRequest? request, CancellationToken cancellationToken) =>
        _owner.CreateGroupAsync(householdId, request, cancellationToken);

    /// <summary>ZMIANA SKLADU: setGroupMembers. Cienkie przekazanie, jeden POST.</summary>
    public Task<SonosGroupMembershipResult> SetGroupMembersAsync(
        string? groupId, SonosPlayerSet? players, CancellationToken cancellationToken) =>
        _owner.SetGroupMembersAsync(groupId, players, cancellationToken);

    /// <summary>
    /// F2: ODCZYT ULUBIONYCH domu. Tak samo cienkie przekazanie jak reszta: caly
    /// bilet, odnawianie i kontrola generacji siedza we wlascicielu i wspolnym
    /// koordynatorze. Tylko GET.
    /// </summary>
    public Task<SonosFavoritesReadResult> ReadFavoritesAsync(
        string? householdId, CancellationToken cancellationToken) =>
        _owner.ReadFavoritesAsync(householdId, cancellationToken);

    /// <summary>
    /// F3c: URUCHOMIENIE ULUBIONEGO w grupie. Tak samo cienkie przekazanie jak
    /// reszta: caly bilet, odnawianie, kontrola generacji i JEDYNY POST siedza we
    /// wlascicielu i wspolnym koordynatorze. Zadnego presetu i zadnego drugiego
    /// polecenia odtwarzania.
    /// </summary>
    public Task<SonosGroupCommandResult> LoadFavoriteAsync(
        string? groupId,
        string? favoriteId,
        SonosFavoriteQueueAction action,
        bool playOnCompletion,
        CancellationToken cancellationToken) =>
        _owner.LoadFavoriteAsync(groupId, favoriteId, action, playOnCompletion, cancellationToken);

    /// <summary>
    /// ODCZYT PLAYLIST domu. Tak samo cienkie przekazanie jak ulubione: caly
    /// bilet, odnawianie i kontrola generacji siedza we wlascicielu i wspolnym
    /// koordynatorze. Tylko GET.
    /// </summary>
    public Task<SonosPlaylistsReadResult> ReadPlaylistsAsync(
        string? householdId, CancellationToken cancellationToken) =>
        _owner.ReadPlaylistsAsync(householdId, cancellationToken);

    /// <summary>
    /// URUCHOMIENIE PLAYLISTY w grupie. Cienkie przekazanie - JEDYNY POST siedzi
    /// we wlascicielu i wspolnym koordynatorze. Zadnego presetu i zadnego
    /// drugiego polecenia odtwarzania.
    /// </summary>
    public Task<SonosGroupCommandResult> LoadPlaylistAsync(
        string? groupId,
        string? playlistId,
        SonosFavoriteQueueAction action,
        bool playOnCompletion,
        CancellationToken cancellationToken) =>
        _owner.LoadPlaylistAsync(groupId, playlistId, action, playOnCompletion, cancellationToken);
}

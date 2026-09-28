using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AccessibleMediaController.Core.Commands;
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

    /// <summary>JEDEN przelot polecenia naraz. Brak ukrytej kolejki.</summary>
    private bool _sonosCommandInFlight;

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
    /// JAWNE wejscie do sesji Sonos: wolno tu obudzic wlasciciela konta i
    /// odczytac domy oraz grupy. Wybor grupy NIE dotyka muzyki.
    /// </summary>
    internal async Task EnterSonosSessionAsync()
    {
        var ticket = _sonosTargetTicket;
        var backend = EnsureSonosBackend();
        var token = EnsureSonosCancellation().Token;
        try
        {
            if (_sonosHouseholds.Count == 0)
            {
                var households = await backend.ReadHouseholdsAsync(token).ConfigureAwait(true);
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
                _sonosEmptyReason = _sonosHouseholds.Count == 0
                    ? SonosSessionEmptyReason.NoAccount
                    : SonosSessionEmptyReason.NotRead;
                ApplySonosGroupRows();
                return;
            }

            var groups = await backend.ReadGroupsAsync(household.Id, token).ConfigureAwait(true);
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
                Artist = row.Text,
                Kind = MediaItemKind.Device
            })
            .ToList());
    }

    /// <summary>
    /// Enter na grupie: grupa staje sie AKTYWNA i otwiera sie odtwarzacz. Sam
    /// wybor NIE wysyla zadnego POST, wiec muzyka w pokoju sie nie zmienia.
    /// </summary>
    internal async Task ActivateSonosGroupAsync(string groupId)
    {
        if (SonosActiveGroupPolicy.Resolve(groupId, _sonosTopology) is not { } group)
        {
            Announce("Ta grupa Sonos już nie istnieje. Odśwież listę grup");
            return;
        }

        // Zmiana celu: stary bilet przestaje byc wazny, spoznione odpowiedzi
        // poprzedniej grupy nie nadpisza tej.
        _sonosTargetTicket++;
        _state.Sonos.SelectedGroupId = group.Id;
        _sonosPlayback = null;
        _sonosMetadata = null;
        _sonosVolume = null;
        _sonosNextBackgroundReadUtc = DateTime.MinValue;
        QueueStateSave(announceFailure: true);
        await ReadSonosGroupStateAsync().ConfigureAwait(true);
    }

    /// <summary>
    /// JAWNY odczyt stanu, metadanych i glosnosci aktywnej grupy. Nieudany
    /// odczyt NIE zostawia poprzednich danych jako biezacych - pola wracaja do
    /// braku informacji, bo stary tytul przy nowym utworze to klamstwo.
    /// </summary>
    internal async Task<bool> ReadSonosGroupStateAsync()
    {
        if (SonosActiveGroup is not { } group) return false;
        var ticket = _sonosTargetTicket;
        var backend = EnsureSonosBackend();
        var token = EnsureSonosCancellation().Token;
        try
        {
            var playback = await backend.ReadGroupPlaybackAsync(group.Id, token).ConfigureAwait(true);
            if (ticket != _sonosTargetTicket || _isClosing) return false;
            var metadata = await backend.ReadGroupMetadataAsync(group.Id, token).ConfigureAwait(true);
            if (ticket != _sonosTargetTicket || _isClosing) return false;
            var volume = await backend.ReadGroupVolumeAsync(group.Id, token).ConfigureAwait(true);
            if (ticket != _sonosTargetTicket || _isClosing) return false;

            _sonosPlayback = playback.Succeeded ? playback.Value : null;
            _sonosMetadata = metadata.Succeeded ? metadata.Value : null;
            _sonosVolume = volume.Succeeded ? volume.Value : null;
            _sonosReadUtc = DateTime.UtcNow;
            var ok = playback.Succeeded && volume.Succeeded;
            _sonosNextBackgroundReadUtc = _sonosReadUtc
                + (ok ? SonosBackgroundReadInterval : SonosBackoffAfterFailure);
            if (_playerViewActive && IsSonosSession(_sessions.Current.Id)) UpdatePlayerView();
            return ok;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
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
        if (updateAccessibleName)
        {
            PlayerPanel.SetValue(
                System.Windows.Automation.AutomationProperties.NameProperty,
                view.Title + ". " + PlayerSessionText.Text + ". " + PlayerStateText.Text);
        }
    }

    /// <summary>
    /// PODSTAWOWE polecenia sesji Sonos ISTNIEJACA droga ExecuteCommand. Sposob
    /// mapowania przepisany z WiiM (te same identyfikatory, te same skroty), ale
    /// nie jego HTTP: tu ida wylacznie polecenia Control API grupy.
    /// </summary>
    internal async Task ExecuteSonosCommandAsync(string commandId)
    {
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
                        _ => 0
                    };
                    if (seconds == 0)
                    {
                        Announce("Ten rodzaj przewijania nie jest obsługiwany w sesji Sonos");
                        return;
                    }

                    result = await backend.SeekRelativeAsync(
                        group.Id, seconds * 1000, beforeItemId, token).ConfigureAwait(true);
                    break;
                }
            }

            if (ticket != _sonosTargetTicket || _isClosing) return;

            // Accepted nie znaczy wykonane. Zawsze robimy JAWNY odczyt w rodzaju
            // polecenia i mowimy tylko to, co odczyt potwierdzil. Zadnego
            // ponowienia POST - nawet po 401.
            var accepted = result.Status == SonosGroupOperationStatus.Attempted
                && result.Outcome?.Status == SonosControlApiStatus.Success;
            var readOk = await ReadSonosGroupStateAsync().ConfigureAwait(true);
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
            // Wlasny przelot, wlasne zwolnienie: nie czyscimy czyjegos nowszego.
            if (ticket == _sonosTargetTicket) _sonosCommandInFlight = false;
        }
    }

    /// <summary>
    /// Enter na liscie: grupa aktywna, a POTEM istniejacy wzorzec widoku
    /// odtwarzacza. Odtwarzacz otwiera sie nawet gdy odczyt nie dal danych -
    /// pokazuje wtedy "Brak informacji", a nie wymyslony stan.
    /// </summary>
    private async Task ActivateSonosGroupThenShowPlayerAsync(string groupId)
    {
        await ActivateSonosGroupAsync(groupId).ConfigureAwait(true);
        if (_isClosing || SonosActiveGroup is not { } group) return;
        var session = _sessions.FindSession(SonosSessionId);
        var row = session?.Items.FirstOrDefault(item =>
            string.Equals(item.Id, group.Id, StringComparison.Ordinal));
        if (session is not null && row is not null) session.SelectItem(row);
        _playerFocusContextPrefix = "Wybrano grupę " + group.Name;
        ShowPlayerView();
    }

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

        await ReadSonosGroupStateAsync().ConfigureAwait(true);
    }

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
        _sonosCommandInFlight = false;
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
internal sealed class SonosAccountOwnerGroupBackend : ISonosGroupSessionBackend
{
    private readonly SonosAccountOwner _owner;

    internal SonosAccountOwnerGroupBackend(SonosAccountOwner owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
    }

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
}

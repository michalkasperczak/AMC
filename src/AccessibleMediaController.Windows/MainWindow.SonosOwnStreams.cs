using System.Windows;
using AccessibleMediaController.Core.Sonos;

namespace AccessibleMediaController.Windows;

public partial class MainWindow
{
    private SonosOwnStreamsWindow? _sonosOwnStreamsWindow;
    private readonly string _sonosOwnStreamAppContext = Guid.NewGuid().ToString("N");
    internal SonosOwnStreamsWindow? OpenSonosOwnStreamsWindowForTests => _sonosOwnStreamsWindow;

    private void ShowSonosOwnStreams()
    {
        if (_sonosOwnStreamsWindow is { IsLiveOwnerTarget: true } open) { open.Activate(); return; }
        if (!CanPresentSonosChildWindow())
        { Announce("Wróć do głównego okna AMC i otwórz Moje stacje jeszcze raz."); return; }
        ApplySonosAccountBinding();
        var backend = EnsureSonosBackend() as ISonosOwnStreamsSessionBackend;
        var group = SonosActiveGroup;
        var home = _state.Sonos.SelectedHouseholdId;
        var ticket = _sonosTargetTicket;
        var window = new SonosOwnStreamsWindow(_state.Sonos.OwnStreams, group?.Name,
            rows =>
            {
                _state.Sonos.OwnStreams = rows.Select(SonosOwnStreamsWindow.Copy).ToList();
                QueueStateSave(announceFailure: true);
            }, backend is null ? null : request =>
                LoadSonosOwnStreamAsync(backend, home, group?.Id, ticket, request)) { Owner = this };
        _sonosOwnStreamsWindow = window;
        try { window.ShowDialog(); }
        finally { _sonosOwnStreamsWindow = null; }
        if (!_isClosing && IsActive && IsSonosSession(_sessions?.Current.Id) && !_playerViewActive)
            RestoreMediaListFocusAfterRefresh();
    }

    private async Task LoadSonosOwnStreamAsync(ISonosOwnStreamsSessionBackend backend,
        string? home, string? groupId, int ticket, SonosOwnStreamsWindow.PlayRequest request)
    {
        var origin = request.Origin;
        bool Live() => !_isClosing && ReferenceEquals(origin, _sonosOwnStreamsWindow) && origin.IsLiveOwnerTarget;
        void Say(string text) { if (Live()) origin.AnnounceForOwner(text); }
        bool ContextValid() => !ApplySonosAccountBinding() && ticket == _sonosTargetTicket
            && IsSonosSession(_sessions?.Current.Id)
            && string.Equals(home, _state.Sonos.SelectedHouseholdId, StringComparison.Ordinal)
            && groupId is not null && SonosActiveGroup?.Id == groupId;
        if (!Live()) return;
        if (!SonosStreamUrlPolicy.IsAcceptable(request.Station.StreamUrl))
        { Say("Adres stacji jest niepoprawny. Wybierz Edytuj i podaj bezpośredni adres HTTP lub HTTPS."); return; }
        if (!ContextValid())
        { Say("Nie wysłano polecenia. Sprawdź cel przez Control F5 i otwórz Moje stacje ponownie."); return; }
        if (_sonosCommandInFlight) { Say("Inne polecenie Sonosa jest już w toku."); return; }
        if (!SonosSessionRequest.TryCreate("pl.amc.accessiblemultimediacontroller", _sonosOwnStreamAppContext,
            out var sessionRequest))
        { Say("Nie przygotowano żądania radia. Żadne polecenie nie zostało wysłane."); return; }

        var gate = ++_sonosCommandGateTicket;
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(
            EnsureSonosCancellation().Token, request.Lifetime);
        _sonosCommandInFlight = true;
        var attempted = false;
        try
        {
            // Utworzenie sesji przejmuje cel. Tylko jawne Odtwórz może tu wejść.
            attempted = true;
            var created = await backend.CreateSessionAsync(groupId, sessionRequest!, lifetime.Token).ConfigureAwait(true);
            attempted = created.RequestSent;
            if (!Live()) return;
            if (!ContextValid() || lifetime.IsCancellationRequested)
            {
                Say(created.RequestSent
                    ? "Kontekst zmienił się podczas przygotowania radia. Adresu nie wysłano; sesja mogła przejąć grupę. Sprawdź Sonosa."
                    : "Kontekst się zmienił. Żądania radia nie wysłano.");
                return;
            }
            if (!created.TryGetSessionId(out var sessionId)) { Say(created.Message); return; }
            var loaded = await backend.LoadStreamUrlAsync(sessionId, request.Station.StreamUrl,
                playOnCompletion: true, itemId: request.Station.Id, lifetime.Token).ConfigureAwait(true);
            if (!Live()) return;
            if (!ContextValid())
            {
                Say("Cel lub konto zmieniło się podczas próby. Nie ma potwierdzenia wyniku; sprawdź stan Sonosa.");
                return;
            }
            Say(loaded.Accepted
                ? $"Sonos przyjął stację: {request.Station.Name}. Odtwarzanie nie zostało jeszcze potwierdzone."
                : loaded.Message);
            if (!lifetime.IsCancellationRequested)
            {
                try { await ReadSonosGroupStateAsync().ConfigureAwait(true); }
                catch (OperationCanceledException) { }
                catch (Exception) { /* Wynik polecenia już przekazano, odczyt nie ponawia zapisu. */ }
            }
        }
        catch (OperationCanceledException)
        {
            Say(attempted ? "Próbę przerwano. Sesja mogła już przejąć grupę; sprawdź stan Sonosa."
                : "Próbę przerwano bez wysłania polecenia.");
        }
        catch (Exception)
        {
            Say("Nie udało się zakończyć próby. Jej skutek może być nieznany; sprawdź Sonosa przed ponowieniem.");
        }
        finally { ReleaseSonosCommandGate(gate); }
    }
}

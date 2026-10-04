using System.Windows;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Core.Sonos;
using AccessibleMediaController.Windows.Services;

namespace AccessibleMediaController.Windows;

public partial class MainWindow
{
    private SonosOwnStreamsWindow? _sonosOwnStreamsWindow;
    private readonly string _sonosOwnStreamAppContext = Guid.NewGuid().ToString("N");
    internal SonosOwnStreamsWindow? OpenSonosOwnStreamsWindowForTests => _sonosOwnStreamsWindow;

    internal void ShowSonosOwnStreamsForTests() => ShowSonosOwnStreams();

    /// <summary>
    /// PARAMETRY WŁASNEJ STACJI pod Strzalka w lewo w oknie Moich stacji.
    ///
    /// Ta metoda NIE ma wlasnej logiki odczytu: sklada ISTNIEJACA sonde radiowa
    /// (<c>RadioMediaOutput.TryReadStreamMetadataAsync</c>) z ISTNIEJACYM
    /// formaterem (<c>BuildQuickMediaInformation</c>), czyli dokladnie te dwa
    /// elementy, ktore juz obsluguja Strzalke w lewo na liscie Radia. Dzieki
    /// temu Michal slyszy TE SAMA wypowiedz w obu miejscach, a przyszla poprawka
    /// formatu dziala od razu w obu.
    ///
    /// Sonda CZYTA NAGLOWKI, nie uruchamia odtwarzania: stan Sonosa i lokalny
    /// odtwarzacz zostaja nietkniete. Pracujemy na KOPII MediaItem - zapisany
    /// wpis stacji nie jest modyfikowany, wiec odczyt nie brudzi listy.
    /// </summary>
    private async Task<string> DescribeSonosOwnStreamAsync(SonosOwnStreamSettings station)
    {
        var address = station.StreamUrl?.Trim();
        if (string.IsNullOrWhiteSpace(address)) return "Ta stacja nie ma zapisanego adresu";

        // KOPIA DO OPISU, nie zapisany wpis: nic z tego nie trafia na dysk.
        var probe = new MediaItem
        {
            Id = station.Id,
            Title = station.Name,
            Source = address,
            // STACJA, nie utwor: ten sam rodzaj, co wpisy radia internetowego,
            // wiec formater mowi o niej tym samym jezykiem.
            Kind = MediaItemKind.Station,
        };

        var metadata = await RadioMediaOutput
            .TryReadStreamMetadataAsync(address, TimeSpan.FromSeconds(6))
            .ConfigureAwait(true);
        if (metadata is not null)
        {
            probe.BitrateKbps = RadioAudioMetadataRules.NormalizeBitrateKbps(metadata.BitrateKbps);
            if (probe.BitrateKbps is not null) probe.IsBitrateEstimated = metadata.IsBitrateEstimated;
            probe.SampleRateHz = metadata.SampleRateHz;
            probe.Codec = metadata.Codec;
        }

        return BuildQuickMediaInformation(probe);
    }

    /// <summary>
    /// TESTOWY punkt podstawienia POKAZANIA Moich stacji (produkcyjnie modal).
    /// Wzor jak <c>PresentSonosLibraryOverrideForTests</c>: pomiar sprawdza, ze
    /// okno POWSTALO na prawdziwej drodze, bez stawiania modala na pulpicie.
    /// </summary>
    internal Action<SonosOwnStreamsWindow>? PresentSonosOwnStreamsOverrideForTests { get; set; }

    private void ShowSonosOwnStreams(string? preferredStationId = null, string? announcement = null)
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
                LoadSonosOwnStreamAsync(backend, home, group?.Id, ticket, request),
            station => AssignSonosOwnStreamPreset(
                station, _sonosOwnStreamsWindow!, home, ticket),
            // KOLEJNOSC I TRYB z ISTNIEJACEGO magazynu kolekcji - tego samego,
            // ktorego uzywa Biblioteka i Ulubione. Zero nowych pol konfiguracji.
            // Tryb siedzi w stanie nawigacji sesji (tak jak w oknie glownym),
            // a sama kolejnosc w CollectionOrders pod kluczem widoku.
            _state.CollectionOrders,
            () => GetSessionNavigationState("sonos").CollectionSortModes
                .GetValueOrDefault(SonosOwnStreamsOrder.ViewName, CollectionSortMode.Custom),
            mode => GetSessionNavigationState("sonos")
                .CollectionSortModes[SonosOwnStreamsOrder.ViewName] = mode,
            // TEN SAM zapis, co reszta listy: kolejnosc trafia na dysk produkcyjna
            // kolejka, a nie wlasnym Save w oknie.
            () => QueueStateSave(announceFailure: true)) { Owner = this };
        // IMPORT Z WNETRZA LISTY: TA SAMA akcja, co w menu Plik. Okno samo nie
        // czyta pliku ani nie zapisuje stanu - oddaje to tej jednej drodze.
        window.ImportPlaylist = () => ImportSonosOwnStreams(window);
        // PARAMETRY STACJI POD STRZALKA W LEWO: TA SAMA sonda strumienia i TEN
        // SAM formater, co w sesji Radia. Zaden nowy silnik, zadne ffprobe,
        // zadne zgadywane wartosci - a sonda nie uruchamia odtwarzania.
        window.DescribeStation = DescribeSonosOwnStreamAsync;
        _sonosOwnStreamsWindow = window;
        // POWROT Z INNEJ SESJI: wiersz, na ktorym uzytkownik stal przed Ctrl+cyfra.
        // Przy zwyklym otwarciu pole jest puste i lista zostaje na pierwszym wierszu.
        var pending = preferredStationId
            ?? ConsumeSonosSublistPendingRowId(SonosLibraryPresentation.OwnStreamsCategoryId);
        if (pending is { } pendingStation)
        {
            window.Loaded += (_, _) => window.RestoreSelectedRow(pendingStation);
        }
        if (announcement is { Length: > 0 } text)
        {
            window.Loaded += (_, _) => window.AnnounceForOwner(text);
        }
        try
        {
            if (PresentSonosOwnStreamsOverrideForTests is { } present) present(window);
            else window.ShowDialog();
        }
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
        var intent = NextSonosPlaybackIntent();
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
            if (intent != _sonosPlaybackIntent || !ContextValid() || lifetime.IsCancellationRequested)
            {
                Say(created.RequestSent
                    ? "Kontekst zmienił się podczas przygotowania radia. Adresu nie wysłano; sesja mogła przejąć grupę. Sprawdź Sonosa."
                    : "Kontekst się zmienił. Żądania radia nie wysłano.");
                return;
            }
            if (!created.TryGetSessionId(out var sessionId)) { Say(created.Message); return; }
            var loaded = await backend.LoadStreamUrlAsync(sessionId, request.Station.StreamUrl,
                playOnCompletion: true, itemId: SonosOwnStreamIdentity.TryComputeItemId(request.Station.Id, request.Station.StreamUrl), lifetime.Token).ConfigureAwait(true);
            if (!Live()) return;
            if (!ContextValid())
            {
                Say("Cel lub konto zmieniło się podczas próby. Nie ma potwierdzenia wyniku; sprawdź stan Sonosa.");
                return;
            }
            Say(loaded.Accepted
                ? $"Uruchamianie: {request.Station.Name}"
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

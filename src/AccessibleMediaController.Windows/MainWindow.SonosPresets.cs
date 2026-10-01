using System.Windows;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sonos;

namespace AccessibleMediaController.Windows;

/// <summary>
/// PRESETY SONOSA w ISTNIEJACYM mechanizmie presetow AMC.
///
/// Zadnego nowego skrotu i zadnego drugiego magazynu:
///  * Ctrl+Alt+Shift+P przypisuje ZAZNACZONY material z otwartego modalu
///    (Ulubione Sonos / Playlisty Sonos / Moje stacje). Glowny Owner jest w
///    modalu WYLACZONY, wiec router polecen do nas nie dojdzie - dlatego klawisz
///    przechwytuje OKNO i oddaje go tu callbackiem,
///  * Ctrl+Alt+P pokazuje liste presetow sesji Sonos - ta SAMA droga co inne
///    sesje (<c>ShowPresets</c>),
///  * Ctrl+Shift+cyfra uruchamia preset, Ctrl+cyfra zostaje wyborem sesji.
///
/// Material, nie glosnik: zapisujemy ulubiony, playliste albo wlasna stacje.
/// Grupy z glownej listy sesji NIE DA SIE zapisac - nie ma takiego rodzaju w
/// <see cref="SonosPresetKinds"/>.
///
/// Uruchomienie idzie na AKTUALNIE WYBRANEJ grupie. Opcjonalne "Zawsze w tym
/// miejscu" wiaze preset z LOGICZNYMI playerIds i domem; brak grupy o dokladnie
/// tym skladzie to UCZCIWA ODMOWA - nie przegrupowujemy i nie gramy obok.
///
/// Ustawienie presetu NIE ODTWARZA niczego.
/// </summary>
public partial class MainWindow
{
    /// <summary>
    /// WSPOLNY licznik ZAMIARU odtwarzania Sonos. Podnosi go KAZDE jawne
    /// zlecenie: Enter z modalu i uruchomienie presetu. Spozniony przelot, ktory
    /// widzi nowszy numer, NIE mowi i NIE dziala - inaczej stary preset
    /// przykrylby nowszego Entera albo nowszy preset.
    /// </summary>
    private int _sonosPlaybackIntent;

    internal int NextSonosPlaybackIntent() => ++_sonosPlaybackIntent;

    internal int SonosPlaybackIntentForTests => _sonosPlaybackIntent;

    /// <summary>Ile POST poszlo droga presetu. Odmowa i powtorzenie NIE licza sie.</summary>
    internal int SonosPresetLoadsSentForTests { get; private set; }

    /// <summary>Ile razy preset rozpoznal GRAJACY material i powiedzial SAMA nazwe.</summary>
    internal int SonosPresetRepeatAnnouncementsForTests { get; private set; }

    internal Task? LastSonosPresetTaskForTests { get; private set; }

    // ---------------------------------------------------------------- PRZYPISANIE

    /// <summary>
    /// PRZYPISANIE z okna ULUBIONYCH. Identyfikator idzie LITERALNIE z pozycji
    /// listy - nigdy z nazwy i nigdy z container.id, ktorego AMC nie parsuje.
    /// Dom bierzemy z WYBRANEGO domu sesji, zeby ulubiony domu A nie pojechal
    /// potem przez dom B.
    /// </summary>
    internal void AssignSonosFavoritePreset(SonosFavorite favorite, Window origin) =>
        AssignSonosFavoritePreset(favorite, origin, _state.Sonos.SelectedHouseholdId, _sonosTargetTicket);

    /// <summary>
    /// Wariant z KONTEKSTEM Z OTWARCIA listy. Dom i bilet celu przychodza z
    /// chwili, w ktorej lista powstala - nie z chwili zapisu. Inaczej material
    /// domu A zostalby zapisany jako nalezacy do domu B, gdyby konto zmienilo
    /// sie w czasie otwartego okna przypisania.
    /// </summary>
    internal void AssignSonosFavoritePreset(
        SonosFavorite favorite,
        Window origin,
        string? originHouseholdId,
        int originTicket)
    {
        ArgumentNullException.ThrowIfNull(favorite);
        AssignSonosMaterialPreset(
            origin,
            SonosPresetKinds.Favorite,
            favorite.Id,
            SonosFavoritesLabels.Describe(favorite),
            // Dla ulubionego i playlisty TargetLocation NIE jest adresem: zostaje
            // puste, zeby nikt nie wzial go za URL do GET.
            targetLocation: null,
            originHouseholdId,
            originTicket);
    }

    internal void AssignSonosPlaylistPreset(SonosPlaylist playlist, Window origin) =>
        AssignSonosPlaylistPreset(playlist, origin, _state.Sonos.SelectedHouseholdId, _sonosTargetTicket);

    internal void AssignSonosPlaylistPreset(
        SonosPlaylist playlist,
        Window origin,
        string? originHouseholdId,
        int originTicket)
    {
        ArgumentNullException.ThrowIfNull(playlist);
        AssignSonosMaterialPreset(
            origin,
            SonosPresetKinds.Playlist,
            playlist.Id,
            SonosPlaylistsLabels.Describe(playlist),
            targetLocation: null,
            originHouseholdId,
            originTicket);
    }

    /// <summary>
    /// PRZYPISANIE WLASNEJ STACJI. Zapisujemy LOKALNY identyfikator stacji, a
    /// NIE adres: adres pobierzemy z AKTUALNEGO wpisu przy uruchomieniu, wiec
    /// edycja adresu nie zostawia w presecie zamrozonego, przestarzalego URL.
    /// </summary>
    internal void AssignSonosOwnStreamPreset(SonosOwnStreamSettings station, Window origin) =>
        AssignSonosOwnStreamPreset(station, origin, _state.Sonos.SelectedHouseholdId, _sonosTargetTicket);

    internal void AssignSonosOwnStreamPreset(
        SonosOwnStreamSettings station,
        Window origin,
        string? originHouseholdId,
        int originTicket)
    {
        ArgumentNullException.ThrowIfNull(station);
        AssignSonosMaterialPreset(
            origin,
            SonosPresetKinds.OwnStream,
            station.Id,
            station.Name,
            targetLocation: null,
            originHouseholdId,
            originTicket);
    }

    /// <summary>
    /// JEDNA droga przypisania dla trzech list. Uzywamy ISTNIEJACEGO
    /// <see cref="RadioPresetAssignmentWindow"/> - z jego wymaganiem DWUKROTNEGO
    /// wyboru zajetego miejsca, z Delete/Enter i z tym samym zestawem wyborow.
    /// Nowa jest TYLKO opcjonalna opcja stalego zestawu, wlaczana wlasciwoscia -
    /// zeby nie ruszac sygnatur 3/7 parametrow, na ktore licza inni wolajacy.
    ///
    /// ZERO ODTWARZANIA: ta droga zapisuje i mowi. Nic nie wysyla do Sonosa.
    /// </summary>
    private void AssignSonosMaterialPreset(
        Window origin,
        string kind,
        string targetId,
        string targetTitle,
        string? targetLocation,
        string? originHouseholdId,
        int originTicket)
    {
        if (!IsSonosSession(_sessions?.Current.Id)) return;
        if (string.IsNullOrWhiteSpace(targetId)) return;

        // DOM Z OTWARCIA listy, nie z chwili zapisu: pozycja pochodzi z katalogu
        // TEGO domu. Gdy konto/dom zmienilo sie jeszcze PRZED dialogiem, nie ma
        // czego zapisywac - material nalezy do domu, ktorego juz nie ma.
        if (!SonosPresetOriginStillValid(kind, originHouseholdId, originTicket))
        {
            AnnounceInSonosOrigin(origin, SonosPresetLabels.AssignContextChanged);
            return;
        }

        var session = _sessions!.Current;
        var choices = SessionPresetChoices(session);
        var entries = SessionPresetEntries(session.Id);
        // ZAJETOSC po RODZAJU + DOMU + identyfikatorze. Samo targetId nie wystarcza:
        // ulubiony i playlista z tym samym identyfikatorem (albo ten sam
        // identyfikator w dwoch domach) to ROZNY material i nie moze uchodzic za
        // juz przypisany, bo ominalby zgode na nadpisanie. Klucz jest LOKALNY dla
        // wyborow okna - w presecie zapisujemy LITERALNY identyfikator.
        var materialKey = SonosPresetMaterialKey(kind, originHouseholdId, targetId);
        var existingSlot = entries.FirstOrDefault(entry =>
            string.Equals(
                SonosPresetMaterialKey(entry.TargetKind, entry.SonosHouseholdId, entry.TargetId),
                materialKey,
                StringComparison.Ordinal))?.Slot;
        var firstFree = choices.FirstOrDefault(choice => choice.StationId is null)?.Slot;
        var initialSlot = existingSlot ?? firstFree ?? 1;

        // IDENTYFIKATOR DLA OKNA: skladowy, zeby okno porownywalo MATERIAL, a nie
        // goly napis. Do presetu zapisujemy dalej literalny targetId.
        var windowChoices = choices
            .Select(choice =>
            {
                var entry = entries.FirstOrDefault(item => item.Slot == choice.Slot);
                return entry is null
                    ? choice
                    : choice with
                    {
                        StationId = SonosPresetMaterialKey(
                            entry.TargetKind, entry.SonosHouseholdId, entry.TargetId)
                    };
            })
            .ToArray();

        var dialog = new RadioPresetAssignmentWindow(
            targetTitle,
            materialKey,
            windowChoices,
            firstFree,
            initialSlot,
            session.DisplayName)
        {
            // WLASCICIELEM jest ZLECAJACY MODAL, nie okno glowne: glowne jest
            // wylaczone, wiec dialog musialby zostac za nim.
            Owner = origin
        };

        // STALY ZESTAW proponujemy TYLKO gdy JEST co zapisac: aktualna grupa z
        // niepustym skladem w znanym domu. Bez tego opcja byla by martwa kontrolka.
        var household = originHouseholdId;
        var group = SonosActiveGroup;
        var fixedIds = SonosPresetFixedTarget.NormalizePlayerIds(group?.PlayerIds);
        var canOfferFixed = !string.IsNullOrWhiteSpace(household) && fixedIds.Count > 0;
        var existingEntry = existingSlot is int slotWithEntry
            ? entries.FirstOrDefault(entry => entry.Slot == slotWithEntry)
            : null;
        if (canOfferFixed)
        {
            dialog.ShowFixedTargetOption(
                group!.Name,
                // Przy EDYCJI istniejacego przypisania pokazujemy stan, ktory tam JEST.
                initiallyChosen: existingEntry?.SonosFixedPlayerIds is { Count: > 0 });
        }

        if (dialog.ShowDialog() != true) return;

        // BRAMKA PO DIALOGU, PRZED ZAPISEM: dom/konto moglo zmienic sie w czasie
        // otwartego okna. Zapis trwaly, wiec stary identyfikator NIE MOZE wpisac
        // sie jako nalezacy do nowego domu.
        if (!SonosPresetOriginStillValid(kind, originHouseholdId, originTicket))
        {
            AnnounceInSonosOrigin(origin, SonosPresetLabels.AssignContextChanged);
            return;
        }

        var index = entries.FindIndex(entry => entry.Slot == dialog.SelectedSlot);
        var slotLabel = RadioPresetSlots.Label(dialog.SelectedSlot);
        if (dialog.SelectedAction == RadioPresetAssignmentAction.Remove)
        {
            // USUNIECIE jest TRWALE: po Save/Load preset nie wraca, bo znika z
            // listy, ktora idzie do zapisu.
            if (index >= 0) entries.RemoveAt(index);
            _state.SessionPresets.EntriesBySession[session.Id] =
                entries.OrderBy(entry => entry.Slot).ToList();
            SavePresetState(session.Id);
            AnnounceInSonosOrigin(origin, $"Usunięto preset {slotLabel}");
            return;
        }

        var useFixed = canOfferFixed && dialog.FixedTargetChosen;
        var preset = new SessionPresetEntry
        {
            Slot = dialog.SelectedSlot,
            TargetId = targetId,
            TargetKind = kind,
            TargetTitle = targetTitle,
            TargetLocation = targetLocation,
            SonosHouseholdId = SonosPresetKinds.IsHouseholdBound(kind) ? household : null,
            SonosFixedPlayerIds = useFixed ? fixedIds.ToList() : null
        };
        // Staly zestaw wymaga domu TAKZE dla wlasnej stacji - inaczej nie ma na
        // czym sprawdzic, ze sklad dotyczy tego samego domu.
        if (useFixed && preset.SonosHouseholdId is null) preset.SonosHouseholdId = household;

        if (index >= 0) entries[index] = preset;
        else entries.Add(preset);
        _state.SessionPresets.EntriesBySession[session.Id] =
            entries.OrderBy(entry => entry.Slot).ToList();
        SavePresetState(session.Id);
        AnnounceInSonosOrigin(origin, useFixed
            ? SonosPresetLabels.DescribeAssignedFixed(slotLabel, targetTitle, fixedIds.Count)
            : SonosPresetLabels.DescribeAssigned(slotLabel, targetTitle));
    }

    /// <summary>
    /// KLUCZ MATERIALU dla wyborow okna przypisania: rodzaj + dom + literalny
    /// identyfikator. SLUZY WYLACZNIE do porownan w oknie - NIE jest tym, co
    /// laduje w presecie. Dom wchodzi tylko dla rodzajow zwiazanych z domem;
    /// wlasna stacja ma identyfikator lokalny, wiec jej dom nie rozroznia.
    /// </summary>
    private static string SonosPresetMaterialKey(string? kind, string? householdId, string? targetId)
    {
        var house = SonosPresetKinds.IsHouseholdBound(kind) ? householdId ?? string.Empty : string.Empty;
        return (kind ?? string.Empty) + "\u001f" + house + "\u001f" + (targetId ?? string.Empty);
    }

    /// <summary>
    /// Czy KONTEKST Z OTWARCIA listy nadal obowiazuje. Sprawdzamy bilet celu
    /// (zmienia go kazde przelaczenie celu/konta) oraz - dla materialu zwiazanego
    /// z domem - zgodnosc wybranego domu. <c>ApplySonosAccountBinding</c> jest
    /// wolane PIERWSZE, bo samo wykrywa zmiane wlasciciela konta.
    /// </summary>
    private bool SonosPresetOriginStillValid(string kind, string? originHouseholdId, int originTicket)
    {
        if (ApplySonosAccountBinding()) return false;
        if (originTicket != _sonosTargetTicket) return false;
        if (!SonosPresetKinds.IsHouseholdBound(kind)) return true;
        if (string.IsNullOrWhiteSpace(originHouseholdId)) return false;
        return string.Equals(
            _state.Sonos.SelectedHouseholdId, originHouseholdId, StringComparison.Ordinal);
    }

    /// <summary>
    /// Status do ZLECAJACEGO modalu, gdy ten zyje; inaczej do okna glownego.
    /// Przy przypisaniu to BEZPIECZNE: nic nie poszlo w siec, wiec komunikat nie
    /// moze "obiecac" cudzego odtwarzania.
    /// </summary>
    private void AnnounceInSonosOrigin(Window origin, string message)
    {
        switch (origin)
        {
            case SonosFavoritesWindow { IsLiveOwnerTarget: true } favorites:
                favorites.AnnounceForOwner(message);
                return;
            case SonosPlaylistsWindow { IsLiveOwnerTarget: true } playlists:
                playlists.AnnounceForOwner(message);
                return;
            case SonosOwnStreamsWindow { IsLiveOwnerTarget: true } streams:
                streams.AnnounceForOwner(message);
                return;
            default:
                Announce(message);
                return;
        }
    }

    // ---------------------------------------------------------------- URUCHOMIENIE

    /// <summary>
    /// Wejscie z ISTNIEJACEJ drogi presetow (Ctrl+Shift+cyfra oraz lista
    /// Ctrl+Alt+P). Zadanie trzymamy dla pomiaru; nie czekamy na nie w UI.
    /// </summary>
    private void StartSonosPresetActivation(
        SessionPresetEntry preset,
        string slotLabel,
        bool fromGlobalShortcut) =>
        LastSonosPresetTaskForTests = ActivateSonosPresetAsync(preset, slotLabel, fromGlobalShortcut);

    /// <summary>
    /// URUCHOMIENIE PRESETU SONOSA.
    ///
    /// Kolejnosc jest cala trescia bezpieczenstwa:
    ///  1) zamiar: biore numer, ktory kazdy nowszy Enter/preset uniewazni,
    ///  2) konto PRZED czymkolwiek - stary identyfikator nie idzie przez nowe konto,
    ///  3) cel: staly zestaw ze SWIEZEJ topologii albo AKTUALNIE wybrana grupa.
    ///     Brak celu to Ctrl+F5 i ZERO POST,
    ///  4) dom materialu: ulubiony/playlista z innego domu NIE ida nigdzie,
    ///  5) material: wlasna stacja musi ISTNIEC teraz i dac AKTUALNY adres,
    ///  6) POWTORZENIE ze SWIEZEGO odczytu - gra, pauza albo zwykly load,
    ///  7) jeden POST ta SAMA bramka co reszta sterowania Sonos.
    ///
    /// Zdalny/globalny skrot NIE kradnie fokusu i NIE otwiera przy okazji listy.
    /// </summary>
    private async Task ActivateSonosPresetAsync(
        SessionPresetEntry preset,
        string slotLabel,
        bool fromGlobalShortcut)
    {
        ArgumentNullException.ThrowIfNull(preset);
        var intent = NextSonosPlaybackIntent();

        if (ApplySonosAccountBinding())
        {
            Announce(SonosPresetLabels.AccountChanged);
            return;
        }

        if (!IsSonosSession(_sessions?.Current.Id)) return;
        var backend = EnsureSonosBackend();
        var household = _state.Sonos.SelectedHouseholdId;
        var ticket = _sonosTargetTicket;
        bool Current() => IsSonosPresetIntentCurrent(intent, ticket)
            && IsSonosPresetHouseholdStill(household);

        // DOM MATERIALU. Zapisany dom, ktory nie jest biezacym, konczy droge:
        // opaque identyfikator z domu A nie ma prawa pojsc jako token do domu B.
        if (SonosPresetKinds.IsHouseholdBound(preset.TargetKind)
            && preset.SonosHouseholdId is { Length: > 0 } boundHousehold
            && !string.Equals(boundHousehold, household, StringComparison.Ordinal))
        {
            Announce(SonosPresetLabels.DescribeMaterialGone(slotLabel, preset.TargetKind));
            return;
        }

        if (_sonosCommandInFlight)
        {
            Announce(SonosPresetLabels.AlreadyInFlight);
            return;
        }

        var gate = ++_sonosCommandGateTicket;
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(
            EnsureSonosCancellation().Token);
        var token = lifetime.Token;
        _sonosCommandInFlight = true;
        try
        {
            // --- CEL ---
            string? groupId;
            if (preset.SonosFixedPlayerIds is { Count: > 0 })
            {
                var resolved = await ResolveSonosFixedPresetGroupAsync(
                    backend, preset, household, token).ConfigureAwait(true);
                if (!Current()) return;
                if (resolved.Resolution != SonosFixedTargetResolution.Resolved)
                {
                    // UCZCIWA ODMOWA. Nic nie uruchamiamy, nie przegrupowujemy i
                    // nie tworzymy par stereo.
                    Announce(SonosPresetLabels.DescribeFixedRefusal(resolved.Resolution));
                    return;
                }
                groupId = resolved.Group!.Id;
            }
            else if (SonosActiveGroup is { } current)
            {
                // DOMYSLNIE: AKTUALNIE wybrana grupa, bez samoczynnego przelaczania.
                groupId = current.Id;
            }
            else
            {
                Announce(SonosPresetLabels.NeedsGroup);
                return;
            }

            // --- MATERIAL ---
            // Wlasna stacja: ID zostaje, ADRES bierzemy z AKTUALNEGO wpisu.
            SonosOwnStreamSettings? station = null;
            if (string.Equals(preset.TargetKind, SonosPresetKinds.OwnStream, StringComparison.OrdinalIgnoreCase))
            {
                station = _state.Sonos.OwnStreams.FirstOrDefault(entry =>
                    string.Equals(entry.Id, preset.TargetId, StringComparison.Ordinal));
                if (station is null)
                {
                    // Usunieta stacja NIE zmartwychwstaje z presetu.
                    Announce(SonosPresetLabels.DescribeMaterialGone(slotLabel, preset.TargetKind));
                    return;
                }
                if (!SonosStreamUrlPolicy.IsAcceptable(station.StreamUrl))
                {
                    // WALIDACJA PRZED przejeciem grupy: zadnej sesji i zadnego GET
                    // na adres, ktorego nie przyjmiemy.
                    Announce($"Adres stacji {station.Name} jest niepoprawny. "
                        + "Popraw go w Moje stacje. Nic nie uruchomiłem");
                    return;
                }
            }

            // --- POWTORZENIE ---
            // ULUBIONY ma WLASNA droge rozpoznania: tozsamosc MATERIALU ze
            // swiezego katalogu domu kontra container.id grupy. Wlasna stacja i
            // playlista zostaja przy dotychczasowej (dla playlisty: zawsze load).
            var repeat = station is null && SonosPresetKinds.IsFavorite(preset.TargetKind)
                ? await DecideSonosFavoriteRepeatAsync(
                    backend, groupId, household, preset, token,
                        () => Current()).ConfigureAwait(true)
                : await DecideSonosPresetRepeatAsync(
                    backend, groupId, preset, station, token,
                        () => Current()).ConfigureAwait(true);
            if (!Current()) return;

            if (repeat == SonosPresetRepeatDecision.Unavailable)
            {
                Announce("Nie udało się potwierdzić bieżącego materiału Sonos. Nic nie uruchomiłem");
                return;
            }

            if (repeat == SonosPresetRepeatDecision.AnnounceOnly)
            {
                // TEN material GRA: SAMA NAZWA. Zero load, zero nowej kolejki,
                // zero restartu - album na kolejnym utworze nie wraca do poczatku.
                SonosPresetRepeatAnnouncementsForTests++;
                Announce(SonosPresetTitle(preset, station));
                return;
            }

            if (repeat == SonosPresetRepeatDecision.Resume)
            {
                // SPAUZOWANY TEN material: zwykle wznowienie WLASCIWEJ grupy bez
                // przeladowania, a potem sama nazwa.
                var resumed = await backend
                    .SendGroupCommandAsync(groupId, SonosGroupCommand.Play, token)
                    .ConfigureAwait(true);
                if (!Current()) return;
                Announce(resumed.Accepted ? SonosPresetTitle(preset, station) : resumed.Message);
                await RefreshSonosStateAfterPresetAsync().ConfigureAwait(true);
                return;
            }

            // --- ZWYKLY LOAD ---
            var message = await SendSonosPresetLoadAsync(
                backend, groupId, preset, station, token,
                    () => Current()).ConfigureAwait(true);
            if (!Current()) return;
            if (message is null) return;
            Announce(message);
            await RefreshSonosStateAfterPresetAsync().ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // Porzucenie pracy sesji albo wlasne zamykanie: CISZA. POST mogl pojsc,
            // wiec nie obiecujemy cofniecia.
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Sonos: preset - nie udalo sie ({exception.GetType().Name}).");
            if (!Current()) return;
            Announce("Nie udało się wykonać polecenia presetu Sonos. Spróbuj ponownie");
        }
        finally
        {
            ReleaseSonosCommandGate(gate);
            // FOKUSU NIE RUSZAMY przy skrocie globalnym: zdalny preset nie ma
            // prawa wejsc na wierzch ani otworzyc listy przy okazji.
            if (!fromGlobalShortcut && !_isClosing && IsActive && Current()
                && IsSonosSession(_sessions?.Current.Id) && !_playerViewActive)
            {
                RestoreMediaListFocusAfterRefresh();
            }
        }
    }

    /// <summary>
    /// Czy TEN przelot nadal jest aktualny: nowszy Enter/preset albo zmiana celu,
    /// konta, sesji i zamykanie uniewazniaja go. Sprawdzane PRZED mowa i PRZED POST.
    /// </summary>
    private bool IsSonosPresetIntentCurrent(int intent, int targetTicket)
    {
        if (_isClosing || ApplySonosAccountBinding()) return false;
        if (intent != _sonosPlaybackIntent) return false;
        if (targetTicket != _sonosTargetTicket) return false;
        return IsSonosSession(_sessions?.Current.Id);
    }

    private static string SonosPresetTitle(SessionPresetEntry preset, SonosOwnStreamSettings? station) =>
        // NAZWA, nie numer slotu: na sukces uzytkownik ma slyszec material.
        station is not null && !string.IsNullOrWhiteSpace(station.Name)
            ? station.Name
            : preset.TargetTitle;

    /// <summary>
    /// STALY CEL ze SWIEZEJ topologii. Czytamy grupy TYM SAMYM zapleczem sesji -
    /// zadnego drugiego klienta HTTP i zadnego wlasnego OAuth.
    /// </summary>
    private async Task<(SonosFixedTargetResolution Resolution, SonosGroup? Group)>
        ResolveSonosFixedPresetGroupAsync(
            ISonosGroupSessionBackend backend,
            SessionPresetEntry preset,
            string? household,
            CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(household))
            return (SonosFixedTargetResolution.TopologyUnavailable, null);

        var groups = await backend.ReadGroupsAsync(household, token).ConfigureAwait(true);
        // NIEUDANY odczyt to NIEZNANA topologia, nie "grupy nie ma": nie zgadujemy
        // i nie siegamy po stary cache jako dowod skladu.
        var topology = groups.Succeeded ? groups.Topology : null;
        var resolution = SonosPresetFixedTarget.Resolve(
            preset.SonosHouseholdId ?? household,
            preset.SonosFixedPlayerIds,
            household,
            topology,
            out var group);
        return (resolution, group);
    }

    /// <summary>
    /// DECYZJA O POWTORZENIU ze SWIEZEGO odczytu grupy dla WLASNEJ STACJI i dla
    /// rodzajow bez tozsamosci materialu. Brak lokalnego cache sam z siebie NIE
    /// wymusza przeladowania - rozstrzyga to, co grupa mowi TERAZ.
    ///
    /// ULUBIONY ma juz WLASNA droge (<see cref="DecideSonosFavoriteRepeatAsync"/>)
    /// opartą o <c>resource.id</c> kontra <c>container.id</c>. Tutaj zostaje
    /// PLAYLISTA: <c>loadPlaylist</c> nie przyjmuje naszego <c>itemId</c>, a
    /// native playlista Sonosa w ogole nie ma <c>resource</c> w katalogu, wiec
    /// zwracamy dla niej ZAWSZE <c>Load</c>, zamiast opierac tozsamosc na tytule,
    /// na numerze ostatniego slotu albo na zgadnietym prefiksie identyfikatora.
    /// </summary>
    private static async Task<SonosPresetRepeatDecision> DecideSonosPresetRepeatAsync(
        ISonosGroupSessionBackend backend,
        string? groupId,
        SessionPresetEntry preset,
        SonosOwnStreamSettings? station,
        CancellationToken token,
        Func<bool> isCurrent)
    {
        if (station is null) return SonosPresetRepeatDecision.Load;
        var expected = SonosOwnStreamIdentity.TryComputeItemId(station.Id, station.StreamUrl);
        if (expected is null) return SonosPresetRepeatDecision.Load;

        var playback = await backend.ReadGroupPlaybackAsync(groupId, token).ConfigureAwait(true);
        if (!isCurrent() || !playback.Succeeded) return SonosPresetRepeatDecision.Unavailable;
        if (!string.Equals(expected, playback.Value!.ItemId, StringComparison.Ordinal))
            return SonosPresetRepeatDecision.Load;
        if (playback.Value.PlaybackState is SonosPlaybackState.Playing or SonosPlaybackState.Buffering)
            return SonosPresetRepeatDecision.AnnounceOnly;
        if (playback.Value.PlaybackState is not (SonosPlaybackState.Paused or SonosPlaybackState.Idle))
            return SonosPresetRepeatDecision.Unavailable;

        var metadata = await backend.ReadGroupMetadataAsync(groupId, token).ConfigureAwait(true);
        if (!isCurrent() || !metadata.Succeeded) return SonosPresetRepeatDecision.Unavailable;
        // A metadata read is not atomic with the first playback read.
        var fresh = await backend.ReadGroupPlaybackAsync(groupId, token).ConfigureAwait(true);
        if (!isCurrent() || !fresh.Succeeded
            || !string.Equals(expected, fresh.Value!.ItemId, StringComparison.Ordinal))
            return SonosPresetRepeatDecision.Unavailable;
        if (fresh.Value.PlaybackState == SonosPlaybackState.Unknown)
            return SonosPresetRepeatDecision.Unavailable;
        return SonosPresetRepeat.Decide(expected, fresh.Value.ItemId,
            fresh.Value.PlaybackState, metadata.Value!.Container is not null);
    }

    /// <summary>
    /// Czy nadal pytamy o TEN SAM dom. Osobno od <see cref="IsSonosPresetIntentCurrent"/>,
    /// bo zmiana wybranego domu NIE musi podniesc biletu celu, a odpowiedz
    /// katalogu domu A nie ma prawa rozstrzygac materialu w domu B.
    /// </summary>
    private bool IsSonosPresetHouseholdStill(string? household) =>
        string.Equals(_state.Sonos.SelectedHouseholdId, household, StringComparison.Ordinal);

    /// <summary>
    /// POWTORZENIE ULUBIONEGO ze SWIEZYCH odczytow. Tozsamosc MATERIALU bierzemy
    /// z <c>favorites.items[].resource.id</c> i porownujemy z
    /// <c>playbackMetadata.container.id</c> - ta sama trojka
    /// (serviceId/objectId/accountId), porownywana WYLACZNIE przez
    /// <see cref="SonosResourceIdentity.Matches"/>.
    ///
    /// Czego tu NIE MA:
    ///  * zadnego drugiego klienta HTTP i zadnego wlasnego OAuth - idziemy
    ///    ISTNIEJACA granica <see cref="ISonosFavoritesSessionBackend"/>,
    ///  * zadnej kopii katalogu z kiedys otwartego modalu i zadnej pamieci po
    ///    POST: katalog czytamy TERAZ, dla WSKAZANEGO domu,
    ///  * zadnego porownania po tytule, <c>favorite.Id</c>, rodzaju ani po
    ///    <c>currentItem</c>: album na dalszym utworze to NADAL ten kontener,
    ///  * zadnego restartu "w ciemno": niepewnosc to UCZCIWA ODMOWA i ZERO POST.
    /// </summary>
    private async Task<SonosPresetRepeatDecision> DecideSonosFavoriteRepeatAsync(
        ISonosGroupSessionBackend backend,
        string? groupId,
        string? household,
        SessionPresetEntry preset,
        CancellationToken token,
        Func<bool> isCurrent)
    {
        // Zaplecze bez odczytu ulubionych mowi to uczciwie przez odmowe - NIE
        // zakladamy zgodnosci i NIE budujemy drugiego klienta.
        if (backend is not ISonosFavoritesSessionBackend favoritesBackend) return SonosPresetRepeatDecision.Unavailable;
        if (string.IsNullOrWhiteSpace(household)) return SonosPresetRepeatDecision.Unavailable;

        var catalogue = await favoritesBackend.ReadFavoritesAsync(household, token).ConfigureAwait(true);
        if (!isCurrent() || !IsSonosPresetHouseholdStill(household) || !catalogue.Succeeded)
            return SonosPresetRepeatDecision.Unavailable;

        // DOKLADNIE ten wiersz katalogu, porzadkowo. Zniknieta pozycja to
        // NIEZNANY material, a nie powod do slepego load.
        var favorite = catalogue.Favorites!.Items.FirstOrDefault(item =>
            string.Equals(item.Id, preset.TargetId, StringComparison.Ordinal));
        if (favorite is null) return SonosPresetRepeatDecision.Unavailable;

        var metadata = await backend.ReadGroupMetadataAsync(groupId, token).ConfigureAwait(true);
        if (!isCurrent() || !IsSonosPresetHouseholdStill(household) || !metadata.Succeeded)
            return SonosPresetRepeatDecision.Unavailable;
        var playback = await backend.ReadGroupPlaybackAsync(groupId, token).ConfigureAwait(true);
        if (!isCurrent() || !IsSonosPresetHouseholdStill(household) || !playback.Succeeded)
            return SonosPresetRepeatDecision.Unavailable;

        // Bracket the playback read with metadata reads for every decision,
        // including announce-only and load. A source switch must not make us
        // announce the wrong material or restart one that has just begun.
        var fresh = await backend.ReadGroupMetadataAsync(groupId, token).ConfigureAwait(true);
        if (!isCurrent() || !IsSonosPresetHouseholdStill(household) || !fresh.Succeeded)
            return SonosPresetRepeatDecision.Unavailable;
        var previousContainer = metadata.Value!.Container;
        var container = fresh.Value!.Container;
        var stableMaterial = SonosResourceIdentity.AreSameMaterial(previousContainer?.Identity, container?.Identity)
            || (previousContainer is null && container is null
                && metadata.Value.CurrentItem is null && fresh.Value.CurrentItem is null);
        if (!stableMaterial) return SonosPresetRepeatDecision.Unavailable;
        var wanted = favorite.ResourceIdentity;
        if (!SonosResourceIdentity.AreSameMaterial(wanted, container?.Identity))
        {
            // ZNANY INNY material: obie trojki kompletne i rozne - zwykly load.
            var otherKnownMaterial = wanted is { IsComplete: true }
                && container?.Identity is { IsComplete: true };
            // POTWIERDZONY pusty gloshnik: brak kontenera, brak pozycji i IDLE.
            var confirmedEmpty = container is null
                && fresh.Value.CurrentItem is null
                && playback.Value!.PlaybackState == SonosPlaybackState.Idle;
            // NIEPELNA/NIEZNANA tozsamosc przy ISTNIEJACYM materiale: ani nazwa,
            // ani 200 nie zastepuja trojki. Odmowa, ZERO POST.
            return otherKnownMaterial || confirmedEmpty
                ? SonosPresetRepeatDecision.Load
                : SonosPresetRepeatDecision.Unavailable;
        }

        var state = playback.Value!.PlaybackState;
        // NIEZNANY stan przy ZGODNEJ tozsamosci: nie restartujemy wlasnego
        // materialu w ciemno (ta sama regula co dla wlasnej stacji).
        if (state == SonosPlaybackState.Unknown) return SonosPresetRepeatDecision.Unavailable;

        // TA SAMA bramka stanu co przy wlasnej stacji: tozsamosc rozstrzygnelo
        // juz Matches wyzej, a Decide odwzorowuje WYLACZNIE stan odtwarzania.
        // Klucze podajemy prawdziwe - objectId obu zgodnych trojek.
        return SonosPresetRepeat.Decide(
            wanted!.ObjectId, container!.Identity!.ObjectId, state, container.Identity is not null);
    }

    /// <summary>
    /// JEDEN POST presetu przez ISTNIEJACE granice zaplecza. Zwraca komunikat do
    /// powiedzenia albo <c>null</c>, gdy droga zostala zamknieta po cichu.
    /// </summary>
    private async Task<string?> SendSonosPresetLoadAsync(
        ISonosGroupSessionBackend backend,
        string? groupId,
        SessionPresetEntry preset,
        SonosOwnStreamSettings? station,
        CancellationToken token,
        Func<bool> isCurrent)
    {
        if (!isCurrent()) return null;
        if (station is not null)
        {
            if (backend is not ISonosOwnStreamsSessionBackend ownStreams)
                return "To połączenie Sonos nie obsługuje własnych stacji";
            if (!SonosSessionRequest.TryCreate(
                    "pl.amc.accessiblemultimediacontroller",
                    _sonosOwnStreamAppContext,
                    out var request))
            {
                return "Nie przygotowano żądania radia. Żadne polecenie nie zostało wysłane";
            }

            SonosPresetLoadsSentForTests++;
            var created = await ownStreams
                .CreateSessionAsync(groupId, request!, token).ConfigureAwait(true);
            if (!isCurrent()) return null;
            if (!created.TryGetSessionId(out var sessionId)) return created.Message;
            var loaded = await ownStreams.LoadStreamUrlAsync(
                sessionId,
                station.StreamUrl,
                // JAWNE play: zamiar "zagraj teraz", bez drugiego Play po fakcie.
                playOnCompletion: true,
                // NASZ itemId: ten sam klucz, ktorego szuka nastepne nacisniecie.
                itemId: SonosOwnStreamIdentity.TryComputeItemId(station.Id, station.StreamUrl),
                token).ConfigureAwait(true);
            // PRZYJECIE to nie dowod, ze gra - i tak to mowimy.
            return loaded.Accepted
                ? $"Sonos przyjął stację: {station.Name}. Odtwarzanie nie zostało jeszcze potwierdzone"
                : loaded.Message;
        }

        if (string.Equals(preset.TargetKind, SonosPresetKinds.Favorite, StringComparison.OrdinalIgnoreCase))
        {
            if (backend is not ISonosFavoriteLoadSessionBackend favorites)
                return "To połączenie Sonos nie obsługuje uruchamiania ulubionych";
            SonosPresetLoadsSentForTests++;
            var result = await favorites.LoadFavoriteAsync(
                groupId,
                preset.TargetId,
                // INSERT + playOnCompletion: ta SAMA akcja kolejki co Enter z listy.
                SonosFavoriteQueueAction.Insert,
                playOnCompletion: true,
                token).ConfigureAwait(true);
            return result.Accepted
                ? SonosFavoritesLabels.DescribePlayAccepted(preset.TargetTitle)
                : result.Message;
        }

        if (string.Equals(preset.TargetKind, SonosPresetKinds.Playlist, StringComparison.OrdinalIgnoreCase))
        {
            if (backend is not ISonosPlaylistLoadSessionBackend playlists)
                return "To połączenie Sonos nie obsługuje uruchamiania playlist";
            SonosPresetLoadsSentForTests++;
            var result = await playlists.LoadPlaylistAsync(
                groupId,
                preset.TargetId,
                SonosFavoriteQueueAction.Insert,
                playOnCompletion: true,
                token).ConfigureAwait(true);
            return result.Accepted
                ? SonosPlaylistsLabels.DescribePlayAccepted(preset.TargetTitle)
                : result.Message;
        }

        // NIEZNANY rodzaj: nie zgadujemy drogi odtwarzania.
        return SonosPresetLabels.AssignNeedsMaterial;
    }

    /// <summary>
    /// ISTNIEJACY jawny odczyt stanu po probie - zeby odtwarzacz i bramki nie
    /// zostaly ze starym stanem. Bez drugiego POST i bez ponawiania zapisu.
    /// </summary>
    private async Task RefreshSonosStateAfterPresetAsync()
    {
        try
        {
            await ReadSonosGroupStateAsync().ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Sonos: odczyt po presecie nie udal sie ({exception.GetType().Name}).");
        }
    }

    // ---------------------------------------------------------------- HOOKI POMIARU

    internal Task ActivateSonosPresetForTests(
        SessionPresetEntry preset,
        string slotLabel = "1",
        bool fromGlobalShortcut = false) =>
        ActivateSonosPresetAsync(preset, slotLabel, fromGlobalShortcut);

    internal void AssignSonosFavoritePresetForTests(SonosFavorite favorite, Window origin) =>
        AssignSonosFavoritePreset(favorite, origin);

    internal void AssignSonosPlaylistPresetForTests(SonosPlaylist playlist, Window origin) =>
        AssignSonosPlaylistPreset(playlist, origin);

    internal void AssignSonosOwnStreamPresetForTests(SonosOwnStreamSettings station, Window origin) =>
        AssignSonosOwnStreamPreset(station, origin);
}

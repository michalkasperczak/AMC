using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using AccessibleMediaController.Core.Sonos;
using AccessibleMediaController.Windows.Services;

namespace AccessibleMediaController.Windows;

/// <summary>
/// WYBOR GLOSNIKOW dla biezacego celu (Ctrl+F5 -> "Wybierz glosniki").
///
/// Co ta droga robi, w kolejnosci:
///  1. SWIEZY GET topologii PRZED otwarciem (stan grup bywa stary),
///  2. okno z PRAWDZIWYMI polami wyboru - czlonkowie celu juz zaznaczeni,
///  3. po "Zastosuj" JESZCZE JEDEN swiezy GET i plan z Core,
///  4. przy konflikcie (dolaczane glosniki GRAJA) nazwane pytanie,
///  5. DOKLADNIE JEDEN POST setGroupMembers z pelnym wybranym zestawem,
///  6. GET po POST i porownanie DOKLADNEGO zestawu playerIds.
///
/// Czego NIE ROBI: zadnego Play/Stop, zadnego ponownego ladowania materialu,
/// zadnych ponowien po niejednoznacznym wyniku i zadnego auto-wyboru celu.
///
/// OCHRONY: ta sama bramka <c>_sonosCommandInFlight</c> z biletem
/// <c>_sonosCommandGateTicket</c>, ten sam <c>ApplySonosAccountBinding</c> i ten
/// sam <c>_sonosTargetTicket</c> co pozostale polecenia - zadnej drugiej,
/// dziurawej bramy.
/// </summary>
public partial class MainWindow
{
    private SonosSpeakerSelectionWindow? _sonosSpeakersWindow;

    internal Task? LastSonosSpeakersTaskForTests { get; private set; }

    internal int SonosSpeakerWindowsCreatedForTests { get; private set; }

    /// <summary>Ile razy wybor glosnikow FAKTYCZNIE wyslal zmiane skladu.</summary>
    internal int SonosSpeakerChangesAppliedForTests { get; private set; }

    internal SonosSpeakerSelectionWindow? OpenSonosSpeakersWindowForTests => _sonosSpeakersWindow;

    /// <summary>TESTOWY punkt podstawienia POKAZANIA wyboru glosnikow (produkcyjnie modal).</summary>
    internal Action<SonosSpeakerSelectionWindow>? PresentSonosSpeakersOverrideForTests { get; set; }

    /// <summary>
    /// TESTOWE podstawienie PYTANIA O KONFLIKT. Produkcyjnie null, czyli
    /// prawdziwy <see cref="AccessibleDialog"/>.
    /// </summary>
    internal Func<string, bool>? ConfirmSonosSpeakerConflictOverrideForTests { get; set; }

    internal string? LastSonosSpeakerConflictQuestionForTests { get; private set; }

    internal void ShowSonosSpeakerSelectionForTests() =>
        LastSonosSpeakersTaskForTests = ShowSonosSpeakerSelectionAsync();

    /// <summary>
    /// WEJSCIE z okna celu. Wolane PO powrocie ze ShowDialog okna celu, wiec nie
    /// ma tu zagniezdzonego modalu w callbacku.
    /// </summary>
    private void StartSonosSpeakerSelection() =>
        LastSonosSpeakersTaskForTests = ShowSonosSpeakerSelectionAsync();

    private async Task ShowSonosSpeakerSelectionAsync()
    {
        if (_sonosSpeakersWindow is not null)
        {
            Announce("Wybór głośników Sonos jest już otwarty");
            try { _sonosSpeakersWindow.Activate(); } catch (InvalidOperationException) { }
            return;
        }

        if (!CanPresentSonosChildWindow())
        {
            Announce("Wybór głośników Sonos nie został otwarty, bo okno AMC nie jest aktywne. "
                + "Wróć do AMC i ponów skrót Control F5");
            return;
        }

        // GRANICA konta PRZED czymkolwiek: stary dom nie pojdzie przez nowe konto.
        if (ApplySonosAccountBinding())
        {
            Announce(SonosAccountChangedInstruction);
            return;
        }

        // KONTEKST ZLAPANY PRZY OTWARCIU - po kazdym await porownywany.
        var household = _state.Sonos.SelectedHouseholdId;
        if (string.IsNullOrWhiteSpace(household))
        {
            Announce(SonosSpeakerSelectionLabels.NoTargetSelected);
            return;
        }

        if (SonosActiveGroup is not { } group)
        {
            // BRAK AKTYWNEJ GRUPY: nie zgadujemy, skad skopiowac muzyke.
            Announce(SonosSpeakerSelectionLabels.NoTargetSelected);
            return;
        }

        var ticket = _sonosTargetTicket;
        var groupId = group.Id;

        // SWIEZY GET PRZED otwarciem: playbackState innych grup bywa stary.
        var before = await ReadSonosTopologyForSpeakersAsync(household!, ticket).ConfigureAwait(true);
        if (before is null) return;
        if (!CanPresentSonosChildWindow()) return;

        var window = new SonosSpeakerSelectionWindow(before, groupId);
        SonosSpeakerWindowsCreatedForTests++;
        _sonosSpeakersWindow = window;
        try
        {
            window.Owner = this;
            if (PresentSonosSpeakersOverrideForTests is { } present) present(window);
            else window.ShowDialog();
        }
        finally
        {
            _sonosSpeakersWindow = null;
        }

        // ANULOWANIE: ZERO POST i jawne zdanie.
        if (!window.Applied)
        {
            Announce(SonosSpeakerSelectionLabels.CanceledBeforeApply);
            return;
        }

        await ApplySonosSpeakerSelectionAsync(
            window.SelectedPlayerIds, household!, groupId, ticket, before).ConfigureAwait(true);
    }

    /// <summary>
    /// JEDNA intencja uzytkownika: swiezy odczyt, plan, ewentualne pytanie o
    /// konflikt, JEDEN POST i weryfikacja skladu.
    /// </summary>
    private async Task ApplySonosSpeakerSelectionAsync(
        IReadOnlyList<string> selected, string household, string groupId, int ticket,
        SonosHouseholdTopology openedTopology)
    {
        if (_isClosing) return;
        // TWARDA granica konta/domu/celu po modalu.
        if (ApplySonosAccountBinding() || ticket != _sonosTargetTicket)
        {
            Announce(SonosAccountChangedInstruction);
            return;
        }

        if (!string.Equals(_state.Sonos.SelectedHouseholdId, household, StringComparison.Ordinal)
            || !string.Equals(_state.Sonos.SelectedGroupId, groupId, StringComparison.Ordinal))
        {
            Announce(SonosSpeakerSelectionLabels.StaleSelection);
            return;
        }

        // JEDNA, WSPOLNA bramka polecen - nie druga, wlasna.
        if (_sonosCommandInFlight)
        {
            Announce("Poprzednie polecenie Sonos jeszcze się nie zakończyło");
            return;
        }

        if (EnsureSonosBackend() is not ISonosGroupMembershipSessionBackend membership)
        {
            // Zaplecze bez zmiany skladu: UCZCIWA odmowa, nie martwy przycisk.
            Announce("To zaplecze Sonos nie umie zmieniać składu grup, więc nic nie wysłano.");
            return;
        }

        var gateTicket = ++_sonosCommandGateTicket;
        _sonosCommandInFlight = true;
        try
        {
            // SWIEZY GET PRZED POST: stan grup zrodlowych i podzial glosnikow.
            var fresh = await ReadSonosTopologyForSpeakersAsync(household, ticket).ConfigureAwait(true);
            if (fresh is null) return;
            if (!SameSpeakerMembership(openedTopology, fresh))
            {
                Announce(SonosSpeakerSelectionLabels.StaleSelection);
                return;
            }

            if (!SonosSpeakerSelectionPlan.TryCreate(fresh, groupId, selected, out var plan, out var refusal)
                || plan is null)
            {
                // ODMOWA Z PRZYCZYNA, zero POST, zero ponowien.
                Announce(refusal);
                return;
            }

            // KONFLIKT: pytamy TYLKO gdy dolaczane glosniki GRAJA albo maja stan
            // nieznany. Idle/Paused bez konfliktu NIE pyta.
            if (plan.HasConflict
                && !ConfirmSonosSpeakerConflict(plan.ConflictNames, plan.ConflictUnknownState))
            {
                Announce(SonosSpeakerSelectionLabels.ConflictCanceled);
                return;
            }

            // Odpowiedź na pytanie nie zatwierdza zmian, które zaszły podczas modala.
            if (!SpeakerContextCurrent(household, ticket)) return;
            if (plan.HasConflict)
            {
                var rechecked = await ReadSonosTopologyForSpeakersAsync(household, ticket).ConfigureAwait(true);
                if (rechecked is null) return;
                if (!SameSpeakerMembership(fresh, rechecked)
                    || !SonosSpeakerSelectionPlan.TryCreate(rechecked, groupId, selected, out var currentPlan, out _)
                    || currentPlan is null
                    || currentPlan.ConflictUnknownState != plan.ConflictUnknownState
                    || !currentPlan.ConflictNames.SequenceEqual(plan.ConflictNames, StringComparer.Ordinal))
                {
                    Announce(SonosSpeakerSelectionLabels.StaleSelection);
                    return;
                }
            }
            if (!SpeakerContextCurrent(household, ticket)) return;
            var token = EnsureSonosCancellation().Token;
            var result = await membership
                .SetGroupMembersAsync(plan.SourceGroupId, plan.Players, token)
                .ConfigureAwait(true);

            SonosSpeakerChangesAppliedForTests++;
            await ReportSonosSpeakerOutcomeAsync(result, plan, household, ticket).ConfigureAwait(true);
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
    /// WERYFIKACJA SKLADU po POST. HTTP 200 nie jest dowodem: dowodem jest SWIEZY
    /// odczyt grupy o identyfikatorze Z ODPOWIEDZI i jej DOKLADNY zestaw
    /// playerIds. Brak dowodu to "wysłano, wynik niepotwierdzony" - nie "Gotowe".
    /// </summary>
    private async Task ReportSonosSpeakerOutcomeAsync(
        SonosGroupMembershipResult result,
        SonosSpeakerSelectionPlan plan,
        string household,
        int ticket)
    {
        if (!SpeakerContextCurrent(household, ticket)) return;
        if (!result.Accepted)
        {
            // Wynik odrzucony/porzucony: mowimy JEGO zdanie, bez ponawiania.
            Announce(result.Message);
            return;
        }

        // IDENTYFIKATOR Z ODPOWIEDZI, nie z nazwy. Gdy API go nie podalo,
        // dopuszczamy JEDNOZNACZNY swiezy odczyt grupy o pelnym zestawie.
        var hasId = result.TryGetGroupId(out var resultGroupId);

        if (_isClosing || ApplySonosAccountBinding() || ticket != _sonosTargetTicket)
        {
            // Nie przenosimy wyniku starego kontekstu do nowego konta.
            return;
        }

        var after = await ReadSonosTopologyForSpeakersAsync(household, ticket, afterWrite: true).ConfigureAwait(true);
        if (!SpeakerContextCurrent(household, ticket)) return;
        if (after is null)
        {
            Announce(SonosSpeakerSelectionLabels.SentNotConfirmed);
            return;
        }

        string? confirmedId = null;
        if (hasId && plan.Matches(after, resultGroupId))
        {
            confirmedId = resultGroupId;
        }
        else if (!hasId)
        {
            // BEZ identyfikatora z API: szukamy grupy o DOKLADNIE tym zestawie.
            // Jednoznacznosc jest warunkiem - dwie takie grupy to brak dowodu.
            var matching = after.Groups
                .Where(group => plan.Matches(after, group.Id))
                .ToArray();
            if (matching.Length == 1) confirmedId = matching[0].Id;
        }

        if (confirmedId is null)
        {
            // Skladu NIE DOWIEDZIONO: zadnego "Gotowe" i zadnego ponowienia.
            Announce(SonosSpeakerSelectionLabels.SentNotConfirmed);
            return;
        }

        // DOPIERO TERAZ publikujemy: topologia i NASTEPCZY cel AMC.
        ApplySonosTopologyForSpeakers(after, confirmedId);
        Announce(SonosSpeakerSelectionLabels.DescribeApplied(plan.DescribeNames(after)));
    }

    /// <summary>
    /// SWIEZY GET grup PRZECHOWANY LOKALNIE. Nie publikujemy go do modelu tutaj -
    /// publikacja nalezy do momentu, w ktorym wiemy, co sie stalo.
    /// </summary>
    private async Task<SonosHouseholdTopology?> ReadSonosTopologyForSpeakersAsync(
        string household, int ticket, bool afterWrite = false)
    {
        var backend = EnsureSonosBackend();
        var token = EnsureSonosCancellation().Token;
        SonosGroupsReadResult groups;
        try
        {
            groups = await backend.ReadGroupsAsync(household, token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return null;
        }

        if (!SpeakerContextCurrent(household, ticket)) return null;
        if (!groups.Succeeded || groups.Topology is null)
        {
            // Po POST caller musi podać wynik niepotwierdzony, nie odmowę wysłania.
            if (!afterWrite)
                Announce("Nie udało się odczytać głośników Sonos, więc nic nie wysłano. " + groups.Message);
            return null;
        }

        return groups.Topology;
    }

    /// <summary>
    /// PYTANIE O KONFLIKT przez ISTNIEJACY <see cref="AccessibleDialog"/>. Nazywa
    /// glosniki PELNYMI nazwami i nie zgaduje tytulow - nie wiemy, co tam gra.
    /// </summary>
    private bool SpeakerContextCurrent(string household, int ticket) =>
        !_isClosing && !ApplySonosAccountBinding() && ticket == _sonosTargetTicket
        && IsSonosSession(_sessions?.Current.Id)
        && string.Equals(household, _state.Sonos.SelectedHouseholdId, StringComparison.Ordinal);

    private static bool SameSpeakerMembership(SonosHouseholdTopology first, SonosHouseholdTopology second)
    {
        if (first.Partial || second.Partial) return false;
        if (!first.Players.Select(p => p.Id).OrderBy(id => id, StringComparer.Ordinal)
            .SequenceEqual(second.Players.Select(p => p.Id).OrderBy(id => id, StringComparer.Ordinal), StringComparer.Ordinal))
            return false;
        if (first.Groups.Count != second.Groups.Count) return false;
        foreach (var group in first.Groups)
        {
            var other = second.Groups.SingleOrDefault(g => string.Equals(g.Id, group.Id, StringComparison.Ordinal));
            if (other is null || !string.Equals(group.CoordinatorId, other.CoordinatorId, StringComparison.Ordinal)
                || !group.PlayerIds.OrderBy(id => id, StringComparer.Ordinal)
                    .SequenceEqual(other.PlayerIds.OrderBy(id => id, StringComparer.Ordinal), StringComparer.Ordinal))
                return false;
        }
        return true;
    }

    private bool ConfirmSonosSpeakerConflict(IReadOnlyList<string> names, bool unknownState)
    {
        var question = SonosSpeakerSelectionLabels.DescribeConflict(names, unknownState);
        LastSonosSpeakerConflictQuestionForTests = question;
        if (ConfirmSonosSpeakerConflictOverrideForTests is { } ask) return ask(question);

        return AccessibleDialog.Show(
            this,
            question,
            SonosSpeakerSelectionLabels.ConflictCaption,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No) == MessageBoxResult.Yes;
    }

    /// <summary>
    /// PUBLIKACJA po DOWIEDZIONEJ zmianie: topologia i NASTEPCZA grupa jako cel.
    /// Idzie ISTNIEJACA droga publikacji, zeby lista sterowania i wiersze grup
    /// zgadzaly sie z odczytem. ZADNEGO ponownego ladowania materialu.
    /// </summary>
    private void ApplySonosTopologyForSpeakers(SonosHouseholdTopology topology, string groupId)
    {
        _sonosTopology = topology;
        var sameGroup = string.Equals(_state.Sonos.SelectedGroupId, groupId, StringComparison.Ordinal);
        // CEL NASTEPCZEJ grupy: to WLASNA, zatwierdzona zmiana, wiec nie
        // odrzucamy jej jako "obcej" i nie podnosimy biletu celu - podniesienie
        // zwolniloby bramke innej operacji i uniewaznilo nasz wlasny wynik.
        _state.Sonos.SelectedGroupId = groupId;
        if (!sameGroup)
        {
            // Zmieniony groupId po poleceniu: stary tytul przy innym celu bylby mylacy.
            // Czyscimy CACHE odczytow, ale NIE ladujemy materialu od nowa.
            _sonosPlayback = null;
            _sonosMetadata = null;
            _sonosVolume = null;
        }

        _sonosNextBackgroundReadUtc = DateTime.MinValue;
        ApplySonosGroupRows();
        QueueStateSave(announceFailure: false);
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace AccessibleMediaController.Core.Sonos;

/// <summary>
/// WYBOR GLOSNIKOW jako SKLAD grupy sterowanej (Ctrl+F5 -> "Wybierz glosniki").
///
/// Co ta warstwa robi: z ODCZYTANEJ topologii, biezacego celu i zaznaczenia
/// uzytkownika wybiera JEDNA metode zapisu i JEDNA intencje. Nic tu nie wysyla,
/// nie czyta sieci i nie dotyka UI.
///
/// Czego NIE ROBI: nie rozbija zestawow stereo/kina (pozycja = LOGICZNY
/// glosnik z topology.Players, nigdy deviceId/SUB/satelita), nie wysyla
/// Play/Stop, nie zgaduje celu gdy go nie ma.
/// </summary>
public static class SonosSpeakerSelectionLabels
{
    public const string WindowTitle = "Wybór głośników Sonos";

    public const string OpenButtonLabel = "Wybierz głośniki";

    public const string ApplyButtonLabel = "Zastosuj";

    public const string SelectAllButtonLabel = "Zaznacz wszystkie";

    /// <summary>
    /// PIERWSZA tresc okna. Mowi wprost, ze ruch zaznaczeniem NIC nie zmienia i
    /// ze muzyka nie zostanie uruchomiona ani zatrzymana.
    /// </summary>
    public const string ViewIntroduction =
        "Wybór głośników, na których ma grać bieżący cel sterowania Sonos. "
        + "Spacja zaznacza i odznacza głośnik, Zastosuj wysyła jedną zmianę składu. "
        + "Zaznaczanie samo nic nie zmienia. AMC zmienia skład grupy bez ponownego uruchamiania materiału.";

    public const string NoTargetSelected =
        "Nie ma wybranego celu sterowania Sonos, więc nie wiadomo, który materiał przenieść. "
        + "Najpierw wybierz grupę jako cel, a potem wybierz głośniki.";

    public const string TopologyUnknown =
        "Lista głośników nie została jeszcze odczytana. Użyj Odśwież, żeby pobrać głośniki z Sonosa.";

    public const string PartialTopology =
        "Uwaga: Sonos oznaczył odczyt jako niepełny, więc na liście może brakować głośników. "
        + "Zmiany składu z niepełnego odczytu nie wysyłamy.";

    public const string EmptyState =
        "Ten dom Sonos nie ma odczytanych głośników, więc nie ma czego wybrać.";

    public const string NothingSelected =
        "Nie zaznaczono żadnego głośnika. Zaznacz co najmniej jeden głośnik spacją.";

    public const string UnchangedSelection =
        "Skład się nie zmienił, więc nic nie wysłano.";

    public const string CanceledBeforeApply =
        "Wybór głośników zamknięty bez zmian. Nic nie wysłano.";

    /// <summary>Nazwa zastepcza glosnika bez nazwy w odczycie - BEZ identyfikatora.</summary>
    public const string UnnamedPlayer = "Głośnik bez nazwy w odczycie";

    /// <summary>
    /// WYNIK POTWIERDZONY SWIEZYM ODCZYTEM: PELNE nazwy, zadnego "+1".
    /// </summary>
    public static string DescribeApplied(IReadOnlyList<string> playerNames)
    {
        ArgumentNullException.ThrowIfNull(playerNames);
        return "Wybrano: " + JoinNames(playerNames) + ".";
    }

    /// <summary>
    /// WYSLANE, ale SKLADU NIE DOWIEDZIONO. Celowo NIE mowi "Gotowe" i nie
    /// zacheca do ponowienia - zlecenie moglo juz zadzialac.
    /// </summary>
    public const string SentNotConfirmed =
        "Polecenie wysłano, wynik niepotwierdzony. Odczytaj głośniki ponownie, "
        + "żeby zobaczyć rzeczywisty skład grupy.";

    public static string DescribeSelectionCount(int selected, int total) =>
        "Zaznaczono "
        + selected.ToString(CultureInfo.CurrentCulture)
        + " z "
        + total.ToString(CultureInfo.CurrentCulture)
        + (total == 1 ? " głośnika" : " głośników");

    /// <summary>
    /// PYTANIE O KONFLIKT: dolaczane glosniki GRAJA cos innego. Nazywa je PELNYMI
    /// nazwami i nie zgaduje tytulow - nie wiemy, co tam leci.
    /// </summary>
    public static string DescribeConflict(IReadOnlyList<string> playingNames, bool unknownState)
    {
        ArgumentNullException.ThrowIfNull(playingNames);
        var head = playingNames.Count == 1
            ? "Głośnik " + JoinNames(playingNames) + " należy teraz do innej grupy, która "
            : "Głośniki " + JoinNames(playingNames) + " należą teraz do innych grup, które ";
        var middle = playingNames.Count == 1
            ? unknownState
                ? "może coś odtwarzać - Sonos nie podał jej stanu."
                : "coś odtwarza."
            : unknownState
                ? "mogą coś odtwarzać - Sonos nie podał ich stanu."
                : "coś odtwarzają.";
        return head + middle
            + " Dołączenie do bieżącego celu zastąpi to, co tam gra. Kontynuować?";
    }

    public const string ConflictCaption = "Głośniki grają coś innego";

    public const string ConflictCanceled =
        "Zmianę składu głośników anulowano. Nic nie wysłano.";

    /// <summary>
    /// ODMOWA, gdy wybor przestal byc aktualny miedzy otwarciem a Zastosuj.
    /// Zadnych ponowien i zadnego zapisu "na slepo".
    /// </summary>
    public const string StaleSelection =
        "Skład głośników Sonos zmienił się w trakcie wyboru, więc nic nie wysłano. "
        + "Otwórz wybór głośników jeszcze raz.";

    public static string JoinNames(IReadOnlyList<string> names)
    {
        ArgumentNullException.ThrowIfNull(names);
        return names.Count switch
        {
            0 => UnnamedPlayer,
            1 => names[0],
            2 => names[0] + " i " + names[1],
            _ => string.Join(", ", names.Take(names.Count - 1)) + " i " + names[^1]
        };
    }

    /// <summary>
    /// PELNA nazwa LOGICZNEGO glosnika z odczytu. Zestaw stereo/kino ma w
    /// topologii JEDNA pozycje, wiec jedna nazwa - nie rozbijamy go na satelity.
    /// </summary>
    public static string DescribePlayer(SonosPlayer player)
    {
        ArgumentNullException.ThrowIfNull(player);
        return string.IsNullOrWhiteSpace(player.Name) ? UnnamedPlayer : player.Name;
    }
}

/// <summary>Pełny nowy skład aktualnej grupy, bez kopiowania jej muzyki.</summary>
public enum SonosSpeakerSelectionMethod
{
    SetGroupMembers
}

/// <summary>
/// PLAN JEDNEJ zmiany skladu: metoda, cel i dokladny zestaw glosnikow. Powstaje
/// TYLKO ze swiezego odczytu; kazda niezgodnosc to ODMOWA z przyczyna, nigdy
/// auto-wybor.
/// </summary>
public sealed class SonosSpeakerSelectionPlan
{
    private SonosSpeakerSelectionPlan(
        SonosSpeakerSelectionMethod method,
        string sourceGroupId,
        SonosPlayerSet players,
        IReadOnlyList<string> playerIds,
        IReadOnlyList<string> conflictNames,
        bool conflictUnknownState)
    {
        Method = method;
        SourceGroupId = sourceGroupId;
        Players = players;
        PlayerIds = playerIds;
        ConflictNames = conflictNames;
        ConflictUnknownState = conflictUnknownState;
    }

    public SonosSpeakerSelectionMethod Method { get; }

    /// <summary>Aktualna grupa, której pełny skład zastępujemy.</summary>
    public string SourceGroupId { get; }

    public SonosPlayerSet Players { get; }

    /// <summary>Zestaw w KOLEJNOSCI odczytu - do porownania skladu po POST.</summary>
    public IReadOnlyList<string> PlayerIds { get; }

    /// <summary>
    /// PELNE nazwy dolaczanych glosnikow, ktorych grupy GRAJA albo maja stan
    /// nieznany. Pusta lista = brak konfliktu = ZADNEGO pytania.
    /// </summary>
    public IReadOnlyList<string> ConflictNames { get; }

    /// <summary>Czy w konflikcie jest stan NIEZNANY (a nie tylko jawne granie).</summary>
    public bool ConflictUnknownState { get; }

    public bool HasConflict => ConflictNames.Count > 0;

    /// <summary>
    /// Buduje plan albo ODMAWIA z powodem. <paramref name="selectedPlayerIds"/>
    /// to zaznaczenie uzytkownika; <paramref name="currentGroupId"/> to biezacy
    /// cel AMC. Niepelna topologia, brak celu, puste zaznaczenie i nieznany
    /// glosnik to ODMOWY - nie poprawiamy wejscia za uzytkownika.
    /// </summary>
    public static bool TryCreate(
        SonosHouseholdTopology? topology,
        string? currentGroupId,
        IReadOnlyList<string>? selectedPlayerIds,
        out SonosSpeakerSelectionPlan? plan,
        out string refusal)
    {
        plan = null;

        if (topology is null)
        {
            refusal = SonosSpeakerSelectionLabels.TopologyUnknown;
            return false;
        }

        // NIEPELNY odczyt: brakujacy glosnik moglby wypasc ze skladu, a
        // setGroupMembers ZASTEPUJE caly zestaw. Nie wysylamy.
        if (topology.Partial)
        {
            refusal = SonosSpeakerSelectionLabels.PartialTopology;
            return false;
        }

        if (selectedPlayerIds is null || selectedPlayerIds.Count == 0)
        {
            refusal = SonosSpeakerSelectionLabels.NothingSelected;
            return false;
        }

        var current = SonosActiveGroupPolicy.Resolve(currentGroupId, topology);
        if (current is null)
        {
            refusal = SonosSpeakerSelectionLabels.NoTargetSelected;
            return false;
        }

        var playersById = topology.Players.ToDictionary(player => player.Id, StringComparer.Ordinal);

        // ZAZNACZENIE musi w calosci pochodzic z TEGO odczytu: nieznany
        // identyfikator znaczy, ze podzial logicznych glosnikow sie rozjechal.
        foreach (var playerId in selectedPlayerIds)
        {
            if (!playersById.ContainsKey(playerId))
            {
                refusal = SonosSpeakerSelectionLabels.StaleSelection;
                return false;
            }
        }

        if (!SonosPlayerSet.TryCreate(selectedPlayerIds.Cast<string?>().ToArray(), out var players))
        {
            refusal = SonosSpeakerSelectionLabels.StaleSelection;
            return false;
        }

        // Zastępujemy pełny skład istniejącej grupy także wtedy, gdy poprzedni
        // koordynator wypada. Kopia kontekstu do nowej grupy nie dowodzi przeniesienia audio.
        var method = SonosSpeakerSelectionMethod.SetGroupMembers;

        var (conflicts, unknown) = DescribeConflicts(topology, current, selectedPlayerIds);

        plan = new SonosSpeakerSelectionPlan(
            method,
            current.Id,
            players!,
            new ReadOnlyCollection<string>(selectedPlayerIds.ToList()),
            conflicts,
            unknown);
        refusal = string.Empty;
        return true;
    }

    /// <summary>
    /// KONFLIKT: zaznaczone glosniki, ktore SIEDZA w INNEJ grupie grajacej,
    /// buforujacej albo o stanie NIEZNANYM. Idle i Paused konfliktem NIE sa -
    /// pytanie w bezkonfliktowym stanie byloby halasem.
    /// </summary>
    private static (IReadOnlyList<string> Names, bool Unknown) DescribeConflicts(
        SonosHouseholdTopology topology,
        SonosGroup current,
        IReadOnlyList<string> selectedPlayerIds)
    {
        var names = new List<string>();
        var unknown = false;
        var playersById = topology.Players.ToDictionary(player => player.Id, StringComparer.Ordinal);

        foreach (var group in topology.Groups)
        {
            if (string.Equals(group.Id, current.Id, StringComparison.Ordinal)) continue;

            var busy = group.PlaybackState is SonosPlaybackState.Playing
                or SonosPlaybackState.Buffering or SonosPlaybackState.Unknown;
            if (!busy) continue;

            foreach (var playerId in group.PlayerIds)
            {
                if (!selectedPlayerIds.Contains(playerId, StringComparer.Ordinal)) continue;
                // Glosnik WCHODZI do naszego celu, a jego obecna grupa gra.
                names.Add(playersById.TryGetValue(playerId, out var player)
                    ? SonosSpeakerSelectionLabels.DescribePlayer(player)
                    : SonosSpeakerSelectionLabels.UnnamedPlayer);
                if (group.PlaybackState == SonosPlaybackState.Unknown) unknown = true;
            }
        }

        return (new ReadOnlyCollection<string>(names), unknown);
    }

    /// <summary>
    /// SPRAWDZENIE SKLADU po POST na SWIEZYM odczycie. Dowodem jest DOKLADNY
    /// zestaw playerIds grupy o <paramref name="groupId"/> - nie nazwa, nie
    /// liczba i nie HTTP 200.
    /// </summary>
    public bool Matches(SonosHouseholdTopology? topology, string? groupId)
    {
        if (topology is null || topology.Partial || string.IsNullOrEmpty(groupId)) return false;

        var group = topology.Groups
            .FirstOrDefault(candidate => string.Equals(candidate.Id, groupId, StringComparison.Ordinal));
        if (group is null) return false;

        return group.PlayerIds.Count == PlayerIds.Count
            && group.PlayerIds.OrderBy(id => id, StringComparer.Ordinal)
                .SequenceEqual(PlayerIds.OrderBy(id => id, StringComparer.Ordinal), StringComparer.Ordinal);
    }

    /// <summary>PELNE nazwy skladu z odczytu - do komunikatu "Wybrano: ...".</summary>
    public IReadOnlyList<string> DescribeNames(SonosHouseholdTopology topology)
    {
        ArgumentNullException.ThrowIfNull(topology);
        var playersById = topology.Players.ToDictionary(player => player.Id, StringComparer.Ordinal);
        return PlayerIds
            .Select(id => playersById.TryGetValue(id, out var player)
                ? SonosSpeakerSelectionLabels.DescribePlayer(player)
                : SonosSpeakerSelectionLabels.UnnamedPlayer)
            .ToArray();
    }

    /// <summary>Kontrolowane ToString: BEZ identyfikatorow grupy i glosnikow.</summary>
    public override string ToString() =>
        "Plan składu Sonos (" + Method + "): głośników "
        + PlayerIds.Count.ToString(CultureInfo.InvariantCulture)
        + (HasConflict ? ", konflikt z grającą grupą" : ", bez konfliktu");
}

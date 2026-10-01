using System;
using System.Threading;
using System.Threading.Tasks;

namespace AccessibleMediaController.Core.Sonos;

/// <summary>
/// ZMIANA SKLADU GRUP SONOSA widziana przez KONTO - dwa cienkie opakowania na
/// <see cref="ISonosGroupMembershipApi"/>.
///
/// Caly mechanizm poswiadczen jest ISTNIEJACY: oba wywolania ida przez
/// <c>RunSessionWriteAsync</c> z <c>SonosAccountCoordinator.PlaybackSession.cs</c>,
/// czyli bilet pod blokada, odnowienie TYLKO PRZED wyslaniem i tylko przy znanej
/// minionej waznosci, potem JEDEN POST, po 401 ZERO ponowien i ZERO odnowien,
/// na koniec kontrola generacji poswiadczen. Tutaj nie ma wlasnej obslugi
/// tokenu, wlasnej blokady ani wlasnego licznika generacji.
///
/// Ten generyczny przebieg jest celowo wspolny dla WSZYSTKICH zapisow, ktore
/// oddaja DANE z odpowiedzi - sesji odtwarzania i teraz skladu grup. Dzieki temu
/// ochrona generacji i biletu nie jest przepisywana po raz trzeci.
///
/// Zmiana konta w trakcie trwajacego POST daje <c>Discarded</c>: identyfikator
/// grupy zdobyty na koncie A NIE wychodzi do kontekstu konta B. Porzucenie NIE
/// kasuje konta i NIE jest cofnieciem - gdy zadanie poszlo, sklad grup moze byc
/// juz zmieniony.
///
/// Anulowanie PO wyslaniu tez nie jest obietnica cofniecia.
///
/// Czego tu NIE MA: UI, wyboru glosnikow, pamieci "ostatniej grupy", odczytu
/// topologii przed i po (to robi wolajacy istniejacym getGroups), autostartu,
/// Play/Stop i drugiego POST.
/// </summary>
public sealed partial class SonosAccountCoordinator
{
    /// <summary>
    /// createGroup przez bilet biezacego konta - JAWNE utworzenie grupy z
    /// podanego zestawu logicznych glosnikow.
    ///
    /// Wolno wywolac TYLKO w odpowiedzi na jawne polecenie uzytkownika: to
    /// przestawia glosniki w domu i moze przerwac to, czego ktos sluchal.
    ///
    /// <paramref name="request"/> podaje WOLAJACY, razem z decyzja o
    /// musicContextGroupId (brak = grupa bez dzwieku). Koordynator nie
    /// podstawia tu zadnej zapamietanej wartosci.
    /// </summary>
    public async Task<SonosGroupMembershipResult> CreateGroupAsync(
        ISonosGroupMembershipApi api,
        string? householdId,
        SonosCreateGroupRequest? request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(api);
        return await RunSessionWriteAsync(
            (token, ct) => api.CreateGroupAsync(token, householdId, request, ct),
            static outcome => outcome.Sent,
            static (status, outcome, sent, renewed, snapshot) =>
                new SonosGroupMembershipResult(
                    SonosGroupMembershipOperation.CreateGroup, status, outcome, sent, renewed, snapshot),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// setGroupMembers przez bilet biezacego konta - ZASTAPIENIE skladu
    /// istniejacej grupy PELNYM, jawnym zestawem glosnikow.
    ///
    /// <paramref name="groupId"/> podaje wolajacy: koordynator NIE przechowuje
    /// "biezacej grupy" i nie podstawia zapamietanej. Gdy grupa w tym czasie
    /// przestala istniec, wynikiem jest blad - nie ciche utworzenie nowej grupy
    /// (to byloby inne polecenie, createGroup).
    /// </summary>
    public async Task<SonosGroupMembershipResult> SetGroupMembersAsync(
        ISonosGroupMembershipApi api,
        string? groupId,
        SonosPlayerSet? players,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(api);
        return await RunSessionWriteAsync(
            (token, ct) => api.SetGroupMembersAsync(token, groupId, players, ct),
            static outcome => outcome.Sent,
            static (status, outcome, sent, renewed, snapshot) =>
                new SonosGroupMembershipResult(
                    SonosGroupMembershipOperation.SetGroupMembers, status, outcome, sent, renewed, snapshot),
            cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Wynik ZMIANY SKLADU GRUPY widziany przez konto. Te same granice co wynik
/// sesji odtwarzania: proba / przyjecie / nieznany skutek, zadnych danych z
/// porzuconego wyniku - a identyfikator grupy wychodzi TYLKO z waznego wyniku
/// BIEZACEGO konta.
/// </summary>
public sealed class SonosGroupMembershipResult
{
    internal SonosGroupMembershipResult(
        SonosGroupMembershipOperation operation,
        SonosGroupOperationStatus status,
        SonosGroupMembershipOutcome? outcome,
        bool requestSent,
        bool renewed,
        SonosAccountSnapshot snapshot)
    {
        Operation = operation;
        Status = status;
        // Przy PORZUCENIU i przy braku konta nie ma wyniku transportu, wiec nie
        // ma tez zadnego identyfikatora grupy do oddania.
        Outcome = status == SonosGroupOperationStatus.Attempted ? outcome : null;
        RequestSent = requestSent;
        Renewed = renewed;
        Snapshot = snapshot;
    }

    /// <summary>Ktora z dwoch operacji skladu byla wywolana.</summary>
    public SonosGroupMembershipOperation Operation { get; }

    public SonosGroupOperationStatus Status { get; }

    /// <summary>Wynik transportu TYLKO gdy proba doszla do skutku na biezacym koncie.</summary>
    public SonosGroupMembershipOutcome? Outcome { get; }

    /// <summary>Czy zadanie HTTP w ogole opuscilo aplikacje.</summary>
    public bool RequestSent { get; }

    /// <summary>Czy po drodze uzyto ISTNIEJACEGO odnowienia dostepu (najwyzej raz, PRZED wyslaniem).</summary>
    public bool Renewed { get; }

    public SonosAccountSnapshot Snapshot { get; }

    /// <summary>Sonos PRZYJAL zlecenie. NIE znaczy, ze sklad grupy jest taki.</summary>
    public bool Accepted => Outcome?.Accepted == true;

    /// <summary>
    /// Przyjete ORAZ z uzytecznym identyfikatorem grupy. HTTP 200 bez obiektu
    /// grupy tym NIE jest - pole group jest w definicji nullable.
    /// </summary>
    public bool HasGroupId => Outcome?.HasGroupId == true;

    /// <summary>
    /// Zawsze false: ani przyjecie zlecenia, ani zwrocony identyfikator nie
    /// dowodza, ze w grupie sa dokladnie te glosniki. Pokaze to tylko SWIEZY
    /// odczyt topologii, ktory nalezy do wolajacego.
    /// </summary>
    public bool EffectConfirmed => false;

    /// <summary>Wynik SPOZNIONY: konto zmienilo sie w trakcie, danych nie publikujemy.</summary>
    public bool Discarded => Status == SonosGroupOperationStatus.Discarded;

    /// <summary>
    /// Skutek NIEZNANY: zapis poszedl w siec bez odpowiedzi albo wynik zostal
    /// porzucony PO wyslaniu. Sklad grup moze byc JUZ zmieniony. Nie wolno tego
    /// ponawiac automatycznie.
    /// </summary>
    public bool EffectAmbiguous => Outcome?.EffectAmbiguous == true || (Discarded && RequestSent);

    public string Message => Status switch
    {
        SonosGroupOperationStatus.NoAccount => Operation == SonosGroupMembershipOperation.CreateGroup
            ? "Nie ma połączonego konta Sonos, więc nie ma gdzie utworzyć grupy."
            : "Nie ma połączonego konta Sonos, więc nie ma czego przegrupować.",
        SonosGroupOperationStatus.Unauthorized =>
            "Nie udało się odnowić dostępu do konta Sonos, więc żądania zmiany głośników nie wysłano.",
        SonosGroupOperationStatus.Canceled => "Zmianę głośników Sonos anulowano przed wysłaniem.",
        SonosGroupOperationStatus.Discarded => RequestSent
            ? "Kontekst konta Sonos zmienił się po wysłaniu żądania: wyniku nie używamy, "
              + "a skutek pozostaje nieznany. Odczytaj głośniki ponownie."
            : "Żądanie zmiany głośników Sonos pominięto: kontekst konta zmienił się w trakcie.",
        _ => Outcome?.Message ?? "Stan żądania zmiany głośników Sonos jest nieznany."
    };

    /// <summary>
    /// Oddaje FAKTYCZNY identyfikator grupy z odpowiedzi - ale TYLKO z waznego
    /// wyniku biezacego konta i tylko gdy da sie go uzyc w sciezce kolejnego
    /// polecenia.
    ///
    /// Dla createGroup moze to byc identyfikator INNY albo ISTNIEJACY (gdy
    /// zadana grupa jest nadzbiorem istniejacej) - dlatego wolajacy ma uzyc
    /// TEJ wartosci, a nie tej, ktora wyslal.
    ///
    /// Zwraca false dla porzuconego wyniku (zmiana konta), dla braku konta, dla
    /// bledu i dla HTTP 200 bez obiektu grupy. To JEDYNA droga wyjscia
    /// identyfikatora z tej warstwy.
    /// </summary>
    public bool TryGetGroupId(out string? groupId)
    {
        groupId = null;
        if (Status != SonosGroupOperationStatus.Attempted || Outcome?.HasGroupId != true)
        {
            return false;
        }

        groupId = Outcome.Info!.Group!.Id;
        return true;
    }

    /// <summary>
    /// WYLACZNIE DO POMIARU: gotowy wynik bez konta, tokenu i transportu.
    /// Migawka jest PUSTA, wiec nikt nie pomyli tego z prawdziwym kontem.
    /// </summary>
    public static SonosGroupMembershipResult CreateForMeasurement(
        SonosGroupMembershipOperation operation,
        SonosGroupOperationStatus status,
        SonosGroupMembershipOutcome? outcome,
        bool requestSent) =>
        new(operation, status, outcome, requestSent, renewed: false, SonosAccountSnapshots.Empty);

    /// <summary>
    /// Kontrolowane ToString: BEZ identyfikatora grupy, BEZ identyfikatorow
    /// glosnikow i bez tokenu. Odczytany identyfikator nie moze trafic do logu.
    /// </summary>
    public override string ToString() =>
        "Zmiana składu grupy Sonos przez konto (" + Operation + "): " + Status
        + (RequestSent ? ", żądanie wysłane" : ", żądania nie wysłano")
        + (HasGroupId ? ", z identyfikatorem grupy" : ", bez identyfikatora grupy")
        + (EffectAmbiguous ? ", skutek nieznany" : string.Empty)
        + (Renewed ? ", po odnowieniu dostępu" : string.Empty);
}

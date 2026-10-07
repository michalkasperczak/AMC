using AccessibleMediaController.Core.Commands;

namespace AccessibleMediaController.Core.Sonos;

/// <summary>
/// CZY SPACJA MUSI NAJPIERW ODCZYTAC STAN, zanim bramka orzeknie odmowe.
///
/// ZGLOSZENIE MICHALA: "w Moich stacjach komunikat po wcisnieciu spacji podczas
/// odtwarzania Sonos nie zglasza mozliwosci zatrzymania tego materialu. Ale potem
/// juz bylo OK i Buforowanie/zatrzymano."
///
/// MECHANIZM: po uruchomieniu wlasnej stacji odczyt lapie grupe w stanie
/// BUFFERING, gdzie <c>availablePlaybackActions</c> jeszcze nie zglasza ani
/// canPause, ani canStop. Ta KOPIA zostaje w pamieci. Pierwsza Spacja ocenia sie
/// z niej i konczy KATEGORYCZNA odmowa "Sonos nie zglasza mozliwosci zatrzymania
/// tego materialu", mimo ze AKTUALNIE grupa gra i canStop jest juz prawdziwe.
/// Druga Spacja trafia na odswiezony stan - i dlatego "potem juz bylo OK".
///
/// ROZSTRZYGNIECIE: transient i nieznane NIE sa zgodą, ale TEZ NIE sa dowodem
/// odmowy. Zamiast usuwac bramke (to wysylaloby slepe pause) albo przyjmowac
/// Unknown (to klamaloby o uprawnieniu), robimy SWIEZY ODCZYT i oceniamy bramke
/// PONOWNIE. Odmowa po swiezym odczycie zostaje odmowa.
///
/// GRANICE: dotyczy WYLACZNIE intencji zatrzymania/odtworzenia (Spacja i Enter na
/// wierszu grupy). Kroki glosnosci NIE dostaja tu dodatkowego GET - uzytkownik
/// chwalil ich szybkosc, a ich bramka patrzy na <c>volume.fixed</c>, ktore nie
/// zmienia sie w rytmie buforowania.
/// </summary>
public static class SonosPlayPauseRefresh
{
    /// <summary>Czy to intencja Spacji (albo Entera na wierszu grupy).</summary>
    public static bool IsTransportToggle(string? commandId) =>
        commandId is CommandIds.PlayPause or CommandIds.ActivateSelected;

    /// <summary>
    /// STAN PRZEJSCIOWY: kopia zrobiona w takiej chwili nie opisuje tego, co
    /// uzytkownik slyszy w momencie nacisniecia klawisza.
    /// </summary>
    public static bool IsTransient(SonosPlaybackState state) =>
        state is SonosPlaybackState.Unknown or SonosPlaybackState.Buffering;

    /// <summary>
    /// Czy przed decyzja trzeba ODCZYTAC stan na nowo.
    ///
    /// <paramref name="cachedGateAllows"/> to wynik bramki policzony z PAMIECI.
    /// SZYBKA SCIEZKA: pewna zgoda na stanie ustalonym (Playing/Paused) idzie
    /// prosto do POST-u, bez dodatkowego GET - Spacja nie ma zwolnic.
    /// ODCZYT dokladamy, gdy pamiec ODMAWIA albo gdy jest przejsciowa/niepelna,
    /// bo tylko wtedy odmowa moglaby byc nieprawdziwa.
    /// </summary>
    public static bool NeedsFreshRead(
        string? commandId,
        bool cachedGateAllows,
        SonosPlaybackState cachedState,
        SonosPlaybackActions? cachedActions)
    {
        if (!IsTransportToggle(commandId)) return false;
        if (cachedActions is null) return true;
        if (IsTransient(cachedState)) return true;
        return !cachedGateAllows;
    }

    /// <summary>
    /// Komunikat, gdy SWIEZEGO odczytu nie udalo sie wykonac. Nie udajemy ani
    /// skutku HTTP 200, ani kategorycznego braku mozliwosci: polecenie NIE
    /// zostalo wyslane i to jest cala prawda.
    /// </summary>
    public const string ReadFailed =
        "Nie udało się odczytać stanu Sonosa. Polecenia nie wysłano";
}

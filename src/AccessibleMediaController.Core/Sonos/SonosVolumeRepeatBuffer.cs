namespace AccessibleMediaController.Core.Sonos;

/// <summary>
/// SKUMULOWANA INTENCJA REGULACJI GLOSNOSCI dla SZYBKICH POWTORZEN.
///
/// Zgloszenie Michala: "jak chce sciszyc Ctrl+Windows+strzalka w dol - Poprzednie
/// polecenie Sonos jeszcze sie nie zakonczylo". Dodatek NVDA dostarcza intencje
/// POPRAWNIE (w logu widac serie wpisow nvda-bridge volumeDown w krotkich
/// odstepach) - to bramka jednego polecenia AMC ODRZUCALA kazde nacisniecie,
/// ktore trafilo w trwajacy POST. Samo wyciszenie komunikatu NIE naprawia
/// zgubionych nacisniec, wiec intencja musi zostac ZAPAMIETANA i wykonana.
///
/// GRANICE, swiadomie wezsze niz "kolejka polecen":
/// - TYLKO KROKI GLOSNOSCI. Pauza, skip, wyciszenie i przewijanie nadal dostaja
///   jawna odmowe: ich nie wolno kumulowac, bo dwa razy pauza to nie "mocniej".
/// - TYLKO TA SAMA GRUPA. Delta policzona dla salonu nie ma prawa dojsc do
///   kuchni, wiec zmiana celu PORZUCA nagromadzona intencje.
/// - JEDNA liczba, nie lista zadan: dowolnie dluga seria zwija sie w JEDNA
///   docelowa glosnosc. Kolejka jest wiec OGRANICZONA z definicji - nie rosnie
///   z liczba nacisniec i nie da sie jej zalac.
/// - ZERO wysylania i ZERO odczytow tutaj: ta klasa nie zna HTTP. Wolajacy
///   serializuje POST-y i dopiero po swoim odczycie pyta o reszte intencji,
///   dzieki czemu nie ma zapisow rownoleglych ani wykonanych nie po kolei.
/// - Limit <see cref="MaxDrains"/> domyka petle takze wtedy, gdy ktos trzyma
///   klawisz bez konca: wolajacy po tylu przebiegach konczy i mowi werdykt.
/// </summary>
public sealed class SonosVolumeRepeatBuffer
{
    /// <summary>Ile razy wolno DOLAC nagromadzona intencje do jednej operacji.</summary>
    public const int MaxDrains = 12;

    private string? _groupId;
    private int _pendingDelta;
    private int _drains;

    /// <summary>
    /// POZIOM, od ktorego liczy sie oczekujaca delta: ostatnia WYSLANA glosnosc.
    /// Bez niego suma krokow gubila ZMIANE KIERUNKU na brzegu - przy 0 seria
    /// "dol, dol, gora" zwijala sie do -5, czyli nadal 0, choc uzytkownik
    /// skonczyl na kroku W GORE i spodziewa sie 5.
    /// </summary>
    private int? _baseVolume;

    /// <summary>Czy to KROK glosnosci (jedyne polecenie, ktore wolno kumulowac).</summary>
    public static bool IsVolumeStep(string? commandId) => StepDelta(commandId) != 0;

    /// <summary>KROK z MAPOWANIA polecen, nie z zalozenia: 5 albo 1, w gore albo w dol.</summary>
    public static int StepDelta(string? commandId) => commandId switch
    {
        Commands.CommandIds.VolumeUp5 => 5,
        Commands.CommandIds.VolumeDown5 => -5,
        Commands.CommandIds.VolumeUp1 => 1,
        Commands.CommandIds.VolumeDown1 => -1,
        _ => 0
    };

    /// <summary>
    /// DOCELOWA glosnosc z ODCZYTANEGO poziomu i sumy krokow. Zakres Sonosa jest
    /// domknięty: 0 i 100 to poprawne wartosci, nie blad do zgloszenia.
    /// </summary>
    public static int ResolveTarget(int currentVolume, int delta) =>
        Math.Clamp(currentVolume + delta, SonosGroupVolume.MinVolume, SonosGroupVolume.MaxVolume);

    /// <summary>Czy cos czeka na dolaczenie do trwajacej operacji.</summary>
    public bool HasPending => _pendingDelta != 0;

    /// <summary>Suma krokow czekajacych na wykonanie - do pomiaru i do diagnostyki.</summary>
    public int PendingDelta => _pendingDelta;

    /// <summary>
    /// START operacji glosnosci na grupie. Od tej chwili kolejne kroki TEJ SAMEJ
    /// grupy maja gdzie sie odlozyc. <paramref name="baseVolume"/> to poziom,
    /// ktory wlasnie wysylamy - oczekujace kroki licza sie OD NIEGO, wiec seria ze
    /// zmiana kierunku na brzegu 0/100 nie gubi ostatniej intencji.
    /// </summary>
    public void Begin(string? groupId, int? baseVolume = null)
    {
        _groupId = groupId;
        _pendingDelta = 0;
        _drains = 0;
        _baseVolume = baseVolume;
    }

    /// <summary>
    /// NOWY punkt odniesienia po UDANYM zapisie: dalsze kroki licza sie od
    /// poziomu, ktory Sonos naprawde dostal.
    /// </summary>
    public void NoteVolumePosted(int volume) => _baseVolume = volume;

    /// <summary>
    /// POWTORZENIE w czasie trwajacej operacji. <c>true</c> znaczy "intencja
    /// zapamietana, nie mow o zajetosci"; <c>false</c> oddaje gest zwyklej
    /// odmowie - inne polecenie, inna grupa albo nic nie trwa.
    /// </summary>
    public bool TryAccumulate(string? commandId, string? groupId)
    {
        if (_groupId is null) return false;
        if (!string.Equals(_groupId, groupId, StringComparison.Ordinal)) return false;
        var delta = StepDelta(commandId);
        if (delta == 0) return false;
        if (_baseVolume is { } baseVolume)
        {
            // CLAMP WZGLEDEM POZIOMU, nie wzgledem +-100: przy znanym punkcie
            // odniesienia suma nie moze wyjsc poza zakres urzadzenia, wiec na 0
            // kolejne "dol" nie buduja dlugu, ktory zjadlby pozniejsze "gora".
            var reachable = Math.Clamp(
                baseVolume + _pendingDelta + delta,
                SonosGroupVolume.MinVolume,
                SonosGroupVolume.MaxVolume);
            _pendingDelta = reachable - baseVolume;
            return true;
        }

        // Suma celowo NIE jest przycinana do 0..100 tutaj: dopiero docelowa
        // glosnosc liczy sie od ODCZYTANEGO poziomu. Ograniczamy jednak jej
        // wartosc bezwzgledna, zeby trzymany klawisz nie przekrecil licznika.
        _pendingDelta = Math.Clamp(
            _pendingDelta + delta,
            -SonosGroupVolume.MaxVolume,
            SonosGroupVolume.MaxVolume);
        return true;
    }

    /// <summary>
    /// ODBIOR nagromadzonej intencji do NASTEPNEGO, pojedynczego POST-u. Zwraca 0,
    /// gdy nie ma czego dosylac, gdy cel sie zmienil albo gdy przekroczono
    /// <see cref="MaxDrains"/>.
    ///
    /// L3 z odbioru po 4.2.1: limit DAWNIEJ ZEROWAL oczekujaca delte, czyli robil
    /// dokladnie to, czemu ta klasa ma zapobiegac - GUBIL intencje uzytkownika.
    /// Teraz limit tylko KONCZY biezaca operacje; delta ZOSTAJE i wolajacy
    /// domknie ja w nastepnym przelocie, ktory ma wlasny budzet dosylek. Zmiana
    /// CELU dalej PORZUCA delte - tam to jedyna uczciwa odpowiedz.
    /// </summary>
    public int TakePendingDelta(string? groupId)
    {
        if (_pendingDelta == 0) return 0;
        if (!string.Equals(_groupId, groupId, StringComparison.Ordinal)) { _pendingDelta = 0; return 0; }
        if (_drains >= MaxDrains) return 0;
        _drains++;
        var delta = _pendingDelta;
        _pendingDelta = 0;
        return delta;
    }

    /// <summary>
    /// Czy budzet dosylek biezacej operacji WYCZERPAL sie, a intencja CZEKA.
    /// Wolajacy ma wtedy domknac operacje i ZACZAC nowa dla reszty - bez tego
    /// <see cref="End"/> wyrzucilby nacisniecia do kosza.
    /// </summary>
    public bool IsDrainBudgetExhausted => _pendingDelta != 0 && _drains >= MaxDrains;

    /// <summary>
    /// PRZENIESIENIE oczekujacej intencji do NOWEJ operacji tej samej grupy:
    /// budzet dosylek startuje od zera, a delta ZOSTAJE. Zwraca <c>false</c>, gdy
    /// nie ma czego przenosic albo gdy cel sie zmienil - wtedy nic nie wychodzi.
    /// </summary>
    public bool TryCarryOver(string? groupId)
    {
        if (_pendingDelta == 0) return false;
        if (!string.Equals(_groupId, groupId, StringComparison.Ordinal)) { _pendingDelta = 0; return false; }
        _drains = 0;
        return true;
    }

    /// <summary>KROTKA odmowa na SAMYM BRZEGU zakresu - zamiast pustego POST-u.</summary>
    public static string DescribeBoundary(int delta, int volume) => delta < 0
        ? "Głośność jest już na zero."
        : "Głośność jest już maksymalna, " + volume.ToString(
            System.Globalization.CultureInfo.CurrentCulture) + " procent.";

    /// <summary>KONIEC operacji: nic nie zostaje na nastepna, niezwiazana probe.</summary>
    public void End()
    {
        _groupId = null;
        _pendingDelta = 0;
        _drains = 0;
        _baseVolume = null;
    }
}

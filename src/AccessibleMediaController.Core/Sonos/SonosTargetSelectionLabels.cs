using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AccessibleMediaController.Core.Sonos;

/// <summary>
/// ETYKIETY I ZASADY WYBORU CELU STEROWANIA (Ctrl+F5).
///
/// Po co to jest: Biblioteka (Ctrl+L) pokazuje teraz MATERIAL, wiec glosniki i
/// grupy musza miec WLASNE, jawne miejsce. Tym miejscem jest Ctrl+F5 - jedno
/// okno, ktore MOWI, na co leci sterowanie, i pozwala to zmienic.
///
/// Czego to okno NIE ROBI, swiadomie:
///  * NIE GRA. Wybor celu nie wysyla ani jednego POST do odtwarzania. Wynik mowi
///    "Wybrano: Biuro", nigdy "gra" - bo nic nie zagralo.
///  * NIE TWORZY i NIE ROZWIAZUJE grup, nie zmienia czlonkostwa glosnikow.
///    Wybiera sie WYLACZNIE grupe, ktora JUZ ISTNIEJE w odczytanej topologii.
///  * NIE WYMYSLA nazw. Nazwa grupy i nazwy jej glosnikow pochodza z ODCZYTU
///    topologii. Gdy nazwy nie ma, mowimy to UCZCIWIE, zamiast pokazac
///    identyfikator albo sklejke typu "RINCON_...".
///
/// Kluczowa zmiana wzgledem dawnych etykiet: ZADNEGO "Biuro +1". Uzytkownik
/// niewidomy nie ma jak sprawdzic, co znaczy "+1" - wiec wymieniamy PELNE nazwy
/// wszystkich glosnikow grupy.
/// </summary>
public static class SonosTargetSelectionLabels
{
    /// <summary>Tytul okna. Mowi o CELU, nie o "urzadzeniach" i nie o bibliotece.</summary>
    public const string WindowTitle = "Cel sterowania Sonos";

    /// <summary>
    /// PIERWSZA tresc okna. Jawnie zdejmuje obawe "czy to zaraz zagra" i mowi,
    /// czego okno NIE zmienia w samym Sonosie.
    /// </summary>
    public const string ViewIntroduction =
        "Wybór celu sterowania Sonos. Wybór wskazuje grupę, do której AMC wysyła polecenia; "
        + "nie uruchamia muzyki, nie tworzy i nie rozwiązuje grup.";

    /// <summary>
    /// BRAK ZALOGOWANIA. Uczciwa przyczyna i droga wyjscia, bez autostartu
    /// logowania i bez otwierania przegladarki za uzytkownika.
    /// </summary>
    public const string NotSignedIn =
        "Nie ma zalogowanego konta Sonos, więc nie ma skąd wziąć grup. "
        + "Zaloguj się w oknie konta Sonos, a potem otwórz wybór celu jeszcze raz.";

    /// <summary>
    /// BRAK WYBRANEGO DOMU. Dom jest warunkiem odczytu topologii; mowimy to
    /// wprost i NIE wybieramy domu samodzielnie.
    /// </summary>
    public const string NoHousehold =
        "Nie ma wybranego domu Sonos, więc lista grup jest nieznana. "
        + "Wybierz dom w oknie konta Sonos, a potem otwórz wybór celu jeszcze raz.";

    /// <summary>
    /// TOPOLOGIA JESZCZE NIEODCZYTANA. "Nie wiem" jest uczciwsze niz pusta lista
    /// udajaca dom bez glosnikow.
    /// </summary>
    public const string TopologyUnknown =
        "Lista grup nie została jeszcze odczytana. Użyj Odśwież, żeby pobrać grupy z Sonosa.";

    /// <summary>
    /// DOM BEZ GRUP. Poprawny wynik odczytu, nie blad - i jawnie NIE wybieramy
    /// wtedy niczego w zastepstwie.
    /// </summary>
    public const string EmptyState =
        "Ten dom Sonos nie ma żadnych grup, więc nie ma czego wybrać jako cel.";

    /// <summary>
    /// TOPOLOGIA NIEPELNA. Sonos sam oznaczyl odczyt jako czesciowy, wiec
    /// ostrzegamy, ze jakiejs grupy moze na liscie brakowac.
    /// </summary>
    public const string PartialTopology =
        "Uwaga: Sonos oznaczył odczyt topologii jako niepełny, więc na liście może brakować grup.";

    /// <summary>ETYKIETA przycisku potwierdzenia. Celowo nie "Odtwórz".</summary>
    public const string ConfirmButtonLabel = "Ustaw jako cel";

    /// <summary>
    /// NAZWA ZASTEPCZA, gdy topologia nie podala nazwy grupy ani nazw jej
    /// glosnikow. UCZCIWIE mowi "bez nazwy" i NIE pokazuje identyfikatora.
    /// </summary>
    public const string UnnamedGroup = "Grupa bez nazwy w odczytanej topologii";

    /// <summary>
    /// WYNIK potwierdzenia. Mowi "Wybrano", bo nic nie zagralo - rozroznienie
    /// proby od odtwarzania jest tu calkowicie swiadome.
    /// </summary>
    public static string DescribeSelected(string groupLabel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupLabel);
        return "Wybrano cel: " + groupLabel + ". Muzyka nie została uruchomiona.";
    }

    /// <summary>
    /// AKTUALNY CEL do pierwszego odczytu okna. Brak celu mowi sie wprost.
    /// </summary>
    public static string DescribeCurrentTarget(string? groupLabel) =>
        string.IsNullOrWhiteSpace(groupLabel)
            ? "Teraz nie ma wybranego celu sterowania."
            : "Teraz cel sterowania to: " + groupLabel;

    /// <summary>
    /// ETYKIETA JEDNEJ GRUPY jako CELU - z PELNYMI nazwami glosnikow.
    ///
    /// Tu znika "Biuro +1": zamiast liczby ukrytych urzadzen wymieniamy nazwy
    /// logicznych glosnikow z topologii. Gdy jakiegos glosnika nie ma w odczycie
    /// (topologia niepelna), mowimy "1 głośnik bez nazwy w odczycie" - nie
    /// zgadujemy i nie pokazujemy identyfikatora.
    /// </summary>
    public static string DescribeGroupTarget(SonosGroup group, SonosHouseholdTopology topology)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(topology);

        var playersById = new Dictionary<string, SonosPlayer>(StringComparer.Ordinal);
        foreach (var player in topology.Players)
        {
            playersById[player.Id] = player;
        }

        var names = new List<string>();
        var unnamed = 0;
        foreach (var playerId in group.PlayerIds)
        {
            if (playersById.TryGetValue(playerId, out var player)
                && !string.IsNullOrWhiteSpace(player.Name))
            {
                names.Add(player.Name);
                continue;
            }

            // Glosnik jest w grupie, ale jego nazwy NIE MA w odczycie. Uczciwy
            // licznik bez identyfikatora i bez wymyslonej nazwy.
            unnamed++;
        }

        var groupName = string.IsNullOrWhiteSpace(group.Name) ? null : group.Name;
        var head = groupName ?? (names.Count > 0 ? string.Join(" i ", names) : UnnamedGroup);

        var parts = new List<string> { head };

        // PELNY sklad grupy wymieniony NAZWAMI - to jest sens tej etykiety.
        if (names.Count > 0)
        {
            parts.Add(names.Count == 1
                ? "głośnik " + names[0]
                : "głośniki: " + string.Join(", ", names));
        }

        if (unnamed > 0)
        {
            parts.Add(unnamed == 1
                ? "1 głośnik bez nazwy w odczycie"
                : unnamed.ToString(CultureInfo.CurrentCulture) + " głośniki bez nazwy w odczycie");
        }

        if (names.Count == 0 && unnamed == 0)
        {
            // Grupa bez ani jednego glosnika w odczycie. Mowimy to wprost.
            parts.Add("brak głośników w odczycie");
        }

        return string.Join(", ", parts);
    }

    /// <summary>
    /// ETYKIETY CALEJ listy grup w KOLEJNOSCI ODCZYTU. Zadnego sortowania po
    /// nazwie: kolejnosc z API jest stabilniejsza niz nasza inwencja.
    /// </summary>
    public static IReadOnlyList<string> DescribeGroupTargets(SonosHouseholdTopology topology)
    {
        ArgumentNullException.ThrowIfNull(topology);
        return topology.Groups
            .Select(group => DescribeGroupTarget(group, topology))
            .ToArray();
    }

    /// <summary>PODSUMOWANIE liczby grup do wypowiedzenia po otwarciu.</summary>
    public static string SummarizeCount(int count) => count switch
    {
        0 => EmptyState,
        1 => "Grupy Sonos: 1 pozycja",
        _ => "Grupy Sonos: " + count.ToString(CultureInfo.CurrentCulture) + " pozycji"
    };

    /// <summary>
    /// INSTRUKCJA ODZYSKANIA WYBORU GRUPY - PRAWDZIWA droga po tej zmianie.
    ///
    /// Dawny tekst mowil "Biblioteka, Enter", bo grupy stały na liscie glosnikow.
    /// Po zastapieniu tej listy Biblioteka MATERIALU taka instrukcja byla by
    /// nieprawdziwa, wiec mowi teraz o Ctrl+F5.
    /// </summary>
    public const string RecoveryHint =
        "Cel sterowania Sonos wybierasz skrótem Control F5.";
}

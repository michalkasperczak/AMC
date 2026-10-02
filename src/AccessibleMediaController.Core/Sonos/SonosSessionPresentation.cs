using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using AccessibleMediaController.Core.Commands;

namespace AccessibleMediaController.Core.Sonos;

/// <summary>
/// Dlaczego lista sesji jest PUSTA. Rozdzielone, bo "nie masz konta" i "konto
/// jest, ale dom nie ma grup" wymagaja INNEJ podpowiedzi.
/// </summary>
public enum SonosSessionEmptyReason
{
    /// <summary>Brak polaczonego konta: droga prowadzi do logowania.</summary>
    NoAccount,

    /// <summary>Konto jest, ale dom nie oddal zadnej grupy.</summary>
    NoGroups,

    /// <summary>Konto jest, grup jeszcze nie odczytano (albo odczyt padl).</summary>
    NotRead,

    /// <summary>
    /// Odczyt domow SIE UDAL i oddal ZERO domow. Konto jest podlaczone, wiec
    /// droga NIE prowadzi do logowania; brakuje samego domu. Wartosc dopisana na
    /// KONCU, zeby nie przenumerowac istniejacych stanow zapisanych wczesniej.
    /// </summary>
    NoHouseholds
}

/// <summary>Jeden wiersz listy sesji Sonos: GRUPA, nie utwor demonstracyjny.</summary>
public sealed class SonosGroupRow
{
    internal SonosGroupRow(string groupId, string name, string text, int playerCount)
    {
        GroupId = groupId;
        Name = name;
        Text = text;
        PlayerCount = playerCount;
    }

    /// <summary>Identyfikator grupy. Wybor i sterowanie ida PO NIM, nie po indeksie.</summary>
    public string GroupId { get; }

    public string Name { get; }

    /// <summary>Tekst dla uzytkownika. Bez surowego identyfikatora RINCON.</summary>
    public string Text { get; }

    public int PlayerCount { get; }

    public override string ToString() => Text;
}

/// <summary>
/// Tekst LISTY sesji Sonos. Czysta prezentacja: zero HTTP, zero tokenow, zero
/// zaleznosci od UI - dzieki temu mierzalna na WSL.
/// </summary>
public static class SonosSessionListPresentation
{
    /// <summary>Identyfikator sesji Sonos w <c>SessionManager</c> i w ustawieniach.</summary>
    public const string SessionId = "sonos";

    public const string SessionDisplayName = "Sonos";

    public static IReadOnlyList<SonosGroupRow> DescribeGroups(SonosHouseholdTopology? topology)
    {
        if (topology is null || topology.Groups.Count == 0)
        {
            return new ReadOnlyCollection<SonosGroupRow>(new List<SonosGroupRow>());
        }

        var rows = new List<SonosGroupRow>(topology.Groups.Count);
        foreach (var group in topology.Groups)
        {
            var name = string.IsNullOrWhiteSpace(group.Name) ? "Grupa Sonos" : group.Name.Trim();
            var players = group.PlayerIds.Count;
            var text = name
                + ", głośników: " + players.ToString(CultureInfo.CurrentCulture)
                + ", " + DescribeState(group.PlaybackState);
            rows.Add(new SonosGroupRow(group.Id, name, text, players));
        }

        return new ReadOnlyCollection<SonosGroupRow>(rows);
    }

    /// <summary>
    /// Stan grupy JAKO SLOWO. Nieznany stan nie udaje ciszy ani odtwarzania.
    /// </summary>
    public static string DescribeState(SonosPlaybackState state) => state switch
    {
        SonosPlaybackState.Playing => "odtwarza",
        SonosPlaybackState.Paused => "wstrzymana",
        SonosPlaybackState.Buffering => "buforuje",
        SonosPlaybackState.Idle => "nie odtwarza",
        _ => "stan nieznany"
    };

    /// <summary>Czytelny PUSTY stan z droga do konta - nigdy pustej listy bez slowa.</summary>
    public static string DescribeEmptyState(SonosSessionEmptyReason reason) => reason switch
    {
        SonosSessionEmptyReason.NoAccount =>
            "Nie ma połączonego konta Sonos. Otwórz konto Sonos z menu Plik i zaloguj się w przeglądarce, "
            + "aby zobaczyć swoje grupy.",
        SonosSessionEmptyReason.NoGroups =>
            "Konto Sonos jest połączone, ale ten dom nie zgłosił żadnej grupy. Sprawdź głośniki w aplikacji Sonos.",
        // Udany odczyt ZERO domow: NIE mowimy o logowaniu (konto jest) i NIE
        // mowimy o grupach (zaden dom nie jest wybrany, wiec nie ma czyich grup).
        SonosSessionEmptyReason.NoHouseholds =>
            "Konto Sonos jest połączone, ale nie udostępnia żadnego domu. Dodaj system Sonos w aplikacji Sonos, "
            + "aby zobaczyć swoje grupy.",
        _ => "Grupy Sonos nie zostały jeszcze odczytane. Odśwież sesję, aby spróbować ponownie."
    };
}

/// <summary>
/// AKTYWNA grupa trzymana PO IDENTYFIKATORZE. Zmiana kolejnosci ani nazwy nie
/// przenosi sterowania na inna grupe, a ZNIKNIECIE grupy NIE wybiera po cichu
/// zastepczej: cicha zmiana celu polecen byloby sterowaniem nie tym pokojem.
/// </summary>
public static class SonosActiveGroupPolicy
{
    public static SonosGroup? Resolve(string? selectedGroupId, SonosHouseholdTopology? topology)
    {
        if (topology is null || string.IsNullOrWhiteSpace(selectedGroupId)) return null;
        return topology.Groups.FirstOrDefault(group =>
            string.Equals(group.Id, selectedGroupId, StringComparison.Ordinal));
    }

    /// <summary>
    /// Czy wybrany dom nadal istnieje. Zniknietego domu nie podmieniamy po cichu
    /// na inny - to znaczylo by czytanie i sterowanie CZYMS INNYM.
    /// </summary>
    public static SonosHousehold? ResolveHousehold(
        string? selectedHouseholdId,
        IReadOnlyList<SonosHousehold>? households)
    {
        if (households is null || string.IsNullOrWhiteSpace(selectedHouseholdId)) return null;
        return households.FirstOrDefault(household =>
            string.Equals(household.Id, selectedHouseholdId, StringComparison.Ordinal));
    }
}

/// <summary>Pozycja i jej WIARYGODNOSC. Zadnego zegara demonstracyjnego.</summary>
public sealed class SonosPositionView
{
    internal SonosPositionView(TimeSpan? position, TimeSpan? duration, bool extrapolated, bool stale)
    {
        Position = position;
        Duration = duration;
        Extrapolated = extrapolated;
        Stale = stale;
    }

    /// <summary>Null znaczy BRAK INFORMACJI, nie zero.</summary>
    public TimeSpan? Position { get; }

    public TimeSpan? Duration { get; }

    /// <summary>Czy do odczytanej pozycji doliczono czas od odczytu.</summary>
    public bool Extrapolated { get; }

    /// <summary>Czy odczyt jest starszy niz okno swiezosci (pokazujemy go JAWNIE jako stary).</summary>
    public bool Stale { get; }
}

/// <summary>
/// Czas w odtwarzaczu Sonos. Poczatkowo wystarczy OSTATNIA odczytana pozycja;
/// doliczanie czasu jest dopuszczalne WYLACZNIE gdy pozycja jest znana, stan to
/// Playing i odczyt jest SWIEZY. Po wygasnieciu swiezosci nie wolno wyliczac -
/// ekstrapolacja poza wygaslym odczytem to wymyslony czas.
/// </summary>
public static class SonosPlayerPosition
{
    /// <summary>Okno swiezosci: POLITYKA AMC, nie limit Sonosa.</summary>
    public static readonly TimeSpan FreshnessWindow = TimeSpan.FromSeconds(30);

    public static SonosPositionView Resolve(
        SonosGroupPlaybackStatus? status,
        int? durationMillis,
        DateTime readUtc,
        DateTime nowUtc)
    {
        var duration = durationMillis.HasValue && durationMillis.Value >= 0
            ? TimeSpan.FromMilliseconds(durationMillis.Value)
            : (TimeSpan?)null;

        if (status?.PositionMillis is not { } positionMillis || positionMillis < 0)
        {
            return new SonosPositionView(null, duration, extrapolated: false, stale: false);
        }

        var read = TimeSpan.FromMilliseconds(positionMillis);
        var age = nowUtc - readUtc;
        if (age < TimeSpan.Zero) age = TimeSpan.Zero;
        var stale = age > FreshnessWindow;
        if (status.PlaybackState != SonosPlaybackState.Playing || stale)
        {
            return new SonosPositionView(read, duration, extrapolated: false, stale: stale);
        }

        var extrapolated = read + age;
        if (duration.HasValue && extrapolated > duration.Value) extrapolated = duration.Value;
        return new SonosPositionView(extrapolated, duration, extrapolated: true, stale: false);
    }
}

/// <summary>Teksty odtwarzacza Sonos z RZECZYWISTEGO odczytu.</summary>
public sealed class SonosPlayerView
{
    internal SonosPlayerView(
        string title,
        string source,
        string stateText,
        string volumeText,
        string positionText)
    {
        Title = title;
        Source = source;
        StateText = stateText;
        VolumeText = volumeText;
        PositionText = positionText;
    }

    public string Title { get; }

    public string Source { get; }

    public string StateText { get; }

    public string VolumeText { get; }

    public string PositionText { get; }
}

/// <summary>
/// Prezentacja ODTWARZACZA grupy. Zadne pole nie jest wymyslane: brak danych ma
/// wlasne slowa "Brak informacji", a nieznane wyciszenie NIE jest raportowane
/// jako wylaczone. Nieudany odczyt nie przenosi poprzednich danych jako sukcesu,
/// bo wolajacy podaje wtedy null.
/// </summary>
public static class SonosPlayerPresentation
{
    public const string Unknown = "Brak informacji";

    public static SonosPlayerView Describe(
        SonosGroupPlaybackStatus? status,
        SonosGroupMetadata? metadata,
        SonosGroupVolume? volume,
        SonosPositionView? position = null)
    {
        return new SonosPlayerView(
            DescribeTitle(metadata),
            DescribeSource(metadata),
            DescribeState(status),
            DescribeVolume(volume),
            DescribePosition(position));
    }

    /// <summary>
    /// Tytul i wykonawca z currentItem. RADIO czesto NIE ma currentItem, wiec
    /// nazwa stacji z kontenera jest POPRAWNYM tytulem, a nie brakiem danych.
    /// </summary>
    public static string DescribeTitle(SonosGroupMetadata? metadata)
    {
        if (metadata is null) return Unknown;
        var track = metadata.CurrentTrack;
        var name = Useful(track?.Name);
        var artist = Useful(track?.ArtistName);
        if (name is not null)
        {
            return artist is null ? name : name + ", " + artist;
        }

        var container = Useful(metadata.Container?.Name);
        if (container is not null)
        {
            var show = Useful(metadata.CurrentShowName);
            return show is null ? container : container + ", " + show;
        }

        return Unknown;
    }

    public static string DescribeSource(SonosGroupMetadata? metadata)
    {
        var service = Useful(metadata?.CurrentTrack?.Service?.Name)
            ?? Useful(metadata?.Container?.Service?.Name);
        if (service is not null) return "Źródło: " + service;
        if (metadata?.Container?.IsStation == true) return "Źródło: stacja radiowa";
        return "Źródło: " + Unknown;
    }

    public static string DescribeState(SonosGroupPlaybackStatus? status)
    {
        if (status is null) return "Stan: " + Unknown;
        return "Stan: " + status.PlaybackState switch
        {
            SonosPlaybackState.Playing => "Odtwarzanie",
            SonosPlaybackState.Paused => "Wstrzymane",
            SonosPlaybackState.Buffering => "Buforowanie",
            SonosPlaybackState.Idle => "Zatrzymane",
            _ => Unknown
        };
    }

    /// <summary>
    /// Glosnosc i wyciszenie. NIEZNANE wyciszenie zostaje nieznane: zgadniety
    /// bool prowadzilby do odwrotnego polecenia i zaskoczenia w pokoju.
    /// </summary>
    public static string DescribeVolume(SonosGroupVolume? volume)
    {
        if (volume is null) return "Głośność: " + Unknown;
        var text = "Głośność: " + volume.Volume.ToString(CultureInfo.CurrentCulture) + " procent";
        text += volume.Muted switch
        {
            true => ", wyciszona",
            false => ", bez wyciszenia",
            _ => ", wyciszenie nieznane"
        };
        if (volume.FixedVolume == true) text += ", poziom stały w tej grupie";
        return text;
    }

    public static string DescribePosition(SonosPositionView? position)
    {
        if (position?.Position is not { } value) return "Czas: " + Unknown;
        var text = "Czas: " + Format(value);
        if (position.Duration is { } duration) text += " z " + Format(duration);
        if (position.Stale) text += ", z ostatniego odczytu";
        return text;
    }

    /// <summary>
    /// Czas Sonosa ZAWIJAL sie po dobie: wzorzec "h\:mm\:ss" bierze KOMPONENT
    /// godzin (0-23), wiec 25 h czytano jako 1:00:00 - doba dluzszy material
    /// (dlugie audycje, strumienie) byl opisywany cudzym czasem. Istniejacy
    /// <see cref="Presentation.MediaItemFormatter.FormatDuration"/> liczy CALE
    /// godziny (TotalHours), wiec uzywamy JEGO, nie drugiego formatera obok.
    /// </summary>
    private static string Format(TimeSpan value) =>
        Presentation.MediaItemFormatter.FormatDuration(value);

    private static string? Useful(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// ETYKIETA TRANSPORTU z ODCZYTU. Zrodlo: docs.sonos.com, playback-playbackstatus,
/// sekcja "Deciding whether to show Pause or Stop":
///
///   * w Control API NIE MA polecenia stop - `pause` jest JEDYNYM transportem
///     zatrzymania i wysylamy je w obu przypadkach,
///   * STOP pokazujemy WYLACZNIE gdy canPause=false I canStop=true; w kazdym
///     innym przypadku PAUZE,
///   * liczymy to z KAZDEGO swiezego availablePlaybackActions, nie z typu tresci
///     ani nazwy usługi.
///
/// Nieznany odczyt (null) NIE jest Stopem: bez odczytu nie zgadujemy etykiety.
/// </summary>
public static class SonosTransportLabels
{
    /// <summary>Czy pokazac i NAZWAC to zatrzymaniem zamiast wstrzymaniem.</summary>
    public static bool IsStopControl(SonosPlaybackActions? actions) =>
        actions?.CanPause == false && actions?.CanStop == true;
}

/// <summary>Werdykt bramki: wolno wyslac POST czy trzeba odmowic.</summary>
public sealed class SonosCommandGate
{
    internal SonosCommandGate(bool allowed, string? refusal)
    {
        Allowed = allowed;
        Refusal = refusal;
    }

    public bool Allowed { get; }

    /// <summary>Tekst ODMOWY. Odmowa konczy droge - zadnego POST.</summary>
    public string? Refusal { get; }
}

/// <summary>
/// BRAMKA dostepnosci polecen liczona z ODCZYTU: availablePlaybackActions i
/// volume.fixed. Brak odczytu albo nieznane uprawnienie NIE jest zgoda - Sonos
/// nie musi przyjac polecenia, a martwy przycisk klamie uzytkownikowi.
/// Repeat, shuffle, stop i URI NIE sa tu obslugiwane celowo.
/// </summary>
public static class SonosCommandGating
{
    public static SonosCommandGate Evaluate(
        string commandId,
        SonosPlaybackState state,
        SonosPlaybackActions? actions,
        SonosGroupVolume? volume)
    {
        switch (commandId)
        {
            case CommandIds.PlayPause:
            case CommandIds.ActivateSelected:
            {
                var pausing = state == SonosPlaybackState.Playing || state == SonosPlaybackState.Buffering;
                if (!pausing)
                {
                    return From(actions?.CanPlay, "Sonos nie zgłasza możliwości odtwarzania w tej grupie.");
                }

                // ZATRZYMANIE: w Control API NIE MA polecenia stop - `pause` jest
                // jedynym transportem zatrzymania, a dla materialu
                // NIEPAUZOWALNEGO (HLS, radio live) player traktuje je jak stop.
                // Dlatego canPause=false + canStop=true to NORMALNY, udokumentowany
                // stan radia, a nie brak mozliwosci zatrzymania - dotad bramka
                // patrzyla TYLKO na canPause i odmawiala "Sonos nie zglasza
                // mozliwosci wstrzymania tego materialu" nad grajacym radiem.
                // Zrodlo: playback-playbackstatus.md, "Deciding whether to show
                // Pause or Stop". OBA false oraz brak odczytu (null) nadal
                // ODMAWIAJA: nieznane uprawnienie nie jest zgoda.
                var canHalt = actions?.CanPause == true || actions?.CanStop == true;
                return canHalt
                    ? new SonosCommandGate(true, null)
                    : new SonosCommandGate(
                        false,
                        actions is null
                            // ROZNE przyczyny, rozne komunikaty: nieodczytany stan
                            // nie jest tym samym co material, ktorego nie wolno
                            // zatrzymac - inaczej uzytkownik nie wie, co zrobic.
                            ? "Stan Sonos nie został odczytany. Odśwież stan Sonos."
                            : "Sonos nie zgłasza możliwości zatrzymania tego materiału.");
            }

            case CommandIds.Next:
                return From(actions?.CanSkip, "Sonos nie zgłasza możliwości przejścia do następnej pozycji.");

            case CommandIds.Previous:
                return From(
                    actions?.SkipToPreviousAllowed,
                    "Sonos nie zgłasza możliwości przejścia do poprzedniej pozycji.");

            case CommandIds.ToggleMuteCurrentSession:
                // volume.fixed dotyczy POZIOMU, nie wyciszenia. Natomiast NIEZNANY
                // stan wyciszenia odmawiamy: przelacznik bez odczytu zgadywalby bool.
                return volume?.Muted is null
                    ? new SonosCommandGate(false, "Nie wiadomo, czy grupa jest wyciszona. Odśwież stan Sonos.")
                    : new SonosCommandGate(true, null);

            case CommandIds.VolumeUp5:
            case CommandIds.VolumeDown5:
            case CommandIds.VolumeUp1:
            case CommandIds.VolumeDown1:
                if (volume is null)
                {
                    return new SonosCommandGate(false, "Głośność grupy Sonos nie została odczytana.");
                }

                return volume.FixedVolume == true
                    ? new SonosCommandGate(false, "Ta grupa Sonos ma stały poziom głośności.")
                    : new SonosCommandGate(true, null);

            default:
                if (IsSeek(commandId))
                {
                    return From(actions?.CanSeek, "Sonos nie zgłasza możliwości przewijania tego materiału.");
                }

                return new SonosCommandGate(
                    false,
                    "To polecenie nie jest obsługiwane w sesji Sonos.");
        }
    }

    public static bool IsSeek(string commandId) =>
        commandId is CommandIds.SeekBackward10
            or CommandIds.SeekForward10
            or CommandIds.SeekBackward30
            or CommandIds.SeekForward30
            or CommandIds.SeekBackward60
            or CommandIds.SeekForward60
            or CommandIds.SeekBackwardCustom
            or CommandIds.SeekForwardCustom
        || commandId.StartsWith("transport.seekPercent.", StringComparison.Ordinal);

    private static SonosCommandGate From(bool? allowed, string refusal) =>
        allowed == true ? new SonosCommandGate(true, null) : new SonosCommandGate(false, refusal);
}

/// <summary>Rodzaj polecenia dla WERDYKTU - decyduje, CO trzeba odczytac.</summary>
public enum SonosVerdictCommand
{
    Play,
    Pause,

    /// <summary>
    /// ZATRZYMANIE materialu niepauzowalnego (radio live). Wysylane POLECENIE to
    /// nadal `pause` - innego w Control API nie ma - ale SKUTEK jest inny:
    /// playbackState idzie do IDLE, nie do PAUSED, a pozniejszy `play` DOLACZA do
    /// transmisji, nie wznawia pozycji. Dlatego werdykt przyjmuje OBA stany
    /// koncowe i nie obiecuje wznowienia od pozycji.
    /// </summary>
    Stop,
    Toggle,
    Next,
    Previous,
    Seek,
    Volume,
    Mute
}

/// <summary>Co naprawde wiemy po poleceniu i JAWNYM odczycie.</summary>
public sealed class SonosVerdict
{
    internal SonosVerdict(bool confirmed, string text)
    {
        Confirmed = confirmed;
        Text = text;
    }

    /// <summary>
    /// true TYLKO gdy ZNANY stan sprzed polecenia i ZNANY odczyt po nim pokazuja
    /// ZMIANE. Stan juz docelowy przed poleceniem nie jest dowodem skutku POST, a
    /// nieznany stan przed poleceniem nie pozwala porownac. false nie znaczy
    /// odmowy ani braku skutku - znaczy BRAK DOWODU.
    /// </summary>
    public bool Confirmed { get; }

    public string Text { get; }
}

/// <summary>
/// WERDYKT po poleceniu. HTTP 200 to przyjecie, nie wykonanie; dopiero JAWNY GET
/// stanu/glosnosci/metadanych moze potwierdzic skutek - i tylko wtedy, gdy
/// widac ZMIANE. Nic tu nie ponawia POST.
/// </summary>
public static class SonosCommandVerdict
{
    public static SonosVerdict Describe(
        SonosVerdictCommand command,
        bool accepted,
        bool stateReadSucceeded,
        SonosPlaybackState before,
        SonosPlaybackState after,
        string? beforeItemId,
        string? afterItemId)
    {
        // accepted=false NIE znaczy odmowy. Ten sam bool powstaje z timeoutu, 5xx i
        // utraconej odpowiedzi PO wyslaniu POST, wiec nie dowodzi ani niewyslania,
        // ani odrzucenia, ani braku skutku w pokoju.
        if (!accepted)
        {
            return new SonosVerdict(
                false,
                "Brak potwierdzenia od Sonos: nie wiadomo, czy polecenie zostało wykonane. Odśwież stan grupy.");
        }

        if (!stateReadSucceeded)
        {
            return new SonosVerdict(
                false,
                "Sonos przyjął polecenie, ale odczyt stanu się nie udał: wykonanie niepotwierdzone.");
        }

        switch (command)
        {
            case SonosVerdictCommand.Play:
            case SonosVerdictCommand.Pause:
            case SonosVerdictCommand.Stop:
            case SonosVerdictCommand.Toggle:
            {
                if (command == SonosVerdictCommand.Toggle)
                {
                    // Bez ZNANEGO stanu sprzed polecenia nie ma z czym porownac:
                    // "zmieniony" byloby wymyslone, choc odczyt PO poleceniu sie udal.
                    return after != before
                        && after != SonosPlaybackState.Unknown
                        && before != SonosPlaybackState.Unknown
                        // KROTKO po sukcesie: samo slowo stanu. Litania techniczna
                        // po KAZDYM udanym klawiszu byla zgloszona jako koszt uwagi.
                        ? new SonosVerdict(true, Word(after) + ".")
                        : new SonosVerdict(
                            false,
                            "Odczytany stan Sonos: " + Word(after) + ". Wykonanie niepotwierdzone.");
                }

                if (command == SonosVerdictCommand.Stop)
                {
                    // ZATRZYMANIE: udokumentowany skutek `pause` na materiale
                    // niepauzowalnym to IDLE, na pauzowalnym PAUSED. OBA sa
                    // dowodem zatrzymania, wiec Idle NIE jest tu bledem. Nie
                    // mowimy "wstrzymano" ani nic o pozycji: po `play` Sonos
                    // DOLACZA do transmisji na zywo, nie wznawia od pozycji.
                    var halted = after is SonosPlaybackState.Idle or SonosPlaybackState.Paused;
                    if (!halted)
                    {
                        return new SonosVerdict(
                            false,
                            "Odczytany stan Sonos: " + Word(after) + ". Wykonanie niepotwierdzone.");
                    }

                    if (before == SonosPlaybackState.Unknown)
                    {
                        return new SonosVerdict(
                            false,
                            "Odczytany stan Sonos: " + Word(after)
                            + ". Stan sprzed polecenia nieznany, więc wykonanie niepotwierdzone.");
                    }

                    return before == after
                        ? new SonosVerdict(
                            false,
                            "Odczytany stan Sonos: " + Word(after) + " - taki był już przed poleceniem.")
                        : new SonosVerdict(true, "Zatrzymano.");
                }

                var target = command == SonosVerdictCommand.Play
                    ? SonosPlaybackState.Playing
                    : SonosPlaybackState.Paused;

                if (after != target)
                {
                    return new SonosVerdict(
                        false,
                        "Odczytany stan Sonos: " + Word(after) + ". Wykonanie niepotwierdzone.");
                }

                if (before == SonosPlaybackState.Unknown)
                {
                    // Stan docelowy mogl trwac juz przed poleceniem - nie wiemy.
                    return new SonosVerdict(
                        false,
                        "Odczytany stan Sonos: " + Word(after)
                        + ". Stan sprzed polecenia nieznany, więc wykonanie niepotwierdzone.");
                }

                return before == target
                    ? new SonosVerdict(
                        false,
                        "Odczytany stan Sonos: " + Word(after) + " - taki był już przed poleceniem.")
                    // KROTKO po sukcesie: "Pauza." zamiast "Pauza potwierdzone
                    // odczytem stanu." Szczegoly zostaja w logu, nie w mowie.
                    : new SonosVerdict(true, Word(after) + ".");
            }

            case SonosVerdictCommand.Next:
            case SonosVerdictCommand.Previous:
            {
                if (beforeItemId is null || afterItemId is null)
                {
                    return new SonosVerdict(
                        false,
                        "Odczyt się udał, ale nie ma porównywalnego materiału: zmiana pozycji niepotwierdzona.");
                }

                return string.Equals(beforeItemId, afterItemId, StringComparison.Ordinal)
                    ? new SonosVerdict(false, "Odczytana pozycja się nie zmieniła: wykonanie niepotwierdzone.")
                    // KROTKO po sukcesie: skok juz slychac po zmianie materialu.
                    : new SonosVerdict(true, "Zmieniono pozycję.");
            }

            default:
                return new SonosVerdict(
                    false,
                    "Sonos przyjął polecenie; sprawdź odczytany stan. Wykonanie niepotwierdzone.");
        }
    }

    /// <summary>Werdykt dla GLOSNOSCI i WYCISZENIA po jawnym odczycie groupVolume.</summary>
    public static SonosVerdict DescribeVolume(
        bool accepted,
        bool readSucceeded,
        SonosGroupVolume? before,
        SonosGroupVolume? after,
        int? requestedVolume,
        bool? requestedMute)
    {
        // Jak wyzej: brak potwierdzenia nie jest odmowa Sonosa.
        if (!accepted)
        {
            return new SonosVerdict(
                false,
                "Brak potwierdzenia od Sonos dla polecenia głośności: nie wiadomo, czy zostało wykonane. "
                + "Odśwież stan grupy.");
        }

        if (!readSucceeded || after is null)
        {
            return new SonosVerdict(
                false,
                "Sonos przyjął polecenie, ale głośności nie udało się odczytać: wykonanie niepotwierdzone.");
        }

        if (requestedMute is { } mute)
        {
            if (after.Muted != mute)
            {
                return new SonosVerdict(
                    false,
                    "Odczytane wyciszenie: " + Mute(after.Muted) + ". Wykonanie niepotwierdzone.");
            }

            // Brak odczytu SPRZED polecenia (albo nieznane wyciszenie) nie dowodzi,
            // ze to nasze polecenie zmienilo stan - mogl byc taki juz wczesniej.
            if (before?.Muted is null)
            {
                return new SonosVerdict(
                    false,
                    "Odczytane wyciszenie: " + Mute(after.Muted)
                    + ". Stan sprzed polecenia nieznany, więc wykonanie niepotwierdzone.");
            }

            return before?.Muted == mute
                ? new SonosVerdict(
                    false,
                    "Odczytane wyciszenie: " + Mute(after.Muted) + " - takie było już przed poleceniem.")
                : new SonosVerdict(true, "Wyciszenie: " + Mute(after.Muted) + ".");
        }

        if (requestedVolume is { } target)
        {
            if (after.Volume != target)
            {
                return new SonosVerdict(
                    false,
                    "Odczytana głośność: " + after.Volume.ToString(CultureInfo.CurrentCulture)
                    + " procent. Wykonanie niepotwierdzone.");
            }

            if (before is null)
            {
                return new SonosVerdict(
                    false,
                    "Odczytana głośność: " + after.Volume.ToString(CultureInfo.CurrentCulture)
                    + " procent. Poziom sprzed polecenia nieznany, więc wykonanie niepotwierdzone.");
            }

            return before?.Volume == target
                ? new SonosVerdict(
                    false,
                    "Odczytana głośność: " + after.Volume.ToString(CultureInfo.CurrentCulture)
                    + " procent - taka była już przed poleceniem.")
                : new SonosVerdict(
                    true,
                    // KROTKO po sukcesie: sama WARTOSC, bez "potwierdzona
                    // odczytem" - doslowne zgloszenie Michala.
                    "Głośność " + after.Volume.ToString(CultureInfo.CurrentCulture) + " procent.");
        }

        return new SonosVerdict(
            false,
            "Odczytana głośność: " + after.Volume.ToString(CultureInfo.CurrentCulture)
            + " procent. Wykonanie niepotwierdzone.");
    }

    private static string Word(SonosPlaybackState state) => state switch
    {
        SonosPlaybackState.Playing => "Odtwarzanie",
        SonosPlaybackState.Paused => "Pauza",
        SonosPlaybackState.Buffering => "Buforowanie",
        SonosPlaybackState.Idle => "Zatrzymane",
        _ => "stan nieznany"
    };

    private static string Mute(bool? muted) => muted switch
    {
        true => "wyciszone",
        false => "bez wyciszenia",
        _ => "nieznane"
    };
}

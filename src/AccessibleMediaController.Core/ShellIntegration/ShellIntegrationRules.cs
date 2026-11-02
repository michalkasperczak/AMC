using System.Text;
using System.Text.Json;

namespace AccessibleMediaController.Core.ShellIntegration;

/// <summary>
/// Reguly wpiecia AMC w Windows: menu kontekstowe ("Otwórz w AMC", "Dodaj do
/// kolejki AMC") i skojarzenia plikow.
///
/// DLACZEGO TO SIEDZI W CORE, A NIE W INSTALATORZE
/// Instalator potrafi zapisac klucze rejestru, ale nie potrafi tego sprawdzic
/// testem. Reguly - ktore rozszerzenia proponujemy, jaka nazwa klasy, jaki
/// wiersz polecenia - trzymamy wiec tutaj, a instalator i okno ustawien tylko
/// je wykonuja. Dzieki temu literowka w rozszerzeniu wychodzi na testach, a nie
/// u uzytkownika, ktory klika plik i nie rozumie, czemu nic sie nie otwiera.
///
/// CZEGO TU CELOWO NIE MA
/// Nie ma ustawiania AMC domyslnym programem "na sile". Windows 10 i 11 pilnuja
/// wyboru uzytkownika skrotem kontrolnym (klucz UserChoice) i cofaja kazda
/// zmiane zapisana w rejestrze poza swoim okienkiem - uzytkownik dostaje wtedy
/// tylko powiadomienie "domyslna aplikacja zostala przywrocona". Program moze
/// legalnie zrobic dwie rzeczy: ZGLOSIC sie jako mozliwosc (wtedy widac go na
/// liscie "Otwórz za pomocą" i w Ustawieniach Windows) oraz OTWORZYC systemowe
/// okno wyboru, w ktorym decyzje podejmuje uzytkownik. Obie robimy; udawania
/// trzeciej nie ma.
/// </summary>
public static class ShellIntegrationRules
{
    /// <summary>
    /// Nazwa klasy w rejestrze. Wersja w nazwie byłaby błędem: przy każdej
    /// aktualizacji uzytkownik traciłby wybór domyslnego programu.
    /// </summary>
    public const string ProgramClassPrefix = "AccessibleMediaController";

    /// <summary>Nazwa pokazywana w oknie "Otwórz za pomocą" i w Ustawieniach.</summary>
    public const string ApplicationDisplayName = "Accessible Multimedia Controller";

    /// <summary>
    /// Rozszerzenia proponowane do skojarzenia, w grupach po ludzku nazwanych.
    ///
    /// To NIE jest cala lista formatow, ktore AMC otwiera - tylko te, przy
    /// ktorych bycie domyslnym programem ma sens. Formaty pojemnikow wideo
    /// (mkv, avi) sa w osobnej grupie, bo ktos moze chciec AMC do muzyki, ale
    /// filmy zostawic odtwarzaczowi wideo.
    /// </summary>
    public static readonly IReadOnlyList<ShellIntegrationGroup> Groups =
    [
        new(
            "common-audio",
            "Muzyka i nagrania",
            "Najczęstsze formaty dźwiękowe. To zwykle jedyna grupa, którą warto włączyć.",
            [".mp3", ".m4a", ".aac", ".flac", ".wav", ".ogg", ".oga", ".opus", ".wma"]),
        new(
            "other-audio",
            "Rzadsze formaty dźwiękowe",
            "Formaty spotykane w nagraniach studyjnych, radiu i na starszych płytach.",
            [".mp2", ".wave", ".rf64", ".bwf", ".adts", ".asf", ".aif", ".aiff", ".aifc",
             ".mka", ".ac3", ".eac3", ".ec3", ".amr"]),
        new(
            "video",
            "Pliki wideo",
            "Formaty filmów i innych plików wideo, które AMC może otwierać.",
            [".mkv", ".webm", ".mp4", ".m4v", ".mov", ".ogv", ".avi", ".wmv",
             ".mpeg", ".mpg", ".mpe", ".ts", ".mts", ".m2ts", ".vob", ".flv",
             ".3gp", ".3g2", ".3gp2", ".3gpp"]),
        new(
            "playlists",
            "Playlisty",
            "Listy odtwarzania i zapisane stacje radiowe.",
            [".m3u", ".m3u8", ".pls", ".xspf"]),
        new(
            "partial-recordings",
            "Niedokończone nagrania AMC",
            "Pliki przerwanych nagrań, które AMC umie odzyskać. Żaden inny program ich nie otworzy.",
            [".amc-partial"])
    ];

    /// <summary>Wszystkie rozszerzenia ze wszystkich grup, bez powtorzen.</summary>
    public static IReadOnlyList<string> AllExtensions { get; } = Groups
        .SelectMany(group => group.Extensions)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    /// <summary>Grupa domyslnie zaznaczona przy pierwszej instalacji.</summary>
    public const string DefaultGroupName = "Muzyka i nagrania";

    /// <summary>
    /// Tożsamość wpisów powłoki jest osobna dla wydania testowego i
    /// publicznego. Ustawienia oraz host korzystają z tego samego kontraktu,
    /// zamiast składać techniczne nazwy kluczy z napisów widocznych w UI.
    /// </summary>
    public static ShellIntegrationProfile ProfileFor(string? profileId) =>
        string.Equals(profileId, "public", StringComparison.OrdinalIgnoreCase)
            ? ShellIntegrationProfile.Public
            : ShellIntegrationProfile.Test;

    /// <summary>
    /// Nazwa klasy rejestru dla rozszerzenia, np. ".mp3" -> "AccessibleMediaController.mp3".
    /// Osobna klasa na rozszerzenie, a nie jedna wspolna, bo Windows pokazuje
    /// opis klasy w kolumnie "Typ" w Eksploratorze - wspolna klasa zamienilaby
    /// wszystkie pliki w bezuzyteczne "Plik AMC".
    /// </summary>
    public static string ClassNameFor(string extension)
    {
        var trimmed = NormalizeExtension(extension)
            ?? throw new ArgumentException($"Nieprawidłowe rozszerzenie: {extension}", nameof(extension));
        return ProgramClassPrefix + trimmed;
    }

    /// <summary>
    /// Sprowadza rozszerzenie do jednej postaci: male litery, z kropka z przodu.
    /// Zwraca null, gdy tekst nie jest rozszerzeniem - nazwa pliku, sciezka,
    /// pusty tekst albo znaki niedozwolone w rejestrze.
    /// </summary>
    public static string? NormalizeExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension)) return null;
        var text = extension.Trim();
        // Gwiazdka z filtrow okien dialogowych: "*.mp3" to nadal ".mp3".
        if (text.StartsWith("*", StringComparison.Ordinal)) text = text[1..];
        if (!text.StartsWith(".", StringComparison.Ordinal)) text = "." + text;
        if (text.Length < 2) return null;
        foreach (var znak in text.AsSpan(1))
        {
            // Kropka w srodku (".tar.gz") tez odpada - Windows takich nie zna.
            if (!char.IsLetterOrDigit(znak) && znak != '-') return null;
        }

        return text.ToLowerInvariant();
    }

    /// <summary>
    /// Opis typu pliku widoczny w Eksploratorze, np. ".mp3" -> "Plik MP3 (AMC)".
    /// Dopisek w nawiasie jest po to, zeby uzytkownik widzial, ze to AMC przejal
    /// ten typ - bez tego trudno zgadnac, co zmienilo opis.
    /// </summary>
    public static string FileTypeDescriptionFor(string extension)
    {
        var normalized = NormalizeExtension(extension)
            ?? throw new ArgumentException($"Nieprawidłowe rozszerzenie: {extension}", nameof(extension));
        return $"Plik {normalized[1..].ToUpperInvariant()} (AMC)";
    }

    /// <summary>
    /// Polecenia w menu kontekstowym. Pokazuja sie po prawym klikniecu na plik
    /// NIEZALEZNIE od tego, czy AMC jest programem domyslnym - to wlasnie sedno
    /// prosby: dostac sie do AMC bez odbierania czegokolwiek innym programom.
    /// </summary>
    public static readonly IReadOnlyList<ShellVerb> Verbs =
    [
        new("otworz", "Otwórz w AMC", "\"%1\""),
        // Kolejka: drugi klik ma DODAC, a nie zastapic to, co gra. Bez tego
        // ukladanie sluchania z kilku plikow wymaga wracania do okna programu.
        new("dokolejki", "Dodaj do kolejki AMC", "/kolejka \"%1\"")
    ];

    /// <summary>
    /// Przelacznik wiersza polecen, po ktorym AMC wie, ze plik ma dolaczyc do
    /// kolejki, a nie zaczac grac natychmiast.
    /// </summary>
    public const string QueueSwitch = "/kolejka";

    /// <summary>
    /// Rozklada wiersz polecen na liste plikow i informacje, czy dopisac je do
    /// kolejki. Przelacznik moze stac w dowolnym miejscu, bo Windows przy
    /// wielu zaznaczonych plikach sklada polecenie w kolejnosci, ktorej nie
    /// obiecuje.
    /// </summary>
    public static ShellLaunchRequest ParseArguments(IEnumerable<string>? arguments)
    {
        var queue = false;
        var files = new List<string>();
        foreach (var argument in arguments ?? [])
        {
            if (string.IsNullOrWhiteSpace(argument)) continue;
            var text = argument.Trim();
            if (string.Equals(text, QueueSwitch, StringComparison.OrdinalIgnoreCase))
            {
                queue = true;
                continue;
            }

            // Nieznanych przelacznikow nie bierzemy za nazwy plikow - inaczej
            // AMC probowalby otworzyc plik o nazwie "/cos" i pokazywal blad.
            if (text.StartsWith("/", StringComparison.Ordinal)
                || text.StartsWith("-", StringComparison.Ordinal))
            {
                continue;
            }

            files.Add(text);
        }

        return new ShellLaunchRequest(files, queue);
    }

    /// <summary>
    /// Sklada jednoliniowy JSON przekazywany dzialajacej juz instancji AMC.
    /// Sciezki pozostaja osobnymi wartosciami, wiec spacje, przecinki, sredniki
    /// i polskie znaki nie zmieniaja znaczenia zadania.
    /// </summary>
    public static string EncodeHandoff(ShellLaunchRequest request)
    {
        return JsonSerializer.Serialize(new ShellHandoffEnvelope(
            1,
            request.AddToQueue ? "queue" : "open",
            request.Files));
    }

    /// <summary>Odczytuje tekst zapisany przez <see cref="EncodeHandoff"/>.</summary>
    public static ShellLaunchRequest DecodeHandoff(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return new ShellLaunchRequest([], false);
        var trimmed = text.Trim();
        if (!trimmed.StartsWith('{'))
        {
            // Zgodnosc z prototypem sprzed AMC14; nowy launcher juz go nie
            // zapisuje, ale aktualizacja nie musi odrzucac starej kolejki IPC.
            var lines = trimmed.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0) return new ShellLaunchRequest([], false);
            var legacyQueue = string.Equals(
                lines[0].Trim(), "kolejka", StringComparison.OrdinalIgnoreCase);
            return new ShellLaunchRequest(
                lines.Skip(1).Select(line => line.Trim()).ToArray(),
                legacyQueue);
        }
        try
        {
            var envelope = JsonSerializer.Deserialize<ShellHandoffEnvelope>(trimmed)
                ?? throw new InvalidDataException("puste polecenie powloki");
            if (envelope.Schema != 1
                || envelope.Operation is not ("open" or "queue")
                || envelope.Files is null)
            {
                throw new InvalidDataException("nieobslugiwana postac polecenia powloki");
            }
            return new ShellLaunchRequest(
                envelope.Files,
                string.Equals(envelope.Operation, "queue", StringComparison.Ordinal));
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("uszkodzone polecenie powloki", exception);
        }
    }

    private sealed record ShellHandoffEnvelope(
        int Schema,
        string Operation,
        IReadOnlyList<string> Files);
}

/// <summary>Grupa rozszerzen proponowana uzytkownikowi jako jedna pozycja.</summary>
public sealed record ShellIntegrationGroup(
    string Id,
    string Name,
    string Description,
    IReadOnlyList<string> Extensions);

/// <summary>Stała, profilowa tożsamość integracji z Eksploratorem Windows.</summary>
public sealed record ShellIntegrationProfile(
    string Id,
    string RegistryKey,
    string DisplayName,
    string RegisteredApplication,
    string OpenLabel,
    string QueueLabel)
{
    public static ShellIntegrationProfile Test { get; } = new(
        "test",
        "Test",
        "AMC Test",
        "Accessible Multimedia Controller — Test",
        "Otwórz w AMC Test",
        "Dodaj do kolejki AMC Test");

    public static ShellIntegrationProfile Public { get; } = new(
        "public",
        "Public",
        "AMC Public",
        "Accessible Multimedia Controller",
        "Otwórz w AMC Public",
        "Dodaj do kolejki AMC Public");

    public string ClassPrefix => $"AccessibleMediaController.{RegistryKey}.";
    public string VerbPrefix => ClassPrefix;
    public string CapabilitiesPath =>
        $@"Software\AccessibleMultimediaController\{RegistryKey}\Capabilities";
    public string SettingsPath =>
        $@"Software\AccessibleMultimediaController\{RegistryKey}\ShellIntegration";

    public string ClassNameFor(string extension)
    {
        var normalized = ShellIntegrationRules.NormalizeExtension(extension)
            ?? throw new ArgumentException(
                $"Nieprawidłowe rozszerzenie: {extension}", nameof(extension));
        return ClassPrefix + normalized[1..];
    }
}

/// <summary>Polecenie w menu kontekstowym Eksploratora.</summary>
public sealed record ShellVerb(string Key, string Label, string ArgumentTemplate);

/// <summary>Co AMC ma zrobic z plikami podanymi przy uruchomieniu.</summary>
public sealed record ShellLaunchRequest(IReadOnlyList<string> Files, bool AddToQueue)
{
    public bool HasFiles => Files.Count > 0;
}

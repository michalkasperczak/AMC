using System;

namespace AccessibleMediaController.Windows.Services;

/// <summary>
/// Tlumaczy blad yt-dlp na komunikat mowiacy PRAWDE o przyczynie.
///
/// Powod istnienia: do 16.09.2026 program na KAZDY blad yt-dlp mowil
/// "YouTube nie udostepnil obecnie publicznego strumienia audio", a prawdziwa
/// tresc bledu wyrzucal do kosza - nie zapisywal jej nawet w logu.
///
/// Skutek dla uzytkownika: transmisja, ktora sie ZAKONCZYLA, nagranie
/// PRYWATNE i LITEROWKA w adresie dawaly ten sam komunikat, sugerujacy
/// blokade. Zgloszenie z 16.09.2026: "Znowu urywa Hermanice, dalej strumien
/// prywatny" - w rzeczywistosci msza sie po prostu skonczyla.
///
/// Dopasowania szukamy po ANGIELSKICH frazach, dlatego AMC pyta yt-dlp BEZ
/// "lang=pl". ZMIERZONE 16.09.2026 na kanalach Michala - polskie komunikaty
/// nie rozrozniaja przyczyn, angielskie tak:
///   po polsku:    zakonczona transmisja -> "Nagranie tej transmisji jest niedostepne."
///                 nieistniejacy adres   -> "Ten film jest niedostepny"
///   po angielsku: zakonczona transmisja -> "This live stream recording is not available."
///                 nieistniejacy adres   -> "This video is unavailable"
/// Po polsku oba zdania mowia to samo, wiec nie da sie ich odroznic.
/// </summary>
internal static class YouTubeErrorTranslator
{
    /// <summary>Komunikat dla uzytkownika na podstawie tego, co powiedzial yt-dlp.</summary>
    internal static string Describe(string ytDlpError)
    {
        var text = ytDlpError ?? string.Empty;

        // Kolejnosc ma znaczenie: frazy szczegolowe PRZED ogolnymi.
        // "This live stream recording is not available" to DOKLADNIE ten
        // przypadek, ktory Michal zglaszal jako "urywa Hermanice, dalej
        // strumien prywatny" - transmisja sie skonczyla, nic nie jest
        // prywatne. Zmierzone 16.09.2026 na txtC7cxR6m8.
        if (Has(text, "live stream recording is not available")
            || Has(text, "This live event has ended"))
        {
            return "Ta transmisja już się zakończyła. Nagranie nie zostało udostępnione.";
        }

        if (Has(text, "is not available on this app")
            || Has(text, "Watch on the latest version of YouTube"))
        {
            return "YouTube nie udostępnia tej transmisji programom zewnętrznym.";
        }

        if (Has(text, "Private video") || Has(text, "This video is private"))
        {
            return "To nagranie jest prywatne.";
        }

        if (Has(text, "members-only") || Has(text, "join this channel"))
        {
            return "To nagranie jest tylko dla członków kanału.";
        }

        if (Has(text, "Sign in to confirm your age") || Has(text, "age-restricted"))
        {
            return "To nagranie ma ograniczenie wieku i wymaga zalogowania."; 
        }

        if (Has(text, "Sign in to confirm you're not a bot")
            || Has(text, "confirm you’re not a bot"))
        {
            return "YouTube żąda potwierdzenia, że nie jesteś automatem.";
        }

        if (Has(text, "This video is unavailable")
            || Has(text, "Video unavailable") || Has(text, "video has been removed")
            || Has(text, "account associated with this video has been terminated"))
        {
            // ZMIERZONE 16.09.2026: to jest odpowiedz YouTube takze na adres z
            // literowka (identyfikator, ktorego nie ma). YouTube nie rozroznia
            // "usuniete" od "nigdy nie istnialo", wiec my tez nie zgadujemy.
            return "Tego nagrania nie ma na YouTube.";
        }

        if (Has(text, "Incomplete YouTube ID") || Has(text, "Unsupported URL")
            || Has(text, "is not a valid URL"))
        {
            return "Ten adres nie jest poprawnym adresem YouTube.";
        }

        if (Has(text, "not available in your country") || Has(text, "blocked it in your country"))
        {
            return "To nagranie jest niedostępne w tym kraju.";
        }

        if (Has(text, "Failed to resolve") || Has(text, "Temporary failure in name resolution")
            || Has(text, "getaddrinfo") || Has(text, "Network is unreachable")
            || Has(text, "Connection reset") || Has(text, "Remote end closed"))
        {
            return "Brak połączenia z YouTube.";
        }

        if (Has(text, "HTTP Error 429") || Has(text, "Too Many Requests"))
        {
            return "YouTube chwilowo odrzuca zapytania z tego komputera. Spróbuj za kilka minut.";
        }

        if (Has(text, "This video is not available"))
        {
            return "To nagranie jest niedostępne.";
        }

        // Nierozpoznany blad: mowimy uczciwie, ze nie wiadomo, i ZOSTAWIAMY
        // oryginalna tresc - inaczej nie da sie ustalic przyczyny po fakcie.
        var szczegol = FirstErrorLine(text);
        return szczegol.Length > 0
            ? $"YouTube nie udostępnił tego nagrania. Powód: {szczegol}"
            : "YouTube nie udostępnił tego nagrania.";
    }

    private static bool Has(string text, string fragment) =>
        text.Contains(fragment, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Pierwsza linia zaczynajaca sie od ERROR, obcieta do rozsadnej dlugosci
    /// dla czytnika ekranu. yt-dlp potrafi wypisac kilkanascie linii, a caly
    /// blok w komunikacie byl nie do przesluchania.
    /// </summary>
    private static string FirstErrorLine(string text)
    {
        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("ERROR:", StringComparison.OrdinalIgnoreCase)) continue;
            trimmed = trimmed[6..].Trim();
            // yt-dlp poprzedza wlasciwa tresc identyfikatorem w nawiasach
            // kwadratowych, np. "[youtube] txtC7cxR6m8:" - dla sluchajacego to
            // szum, wiec go zdejmujemy.
            var close = trimmed.IndexOf("]:", StringComparison.Ordinal);
            if (close >= 0 && close < 40) trimmed = trimmed[(close + 2)..].Trim();
            if (trimmed.Length > 180) trimmed = trimmed[..180].TrimEnd() + "...";
            return trimmed;
        }
        return string.Empty;
    }
}

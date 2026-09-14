using System;
using System.Globalization;
using System.Text.Json;

namespace AccessibleMediaController.Core.Tidal;

/// <summary>
/// Rodzaj elementu, ktory ma zagrac oryginalny TIDAL desktop.
/// </summary>
public enum TidalDesktopPlayKind
{
    /// <summary>Jeden utwor wskazany tytulem na stronie jego albumu.</summary>
    Track,

    /// <summary>Caly album albo cala playlista, od pierwszego utworu.</summary>
    Container
}

/// <summary>
/// Zadanie odtworzenia przekazywane oryginalnemu TIDAL desktop.
/// </summary>
public sealed class TidalDesktopPlayRequest
{
    public TidalDesktopPlayKind Kind { get; init; } = TidalDesktopPlayKind.Track;

    /// <summary>Adres strony, ktora ma zostac otwarta w oryginalnym TIDALu.</summary>
    public string PageUri { get; init; } = string.Empty;

    /// <summary>
    /// Dokladny tytul utworu szukany na stronie. Wymagany dla <see cref="TidalDesktopPlayKind.Track"/>,
    /// nieuzywany dla calego albumu i playlisty.
    /// </summary>
    public string? TrackTitle { get; init; }

    /// <summary>Nazwa elementu do komunikatow dla uzytkownika.</summary>
    public string DisplayName { get; init; } = string.Empty;
}

/// <summary>
/// Sklada adresy stron oryginalnego TIDAL desktop i wyrazenia sterujace jego
/// interfejsem.
///
/// WSZYSTKO PONIZEJ ZMIERZONE NA TIDAL DESKTOP 2.43.2 (Electron 43), 14.09.2026.
/// Miernikiem byl TYTUL GLOWNEGO OKNA procesu TIDAL ("Utwor - Wykonawca"), bo
/// Windowsowa sesja multimediow (SMTC) dla TIDALa zwraca puste wartosci nawet
/// gdy muzyka gra, a navigator.mediaSession pokazuje stan "none" - TIDAL gra
/// wlasnym silnikiem, nie elementem audio strony.
///
/// Metody potwierdzone:
/// - utwor: .click() na [data-test="play-button"] WEWNATRZ wiersza utworu,
/// - calosc: .click() na [data-test="play-all"].
/// Metody odrzucone po pomiarze:
/// - focus() na wierszu + klawisz Enter: fokus zostaje przyjety, ale utwor sie
///   NIE zmienia,
/// - klikanie wspolrzednymi: kruche, bo przewinieta strona daje wspolrzedne
///   poza ekranem (zmierzone y=-93) i klik nie trafia.
/// </summary>
public static class TidalDesktopPlaybackPlan
{
    /// <summary>Host aplikacji desktop. Zmierzony: strona albumu wystawia liste
    /// utworow. Adres tidal.com/browse NIE nadaje sie - oddaje strone reklamowa
    /// bez wierszy utworow.</summary>
    public const string DesktopHost = "https://desktop.tidal.com";

    /// <summary>Przelacznik, ktory otwiera w TIDALu port sterowania.</summary>
    public const string RemoteDebuggingSwitch = "--remote-debugging-port=";

    /// <summary>Port sterowania.</summary>
    public const int DefaultDebuggingPort = 9222;

    /// <summary>Selektor wiersza utworu na liscie. Zmierzony: 10 wierszy na
    /// albumie o 10 utworach. Starszy "table-list-row" juz nie istnieje.</summary>
    public const string TrackRowSelector = "[data-test=\"tracklist-row\"]";

    /// <summary>Adres strony albumu.</summary>
    public static string AlbumPageUri(string albumExternalId)
    {
        var id = NormalizeId(albumExternalId);
        if (id.Length == 0) throw new ArgumentException("Album bez identyfikatora.", nameof(albumExternalId));
        return $"{DesktopHost}/album/{Uri.EscapeDataString(id)}";
    }

    /// <summary>Adres strony playlisty. Identyfikatory playlist TIDAL to GUID-y.</summary>
    public static string PlaylistPageUri(string playlistExternalId)
    {
        var id = NormalizeId(playlistExternalId);
        if (id.Length == 0) throw new ArgumentException("Playlista bez identyfikatora.", nameof(playlistExternalId));
        return $"{DesktopHost}/playlist/{Uri.EscapeDataString(id)}";
    }

    /// <summary>
    /// Obcina identyfikator do samego numeru albo GUID-a. Zrodla podaja je tez
    /// w postaci "tidal:album:123" albo jako adres, a do sciezki strony musi
    /// trafic sam identyfikator.
    /// </summary>
    public static string NormalizeId(string? value)
    {
        var id = (value ?? string.Empty).Trim();
        if (id.Length == 0) return string.Empty;
        var cut = id.LastIndexOfAny(new[] { ':', '/' });
        if (cut >= 0 && cut < id.Length - 1) id = id[(cut + 1)..];
        return id.Trim();
    }

    /// <summary>
    /// Wyrazenie uruchamiajace utwor o podanym tytule: znajduje jego wiersz i
    /// klika przycisk odtwarzania WEWNATRZ tego wiersza.
    ///
    /// Zwraca napis "ok" albo powod niepowodzenia, zeby warstwa wywolujaca
    /// mogla powiedziec uzytkownikowi, CZEGO nie znaleziono, zamiast milczec.
    /// </summary>
    public static string PlayTrackExpression(string trackTitle)
    {
        var literal = JavaScriptStringLiteral(trackTitle);
        return "(()=>{"
            + $"const cel={literal};"
            + "const norm=t=>(t||'').replace(/\\s+/g,' ').trim().toLowerCase();"
            + "const szukany=norm(cel);"
            + $"const wiersze=[...document.querySelectorAll('{TrackRowSelector}')];"
            + "if(!wiersze.length) return 'brak-listy-utworow';"
            + "let w=wiersze.find(r=>norm((r.querySelector('[data-test=\"table-row-title\"]')||r).textContent).startsWith(szukany));"
            + "if(!w) w=wiersze.find(r=>norm(r.textContent).includes(szukany));"
            + "if(!w) return 'brak-utworu';"
            + "const b=w.querySelector('[data-test=\"play-button\"]');"
            + "if(!b) return 'brak-przycisku-w-wierszu';"
            + "b.click();"
            + "return 'ok';"
            + "})()";
    }

    /// <summary>
    /// Wyrazenie uruchamiajace CALY album albo playliste od pierwszego utworu.
    /// Celowo omija sasiedni przycisk odtwarzania losowego.
    /// </summary>
    public static string PlayAllExpression()
    {
        return "(()=>{"
            + "const b=document.querySelector('[data-test=\"play-all\"]');"
            + "if(!b) return 'brak-przycisku';"
            + "b.click();"
            + "return 'ok';"
            + "})()";
    }

    /// <summary>
    /// Wyrazenie oddajace liczbe wierszy utworow. Sluzy do czekania, az strona
    /// sie zaladuje - klikniecie w pusta jeszcze liste nic by nie dalo.
    /// </summary>
    public static string TrackRowCountExpression()
        => $"document.querySelectorAll('{TrackRowSelector}').length";

    /// <summary>
    /// Czyta odpowiedz wyrazenia sterujacego i zamienia powod techniczny na
    /// zdanie dla uzytkownika. Cisza po nieudanej probie jest niedopuszczalna.
    /// </summary>
    public static bool IsSuccess(string? answer, string displayName, out string? message)
    {
        var value = (answer ?? string.Empty).Trim();
        if (string.Equals(value, "ok", StringComparison.Ordinal))
        {
            message = null;
            return true;
        }

        message = value switch
        {
            "brak-listy-utworow" => "TIDAL nie zdążył wczytać listy utworów",
            "brak-utworu" => $"Nie znalazłem w TIDALu utworu {displayName}",
            "brak-przycisku-w-wierszu" => $"TIDAL nie pozwala odtworzyć {displayName} z listy",
            "brak-przycisku" => "TIDAL nie pokazuje przycisku odtwarzania całości",
            "" => "TIDAL nie odpowiedział",
            _ => $"TIDAL odmówił odtworzenia: {value}"
        };
        return false;
    }

    /// <summary>
    /// Buduje zadanie odtworzenia dla wskazanego elementu albo oddaje powod,
    /// dla ktorego to niemozliwe.
    /// </summary>
    public static bool TryBuildRequest(
        TidalDesktopPlayKind kind,
        string? trackTitle,
        string? albumExternalId,
        string? containerExternalId,
        bool containerIsPlaylist,
        string displayName,
        out TidalDesktopPlayRequest? request,
        out string? reason)
    {
        request = null;
        reason = null;

        if (kind == TidalDesktopPlayKind.Track)
        {
            if (string.IsNullOrWhiteSpace(trackTitle))
            {
                reason = "Ten utwór nie ma tytułu, więc nie da się go wskazać w TIDALu";
                return false;
            }
            if (NormalizeId(albumExternalId).Length == 0)
            {
                reason = "Ten utwór nie ma przypisanego albumu, a oryginalny TIDAL otwiera utwory tylko przez stronę albumu";
                return false;
            }

            request = new TidalDesktopPlayRequest
            {
                Kind = TidalDesktopPlayKind.Track,
                PageUri = AlbumPageUri(albumExternalId!),
                TrackTitle = trackTitle!.Trim(),
                DisplayName = displayName
            };
            return true;
        }

        if (NormalizeId(containerExternalId).Length == 0)
        {
            reason = containerIsPlaylist
                ? "Ta playlista nie ma identyfikatora TIDAL, więc nie da się jej otworzyć w oryginalnym programie"
                : "Ten album nie ma identyfikatora TIDAL, więc nie da się go otworzyć w oryginalnym programie";
            return false;
        }

        request = new TidalDesktopPlayRequest
        {
            Kind = TidalDesktopPlayKind.Container,
            PageUri = containerIsPlaylist
                ? PlaylistPageUri(containerExternalId!)
                : AlbumPageUri(containerExternalId!),
            DisplayName = displayName
        };
        return true;
    }

    /// <summary>
    /// Zamienia napis w bezpieczny literal JavaScript. Tytuly utworow zawieraja
    /// apostrofy i cudzyslowy, a wklejenie ich wprost zepsuloby skladnie.
    /// </summary>
    public static string JavaScriptStringLiteral(string? value)
        => JsonSerializer.Serialize(value ?? string.Empty);
}

using AccessibleMediaController.Core.Tidal;

/// <summary>
/// Testy planu odtwarzania w ORYGINALNYM programie TIDAL.
///
/// Sprawdzamy warstwe decyzyjna, ktora dziala bez zywego TIDALa: adresy stron,
/// wymagane dane wejsciowe, bezpieczne skladanie wyrazen i zamiane powodu
/// niepowodzenia na zdanie dla uzytkownika. Same metody sterowania interfejsem
/// TIDALa sa zmierzone recznie na zywym programie (tytul okna jako miernik).
/// </summary>
internal static class TidalDesktopPlaybackTests
{
    internal static void Run()
    {
        TrackUsesAlbumPage();
        TrackWithoutAlbumIsRejected();
        TrackWithoutTitleIsRejected();
        AlbumUsesAlbumPage();
        PlaylistUsesPlaylistPage();
        ContainerWithoutIdentifierIsRejected();
        IdentifierIsTrimmedToBareId();
        BrowseHostIsNotUsed();
        TrackTitleIsSafelyQuoted();
        TrackExpressionClicksRowButton();
        PlayAllAvoidsShuffle();
        FailureReasonsBecomeSentences();
    }

    private static void TrackUsesAlbumPage()
    {
        var built = TidalDesktopPlaybackPlan.TryBuildRequest(
            TidalDesktopPlayKind.Track,
            "Kayleigh (2017 Remaster)",
            "81326515",
            null,
            false,
            "Kayleigh",
            out var request,
            out var reason);

        Check(built, "Utwór z albumem powinien dać się przekazać do TIDALa");
        Check(reason is null, "Poprawny utwór nie może zwracać powodu odmowy");
        Check(request!.PageUri == "https://desktop.tidal.com/album/81326515",
            "Utwór musi wskazywać stronę swojego albumu");
        Check(request.Kind == TidalDesktopPlayKind.Track, "Rodzaj żądania utworu");
        Check(request.TrackTitle == "Kayleigh (2017 Remaster)", "Tytuł utworu do wskazania wiersza");
    }

    private static void TrackWithoutAlbumIsRejected()
    {
        // Utworu nie da sie wskazac bez strony, na ktorej stoi jego wiersz -
        // i uzytkownik musi uslyszec dlaczego, a nie cisze.
        var built = TidalDesktopPlaybackPlan.TryBuildRequest(
            TidalDesktopPlayKind.Track,
            "Kayleigh",
            null,
            null,
            false,
            "Kayleigh",
            out var request,
            out var reason);

        Check(!built, "Utwór bez albumu nie może zostać przekazany");
        Check(request is null, "Odrzucone żądanie nie może zwracać planu");
        Check(!string.IsNullOrWhiteSpace(reason), "Odmowa musi mieć powód do zapowiedzenia");
        Check(reason!.Contains("album", StringComparison.OrdinalIgnoreCase),
            "Powód odmowy powinien wyjaśniać brak albumu");
    }

    private static void TrackWithoutTitleIsRejected()
    {
        var built = TidalDesktopPlaybackPlan.TryBuildRequest(
            TidalDesktopPlayKind.Track,
            "   ",
            "81326515",
            null,
            false,
            "bez nazwy",
            out var request,
            out var reason);

        Check(!built, "Utwór bez tytułu nie może zostać przekazany");
        Check(request is null, "Odrzucone żądanie nie może zwracać planu");
        Check(!string.IsNullOrWhiteSpace(reason), "Odmowa musi mieć powód");
    }

    private static void AlbumUsesAlbumPage()
    {
        var built = TidalDesktopPlaybackPlan.TryBuildRequest(
            TidalDesktopPlayKind.Container,
            null,
            null,
            "81326515",
            false,
            "Misplaced Childhood",
            out var request,
            out var reason);

        Check(built, "Album powinien dać się odtworzyć w całości");
        Check(reason is null, "Poprawny album nie może zwracać powodu odmowy");
        Check(request!.PageUri == "https://desktop.tidal.com/album/81326515", "Adres strony albumu");
        Check(request.Kind == TidalDesktopPlayKind.Container, "Rodzaj żądania całości");
    }

    private static void PlaylistUsesPlaylistPage()
    {
        var built = TidalDesktopPlaybackPlan.TryBuildRequest(
            TidalDesktopPlayKind.Container,
            null,
            null,
            "e6389047-0ffa-4084-9e7b-dcf4121d5a20",
            true,
            "Moja playlista",
            out var request,
            out var reason);

        Check(built, "Playlista powinna dać się odtworzyć w całości");
        Check(reason is null, "Poprawna playlista nie może zwracać powodu odmowy");
        Check(
            request!.PageUri == "https://desktop.tidal.com/playlist/e6389047-0ffa-4084-9e7b-dcf4121d5a20",
            "Playlista musi używać adresu playlisty, a nie albumu");
    }

    private static void ContainerWithoutIdentifierIsRejected()
    {
        var built = TidalDesktopPlaybackPlan.TryBuildRequest(
            TidalDesktopPlayKind.Container,
            null,
            null,
            "   ",
            false,
            "Album bez numeru",
            out var request,
            out var reason);

        Check(!built, "Całość bez identyfikatora nie może zostać przekazana");
        Check(request is null, "Odrzucone żądanie nie może zwracać planu");
        Check(!string.IsNullOrWhiteSpace(reason), "Odmowa musi mieć powód");
    }

    private static void IdentifierIsTrimmedToBareId()
    {
        // Zrodla podaja identyfikatory w roznej postaci; adres strony musi
        // zawierac sam identyfikator, inaczej TIDAL otworzy pustke.
        Check(TidalDesktopPlaybackPlan.NormalizeId("tidal:album:81326515") == "81326515",
            "Identyfikator ze ścieżki powinien zostać obcięty do numeru");
        Check(TidalDesktopPlaybackPlan.NormalizeId("  81326515  ") == "81326515",
            "Odstępy nie mogą trafiać do adresu");
        Check(TidalDesktopPlaybackPlan.NormalizeId(null).Length == 0,
            "Brak identyfikatora musi dać pusty wynik, nie wyjątek");

        var built = TidalDesktopPlaybackPlan.TryBuildRequest(
            TidalDesktopPlayKind.Container,
            null,
            null,
            "tidal:album:81326515",
            false,
            "Misplaced Childhood",
            out var request,
            out _);

        Check(built, "Identyfikator ze ścieżki powinien zostać przyjęty");
        Check(request!.PageUri == "https://desktop.tidal.com/album/81326515",
            "Adres musi zawierać sam numer albumu");
    }

    private static void BrowseHostIsNotUsed()
    {
        // Zmierzone: strona tidal.com/browse nie wystawia listy utworow, wiec
        // klikniecie w wiersz nie mialoby czego znalezc.
        Check(!TidalDesktopPlaybackPlan.AlbumPageUri("81326515").Contains("/browse/", StringComparison.Ordinal),
            "Album nie może używać strony przeglądania, która nie ma listy utworów");
        Check(TidalDesktopPlaybackPlan.DesktopHost == "https://desktop.tidal.com",
            "Host aplikacji desktop");
    }

    private static void TrackTitleIsSafelyQuoted()
    {
        // Tytul z apostrofem nie moze zepsuc wyrazenia wysylanego do TIDALa.
        var expression = TidalDesktopPlaybackPlan.PlayTrackExpression("Don't Stop 'Til You Get Enough");

        Check(!expression.Contains("'Til You", StringComparison.Ordinal),
            "Apostrof w tytule musi zostać zabezpieczony przed zepsuciem wyrażenia");
        // Cudzyslow moze byc zabezpieczony jako \" albo jako \u0022 - liczy sie
        // to, ze nie wychodzi z literalu i nie psuje skladni wyrazenia.
        var quoted = TidalDesktopPlaybackPlan.JavaScriptStringLiteral("a\"b");
        Check(quoted.StartsWith('"') && quoted.EndsWith('"'),
            "Literal musi być zamknięty w cudzysłowach");
        Check(!quoted[1..^1].Contains('"', StringComparison.Ordinal),
            "Cudzysłów w tytule nie może wystąpić w literalu bez zabezpieczenia");
        Check(quoted.Contains("a", StringComparison.Ordinal) && quoted.Contains("b", StringComparison.Ordinal),
            "Zabezpieczenie nie może gubić treści tytułu");
    }

    private static void TrackExpressionClicksRowButton()
    {
        // Zmierzone: dziala klik przycisku W WIERSZU, a nie fokus i Enter.
        var expression = TidalDesktopPlaybackPlan.PlayTrackExpression("Kayleigh (2017 Remaster)");

        Check(expression.Contains("tracklist-row", StringComparison.Ordinal),
            "Wyrażenie musi szukać wierszy listy utworów TIDALa");
        Check(expression.Contains("play-button", StringComparison.Ordinal),
            "Wyrażenie musi kliknąć przycisk odtwarzania wewnątrz wiersza");
        Check(!expression.Contains("table-list-row", StringComparison.Ordinal),
            "Nieaktualny selektor wiersza nie może wrócić do kodu");
        Check(expression.Contains("brak-utworu", StringComparison.Ordinal),
            "Wyrażenie musi umieć powiedzieć, że nie znalazło utworu");
    }

    private static void PlayAllAvoidsShuffle()
    {
        var expression = TidalDesktopPlaybackPlan.PlayAllExpression();

        Check(expression.Contains("play-all", StringComparison.Ordinal),
            "Wyrażenie musi klikać przycisk odtwarzania całości");
        Check(!expression.Contains("shuffle", StringComparison.OrdinalIgnoreCase),
            "Odtwarzanie całości nie może trafić w przycisk losowej kolejności");
    }

    private static void FailureReasonsBecomeSentences()
    {
        Check(TidalDesktopPlaybackPlan.IsSuccess("ok", "Kayleigh", out var brak),
            "Odpowiedź ok musi być uznana za powodzenie");
        Check(brak is null, "Powodzenie nie może dawać komunikatu o błędzie");

        foreach (var answer in new[] { "brak-listy-utworow", "brak-utworu", "brak-przycisku-w-wierszu", "brak-przycisku", "", "cos-nieznanego" })
        {
            Check(!TidalDesktopPlaybackPlan.IsSuccess(answer, "Kayleigh", out var message),
                $"Odpowiedź {answer} nie może być uznana za powodzenie");
            Check(!string.IsNullOrWhiteSpace(message),
                $"Nieudana próba musi mieć komunikat dla użytkownika: {answer}");
            Check(!message!.Contains('-', StringComparison.Ordinal) || message.Contains(' ', StringComparison.Ordinal),
                $"Komunikat musi być zdaniem, nie kodem technicznym: {answer}");
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}

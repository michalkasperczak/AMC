using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Core.Spotify;
using AccessibleMediaController.Windows.Services;

namespace AccessibleMediaController.Windows;

/// <summary>
/// Preset albumu Spotify (Ctrl+Shift+cyfra) ODTWARZA album, a nie otwiera go
/// na liscie.
///
/// ZGLOSZENIE Michala (20.09.2026): Ctrl+Shift+5 z przypisanym albumem Spotify
/// "Nieklikalnosc" (Maja Kleszcz) przenosilo zaznaczenie do albumu w glownym
/// zbiorze i czytnik czytal "9664 z 10776". Ogolna galaz
/// <c>ActivatePreset</c> dla pozycji, ktora NIE jest utworem, stacja ani
/// odcinkiem, wola <c>SelectSessionBrowserItem</c> + <c>NavigateTo</c>. Dla
/// albumu to nie jest odtwarzanie - to nawigacja.
///
/// Zasady, ktorych trzeba sie tu trzymac:
/// - Widok, zaznaczenie i fokus listy NIE MOGA sie zmienic. Preset ma zagrac
///   w tle; czytnik ekranu zostaje tam, gdzie byl uzytkownik. Dlatego ta droga
///   NIE wola <c>NavigateTo</c>, <c>SelectSessionBrowserItem</c> ani
///   <c>FocusMediaList</c>.
/// - Utwory albumu pobieramy TA SAMA droga co strzalka w prawo
///   (<c>LoadSpotifyContainerItemsAsync</c>) i rejestrujemy TYM SAMYM
///   mechanizmem (<c>StoreSpotifyContainerForSession</c>), zeby album nie
///   dostal drugiej kopii pobierania ani wlasnego cache. Rejestracja nie
///   promuje niczego do biblioteki konta: pozycje katalogu wchodza do sesji
///   bez flag kolekcji, dokladnie jak przy otwarciu albumu.
/// - Kolejnosc utworow bierzemy z ZAREJESTROWANEGO widoku kontenera, bo dla
///   drugiej sesji Spotify pozycje sa kopiami z innymi identyfikatorami;
///   kolejnosc albumu zostaje kolejnoscia z Spotify.
/// - MOWA PO UDANYM WLACZENIU: SAMA NAZWA ALBUMU. Bez numeru presetu i bez
///   doklejania tytulu utworu. Numer presetu zostaje TYLKO w diagnostyce
///   bledu, pustego albumu i braku przypisania.
/// - Do kontekstu i do odtwarzania biora sie tylko utwory GRYWALNE
///   (<c>IsAvailable</c>). Niegrywalny pierwszy utwor nie moze zastapic
///   dzialajacego kontekstu, bo tor dzwieku i tak go odrzuci. Gdy grywalnego
///   nie ma ani jednego, kontekst zostaje nietkniety.
/// - Spozniona odpowiedz sieci (uzytkownik przycisnal dwa presety pod rzad)
///   NIE MOZE zagrac. <c>_spotifyNavigationVersion</c> na to NIE WYSTARCZA:
///   liczy tylko albumy i nawigacje, wiec stare zadanie albumu przebilo by
///   nowszy preset UTWORU, a wczesne wyjscie powtorzonego albumu wraca przed
///   inkrementacja, wiec czekajacy album B przebil by powtorzony, grajacy
///   album A. Dlatego kazdy preset pozycji w sesji Spotify podbija wlasny
///   licznik <c>_spotifyPresetPlaybackVersion</c> (tez powtorzenie i preset
///   utworu), a spozniona odpowiedz sprawdza dodatkowo, ze sesja jest wciaz ta
///   sama i ze kontekst odtwarzania nie zmienil sie pod nami - to lapie takze
///   zwykle wlaczenie czegos z listy w trakcie pobierania.
///   Tu NIE wolno uzyc <c>CanPresentSpotifyResponse</c>: ono slusznie pilnuje
///   widoku i zaznaczenia przed RUSZENIEM listy, a preset ma zagrac wlasnie
///   wtedy, gdy uzytkownik chodzi po liscie.
/// - POWTORZENIE TEGO SAMEGO PRESETU (zatwierdzone przez Michala 20.09.2026):
///   jesli ten sam album gra dalej - nawet gdy zeszlo juz na dalszy utwor -
///   preset MOWI sama nazwe albumu. NIE wraca na pierwszy utwor, nie
///   przebudowuje kolejki, nie pyta Spotify o nic. Jesli ten sam album stoi na
///   pauzie, preset WZNAWIA biezacy utwor od jego pozycji (nie od pierwszego).
///   Gdy gra co innego albo przypisanie sie zmienilo, preset dziala normalnie.
///   Tozsamosc mierzymy ALBUMEM I SESJA (widok kontekstu + biezaca pozycja
///   nalezaca do kontekstu tego albumu), nie ostatnio uzyta cyfra - inaczej
///   zamierzone wlaczenie innego albumu po zmianie kontekstu zostaloby
///   polkniete jako "powtorzenie".
/// - Ta straz nalezy WYLACZNIE do drogi albumu Spotify. Ogolna galaz
///   <c>ActivatePreset</c> dla utworow zostaje nietknieta (zmiany dla folderu
///   lokalnego, utworow i TIDAL prowadzi ktos inny).
/// - Pusty album, odmowa Spotify i blad sieci mowia, CO sie stalo, i NIE
///   przerywaja tego, co aktualnie gra. Anulowanie tylko sie loguje; mowi o
///   nim wylacznie zadanie, ktore jest jeszcze aktualne.
/// </summary>
public partial class MainWindow
{
    /// <summary>
    /// Licznik ODTWARZANIA z presetu w sesji Spotify. Podbija go KAZDY preset
    /// pozycji Spotify - albumu, powtorzonego albumu i utworu - zeby czekajaca
    /// odpowiedz albumu nie przebila nowszej decyzji uzytkownika.
    /// </summary>
    private long _spotifyPresetPlaybackVersion;

    /// <summary>
    /// Uniewaznia czekajace pobranie albumu presetu. Wola to KAZDA droga
    /// presetu w sesji Spotify, takze preset utworu.
    /// </summary>
    private long InvalidatePendingSpotifyPresetPlayback() => ++_spotifyPresetPlaybackVersion;

    /// <summary>
    /// Czy preset wskazuje album Spotify, ktory trzeba ZAGRAC zamiast otworzyc.
    /// </summary>
    private static bool IsSpotifyAlbumPresetTarget(string sessionId, MediaItem item) =>
        SpotifyPlaybackSettingsResolver.IsSpotifySession(sessionId)
        && item.Kind == MediaItemKind.Album
        && item.ExternalId is { Length: > 0 };

    /// <summary>
    /// Czy odtwarzanie stoi WLASNIE na tym albumie: kontekst odtwarzania sesji
    /// jest kontekstem tego albumu i biezaca pozycja do niego nalezy. Dalszy
    /// utwor albumu tez sie liczy - uzytkownik nadal slucha tego albumu.
    /// </summary>
    private bool IsSpotifyAlbumPresetAlreadyActive(DemoMediaSession session, MediaItem album)
    {
        if (!session.HasCurrentItem) return false;
        if (!session.IsPlaying && !session.IsPaused) return false;
        var navigation = GetSessionNavigationState(session.Id);
        if (!string.Equals(
                navigation.PlaybackContextView,
                SpotifyContainerViewName(session.Id, album),
                StringComparison.Ordinal))
        {
            return false;
        }
        return session.PlaybackContextItemIds.Contains(session.CurrentItem.Id, StringComparer.Ordinal);
    }

    /// <summary>
    /// Powtorzony preset tego samego albumu. Zwraca <c>true</c>, gdy obsluzyl
    /// sprawe i nie trzeba nic pobierac ani odtwarzac od nowa.
    /// </summary>
    private bool TryHandleRepeatedSpotifyAlbumPreset(DemoMediaSession session, MediaItem album)
    {
        if (!IsSpotifyAlbumPresetAlreadyActive(session, album)) return false;
        if (session.IsPlaying)
        {
            // Gra dalej - nie wolno restartowac. Sama nazwa albumu.
            Announce(album.Title);
            return true;
        }
        // Pauza w tym samym albumie: wznow BIEZACY utwor od jego pozycji.
        session.TogglePlayback();
        RefreshPlaybackIndicators();
        UpdatePlaybackStatusBar();
        UpdateWindowTitle();
        Announce(album.Title);
        return true;
    }

    /// <summary>
    /// Odtwarza album Spotify przypisany do presetu, nie zmieniajac widoku,
    /// zaznaczenia ani fokusu. Wolane z <c>ActivatePreset</c>.
    /// </summary>
    private async Task PlaySpotifyAlbumPresetAsync(
        DemoMediaSession session,
        MediaItem album,
        string slotLabel)
    {
        var sessionAtStart = session.Id;
        // Podbijamy licznik PRZED wczesnym wyjsciem powtorzenia: inaczej
        // czekajacy album B przebilby powtorzony, grajacy album A.
        var requestVersion = InvalidatePendingSpotifyPresetPlayback();
        if (TryHandleRepeatedSpotifyAlbumPreset(session, album)) return;
        var navigationVersion = ++_spotifyNavigationVersion;
        try
        {
            var tracks = await LoadSpotifyContainerItemsAsync(album).ConfigureAwait(true);
            if (_isClosing) return;
            // Nowszy preset, nowsza nawigacja albo zmiana sesji: ta odpowiedz
            // jest nieaktualna i nie ma prawa przestawic odtwarzania.
            if (!IsCurrentSpotifyPresetRequest(requestVersion, navigationVersion, sessionAtStart)) return;

            if (tracks is null)
            {
                Announce($"Preset {slotLabel}: Spotify nie udostępnia zawartości albumu {album.Title}");
                return;
            }
            if (tracks.Count == 0)
            {
                Announce($"Preset {slotLabel}: album {album.Title} nie zawiera utworów do odtworzenia");
                return;
            }

            foreach (var track in tracks)
            {
                if (track.Kind != MediaItemKind.Track) continue;
                track.RelatedAlbumExternalId = album.ExternalId;
                track.RelatedAlbumTitle = album.Title;
            }

            // Rejestracja BEZ NavigateTo: pozycje wchodza do sesji i do cache
            // widoku albumu (gdy uzytkownik wejdzie tam pozniej, lista juz
            // jest), ale widok sie NIE zmienia.
            var viewName = StoreSpotifyContainerForSession(sessionAtStart, album, tracks);
            var ordered = _spotifyContainerViews.TryGetValue(viewName, out var stored)
                ? stored.Items
                : tracks;
            // Tylko GRYWALNE utwory: niegrywalny pierwszy utwor nie moze
            // zastapic dzialajacego kontekstu i wywrocic sie na torze dzwieku.
            var orderedIds = ordered
                .Where(item => item is { Kind: MediaItemKind.Track, IsAvailable: true })
                .Select(item => item.Id)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var first = orderedIds.Length == 0
                ? null
                : session.Items.FirstOrDefault(item =>
                    string.Equals(item.Id, orderedIds[0], StringComparison.Ordinal));
            if (first is null)
            {
                Announce($"Preset {slotLabel}: album {album.Title} nie zawiera utworów do odtworzenia");
                return;
            }

            session.SetPlaybackContext(orderedIds);
            var navigation = GetSessionNavigationState(sessionAtStart);
            navigation.PlaybackContextView = viewName;
            navigation.PlaybackContextItemIds = orderedIds.ToList();
            session.Play(first);
            RecordPlayback(session, first);
            SavePresetState(sessionAtStart);
            RefreshPlaybackIndicators();
            UpdatePlaybackStatusBar();
            UpdateWindowTitle();
            // Sama nazwa albumu: bez numeru presetu i bez tytulu utworu.
            Announce(album.Title);
            DiagnosticLog.Info(
                "spotify-preset",
                $"Preset {slotLabel} zagrał album {album.ExternalId}; utworów: {orderedIds.Length}.");
        }
        catch (OperationCanceledException exception)
        {
            // Anulowanie nie jest awaria, ale musi zostac slad. Mowi o nim tylko
            // zadanie, ktore jest jeszcze aktualne - stare milczy.
            DiagnosticLog.Info(
                "spotify-preset",
                $"Preset {slotLabel}: pobranie albumu {album.ExternalId} anulowano ({exception.GetType().Name}).");
            if (!_isClosing
                && IsCurrentSpotifyPresetRequest(requestVersion, navigationVersion, sessionAtStart))
            {
                Announce($"Preset {slotLabel}: pobieranie albumu {album.Title} przerwano");
            }
        }
        catch (Exception exception)
        {
            DiagnosticLog.Error(
                "spotify-preset",
                $"Preset {slotLabel} nie zagrał albumu {album.ExternalId}.",
                exception);
            if (!_isClosing
                && IsCurrentSpotifyPresetRequest(requestVersion, navigationVersion, sessionAtStart))
            {
                Announce($"Preset {slotLabel}: nie udało się odtworzyć albumu {album.Title}. {exception.Message}");
            }
        }
    }

    /// <summary>
    /// Czy odpowiedz nalezy jeszcze do NAJNOWSZEJ decyzji uzytkownika: ten sam
    /// licznik presetu, ta sama nawigacja i ta sama, wciaz wybrana sesja.
    /// </summary>
    private bool IsCurrentSpotifyPresetRequest(
        long requestVersion,
        long navigationVersion,
        string sessionAtStart) =>
        _spotifyPresetPlaybackVersion == requestVersion
        && _spotifyNavigationVersion == navigationVersion
        && string.Equals(_sessions.Current.Id, sessionAtStart, StringComparison.Ordinal);
}

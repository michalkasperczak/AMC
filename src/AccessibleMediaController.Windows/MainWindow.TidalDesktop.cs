using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using AccessibleMediaController.Core.Commands;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Core.Tidal;
using AccessibleMediaController.Windows.Services;

namespace AccessibleMediaController.Windows;

/// <summary>
/// Przekazywanie utworow, albumow i playlist ORYGINALNEMU programowi TIDAL.
///
/// Powod istnienia: wlasny silnik AMC dostaje z TIDALa tylko 30-sekundowe
/// probki, bo SDK nie pozwala odtwarzac calych utworow w cudzej aplikacji.
/// Oryginalny TIDAL desktop gra pelne utwory w jakosci ustawionej przez
/// uzytkownika, a dzieki portowi sterowania mozna mu wskazac konkretny element.
///
/// Podzial klawiszy uzgodniony z uzytkownikiem:
/// - Enter na UTWORZE  -> odtworz go w oryginalnym TIDALu,
/// - Enter na ALBUMIE lub PLAYLISCIE -> jak dotad, otwiera liste elementow,
/// - Ctrl+Enter na ALBUMIE lub PLAYLISCIE -> zagraj calosc od pierwszego utworu.
/// </summary>
public partial class MainWindow
{
    private TidalDesktopController? _tidalDesktop;
    private CancellationTokenSource? _tidalDesktopCancellation;

    private TidalDesktopController TidalDesktop =>
        _tidalDesktop ??= new TidalDesktopController(
            message => DiagnosticLog.Info("tidal-desktop", message));

    /// <summary>
    /// Czy wskazany element jest elementem TIDALa, ktory mozna oddac
    /// oryginalnemu programowi.
    /// </summary>
    private bool CanPlayInTidalDesktop(MediaItem? item)
    {
        if (item is null) return false;
        if (!string.Equals(_sessions.Current.Id, "tidal", StringComparison.Ordinal)) return false;

        return item.Kind switch
        {
            MediaItemKind.Track => item.ExternalId is { Length: > 0 },
            MediaItemKind.Album or MediaItemKind.Playlist => item.ExternalId is { Length: > 0 },
            _ => false
        };
    }

    /// <summary>
    /// Odtwarza pojedynczy utwor w oryginalnym TIDALu.
    ///
    /// Drugi parametr to LISTA, z ktorej utwor pochodzi, w kolejnosci widzianej
    /// przez uzytkownika. Zapamietujemy ja, zeby nastepny i poprzedni szly PO
    /// NIEJ, a nie kolejka wymyslona przez TIDALa - zgloszenie z 16.09.2026.
    /// </summary>
    private void PlayTrackInTidalDesktop(
        MediaItem track,
        IReadOnlyList<MediaItem>? listInOrder = null,
        string? sourceName = null)
    {
        if (!TidalDesktopPlaybackPlan.TryBuildRequest(
                TidalDesktopPlayKind.Track,
                track.Title,
                track.RelatedAlbumExternalId,
                containerExternalId: null,
                containerIsPlaylist: false,
                displayName: track.Title,
                out var request,
                out var reason))
        {
            DiagnosticLog.Warning("tidal-desktop",
                $"Nie zbudowano żądania dla utworu \"{track.Title}\" (identyfikator {track.ExternalId ?? "brak"}, album {track.RelatedAlbumExternalId ?? "brak"}): {reason}");
            Announce(reason ?? "Nie da się odtworzyć tego utworu w oryginalnym TIDALu");
            return;
        }

        if (listInOrder is { Count: > 0 })
        {
            _tidalTrackQueue.Capture(listInOrder, track, sourceName ?? string.Empty);
            DiagnosticLog.Info("tidal-desktop",
                $"Kolejka AMC: {_tidalTrackQueue.Count} utworów z listy \"{_tidalTrackQueue.SourceName}\", grający numer {_tidalTrackQueue.HumanPosition}.");
        }
        else
        {
            // Bez listy nie ma po czym chodzic - lepiej zapomniec stara kolejke
            // niz przeskakiwac po liscie, ktorej uzytkownik juz nie slucha.
            _tidalTrackQueue.Clear();
        }

        _ = RunTidalDesktopPlaybackAsync(request!);
    }

    /// <summary>
    /// Odtwarza caly album albo cala playliste w oryginalnym TIDALu.
    /// </summary>
    private void PlayContainerInTidalDesktop(MediaItem container)
    {
        var isPlaylist = container.Kind == MediaItemKind.Playlist;

        if (!TidalDesktopPlaybackPlan.TryBuildRequest(
                TidalDesktopPlayKind.Container,
                trackTitle: null,
                albumExternalId: null,
                containerExternalId: container.ExternalId,
                containerIsPlaylist: isPlaylist,
                displayName: container.Title,
                out var request,
                out var reason))
        {
            DiagnosticLog.Warning("tidal-desktop",
                $"Nie zbudowano żądania dla elementu \"{container.Title}\" ({container.KindLabel}, identyfikator {container.ExternalId ?? "brak"}): {reason}");
            Announce(reason ?? "Nie da się odtworzyć tego elementu w oryginalnym TIDALu");
            return;
        }

        _ = RunTidalDesktopPlaybackAsync(request!);
    }

    private async Task RunTidalDesktopPlaybackAsync(TidalDesktopPlayRequest request)
    {
        // Kolejne wywolanie przerywa poprzednie: uzytkownik moze szybko
        // przeskakiwac miedzy utworami, a dwa rownolegle sterowania tym samym
        // oknem TIDALa walczylyby o nie.
        var previous = _tidalDesktopCancellation;
        _tidalDesktopCancellation = new CancellationTokenSource();
        previous?.Cancel();
        previous?.Dispose();

        var token = _tidalDesktopCancellation.Token;

        try
        {
            Announce($"{request.DisplayName}: przekazuję do oryginalnego TIDALa");

            var result = await TidalDesktop.PlayAsync(request, restartConsent: false, token)
                .ConfigureAwait(true);

            if (token.IsCancellationRequested) return;

            if (result.NeedsRestartConsent)
            {
                // Pytanie zadajemy PO komunikacie o stanie, nie zamiast niego -
                // przy czytniku ekranu okno wychodzace na wierzch zjadloby
                // wcześniejszą zapowiedź.
                var answer = AccessibleDialog.Show(
                    this,
                    result.Message + ". Zamknąć oryginalny TIDAL i uruchomić go ponownie?",
                    "Odtwarzanie w oryginalnym TIDALu",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question,
                    MessageBoxResult.No);

                if (answer != MessageBoxResult.Yes)
                {
                    DiagnosticLog.Info("tidal-desktop",
                        $"Użytkownik odmówił ponownego uruchomienia TIDALa dla \"{request.DisplayName}\".");
                    Announce("Odtwarzanie w oryginalnym TIDALu anulowane");
                    return;
                }

                Announce($"{request.DisplayName}: uruchamiam ponownie oryginalny TIDAL");
                result = await TidalDesktop.PlayAsync(request, restartConsent: true, token)
                    .ConfigureAwait(true);

                if (token.IsCancellationRequested) return;
            }

            Announce(result.Message);

            if (result.Success)
            {
                // OD TEJ CHWILI transport idzie do oryginalnego TIDALa. Bez tego
                // spacja i strzalki trafialyby do wbudowanego odtwarzacza, czyli
                // do 30-sekundowych probek.
                _tidalDesktopHasPlayback = true;
                DiagnosticLog.Info("tidal-desktop",
                    $"Odtworzono \"{request.DisplayName}\" w oryginalnym TIDALu ({request.PageUri}). Transport przekazany TIDALowi.");
            }
            else
            {
                DiagnosticLog.Warning("tidal-desktop",
                    $"Nie odtworzono \"{request.DisplayName}\": {result.Message}");
            }
        }
        catch (OperationCanceledException)
        {
            DiagnosticLog.Info("tidal-desktop",
                $"Przekazywanie \"{request.DisplayName}\" przerwane nowym poleceniem.");
        }
        catch (Exception ex)
        {
            DiagnosticLog.Warning("tidal-desktop",
                $"Błąd przekazywania \"{request.DisplayName}\": {ex.GetType().Name}: {ex.Message}");
            Announce("Nie udało się przekazać elementu oryginalnemu TIDALowi");
        }
    }

    /// <summary>
    /// Wykonuje polecenie transportu na ORYGINALNYM TIDALu. Gdy sie nie uda
    /// (TIDAL zamkniety, nic nie gra), oddaje polecenie wbudowanemu odtwarzaczowi,
    /// zeby klawisz nigdy nie byl "martwy".
    /// </summary>
    private async Task RouteTransportToTidalDesktopAsync(string commandId)
    {
        var handled = commandId switch
        {
            CommandIds.PlayPause => await TryTogglePlayPauseInTidalDesktopAsync().ConfigureAwait(true),
            CommandIds.Next => await TrySkipInTidalDesktopAsync(true).ConfigureAwait(true),
            CommandIds.Previous => await TrySkipInTidalDesktopAsync(false).ConfigureAwait(true),
            _ => await TryAnnounceTidalDesktopNowPlayingAsync().ConfigureAwait(true)
        };

        if (handled) return;

        // Sesja SMTC zniknela - flaga jest zdjeta w funkcjach powyzej, wiec
        // to wywolanie NIE wroci tutaj i nie zapetli sie.
        DiagnosticLog.Info("tidal-smtc",
            $"Polecenie {commandId} wraca do wbudowanego odtwarzacza: oryginalny TIDAL nie odpowiada.");
        ExecuteCommand(commandId);
    }

    // ----------------------------------------------------------------------
    // STEROWANIE UTWOREM, KTORY JUZ GRA W ORYGINALNYM TIDALU
    //
    // Zgloszenie uzytkownika 15.09.2026: "sterowanie generalnie dziala, ale
    // przekazuje tylko pojedyncze nagranie - nastepny/poprzedni nie dzialaja,
    // bo AMC tylko wywoluje Tidala i sesja nie ma go w playerze".
    //
    // Przyczyna byla dokladnie taka: TidalDesktopController konczyl robote w
    // chwili, gdy TIDAL zaczynal grac. Od tej sekundy spacja i strzalki szly do
    // wbudowanego odtwarzacza WebView2 (30-sekundowe probki), a nie do TIDALa.
    // ----------------------------------------------------------------------

    private ExternalMediaController? _externalMedia;

    private ExternalMediaController ExternalMedia =>
        _externalMedia ??= new ExternalMediaController(
            message => DiagnosticLog.Info("tidal-smtc", message));

    /// <summary>
    /// Ustawiane, gdy oddalismy odtwarzanie oryginalnemu TIDALowi. Dopoki jest
    /// true, klawisze transportu ida do NIEGO, nie do wbudowanego odtwarzacza.
    /// </summary>
    private bool _tidalDesktopHasPlayback;

    /// <summary>
    /// Kolejka utworow prowadzona PRZEZ AMC: kolejnosc z listy uzytkownika,
    /// nie z kolejki oryginalnego TIDALa.
    /// </summary>
    private readonly TidalDesktopTrackQueue _tidalTrackQueue = new();

    /// <summary>
    /// Przesuwa sie po kolejce AMC i WSKAZUJE TIDALowi konkretny utwor.
    ///
    /// Kosztuje to otwarcie strony albumu w TIDALu (2-3 sekundy), ale tylko tak
    /// da sie utrzymac kolejnosc z listy - polecenie "nastepny" przez sesje
    /// multimediow oddaje wybor TIDALowi i wtedy leci jego kolejnosc.
    /// </summary>
    private async Task<bool> TrySkipWithinAmcQueueAsync(bool forward)
    {
        if (!_tidalTrackQueue.TryMove(forward, out var track) || track is null)
        {
            // Koniec listy MUSI byc powiedziany. Cisza brzmi jak zepsuty klawisz.
            Announce(_tidalTrackQueue.EndOfQueueMessage(forward));
            return true;
        }

        if (!TidalDesktopPlaybackPlan.TryBuildRequest(
                TidalDesktopPlayKind.Track,
                track.Title,
                track.RelatedAlbumExternalId,
                containerExternalId: null,
                containerIsPlaylist: false,
                displayName: track.Title,
                out var request,
                out var reason))
        {
            DiagnosticLog.Warning("tidal-desktop",
                $"Kolejka AMC: nie zbudowano żądania dla \"{track.Title}\": {reason}");
            Announce(reason ?? "Nie da się odtworzyć tego utworu w oryginalnym TIDALu");
            return true;
        }

        DiagnosticLog.Info("tidal-desktop",
            $"Kolejka AMC: {(forward ? "następny" : "poprzedni")} to \"{track.Title}\", numer {_tidalTrackQueue.HumanPosition} z {_tidalTrackQueue.Count}.");

        await RunTidalDesktopPlaybackAsync(request!).ConfigureAwait(true);
        return true;
    }

    /// <summary>
    /// Czy klawisze transportu (spacja, nastepny, poprzedni) maja iść do
    /// oryginalnego TIDALa.
    /// </summary>
    private bool ShouldRouteTransportToTidalDesktop =>
        _tidalDesktopHasPlayback
        && string.Equals(_sessions.Current.Id, "tidal", StringComparison.Ordinal);

    /// <summary>
    /// Gra albo pauza w oryginalnym TIDALu. Zwraca false, gdy nie ma czym
    /// sterowac - wtedy wolajacy ma zrobic to, co robil dotad.
    /// </summary>
    private async Task<bool> TryTogglePlayPauseInTidalDesktopAsync()
    {
        if (!ShouldRouteTransportToTidalDesktop) return false;

        var state = await ExternalMedia.GetStateAsync().ConfigureAwait(true);
        if (!state.HasSession)
        {
            // TIDAL zamkniety albo nic nie gra - oddajemy sterowanie z powrotem
            // wbudowanemu odtwarzaczowi, zeby spacja nie przestala dzialac.
            _tidalDesktopHasPlayback = false;
            return false;
        }

        if (!await ExternalMedia.TogglePlayPauseAsync().ConfigureAwait(true)) return false;

        // Mowimy stan DOCELOWY, nie ten sprzed polecenia.
        Announce(state.IsPlaying ? "Wstrzymano" : "Odtwarzanie");
        return true;
    }

    /// <summary>Nastepny albo poprzedni utwor.</summary>
    ///
    /// NAJPIERW proba po LISCIE AMC (playlista, album, Ulubione - to, co widzi
    /// uzytkownik), bo o to bylo zgloszenie z 16.09.2026. Dopiero gdy kolejki
    /// AMC nie ma, prosimy oryginalny TIDAL o jego wlasny nastepny utwor.
    private async Task<bool> TrySkipInTidalDesktopAsync(bool forward)
    {
        if (!ShouldRouteTransportToTidalDesktop) return false;

        if (_tidalTrackQueue.HasQueue && await TrySkipWithinAmcQueueAsync(forward).ConfigureAwait(true))
            return true;

        if (!await ExternalMedia.IsAvailableAsync().ConfigureAwait(true))
        {
            _tidalDesktopHasPlayback = false;
            return false;
        }

        var done = forward
            ? await ExternalMedia.NextAsync().ConfigureAwait(true)
            : await ExternalMedia.PreviousAsync().ConfigureAwait(true);

        if (!done)
        {
            Announce(forward
                ? "Oryginalny TIDAL nie ma następnego utworu"
                : "Oryginalny TIDAL nie ma poprzedniego utworu");
            return true;
        }

        // TIDAL potrzebuje chwili, zeby zglosic nowy tytul. Bez tego czytnik
        // przeczytalby jeszcze poprzedni utwor.
        await Task.Delay(TimeSpan.FromMilliseconds(700)).ConfigureAwait(true);
        var state = await ExternalMedia.GetStateAsync().ConfigureAwait(true);
        Announce(state.HasSession && state.Title.Length > 0
            ? (state.Artist.Length > 0 ? $"{state.Title}, {state.Artist}" : state.Title)
            : (forward ? "Następny utwór" : "Poprzedni utwór"));
        return true;
    }

    /// <summary>
    /// Przeskok KOLEJKA ORYGINALNEGO TIDALA, celowo z pominieciem listy AMC.
    /// Pod Shift+PageDown / Shift+PageUp - dla tego, kto chce isc dalej tak,
    /// jak proponuje TIDAL, i chce tego natychmiast.
    /// </summary>
    private async Task SkipUsingTidalOwnQueueAsync(bool forward)
    {
        if (!ShouldRouteTransportToTidalDesktop) return;

        if (!await ExternalMedia.IsAvailableAsync().ConfigureAwait(true))
        {
            _tidalDesktopHasPlayback = false;
            Announce("Oryginalny TIDAL nie odpowiada");
            return;
        }

        var done = forward
            ? await ExternalMedia.NextAsync().ConfigureAwait(true)
            : await ExternalMedia.PreviousAsync().ConfigureAwait(true);

        if (!done)
        {
            Announce(forward
                ? "Oryginalny TIDAL nie ma następnego utworu"
                : "Oryginalny TIDAL nie ma poprzedniego utworu");
            return;
        }

        // Kolejnosc idzie teraz TIDALem, wiec kolejka AMC przestala opisywac
        // rzeczywistosc - jej dalsze uzycie przeskakiwaloby w zle miejsce.
        _tidalTrackQueue.Clear();

        await Task.Delay(TimeSpan.FromMilliseconds(700)).ConfigureAwait(true);
        var state = await ExternalMedia.GetStateAsync().ConfigureAwait(true);
        var opis = state.HasSession && state.Title.Length > 0
            ? (state.Artist.Length > 0 ? $"{state.Title}, {state.Artist}" : state.Title)
            : (forward ? "Następny utwór" : "Poprzedni utwór");
        Announce($"Kolejka TIDALa: {opis}");
    }

    /// <summary>
    /// Czas utworu granego przez ORYGINALNY TIDAL - Ctrl+Shift+E, R, T.
    ///
    /// Zgloszenie uzytkownika 16.09.2026: "Ctrl+Shift+E R i T nie czyta czasu
    /// utworu, czyta go jako zero". Przyczyna: te skroty pytaly wlasny silnik
    /// AMC, ktory przy TIDALu NIC nie gra (transport oddany TIDALowi), wiec
    /// zwracal zero. Windows zna ten czas - trzeba go tylko zapytac.
    /// </summary>
    private async Task<bool> TryAnnounceTidalDesktopTimeAsync(string commandId)
    {
        if (!ShouldRouteTransportToTidalDesktop) return false;

        var state = await ExternalMedia.GetStateAsync().ConfigureAwait(true);
        if (!state.HasSession)
        {
            // Bez sesji nie wiemy nic - mowimy to wprost, zeby nie zgadywac.
            Announce("Oryginalny TIDAL nie podaje teraz czasu utworu");
            return true;
        }

        if (commandId == CommandIds.TimeElapsed)
        {
            Announce(state.HasPosition
                ? $"Czas od początku: {CommandRouter.FormatTime(state.Position)}"
                : "Oryginalny TIDAL nie podaje czasu od początku");
            return true;
        }

        if (commandId == CommandIds.TimeTotal)
        {
            Announce(state.Duration > TimeSpan.Zero
                ? $"Czas całkowity: {CommandRouter.FormatTime(state.Duration)}"
                : "Oryginalny TIDAL nie podaje czasu całkowitego");
            return true;
        }

        // Czas pozostaly wymaga OBU liczb; brak ktorejkolwiek trzeba powiedziec,
        // bo inaczej odjęlibyśmy od zera i wyszlaby nieprawda.
        if (state.Duration <= TimeSpan.Zero || !state.HasPosition)
        {
            Announce("Oryginalny TIDAL nie podaje czasu pozostałego");
            return true;
        }
        var pozostalo = state.Position >= state.Duration
            ? TimeSpan.Zero
            : state.Duration - state.Position;
        Announce($"Czas pozostały: {CommandRouter.FormatTime(pozostalo)}");
        return true;
    }

    /// <summary>Co gra teraz w oryginalnym TIDALu - do odczytania na zadanie.</summary>
    private async Task<bool> TryAnnounceTidalDesktopNowPlayingAsync()
    {
        if (!ShouldRouteTransportToTidalDesktop) return false;

        var state = await ExternalMedia.GetStateAsync().ConfigureAwait(true);
        if (!state.HasSession)
        {
            _tidalDesktopHasPlayback = false;
            return false;
        }

        var stan = state.IsPlaying ? "odtwarzanie" : "wstrzymane";
        var opis = state.Artist.Length > 0
            ? $"{state.Title}, {state.Artist}"
            : state.Title;
        // Dlugosc podajemy, bo TIDAL ja zglasza. POZYCJI nie - zawsze zwraca
        // zero (zmierzone 11.09.2026), wiec nie ma czego mowic.
        var dlugosc = state.Duration > TimeSpan.Zero
            ? $", długość {(int)state.Duration.TotalMinutes} minut {state.Duration.Seconds} sekund"
            : string.Empty;
        Announce($"Oryginalny TIDAL, {stan}: {opis}{dlugosc}");
        return true;
    }
}

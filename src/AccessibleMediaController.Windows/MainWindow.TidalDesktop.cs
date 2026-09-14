using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
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
    /// </summary>
    private void PlayTrackInTidalDesktop(MediaItem track)
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
                DiagnosticLog.Info("tidal-desktop",
                    $"Odtworzono \"{request.DisplayName}\" w oryginalnym TIDALu ({request.PageUri}).");
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
}

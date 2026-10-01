using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AccessibleMediaController.Core.Sonos;

/// <summary>
/// ODCZYT PLAYLIST SONOSA domu. Jedna operacja, jedno zapytanie GET.
///
/// Ta czesc klienta korzysta DOKLADNIE z tego samego transportu co odczyt domow,
/// grup i ulubionych - prywatne ReadAsync, Parse, Array, Text: staly host HTTPS,
/// brak przekierowan i ciasteczek, naglowki na POJEDYNCZYM zadaniu, deadline
/// obejmujacy naglowki i cialo, ograniczone czytanie (256 KiB), UTF-8 ze sciscie
/// sprawdzanym kodowaniem, odrzucanie duplikatow pol JSON, MaxDepth 16. Nic z
/// tego nie jest tu kopiowane ani liberalizowane.
///
/// RoZNICA WOBEC ULUBIONYCH, ktorej nie wolno zatrzec: w playlistsList zarowno
/// version, jak i playlists sa OPCJONALNE i nullable. Dlatego Text i Array ida
/// tu BEZ required:true - brak pola playlists i playlists:null to dla playlist
/// NORMALNY sukces z pusta lista, a nie "usluga nic nie powiedziala". Przy
/// ulubionych jest odwrotnie i tamta regula zostaje tam, gdzie jest.
///
/// Czego tu NIE MA i byc nie moze:
///  * ZADNEGO POST (uruchomienie playlisty ma osobna metode i osobny kontrakt),
///  * zadnego cache, pollingu ani subskrypcji zdarzenia playlistsVersionChange,
///  * zadnego odswiezania tokenu ani kasowania konta przy 401 - to warstwa
///    koordynatora, nie transport,
///  * zadnej PARTIAL listy: pierwsza niezgodnosc struktury odrzuca CALA
///    odpowiedz i AMC nie pokazuje polowy playlist jako calosci.
/// </summary>
public sealed partial class SonosControlApiClient : ISonosPlaylistsApi
{
    /// <summary>
    /// GET /households/{householdId}/playlists - playlisty Sonosa domu.
    ///
    /// Zwrocona lista niesie LITERALNIE ten householdId, o ktory pytalismy (bez
    /// trim i bez zmiany wielkosci liter), zeby wolajacy mogl odrzucic wynik,
    /// ktory przyszedl po zmianie domu.
    /// </summary>
    public async Task<SonosPlaylistsOutcome> GetPlaylistsAsync(
        string? accessToken, string? householdId, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return SonosPlaylistsOutcome.Failure(SonosControlApiStatus.Canceled);
        }

        if (!SonosHouseholdIdPolicy.TryEncode(householdId, out var encoded))
        {
            return SonosPlaylistsOutcome.Failure(SonosControlApiStatus.InvalidConfiguration);
        }

        var uri = new Uri(_configuration.Origin, "households/" + encoded + "/playlists");
        var reply = await ReadAsync(uri, accessToken, cancellationToken).ConfigureAwait(false);
        if (reply.Status != SonosControlApiStatus.Success)
        {
            return SonosPlaylistsOutcome.Failure(reply.Status);
        }

        try
        {
            using var document = Parse(reply.Body!);
            var root = document.RootElement;

            // version OPCJONALNA i nullable wprost z definicji: brak to null, nie blad.
            var version = Text(root, "version", SonosPlaylistsLimits.MaxVersionLength);

            var items = new List<SonosPlaylist>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            // playlists tez OPCJONALNE i nullable - domyslne required:false w
            // Array(...) jest tu ZGODNE z definicja, a nie przeoczeniem.
            foreach (var item in Array(root, "playlists", SonosPlaylistsLimits.MaxItems))
            {
                var id = Text(item, "id", SonosPlaylistsLimits.MaxPlaylistIdLength, true)!;
                // Powtorzony identyfikator to sprzeczna odpowiedz: nie da sie
                // wskazac, ktory wiersz jest ktory. Odrzucamy CALOSC. Powtorzona
                // NAZWA jest natomiast legalna - nazwa nie jest kluczem.
                if (!seen.Add(id))
                {
                    throw new JsonException();
                }

                items.Add(new SonosPlaylist(
                    id,
                    Text(item, "name", SonosPlaylistsLimits.MaxPlaylistNameLength, true)!,
                    Text(item, "type", SonosPlaylistsLimits.MaxPlaylistTypeLength),
                    OptionalInt32(item, "trackCount")));
            }

            // householdId wchodzi LITERALNIE z zapytania, nie z odpowiedzi.
            return SonosPlaylistsOutcome.Ok(new SonosPlaylistsList(householdId!, version, items));
        }
        catch (Exception ex) when (IsInvalidData(ex))
        {
            return SonosPlaylistsOutcome.Failure(SonosControlApiStatus.InvalidResponse);
        }
    }
}

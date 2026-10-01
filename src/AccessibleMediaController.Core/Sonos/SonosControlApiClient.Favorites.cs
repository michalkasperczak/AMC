using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AccessibleMediaController.Core.Sonos;

/// <summary>
/// ODCZYT ULUBIONYCH domu (F1a). Jedna operacja, jedno zapytanie GET.
///
/// Ta czesc klienta korzysta DOKLADNIE z tego samego transportu co odczyt domow
/// i grup - prywatne ReadAsync, Parse, Array, Text: staly host HTTPS, brak
/// przekierowan i ciasteczek, naglowki na POJEDYNCZYM zadaniu, deadline
/// obejmujacy naglowki i cialo, ograniczone czytanie (256 KiB), UTF-8 ze
/// sciscie sprawdzanym kodowaniem, odrzucanie duplikatow pol JSON, MaxDepth 16.
/// Nic z tego nie jest tu kopiowane ani liberalizowane.
///
/// Czego tu NIE MA i byc nie moze:
///  * ZADNEGO POST - loadFavorite, zapis i odtwarzanie ulubionego naleza do
///    osobnego etapu,
///  * zadnego cache, pollingu ani subskrypcji zdarzenia favoritesVersionChange,
///  * zadnego odswiezania tokenu ani kasowania konta przy 401 - to warstwa
///    koordynatora, nie transport,
///  * zadnej PARTIAL listy: pierwsza niezgodnosc struktury odrzuca CALA
///    odpowiedz i AMC nie pokazuje polowy ulubionych jako calosci.
/// </summary>
public sealed partial class SonosControlApiClient : ISonosFavoritesApi
{
    /// <summary>
    /// GET /households/{householdId}/favorites - ulubione domu.
    ///
    /// Zwrocona lista niesie LITERALNIE ten householdId, o ktory pytalismy
    /// (bez trim i bez zmiany wielkosci liter), zeby wolajacy mogl odrzucic
    /// wynik, ktory przyszedl po zmianie domu.
    /// </summary>
    public async Task<SonosFavoritesOutcome> GetFavoritesAsync(
        string? accessToken, string? householdId, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return SonosFavoritesOutcome.Failure(SonosControlApiStatus.Canceled);
        }

        if (!SonosHouseholdIdPolicy.TryEncode(householdId, out var encoded))
        {
            return SonosFavoritesOutcome.Failure(SonosControlApiStatus.InvalidConfiguration);
        }

        var uri = new Uri(_configuration.Origin, "households/" + encoded + "/favorites");
        var reply = await ReadAsync(uri, accessToken, cancellationToken).ConfigureAwait(false);
        if (reply.Status != SonosControlApiStatus.Success)
        {
            return SonosFavoritesOutcome.Failure(reply.Status);
        }

        try
        {
            using var document = Parse(reply.Body!);
            var root = document.RootElement;

            // version i items sa WYMAGANE w definicji favoritesList. Array(...)
            // ma domyslnie required:false, wiec brak wymuszenia pomylilby
            // "dom bez ulubionych" z "usluga nic nie powiedziala".
            var version = Text(root, "version", SonosFavoritesLimits.MaxVersionLength, true)!;

            var items = new List<SonosFavorite>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in Array(root, "items", SonosFavoritesLimits.MaxItems, true))
            {
                var id = Text(item, "id", SonosFavoritesLimits.MaxFavoriteIdLength, true)!;
                // Powtorzony identyfikator to sprzeczna odpowiedz: nie da sie
                // wskazac, ktory wiersz jest ktory. Odrzucamy CALOSC.
                if (!seen.Add(id))
                {
                    throw new JsonException();
                }

                items.Add(new SonosFavorite(
                    id,
                    Text(item, "name", SonosFavoritesLimits.MaxFavoriteNameLength, true)!,
                    Text(item, "description", SonosFavoritesLimits.MaxFavoriteDescriptionLength),
                    ReadFavoriteService(item),
                    ReadFavoriteResourceIdentity(item)));
            }

            // householdId wchodzi LITERALNIE z zapytania, nie z odpowiedzi.
            return SonosFavoritesOutcome.Ok(new SonosFavoritesList(householdId!, version, items));
        }
        catch (Exception ex) when (IsInvalidData(ex))
        {
            return SonosFavoritesOutcome.Failure(SonosControlApiStatus.InvalidResponse);
        }
    }

    /// <summary>
    /// service jest OPCJONALNY i nullable, ale jesli jest, musi byc OBIEKTEM -
    /// liczba albo napis w tym miejscu znaczy, ze nie rozumiemy odpowiedzi.
    /// imageUrl (deprecated od 1.21.0) nie jest czytany.
    /// </summary>
    private static SonosFavoriteService? ReadFavoriteService(JsonElement favorite)
    {
        if (!favorite.TryGetProperty("service", out var service)
            || service.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (service.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException();
        }

        return new SonosFavoriteService(
            Text(service, "name", SonosFavoritesLimits.MaxServiceNameLength),
            Text(service, "id", SonosFavoritesLimits.MaxServiceIdLength));
    }

    // Resource identity is optional enrichment. Unsupported shapes or limits
    // disable matching, not the existing catalogue/metadata read. Parse() still
    // rejects duplicate properties and excessive depth; Text() still validates
    // escaped strings. No second JSON parser or broad exception swallowing.
    private static SonosResourceIdentity? ReadFavoriteResourceIdentity(JsonElement favorite)
    {
        return favorite.TryGetProperty("resource", out var resource)
            && resource.ValueKind == JsonValueKind.Object ? ReadResourceIdentity(resource) : null;
    }

    private static SonosResourceIdentity? ReadResourceIdentity(JsonElement parent)
    {
        if (!parent.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.Object)
            return null;
        foreach (var field in new[] { "serviceId", "objectId", "accountId" })
        {
            if (id.TryGetProperty(field, out var value)
                && value.ValueKind is not (JsonValueKind.Null or JsonValueKind.String))
                return null;
        }
        return SonosResourceIdentity.TryCreate(
            Text(id, "serviceId", int.MaxValue),
            Text(id, "objectId", int.MaxValue),
            Text(id, "accountId", int.MaxValue));
    }
}

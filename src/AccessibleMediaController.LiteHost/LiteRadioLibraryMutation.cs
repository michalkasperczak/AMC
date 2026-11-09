using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace AccessibleMediaController.LiteHost;

/// <summary>
/// Jedna stacja przekazana przez okno dodawania albo wspólny importer AMC.
/// Id pozostaje własnością hosta; frontend nie tworzy identyfikatorów profilu.
/// </summary>
internal sealed record LiteRadioStationDraft(string Name, string Url);

/// <summary>
/// Wynik wąskiej zmiany Biblioteki radia. Pola liczbowe służą do krótkiego,
/// użytkowego komunikatu, a identyfikator wyłącznie do przywrócenia fokusu.
/// </summary>
internal sealed record LiteRadioLibraryMutationResult(
    [property: JsonPropertyName("addedCount")] int AddedCount,
    [property: JsonPropertyName("promotedCount")] int PromotedCount,
    [property: JsonPropertyName("updatedCount")] int UpdatedCount,
    [property: JsonPropertyName("skippedCount")] int SkippedCount,
    [property: JsonPropertyName("preferredStationId")] string? PreferredStationId,
    [property: JsonPropertyName("preferredStationName")] string? PreferredStationName,
    [property: JsonIgnore] IReadOnlyList<string> ChangedStationIds,
    [property: JsonIgnore] IReadOnlyList<string> SourceStationIds)
{
    [JsonIgnore]
    public bool Changed => ChangedStationIds.Count > 0;
}

/// <summary>
/// Wspólne reguły dodawania i importu stacji, zgodne z głównym AMC.
/// Klasa zmienia wyłącznie przekazany węzeł JSON; zapis atomowy i kolejność
/// w SQLite pozostają odpowiedzialnością <see cref="LiteProfileMutationStore"/>.
/// </summary>
internal static class LiteRadioLibraryMutation
{
    public static LiteRadioLibraryMutationResult Apply(
        JsonObject root,
        IReadOnlyList<LiteRadioStationDraft> drafts,
        int parserSkipped,
        bool updateExistingTitles)
    {
        if (root["radio"]?["stations"] is not JsonArray stations)
            throw new InvalidDataException("Profil AMC nie zawiera listy stacji.");

        var existingByUrl = new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in stations
                     .OfType<JsonObject>()
                     .Select(station => (Station: station, Url: Text(station, "streamUrl", "StreamUrl")))
                     .Where(entry => entry.Url.Length > 0)
                     .GroupBy(entry => entry.Url, StringComparer.OrdinalIgnoreCase))
        {
            existingByUrl[group.Key] = group
                .Select(entry => entry.Station)
                .FirstOrDefault(station => Boolean(station, "isInLibrary", "IsInLibrary"))
                ?? group.First().Station;
        }

        var added = new List<string>();
        var promoted = new List<string>();
        var updated = new List<string>();
        var changedInSourceOrder = new List<string>();
        var sourceStationIds = new List<string>();
        var sourceNames = new Dictionary<string, string>(StringComparer.Ordinal);
        var skipped = Math.Max(0, parserSkipped);

        foreach (var draft in drafts)
        {
            if (existingByUrl.TryGetValue(draft.Url, out var existing))
            {
                var existingId = Text(existing, "id", "Id");
                if (existingId.Length == 0)
                {
                    skipped++;
                    continue;
                }
                sourceStationIds.Add(existingId);
                // Starszy import M3U bez #EXTINF nazywał stację samym hostem.
                // Ponowny import z prawdziwą nazwą (np. z icy-name) ją uzupełnia;
                // nazw nadanych przez użytkownika nie ruszamy.
                var replacesHostName = !updateExistingTitles
                    && Uri.TryCreate(draft.Url, UriKind.Absolute, out var draftUri)
                    && string.Equals(Text(existing, "name", "Name"), draftUri.Host, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(draft.Name, draftUri.Host, StringComparison.OrdinalIgnoreCase);
                if (replacesHostName)
                {
                    existing["name"] = draft.Name;
                    updated.Add(existingId);
                    changedInSourceOrder.Add(existingId);
                }
                sourceNames[existingId] = updateExistingTitles || replacesHostName
                    ? draft.Name
                    : Text(existing, "name", "Name");

                if (updateExistingTitles)
                {
                    if (!Boolean(existing, "isInLibrary", "IsInLibrary"))
                        promoted.Add(existingId);
                    existing["name"] = draft.Name;
                    existing["streamUrl"] = draft.Url;
                    existing["hasCustomTitle"] = true;
                    existing["isInLibrary"] = true;
                    updated.Add(existingId);
                    changedInSourceOrder.Add(existingId);
                    continue;
                }

                if (Boolean(existing, "isInLibrary", "IsInLibrary"))
                {
                    if (!replacesHostName) skipped++;
                    continue;
                }

                existing["isInLibrary"] = true;
                promoted.Add(existingId);
                changedInSourceOrder.Add(existingId);
                continue;
            }

            var idPrefix = updateExistingTitles ? "radio:custom:" : "radio:imported:";
            var newId = idPrefix + Guid.NewGuid().ToString("N");
            var station = new JsonObject
            {
                ["id"] = newId,
                ["name"] = draft.Name,
                ["streamUrl"] = draft.Url,
                ["hasCustomTitle"] = true,
                ["isFavorite"] = false,
                ["isInLibrary"] = true,
                ["isInQueue"] = false,
                ["isPlayNext"] = false,
                ["isCustom"] = true,
                ["isBitrateEstimated"] = false
            };
            stations.Add(station);
            existingByUrl[draft.Url] = station;
            added.Add(newId);
            changedInSourceOrder.Add(newId);
            sourceStationIds.Add(newId);
            sourceNames[newId] = draft.Name;
        }

        // To jest kolejność playlisty, a nie kolejność typów zmian. Import
        // może przeplatać stacje istniejące i nowe; grupowanie ich na
        // promoted/added odwracało lub tasowało wynik widoczny po imporcie.
        var changedIds = changedInSourceOrder
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var orderedIds = sourceStationIds
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var preferred = orderedIds.FirstOrDefault();
        return new LiteRadioLibraryMutationResult(
            added.Count,
            promoted.Count,
            updated.Count,
            skipped,
            preferred,
            preferred is not null && sourceNames.TryGetValue(preferred, out var name) ? name : null,
            changedIds,
            orderedIds);
    }

    public static void ApplyChangedOrder(
        List<string> customOrder,
        List<string> addedOldestToNewest,
        IReadOnlyList<string> changedInSourceOrder)
    {
        foreach (var id in changedInSourceOrder)
        {
            customOrder.RemoveAll(candidate =>
                string.Equals(candidate, id, StringComparison.Ordinal));
            customOrder.Add(id);
            addedOldestToNewest.RemoveAll(candidate =>
                string.Equals(candidate, id, StringComparison.Ordinal));
        }
        foreach (var id in changedInSourceOrder.Reverse())
            addedOldestToNewest.Add(id);
    }

    private static string Text(JsonObject value, string first, string second) =>
        value[first]?.GetValue<string?>()?.Trim()
        ?? value[second]?.GetValue<string?>()?.Trim()
        ?? string.Empty;

    private static bool Boolean(JsonObject value, string first, string second)
    {
        foreach (var name in new[] { first, second })
        {
            if (value[name] is JsonValue json && json.TryGetValue<bool>(out var result))
                return result;
        }
        return false;
    }
}

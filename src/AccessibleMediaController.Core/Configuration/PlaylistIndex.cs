using AccessibleMediaController.Core.LocalMedia;

namespace AccessibleMediaController.Core.Configuration;

public enum PlaylistMembershipState
{
    None,
    Some,
    All
}

public sealed class PlaylistIndex
{
    private readonly PlaylistSettings settings;

    public PlaylistIndex(PlaylistSettings settings)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        Normalize();
    }

    public IReadOnlyList<PlaylistEntry> GetForSession(string sessionId) =>
        settings.Entries
            .Where(entry => string.Equals(entry.SessionId, sessionId, StringComparison.OrdinalIgnoreCase))
            .ToArray();

    public PlaylistEntry? Find(string playlistId) => settings.Entries.FirstOrDefault(entry =>
        string.Equals(entry.Id, playlistId, StringComparison.Ordinal));

    public PlaylistEntry Create(string sessionId, string name)
    {
        var normalizedSession = NormalizeRequired(sessionId, "Sesja playlisty jest pusta.");
        var normalizedName = NormalizeName(name);
        EnsureUniqueName(normalizedSession, normalizedName);
        var entry = new PlaylistEntry
        {
            SessionId = normalizedSession,
            Name = normalizedName
        };
        settings.Entries.Add(entry);
        return entry;
    }

    public void Rename(string playlistId, string name)
    {
        var entry = FindRequired(playlistId);
        var normalizedName = NormalizeName(name);
        EnsureUniqueName(entry.SessionId, normalizedName, entry.Id);
        entry.Name = normalizedName;
    }

    public bool Remove(string playlistId)
    {
        var entry = Find(playlistId);
        return entry is not null && settings.Entries.Remove(entry);
    }

    public PlaylistMembershipState GetMembership(
        string playlistId,
        IEnumerable<string> itemIds)
    {
        var entry = FindRequired(playlistId);
        var selected = NormalizeItemIds(itemIds);
        if (selected.Count == 0) return PlaylistMembershipState.None;
        var contained = selected.Count(id => entry.ItemIds.Contains(id, StringComparer.Ordinal));
        return contained == 0
            ? PlaylistMembershipState.None
            : contained == selected.Count
                ? PlaylistMembershipState.All
                : PlaylistMembershipState.Some;
    }

    public bool SetMembership(
        string playlistId,
        IEnumerable<string> itemIds,
        bool include)
    {
        var entry = FindRequired(playlistId);
        var selected = NormalizeItemIds(itemIds);
        var changed = false;
        if (include)
        {
            foreach (var itemId in selected)
            {
                if (entry.ItemIds.Contains(itemId, StringComparer.Ordinal)) continue;
                entry.ItemIds.Add(itemId);
                changed = true;
            }
            return changed;
        }

        var selectedSet = selected.ToHashSet(StringComparer.Ordinal);
        changed = entry.ItemIds.RemoveAll(selectedSet.Contains) > 0;
        return changed;
    }

    public ManualOrderMoveResult MoveItems(
        string playlistId,
        IReadOnlyList<string> visibleItemIds,
        IReadOnlyList<string> selectedItemIds,
        int direction)
    {
        var entry = FindRequired(playlistId);
        return LocalLibraryManualOrder.MoveVisibleBlock(
            entry.ItemIds,
            visibleItemIds,
            selectedItemIds,
            direction);
    }

    public void ReplaceSession(string sessionId, IEnumerable<PlaylistEntry> replacements)
    {
        var normalizedSession = NormalizeRequired(sessionId, "Sesja playlisty jest pusta.");
        var firstIndex = settings.Entries.FindIndex(entry =>
            string.Equals(entry.SessionId, normalizedSession, StringComparison.OrdinalIgnoreCase));
        settings.Entries.RemoveAll(entry =>
            string.Equals(entry.SessionId, normalizedSession, StringComparison.OrdinalIgnoreCase));
        var cloned = replacements.Select(CloneEntry).ToList();
        foreach (var entry in cloned) entry.SessionId = normalizedSession;
        settings.Entries.InsertRange(firstIndex < 0 ? settings.Entries.Count : firstIndex, cloned);
        Normalize();
    }

    public PlaylistSettings CloneSettings() => new()
    {
        Entries = settings.Entries.Select(CloneEntry).ToList()
    };

    public void Normalize()
    {
        settings.Entries ??= [];
        var normalized = new List<PlaylistEntry>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var names = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
        foreach (var source in settings.Entries.Where(entry => entry is not null))
        {
            var sessionId = source.SessionId?.Trim() ?? string.Empty;
            if (sessionId.Length == 0) continue;
            var id = source.Id?.Trim() ?? string.Empty;
            if (id.Length == 0 || !ids.Add(id))
            {
                do id = Guid.NewGuid().ToString("N"); while (!ids.Add(id));
            }
            var baseName = string.IsNullOrWhiteSpace(source.Name) ? "Playlista" : source.Name.Trim();
            if (baseName.Length > 200) baseName = baseName[..200];
            var name = MakeUniqueName(sessionId, baseName, names);
            names.Add(NameKey(sessionId, name));
            normalized.Add(new PlaylistEntry
            {
                Id = id,
                SessionId = sessionId,
                Name = name,
                CreatedUtcTicks = source.CreatedUtcTicks > 0 ? source.CreatedUtcTicks : DateTime.UtcNow.Ticks,
                ItemIds = NormalizeItemIds(source.ItemIds)
            });
        }
        settings.Entries = normalized;
    }

    public static PlaylistEntry CloneEntry(PlaylistEntry entry) => new()
    {
        Id = entry.Id,
        SessionId = entry.SessionId,
        Name = entry.Name,
        CreatedUtcTicks = entry.CreatedUtcTicks,
        ItemIds = [.. entry.ItemIds]
    };

    private PlaylistEntry FindRequired(string playlistId) => Find(playlistId)
        ?? throw new InvalidOperationException("Nie znaleziono playlisty.");

    private static List<string> NormalizeItemIds(IEnumerable<string>? itemIds) =>
        (itemIds ?? [])
            .Where(itemId => !string.IsNullOrWhiteSpace(itemId))
            .Select(itemId => itemId.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

    private string MakeUniqueName(string sessionId, string baseName, ISet<string>? knownNames = null)
    {
        knownNames ??= settings.Entries
            .Select(entry => NameKey(entry.SessionId, entry.Name))
            .ToHashSet(StringComparer.CurrentCultureIgnoreCase);
        if (!knownNames.Contains(NameKey(sessionId, baseName))) return baseName;
        for (var suffix = 2; ; suffix++)
        {
            var marker = $" ({suffix})";
            var stemLength = Math.Min(baseName.Length, 200 - marker.Length);
            var candidate = baseName[..stemLength] + marker;
            if (!knownNames.Contains(NameKey(sessionId, candidate))) return candidate;
        }
    }

    private void EnsureUniqueName(string sessionId, string name, string? ignoredId = null)
    {
        if (settings.Entries.Any(entry =>
                !string.Equals(entry.Id, ignoredId, StringComparison.Ordinal)
                && string.Equals(entry.SessionId, sessionId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(entry.Name, name, StringComparison.CurrentCultureIgnoreCase)))
        {
            throw new InvalidOperationException($"Playlista „{name}” już istnieje w tej sesji.");
        }
    }

    private static string NormalizeName(string name)
    {
        var normalized = NormalizeRequired(name, "Nazwa playlisty jest pusta.");
        if (normalized.Length > 200) throw new InvalidOperationException("Nazwa playlisty może mieć najwyżej 200 znaków.");
        return normalized;
    }

    private static string NormalizeRequired(string value, string error)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0) throw new InvalidOperationException(error);
        return normalized;
    }

    private static string NameKey(string sessionId, string name) => $"{sessionId}\0{name}";
}

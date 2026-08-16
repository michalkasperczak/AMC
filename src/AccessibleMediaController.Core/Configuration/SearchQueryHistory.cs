namespace AccessibleMediaController.Core.Configuration;

public sealed class SearchQueryHistory
{
    public const int MaxEntriesPerScope = 20;
    public const string GlobalScope = "global";

    private readonly SearchHistorySettings _settings;

    public SearchQueryHistory(SearchHistorySettings settings)
    {
        _settings = settings;
        Normalize();
    }

    public IReadOnlyList<string> GetEntries(string scope)
    {
        var key = FindScopeKey(NormalizeScope(scope));
        return key is null ? [] : _settings.Entries[key].ToArray();
    }

    public bool Record(string scope, string query)
    {
        var normalizedScope = NormalizeScope(scope);
        var normalizedQuery = query.Trim();
        if (normalizedScope.Length == 0 || normalizedQuery.Length == 0) return false;

        var key = FindScopeKey(normalizedScope);
        if (key is null)
        {
            key = normalizedScope;
            _settings.Entries[key] = [];
        }

        var entries = _settings.Entries[key];
        var existingIndex = entries.FindIndex(value =>
            string.Equals(value, normalizedQuery, StringComparison.OrdinalIgnoreCase));
        if (existingIndex == 0
            && string.Equals(entries[0], normalizedQuery, StringComparison.Ordinal))
        {
            return false;
        }

        if (existingIndex >= 0) entries.RemoveAt(existingIndex);
        entries.Insert(0, normalizedQuery);
        if (entries.Count > MaxEntriesPerScope)
        {
            entries.RemoveRange(MaxEntriesPerScope, entries.Count - MaxEntriesPerScope);
        }

        return true;
    }

    public void Normalize()
    {
        _settings.Entries ??= new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var normalized = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in _settings.Entries)
        {
            var scope = NormalizeScope(pair.Key);
            if (scope.Length == 0 || pair.Value is null) continue;

            if (!normalized.TryGetValue(scope, out var entries))
            {
                entries = [];
                normalized[scope] = entries;
            }

            foreach (var value in pair.Value)
            {
                if (entries.Count >= MaxEntriesPerScope) break;
                var query = value?.Trim();
                if (string.IsNullOrWhiteSpace(query)
                    || entries.Contains(query, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                entries.Add(query);
            }
        }

        _settings.Entries = normalized;
    }

    private string? FindScopeKey(string scope) =>
        _settings.Entries.Keys.FirstOrDefault(key =>
            string.Equals(key, scope, StringComparison.OrdinalIgnoreCase));

    private static string NormalizeScope(string scope) => scope.Trim();
}

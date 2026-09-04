namespace AccessibleMediaController.Core.Podcasts;

public static class PodcastEpisodePaging
{
    public const int DefaultPageSize = 150;

    public static int ResolveLoadedCount(
        int totalCount,
        int rememberedCount,
        int preferredItemIndex = -1,
        int pageSize = DefaultPageSize)
    {
        if (totalCount <= 0) return 0;
        if (pageSize <= 0) throw new ArgumentOutOfRangeException(nameof(pageSize));

        var requiredCount = preferredItemIndex >= 0
            ? Math.Min(totalCount, preferredItemIndex + 1)
            : 1;
        var requestedCount = Math.Max(Math.Max(rememberedCount, pageSize), requiredCount);
        var pageCount = (requestedCount + pageSize - 1) / pageSize;
        return Math.Min(totalCount, checked(pageCount * pageSize));
    }

    public static int ResolveNextLoadedCount(
        int totalCount,
        int currentCount,
        int pageSize = DefaultPageSize)
    {
        if (totalCount <= 0) return 0;
        if (pageSize <= 0) throw new ArgumentOutOfRangeException(nameof(pageSize));
        return Math.Min(totalCount, checked(Math.Max(0, currentCount) + pageSize));
    }
}

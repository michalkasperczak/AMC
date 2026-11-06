using AccessibleMediaController.Core.Configuration;

namespace AccessibleMediaController.Core.YouTube;

public sealed record YouTubeSubscriptionSyncApplyResult(
    int Added,
    int Restored,
    int AlreadyPresent,
    int Suppressed,
    int Duplicates,
    int Invalid)
{
    public int Changed => Added + Restored;
}

/// <summary>
/// Stosuje zatwierdzoną synchronizację konta bez pobierania całych kanałów.
/// Nowe źródła są zapisywane jako puste rekordy oczekujące na istniejący,
/// porcjowany mechanizm odświeżania Podcastów i YouTube.
/// </summary>
public static class YouTubeSubscriptionSyncApplier
{
    private const string AccountChannelIdPrefix = "youtube-account-channel:";

    public static YouTubeSubscriptionSyncApplyResult Apply(
        IEnumerable<YouTubeSubscription> remoteSubscriptions,
        PodcastSettings localPodcasts,
        bool restoreSuppressed = false,
        DateTime? refreshUtc = null)
    {
        ArgumentNullException.ThrowIfNull(remoteSubscriptions);
        ArgumentNullException.ThrowIfNull(localPodcasts);
        var remote = remoteSubscriptions.ToArray();
        var refreshedAt = (refreshUtc ?? DateTime.UtcNow).ToUniversalTime().Ticks;
        var plan = YouTubeSubscriptionSyncPlanner.Create(
            remote,
            localPodcasts,
            restoreSuppressed);

        // Sprawdź cały plan przed pierwszą zmianą. Uszkodzony lub ręcznie
        // zmieniony profil nie może pozostawić połowy zaimportowanych kanałów.
        foreach (var action in plan.Actions)
        {
            if (action.Kind == YouTubeSubscriptionSyncActionKind.Add)
            {
                var newId = AccountChannelIdPrefix + action.ChannelId;
                if (localPodcasts.Subscriptions.Any(candidate =>
                        string.Equals(candidate.Id, newId, StringComparison.Ordinal)))
                {
                    throw new InvalidDataException(
                        "Biblioteka zawiera sprzeczny rekord kanału YouTube. Nie zapisano synchronizacji.");
                }
                continue;
            }

            if (string.IsNullOrWhiteSpace(action.ExistingSubscriptionId)
                || localPodcasts.Subscriptions.All(candidate =>
                    !string.Equals(
                        candidate.Id,
                        action.ExistingSubscriptionId,
                        StringComparison.Ordinal)))
            {
                throw new InvalidDataException(
                    "Biblioteka zmieniła się przed przywróceniem kanału YouTube. Nie zapisano synchronizacji.");
            }
        }

        var added = 0;
        var restored = 0;
        foreach (var action in plan.Actions)
        {
            if (action.Kind == YouTubeSubscriptionSyncActionKind.Restore)
            {
                var existing = localPodcasts.Subscriptions.First(candidate =>
                    string.Equals(
                        candidate.Id,
                        action.ExistingSubscriptionId,
                        StringComparison.Ordinal));
                existing.IsInLibrary = true;
                var cameFromAccount = existing.Id.StartsWith(
                    AccountChannelIdPrefix,
                    StringComparison.Ordinal);
                if (cameFromAccount
                    && !existing.HasCustomTitle
                    && !string.IsNullOrWhiteSpace(action.Title))
                    existing.Title = action.Title;
                if (cameFromAccount)
                {
                    existing.IsYouTubeAccountData = true;
                    existing.YouTubeAccountDataRefreshedUtcTicks = refreshedAt;
                }
                restored++;
                continue;
            }

            localPodcasts.Subscriptions.Add(new PodcastSubscriptionSettings
            {
                Id = AccountChannelIdPrefix + action.ChannelId,
                Title = action.Title,
                Author = action.Title,
                Description = "Kanał dodany z subskrypcji konta YouTube; materiały pojawią się po odświeżeniu.",
                FeedUrl = action.ChannelUri.AbsoluteUri,
                SourceKind = PodcastSourceKind.YouTubeChannel,
                HomepageUrl = $"https://www.youtube.com/channel/{Uri.EscapeDataString(action.ChannelId)}",
                LastRefreshUtcTicks = 0,
                RefreshIntervalMinutes = localPodcasts.InboxSelectionCustomized
                    ? 0 : localPodcasts.YouTubeRefreshIntervalMinutes,
                IsInLibrary = true,
                IsYouTubeAccountData = true,
                YouTubeAccountDataRefreshedUtcTicks = refreshedAt
            });
            added++;
        }

        // Pozycja już obecna nie trafia do planu zmian. Jeśli utworzyła ją
        // wcześniejsza synchronizacja konta, samo jawne ponowne sprawdzenie
        // również odnawia trzydziestodniowy termin danych API.
        var remoteIds = remote.Select(item => item.ChannelId).ToHashSet(StringComparer.Ordinal);
        foreach (var existing in localPodcasts.Subscriptions.Where(item =>
                     item.IsYouTubeAccountData
                     && item.Id.StartsWith(AccountChannelIdPrefix, StringComparison.Ordinal)
                     && remoteIds.Contains(item.Id[AccountChannelIdPrefix.Length..])))
        {
            existing.YouTubeAccountDataRefreshedUtcTicks = refreshedAt;
        }

        return new YouTubeSubscriptionSyncApplyResult(
            added,
            restored,
            plan.AlreadyPresent,
            plan.Suppressed,
            plan.Duplicates,
            plan.Invalid);
    }
}

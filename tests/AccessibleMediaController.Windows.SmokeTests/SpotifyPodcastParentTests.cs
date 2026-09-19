using System.Reflection;
using AccessibleMediaController.Core.Commands;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Windows;

internal static class SpotifyPodcastParentTests
{
    internal static void Run()
    {
        var help = AccessibleMediaController.Core.Presentation.ShortcutHelpCatalog.Create(
            AccessibleMediaController.Core.Input.KeyboardProfile.CreateDefault(), new AppSettings())
            .SelectMany(section => section.Entries).SingleOrDefault(entry => entry.CommandId == CommandIds.ViewSpotifyPodcasts);
        Check(help?.Shortcut == "Ctrl+Alt+O" && help.Context.Contains("Spotify", StringComparison.Ordinal),
            "Pomoc nie podaje skrotu widoku podcastow Spotify.");
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;
        var episode = new MediaItem
        {
            Id = "probe-episode", Kind = MediaItemKind.Episode, ExternalId = "ep1",
            Source = "spotify:episode:ep1", Title = "Odcinek probny", IsFavorite = true,
            RelatedAlbumExternalId = "show1", RelatedAlbumTitle = "Podcast probny"
        };
        var create = typeof(MainWindow).GetMethod("CreateRelatedSpotifyContainer", flags)!;
        foreach (var kind in new[] { MediaItemKind.Album, MediaItemKind.Podcast })
        {
            var parent = create.Invoke(null, new object[] { episode, kind }) as MediaItem;
            Check(parent?.Kind == MediaItemKind.Podcast, "Rodzic odcinka Spotify musi byc podcastem, nie albumem.");
            Check(parent!.ExternalId == "show1" && parent.Title == "Podcast probny" && parent.Source == "spotify:show:show1",
                "Powrot z odcinka nie prowadzi do adresu jego podcastu.");
        }
        Check((bool)typeof(MainWindow).GetMethod("HasSpotifyRelations", flags)!.Invoke(null, new object[] { episode })!,
            "Strzalka w prawo pomija podcast nadrzedny odcinka.");
        episode.RelatedAlbumExternalId = null;
        Check(create.Invoke(null, new object[] { episode, MediaItemKind.Podcast }) is null,
            "Odcinek bez powiazania dostal wymyslony podcast.");
        episode.RelatedAlbumExternalId = "show/id?x=1";
        var encodedParent = (MediaItem)create.Invoke(null, new object[] { episode, MediaItemKind.Podcast })!;
        Check(encodedParent.PublicUri == "https://open.spotify.com/show/show%2Fid%3Fx%3D1",
            "Identyfikator podcastu zmienia strukture publicznego adresu zamiast zostac zakodowany.");
        episode.RelatedAlbumExternalId = "show1";
        var runInWindow = typeof(SpotifyStartupEngineTests).GetMethod("WOknie", flags)!;
        Action<PersistedState> prepare = state =>
        {
            state.Settings.LastSessionId = "spotify";
            state.Spotify.CachedCollectionItems.Add(TidalCachedCollectionItemSettings.FromMediaItem(episode));
        };
        Action<MainWindow, PersistedState> verify = (window, state) =>
        {
            const BindingFlags instance = BindingFlags.Instance | BindingFlags.NonPublic;
            var manager = (SessionManager)typeof(MainWindow).GetField("_sessions", instance)!.GetValue(window)!;
            manager.SelectSession("spotify");
            var view = (string)typeof(MainWindow).GetMethod("StoreSpotifyPodcastsForSession", instance)!
                .Invoke(window, new object[] { "spotify", new[] { episode } })!;
            window.ShowCurrentSession(view);
            Check(window.ActionItem?.ExternalId == "ep1", "Proba nie zaznaczyla odcinka w prawdziwym widoku podcastow.");
            object?[] keys = { System.Windows.Input.Key.O, System.Windows.Input.ModifierKeys.Control | System.Windows.Input.ModifierKeys.Alt, null };
            Check((bool)typeof(MainWindow).GetMethod("TryResolveKeyboardHelpCommand", instance)!.Invoke(window, keys)!
                && keys[2]?.ToString() == CommandIds.ViewSpotifyPodcasts,
                "Pomoc klawiatury nie rozpoznaje Ctrl+Alt+O w Spotify.");
            Check((bool)typeof(MainWindow).GetMethod("CommandVisibleInPalette", instance)!.Invoke(window, new object[] { CommandIds.GoToPodcast })!,
                "Polecenie Przejdz do podcastu nadal ograniczone do RSS.");
            Check(!(bool)typeof(MainWindow).GetMethod("CommandVisibleInPalette", instance)!.Invoke(window, new object[] { CommandIds.GoToAlbum })!,
                "Odcinek pokazuje mylace polecenie Przejdz do albumu.");
            VerifySearchParentMenu(manager);
        };
        runInWindow.Invoke(null, new object[] { prepare, verify });
        Console.WriteLine("OK: odcinek Spotify wraca do podcastu, ma poprawny URI i polecenie poza RSS");
    }

    private static void VerifySearchParentMenu(SessionManager sessions)
    {
        const BindingFlags instance = BindingFlags.NonPublic | BindingFlags.Instance;
        var dialog = new SearchWindow(sessions, false, item => item.Title, item => item.Title, _ => string.Empty,
            (_, _, _, _) => null, new SearchQueryHistory(new SearchHistorySettings()), "spotify", () => { }, false);
        try
        {
            var searchBox = (System.Windows.Controls.TextBox)typeof(SearchWindow).GetField("SearchBox", instance)!.GetValue(dialog)!;
            searchBox.Text = "Odcinek probny";
            typeof(SearchWindow).GetMethod("RunSearch", instance)!.Invoke(dialog, null);
            var results = (System.Windows.Controls.ListBox)typeof(SearchWindow).GetField("ResultsList", instance)!.GetValue(dialog)!;
            Check(results.Items.Count == 1 && results.SelectedIndex == 0, "Brak rzeczywistego wyniku wyszukiwania odcinka w probie.");
            typeof(SearchWindow).GetMethod("ResultsContextMenu_Opened", instance)!
                .Invoke(dialog, new object[] { results.ContextMenu, new System.Windows.RoutedEventArgs() });
            var parent = (System.Windows.Controls.MenuItem)typeof(SearchWindow).GetField("SearchGoToPodcastMenuItem", instance)!.GetValue(dialog)!;
            Check(parent.Visibility == System.Windows.Visibility.Visible,
                "Menu wyniku wyszukiwania Spotify nie pozwala przejsc od odcinka do podcastu.");
        }
        finally { dialog.Close(); }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

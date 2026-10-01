using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using AccessibleMediaController.Core.Sonos;
using AccessibleMediaController.Windows;

/// <summary>
/// APARATURA pomiaru Biblioteki Sonosa. Wszystko PO STRONIE TESTU: zero nowych
/// publicznych fabryk w produkcji. Okna sterujemy PRAWDZIWYMI zdarzeniami WPF -
/// zywym urzadzeniem klawiatury i tunelujacym PreviewKeyDown, czyli dokladnie
/// tym, co obsluguje produkcja.
///
/// SWIADOMA kopia dyscypliny odebranej aparatury ulubionych
/// (<c>SonosFavoritePlayDriving</c>), a nie nowy, ogolny framework.
/// </summary>
internal static class LibraryFixture
{
    internal static LibraryUi ShowLibrary(
        Action<SonosLibraryPresentation.SonosLibraryCategoryRow>? open)
    {
        var window = new SonosLibraryWindow(open);
        return new LibraryUi(window);
    }

    internal static PlaylistsUi ShowPlaylists(
        PlaylistLoadFake backend,
        (string Id, string Name)[] playlists,
        string? groupName)
    {
        var items = playlists
            .Select(item => new SonosPlaylist(item.Id, item.Name))
            .ToArray();

        SonosPlaylistsWindow window = null!;

        // WLASCICIEL zlecenia: odpowiada DOKLADNIE temu oknu, ktore zlecilo, i
        // przekazuje IDENTYFIKATOR z wiersza - nigdy tytul.
        window = new SonosPlaylistsWindow(
            items,
            groupName,
            async request =>
            {
                var result = await backend.LoadPlaylistAsync(
                    groupName is null ? null : "GRUPA-" + groupName.ToUpperInvariant(),
                    request.Playlist.Id,
                    SonosFavoriteQueueAction.Insert,
                    playOnCompletion: true,
                    request.Lifetime).ConfigureAwait(true);

                // SPOZNIONA ODPOWIEDZ ma DOKLADNIE JEDEN adres: okno, ktore ja
                // zlecilo, i tylko jesli nadal zyje.
                if (!ReferenceEquals(request.Origin, window)) return;
                if (!window.IsLiveOwnerTarget) return;

                // UCZCIWIE: przyjecie zlecenia, nie potwierdzenie odtwarzania.
                window.AnnounceForOwner(result.Accepted
                    ? "Zlecono odtworzenie playlisty. AMC nie potwierdza, że już gra."
                    : "Nie udało się zlecić odtworzenia playlisty.");
            });

        return new PlaylistsUi(window);
    }

    internal static TargetUi ShowTarget(SonosHouseholdTopology topology, string? currentGroupId) =>
        new(new SonosTargetSelectionWindow(topology, currentGroupId));

    internal static TargetUi ShowTargetUnavailable(string reason) =>
        new(new SonosTargetSelectionWindow(reason));

    internal static void PumpOnce()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(
            DispatcherPriority.Background, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    /// <summary>
    /// PRAWDZIWY klawisz: zywe urzadzenie klawiatury, zrodlo prezentacji TEGO
    /// okna i tunelujacy PreviewKeyDown. Zadnego wolania metody prywatnej.
    /// </summary>
    internal static void RaiseKey(Window window, ListBox list, Key key)
    {
        if (!list.IsKeyboardFocusWithin)
        {
            list.Focus();
            Keyboard.Focus(list);
            Pump(TimeSpan.FromMilliseconds(40));
        }

        var target = Keyboard.FocusedElement as UIElement ?? list;
        var source = PresentationSource.FromVisual(window)
            ?? throw new Exception("Okno nie ma powierzchni prezentacji.");
        target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, key)
        {
            RoutedEvent = Keyboard.PreviewKeyDownEvent
        });
        Pump(TimeSpan.FromMilliseconds(40));
    }

    internal static void Pump(TimeSpan duration)
    {
        var deadline = DateTime.UtcNow + duration;
        while (DateTime.UtcNow < deadline) PumpOnce();
    }

    /// <summary>OKNO BIBLIOTEKI pokazane PRAWDZIWIE, sprzatane deterministycznie.</summary>
    internal sealed class LibraryUi : IDisposable
    {
        internal LibraryUi(SonosLibraryWindow window)
        {
            Window = window;
            // Show, nie ShowDialog: ShowDialog zablokowalby watek pomiaru. Kod
            // okna nie polega na modalnosci - Enter i Escape obsluguje samo.
            window.Show();
            Pump(TimeSpan.FromMilliseconds(120));
        }

        internal SonosLibraryWindow Window { get; }

        internal void Pump(TimeSpan duration) => LibraryFixture.Pump(duration);

        public void Dispose()
        {
            Window.Close();
            LibraryFixture.Pump(TimeSpan.FromMilliseconds(60));
        }
    }

    internal sealed class PlaylistsUi : IDisposable
    {
        internal PlaylistsUi(SonosPlaylistsWindow window)
        {
            Window = window;
            window.Show();
            Pump(TimeSpan.FromMilliseconds(120));
        }

        internal SonosPlaylistsWindow Window { get; }

        internal void Pump(TimeSpan duration) => LibraryFixture.Pump(duration);

        /// <summary>CZEKANIE na DOKLADNIE to zadanie proby, nie na milisekundy.</summary>
        internal void AwaitPlay()
        {
            if (Window.LastPlayTaskForTests is not { } task) return;
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);
            while (!task.IsCompleted)
            {
                if (DateTime.UtcNow > deadline)
                {
                    throw new Exception("Limit czasu: zadanie uruchomienia playlisty.");
                }

                PumpOnce();
            }

            task.GetAwaiter().GetResult();
            LibraryFixture.Pump(TimeSpan.FromMilliseconds(60));
        }

        public void Dispose()
        {
            Window.Close();
            LibraryFixture.Pump(TimeSpan.FromMilliseconds(60));
        }
    }

    internal sealed class TargetUi : IDisposable
    {
        internal TargetUi(SonosTargetSelectionWindow window)
        {
            Window = window;
            window.Show();
            Pump(TimeSpan.FromMilliseconds(120));
        }

        internal SonosTargetSelectionWindow Window { get; }

        internal void Pump(TimeSpan duration) => LibraryFixture.Pump(duration);

        public void Dispose()
        {
            Window.Close();
            LibraryFixture.Pump(TimeSpan.FromMilliseconds(60));
        }
    }
}

/// <summary>DROGA KLAWIATURY dla okien Biblioteki - prawdziwe zdarzenia WPF.</summary>
internal static class LibraryDriving
{
    internal static void PressEnter(this SonosLibraryWindow window) =>
        LibraryFixture.RaiseKey(window, window.ListForTests, Key.Enter);

    internal static void PressEscape(this SonosLibraryWindow window) =>
        LibraryFixture.RaiseKey(window, window.ListForTests, Key.Escape);

    internal static void PressEnter(this SonosPlaylistsWindow window) =>
        LibraryFixture.RaiseKey(window, window.ListForTests, Key.Enter);

    internal static void PressTab(this SonosPlaylistsWindow window) =>
        LibraryFixture.RaiseKey(window, window.ListForTests, Key.Tab);

    internal static void ClickPlay(this SonosPlaylistsWindow window)
    {
        var play = (Button)window.FindName("PlayButton")!;
        play.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        LibraryFixture.Pump(TimeSpan.FromMilliseconds(40));
    }

    internal static void PressEnter(this SonosTargetSelectionWindow window) =>
        LibraryFixture.RaiseKey(window, window.ListForTests, Key.Enter);

    internal static void PressEscape(this SonosTargetSelectionWindow window) =>
        LibraryFixture.RaiseKey(window, window.ListForTests, Key.Escape);
}

/// <summary>
/// SYNTETYCZNE zaplecze uruchamiania playlist. Zero HTTP, zero tokenu, zero
/// konta, zero audio: liczniki mowia, ile razy wywolano TE ATRAPE, a nie ile
/// zadan poszlo w siec. Kazde zlecenie jest ZAPISANE tak, jak przyszlo, i moze
/// byc WSTRZYMANE - stad pomiar spoznionej odpowiedzi.
/// </summary>
internal sealed class PlaylistLoadFake : ISonosPlaylistLoadSessionBackend
{
    internal sealed record LoadPost(
        string? GroupId, string? PlaylistId, SonosFavoriteQueueAction Action, bool PlayOnCompletion);

    internal List<LoadPost> Loads { get; } = [];

    internal List<SonosGroupCommand> Commands { get; } = [];

    /// <summary>WSTRZYMANIE odpowiedzi: serce pomiaru spoznionego wyniku.</summary>
    internal Task? LoadGate { get; set; }

    public async Task<SonosGroupCommandResult> LoadPlaylistAsync(
        string? groupId,
        string? playlistId,
        SonosFavoriteQueueAction action,
        bool playOnCompletion,
        CancellationToken cancellationToken)
    {
        Loads.Add(new LoadPost(groupId, playlistId, action, playOnCompletion));
        if (LoadGate is { } gate) await gate.WaitAsync(cancellationToken).ConfigureAwait(true);

        // PRZYJECIE zlecenia ladowania playlisty - nie dowod, ze gra.
        return SonosGroupCommandResult.CreateAcceptedForMeasurement(SonosGroupCommand.LoadFavorite);
    }
}

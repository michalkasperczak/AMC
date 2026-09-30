using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using AccessibleMediaController.Core.Sonos;
using AccessibleMediaController.Windows;

/// <summary>
/// F3c TRIAGE: APARATURA etapu B. Wszystko po STRONIE TESTU - zero nowych
/// publicznych fabryk i zero zmian w produkcji. Okno ulubionych sterujemy
/// PRAWDZIWYMI zdarzeniami WPF, a jego wlasne hooki pomiarowe (juz istniejace
/// w szkicu) czytamy REFLEKSJA tylko tam, gdzie test ich potrzebuje.
/// </summary>
internal static class SonosFavoritePlayDriving
{
    /// <summary>ZAZNACZENIE wiersza wraz z fokusem kontenera - jak u uzytkownika.</summary>
    internal static void SelectForTests(this SonosFavoritesWindow window, int index)
    {
        var list = (ListBox)window.FindName("FavoritesList")!;
        list.SelectedIndex = index;
        list.UpdateLayout();
        if (list.ItemContainerGenerator.ContainerFromIndex(index) is ListBoxItem item)
        {
            item.Focus();
            Keyboard.Focus(item);
        }

        window.PumpForTests(TimeSpan.FromMilliseconds(60));
    }

    /// <summary>
    /// PRAWDZIWY Enter: zywe urzadzenie klawiatury, zrodlo prezentacji TEGO okna
    /// i tunelujacy <c>PreviewKeyDown</c>, czyli dokladnie to zdarzenie, ktore
    /// obsluguje produkcja. Zadnego wolania metody prywatnej.
    /// </summary>
    internal static void PressEnterForTests(this SonosFavoritesWindow window) => window.PressKey(Key.Enter);

    /// <summary>PRAWDZIWY Escape: ta sama droga zdarzen co Enter.</summary>
    internal static void PressEscapeForTests(this SonosFavoritesWindow window) => window.PressKey(Key.Escape);

    private static void PressKey(this SonosFavoritesWindow window, Key key)
    {
        var list = (ListBox)window.FindName("FavoritesList")!;
        if (!list.IsKeyboardFocusWithin)
        {
            list.Focus();
            Keyboard.Focus(list);
            window.PumpForTests(TimeSpan.FromMilliseconds(40));
        }

        var target = Keyboard.FocusedElement as UIElement ?? list;
        var source = PresentationSource.FromVisual(window)
            ?? throw new Exception("Okno ulubionych nie ma powierzchni prezentacji.");
        target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, key)
        {
            RoutedEvent = Keyboard.PreviewKeyDownEvent
        });
        window.PumpForTests(TimeSpan.FromMilliseconds(40));
    }

    internal static void ClickPlayForTests(this SonosFavoritesWindow window)
    {
        var play = (Button)window.FindName("PlayButton")!;
        play.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        window.PumpForTests(TimeSpan.FromMilliseconds(40));
    }

    /// <summary>CZEKANIE na DOKLADNIE to zadanie proby, nie na milisekundy.</summary>
    internal static void AwaitPlayForTests(this SonosFavoritesWindow window)
    {
        if (window.LastPlayTaskForTests is not { } task) return;
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);
        while (!task.IsCompleted)
        {
            if (DateTime.UtcNow > deadline) throw new Exception("Limit czasu: zadanie uruchomienia ulubionego.");
            PumpOnce();
        }

        task.GetAwaiter().GetResult();
        window.PumpForTests(TimeSpan.FromMilliseconds(60));
    }

    internal static void PumpForTests(this SonosFavoritesWindow window, TimeSpan duration)
    {
        _ = window;
        var deadline = DateTime.UtcNow + duration;
        while (DateTime.UtcNow < deadline) PumpOnce();
    }

    private static void PumpOnce()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(
            DispatcherPriority.Background, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }
}

/// <summary>
/// SYNTETYCZNE zaplecze z granica URUCHAMIANIA. Zero HTTP, zero tokenu, zero
/// konta, zero audio. Kazdy POST <c>loadFavorite</c> jest ZAPISANY dokladnie tak,
/// jak przyszedl, i moze byc WSTRZYMANY - stad pomiar spoznionego wyniku.
/// </summary>
internal sealed class LoadFakeBackend : ISonosGroupSessionBackend, ISonosFavoritesSessionBackend,
    ISonosFavoriteLoadSessionBackend
{
    private (string Id, string Name)[] _favorites = [("ULU-1", "Nokturny")];

    private readonly SonosPlaybackActions _actions = new(
        canPlay: true, canSkip: true, canSkipBack: true, canSkipToPrevious: true,
        canSeek: true, canPause: true, canStop: null, canRepeat: null, canRepeatOne: null,
        canCrossfade: null, canShuffle: null);

    internal sealed record LoadPost(
        string? GroupId, string? FavoriteId, SonosFavoriteQueueAction Action, bool PlayOnCompletion);

    internal List<LoadPost> Loads { get; } = [];

    internal List<SonosGroupCommand> Commands { get; } = [];

    internal int FavoriteReadsForTests { get; private set; }

    /// <summary>WSTRZYMANIE odpowiedzi POST: serce pomiaru spoznionego wyniku.</summary>
    internal Task? LoadGate { get; set; }

    internal int OutstandingLoads { get; private set; }

    internal void SetFavorites(params (string Id, string Name)[] favorites) => _favorites = favorites;

    public Task<SonosGroupCommandResult> LoadFavoriteAsync(
        string? groupId,
        string? favoriteId,
        SonosFavoriteQueueAction action,
        bool playOnCompletion,
        CancellationToken cancellationToken)
    {
        Loads.Add(new LoadPost(groupId, favoriteId, action, playOnCompletion));
        return LoadCoreAsync(cancellationToken);
    }

    private async Task<SonosGroupCommandResult> LoadCoreAsync(CancellationToken cancellationToken)
    {
        OutstandingLoads++;
        try
        {
            if (LoadGate is { } gate) await gate.WaitAsync(cancellationToken).ConfigureAwait(true);
            return SonosGroupCommandResult.CreateAcceptedForMeasurement(SonosGroupCommand.Play);
        }
        finally
        {
            OutstandingLoads--;
        }
    }

    public Task<SonosFavoritesReadResult> ReadFavoritesAsync(
        string? householdId, CancellationToken cancellationToken)
    {
        FavoriteReadsForTests++;
        var list = new SonosFavoritesList(
            householdId ?? "DOM-1",
            "W1",
            _favorites.Select(item => new SonosFavorite(item.Id, item.Name, null, null)).ToArray());
        return Task.FromResult(SonosFavoritesReadResult.CreateForMeasurement(SonosDeviceReadStatus.Success, list));
    }

    public Task<SonosGroupReadResult<SonosGroupPlaybackStatus>> ReadGroupPlaybackAsync(
        string? groupId, CancellationToken cancellationToken) =>
        Task.FromResult(SonosGroupReadResult<SonosGroupPlaybackStatus>.Success(new SonosGroupPlaybackStatus(
            SonosPlaybackState.Playing, null, null, "UTWOR-1", 12_000, null, null, null, _actions)));

    public Task<SonosGroupReadResult<SonosGroupMetadata>> ReadGroupMetadataAsync(
        string? groupId, CancellationToken cancellationToken)
    {
        var track = new SonosTrackMetadata(
            "track", "Preludium", "Chopin", "Nokturny", null, new SonosMetadataService("Sonos Radio", "9"), 180_000);
        return Task.FromResult(SonosGroupReadResult<SonosGroupMetadata>.Success(
            new SonosGroupMetadata(null, new SonosQueueItem("UTWOR-1", track, null), null, null, null)));
    }

    public Task<SonosGroupReadResult<SonosGroupVolume>> ReadGroupVolumeAsync(
        string? groupId, CancellationToken cancellationToken) =>
        Task.FromResult(SonosGroupReadResult<SonosGroupVolume>.Success(new SonosGroupVolume(30, false, false)));

    public Task<SonosGroupCommandResult> SendGroupCommandAsync(
        string? groupId, SonosGroupCommand command, CancellationToken cancellationToken)
    {
        Commands.Add(command);
        return Task.FromResult(SonosGroupCommandResult.CreateAcceptedForMeasurement(command));
    }

    public Task<SonosGroupCommandResult> SeekRelativeAsync(
        string? groupId, int deltaMillis, string? itemId, CancellationToken cancellationToken)
    {
        Commands.Add(SonosGroupCommand.SeekRelative);
        return Task.FromResult(
            SonosGroupCommandResult.CreateAcceptedForMeasurement(SonosGroupCommand.SeekRelative));
    }

    public Task<SonosGroupCommandResult> SetGroupVolumeAsync(
        string? groupId, int volume, CancellationToken cancellationToken)
    {
        Commands.Add(SonosGroupCommand.SetVolume);
        return Task.FromResult(SonosGroupCommandResult.CreateAcceptedForMeasurement(SonosGroupCommand.SetVolume));
    }

    public Task<SonosGroupCommandResult> SetGroupMuteAsync(
        string? groupId, bool muted, CancellationToken cancellationToken)
    {
        Commands.Add(SonosGroupCommand.SetMute);
        return Task.FromResult(SonosGroupCommandResult.CreateAcceptedForMeasurement(SonosGroupCommand.SetMute));
    }

    public Task<SonosHouseholdsReadResult> ReadHouseholdsAsync(CancellationToken cancellationToken) =>
        Task.FromResult(SonosHouseholdsReadResult.Success([new SonosHousehold("DOM-1", "Dom", null)]));

    public Task<SonosGroupsReadResult> ReadGroupsAsync(
        string householdId, CancellationToken cancellationToken) =>
        Task.FromResult(SonosGroupsReadResult.Success(new SonosHouseholdTopology(
            [new SonosGroup("GRUPA-SALON", "Salon", "P1", ["P1"], SonosPlaybackState.Idle)],
            [new SonosPlayer("P1", "Salon", null, null, null)],
            false)));
}

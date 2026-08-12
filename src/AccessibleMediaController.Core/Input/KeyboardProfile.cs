using AccessibleMediaController.Core.Commands;

namespace AccessibleMediaController.Core.Input;

public sealed class KeyboardProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Profil użytkownika";
    public bool IsBuiltIn { get; set; }
    public Dictionary<string, string> Bindings { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public string? Resolve(KeyChord chord) =>
        Bindings.TryGetValue(chord.Canonical, out var commandId) ? commandId : null;

    public IReadOnlyList<string> FindConflicts()
    {
        return Bindings
            .GroupBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public KeyboardProfile CreateEditableCopy(string name)
    {
        return new KeyboardProfile
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = name,
            IsBuiltIn = false,
            Bindings = new Dictionary<string, string>(Bindings, StringComparer.OrdinalIgnoreCase)
        };
    }

    public static KeyboardProfile CreateDefault()
    {
        var profile = new KeyboardProfile
        {
            Id = "default",
            Name = "Domyślny",
            IsBuiltIn = true
        };

        void Bind(string chord, string command) =>
            profile.Bindings[KeyChord.Parse(chord).Canonical] = command;

        Bind("1", CommandIds.SessionSlot(1));
        Bind("2", CommandIds.SessionSlot(2));
        Bind("3", CommandIds.SessionSlot(3));
        for (var slot = 4; slot <= 9; slot++) Bind($"{slot}", CommandIds.SessionSlot(slot));
        Bind("0", CommandIds.SessionList);
        Bind("PageUp", CommandIds.SessionPrevious);
        Bind("PageDown", CommandIds.SessionNext);

        Bind("Space", CommandIds.PlayPause);
        Bind("Left", CommandIds.SeekBackward10);
        Bind("Right", CommandIds.SeekForward10);
        Bind("Up", CommandIds.VolumeUp5);
        Bind("Down", CommandIds.VolumeDown5);
        Bind("Ctrl+Left", CommandIds.Previous);
        Bind("Ctrl+Right", CommandIds.Next);
        Bind("Shift+Left", CommandIds.SeekBackward60);
        Bind("Shift+Right", CommandIds.SeekForward60);
        Bind("Shift+Up", CommandIds.VolumeUp1);
        Bind("Shift+Down", CommandIds.VolumeDown1);
        Bind("Ctrl+Home", CommandIds.TrackStart);
        Bind("Ctrl+End", CommandIds.TrackEnd);

        Bind("Ctrl+E", CommandIds.TimeElapsed);
        Bind("Ctrl+R", CommandIds.TimeRemaining);
        Bind("Ctrl+T", CommandIds.TimeTotal);

        Bind("U", CommandIds.ViewFavorites);
        Bind("Shift+U", CommandIds.ToggleFavorite);
        Bind("P", CommandIds.ViewPlaylists);
        Bind("Shift+P", CommandIds.ManagePlaylists);
        Bind("F", CommandIds.SearchCurrent);
        Bind("Shift+F", CommandIds.SearchAll);
        Bind("K", CommandIds.FilterCurrent);
        Bind("Shift+K", CommandIds.CommandPalette);
        Bind("L", CommandIds.ViewLibrary);
        Bind("Shift+L", CommandIds.ToggleLibrary);
        Bind("Q", CommandIds.ViewQueue);
        Bind("Shift+Q", CommandIds.AddQueue);
        Bind("A", CommandIds.ViewAlbums);
        Bind("R", CommandIds.ViewRadio);
        Bind("Shift+R", CommandIds.StartRadio);
        Bind("M", CommandIds.ViewMixes);
        Bind("H", CommandIds.ViewHistory);
        Bind("N", CommandIds.ViewNowPlaying);
        Bind("Shift+N", CommandIds.OpenOfficialApp);
        Bind("I", CommandIds.ItemInformation);
        Bind("Shift+I", CommandIds.ExtendedInformation);
        Bind("O", CommandIds.ViewOutputs);
        Bind("D", CommandIds.DownloadInService);
        Bind("Shift+D", CommandIds.DownloadToDisk);
        Bind("F1", CommandIds.Help);

        return profile;
    }
}

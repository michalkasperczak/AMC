using AccessibleMediaController.Core.Configuration;

namespace AccessibleMediaController.Core.Sessions;

public sealed class SessionManager
{
    private readonly AppSettings _settings;

    public SessionManager(AppSettings settings)
    {
        _settings = settings;
        Sessions = CreateDemoSessions();
        Current = Sessions.FirstOrDefault(session => session.Id == settings.LastSessionId) ?? Sessions[0];
    }

    public IReadOnlyList<DemoMediaSession> Sessions { get; }
    public DemoMediaSession Current { get; private set; }

    public DemoMediaSession? SelectSlot(int slot)
    {
        if (!_settings.SessionSlots.TryGetValue(slot, out var sessionId)) return null;
        var session = Sessions.FirstOrDefault(candidate => candidate.Id == sessionId);
        if (session is null) return null;
        Current = session;
        _settings.LastSessionId = session.Id;
        return session;
    }

    public DemoMediaSession MoveSession(int direction)
    {
        var index = Sessions.ToList().FindIndex(session => session.Id == Current.Id);
        index = (index + direction + Sessions.Count) % Sessions.Count;
        Current = Sessions[index];
        _settings.LastSessionId = Current.Id;
        return Current;
    }

    private static IReadOnlyList<DemoMediaSession> CreateDemoSessions()
    {
        static List<MediaItem> Items(string service) =>
        [
            new() { Id = $"{service}-1", Title = "Pierwszy utwór demonstracyjny", Artist = "Wykonawca A", Duration = TimeSpan.FromMinutes(4.333), IsFavorite = true, IsInLibrary = true },
            new() { Id = $"{service}-2", Title = "Drugi utwór demonstracyjny", Artist = "Wykonawca B", Duration = TimeSpan.FromMinutes(3.75), IsInLibrary = true },
            new() { Id = $"{service}-3", Title = "Album demonstracyjny", Artist = "Wykonawca C", Kind = MediaItemKind.Album, Duration = TimeSpan.FromMinutes(47.383) },
            new() { Id = $"{service}-4", Title = "Do odsłuchu", Kind = MediaItemKind.Playlist, Duration = TimeSpan.FromMinutes(166) }
        ];

        return
        [
            new DemoMediaSession("tidal", "TIDAL", Items("tidal")),
            new DemoMediaSession("appleMusic", "Apple Music", Items("apple")),
            new DemoMediaSession("wiim", "WiiM", Items("wiim"))
        ];
    }
}

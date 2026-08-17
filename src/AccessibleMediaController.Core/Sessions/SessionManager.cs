using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Playback;

namespace AccessibleMediaController.Core.Sessions;

public sealed class SessionManager
{
    private readonly AppSettings _settings;
    private readonly List<DemoMediaSession> _sessions;
    private readonly Dictionary<int, string> _sessionSlots;

    public SessionManager(AppSettings settings)
    {
        _settings = settings;
        _sessions = CreateDemoSessions().ToList();
        _sessionSlots = new Dictionary<int, string>(settings.SessionSlots);
        var remembered = Sessions.FirstOrDefault(session => session.Id == settings.LastSessionId);
        Current = remembered ?? Sessions[0];
        if (remembered is null) _settings.LastSessionId = Current.Id;
    }

    public IReadOnlyList<DemoMediaSession> Sessions => _sessions;
    public IReadOnlyDictionary<int, string> SessionSlots => _sessionSlots;
    public DemoMediaSession Current { get; private set; }

    public DemoMediaSession? SelectSlot(int slot)
    {
        if (!_sessionSlots.TryGetValue(slot, out var sessionId)) return null;
        return SelectSession(sessionId);
    }

    public DemoMediaSession? SelectSession(string sessionId)
    {
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

    public DemoMediaSession? FindSession(string sessionId) =>
        _sessions.FirstOrDefault(session => session.Id == sessionId);

    public int? FindSlot(string sessionId)
    {
        var pair = _sessionSlots.FirstOrDefault(pair =>
            string.Equals(pair.Value, sessionId, StringComparison.Ordinal));
        return pair.Key > 0 ? pair.Key : null;
    }

    public (DemoMediaSession Session, int? Slot) AddOrUpdateTransientSession(
        string id,
        string displayName,
        IEnumerable<MediaItem> items,
        IMediaOutput output,
        int preferredSlot)
    {
        var session = FindSession(id);
        if (session is null)
        {
            session = new DemoMediaSession(id, displayName, items, output);
            _sessions.Add(session);
        }
        else
        {
            session.AddItems(items);
        }

        var existingSlot = FindSlot(id);
        if (existingSlot.HasValue) return (session, existingSlot);

        var slot = Enumerable.Range(Math.Clamp(preferredSlot, 1, 9), 10 - Math.Clamp(preferredSlot, 1, 9))
            .Concat(Enumerable.Range(1, Math.Clamp(preferredSlot, 1, 9) - 1))
            .FirstOrDefault(candidate => !_sessionSlots.ContainsKey(candidate));
        if (slot > 0) _sessionSlots[slot] = id;
        return (session, slot > 0 ? slot : null);
    }

    private static IReadOnlyList<DemoMediaSession> CreateDemoSessions()
    {
        static List<MediaItem> Items(string service) =>
        [
            new() { Id = $"{service}-1", Title = "Pierwszy utwór demonstracyjny", Artist = "Wykonawca A", Duration = TimeSpan.FromMinutes(4.333), IsFavorite = true, IsInLibrary = true },
            new() { Id = $"{service}-2", Title = "Drugi utwór demonstracyjny", Artist = "Wykonawca B", Duration = TimeSpan.FromMinutes(3.75), IsInLibrary = true },
            new() { Id = $"{service}-3", Title = "Album demonstracyjny", Artist = "Wykonawca C", Kind = MediaItemKind.Album, Duration = TimeSpan.FromMinutes(47.383) },
            new() { Id = $"{service}-4", Title = "Do odsłuchu", Kind = MediaItemKind.Playlist, Duration = TimeSpan.FromMinutes(166) },
            new() { Id = $"{service}-5", Title = "Brzeg ciszy", Artist = "Anna Kowalska", Duration = TimeSpan.FromMinutes(3.2), IsFavorite = true },
            new() { Id = $"{service}-6", Title = "Błękitna godzina", Artist = "Bartosz Nowak", Duration = TimeSpan.FromMinutes(4.1) },
            new() { Id = $"{service}-7", Title = "Ciepły deszcz", Artist = "Celina Maj", Duration = TimeSpan.FromMinutes(2.9) },
            new() { Id = $"{service}-8", Title = "Cisza o świcie", Artist = "Chór Północy", Duration = TimeSpan.FromMinutes(5.05) },
            new() { Id = $"{service}-9", Title = "Droga przez las", Artist = "Daniel Wrona", Duration = TimeSpan.FromMinutes(3.6) },
            new() { Id = $"{service}-10", Title = "Echo miasta", Artist = "Ewa Sadowska", Duration = TimeSpan.FromMinutes(4.45) },
            new() { Id = $"{service}-11", Title = "Fala światła", Artist = "Filip Górski", Duration = TimeSpan.FromMinutes(3.35) },
            new() { Id = $"{service}-12", Title = "Jesienny poranek", Artist = "Julia Lis", Duration = TimeSpan.FromMinutes(4.75) },
            new() { Id = $"{service}-13", Title = "Nocny pociąg", Artist = "Natalia Róża", Duration = TimeSpan.FromMinutes(6.1), IsInQueue = true },
            new() { Id = $"{service}-14", Title = "Północny wiatr", Artist = "Piotr Wilk", Duration = TimeSpan.FromMinutes(3.95), IsFavorite = true },
            new() { Id = $"{service}-15", Title = "Szept fal", Artist = "Sara Klon", Duration = TimeSpan.FromMinutes(4.6) },
            new() { Id = $"{service}-16", Title = "Światło księżyca", Artist = "Świt", Duration = TimeSpan.FromMinutes(5.25) },
            new() { Id = $"{service}-17", Title = "Zielony horyzont", Artist = "Zofia Polna", Duration = TimeSpan.FromMinutes(3.8) }
        ];

        return
        [
            new DemoMediaSession("tidal", "TIDAL", Items("tidal")),
            new DemoMediaSession("appleMusic", "Apple Music", Items("apple")),
            new DemoMediaSession("wiim", "WiiM", Items("wiim"))
        ];
    }
}

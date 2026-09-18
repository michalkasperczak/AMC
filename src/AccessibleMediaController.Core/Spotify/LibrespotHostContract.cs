namespace AccessibleMediaController.Core.Spotify;

/// <summary>
/// Stabilny kontrakt v1 miedzy AMC i osobnym procesem hosta Librespot
/// (sesja "Spotify — Librespot"). Trzymamy go w Core, zeby transport dawal sie
/// testowac bez WPF i bez konta.
///
/// Zasady, ktore ten kontrakt wymusza:
/// - token konta idzie WYLACZNIE strumieniem stdin w poleceniu "initialize";
///   nigdy w argv ani w zmiennych srodowiskowych,
/// - stdout hosta to WYLACZNIE linie JSON (camelCase),
/// - kazde polecenie ma wlasny requestId, a kazde odtwarzanie wlasny playId
///   nadany przez AMC; odpowiedz i zdarzenie STAREGO playId nie moga ruszyc
///   stanu biezacego utworu,
/// - zdarzenie "ready" mowi tylko, ze proces wstal i zna wersje protokolu.
///   NIE znaczy, ze konto jest zalogowane.
/// </summary>
public static class LibrespotHostContract
{
    /// <summary>Wersja protokolu, ktora ten klient rozumie.</summary>
    public const int ProtocolVersion = 1;

    /// <summary>Identyfikator sesji wysylany w kazdym poleceniu.</summary>
    public const string SessionId = "spotifyLibrespot";

    public const string CommandPing = "ping";
    public const string CommandDevices = "devices";
    public const string CommandInitialize = "initialize";
    public const string CommandPlay = "play";
    public const string CommandPause = "pause";
    public const string CommandResume = "resume";
    public const string CommandStop = "stop";
    public const string CommandSeek = "seek";
    public const string CommandVolume = "volume";
    public const string CommandShutdown = "shutdown";
}

/// <summary>Urzadzenie wyjscia zgloszone przez hosta. Nazwa jest dokladna.</summary>
public sealed record LibrespotOutputDevice(string Name, bool IsDefault);

/// <summary>
/// Kody bledow transportu. Sluza do budowania czytelnego komunikatu w warstwie
/// interfejsu; nigdy nie niosa tokenu ani tresci linii stdout.
/// </summary>
public static class LibrespotHostErrorCodes
{
    public const string NotStarted = "host_not_started";
    public const string StartFailed = "host_start_failed";
    public const string HostExited = "host_exited";
    public const string Timeout = "host_timeout";
    public const string ProtocolVersionMismatch = "protocol_version_mismatch";
    public const string ProtocolViolation = "protocol_violation";
    public const string LineTooLong = "protocol_line_too_long";
    public const string HostError = "host_error";
    public const string Disposed = "host_disposed";
    public const string TokenLeakGuard = "token_leak_guard";
}

/// <summary>
/// Blad transportu hosta Librespot. Zawsze ma kod i tresc dla czlowieka;
/// cisza jest niedopuszczalna, wiec kazda awaria procesu, nieznana wersja
/// protokolu i przekroczony czas konczy sie tym wyjatkiem.
/// </summary>
public sealed class LibrespotHostException : Exception
{
    public LibrespotHostException(string code, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
    }

    public string Code { get; }
}

/// <summary>Stan odtwarzania zgloszony przez hosta dla biezacego playId.</summary>
public sealed class LibrespotStateEventArgs(
    long playId,
    string uri,
    TimeSpan position,
    TimeSpan duration,
    bool isPlaying,
    bool isPaused) : EventArgs
{
    public long PlayId { get; } = playId;
    public string Uri { get; } = uri;
    public TimeSpan Position { get; } = position;
    public TimeSpan Duration { get; } = duration;
    public bool IsPlaying { get; } = isPlaying;
    public bool IsPaused { get; } = isPaused;
}

/// <summary>Potwierdzony koniec utworu dla biezacego playId.</summary>
public sealed class LibrespotEndedEventArgs(long playId, string uri) : EventArgs
{
    public long PlayId { get; } = playId;
    public string Uri { get; } = uri;
}

/// <summary>Blad pojedynczego utworu; nie konczy sesji ani procesu.</summary>
public sealed class LibrespotTrackErrorEventArgs(long playId, string uri, string code) : EventArgs
{
    public long PlayId { get; } = playId;
    public string Uri { get; } = uri;
    public string Code { get; } = code;
}

/// <summary>
/// Awaria calego hosta: proces zakonczyl sie, strumien sie zamknal, host
/// przyslal cos innego niz JSON albo zglosil nieznana wersje protokolu.
/// </summary>
public sealed class LibrespotHostFailureEventArgs(string code, string message) : EventArgs
{
    public string Code { get; } = code;
    public string Message { get; } = message;
}

/// <summary>Skutek polecenia "graj" po odpowiedzi hosta.</summary>
public enum LibrespotPlayOutcome
{
    /// <summary>Host przyjal utwor i to nadal jest biezaca proba.</summary>
    Accepted,

    /// <summary>W trakcie przygotowania przyszla pauza; host zostal zapauzowany.</summary>
    SupersededByPause,

    /// <summary>W trakcie przygotowania przyszlo zatrzymanie; host zostal zatrzymany.</summary>
    SupersededByStop,

    /// <summary>W trakcie przygotowania uruchomiono inny utwor.</summary>
    SupersededByNewPlay
}

/// <summary>Limity i czasy transportu. Testy skracaja je do milisekund.</summary>
public sealed class LibrespotHostOptions
{
    /// <summary>Ile czekamy na zdarzenie startowe "ready".</summary>
    public TimeSpan ReadyTimeout { get; init; } = TimeSpan.FromSeconds(20);

    /// <summary>Ile czekamy na odpowiedz na jedno polecenie.</summary>
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Gorna granica dlugosci jednej linii stdout. Bez niej zepsuty host moze
    /// zjesc cala pamiec AMC jednym niezakonczonym wierszem.
    /// </summary>
    public int MaxLineLength { get; init; } = 64 * 1024;

    /// <summary>Ile czekamy na zamkniecie procesu po "shutdown", zanim go ubijemy.</summary>
    public TimeSpan ShutdownTimeout { get; init; } = TimeSpan.FromSeconds(5);
}

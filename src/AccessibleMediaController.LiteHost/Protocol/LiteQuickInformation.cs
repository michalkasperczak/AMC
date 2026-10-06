using System.Globalization;
using System.Text.Json;
using AccessibleMediaController.Core.LocalMedia;
using AccessibleMediaController.Core.Presentation;
using AccessibleMediaController.Core.Sessions;

namespace AccessibleMediaController.LiteHost.Protocol;

/// <summary>
/// Zadanie o krotka informacje uzupelniajaca (LEWA STRZALKA na liscie).
/// Niesie DANE wiersza, nie gotowy napis -- napis sklada host.
/// </summary>
/// <param name="ItemId">
/// Tozsamosc wiersza, zawsze NAPIS. Frontend porownuje ja po powrocie
/// odpowiedzi, zeby nie przeczytac parametrow cudzego wyboru.
/// </param>
public sealed record LiteQuickInfoRequest(
    string Session,
    string ItemId,
    string Title,
    string Kind,
    string Source,
    long DurationTicks,
    long? PodcastMediaLength,
    string? PodcastMediaType);

/// <summary>
/// Wynik POMIARU silnika. Protokol nie czyta tu dzwieku ani atrybutow
/// chmury: na Windows wypelnia to <c>WindowsMediaOutput.TryReadMetadataAsync</c>
/// (plik, 5 s) albo <c>RadioMediaOutput.TryReadStreamMetadataAsync</c>
/// (strumien, 6 s), a <c>CloudFileAvailability.MayRequireRemoteAccess</c>
/// oddaje decyzje o chmurze z SAMYCH ATRYBUTOW pliku.
///
/// Rozdzielenie jest celowe: dzieki niemu skladanie komunikatu da sie
/// wykonac i zmierzyc bez Windows, a mimo to tym samym kodem C#.
/// </summary>
public sealed class LiteQuickInfoProbe
{
    /// <summary>Czy plik DA SIE odczytac. Brak pliku to brak rozmiaru, nie rozmiar zero.</summary>
    public bool Exists { get; init; }

    /// <summary><c>FileInfo.Length</c>. <c>null</c>, gdy nie zmierzono -- np. placeholder chmury.</summary>
    public long? SizeBytes { get; init; }

    public int? BitrateKbps { get; init; }
    public int? SampleRateHz { get; init; }
    public string? Codec { get; init; }

    /// <summary>
    /// Czas ZMIERZONY przez silnik. Uzupelnia wiersz tylko wtedy, gdy ten nie
    /// zna czasu (cs:5504-5508); znanego czasu NIE nadpisuje.
    /// </summary>
    public long? DurationTicks { get; init; }

    public bool MayRequireCloudDownload { get; init; }
}

/// <summary>
/// Port <c>MainWindow.BuildQuickMediaInformation</c> (<c>MainWindow.xaml.cs:5425-5483</c>)
/// do warstwy protokolu hosta.
///
/// Napis skleja PRAWDZIWY <see cref="QuickMediaInformationFormatter"/> z Core --
/// ten sam, ktorego uzywa pelne AMC. Kolejnosc czesci, jednostki, polskie
/// separatory i zdanie o braku danych NIE sa tu powtorzone, bo kazda kopia
/// rozjechalaby sie z oryginalem.
///
/// CZEGO TU NIE MA, swiadomie:
///   * ZAPISU uzupelnionych parametrow. Oryginal wola <c>TrySaveLocalMediaState</c>
///     (cs:5472) i <c>QueueStateSave</c> -- wlascicielem profilu zostaje pelne
///     AMC, a port wx czyta go TYLKO DO ODCZYTU.
///   * galezi TIDAL (cs:5474-5481). Uslugi streamingowe sa poza zakresem tej
///     zmiany, wiec komunikat katalogu nie jest tu przepisywany.
/// </summary>
public static class LiteQuickInformation
{
    public const string Operation = "media.quickInformation";

    public static LiteQuickInfoRequest ReadRequest(JsonElement args)
    {
        var podcastMediaLength = ReadLong(args, "podcastMediaLength");
        return new LiteQuickInfoRequest(
            Session: LiteArgs.RequireText(args, "session"),
            ItemId: LiteArgs.RequireText(args, "itemId"),
            Title: LiteArgs.ReadText(args, "title") ?? string.Empty,
            Kind: LiteArgs.ReadText(args, "kind") ?? string.Empty,
            Source: LiteArgs.RequireText(args, "source"),
            DurationTicks: ReadLong(args, "durationTicks") ?? 0L,
            PodcastMediaLength: podcastMediaLength,
            PodcastMediaType: LiteArgs.ReadText(args, "podcastMediaType"));
    }

    public static string Build(
        LiteQuickInfoRequest request,
        LiteQuickInfoProbe probe,
        CultureInfo? culture = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(probe);

        var item = new MediaItem
        {
            Id = request.ItemId,
            Title = request.Title,
            Kind = ParseKind(request.Kind),
            Source = request.Source,
            // Czas niedodatni ZNACZY brak pomiaru (warunek cs:5451 i
            // Formatter:37), a nie utwor dlugosci zero. Gdy wiersz go nie zna,
            // wchodzi czas ZMIERZONY przez silnik (cs:5504-5508) -- i tylko
            // wtedy, bo oryginal nie nadpisuje znanej dlugosci.
            Duration = request.DurationTicks > 0
                ? TimeSpan.FromTicks(request.DurationTicks)
                : probe.DurationTicks is > 0
                    ? TimeSpan.FromTicks(probe.DurationTicks.Value)
                    : TimeSpan.Zero,
            BitrateKbps = probe.BitrateKbps,
            SampleRateHz = probe.SampleRateHz,
            Codec = probe.Codec
        };

        var isLocal = IsLocalSource(request.Source);
        string? containerFormat = null;
        long? sizeBytes = null;

        if (isLocal)
        {
            // cs:5439-5440 -- kontener z ROZSZERZENIA. Zostaje nawet wtedy, gdy
            // pliku nie da sie otworzyc: to jedyna uczciwa dana, jaka mamy.
            var extension = Path.GetExtension(request.Source).TrimStart('.');
            if (!string.IsNullOrWhiteSpace(extension)) containerFormat = extension;

            if (probe.Exists)
            {
                if (item.BitrateKbps is null
                    && item.Duration > TimeSpan.Zero
                    && probe.SizeBytes is > 0
                    && !LocalAudioFileDiscovery.IsVideoFile(request.Source))
                {
                    // cs:5450-5458. Ten sam kod Core, zeby liczba zgadzala sie
                    // z pelnym AMC do jedynki.
                    item.BitrateKbps = LocalAudioFileDiscovery.EstimateBitrateKbps(
                        probe.SizeBytes.Value,
                        item.Duration);
                    item.IsBitrateEstimated = item.BitrateKbps.HasValue;
                }
                sizeBytes = probe.SizeBytes;
            }
        }
        else if (request.PodcastMediaLength is > 0)
        {
            // cs:5468-5471 -- odcinek podcastu podaje rozmiar z kanalu.
            sizeBytes = request.PodcastMediaLength;
            item.Codec ??= FormatPodcastCodec(request.PodcastMediaType);
            item.BitrateKbps ??= EstimatePodcastBitrateKbps(
                request.PodcastMediaLength.Value,
                item.Duration);
            item.IsBitrateEstimated = item.BitrateKbps.HasValue && probe.BitrateKbps is null;
        }

        return QuickMediaInformationFormatter.Format(
            item,
            containerFormat,
            sizeBytes,
            probe.MayRequireCloudDownload,
            unavailableFormatMessage: null,
            culture ?? CultureInfo.CurrentCulture);
    }

    /// <summary>
    /// Port <c>MainWindow.FormatPodcastCodec</c> (<c>cs:5413-5423</c>).
    /// Nieznany typ MIME daje <c>null</c>, czyli BRAK kodeka -- nie wolno
    /// zastapic go slowem "nieznany".
    /// </summary>
    public static string? FormatPodcastCodec(string? mediaType) =>
        mediaType?.Trim().ToLowerInvariant() switch
        {
            "audio/mpeg" or "audio/mp3" => "MP3",
            "audio/mp4" or "audio/x-m4a" => "M4A/AAC",
            "audio/aac" or "audio/aacp" => "AAC",
            "audio/ogg" or "application/ogg" => "OGG",
            "audio/opus" => "Opus",
            "audio/flac" or "audio/x-flac" => "FLAC",
            "audio/wav" or "audio/wave" or "audio/x-wav" => "WAV",
            _ => null
        };

    /// <summary>Port <c>MainWindow.EstimatePodcastBitrateKbps</c> (<c>cs:5405-5411</c>).</summary>
    public static int? EstimatePodcastBitrateKbps(long mediaLength, TimeSpan duration)
    {
        if (mediaLength <= 0 || duration <= TimeSpan.Zero) return null;
        return LocalAudioFileDiscovery.EstimateBitrateKbps(mediaLength, duration);
    }

    /// <summary>
    /// Rozpoznanie zrodla LOKALNEGO. Odpowiednik <c>MainWindow.TryGetLocalPath</c>
    /// (<c>cs:5378-5390</c>), ale BEZ <c>Path.IsPathFullyQualified</c>: ta metoda
    /// ma semantyke systemu, na ktorym biegnie, i na Linuksie odrzucilaby
    /// <c>D:\muzyka\a.mp3</c>. Warstwa protokolu musi dawac ten sam wynik w WSL
    /// i na Windows, bo inaczej testu nie da sie uznac za pomiar.
    /// </summary>
    public static bool IsLocalSource(string? source)
    {
        if (string.IsNullOrWhiteSpace(source)) return false;

        // http(s), tidal:, spotify: itd. -- zdalne, jak w oryginale.
        if (Uri.TryCreate(source, UriKind.Absolute, out var uri) && !uri.IsFile) return false;

        if (source.StartsWith(@"\\", StringComparison.Ordinal)) return true;          // UNC
        if (source.StartsWith('/')) return true;                                       // POSIX
        return source.Length >= 3
            && char.IsAsciiLetter(source[0])
            && source[1] == ':'
            && source[2] is '\\' or '/';                                               // dysk Windows
    }

    private static MediaItemKind ParseKind(string? kind) =>
        kind?.Trim().ToLowerInvariant() switch
        {
            "station" => MediaItemKind.Station,
            "episode" => MediaItemKind.Episode,
            "podcast" => MediaItemKind.Podcast,
            "video" => MediaItemKind.Video,
            "album" => MediaItemKind.Album,
            "playlist" => MediaItemKind.Playlist,
            "artist" => MediaItemKind.Artist,
            "folder" => MediaItemKind.Folder,
            _ => MediaItemKind.Track
        };

    /// <summary>
    /// Rozmiar i czas nie mieszcza sie w <c>int</c>. <c>LiteArgs.ReadInt</c>
    /// przyciąłby je bez slowa, dlatego czytamy je tu jawnie jako <c>long</c>.
    /// </summary>
    private static long? ReadLong(JsonElement args, string name)
    {
        if (args.ValueKind != JsonValueKind.Object) return null;
        if (!args.TryGetProperty(name, out var property)) return null;
        if (property.ValueKind != JsonValueKind.Number) return null;
        return property.TryGetInt64(out var value) ? value : null;
    }
}

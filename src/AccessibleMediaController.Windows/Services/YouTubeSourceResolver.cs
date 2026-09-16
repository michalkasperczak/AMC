using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;

namespace AccessibleMediaController.Windows.Services;

internal sealed record ResolvedYouTubeAudioSource(
    string PageUrl,
    string StreamUrl,
    string Title,
    string Channel,
    TimeSpan Duration,
    bool IsLive,
    bool IsHls,
    string? Codec,
    int? BitrateKbps);

/// <summary>
/// Resolves a stable public YouTube page address to one temporary audio URL.
/// The signed URL is never persisted and no browser cookies are read.
/// </summary>
internal static class YouTubeSourceResolver
{
    private const int MaximumJsonCharacters = 8 * 1024 * 1024;
    private static readonly string[] YouTubeHosts =
    [
        "youtube.com",
        "youtu.be",
        "youtube-nocookie.com"
    ];

    internal static bool IsYouTubeUrl(string? value)
    {
        if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https")
            || string.IsNullOrWhiteSpace(uri.Host))
        {
            return false;
        }

        var host = uri.IdnHost.TrimEnd('.');
        return YouTubeHosts.Any(candidate =>
            host.Equals(candidate, StringComparison.OrdinalIgnoreCase)
            || host.EndsWith('.' + candidate, StringComparison.OrdinalIgnoreCase));
    }

    internal static async Task<ResolvedYouTubeAudioSource> ResolveLiveAudioAsync(
        string pageUrl,
        CancellationToken cancellationToken) =>
        await ResolveAudioCoreAsync(pageUrl, requireLive: true, cancellationToken).ConfigureAwait(false);

    internal static async Task<ResolvedYouTubeAudioSource> ResolveAudioAsync(
        string pageUrl,
        CancellationToken cancellationToken) =>
        await ResolveAudioCoreAsync(pageUrl, requireLive: false, cancellationToken).ConfigureAwait(false);

    private static async Task<ResolvedYouTubeAudioSource> ResolveAudioCoreAsync(
        string pageUrl,
        bool requireLive,
        CancellationToken cancellationToken)
    {
        if (!IsYouTubeUrl(pageUrl))
            throw new InvalidDataException("Adres nie prowadzi do YouTube.");

        var executable = FindExecutable();
        if (executable is null)
        {
            throw new NotSupportedException(
                "Odtwarzanie publicznego materiału YouTube wymaga składnika yt-dlp. Wybierz Pomoc, Sprawdź składniki: FFmpeg i yt-dlp.");
        }

        var start = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        ExternalToolProcess.ApplySafeEnvironment(start, executable);
        foreach (var argument in new[]
        {
            "--ignore-config",
            "--no-playlist",
            "--no-warnings",
            "--no-live-from-start",
            "--force-ipv4",
            "--socket-timeout", "20",
            "--retries", "3",
            "--fragment-retries", "3",
            "--extractor-retries", "2",
            "--format", "bestaudio[ext=m4a]/bestaudio/best[acodec!=none]",
            // 2026-09-14: ZMIERZONE na czterech zywych transmisjach. Czesc kanalow
            // (np. Dominikanie Ustron, txtC7cxR6m8) oddaje strumien WYLACZNIE
            // klientowi "android"; domyslny zwraca "This video is not available",
            // a "web"/"mweb"/"web_safari" - "No video formats found". Kolejnosc ma
            // znaczenie: "default,android" zostawia sprawnym kanalom lzejszy format
            // 234 (czysty AAC), a Ustroniowi daje 95. Odwrotna kolejnosc pogarsza
            // sprawne kanaly. Jeden extractor-args, bo dwa osobne wykluczaja sie.
            // 2026-09-16: ZDJETE "lang=pl". Zmierzone na kanalach Michala: z
            // polskim jezykiem YouTube na KAZDA przyczyne odpowiada tym samym
            // zdaniem ("Ten film jest niedostepny"), a po angielsku rozroznia
            // zakonczona transmisje ("This live stream recording is not
            // available") od nieistniejacego nagrania ("This video is
            // unavailable"). Program tlumaczy te frazy sam - patrz
            // YouTubeErrorTranslator - i dzieki temu mowi PRAWDZIWA przyczyne
            // zamiast sugerowac blokade. Tytuly transmisji sa i tak wlasnymi
            // nazwami kanalow, wiec nic po polsku nie tracimy.
            "--extractor-args", "youtube:player_client=default,android",
            "--dump-single-json",
            "--",
            pageUrl.Trim()
        })
        {
            start.ArgumentList.Add(argument);
        }

        Process? process = null;
        try
        {
            process = Process.Start(start)
                ?? throw new InvalidDataException("Nie udało się uruchomić składnika yt-dlp.");
            process.StandardInput.Close();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(75));
            using var termination = timeout.Token.Register(
                static state =>
                {
                    try
                    {
                        var running = (Process)state!;
                        if (!running.HasExited) running.Kill(entireProcessTree: true);
                    }
                    catch (Exception)
                    {
                    }
                },
                process);
            var outputTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var errorTask = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            var output = await outputTask.ConfigureAwait(false);
            var error = await errorTask.ConfigureAwait(false);
            if (process.ExitCode != 0)
            {
                // Do 16.09.2026 tresc bledu byla tu WYRZUCANA, a uzytkownik
                // slyszal jedno zdanie o "braku publicznego strumienia" na
                // kazda przyczyne - takze na zakonczona transmisje. Teraz blad
                // idzie do logu w calosci i do komunikatu w wersji zrozumialej.
                DiagnosticLog.Warning("youtube",
                    $"yt-dlp zakonczyl sie kodem {process.ExitCode} dla {pageUrl.Trim()}: {error.Trim()}");
                throw new InvalidDataException(YouTubeErrorTranslator.Describe(error));
            }
            if (output.Length == 0 || output.Length > MaximumJsonCharacters)
                throw new InvalidDataException("YouTube zwrócił nieprawidłowe dane źródła.");
            return ParseResult(pageUrl.Trim(), output, requireLive);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("YouTube nie odpowiedział w bezpiecznym czasie.");
        }
        catch (Win32Exception exception)
        {
            throw new InvalidDataException("Nie udało się uruchomić składnika yt-dlp.", exception);
        }
        finally
        {
            process?.Dispose();
        }
    }

    internal static ResolvedYouTubeAudioSource ParseResult(
        string pageUrl,
        string json,
        bool requireLive)
    {
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                MaxDepth = 64
            });
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("YouTube zwrócił nieprawidłowe dane źródła.");
            var root = document.RootElement;
            var liveStatus = ReadString(root, "live_status");
            var isLive = ReadBoolean(root, "is_live")
                         || liveStatus.Equals("is_live", StringComparison.OrdinalIgnoreCase);
            if (requireLive && !isLive)
            {
                throw new InvalidDataException(
                    "Ten adres YouTube nie jest obecnie transmisją na żywo. Zwykły film dodaj w sesji Podcasty i YouTube jako medium internetowe.");
            }

            var selected = SelectAudio(root);
            if (selected is null)
                throw new InvalidDataException("YouTube nie udostępnił obsługiwanego strumienia audio.");
            var streamUrl = ReadString(selected.Value, "url");
            if (!Uri.TryCreate(streamUrl, UriKind.Absolute, out var streamUri)
                || streamUri.Scheme is not ("http" or "https")
                || !string.IsNullOrWhiteSpace(streamUri.UserInfo))
            {
                throw new InvalidDataException("YouTube zwrócił nieprawidłowy adres strumienia audio.");
            }

            var protocol = ReadString(selected.Value, "protocol");
            var extension = ReadString(selected.Value, "ext");
            var isHls = protocol.Contains("m3u8", StringComparison.OrdinalIgnoreCase)
                        || extension.Equals("m3u8", StringComparison.OrdinalIgnoreCase)
                        || streamUri.Host.Equals("manifest.googlevideo.com", StringComparison.OrdinalIgnoreCase)
                        || streamUri.AbsolutePath.Contains("/manifest/hls_", StringComparison.OrdinalIgnoreCase);
            var title = ReadString(root, "title").Trim();
            if (title.Length == 0) title = isLive ? "YouTube na żywo" : "Materiał YouTube";
            if (title.Length > 500) title = title[..500].Trim();
            var channel = ReadString(root, "channel").Trim();
            if (channel.Length == 0) channel = ReadString(root, "uploader").Trim();
            if (channel.Length > 300) channel = channel[..300].Trim();
            var durationSeconds = ReadNumber(root, "duration");
            var duration = durationSeconds is > 0 and < 365 * 24 * 60 * 60
                ? TimeSpan.FromSeconds(durationSeconds.Value)
                : TimeSpan.Zero;
            var codec = ReadString(selected.Value, "acodec");
            if (codec.Equals("none", StringComparison.OrdinalIgnoreCase)) codec = string.Empty;
            var bitrate = ReadNumber(selected.Value, "abr");
            var normalizedBitrate = bitrate is > 0 and < 10_000
                ? (int?)Math.Round(bitrate.Value)
                : null;
            return new ResolvedYouTubeAudioSource(
                pageUrl,
                streamUri.AbsoluteUri,
                title,
                channel,
                duration,
                isLive,
                isHls,
                codec.Length == 0 ? null : codec,
                normalizedBitrate);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("YouTube zwrócił nieprawidłowe dane źródła.", exception);
        }
    }

    private static JsonElement? SelectAudio(JsonElement root)
    {
        // Najpierw szukamy sciezki BEZ obrazu - jest najlzejsza dla sieci.
        // Gdy jej nie ma, bierzemy strumien z obrazem, bo ffmpeg wyciaga z niego
        // sam dzwiek. ZMIERZONE 15.09.2026 (Dominikanie Ustron Hermanice,
        // txtC7cxR6m8): ten kanal oddaje WYLACZNIE formaty 91-95, czyli obraz
        // razem z dzwiekiem, i zadnego tylko-audio. Wczesniejszy warunek
        // odrzucal je wszystkie, a program mowil "YouTube nie udostepnil
        // publicznego strumienia audio" - co bylo nieprawda, transmisja byla
        // publiczna i grala.
        foreach (var audioOnly in new[] { true, false })
        {
            foreach (var property in new[] { "requested_downloads", "requested_formats" })
            {
                if (!root.TryGetProperty(property, out var container)
                    || container.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }
                foreach (var candidate in container.EnumerateArray())
                {
                    if (IsUsableAudio(candidate, audioOnly)) return candidate;
                }
            }
            if (IsUsableAudio(root, audioOnly)) return root;
            if (root.TryGetProperty("formats", out var formats)
                && formats.ValueKind == JsonValueKind.Array)
            {
                foreach (var candidate in formats.EnumerateArray())
                {
                    if (IsUsableAudio(candidate, audioOnly)) return candidate;
                }
            }
        }
        return null;
    }

    private static bool IsUsableAudio(JsonElement candidate, bool requireAudioOnly)
    {
        if (candidate.ValueKind != JsonValueKind.Object
            || string.IsNullOrWhiteSpace(ReadString(candidate, "url")))
        {
            return false;
        }
        var audioCodec = ReadString(candidate, "acodec");
        // Dzwiek musi byc - format bez dzwieku jest bezuzyteczny.
        if (audioCodec.Length == 0
            || audioCodec.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        if (!requireAudioOnly) return true;
        var videoCodec = ReadString(candidate, "vcodec");
        return videoCodec.Length == 0 || videoCodec.Equals("none", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ReadBoolean(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value)
        && value.ValueKind is JsonValueKind.True;

    private static string ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static double? ReadNumber(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetDouble(out var number)
            ? number
            : null;

    internal static string? FindExecutable()
    {
        var managed = YtDlpComponentManager.FindInstalledExecutable();
        if (managed is not null) return managed;
        var bundled = Path.Combine(AppContext.BaseDirectory, "yt-dlp.exe");
        if (File.Exists(bundled)) return bundled;
        var configured = Environment.GetEnvironmentVariable("YTDLP_PATH");
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured)) return configured;
        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                     .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            try
            {
                var candidate = Path.Combine(directory, "yt-dlp.exe");
                if (File.Exists(candidate)) return candidate;
            }
            catch (Exception exception) when (exception is ArgumentException
                or NotSupportedException
                or PathTooLongException)
            {
            }
        }
        return null;
    }
}

using System.Diagnostics;
using System.Text.Json;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Windows.Services;

internal static class RadioLiveTimingProbe
{
    internal static void Run(string url, int seconds, string outputPath)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var source)
            || source.Scheme != Uri.UriSchemeHttps
            || seconds is < 30 or > 600)
            throw new ArgumentException("Probe requires an HTTPS stream and 30–600 seconds.");
        var parent = Path.GetDirectoryName(Path.GetFullPath(outputPath))!;
        Directory.CreateDirectory(parent);
        using var writer = new StreamWriter(outputPath, append: false,
            new System.Text.UTF8Encoding(false)) { AutoFlush = true };
        var gate = new object();
        var watch = Stopwatch.StartNew();
        void Log(string kind, object? details = null)
        {
            lock (gate)
            {
                writer.WriteLine(JsonSerializer.Serialize(new
                {
                    kind,
                    elapsedMs = watch.ElapsedMilliseconds,
                    utc = DateTimeOffset.UtcNow.ToString("O"),
                    details
                }));
            }
        }

        using var radio = new RadioMediaOutput(1, audible: false);
        var started = false;
        radio.PlaybackStarted += (_, _) => { started = true; Log("started"); };
        radio.PlaybackFailed += (_, eventArgs) =>
            Log("failed", new { eventArgs.Message });
        radio.NowPlayingChanged += (_, eventArgs) =>
        {
            radio.TryGetRecentCapturedAudio(TimeSpan.FromSeconds(3), out var sample);
            Log("title", new
            {
                eventArgs.StreamTitle,
                endByte = sample?.Anchor?.EndByte,
                segmentId = sample?.Anchor?.SegmentId,
                bytesPerSecond = sample?.Format.AverageBytesPerSecond
            });
        };
        Log("opening", new { url });
        radio.Play(new MediaItem
        {
            Id = "radio-live-timing-probe",
            Title = "Pomiar radia",
            Source = url,
            Kind = MediaItemKind.Station
        }, TimeSpan.Zero, 0, 1d);

        Task? recognition = null;
        for (var tick = 1; tick <= seconds; tick++)
        {
            Thread.Sleep(1000);
            if (tick % 5 == 0)
            {
                var available = radio.TryGetRecentCapturedAudio(
                    TimeSpan.FromSeconds(12), out var sample);
                Log("sample", new
                {
                    available,
                    endByte = sample?.Anchor?.EndByte,
                    startByte = sample?.Anchor?.StartByte,
                    segmentId = sample?.Anchor?.SegmentId,
                    bytesPerSecond = sample?.Format.AverageBytesPerSecond,
                    bufferedSeconds = radio.BufferedDuration.TotalSeconds
                });
                if (tick >= 30 && recognition is null && available && sample is not null)
                {
                    var captured = sample;
                    var requestStarted = watch.ElapsedMilliseconds;
                    Log("recognitionStart", new
                    {
                        captured.Anchor?.StartByte,
                        captured.Anchor?.EndByte,
                        bytesPerSecond = captured.Format.AverageBytesPerSecond
                    });
                    recognition = Task.Run(async () =>
                    {
                        using var recognitionTimeout = new CancellationTokenSource(
                            TimeSpan.FromSeconds(35));
                        try
                        {
                            var result = await new ShazamTrackRecognitionService()
                                .RecognizeAsync(captured, recognitionTimeout.Token);
                            Log("recognitionResult", new
                            {
                                result.Success,
                                result.Title,
                                result.Artist,
                                result.Error,
                                latencyMs = watch.ElapsedMilliseconds - requestStarted
                            });
                        }
                        catch (Exception exception)
                        {
                            Log("recognitionException", new
                            {
                                type = exception.GetType().Name,
                                exception.Message
                            });
                        }
                    });
                }
            }
            if (!started && tick >= (url.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase) ? 95 : 35)) break;
        }
        radio.Stop();
        if (recognition is not null)
        {
            try { recognition.Wait(TimeSpan.FromSeconds(40)); }
            catch (AggregateException exception) { Log("recognitionWait", exception.Message); }
        }
        Log("completed", new { started });
        Console.WriteLine(outputPath);
    }
}

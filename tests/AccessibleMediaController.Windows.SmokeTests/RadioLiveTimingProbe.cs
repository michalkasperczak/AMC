using System.Diagnostics;
using System.Text.Json;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Windows.Services;

internal static class RadioLiveTimingProbe
{
    internal static void Run(string url, int seconds, string outputPath,
        bool recognize = true, bool captureTransitions = false)
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
        var pending = new List<(int Number, RadioAudioSampleAnchor Anchor,
            int BytesPerSecond, long EventElapsedMs)>();
        var titleCount = 0;
        var started = false;
        radio.PlaybackStarted += (_, _) => { started = true; Log("started"); };
        radio.PlaybackFailed += (_, eventArgs) =>
            Log("failed", new { eventArgs.Message });
        radio.NowPlayingChanged += (_, eventArgs) =>
        {
            radio.TryGetRecentCapturedAudio(TimeSpan.FromSeconds(3), out var sample);
            if (captureTransitions && !string.IsNullOrWhiteSpace(eventArgs.StreamTitle))
            {
                lock (gate)
                {
                    titleCount++;
                    if (titleCount > 1 && sample?.Anchor is { } anchor)
                        pending.Add((titleCount - 1, anchor,
                            sample.Format.AverageBytesPerSecond, watch.ElapsedMilliseconds));
                }
            }
            Log("title", new
            {
                eventArgs.StreamTitle,
                endByte = sample?.Anchor?.EndByte,
                segmentId = sample?.Anchor?.SegmentId,
                bytesPerSecond = sample?.Format.AverageBytesPerSecond
            });
        };
        Log("opening", new { source.Host, hls = url.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase) });
        radio.Play(new MediaItem
        {
            Id = "radio-live-timing-probe",
            Title = "Pomiar radia",
            Source = url,
            Kind = MediaItemKind.Station
        }, TimeSpan.Zero, 0, 1d);

        void FlushPending()
        {
            (int Number, RadioAudioSampleAnchor Anchor, int BytesPerSecond,
                long EventElapsedMs)[] waiting;
            lock (gate) waiting = pending.ToArray();
            foreach (var item in waiting)
            {
                var first = item.Anchor.EndByte - 12L * item.BytesPerSecond;
                var last = item.Anchor.EndByte + 12L * item.BytesPerSecond;
                if (!radio.TryGetClipBounds(item.Anchor, out var bounds) || bounds is null
                    || first < bounds.FirstByte)
                {
                    Log("clipUnavailable", new { item.Number, item.EventElapsedMs });
                }
                else if (last > bounds.LastByte)
                {
                    continue;
                }
                else
                {
                    var folder = outputPath + ".clips";
                    Directory.CreateDirectory(folder);
                    var clipPath = Path.Combine(folder, $"transition-{item.Number}.wav");
                    try
                    {
                        radio.SaveClip(item.Anchor, first, last, clipPath);
                        Log("clipSaved", new
                        {
                            item.Number, item.EventElapsedMs, clipPath,
                            firstByte = first, lastByte = last,
                            sampleEndByte = item.Anchor.EndByte,
                            item.BytesPerSecond
                        });
                    }
                    catch (Exception exception)
                    {
                        Log("clipFailed", new { item.Number, exception.Message });
                    }
                }
                lock (gate) pending.Remove(item);
            }
        }

        Task? recognition = null;
        for (var tick = 1; tick <= seconds; tick++)
        {
            Thread.Sleep(1000);
            if (captureTransitions) FlushPending();
            if (tick % 5 == 0)
            {
                var available = radio.TryGetRecentCapturedAudio(
                    TimeSpan.FromSeconds(12), out var sample);
                if (tick == 60 && sample?.Anchor is not null)
                {
                    var capturedSeconds = sample.Anchor.EndByte
                        / (double)sample.Format.AverageBytesPerSecond;
                    radio.Seek(TimeSpan.FromSeconds(Math.Max(0, capturedSeconds - 20)));
                    Log("seek", new { capturedSeconds, positionSeconds = radio.Position.TotalSeconds });
                }
                if (tick == 100 && sample?.Anchor is not null)
                {
                    radio.JumpToLive();
                    Log("jumpToLive", new { positionSeconds = radio.Position.TotalSeconds });
                }
                var playbackAvailable = radio.TryGetRecentPlaybackAudio(
                    TimeSpan.FromSeconds(12), out var playbackSample);
                Log("sample", new
                {
                    available,
                    endByte = sample?.Anchor?.EndByte,
                    startByte = sample?.Anchor?.StartByte,
                    segmentId = sample?.Anchor?.SegmentId,
                    bytesPerSecond = sample?.Format.AverageBytesPerSecond,
                    bufferedSeconds = radio.BufferedDuration.TotalSeconds,
                    playbackAvailable,
                    playbackStartByte = playbackSample?.Anchor?.StartByte,
                    playbackEndByte = playbackSample?.Anchor?.EndByte,
                    behindLiveSeconds = radio.BehindLive.TotalSeconds
                });
                if (recognize && tick >= 30 && recognition is null
                    && available && sample is not null)
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
        if (captureTransitions)
        {
            for (var grace = 0; grace < 20; grace++)
            {
                lock (gate) { if (pending.Count == 0) break; }
                Thread.Sleep(1000);
                FlushPending();
            }
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

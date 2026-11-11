using System.Text.Json;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Windows.Services;

internal static class RadioPrivateRecordingProbe
{
    internal static void Run(string url, string outputDirectory)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var source)
            || source.Scheme != Uri.UriSchemeHttps)
            throw new ArgumentException("Probe requires an HTTPS stream.");

        Directory.CreateDirectory(outputDirectory);
        using var started = new ManualResetEventSlim(false);
        using var radio = new RadioMediaOutput(1, audible: false);
        string? failure = null;
        radio.PlaybackStarted += (_, _) => started.Set();
        radio.PlaybackFailed += (_, eventArgs) =>
        {
            failure = eventArgs.Message;
            started.Set();
        };
        radio.Play(new MediaItem
        {
            Id = "radio-private-recording-probe",
            Title = "Pomiar prywatnego toru",
            Source = url,
            Kind = MediaItemKind.Station
        }, TimeSpan.Zero, 0, 1d);
        if (!started.Wait(TimeSpan.FromSeconds(45)) || failure is not null
            || radio.LoadedItemId is null)
            throw new InvalidOperationException(failure ?? "Private stream did not start.");

        var control = new RadioRecordingControl();
        var path = radio.StartRecording(outputDirectory, RadioRecordingFormat.Wav,
            preferredBaseName: "private-recognition-sample");
        try
        {
            control.Attach(radio, outputDirectory, RadioRecordingFormat.Wav,
                0, path);
            Thread.Sleep(TimeSpan.FromSeconds(15));
            var available = control.TryGetRecentAudio(
                TimeSpan.FromSeconds(12), out var privateSample);
            var captured = radio.TryGetRecentCapturedAudio(
                TimeSpan.FromSeconds(12), out var directSample);
            if (!available || !captured || privateSample?.Anchor is not { } privateAnchor
                || directSample?.Anchor is not { } directAnchor)
                throw new InvalidOperationException("Private sample was unavailable.");

            var report = new
            {
                source.Host,
                recordingActiveWhenSampled = control.IsReady,
                privateOrigin = privateAnchor.Origin.ToString(),
                directOrigin = directAnchor.Origin.ToString(),
                sameBuffer = privateAnchor.BufferId == directAnchor.BufferId,
                sameSegment = privateAnchor.SegmentId == directAnchor.SegmentId,
                sameRange = privateAnchor.StartByte == directAnchor.StartByte
                    && privateAnchor.EndByte == directAnchor.EndByte,
                privateDurationSeconds = privateSample.Duration.TotalSeconds,
                directDurationSeconds = directSample.Duration.TotalSeconds,
                privateBytes = privateSample.Audio.Length,
                directBytes = directSample.Audio.Length
            };
            File.WriteAllText(Path.Combine(outputDirectory, "private-sample-report.json"),
                JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            if (!report.recordingActiveWhenSampled || report.privateOrigin != "Capture"
                || !report.sameBuffer || !report.sameSegment || !report.sameRange)
                throw new InvalidOperationException("Private sample anchor differs from captured PCM.");
        }
        finally
        {
            radio.StopRecording();
            radio.Stop();
        }
        Console.WriteLine(Path.Combine(outputDirectory, "private-sample-report.json"));
    }
}

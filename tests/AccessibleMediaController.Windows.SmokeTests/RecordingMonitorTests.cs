using System.Reflection;
using AccessibleMediaController.Windows.Services;
using NAudio.Wave;

internal static class RecordingMonitorTests
{
    internal static void Run()
    {
        CapturedTimelineExcludesPauseAndPreStart();
        RenamingUnpublishedRecordingKeepsAudio();
    }

    private static void CapturedTimelineExcludesPauseAndPreStart()
    {
        var format = new WaveFormat(8000, 16, 1);
        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var type = typeof(RadioMediaOutput).GetNestedType(
            "RadioTimeshiftWaveProvider", BindingFlags.NonPublic)!;
        using var buffer = (IDisposable)Activator.CreateInstance(type, flags, null,
            new object?[] { format, 1, 5L * format.AverageBytesPerSecond,
                (Action<Exception>)(e => throw e), null, true }, null)!;
        var write = type.GetMethod("Write", flags)!;
        var captured = type.GetProperty("CapturedPosition", flags)!;
        var earliest = type.GetProperty("EarliestBufferedPosition", flags)!;
        var seek = type.GetMethod("Seek", flags)!;
        var jumpToLive = type.GetMethod("JumpToLive", flags)!;
        var playback = type.GetProperty("PlaybackPosition", flags)!;
        var data = Enumerable.Repeat((byte)0x5A, format.AverageBytesPerSecond).ToArray();
        void Feed(int seconds)
        {
            for (var i = 0; i < seconds; i++)
                write.Invoke(buffer, new object[] { data, 0, data.Length });
        }
        Feed(1);
        Check(((TimeSpan)captured.GetValue(buffer)!).TotalSeconds == 0,
            "Odsłuch przejął dźwięk sprzed rozpoczęcia zapisu.");
        var recorder = new FakeRecorder();
        type.GetField("_recording", flags)!.SetValue(buffer, recorder);
        Feed(2);
        recorder.IsPaused = true;
        Feed(2);
        Check(((TimeSpan)captured.GetValue(buffer)!).TotalSeconds == 2,
            "Pauza nagrania nie została pominięta na osi odsłuchu.");
        recorder.IsPaused = false;
        Feed(1);
        var received = new byte[data.Length];
        ((IWaveProvider)buffer).Read(received, 0, received.Length);
        Check(recorder.AcceptedBytes == 3 * data.Length
              && received.SequenceEqual(data),
            "Odczyt odsłuchu zmienił zapis albo oddał niewłaściwe próbki.");
        Feed(5);
        seek.Invoke(buffer, new object[] { TimeSpan.Zero });
        Check(((TimeSpan)earliest.GetValue(buffer)!).TotalSeconds == 3
              && ((TimeSpan)playback.GetValue(buffer)!).TotalSeconds == 3,
            "Po nadpisaniu pierścienia odsłuch wyszedł poza dostępny fragment.");
        jumpToLive.Invoke(buffer, null);
        var lag = ((TimeSpan)captured.GetValue(buffer)!).TotalSeconds
            - ((TimeSpan)playback.GetValue(buffer)!).TotalSeconds;
        Check(lag >= 0 && lag <= 0.26,
            "Powrót na żywo przekroczył ochronny zapas 0,25 sekundy.");
    }

    private static void RenamingUnpublishedRecordingKeepsAudio()
    {
        var root = Path.Combine(Path.GetTempPath(), $"amc-monitor-rename-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var original = Path.Combine(root, "pierwsza.mp3");
            var renamed = Path.Combine(root, "audycja.mp3");
            var format = new WaveFormat(44_100, 16, 1);
            using var recorder = RadioMp3Recorder.Start(original, format);
            var pcm = new byte[format.AverageBytesPerSecond * 2];
            for (var frame = 0; frame < format.SampleRate * 2; frame++)
            {
                var sample = (short)(Math.Sin(2 * Math.PI * 440 * frame / format.SampleRate) * 2000);
                BitConverter.TryWriteBytes(pcm.AsSpan(frame * 2, 2), sample);
            }
            recorder.Write(pcm, 0, pcm.Length);
            recorder.RenameFinalPath(renamed);
            var published = recorder.Stop();
            Check(published == renamed && File.Exists(renamed) && !File.Exists(original),
                "Zmiana nazwy w trakcie zapisu nie opublikowała gotowego pliku.");
            using var reader = new MediaFoundationReader(renamed);
            Check(reader.TotalTime > TimeSpan.FromSeconds(1),
                "Po zmianie nazwy gotowy MP3 nie zawiera pełnego dźwięku.");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private sealed class FakeRecorder : IRadioRecorder
    {
        public string FinalPath => "";
        public bool CanPause => true;
        public bool IsPaused { get; set; }
        public TimeSpan RecordedDuration => TimeSpan.Zero;
        public int AcceptedBytes { get; private set; }
        public void Write(byte[] buffer, int offset, int count)
        {
            if (!IsPaused) AcceptedBytes += count;
        }
        public void Pause() => IsPaused = true;
        public void Resume() => IsPaused = false;
        public string Stop() => "";
        public void Abort() { }
        public void Dispose() { }
    }
}

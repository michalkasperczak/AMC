using System.Reflection;
using AccessibleMediaController.Windows.Services;
using NAudio.Wave;

internal static class RadioAudioSampleAnchorTests
{
    internal static void Run()
    {
        var format = new WaveFormat(8000, 16, 1);
        using var first = new BufferFixture(format);
        first.WriteSeconds(5);
        first.WriteSeconds(1);
        first.ReadSeconds(3);

        var playback = first.Snapshot("TryGetRecentPlaybackAudio", 3);
        var capture = first.Snapshot("TryGetRecentCapturedAudio", 3);
        var bytesPerSecond = format.AverageBytesPerSecond;
        if (playback.Anchor is not { Origin: RadioAudioSampleOrigin.Playback } played
            || capture.Anchor is not { Origin: RadioAudioSampleOrigin.Capture } received)
            throw new Exception("Próbka PCM nie wskazuje odrębnej osi odsłuchu i odbioru.");
        if (played.BufferId == Guid.Empty || played.BufferId != received.BufferId
            || played.StartByte != bytesPerSecond
            || played.EndByte != 4L * bytesPerSecond
            || received.StartByte != 3L * bytesPerSecond
            || received.EndByte != 6L * bytesPerSecond
            || playback.Audio.Length != played.EndByte - played.StartByte
            || capture.Audio.Length != received.EndByte - received.StartByte)
            throw new Exception("Kotwica próbki nie odpowiada skopiowanym bajtom pierścienia.");

        var clipPath = Path.Combine(Path.GetTempPath(),
            "amc-radio-clip-test-" + Guid.NewGuid().ToString("N") + ".wav");
        try
        {
            if (!first.TryBounds(played, out var bounds) || bounds is null
                || bounds.FirstByte != bytesPerSecond || bounds.LastByte != 6L * bytesPerSecond)
                throw new Exception("Zakres eksportu nie odpowiada dostępnemu buforowi.");
            var playbackPosition = first.PlaybackPosition;
            first.SaveClip(played, played.StartByte, played.EndByte, clipPath);
            if (first.PlaybackPosition != playbackPosition)
                throw new Exception("Eksport przesunął kursor odsłuchu radia.");
            using (var reader = new WaveFileReader(clipPath))
            {
                if (reader.Length != 3L * bytesPerSecond)
                    throw new Exception("Eksport WAV ma inną długość niż wybrany zakres.");
            }
            first.WriteSeconds(5);
            if (first.TryBounds(played, out _))
                throw new Exception("Nadpisana próbka nadal jest dostępna do eksportu.");
        }
        finally
        {
            if (File.Exists(clipPath)) File.Delete(clipPath);
        }

        using var second = new BufferFixture(format, capacitySeconds: 10);
        second.WriteSeconds(6);
        second.ReadSeconds(4);
        var beforeReconnect = second.Snapshot("TryGetRecentCapturedAudio", 3);
        second.MarkDiscontinuity();
        second.WriteSeconds(1);
        if (second.TrySnapshot("TryGetRecentCapturedAudio", 3) is not null)
            throw new Exception("Próbka rozpoznania po reconnect przekroczyła granicę segmentu.");
        var oldPlayback = second.Snapshot("TryGetRecentPlaybackAudio", 3);
        if (oldPlayback.Anchor?.SegmentId != beforeReconnect.Anchor?.SegmentId)
            throw new Exception("Odsłuch starej części bufora stracił właściwy segment.");
        second.WriteSeconds(2);
        var afterReconnect = second.Snapshot("TryGetRecentCapturedAudio", 3);
        if (afterReconnect.Anchor is not { } after
            || after.BufferId != beforeReconnect.Anchor?.BufferId
            || after.BufferId == received.BufferId
            || after.SegmentId == beforeReconnect.Anchor?.SegmentId
            || after.StartByte != 6L * bytesPerSecond
            || after.EndByte != 9L * bytesPerSecond)
            throw new Exception("Nowy segment nie ma poprawnej granicy i tożsamości.");
    }

    private sealed class BufferFixture : IDisposable
    {
        private static readonly BindingFlags Flags = BindingFlags.Instance
            | BindingFlags.Public | BindingFlags.NonPublic;
        private readonly object _buffer;
        private readonly Type _type;
        private readonly WaveFormat _format;

        internal BufferFixture(WaveFormat format, int capacitySeconds = 5)
        {
            _format = format;
            _type = typeof(RadioMediaOutput).GetNestedType(
                "RadioTimeshiftWaveProvider", BindingFlags.NonPublic)!;
            _buffer = Activator.CreateInstance(_type, Flags, null,
                new object?[] { format, 1, (long)capacitySeconds * format.AverageBytesPerSecond,
                    (Action<Exception>)(exception => throw exception), null }, null)!;
        }

        internal void WriteSeconds(int seconds)
        {
            var audio = new byte[seconds * _format.AverageBytesPerSecond];
            _type.GetMethod("Write", Flags)!.Invoke(_buffer,
                new object[] { audio, 0, audio.Length });
        }

        internal void ReadSeconds(int seconds)
        {
            var audio = new byte[seconds * _format.AverageBytesPerSecond];
            ((IWaveProvider)_buffer).Read(audio, 0, audio.Length);
        }

        internal void MarkDiscontinuity() =>
            _type.GetMethod("MarkDiscontinuity", Flags)!.Invoke(_buffer, null);

        internal TimeSpan PlaybackPosition => (TimeSpan)_type.GetProperty(
            "PlaybackPosition", Flags)!.GetValue(_buffer)!;

        internal bool TryBounds(RadioAudioSampleAnchor anchor, out RadioAudioClipBounds? bounds)
        {
            var arguments = new object?[] { anchor, null };
            var found = _type.GetMethod("TryGetClipBounds", Flags)!.Invoke(_buffer, arguments) is true;
            bounds = arguments[1] as RadioAudioClipBounds;
            return found;
        }

        internal void SaveClip(RadioAudioSampleAnchor anchor, long first, long last, string path) =>
            _type.GetMethod("SaveClip", Flags)!.Invoke(_buffer,
                new object[] { anchor, first, last, path });

        internal RadioAudioSnapshot Snapshot(string method, int seconds) =>
            TrySnapshot(method, seconds)
            ?? throw new Exception($"Nie udało się skopiować próbki: {method}.");

        internal RadioAudioSnapshot? TrySnapshot(string method, int seconds)
        {
            var arguments = new object?[] { TimeSpan.FromSeconds(seconds), null };
            return _type.GetMethod(method, Flags)!.Invoke(_buffer, arguments) is true
                ? arguments[1] as RadioAudioSnapshot : null;
        }

        public void Dispose() => ((IDisposable)_buffer).Dispose();
    }
}

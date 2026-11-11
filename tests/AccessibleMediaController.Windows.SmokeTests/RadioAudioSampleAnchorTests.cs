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

        using var second = new BufferFixture(format);
        second.WriteSeconds(3);
        var next = second.Snapshot("TryGetRecentCapturedAudio", 3);
        if (next.Anchor?.BufferId == received.BufferId)
            throw new Exception("Nowe uruchomienie toru odziedziczyło poprzednią epokę PCM.");
    }

    private sealed class BufferFixture : IDisposable
    {
        private static readonly BindingFlags Flags = BindingFlags.Instance
            | BindingFlags.Public | BindingFlags.NonPublic;
        private readonly object _buffer;
        private readonly Type _type;
        private readonly WaveFormat _format;

        internal BufferFixture(WaveFormat format)
        {
            _format = format;
            _type = typeof(RadioMediaOutput).GetNestedType(
                "RadioTimeshiftWaveProvider", BindingFlags.NonPublic)!;
            _buffer = Activator.CreateInstance(_type, Flags, null,
                new object?[] { format, 1, 5L * format.AverageBytesPerSecond,
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

        internal RadioAudioSnapshot Snapshot(string method, int seconds)
        {
            var arguments = new object?[] { TimeSpan.FromSeconds(seconds), null };
            if (_type.GetMethod(method, Flags)!.Invoke(_buffer, arguments) is not true
                || arguments[1] is not RadioAudioSnapshot snapshot)
                throw new Exception($"Nie udało się skopiować próbki: {method}.");
            return snapshot;
        }

        public void Dispose() => ((IDisposable)_buffer).Dispose();
    }
}

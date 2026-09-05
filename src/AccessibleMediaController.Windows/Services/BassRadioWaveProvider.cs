using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using NAudio.Wave;

namespace AccessibleMediaController.Windows.Services;

/// <summary>
/// Optional BASS-backed decoder for direct internet radio streams. BASS is kept
/// behind this small adapter so the session core and the remaining radio paths
/// continue to work when the proprietary native library is absent or rejects a
/// particular stream.
/// </summary>
internal sealed class BassRadioWaveProvider : IWaveProvider, IRadioStreamTitleSource, IDisposable
{
    private const uint BassSampleFloat = 0x100;
    private const uint BassStreamBlock = 0x100000;
    private const uint BassStreamDecode = 0x200000;
    private const uint BassUnicode = 0x80000000;
    private const uint BassActiveStopped = 0;
    // BASS_ATTRIB_FREQ is 1. BASS_ATTRIB_BITRATE is 12; confusing the two
    // stores a typical 44100 Hz sample rate as an impossible 44100 kb/s.
    internal const uint BitrateAttribute = 12;
    private const uint BassTagOgg = 2;
    private const uint BassTagMeta = 5;
    private const int BassErrorEnded = 45;
    private static readonly TimeSpan MetadataPollInterval = TimeSpan.FromMilliseconds(400);
    private static readonly object InitializationGate = new();
    private static bool _initializationAttempted;
    private static bool _available;
    private readonly object _nativeGate = new();
    private uint _stream;
    private CancellationTokenRegistration _lifetimeCancellation;
    private long _lastMetadataPollTimestamp;
    private string? _streamTitle;

    private BassRadioWaveProvider(
        uint stream,
        WaveFormat waveFormat,
        CancellationToken cancellationToken)
    {
        _stream = stream;
        WaveFormat = waveFormat;
        _lifetimeCancellation = cancellationToken.Register(
            static state => ((BassRadioWaveProvider)state!).CancelStream(),
            this);
    }

    public WaveFormat WaveFormat { get; }

    public string? StreamTitle
    {
        get
        {
            lock (_nativeGate) return _streamTitle;
        }
    }

    public event EventHandler<RadioStreamTitleChangedEventArgs>? StreamTitleChanged;

    public int? BitrateKbps
    {
        get
        {
            lock (_nativeGate)
            {
                var stream = _stream;
                if (stream == 0
                    || !BassNative.ChannelGetAttribute(stream, BitrateAttribute, out var bitrate)
                    || !float.IsFinite(bitrate)
                    || RadioAudioMetadataRules.NormalizeBitrateKbps((int)Math.Round(bitrate)) is not int normalized)
                {
                    return null;
                }
                return normalized;
            }
        }
    }

    public static bool IsAvailable
    {
        get
        {
            EnsureInitialized();
            return _available;
        }
    }

    public static async Task<BassRadioWaveProvider> OpenAsync(
        string source,
        CancellationToken cancellationToken)
    {
        if (!IsAvailable) throw new NotSupportedException("Opcjonalny dekoder BASS nie jest dostępny.");
        cancellationToken.ThrowIfCancellationRequested();

        var requestHandle = GCHandle.Alloc(new object());
        var requestPointer = GCHandle.ToIntPtr(requestHandle);
        uint stream = 0;
        try
        {
            var cancellationSignal = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            using var cancellationRegistration = cancellationToken.Register(() =>
            {
                BassNative.StreamCancel(requestPointer);
                cancellationSignal.TrySetResult();
            });
            var nativeOpenTask = Task.Run(
                () => BassNative.StreamCreateUrl(
                    source,
                    BassSampleFloat | BassStreamBlock | BassStreamDecode | BassUnicode,
                    requestPointer));
            var completed = await Task.WhenAny(
                    nativeOpenTask,
                    cancellationSignal.Task)
                .ConfigureAwait(false);
            if (!ReferenceEquals(completed, nativeOpenTask))
            {
                // Some servers leave the native URL open call waiting even
                // after BASS_StreamCancel. Return control to AMC immediately,
                // but keep the request token alive until BASS really exits and
                // then release any late stream in the background.
                var cleanupHandle = requestHandle;
                requestHandle = default;
                _ = CleanupCanceledOpenAsync(nativeOpenTask, cleanupHandle);
                cancellationToken.ThrowIfCancellationRequested();
            }
            stream = await nativeOpenTask.ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();
            if (stream == 0)
            {
                throw new IOException($"BASS nie otworzył strumienia; kod {BassNative.ErrorGetCode()}.");
            }
            if (!BassNative.ChannelGetInfo(stream, out var info)
                || info.Frequency is < 8_000 or > 384_000
                || info.Channels is < 1 or > 32)
            {
                throw new InvalidDataException("BASS nie podał prawidłowego formatu dźwięku.");
            }

            var result = new BassRadioWaveProvider(
                stream,
                WaveFormat.CreateIeeeFloatWaveFormat((int)info.Frequency, (int)info.Channels),
                cancellationToken);
            stream = 0;
            if (cancellationToken.IsCancellationRequested)
            {
                result.Dispose();
                cancellationToken.ThrowIfCancellationRequested();
            }
            return result;
        }
        finally
        {
            if (stream != 0) BassNative.StreamFree(stream);
            if (requestHandle.IsAllocated) requestHandle.Free();
        }
    }

    private static async Task CleanupCanceledOpenAsync(Task<uint> nativeOpenTask, GCHandle requestHandle)
    {
        try
        {
            var lateStream = await nativeOpenTask.ConfigureAwait(false);
            if (lateStream != 0) BassNative.StreamFree(lateStream);
        }
        catch
        {
            // The foreground operation has already reported cancellation.
            // Cleanup must never surface a second, unobserved exception.
        }
        finally
        {
            if (requestHandle.IsAllocated) requestHandle.Free();
        }
    }

    public int Read(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (offset > buffer.Length - count) throw new ArgumentException("Zakres bufora jest nieprawidłowy.");
        if (count == 0) return 0;

        var stream = Volatile.Read(ref _stream);
        if (stream == 0) return 0;
        while (true)
        {
            stream = Volatile.Read(ref _stream);
            if (stream == 0) return 0;
            var read = BassNative.ChannelGetData(stream, buffer, offset, count);
            if (read > 0)
            {
                RefreshStreamTitle();
                return read;
            }
            if (read < 0)
            {
                var error = BassNative.ErrorGetCode();
                if (error == BassErrorEnded || Volatile.Read(ref _stream) == 0) return 0;
                throw new IOException($"BASS przerwał dekodowanie; kod {error}.");
            }
            if (BassNative.ChannelIsActive(stream) == BassActiveStopped
                || Volatile.Read(ref _stream) == 0)
            {
                return 0;
            }
            Thread.Sleep(20);
        }
    }

    public void Dispose()
    {
        CancelStream();
        _lifetimeCancellation.Dispose();
    }

    private void CancelStream()
    {
        lock (_nativeGate)
        {
            var stream = _stream;
            _stream = 0;
            if (stream != 0) BassNative.StreamFree(stream);
        }
    }

    private void RefreshStreamTitle()
    {
        var now = Stopwatch.GetTimestamp();
        if (_lastMetadataPollTimestamp != 0
            && Stopwatch.GetElapsedTime(_lastMetadataPollTimestamp, now) < MetadataPollInterval)
        {
            return;
        }
        _lastMetadataPollTimestamp = now;

        string? title;
        lock (_nativeGate)
        {
            if (_stream == 0) return;
            var metadata = ReadSingleTag(BassNative.ChannelGetTags(_stream, BassTagMeta));
            title = RadioStreamTitleMetadata.ParseIcyText(metadata);
            if (title is null)
            {
                title = RadioStreamTitleMetadata.ParseOggTags(
                    ReadTagList(BassNative.ChannelGetTags(_stream, BassTagOgg)));
            }
            if (string.Equals(_streamTitle, title, StringComparison.Ordinal)) return;
            _streamTitle = title;
        }
        StreamTitleChanged?.Invoke(this, new RadioStreamTitleChangedEventArgs(title));
    }

    private static string? ReadSingleTag(IntPtr pointer)
    {
        if (pointer == IntPtr.Zero) return null;
        const int maximumBytes = 8 * 1024;
        var bytes = new List<byte>(256);
        for (var index = 0; index < maximumBytes; index++)
        {
            var value = Marshal.ReadByte(pointer, index);
            if (value == 0) break;
            bytes.Add(value);
        }
        return bytes.Count == 0 ? null : RadioStreamTitleMetadata.Decode(CollectionsMarshal.AsSpan(bytes));
    }

    private static IReadOnlyList<string> ReadTagList(IntPtr pointer)
    {
        if (pointer == IntPtr.Zero) return [];
        const int maximumBytes = 32 * 1024;
        var result = new List<string>();
        var current = new List<byte>(128);
        for (var index = 0; index < maximumBytes; index++)
        {
            var value = Marshal.ReadByte(pointer, index);
            if (value != 0)
            {
                current.Add(value);
                continue;
            }
            if (current.Count == 0) break;
            result.Add(RadioStreamTitleMetadata.Decode(CollectionsMarshal.AsSpan(current)));
            current.Clear();
        }
        return result;
    }

    private static void EnsureInitialized()
    {
        if (_initializationAttempted) return;
        lock (InitializationGate)
        {
            if (_initializationAttempted) return;
            try
            {
                // Device 0 with NOSPEAKER initializes decoding and networking
                // without taking ownership of an audio output device.
                _available = BassNative.Init(0, 48_000, 0x1000);
                if (_available)
                {
                    BassNative.SetConfig(11, 10_000); // connection timeout, ms
                    BassNative.SetConfig(37, 8_000);  // stalled read timeout, ms
                }
            }
            catch (Exception exception) when (exception is DllNotFoundException
                or BadImageFormatException
                or EntryPointNotFoundException)
            {
                _available = false;
            }
            finally
            {
                _initializationAttempted = true;
            }
        }
    }

    private static class BassNative
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct ChannelInfo
        {
            internal uint Frequency;
            internal uint Channels;
            internal uint Flags;
            internal uint ChannelType;
            internal uint OriginalResolution;
            internal IntPtr Plugin;
            internal IntPtr Sample;
            internal IntPtr FileName;
        }

        [DllImport("bass.dll", EntryPoint = "BASS_Init", CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool InitNative(
            int device,
            uint frequency,
            uint flags,
            IntPtr window,
            IntPtr classId);

        [DllImport("bass.dll", EntryPoint = "BASS_SetConfig", CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetConfigNative(uint option, uint value);

        [DllImport("bass.dll", EntryPoint = "BASS_StreamCreateURL", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        private static extern uint StreamCreateUrlNative(
            string url,
            uint offset,
            uint flags,
            IntPtr downloadCallback,
            IntPtr user);

        [DllImport("bass.dll", EntryPoint = "BASS_StreamCancel", CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool StreamCancelNative(IntPtr user);

        [DllImport("bass.dll", EntryPoint = "BASS_ChannelGetInfo", CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ChannelGetInfo(uint handle, out ChannelInfo info);

        [DllImport("bass.dll", EntryPoint = "BASS_ChannelGetData", CallingConvention = CallingConvention.StdCall)]
        private static extern int ChannelGetDataNative(uint handle, IntPtr buffer, uint length);

        [DllImport("bass.dll", EntryPoint = "BASS_ChannelGetAttribute", CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ChannelGetAttribute(uint handle, uint attribute, out float value);

        [DllImport("bass.dll", EntryPoint = "BASS_ChannelGetTags", CallingConvention = CallingConvention.StdCall)]
        internal static extern IntPtr ChannelGetTags(uint handle, uint tags);

        [DllImport("bass.dll", EntryPoint = "BASS_ChannelIsActive", CallingConvention = CallingConvention.StdCall)]
        internal static extern uint ChannelIsActive(uint handle);

        [DllImport("bass.dll", EntryPoint = "BASS_StreamFree", CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool StreamFree(uint handle);

        [DllImport("bass.dll", EntryPoint = "BASS_ErrorGetCode", CallingConvention = CallingConvention.StdCall)]
        internal static extern int ErrorGetCode();

        internal static bool Init(int device, uint frequency, uint flags) =>
            InitNative(device, frequency, flags, IntPtr.Zero, IntPtr.Zero);

        internal static bool SetConfig(uint option, uint value) => SetConfigNative(option, value);

        internal static uint StreamCreateUrl(string url, uint flags, IntPtr user) =>
            StreamCreateUrlNative(url, 0, flags, IntPtr.Zero, user);

        internal static bool StreamCancel(IntPtr user) => StreamCancelNative(user);

        internal static unsafe int ChannelGetData(
            uint handle,
            byte[] buffer,
            int offset,
            int count)
        {
            fixed (byte* pointer = &buffer[offset])
            {
                return ChannelGetDataNative(handle, (IntPtr)pointer, (uint)count);
            }
        }
    }
}

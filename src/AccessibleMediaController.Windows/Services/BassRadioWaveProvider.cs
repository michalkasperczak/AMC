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
internal sealed class BassRadioWaveProvider : IWaveProvider, IDisposable
{
    private const uint BassSampleFloat = 0x100;
    private const uint BassStreamBlock = 0x100000;
    private const uint BassStreamDecode = 0x200000;
    private const uint BassUnicode = 0x80000000;
    private const uint BassActiveStopped = 0;
    private const uint BassAttribBitrate = 1;
    private const int BassErrorEnded = 45;
    private static readonly object InitializationGate = new();
    private static bool _initializationAttempted;
    private static bool _available;
    private uint _stream;
    private CancellationTokenRegistration _lifetimeCancellation;

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

    public int? BitrateKbps
    {
        get
        {
            var stream = Volatile.Read(ref _stream);
            if (stream == 0
                || !BassNative.ChannelGetAttribute(stream, BassAttribBitrate, out var bitrate)
                || !float.IsFinite(bitrate)
                || bitrate <= 0)
            {
                return null;
            }
            return Math.Max(1, (int)Math.Round(bitrate));
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
            using var cancellationRegistration = cancellationToken.Register(
                static state => BassNative.StreamCancel((IntPtr)state!),
                requestPointer);
            stream = await Task.Run(
                    () => BassNative.StreamCreateUrl(
                        source,
                        BassSampleFloat | BassStreamBlock | BassStreamDecode | BassUnicode,
                        requestPointer))
                .ConfigureAwait(false);

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
            var read = BassNative.ChannelGetData(stream, buffer, offset, count);
            if (read > 0) return read;
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
        var stream = Interlocked.Exchange(ref _stream, 0);
        if (stream != 0) BassNative.StreamFree(stream);
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

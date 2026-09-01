using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace AccessibleMediaController.Windows.Services;

internal sealed record TrackRecognitionResult(
    bool Success,
    string Title = "",
    string Artist = "",
    string Album = "",
    string ReleaseDate = "",
    string? ProviderUri = null,
    string? Error = null);

internal interface ITrackRecognitionService
{
    Task<TrackRecognitionResult> RecognizeAsync(
        RadioAudioSnapshot snapshot,
        CancellationToken cancellationToken);
}

/// <summary>
/// Optional, replaceable music-recognition adapter. Only an acoustic
/// fingerprint is sent to Shazam; no station URL and no recorded audio leave
/// AMC. The fingerprint implementation is a C# port of the MIT-licensed
/// ShazamIO signature algorithm. See licenses/ShazamIO-MIT.txt.
/// </summary>
internal sealed class ShazamTrackRecognitionService : ITrackRecognitionService
{
    private const int TargetSampleRate = 16_000;
    private const int SampleSeconds = 12;
    private const string DataUriPrefix = "data:audio/vnd.shazam.sig;base64,";
    private const string Endpoint =
        "https://amp.shazam.com/discovery/v5/en/US/android/-/tag/{0}/{1}";
    private static readonly HttpClient HttpClient = CreateHttpClient();
    private static readonly SemaphoreSlim RecognitionGate = new(1, 1);

    public async Task<TrackRecognitionResult> RecognizeAsync(
        RadioAudioSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        if (!await RecognitionGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return new TrackRecognitionResult(false, Error: "Rozpoznawanie już trwa");
        }

        try
        {
            // Conversion and fingerprinting are CPU-intensive. Keeping them off
            // the UI thread prevents automatic recognition from interrupting
            // keyboard navigation or the audible radio monitor.
            var preparationStarted = Stopwatch.GetTimestamp();
            var prepared = await Task.Run(
                () => PrepareSignature(snapshot, cancellationToken),
                cancellationToken).ConfigureAwait(false);
            DiagnosticLog.Info(
                "recognition",
                $"Przygotowanie podpisu akustycznego: {Stopwatch.GetElapsedTime(preparationStarted).TotalMilliseconds:F0} ms.");
            if (prepared.Error is not null)
            {
                return new TrackRecognitionResult(
                    false,
                    Error: prepared.Error);
            }

            var signature = prepared.Signature!;
            var uri = DataUriPrefix + Convert.ToBase64String(signature.Data);
            var requestUri = string.Format(
                CultureInfo.InvariantCulture,
                Endpoint,
                Guid.NewGuid().ToString().ToUpperInvariant(),
                Guid.NewGuid().ToString().ToUpperInvariant());
            var payload = JsonSerializer.Serialize(new
            {
                signature = new
                {
                    uri,
                    samplems = signature.SampleMilliseconds
                },
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                timezone = "Europe/Warsaw",
                context = new { },
                geolocation = new { }
            });
            using var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
            request.Headers.Accept.ParseAdd("application/json");
            request.Headers.AcceptLanguage.ParseAdd("pl-PL,pl;q=0.9,en;q=0.8");

            using var response = await HttpClient.SendAsync(request, cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new TrackRecognitionResult(
                    false,
                    Error: $"Usługa rozpoznawania zwróciła błąd {(int)response.StatusCode}");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            if (!document.RootElement.TryGetProperty("track", out var track))
            {
                return new TrackRecognitionResult(false, Error: "Nie rozpoznano utworu");
            }

            var title = GetString(track, "title");
            var artist = GetString(track, "subtitle");
            var providerUri = GetString(track, "url");
            var album = string.Empty;
            var release = string.Empty;
            if (track.TryGetProperty("sections", out var sections)
                && sections.ValueKind == JsonValueKind.Array)
            {
                foreach (var section in sections.EnumerateArray())
                {
                    if (!string.Equals(GetString(section, "type"), "SONG", StringComparison.OrdinalIgnoreCase)
                        || !section.TryGetProperty("metadata", out var metadata)
                        || metadata.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    foreach (var entry in metadata.EnumerateArray())
                    {
                        var label = GetString(entry, "title");
                        var value = GetString(entry, "text");
                        if (string.Equals(label, "Album", StringComparison.OrdinalIgnoreCase)) album = value;
                        else if (label is "Released" or "Release" or "Year") release = value;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(artist))
                return new TrackRecognitionResult(false, Error: "Nie rozpoznano utworu");
            return new TrackRecognitionResult(
                true,
                title.Trim(),
                artist.Trim(),
                album.Trim(),
                release.Trim(),
                string.IsNullOrWhiteSpace(providerUri) ? null : providerUri.Trim());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new TrackRecognitionResult(false, Error: "Anulowano rozpoznawanie");
        }
        catch (Exception exception) when (exception is HttpRequestException
            or IOException
            or InvalidDataException
            or JsonException
            or ArgumentException)
        {
            DiagnosticLog.Warning("recognition", $"Rozpoznawanie nie powiodło się: {exception.Message}");
            return new TrackRecognitionResult(
                false,
                Error: "Nie udało się rozpoznać utworu. Sprawdź połączenie i spróbuj ponownie");
        }
        finally
        {
            RecognitionGate.Release();
        }
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(12)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Shazam/3.25.0-230206.1 CFNetwork/1220.1 Darwin/20.3.0");
        return client;
    }

    private static string GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    internal static byte[] CreateFingerprintForTests(short[] samples) =>
        SignatureGenerator.Create(samples).Data;

    private static PreparedSignature PrepareSignature(
        RadioAudioSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var samples = ConvertToMono16Khz(snapshot);
        if (samples.Length < TargetSampleRate * 3)
        {
            return new PreparedSignature(
                null,
                "Za mało dźwięku w buforze. Poczekaj kilka sekund i spróbuj ponownie");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var signature = SignatureGenerator.Create(samples);
        if (signature.PeakCount == 0)
        {
            return new PreparedSignature(
                null,
                "Nie znaleziono charakterystycznych fragmentów dźwięku");
        }

        return new PreparedSignature(signature, null);
    }

    private static short[] ConvertToMono16Khz(RadioAudioSnapshot snapshot)
    {
        using var stream = new RawSourceWaveStream(new MemoryStream(snapshot.Audio, writable: false), snapshot.Format);
        ISampleProvider provider = stream.ToSampleProvider();
        if (provider.WaveFormat.Channels == 2)
        {
            provider = new StereoToMonoSampleProvider(provider)
            {
                LeftVolume = 0.5f,
                RightVolume = 0.5f
            };
        }
        else if (provider.WaveFormat.Channels > 2)
        {
            provider = new AverageToMonoSampleProvider(provider);
        }

        if (provider.WaveFormat.SampleRate != TargetSampleRate)
            provider = new WdlResamplingSampleProvider(provider, TargetSampleRate);

        var requested = TargetSampleRate * SampleSeconds;
        var floats = new float[requested];
        var count = 0;
        while (count < floats.Length)
        {
            var read = provider.Read(floats, count, floats.Length - count);
            if (read <= 0) break;
            count += read;
        }

        var result = new short[count];
        for (var index = 0; index < count; index++)
        {
            var sample = Math.Clamp(floats[index], -1f, 1f);
            result[index] = (short)Math.Round(sample * (sample < 0 ? 32768f : 32767f));
        }
        return result;
    }

    private sealed class AverageToMonoSampleProvider(ISampleProvider source) : ISampleProvider
    {
        private readonly float[] _sourceBuffer = new float[4096 * source.WaveFormat.Channels];

        public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(
            source.WaveFormat.SampleRate,
            1);

        public int Read(float[] buffer, int offset, int count)
        {
            var channels = source.WaveFormat.Channels;
            var requested = Math.Min(count, _sourceBuffer.Length / channels);
            var read = source.Read(_sourceBuffer, 0, requested * channels);
            var frames = read / channels;
            for (var frame = 0; frame < frames; frame++)
            {
                var sum = 0f;
                for (var channel = 0; channel < channels; channel++)
                    sum += _sourceBuffer[frame * channels + channel];
                buffer[offset + frame] = sum / channels;
            }
            return frames;
        }
    }

    private sealed record EncodedSignature(byte[] Data, int SampleMilliseconds, int PeakCount);

    private sealed record PreparedSignature(EncodedSignature? Signature, string? Error);

    private enum FrequencyBand
    {
        Hz250To520,
        Hz520To1450,
        Hz1450To3500
    }

    private sealed record FrequencyPeak(
        int FftPassNumber,
        ushort PeakMagnitude,
        ushort CorrectedPeakFrequencyBin);

    private sealed class SignatureGenerator
    {
        private const int FftSize = 2048;
        private const int FftBins = 1025;
        private const int SpectrumFrames = 256;
        private static readonly double[] Hann = Enumerable.Range(1, FftSize)
            .Select(index => 0.5 * (1 - Math.Cos(2 * Math.PI * index / 2049d)))
            .ToArray();

        private readonly short[] _sampleRing = new short[FftSize];
        private readonly double[] _fftReal = new double[FftSize];
        private readonly double[] _fftImaginary = new double[FftSize];
        private readonly double[][] _fft = CreateFrames();
        private readonly double[][] _spread = CreateFrames();
        private readonly Dictionary<FrequencyBand, List<FrequencyPeak>> _peaks = [];
        private int _samplePosition;
        private int _fftPosition;
        private int _spreadPosition;
        private int _spreadWritten;

        public static EncodedSignature Create(short[] samples)
        {
            var generator = new SignatureGenerator();
            var usable = samples.Length - samples.Length % 128;
            for (var offset = 0; offset < usable; offset += 128)
                generator.ProcessChunk(samples.AsSpan(offset, 128));
            return generator.Encode(usable);
        }

        private static double[][] CreateFrames() =>
            Enumerable.Range(0, SpectrumFrames).Select(_ => new double[FftBins]).ToArray();

        private void ProcessChunk(ReadOnlySpan<short> chunk)
        {
            for (var index = 0; index < chunk.Length; index++)
            {
                _sampleRing[_samplePosition] = chunk[index];
                _samplePosition = (_samplePosition + 1) % FftSize;
            }

            var real = _fftReal;
            var imaginary = _fftImaginary;
            Array.Clear(imaginary);
            for (var index = 0; index < FftSize; index++)
            {
                var reversed = ReverseElevenBits(index);
                var sample = _sampleRing[(_samplePosition + reversed) % FftSize];
                real[index] = sample * Hann[reversed];
            }
            for (var length = 2; length <= FftSize; length <<= 1)
            {
                var angle = -2 * Math.PI / length;
                var twiddleStepReal = Math.Cos(angle);
                var twiddleStepImaginary = Math.Sin(angle);
                for (var block = 0; block < FftSize; block += length)
                {
                    var twiddleReal = 1d;
                    var twiddleImaginary = 0d;
                    for (var index = 0; index < length / 2; index++)
                    {
                        var first = block + index;
                        var second = first + length / 2;
                        var valueReal = real[second] * twiddleReal
                            - imaginary[second] * twiddleImaginary;
                        var valueImaginary = real[second] * twiddleImaginary
                            + imaginary[second] * twiddleReal;
                        var firstReal = real[first];
                        var firstImaginary = imaginary[first];
                        real[first] = firstReal + valueReal;
                        imaginary[first] = firstImaginary + valueImaginary;
                        real[second] = firstReal - valueReal;
                        imaginary[second] = firstImaginary - valueImaginary;
                        var nextReal = twiddleReal * twiddleStepReal
                            - twiddleImaginary * twiddleStepImaginary;
                        twiddleImaginary = twiddleReal * twiddleStepImaginary
                            + twiddleImaginary * twiddleStepReal;
                        twiddleReal = nextReal;
                    }
                }
            }

            var spectrum = _fft[_fftPosition];
            for (var bin = 0; bin < FftBins; bin++)
            {
                var magnitude = (real[bin] * real[bin]
                    + imaginary[bin] * imaginary[bin]) / (1 << 17);
                spectrum[bin] = Math.Max(magnitude, 1e-10);
            }
            _fftPosition = (_fftPosition + 1) % SpectrumFrames;
            SpreadSpectrum(spectrum);
            if (_spreadWritten >= 46) RecognizePeaks();
        }

        private void SpreadSpectrum(double[] spectrum)
        {
            var spread = _spread[_spreadPosition];
            Array.Clear(spread);
            for (var bin = 0; bin < FftBins; bin++)
            {
                spread[bin] = Math.Max(
                    spectrum[bin],
                    Math.Max(
                        bin + 1 < FftBins ? spectrum[bin + 1] : 0,
                        bin + 2 < FftBins ? spectrum[bin + 2] : 0));
            }

            foreach (var offset in new[] { -1, -3, -6 })
            {
                var prior = _spread[Mod(_spreadPosition + offset, SpectrumFrames)];
                for (var bin = 0; bin < FftBins; bin++) prior[bin] = Math.Max(prior[bin], spread[bin]);
            }
            _spreadPosition = (_spreadPosition + 1) % SpectrumFrames;
            _spreadWritten++;
        }

        private void RecognizePeaks()
        {
            var fftM46 = _fft[Mod(_fftPosition - 46, SpectrumFrames)];
            var spreadM49 = _spread[Mod(_spreadPosition - 49, SpectrumFrames)];
            ReadOnlySpan<int> frequencyOffsets = [-10, -7, -4, -3, 1, 4, 7];
            ReadOnlySpan<int> timeOffsets = [-53, -45, 165, 172, 179, 186, 193, 200, 214, 221, 228, 235, 242, 249];

            for (var bin = 10; bin < 1015; bin++)
            {
                var value = fftM46[bin];
                if (value < 1f / 64 || value < spreadM49[bin - 1]) continue;
                var maximum = 0d;
                foreach (var offset in frequencyOffsets)
                    maximum = Math.Max(maximum, spreadM49[bin + offset]);
                if (value <= maximum) continue;
                foreach (var offset in timeOffsets)
                {
                    var frame = _spread[Mod(_spreadPosition + offset, SpectrumFrames)];
                    maximum = Math.Max(maximum, frame[bin - 1]);
                }
                if (value <= maximum) continue;

                var magnitude = LogMagnitude(value);
                var previous = LogMagnitude(fftM46[bin - 1]);
                var next = LogMagnitude(fftM46[bin + 1]);
                var denominator = magnitude * 2 - previous - next;
                if (denominator <= 0) continue;
                var correctedBin = bin * 64 + (next - previous) * 32 / denominator;
                var frequency = correctedBin * (TargetSampleRate / 2d / 1024d / 64d);
                var band = frequency switch
                {
                    > 250 and < 520 => FrequencyBand.Hz250To520,
                    > 520 and < 1450 => FrequencyBand.Hz520To1450,
                    > 1450 and < 3500 => FrequencyBand.Hz1450To3500,
                    _ => (FrequencyBand?)null
                };
                if (band is null) continue;
                if (!_peaks.TryGetValue(band.Value, out var peaks))
                {
                    peaks = [];
                    _peaks[band.Value] = peaks;
                }
                peaks.Add(new FrequencyPeak(
                    _spreadWritten - 46,
                    (ushort)Math.Clamp((int)magnitude, 0, ushort.MaxValue),
                    (ushort)Math.Clamp((int)correctedBin, 0, ushort.MaxValue)));
            }
        }

        private EncodedSignature Encode(int sampleCount)
        {
            using var content = new MemoryStream();
            var peakCount = 0;
            foreach (var band in _peaks.OrderBy(pair => pair.Key))
            {
                using var peakData = new MemoryStream();
                var fftPass = 0;
                foreach (var peak in band.Value)
                {
                    if (peak.FftPassNumber - fftPass >= 255)
                    {
                        peakData.WriteByte(0xff);
                        WriteUInt32(peakData, (uint)peak.FftPassNumber);
                        fftPass = peak.FftPassNumber;
                    }
                    peakData.WriteByte((byte)(peak.FftPassNumber - fftPass));
                    WriteUInt16(peakData, peak.PeakMagnitude);
                    WriteUInt16(peakData, peak.CorrectedPeakFrequencyBin);
                    fftPass = peak.FftPassNumber;
                    peakCount++;
                }
                var raw = peakData.ToArray();
                WriteUInt32(content, 0x60030040u + (uint)band.Key);
                WriteUInt32(content, (uint)raw.Length);
                content.Write(raw);
                while (content.Length % 4 != 0) content.WriteByte(0);
            }

            var contentBytes = content.ToArray();
            var sizeMinusHeader = (uint)(contentBytes.Length + 8);
            var header = new byte[48];
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0), 0xCAFE2580);
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(8), sizeMinusHeader);
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(12), 0x94119C00);
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(28), 3u << 27);
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(40), (uint)(sampleCount + TargetSampleRate * 0.24));
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(44), (15u << 19) + 0x40000);
            var tlv = new byte[8];
            BinaryPrimitives.WriteUInt32LittleEndian(tlv.AsSpan(0), 0x40000000);
            BinaryPrimitives.WriteUInt32LittleEndian(tlv.AsSpan(4), sizeMinusHeader);
            var crcInput = header.AsSpan(8).ToArray().Concat(tlv).Concat(contentBytes).ToArray();
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(4), Crc32(crcInput));
            var output = header.Concat(tlv).Concat(contentBytes).ToArray();
            return new EncodedSignature(output, sampleCount * 1000 / TargetSampleRate, peakCount);
        }

        private static double LogMagnitude(double value) =>
            Math.Log(Math.Max(1d / 64, value)) * 1477.3 + 6144;

        private static int Mod(int value, int modulus) => (value % modulus + modulus) % modulus;

        private static int ReverseElevenBits(int value)
        {
            var result = 0;
            for (var bit = 0; bit < 11; bit++)
            {
                result = (result << 1) | (value & 1);
                value >>= 1;
            }
            return result;
        }

        private static void WriteUInt16(Stream stream, ushort value)
        {
            Span<byte> bytes = stackalloc byte[2];
            BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
            stream.Write(bytes);
        }

        private static void WriteUInt32(Stream stream, uint value)
        {
            Span<byte> bytes = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
            stream.Write(bytes);
        }

        private static uint Crc32(ReadOnlySpan<byte> data)
        {
            var crc = 0xffffffffu;
            foreach (var value in data)
            {
                crc ^= value;
                for (var bit = 0; bit < 8; bit++)
                    crc = (crc >> 1) ^ (0xedb88320u & (uint)-(int)(crc & 1));
            }
            return ~crc;
        }
    }
}

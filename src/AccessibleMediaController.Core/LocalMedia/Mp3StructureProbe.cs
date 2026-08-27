namespace AccessibleMediaController.Core.LocalMedia;

public readonly record struct Mp3StructureProbeResult(
    long FileLength,
    long AudioStartOffset,
    bool HasConsecutiveFrames,
    int? SampleRateHz,
    int? BitrateKbps,
    string? Warning);

/// <summary>
/// Performs a bounded, read-only MP3 structure check. It reads at most the
/// ID3 header and a small window around the first audio frame, never scans the
/// complete recording and never allocates according to sizes declared by an
/// untrusted tag.
/// </summary>
public static class Mp3StructureProbe
{
    private const int DefaultScanBytes = 1_048_576;
    private const int Id3HeaderLength = 10;
    private static readonly int[] Mpeg1Layer1Bitrates = [0, 32, 64, 96, 128, 160, 192, 224, 256, 288, 320, 352, 384, 416, 448];
    private static readonly int[] Mpeg1Layer2Bitrates = [0, 32, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 384];
    private static readonly int[] Mpeg1Layer3Bitrates = [0, 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320];
    private static readonly int[] Mpeg2Layer1Bitrates = [0, 32, 48, 56, 64, 80, 96, 112, 128, 144, 160, 176, 192, 224, 256];
    private static readonly int[] Mpeg2Layer23Bitrates = [0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160];

    public static Mp3StructureProbeResult Probe(string path, int maximumScanBytes = DefaultScanBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (maximumScanBytes is < 4 or > 16 * 1_048_576)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumScanBytes));
        }

        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            4_096,
            FileOptions.RandomAccess);
        var fileLength = stream.Length;
        if (fileLength < 4)
        {
            return new Mp3StructureProbeResult(
                fileLength,
                0,
                false,
                null,
                null,
                "Plik jest zbyt krótki, aby zawierać ramkę MP3.");
        }

        Span<byte> id3Header = stackalloc byte[Id3HeaderLength];
        var headerRead = ReadUpTo(stream, id3Header);
        long audioStart = 0;
        string? warning = null;
        if (headerRead == Id3HeaderLength
            && id3Header[0] == (byte)'I'
            && id3Header[1] == (byte)'D'
            && id3Header[2] == (byte)'3')
        {
            if ((id3Header[6] | id3Header[7] | id3Header[8] | id3Header[9]) >= 0x80)
            {
                warning = "Nagłówek ID3 zawiera nieprawidłowy rozmiar.";
            }
            else
            {
                var tagPayloadLength = ((long)id3Header[6] << 21)
                    | ((long)id3Header[7] << 14)
                    | ((long)id3Header[8] << 7)
                    | id3Header[9];
                var footerLength = (id3Header[5] & 0x10) != 0 ? Id3HeaderLength : 0;
                audioStart = checked(Id3HeaderLength + tagPayloadLength + footerLength);
                if (audioStart >= fileLength)
                {
                    return new Mp3StructureProbeResult(
                        fileLength,
                        audioStart,
                        false,
                        null,
                        null,
                        "Znacznik ID3 wskazuje poza końcem pliku.");
                }
            }
        }

        stream.Position = Math.Clamp(audioStart, 0, fileLength);
        var scanLength = checked((int)Math.Min(maximumScanBytes, fileLength - stream.Position));
        var buffer = new byte[scanLength];
        var bytesRead = ReadUpTo(stream, buffer);
        for (var index = 0; index <= bytesRead - 4; index++)
        {
            if (!TryParseFrameHeader(buffer.AsSpan(index, 4), out var first)) continue;
            var nextIndex = index + first.FrameLength;
            if (nextIndex > bytesRead - 4) continue;
            if (!TryParseFrameHeader(buffer.AsSpan(nextIndex, 4), out var second)) continue;
            if (first.Version != second.Version
                || first.Layer != second.Layer
                || first.SampleRateHz != second.SampleRateHz)
            {
                continue;
            }

            return new Mp3StructureProbeResult(
                fileLength,
                audioStart + index,
                true,
                first.SampleRateHz,
                first.BitrateKbps,
                warning);
        }

        return new Mp3StructureProbeResult(
            fileLength,
            audioStart,
            false,
            null,
            null,
            warning ?? $"Nie znaleziono dwóch kolejnych ramek MP3 w pierwszych {bytesRead} bajtach danych audio.");
    }

    private static int ReadUpTo(Stream stream, Span<byte> buffer)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = stream.Read(buffer[total..]);
            if (read <= 0) break;
            total += read;
        }
        return total;
    }

    private static bool TryParseFrameHeader(ReadOnlySpan<byte> header, out Mp3FrameHeader frame)
    {
        frame = default;
        if (header.Length < 4
            || header[0] != 0xFF
            || (header[1] & 0xE0) != 0xE0)
        {
            return false;
        }

        var versionBits = (header[1] >> 3) & 0x03;
        var layerBits = (header[1] >> 1) & 0x03;
        var bitrateIndex = (header[2] >> 4) & 0x0F;
        var sampleRateIndex = (header[2] >> 2) & 0x03;
        var padding = (header[2] >> 1) & 0x01;
        if (versionBits == 1
            || layerBits == 0
            || bitrateIndex is 0 or 15
            || sampleRateIndex == 3)
        {
            return false;
        }

        var version = versionBits switch
        {
            3 => 1,
            2 => 2,
            0 => 25,
            _ => 0
        };
        var layer = 4 - layerBits;
        var sampleRateHz = SampleRates(version, sampleRateIndex);
        var bitrateKbps = Bitrate(version, layer, bitrateIndex);
        if (sampleRateHz <= 0 || bitrateKbps <= 0) return false;

        var frameLength = layer switch
        {
            1 => ((12 * bitrateKbps * 1_000 / sampleRateHz) + padding) * 4,
            3 when version != 1 => 72 * bitrateKbps * 1_000 / sampleRateHz + padding,
            _ => 144 * bitrateKbps * 1_000 / sampleRateHz + padding
        };
        if (frameLength < 24) return false;

        frame = new Mp3FrameHeader(version, layer, sampleRateHz, bitrateKbps, frameLength);
        return true;
    }

    private static int SampleRates(int version, int index) => version switch
    {
        1 => index switch { 0 => 44_100, 1 => 48_000, _ => 32_000 },
        2 => index switch { 0 => 22_050, 1 => 24_000, _ => 16_000 },
        25 => index switch { 0 => 11_025, 1 => 12_000, _ => 8_000 },
        _ => 0
    };

    private static int Bitrate(int version, int layer, int index)
    {
        var values = (version, layer) switch
        {
            (1, 1) => Mpeg1Layer1Bitrates,
            (1, 2) => Mpeg1Layer2Bitrates,
            (1, 3) => Mpeg1Layer3Bitrates,
            (_, 1) => Mpeg2Layer1Bitrates,
            _ => Mpeg2Layer23Bitrates
        };
        return values[index];
    }

    private readonly record struct Mp3FrameHeader(
        int Version,
        int Layer,
        int SampleRateHz,
        int BitrateKbps,
        int FrameLength);
}

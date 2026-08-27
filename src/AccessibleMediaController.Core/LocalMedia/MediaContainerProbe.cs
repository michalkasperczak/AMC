using System.Buffers.Binary;

namespace AccessibleMediaController.Core.LocalMedia;

public enum MediaContainerKind
{
    Unknown,
    MpegAudio,
    Wave,
    Aiff,
    Flac,
    OggVorbis,
    OggOpus,
    OggOther,
    Mp4,
    Asf,
    Matroska,
    Avi,
    AdtsAac,
    AdifAac
}

public readonly record struct MediaContainerProbeResult(
    long FileLength,
    long ContentOffset,
    MediaContainerKind Kind,
    bool HeaderRecognized,
    string? Warning);

/// <summary>
/// Performs a bounded, read-only identification of common media containers.
/// The probe never follows chunk sizes through the whole file and never
/// allocates a buffer from untrusted metadata. It is diagnostic and routing
/// input only; the actual decoder remains the authority on codec support.
/// </summary>
public static class MediaContainerProbe
{
    private const int DefaultProbeBytes = 65_536;
    private const int Id3HeaderLength = 10;
    private static readonly byte[] AsfHeaderGuid =
        [0x30, 0x26, 0xB2, 0x75, 0x8E, 0x66, 0xCF, 0x11, 0xA6, 0xD9, 0x00, 0xAA, 0x00, 0x62, 0xCE, 0x6C];

    public static MediaContainerProbeResult Probe(
        string path,
        int maximumProbeBytes = DefaultProbeBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (maximumProbeBytes is < 16 or > 1_048_576)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumProbeBytes));
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
            return new MediaContainerProbeResult(
                fileLength,
                0,
                MediaContainerKind.Unknown,
                false,
                "Plik jest zbyt krótki, aby zawierać nagłówek multimedialny.");
        }

        var contentOffset = ReadId3Offset(stream, fileLength, out var id3Warning);
        if (contentOffset >= fileLength)
        {
            return new MediaContainerProbeResult(
                fileLength,
                contentOffset,
                MediaContainerKind.Unknown,
                false,
                id3Warning ?? "Znacznik ID3 wskazuje poza końcem pliku.");
        }

        stream.Position = contentOffset;
        var bytesToRead = checked((int)Math.Min(maximumProbeBytes, fileLength - contentOffset));
        var buffer = new byte[bytesToRead];
        var bytesRead = ReadUpTo(stream, buffer);
        var data = buffer.AsSpan(0, bytesRead);
        var kind = Detect(data);
        var warning = CombineWarnings(
            id3Warning,
            ExtensionWarning(Path.GetExtension(path), kind),
            ValidateInitialStructure(data, fileLength - contentOffset, kind));
        return new MediaContainerProbeResult(
            fileLength,
            contentOffset,
            kind,
            kind != MediaContainerKind.Unknown,
            warning);
    }

    private static long ReadId3Offset(
        Stream stream,
        long fileLength,
        out string? warning)
    {
        warning = null;
        Span<byte> header = stackalloc byte[Id3HeaderLength];
        stream.Position = 0;
        var read = ReadUpTo(stream, header);
        if (read < Id3HeaderLength
            || header[0] != (byte)'I'
            || header[1] != (byte)'D'
            || header[2] != (byte)'3')
        {
            return 0;
        }

        var id3MajorVersion = header[3];
        if (id3MajorVersion is < 2 or > 4)
        {
            warning = $"Nagłówek ID3 ma nieobsługiwaną wersję {id3MajorVersion}.";
            return 0;
        }

        if ((header[6] | header[7] | header[8] | header[9]) >= 0x80)
        {
            warning = "Nagłówek ID3 zawiera nieprawidłowy rozmiar.";
            return 0;
        }

        var payloadLength = ((long)header[6] << 21)
            | ((long)header[7] << 14)
            | ((long)header[8] << 7)
            | header[9];
        var footerLength = id3MajorVersion == 4 && (header[5] & 0x10) != 0
            ? Id3HeaderLength
            : 0;
        var offset = Id3HeaderLength + payloadLength + footerLength;
        if (offset >= fileLength)
        {
            warning = "Znacznik ID3 wskazuje poza końcem pliku.";
        }
        return offset;
    }

    private static MediaContainerKind Detect(ReadOnlySpan<byte> data)
    {
        if (data.Length >= 12)
        {
            var signature = data[..4];
            var form = data.Slice(8, 4);
            if ((signature.SequenceEqual("RIFF"u8)
                    || signature.SequenceEqual("RF64"u8)
                    || signature.SequenceEqual("BW64"u8))
                && form.SequenceEqual("WAVE"u8))
            {
                return MediaContainerKind.Wave;
            }
            if (signature.SequenceEqual("RIFF"u8)
                && form.SequenceEqual("AVI "u8))
            {
                return MediaContainerKind.Avi;
            }
            if (signature.SequenceEqual("FORM"u8)
                && (form.SequenceEqual("AIFF"u8) || form.SequenceEqual("AIFC"u8)))
            {
                return MediaContainerKind.Aiff;
            }
        }

        if (data.StartsWith("fLaC"u8)) return MediaContainerKind.Flac;
        if (data.StartsWith("OggS"u8))
        {
            if (data.IndexOf("OpusHead"u8) >= 0) return MediaContainerKind.OggOpus;
            if (data.IndexOf("\x01vorbis"u8) >= 0) return MediaContainerKind.OggVorbis;
            return MediaContainerKind.OggOther;
        }
        if (data.Length >= AsfHeaderGuid.Length
            && data[..AsfHeaderGuid.Length].SequenceEqual(AsfHeaderGuid))
        {
            return MediaContainerKind.Asf;
        }
        if (data.Length >= 4
            && data[0] == 0x1A
            && data[1] == 0x45
            && data[2] == 0xDF
            && data[3] == 0xA3)
        {
            return MediaContainerKind.Matroska;
        }
        if (HasMp4FileTypeBox(data)) return MediaContainerKind.Mp4;
        if (data.StartsWith("ADIF"u8)) return MediaContainerKind.AdifAac;
        if (data.Length >= 2
            && data[0] == 0xFF
            && (data[1] & 0xF6) == 0xF0)
        {
            return MediaContainerKind.AdtsAac;
        }
        if (data.Length >= 4
            && data[0] == 0xFF
            && (data[1] & 0xE0) == 0xE0)
        {
            return MediaContainerKind.MpegAudio;
        }
        return MediaContainerKind.Unknown;
    }

    private static bool HasMp4FileTypeBox(ReadOnlySpan<byte> data)
    {
        var maximum = Math.Min(data.Length, 4_096);
        var offset = 0;
        while (offset <= maximum - 8)
        {
            var boxSize = ReadBigEndianUInt32(data.Slice(offset, 4));
            var boxType = data.Slice(offset + 4, 4);
            if (boxType.SequenceEqual("ftyp"u8)) return boxSize >= 8 && boxSize <= data.Length - offset;
            if (boxSize == 0 || boxSize < 8 || boxSize > maximum - offset) return false;
            offset = checked(offset + (int)boxSize);
        }
        return false;
    }

    private static uint ReadBigEndianUInt32(ReadOnlySpan<byte> bytes) =>
        ((uint)bytes[0] << 24)
        | ((uint)bytes[1] << 16)
        | ((uint)bytes[2] << 8)
        | bytes[3];

    private static string? ValidateInitialStructure(
        ReadOnlySpan<byte> data,
        long contentLength,
        MediaContainerKind kind)
    {
        switch (kind)
        {
            case MediaContainerKind.Wave when data.Length >= 12:
            case MediaContainerKind.Avi when data.Length >= 12:
            {
                var declaredSize = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(4, 4));
                if (declaredSize < 4)
                {
                    return "Kontener RIFF deklaruje nieprawidłowy rozmiar.";
                }
                if (!data[..4].SequenceEqual("RF64"u8)
                    && !data[..4].SequenceEqual("BW64"u8)
                    && declaredSize != uint.MaxValue
                    && (long)declaredSize + 8 > contentLength)
                {
                    return "Kontener RIFF deklaruje dane poza końcem pliku.";
                }
                break;
            }
            case MediaContainerKind.Aiff when data.Length >= 12:
            {
                var declaredSize = ReadBigEndianUInt32(data.Slice(4, 4));
                if (declaredSize < 4 || (long)declaredSize + 8 > contentLength)
                {
                    return "Kontener AIFF deklaruje dane poza końcem pliku.";
                }
                break;
            }
            case MediaContainerKind.Flac:
            {
                if (data.Length < 8) return "Nagłówek metadanych FLAC jest ucięty.";
                var metadataLength = (data[5] << 16) | (data[6] << 8) | data[7];
                if (8L + metadataLength > contentLength)
                {
                    return "Blok metadanych FLAC wykracza poza koniec pliku.";
                }
                break;
            }
            case MediaContainerKind.OggVorbis:
            case MediaContainerKind.OggOpus:
            case MediaContainerKind.OggOther:
            {
                if (data.Length < 27) return "Pierwsza strona OGG jest ucięta.";
                if (data[4] != 0) return "Kontener OGG ma nieobsługiwaną wersję.";
                var segmentCount = data[26];
                if (data.Length < 27 + segmentCount)
                {
                    return "Tablica segmentów pierwszej strony OGG jest ucięta.";
                }
                long bodyLength = 0;
                for (var index = 0; index < segmentCount; index++) bodyLength += data[27 + index];
                if (27L + segmentCount + bodyLength > contentLength)
                {
                    return "Pierwsza strona OGG wykracza poza koniec pliku.";
                }
                break;
            }
            case MediaContainerKind.Mp4 when data.Length >= 8:
            {
                var boxSize = ReadBigEndianUInt32(data[..4]);
                long resolvedSize = boxSize;
                if (boxSize == 1)
                {
                    if (data.Length < 16) return "Rozszerzony nagłówek MP4 jest ucięty.";
                    var largeSize = BinaryPrimitives.ReadUInt64BigEndian(data.Slice(8, 8));
                    if (largeSize > long.MaxValue) return "Kontener MP4 deklaruje zbyt duży blok.";
                    resolvedSize = (long)largeSize;
                }
                if (resolvedSize != 0 && (resolvedSize < 8 || resolvedSize > contentLength))
                {
                    return "Pierwszy blok MP4 ma nieprawidłowy rozmiar.";
                }
                break;
            }
            case MediaContainerKind.AdtsAac:
            {
                if (data.Length < 7) return "Nagłówek ramki AAC jest ucięty.";
                var frameLength = ((data[3] & 0x03) << 11) | (data[4] << 3) | (data[5] >> 5);
                if (frameLength < 7 || frameLength > contentLength)
                {
                    return "Ramka AAC deklaruje dane poza końcem pliku.";
                }
                break;
            }
        }
        return null;
    }

    private static string? CombineWarnings(params string?[] warnings)
    {
        var present = warnings.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
        return present.Length == 0 ? null : string.Join(" ", present);
    }

    private static string? ExtensionWarning(string extension, MediaContainerKind kind)
    {
        var expected = extension.ToLowerInvariant() switch
        {
            ".wav" or ".wave" or ".rf64" or ".bwf" => kind == MediaContainerKind.Wave,
            ".aif" or ".aiff" or ".aifc" => kind == MediaContainerKind.Aiff,
            ".flac" => kind == MediaContainerKind.Flac,
            ".ogg" or ".oga" => kind is MediaContainerKind.OggVorbis
                or MediaContainerKind.OggOpus
                or MediaContainerKind.OggOther,
            ".opus" => kind == MediaContainerKind.OggOpus,
            ".m4a" or ".m4v" or ".mov" or ".mp4"
                or ".3g2" or ".3gp" or ".3gp2" or ".3gpp" => kind == MediaContainerKind.Mp4,
            ".wma" or ".wmv" or ".asf" => kind == MediaContainerKind.Asf,
            ".mka" or ".mkv" or ".webm" => kind == MediaContainerKind.Matroska,
            ".aac" or ".adts" => kind is MediaContainerKind.AdtsAac or MediaContainerKind.AdifAac,
            ".avi" => kind == MediaContainerKind.Avi,
            ".mp2" or ".mp3" => kind == MediaContainerKind.MpegAudio,
            _ => true
        };
        if (expected) return null;
        return kind == MediaContainerKind.Unknown
            ? "Nie rozpoznano typowego nagłówka dla rozszerzenia pliku."
            : "Rozszerzenie pliku nie odpowiada rozpoznanemu kontenerowi multimedialnemu.";
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
}

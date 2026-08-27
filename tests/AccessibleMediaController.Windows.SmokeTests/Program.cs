using System.Buffers.Binary;
using AccessibleMediaController.Windows.Services;

const string FixtureBase64 = """
T2dnUwACAAAAAAAAAAC43PvDAAAAAHCxeIUBHgF2b3JiaXMAAAAAAUAfAAAAAAAAgFcAAAAAAACZAU9nZ1MAAAAAAAAAAAAAuNz7wwEAAABjSiSUCz////////////+1A3ZvcmJpcwwAAABMYXZmNjIuMy4xMDABAAAAHwAAAGVuY29kZXI9TGF2YzYyLjExLjEwMCBsaWJ2b3JiaXMBBXZvcmJpcxJCQ1YBAAABAAxSFCElGVNKYwiVUlIpBR1jUFtHHWPUOUYhZBBTiEkZpXtPKpVYSsgRUlgpRR1TTFNJlVKWKUUdYxRTSCFT1jFloXMUS4ZJCSVsTa50FkvomWOWMUYdY85aSp1j1jFFHWNSUkmhcxg6ZiVkFDpGxehifDA6laJCKL7H3lLpLYWKW4q91xpT6y2EGEtpwQhhc+211dxKasUYY4wxxsXiUyiC0JBVAAABAABABAFCQ1YBAAoAAMJQDEVRgNCQVQBABgCAABRFcRTHcRxHkiTLAkJDVgEAQAAAAgAAKI7hKJIjSZJkWZZlWZameZaouaov+64u667t6roOhIasBADIAAAYhiGH3knMkFOQSSYpVcw5CKH1DjnlFGTSUsaYYoxRzpBTDDEFMYbQKYUQ1E45pQwiCENInWTOIEs96OBi5zgQGrIiAIgCAACMQYwhxpBzDEoGIXKOScggRM45KZ2UTEoorbSWSQktldYi55yUTkompbQWUsuklNZCKwUAAAQ4AAAEWAiFhqwIAKIAABCDkFJIKcSUYk4xh5RSjinHkFLMOcWYcowx6CBUzDHIHIRIKcUYc0455iBkDCrmHIQMMgEAAAEOAAABFkKhISsCgDgBAIMkaZqlaaJoaZooeqaoqqIoqqrleabpmaaqeqKpqqaquq6pqq5seZ5peqaoqp4pqqqpqq5rqqrriqpqy6ar2rbpqrbsyrJuu7Ks256qyrapurJuqq5tu7Js664s27rkearqmabreqbpuqrr2rLqurLtmabriqor26bryrLryratyrKua6bpuqKr2q6purLtyq5tu7Ks+6br6rbqyrquyrLu27au+7KtC7vourauyq6uq7Ks67It67Zs20LJ81TVM03X9UzTdVXXtW3VdW1bM03XNV1XlkXVdWXVlXVddWVb90zTdU1XlWXTVWVZlWXddmVXl0XXtW1Vln1ddWVfl23d92VZ133TdXVblWXbV2VZ92Vd94VZt33dU1VbN11X103X1X1b131htm3fF11X11XZ1oVVlnXf1n1lmHWdMLqurqu27OuqLOu+ruvGMOu6MKy6bfyurQvDq+vGseu+rty+j2rbvvDqtjG8um4cu7Abv+37xrGpqm2brqvrpivrumzrvm/runGMrqvrqiz7uurKvm/ruvDrvi8Mo+vquirLurDasq/Lui4Mu64bw2rbwu7aunDMsi4Mt+8rx68LQ9W2heHVdaOr28ZvC8PSN3a+AACAAQcAgAATykChISsCgDgBAAYhCBVjECrGIIQQUgohpFQxBiFjDkrGHJQQSkkhlNIqxiBkjknIHJMQSmiplNBKKKWlUEpLoZTWUmotptRaDKG0FEpprZTSWmopttRSbBVjEDLnpGSOSSiltFZKaSlzTErGoKQOQiqlpNJKSa1lzknJoKPSOUippNJSSam1UEproZTWSkqxpdJKba3FGkppLaTSWkmptdRSba21WiPGIGSMQcmck1JKSamU0lrmnJQOOiqZg5JKKamVklKsmJPSQSglg4xKSaW1kkoroZTWSkqxhVJaa63VmFJLNZSSWkmpxVBKa621GlMrNYVQUgultBZKaa21VmtqLbZQQmuhpBZLKjG1FmNtrcUYSmmtpBJbKanFFluNrbVYU0s1lpJibK3V2EotOdZaa0ot1tJSjK21mFtMucVYaw0ltBZKaa2U0lpKrcXWWq2hlNZKKrGVklpsrdXYWow1lNJiKSm1kEpsrbVYW2w1ppZibLHVWFKLMcZYc0u11ZRai621WEsrNcYYa2415VIAAMCAAwBAgAlloNCQlQBAFAAAYAxjjEFoFHLMOSmNUs45JyVzDkIIKWXOQQghpc45CKW01DkHoZSUQikppRRbKCWl1losAACgwAEAIMAGTYnFAQoNWQkARAEAIMYoxRiExiClGIPQGKMUYxAqpRhzDkKlFGPOQcgYc85BKRljzkEnJYQQQimlhBBCKKWUAgAAChwAAAJs0JRYHKDQkBUBQBQAAGAMYgwxhiB0UjopEYRMSielkRJaCylllkqKJcbMWomtxNhICa2F1jJrJcbSYkatxFhiKgAA7MABAOzAQig0ZCUAkAcAQBijFGPOOWcQYsw5CCE0CDHmHIQQKsaccw5CCBVjzjkHIYTOOecghBBC55xzEEIIoYMQQgillNJBCCGEUkrpIIQQQimldBBCCKGUUgoAACpwAAAIsFFkc4KRoEJDVgIAeQAAgDFKOSclpUYpxiCkFFujFGMQUmqtYgxCSq3FWDEGIaXWYuwgpNRajLV2EFJqLcZaQ0qtxVhrziGl1mKsNdfUWoy15tx7ai3GWnPOuQAA3AUHALADG0U2JxgJKjRkJQCQBwBAIKQUY4w5h5RijDHnnENKMcaYc84pxhhzzjnnFGOMOeecc4wx55xzzjnGmHPOOeecc84556CDkDnnnHPQQeicc845CCF0zjnnHIQQCgAAKnAAAAiwUWRzgpGgQkNWAgDhAACAMZRSSimllFJKqKOUUkoppZRSAiGllFJKKaWUUkoppZRSSimllFJKKaWUUkoppZRSSimllFJKKaWUUkoppZRSSimllFJKKaWUUkoppZRSSimllFJKKaWUUkoppZRSSimllFJKKaWUUkoppZRSSimllFJKKaWUUkoppZRSSimVUkoppZRSSimllFJKKaUAIN8KBwD/BxtnWEk6KxwNLjRkJQAQDgAAGMMYhIw5JyWlhjEIpXROSkklNYxBKKVzElJKKYPQWmqlpNJSShmElGILIZWUWgqltFZrKam1lFIoKcUaS0qppdYy5ySkklpLrbaYOQelpNZaaq3FEEJKsbXWUmuxdVJSSa211lptLaSUWmstxtZibCWlllprqcXWWkyptRZbSy3G1mJLrcXYYosxxhoLAOBucACASLBxhpWks8LR4EJDVgIAIQEABDJKOeecgxBCCCFSijHnoIMQQgghREox5pyDEEIIIYSMMecghBBCCKGUkDHmHIQQQgghhFI65yCEUEoJpZRSSucchBBCCKWUUkoJIYQQQiillFJKKSGEEEoppZRSSiklhBBCKKWUUkoppYQQQiillFJKKaWUEEIopZRSSimllBJCCKGUUkoppZRSQgillFJKKaWUUkooIYRSSimllFJKCSWUUkoppZRSSikhlFJKKaWUUkoppQAAgAMHAIAAI+gko8oibDThwgMQAAAAAgACTACBAYKCUQgChBEIAAAAAAAIAPgAAEgKgIiIaOYMDhASFBYYGhweICIkAAAAAAAAAAAAAAAABE9nZ1MABMADAAAAAAAAuNz7wwIAAAA/BbY+BTgUEhQjipUZ81O9AoBfTIZAZUAKGamKVqd3pxvYDz80TdNaaw3cSDfSdV3XdV3XdV2VNVjDYY7oiI7o7wSSlpndjVcA8FQBAAAAhBRyaT6lAJaWmX0brwBAVQAAAICQQioDNJKWmd2NVwDwWQUAAABirIh35xcKhstQWc0rAPSdGQByAGSIhp6Gv12MkxDV2lKimaT3PfLd2wU=
""";

const long LiveStreamSampleOffset = 2_256_060_119_296;
var path = Path.Combine(Path.GetTempPath(), $"amc-offset-vorbis-{Guid.NewGuid():N}.ogg");

try
{
    var bytes = Convert.FromBase64String(FixtureBase64);
    AddGranuleOffset(bytes, LiveStreamSampleOffset);
    File.WriteAllBytes(path, bytes);

    using var reader = new NormalizedVorbisWaveReader(path);
    Assert(reader.HasNormalizedTimeline, "Nie rozpoznano osi czasu fragmentu transmisji.");
    Assert(
        Math.Abs(reader.SampleOrigin - LiveStreamSampleOffset) < reader.WaveFormat.SampleRate * 2L,
        $"Nie odjęto początkowego numeru próbki: {reader.SampleOrigin}.");
    Assert(reader.TotalTime > TimeSpan.Zero && reader.TotalTime < TimeSpan.FromSeconds(1),
        $"Nieprawidłowy czas fragmentu: {reader.TotalTime}.");

    reader.CurrentTime = TimeSpan.FromTicks(reader.TotalTime.Ticks / 2);
    var buffer = new byte[16_384];
    Assert(reader.Read(buffer, 0, buffer.Length) > 0, "Przewinięty fragment nie zwrócił dźwięku.");

    reader.Position = 0;
    long totalRead = 0;
    int read;
    while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
    {
        totalRead += read;
        Assert(totalRead <= reader.Length, "Czytnik przekroczył rzeczywisty koniec fragmentu.");
    }
    Assert(totalRead == reader.Length, "Czytnik nie zatrzymał się dokładnie na końcu fragmentu.");

    Console.WriteLine("OK: normalizacja osi czasu fragmentu OGG/Vorbis");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"BŁĄD: normalizacja osi czasu fragmentu OGG/Vorbis: {exception}");
    return 1;
}
finally
{
    if (File.Exists(path)) File.Delete(path);
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static void AddGranuleOffset(byte[] data, long sampleOffset)
{
    var pageOffset = 0;
    while (pageOffset <= data.Length - 27)
    {
        if (data[pageOffset] != (byte)'O'
            || data[pageOffset + 1] != (byte)'g'
            || data[pageOffset + 2] != (byte)'g'
            || data[pageOffset + 3] != (byte)'S')
        {
            throw new InvalidDataException("Nieprawidłowy nagłówek strony OGG w pliku testowym.");
        }

        var segmentCount = data[pageOffset + 26];
        var segmentTableEnd = checked(pageOffset + 27 + segmentCount);
        if (segmentTableEnd > data.Length) throw new InvalidDataException("Niepełna tablica segmentów OGG.");

        var bodyLength = 0;
        for (var index = 0; index < segmentCount; index++)
        {
            bodyLength += data[pageOffset + 27 + index];
        }
        var pageLength = checked(27 + segmentCount + bodyLength);
        if (pageOffset + pageLength > data.Length) throw new InvalidDataException("Niepełna strona OGG.");

        var granule = BinaryPrimitives.ReadInt64LittleEndian(data.AsSpan(pageOffset + 6, sizeof(long)));
        if (granule > 0)
        {
            BinaryPrimitives.WriteInt64LittleEndian(
                data.AsSpan(pageOffset + 6, sizeof(long)),
                checked(granule + sampleOffset));
            data.AsSpan(pageOffset + 22, sizeof(uint)).Clear();
            BinaryPrimitives.WriteUInt32LittleEndian(
                data.AsSpan(pageOffset + 22, sizeof(uint)),
                CalculateOggChecksum(data.AsSpan(pageOffset, pageLength)));
        }

        pageOffset += pageLength;
    }

    if (pageOffset != data.Length) throw new InvalidDataException("Dodatkowe dane za ostatnią stroną OGG.");
}

static uint CalculateOggChecksum(ReadOnlySpan<byte> page)
{
    const uint polynomial = 0x04C11DB7;
    uint checksum = 0;
    foreach (var value in page)
    {
        checksum ^= (uint)value << 24;
        for (var bit = 0; bit < 8; bit++)
        {
            checksum = (checksum & 0x80000000) != 0
                ? (checksum << 1) ^ polynomial
                : checksum << 1;
        }
    }
    return checksum;
}

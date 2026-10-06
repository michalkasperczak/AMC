using System.Globalization;
using System.Text.Json;
using AccessibleMediaController.LiteHost.Protocol;

namespace AccessibleMediaController.LiteHost.ProtocolTests;

/// <summary>
/// Krotka informacja uzupelniajaca (LEWA STRZALKA na liscie) skladana PRZEZ
/// HOSTA. Mierzy to, co na Linuksie da sie zmierzyc uczciwie: odczyt zadania
/// i zlozenie napisu PRAWDZIWYM <c>QuickMediaInformationFormatter</c> z Core --
/// tym samym, ktorego uzywa pelne AMC (<c>MainWindow.xaml.cs:5476-5482</c>).
///
/// Pomiar pliku i strumienia (NAudio, atrybuty chmury) zostaje w warstwie
/// silnika na Windows; tutaj wchodzi jako <see cref="LiteQuickInfoProbe"/>,
/// wiec zestaw nie udaje, ze czyta dzwiek w WSL.
/// </summary>
internal static class QuickInformationTests
{
    public static void Run()
    {
        RequestIsReadFromProtocolArguments();
        MissingSourceIsRequestError();
        LocalFileUsesExtensionAndRealFileSize();
        LocalFileEstimatesBitrateFromSizeAndDuration();
        VideoFileKeepsNoEstimatedBitrate();
        MeasuredDurationFillsTheMissingRowDuration();
        RowDurationWinsOverMeasuredDuration();
        RadioStationUsesStreamMetadataWithoutSizeOrContainer();
        PodcastUsesMediaLengthAndMimeCodec();
        UnknownPodcastMimeGivesNoCodec();
        EmptyProbeGivesHonestShortMessage();
        CloudPlaceholderIsAnnouncedNotDownloaded();
        Console.WriteLine("QuickInformationTests: OK");
    }

    private static readonly CultureInfo Polish = CultureInfo.GetCultureInfo("pl-PL");

    private static JsonElement Parse(string json) =>
        JsonDocument.Parse(json).RootElement.Clone();

    // ------------------------------------------------- odczyt zadania

    private static void RequestIsReadFromProtocolArguments()
    {
        var request = LiteQuickInformation.ReadRequest(Parse("""
            {"session":"files","itemId":"file:7","title":"Łąka","kind":"track",
             "source":"D:\\Muzyka\\Łąka.mp3","durationTicks":1234567890}
            """));
        Assert.True(request.Session == "files", "sesja czytana z zadania");
        // ID MUSI zostac NAPISEM: frontend liczy na tozsamosc wiersza.
        Assert.True(request.ItemId == "file:7", "itemId jako napis");
        Assert.True(request.Title == "Łąka", "polskie znaki bez uszkodzenia");
        Assert.True(request.Source == @"D:\Muzyka\Łąka.mp3", "zrodlo bez zmian");
        Assert.True(request.DurationTicks == 1234567890L, "czas jako long, nie int");
    }

    private static void MissingSourceIsRequestError()
    {
        try
        {
            LiteQuickInformation.ReadRequest(Parse("""{"session":"files","itemId":"x"}"""));
            throw new InvalidOperationException("ZALOZENIE NIESPELNIONE: brak zrodla musi dac blad tresci.");
        }
        catch (LiteRequestException exception)
        {
            Assert.True(
                exception.Message.Contains("source", StringComparison.Ordinal),
                "komunikat nazywa brakujacy argument");
        }
    }

    // ------------------------------------------------- pliki lokalne

    private static void LocalFileUsesExtensionAndRealFileSize()
    {
        var request = new LiteQuickInfoRequest(
            "files", "file:7", "Łąka", "track", @"D:\Muzyka\Łąka.flac", 0, null, null);
        var probe = new LiteQuickInfoProbe
        {
            Exists = true,
            SizeBytes = 5_242_880,
            SampleRateHz = 44_100,
            BitrateKbps = 320
        };
        var text = LiteQuickInformation.Build(request, probe, Polish);
        // Kolejnosc i jednostki naleza do formattera C#, nie do nas.
        Assert.True(text.StartsWith("FLAC,", StringComparison.Ordinal),
            "kontener z rozszerzenia, WIELKIMI literami: " + text);
        Assert.True(text.Contains("320 kb/s", StringComparison.Ordinal), text);
        Assert.True(text.Contains("44,1 kHz", StringComparison.Ordinal),
            "czestotliwosc po polsku, z przecinkiem: " + text);
        Assert.True(text.Contains("5 MB", StringComparison.Ordinal), text);
        // Nazwa wiersza jest JUZ przeczytana przez czytnik przy nawigacji.
        Assert.True(!text.Contains("Łąka", StringComparison.Ordinal),
            "komunikat NIE powtarza tytulu wiersza: " + text);
    }

    private static void LocalFileEstimatesBitrateFromSizeAndDuration()
    {
        // Port cs:5450-5459: brak bitrate + znany czas + plik NIE-wideo
        // => LocalAudioFileDiscovery.EstimateBitrateKbps(file.Length, duration).
        var duration = TimeSpan.FromMinutes(4);
        var request = new LiteQuickInfoRequest(
            "files", "file:8", "Bez bitrate", "track", @"D:\a.mp3",
            duration.Ticks, null, null);
        var probe = new LiteQuickInfoProbe { Exists = true, SizeBytes = 5_760_000 };
        var text = LiteQuickInformation.Build(request, probe, Polish);
        // 5 760 000 B * 8 / 240 s / 1000 = 192 kb/s
        Assert.True(text.Contains("192 kb/s", StringComparison.Ordinal),
            "bitrate OSZACOWANY tym samym kodem Core: " + text);
        Assert.True(text.Contains("4:00", StringComparison.Ordinal), text);
    }

    private static void VideoFileKeepsNoEstimatedBitrate()
    {
        // cs:5452 -- ``!LocalAudioFileDiscovery.IsVideoFile`` jest czescia
        // warunku. Dla wideo oszacowanie z rozmiaru bylo by falszywe.
        var request = new LiteQuickInfoRequest(
            "files", "file:9", "Film", "video", @"D:\film.mp4",
            TimeSpan.FromMinutes(4).Ticks, null, null);
        var probe = new LiteQuickInfoProbe { Exists = true, SizeBytes = 5_760_000 };
        var text = LiteQuickInformation.Build(request, probe, Polish);
        Assert.True(!text.Contains("kb/s", StringComparison.Ordinal),
            "dla wideo NIE oszacowujemy bitrate z rozmiaru: " + text);
        Assert.True(text.Contains("MP4", StringComparison.Ordinal), text);
    }

    private static void MeasuredDurationFillsTheMissingRowDuration()
    {
        // cs:5504-5508 -- gdy wiersz nie zna czasu, uzupelnia go POMIAR
        // silnika. Bez tego plik z Biblioteki bez ``duration_ticks`` nie
        // dostalby ani czasu, ani oszacowanego bitrate.
        var request = new LiteQuickInfoRequest(
            "files", "file:10", "Bez czasu", "track", @"D:\b.mp3", 0, null, null);
        var probe = new LiteQuickInfoProbe
        {
            Exists = true,
            SizeBytes = 5_760_000,
            DurationTicks = TimeSpan.FromMinutes(4).Ticks
        };
        var text = LiteQuickInformation.Build(request, probe, Polish);
        Assert.True(text.Contains("4:00", StringComparison.Ordinal),
            "czas ZMIERZONY przez silnik: " + text);
        Assert.True(text.Contains("192 kb/s", StringComparison.Ordinal),
            "zmierzony czas pozwala oszacowac bitrate: " + text);
    }

    private static void RowDurationWinsOverMeasuredDuration()
    {
        // Oryginal uzupelnia czas TYLKO gdy go nie ma (``item.Duration <=
        // TimeSpan.Zero``, cs:5504). Znanego czasu wiersza nie nadpisuje.
        var request = new LiteQuickInfoRequest(
            "files", "file:11", "Ze czasem", "track", @"D:\c.mp3",
            TimeSpan.FromMinutes(3).Ticks, null, null);
        var probe = new LiteQuickInfoProbe
        {
            Exists = true,
            DurationTicks = TimeSpan.FromMinutes(9).Ticks
        };
        var text = LiteQuickInformation.Build(request, probe, Polish);
        Assert.True(text.Contains("3:00", StringComparison.Ordinal), text);
        Assert.True(!text.Contains("9:00", StringComparison.Ordinal),
            "pomiar NIE nadpisuje znanego czasu wiersza: " + text);
    }

    // ------------------------------------------------- radio

    private static void RadioStationUsesStreamMetadataWithoutSizeOrContainer()
    {
        var request = new LiteQuickInfoRequest(
            "radio", "st1", "Radio Nowy Świat", "station",
            "https://stream.example.invalid/live", 0, null, null);
        var probe = new LiteQuickInfoProbe
        {
            BitrateKbps = 128,
            SampleRateHz = 48_000,
            Codec = "mp3"
        };
        var text = LiteQuickInformation.Build(request, probe, Polish);
        Assert.True(text.StartsWith("MP3,", StringComparison.Ordinal),
            "kodek strumienia WIELKIMI literami: " + text);
        Assert.True(text.Contains("128 kb/s", StringComparison.Ordinal), text);
        Assert.True(text.Contains("48 kHz", StringComparison.Ordinal), text);
        // Stacja nie ma rozmiaru pliku ani dlugosci -- zera byly by zmysleniem.
        Assert.True(!text.Contains(" B", StringComparison.Ordinal)
            && !text.Contains("KB", StringComparison.Ordinal)
            && !text.Contains("MB", StringComparison.Ordinal),
            "stacja bez rozmiaru pliku: " + text);
        Assert.True(!text.Contains("0:00", StringComparison.Ordinal),
            "stacja bez zerowego czasu: " + text);
    }

    // ------------------------------------------------- podcasty (kontrakt)

    private static void PodcastUsesMediaLengthAndMimeCodec()
    {
        // Lista Podcastow w porcie wx NIE ISTNIEJE. Ten przypadek utrwala
        // KONTRAKT z cs:5468-5471 (MediaLength jako rozmiar) i cs:5413-5423
        // (FormatPodcastCodec), zeby jej przyszly port szedl TA SAMA droga.
        var request = new LiteQuickInfoRequest(
            "podcasts", "ep1", "Odcinek 12", "episode",
            "https://feed.example.invalid/ep12.mp3",
            TimeSpan.FromMinutes(30).Ticks, 24_117_248, "audio/mpeg");
        var text = LiteQuickInformation.Build(request, new LiteQuickInfoProbe(), Polish);
        Assert.True(text.StartsWith("MP3,", StringComparison.Ordinal), text);
        Assert.True(text.Contains("23 MB", StringComparison.Ordinal),
            "rozmiar z MediaLength kanalu: " + text);
        Assert.True(text.Contains("30:00", StringComparison.Ordinal), text);
        // Bitrate oszacowany z MediaLength i czasu (cs:5405-5411).
        Assert.True(text.Contains("107 kb/s", StringComparison.Ordinal),
            "bitrate z EstimatePodcastBitrateKbps: " + text);
    }

    private static void UnknownPodcastMimeGivesNoCodec()
    {
        var request = new LiteQuickInfoRequest(
            "podcasts", "ep2", "Odcinek 13", "episode",
            "https://feed.example.invalid/ep13.webm", 0, 1024, "audio/webm");
        var text = LiteQuickInformation.Build(request, new LiteQuickInfoProbe(), Polish);
        // Wzorzec oddaje tu ``null``; nie wolno zastapic go slowem "nieznany".
        Assert.True(!text.Contains("WEBM", StringComparison.OrdinalIgnoreCase)
            && !text.Contains("nieznan", StringComparison.OrdinalIgnoreCase),
            "nieznany MIME = BRAK kodeka: " + text);
        Assert.True(text.Contains("1 KB", StringComparison.Ordinal), text);
    }

    // ------------------------------------------------- brak danych

    private static void EmptyProbeGivesHonestShortMessage()
    {
        var request = new LiteQuickInfoRequest(
            "files", "file:0", "Niedostepny", "track", @"D:\brak.mp3", 0, null, null);
        // Plik niedostepny: sonda NIE istnieje, nie ma rozmiaru ani metadanych.
        // Oryginal mowi wtedy jedno zdanie, a nie "0 B, 0 kb/s".
        var text = LiteQuickInformation.Build(
            request, new LiteQuickInfoProbe { Exists = false }, Polish);
        Assert.True(text == "MP3", "zostaje sam znany kontener z rozszerzenia: " + text);

        var noExtension = new LiteQuickInfoRequest(
            "files", "file:00", "Bez rozszerzenia", "track", @"D:\plik", 0, null, null);
        var empty = LiteQuickInformation.Build(
            noExtension, new LiteQuickInfoProbe { Exists = false }, Polish);
        Assert.True(empty == "Brak zapisanych informacji uzupełniających",
            "brak danych = krotki uczciwy komunikat formattera: " + empty);
        Assert.True(!empty.Contains("0 B", StringComparison.Ordinal)
            && !empty.Contains("0 kb/s", StringComparison.Ordinal),
            "zadnych zmyslonych zer: " + empty);
    }

    private static void CloudPlaceholderIsAnnouncedNotDownloaded()
    {
        // cs:5441 -- ``CloudFileAvailability.MayRequireRemoteAccess`` czyta
        // SAME ATRYBUTY. Informacja o chmurze ma sie pojawic BEZ pobierania
        // pliku, wiec sonda oddaje tu gotowa decyzje silnika.
        var request = new LiteQuickInfoRequest(
            "files", "file:c", "W chmurze", "track", @"D:\OneDrive\a.mp3", 0, null, null);
        var probe = new LiteQuickInfoProbe { Exists = true, MayRequireCloudDownload = true };
        var text = LiteQuickInformation.Build(request, probe, Polish);
        Assert.True(text.Contains("plik w chmurze, pobierany przy odtwarzaniu", StringComparison.Ordinal),
            text);
        // Brak rozmiaru: placeholdera nie wolno dotknac, zeby go zmierzyc.
        Assert.True(!text.Contains("MB", StringComparison.Ordinal), text);
    }
}

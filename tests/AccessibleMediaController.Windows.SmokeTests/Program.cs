using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using AccessibleMediaController.Core.Commands;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Input;
using AccessibleMediaController.Core.LocalMedia;
using AccessibleMediaController.Core.Playback;
using AccessibleMediaController.Core.Podcasts;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Windows;
using AccessibleMediaController.Windows.Controls;
using AccessibleMediaController.Windows.Services;
using NAudio.Wave;
using NLayer.NAudioSupport;

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

    TestAccessiblePlaybackStatusStrip();
    TestEditableFieldReplacement();
    TestGlobalPrefixCapture();
    TestMenuAccessibility();
    TestSegmentedDateTimeDigitEntry();
    TestRadioScheduleAccessibility();
    TestStatePersistenceQueue();
    TestRadioRecognitionAnnouncementPolicy();
    TestRadioRecognitionSchedulingPolicy();
    TestRadioRecognitionSettingAccessibility();
    TestRadioRecognitionHistoryFilterAccessibility();
    TestPlaybackAudioSettingAccessibility();
    TestAudioOutputDeviceAccessibility();
    TestAudioOutputPauseRaceGuard();
    TestPodcastFeedClient();
    TestPodcastOpmlImportSelectionAccessibility();
    TestRadioPresetAccessibleLabels();
    TestRadioPresetKeyboardMap();
    TestMainWindowDigitShortcutRouting();
    TestPlayerAudioProcessingKeyboardMap();
    TestPlaylistPresentation();
    TestGuardDoesNotBlockPositionReads();
    TestCompleteOutputChainMonitor();
    TestInvalidSamplesAreSilenced();
    TestPlaybackAudioProcessors();
    TestGuardRejectsAbsurdDuration();
    TestManagedMp3Fallback();
    TestAudioClipExporter();
    TestAudioClipOriginalEditor();
    TestAudioClipExportAccessibility();
    TestFfmpegComponentSecurity();
    TestWaveMetadataAndDamagedContainers();
    TestLocalVideoAudioExtraction();
    TestLocalTransportStreamRecovery();
    TestRadioBrowserSearchMapping();
    TestRadioScheduleStationScope();
    TestRadioPlaylistImport();
    TestRadioPlaylistResolution();
    TestRadioAudioMetadataValidation();
    TestRadioStreamTitleMetadata();
    TestLegacyRadioContentTypes();
    TestRadioCompatibilityCandidates();
    TestRadioReconnectFormatCompatibility();
    TestRadioRecordingFolderFallback();
    TestRadioRecordingSplitControl();
    TestRadioRecordingSplitPipeline();
    TestRadioRecordingStagingPublication();
    TestScheduledRadioSegmentation();
    TestShazamFingerprint();
    TestRecognitionSearchLinks();
    TestRadioMp3Recording();
    TestLegacyIcyMp3Stream();
    TestLegacyIcyCancellation();
    TestBassCancellation();
    foreach (var mediaPath in args)
    {
        if (mediaPath.StartsWith("--bass-radio-url=", StringComparison.OrdinalIgnoreCase))
        {
            TestLiveBassRadio(mediaPath["--bass-radio-url=".Length..]);
        }
        else if (mediaPath.StartsWith("--system-radio-url=", StringComparison.OrdinalIgnoreCase))
        {
            TestLiveSystemRadio(mediaPath["--system-radio-url=".Length..]);
        }
        else if (mediaPath.StartsWith("--radio-url=", StringComparison.OrdinalIgnoreCase))
        {
            TestLiveLegacyRadio(mediaPath["--radio-url=".Length..]);
        }
        else if (mediaPath.StartsWith("--hls-radio-url=", StringComparison.OrdinalIgnoreCase))
        {
            TestLiveHlsRadio(mediaPath["--hls-radio-url=".Length..]);
        }
        else if (mediaPath.StartsWith("--radio-metadata-url=", StringComparison.OrdinalIgnoreCase))
        {
            TestLiveRadioMetadata(mediaPath["--radio-metadata-url=".Length..]);
        }
        else if (mediaPath.StartsWith("--ffmpeg-local-path=", StringComparison.OrdinalIgnoreCase))
        {
            TestFfmpegLocalFile(mediaPath["--ffmpeg-local-path=".Length..]);
        }
        else if (mediaPath.Equals("--install-ffmpeg", StringComparison.OrdinalIgnoreCase))
        {
            var result = FfmpegComponentManager.CheckAndUpdateAsync(
                    installAvailable: true,
                    cancellationToken: CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            Assert(result.Success && result.Status.Installed, result.Message);
            Console.WriteLine($"OK: {result.Message}");
        }
        else
        {
            TestFormatMetadata(mediaPath);
        }
    }
    return 0;
}

catch (Exception exception)
{
    Console.Error.WriteLine($"BŁĄD: testy dekoderów Windows: {exception}");
    return 1;
}
finally
{
    if (File.Exists(path)) File.Delete(path);
}

static void TestMenuAccessibility()
{
    Exception? failure = null;
    var thread = new Thread(() =>
    {
        try
        {
            var mainMenu = new Menu();
            var topLevel = new MenuItem { Header = "_Odtwarzanie" };
            var command = new MenuItem
            {
                Header = "Wycisz lub przywróć dźwięk _bieżącej sesji",
                InputGestureText = "Ctrl+M"
            };
            AutomationProperties.SetName(command, "Wycisz lub przywróć dźwięk bieżącej sesji, Ctrl+M");
            topLevel.Items.Add(command);
            var sessionsCommand = new MenuItem
            {
                Header = "_Lista sesji",
                InputGestureText = "Ctrl+Shift+S"
            };
            AutomationProperties.SetName(sessionsCommand, "Lista sesji");
            AutomationProperties.SetAcceleratorKey(sessionsCommand, "Ctrl+Shift+S");
            topLevel.Items.Add(sessionsCommand);
            var globalAudioMenu = new MenuItem
            {
                Header = "Cisza między utworami — ustawienie globalne: bez dodatkowej ciszy",
                InputGestureText = "prefiks C"
            };
            AutomationProperties.SetAcceleratorKey(globalAudioMenu, "po prefiksie C");
            topLevel.Items.Add(globalAudioMenu);
            mainMenu.Items.Add(topLevel);

            MenuAccessibility.NormalizeMainMenu(mainMenu);

            Assert(topLevel.Header?.ToString() == "_Odtwarzanie", "Usunięto literę dostępu z głównej kategorii menu.");
            Assert(AutomationProperties.GetName(topLevel) == "Odtwarzanie", "Nazwa kategorii menu zawiera znak mnemonika.");
            Assert(command.Header?.ToString() == "Wycisz lub przywróć dźwięk bieżącej sesji", "Nie usunięto mnemonika z polecenia menu.");
            Assert(AutomationProperties.GetName(command) == "Wycisz lub przywróć dźwięk bieżącej sesji", "Nazwa polecenia powtarza skrót.");
            Assert(command.InputGestureText == "Ctrl+M", "Usunięto widoczny skrót polecenia.");
            Assert(AutomationProperties.GetName(sessionsCommand) == "Lista sesji",
                "Menu sesji nie ma pojedynczej nazwy użytkowej.");
            Assert(AutomationProperties.GetAcceleratorKey(sessionsCommand) == "Ctrl+Shift+S",
                "Menu sesji nie podaje głównego skrótu Ctrl+Shift+S.");
            Assert(AutomationProperties.GetName(globalAudioMenu) == "Cisza między utworami — ustawienie globalne: bez dodatkowej ciszy",
                "Globalne menu ciszy nie ma jednoznacznej nazwy.");
            Assert(AutomationProperties.GetAcceleratorKey(globalAudioMenu) == "po prefiksie C",
                "Globalne menu ciszy utraciło informację o prefiksie.");

            var contextMenu = new ContextMenu();
            var contextCommand = new MenuItem
            {
                Header = "_Rozpoznane utwory",
                InputGestureText = "Ctrl+Alt+S"
            };
            AutomationProperties.SetName(contextCommand, "Rozpoznane utwory, Ctrl+Alt+S");
            contextMenu.Items.Add(contextCommand);
            var audioMenu = new MenuItem
            {
                Header = "Cisza między utworami: bez dodatkowej ciszy",
                InputGestureText = "Shift+C"
            };
            AutomationProperties.SetAcceleratorKey(audioMenu, "Shift+C");
            var noSilenceChoice = new MenuItem
            {
                Header = "Bez dodatkowej ciszy",
                IsCheckable = true,
                IsChecked = true
            };
            audioMenu.Items.Add(noSilenceChoice);
            contextMenu.Items.Add(audioMenu);

            MenuAccessibility.NormalizeContextMenu(contextMenu);

            Assert(contextCommand.Header?.ToString() == "Rozpoznane utwory", "Nie usunięto mnemonika z menu kontekstowego.");
            Assert(AutomationProperties.GetName(contextCommand) == "Rozpoznane utwory", "Menu kontekstowe powtarza skrót w nazwie.");
            Assert(contextCommand.InputGestureText == "Ctrl+Alt+S", "Usunięto skrót menu kontekstowego.");
            Assert(AutomationProperties.GetName(audioMenu) == "Cisza między utworami: bez dodatkowej ciszy",
                "Nazwa menu ciszy nie opisuje jednoznacznie wartości neutralnej.");
            Assert(AutomationProperties.GetAcceleratorKey(audioMenu) == "Shift+C",
                "Lokalny skrót odtwarzacza nie jest oddzielony od nazwy menu.");
            Assert(AutomationProperties.GetName(noSilenceChoice) == "Bez dodatkowej ciszy",
                "Wybór ciszy nie ma jawnej nazwy użytkowej.");
            Assert(noSilenceChoice.IsChecked, "Normalizacja menu zmieniła zaznaczoną wartość ciszy.");

            MenuAccessibility.SetPresentation(contextCommand, "Nagrywaj tę stację w tle");
            Assert(AutomationProperties.GetName(contextCommand) == "Nagrywaj tę stację w tle", "Dynamiczna nazwa menu powtarza skrót.");
            Assert(contextCommand.InputGestureText == "Ctrl+Alt+S", "Dynamiczna prezentacja usunęła skrót.");
        }
        catch (Exception exception)
        {
            failure = exception;
        }
    });
    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();

    if (failure is not null)
    {
        throw new InvalidOperationException("Test dostępności menu nie powiódł się.", failure);
    }

    Console.WriteLine("OK: pojedyncze oznajmianie skrótów w menu");
}

static void TestEditableFieldReplacement()
{
    Exception? failure = null;
    var thread = new Thread(() =>
    {
        try
        {
            var text = new TextBox { Text = "3000" };
            EditableFieldSelection.SelectAllForKeyboardEntry(text);
            Assert(text.SelectionStart == 0 && text.SelectionLength == text.Text.Length,
                "Wejście klawiaturą nie zaznacza całej dotychczasowej wartości tekstowej.");

            text.IsReadOnly = true;
            text.Select(2, 0);
            EditableFieldSelection.SelectAllForKeyboardEntry(text);
            Assert(text.SelectionStart == 2 && text.SelectionLength == 0,
                "Wspólna reguła niepotrzebnie zmienia zaznaczenie pola tylko do odczytu.");

            using var number = new System.Windows.Forms.NumericUpDown
            {
                Minimum = 1,
                Maximum = 10_080,
                Value = 60
            };
            number.CreateControl();
            EditableFieldSelection.SelectAllForKeyboardEntry(number);
            var numberEditor = number.Controls
                .OfType<System.Windows.Forms.TextBox>()
                .Single();
            Assert(numberEditor.SelectionStart == 0
                   && numberEditor.SelectionLength == numberEditor.Text.Length,
                "Wejście do pola liczbowego nie zaznacza poprzedniej liczby do zastąpienia.");
        }
        catch (Exception exception)
        {
            failure = exception;
        }
    });
    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();

    if (failure is not null)
        throw new InvalidOperationException("Test zastępowania wartości pola nie powiódł się.", failure);
    Console.WriteLine("OK: wpisywanie po wejściu klawiaturą zastępuje całą poprzednią wartość pola");
}

static void TestAudioOutputDeviceAccessibility()
{
    const string missingDeviceId = "amc-test-device-that-does-not-exist";
    var choices = AudioOutputDeviceCatalog.Enumerate(missingDeviceId);
    Assert(choices.Count >= 2, "Lista urządzeń nie zawiera wyboru domyślnego i niedostępnego urządzenia.");
    Assert(choices[0].Id is null, "Pierwszym wyborem nie jest urządzenie domyślne Windows.");
    Assert(choices[0].Label == "Domyślne urządzenie systemowe", "Domyślne urządzenie nie ma stabilnej nazwy.");
    var unavailable = choices.Single(choice => choice.Id == missingDeviceId);
    Assert(!unavailable.IsAvailable, "Brakujące urządzenie nie zostało oznaczone jako niedostępne.");
    Assert(unavailable.ToString() == unavailable.Label, "NVDA może otrzymać techniczny zapis wyboru urządzenia.");
    Assert(!unavailable.Label.Contains(missingDeviceId, StringComparison.Ordinal),
        "Identyfikator urządzenia wyciekł do dostępnej etykiety.");
    Console.WriteLine("OK: dostępny i bezpieczny wybór urządzenia audio sesji");
}

static void TestAudioOutputPauseRaceGuard()
{
    var pauseRequested = false;
    using var output = new TestWavePlayer
    {
        OnPlay = () => pauseRequested = true
    };

    AudioOutputPauseGuard.Play(output, () => pauseRequested);
    Assert(output.PlayCount == 1 && output.PauseCount == 1,
        "Pauza wydana podczas uruchamiania zewnętrznego wyjścia audio została pominięta.");

    pauseRequested = false;
    output.OnPlay = null;
    AudioOutputPauseGuard.Play(output, () => pauseRequested);
    Assert(output.PlayCount == 2 && output.PauseCount == 1,
        "Uruchomienie bez żądania pauzy niepotrzebnie zatrzymało wyjście audio.");
    Console.WriteLine("OK: pauza nie ginie podczas uruchamiania wyjścia audio");
}

static void TestAudioClipExporter()
{
    var directory = Path.Combine(Path.GetTempPath(), $"amc-clip-export-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    var source = Path.Combine(directory, "source.wav");
    var destination = Path.Combine(directory, "fragment.wav");
    try
    {
        var format = new WaveFormat(8_000, 16, 1);
        using (var writer = new WaveFileWriter(source, format))
        {
            writer.Write(new byte[format.AverageBytesPerSecond * 2]);
        }
        var sourceLength = new FileInfo(source).Length;
        AudioClipExporter.ExportAsync(
                new AudioClipExportRequest(
                    source,
                    destination,
                    TimeSpan.FromMilliseconds(250),
                    TimeSpan.FromMilliseconds(1250),
                    AudioClipExportFormat.Wav),
                progress: null,
                CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        Assert(File.Exists(destination), "Eksporter nie utworzył nowego pliku WAV.");
        Assert(new FileInfo(source).Length == sourceLength, "Eksporter zmienił plik źródłowy.");
        using var fragment = new WaveFileReader(destination);
        Assert(
            Math.Abs(fragment.TotalTime.TotalSeconds - 1d) < 0.05d,
            $"Nieprawidłowa długość wyeksportowanego fragmentu: {fragment.TotalTime}.");
        Console.WriteLine("OK: niedestrukcyjny eksport dokładnego fragmentu WAV");

        if (AudioClipExporter.IsFfmpegAvailable)
        {
            var copied = Path.Combine(directory, "fragment-bez-konwersji.wav");
            var flac = Path.Combine(directory, "fragment.flac");
            AudioClipExporter.ExportAsync(
                    new AudioClipExportRequest(
                        source,
                        copied,
                        TimeSpan.FromMilliseconds(250),
                        TimeSpan.FromMilliseconds(1250),
                        AudioClipExportFormat.OriginalStream),
                    progress: null,
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            using (var copiedReader = new WaveFileReader(copied))
            {
                Assert(Math.Abs(copiedReader.TotalTime.TotalSeconds - 1d) < 0.05d,
                    "FFmpeg nie zapisał prawidłowego fragmentu bez konwersji.");
            }
            AudioClipExporter.ExportAsync(
                    new AudioClipExportRequest(
                        source,
                        flac,
                        TimeSpan.FromMilliseconds(250),
                        TimeSpan.FromMilliseconds(1250),
                        AudioClipExportFormat.Flac),
                    progress: null,
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            Assert(File.Exists(flac) && new FileInfo(flac).Length > 0,
                "FFmpeg nie zapisał dokładnego fragmentu FLAC.");
            using var flacReader = WindowsMediaOutput.OpenReaderForExport(flac);
            Assert(Math.Abs(flacReader.TotalTime.TotalSeconds - 1d) < 0.08d,
                $"Nieprawidłowa długość fragmentu FLAC: {flacReader.TotalTime}.");
            Console.WriteLine("OK: zweryfikowany FFmpeg zapisuje bez konwersji i do FLAC");
        }
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static void TestAudioClipOriginalEditor()
{
    var longExpectedDuration = TimeSpan.FromSeconds(6482.476644);
    Assert(
        AudioClipOriginalEditor.DurationMatches(
            longExpectedDuration,
            TimeSpan.FromSeconds(6482.508821)),
        "Rzeczywista oś czasu długiego MP3 powinna przejść kontrolę cięcia.");
    Assert(
        !AudioClipOriginalEditor.DurationMatches(
            longExpectedDuration,
            TimeSpan.FromSeconds(6464.876168)),
        "Wynik obcięty według zaniżonego czasu nagłówka MP3 nie powinien przejść kontroli.");

    if (!AudioClipOriginalEditor.IsAvailable)
    {
        Console.WriteLine("POMINIĘTO: destrukcyjna edycja fragmentu wymaga zainstalowanego FFmpeg");
        return;
    }

    var directory = Path.Combine(Path.GetTempPath(), $"amc-clip-remove-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    var source = Path.Combine(directory, "source.wav");
    try
    {
        var format = new WaveFormat(8_000, 16, 1);
        using (var writer = new WaveFileWriter(source, format))
        {
            writer.Write(new byte[format.AverageBytesPerSecond * 3]);
        }
        var originalBytes = File.ReadAllBytes(source);
        var result = AudioClipOriginalEditor.RemoveAsync(
                new AudioClipRemovalRequest(
                    source,
                    TimeSpan.FromSeconds(1),
                    TimeSpan.FromSeconds(2),
                    TimeSpan.FromSeconds(3)),
                progress: null,
                CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        Assert(File.Exists(source), "Po edycji zabrakło pliku źródłowego.");
        Assert(File.Exists(result.BackupPath), "Edycja nie utworzyła kopii bezpieczeństwa.");
        Assert(
            originalBytes.SequenceEqual(File.ReadAllBytes(result.BackupPath)),
            "Kopia bezpieczeństwa nie jest identyczna z oryginałem.");
        using var edited = new WaveFileReader(source);
        Assert(
            Math.Abs(edited.TotalTime.TotalSeconds - 2d) < 0.08d,
            $"Nieprawidłowa długość pliku po usunięciu fragmentu: {edited.TotalTime}.");
        Assert(
            !Directory.EnumerateFiles(directory).Any(path => Path.GetFileName(path).Contains(".amc-cut-", StringComparison.Ordinal)),
            "Po udanej edycji pozostał plik tymczasowy.");

        var ffmpeg = FfmpegRadioWaveProvider.FindExecutable()
            ?? throw new InvalidOperationException("FFmpeg zniknął podczas testu edycji.");
        var mp3 = Path.Combine(directory, "source.mp3");
        var conversion = Process.Start(new ProcessStartInfo
        {
            FileName = ffmpeg,
            UseShellExecute = false,
            CreateNoWindow = true,
            ArgumentList =
            {
                "-nostdin", "-hide_banner", "-loglevel", "error", "-y",
                "-i", result.BackupPath, "-c:a", "libmp3lame", "-b:a", "128k", mp3
            }
        }) ?? throw new InvalidOperationException("Nie uruchomiono FFmpeg do testu MP3.");
        conversion.WaitForExit();
        Assert(conversion.ExitCode == 0 && File.Exists(mp3), "Nie przygotowano kontrolnego MP3.");
        var originalMp3 = File.ReadAllBytes(mp3);
        TimeSpan originalMp3Duration;
        using (var originalMp3Reader = WindowsMediaOutput.OpenReaderForExport(mp3))
        {
            originalMp3Duration = originalMp3Reader.TotalTime;
        }
        var mp3Result = AudioClipOriginalEditor.RemoveAsync(
                new AudioClipRemovalRequest(
                    mp3,
                    TimeSpan.FromSeconds(1),
                    TimeSpan.FromSeconds(2),
                    originalMp3Duration),
                progress: null,
                CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        Assert(originalMp3.SequenceEqual(File.ReadAllBytes(mp3Result.BackupPath)),
            "Kopia bezpieczeństwa MP3 nie jest identyczna z oryginałem.");
        using var editedMp3 = WindowsMediaOutput.OpenReaderForExport(mp3);
        var expectedMp3Duration = originalMp3Duration - TimeSpan.FromSeconds(1);
        Assert(Math.Abs((editedMp3.TotalTime - expectedMp3Duration).TotalSeconds) < 0.2d,
            $"Nieprawidłowa długość MP3 po usunięciu fragmentu: {editedMp3.TotalTime}.");
        Console.WriteLine("OK: usuwanie fragmentu WAV i MP3 podmienia plik dopiero po weryfikacji i zachowuje pełną kopię");
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static void TestAudioClipExportAccessibility()
{
    Exception? failure = null;
    var thread = new Thread(() =>
    {
        try
        {
            var window = new AudioClipExportWindow(
                @"C:\Nagrania\audycja.mp3",
                "Audycja",
                TimeSpan.FromSeconds(15),
                TimeSpan.FromSeconds(45));
            var choices = window.FormatCombo.Items.OfType<ComboBoxItem>().ToArray();
            Assert(choices.Length >= 1, "Okno eksportu nie zawiera sposobów zapisu.");
            foreach (var choice in choices)
            {
                var name = AutomationProperties.GetName(choice);
                Assert(!string.IsNullOrWhiteSpace(name), "Sposób zapisu nie ma jawnej nazwy dla NVDA.");
                Assert(!name.Contains('{') && !name.Contains("AudioClipExportFormat", StringComparison.Ordinal),
                    "Techniczna reprezentacja sposobu zapisu wyciekła do nazwy dostępnościowej.");
            }
            Assert(window.FormatCombo.SelectedItem is ComboBoxItem,
                "Okno eksportu nie wybiera bezpiecznego sposobu zapisu przy otwarciu.");
        }
        catch (Exception exception)
        {
            failure = exception;
        }
    });
    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();

    if (failure is not null)
        throw new InvalidOperationException("Test dostępności okna eksportu fragmentu nie powiódł się.", failure);
    Console.WriteLine("OK: jawne etykiety NVDA sposobów zapisu fragmentu");
}

static void TestFfmpegComponentSecurity()
{
    var expected = new string('a', 64);
    var checksums = $"{new string('b', 64)}  other.zip\n{expected} *{FfmpegComponentManager.AssetName}\n";
    Assert(
        FfmpegComponentManager.ParseChecksum(checksums, FfmpegComponentManager.AssetName) == expected,
        "Aktualizator FFmpeg nie wybiera sumy przypisanej do dokładnej nazwy pakietu.");
    Assert(
        FfmpegComponentManager.ParseChecksum(
            $"1234  {FfmpegComponentManager.AssetName}",
            FfmpegComponentManager.AssetName) is null,
        "Aktualizator przyjmuje nieprawidłową sumę SHA-256.");
    Assert(
        FfmpegComponentManager.ArchiveUri.Scheme == Uri.UriSchemeHttps
        && FfmpegComponentManager.ArchiveUri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase),
        "Pakiet FFmpeg nie pochodzi z przypiętego źródła HTTPS GitHub BtbN.");

    var root = Path.Combine(Path.GetTempPath(), $"amc-ffmpeg-security-{Guid.NewGuid():N}");
    Assert(
        FfmpegComponentManager.IsSafeArchiveDestination(root, Path.Combine(root, "package", "bin", "ffmpeg.exe")),
        "Aktualizator odrzuca bezpieczną ścieżkę wewnątrz pakietu.");
    Assert(
        !FfmpegComponentManager.IsSafeArchiveDestination(root, Path.Combine(root, "..", "outside.exe")),
        "Aktualizator nie blokuje wyjścia archiwum poza katalog tymczasowy.");
    Console.WriteLine("OK: suma SHA-256, przypięte źródło i ochrona rozpakowywania FFmpeg");
}

static void TestGlobalPrefixCapture()
{
    if (IntPtr.Size == 8)
    {
        Assert(
            !ShortcutCaptureWindow.TryReadExactNumpadMessage(
                0x0007,
                new IntPtr((long)int.MaxValue + 1),
                IntPtr.Zero,
                out _,
                out _,
                out _),
            "Okno przechwytywania próbuje interpretować 64-bitowy komunikat fokusu jako klawisz.");
    }
    Assert(
        !ShortcutCaptureWindow.TryReadExactNumpadMessage(
            0x0100,
            new IntPtr(0x6B),
            IntPtr.Zero,
            out _,
            out _,
            out _)
        && WindowsKeyMap.FromVirtualKey(0x6B) == "NumpadAdd",
        "Plus numeryczny powinien używać zwykłej bezpiecznej ścieżki WPF, nie natywnego haka okna.");
    Assert(
        ShortcutCaptureWindow.TryReadExactNumpadMessage(
            0x0100,
            new IntPtr(0x0D),
            new IntPtr(1L << 24),
            out var capturedVirtualKey,
            out var capturedKeyName,
            out var capturedKeyDown)
        && capturedVirtualKey == 0x0D
        && capturedKeyName == "NumpadEnter"
        && capturedKeyDown,
        "Enter numeryczny utracił dokładną, zabezpieczoną ścieżkę przechwytywania.");
    Assert(
        WindowsKeyMap.ToDisplayText(KeyChord.Parse("Ctrl+NumpadEnter")) == "Ctrl+Enter numeryczny",
        "Enter numeryczny nie ma użytkowej etykiety dla NVDA.");
    Assert(
        WindowsKeyMap.ToDisplayText(KeyChord.Parse("NumpadAdd")) == "Plus numeryczny"
        && WindowsKeyMap.ToDisplayText(KeyChord.Parse("NumpadSubtract")) == "Minus numeryczny"
        && WindowsKeyMap.ToDisplayText(KeyChord.Parse("NumpadDecimal")) == "Kropka numeryczna"
        && WindowsKeyMap.ToDisplayText(KeyChord.Parse("NumpadNumLock")) == "Num Lock"
        && WindowsKeyMap.ToDisplayText(KeyChord.Parse("NumpadInsert")) == "Insert numeryczny",
        "Klawisze bloku numerycznego nie mają użytkowych nazw dla NVDA.");
    Assert(
        WindowsKeyMap.TryGetVirtualKey("NumpadAdd", out var numpadAdd) && numpadAdd == 0x6B
        && WindowsKeyMap.TryGetVirtualKey("NumpadSubtract", out var numpadSubtract) && numpadSubtract == 0x6D
        && WindowsKeyMap.TryGetVirtualKey("NumpadDecimal", out var numpadDecimal) && numpadDecimal == 0x6E
        && WindowsKeyMap.TryGetVirtualKey("NumpadNumLock", out var numpadNumLock) && numpadNumLock == 0x90
        && WindowsKeyMap.TryGetVirtualKey("Pause", out var pause) && pause == 0x13
        && WindowsKeyMap.FromVirtualKey(0x13) == "Pause",
        "Operatory bloku numerycznego lub klawisz Pause nie zachowują własnych klawiszy wirtualnych.");
    Assert(
        GlobalPrefixService.RequiresLowLevelHook(KeyChord.Parse("Ctrl+NumpadEnter")),
        "Prefiks z Enterem numerycznym nie jest kierowany do dokładnego przechwytywania.");
    Assert(
        !GlobalPrefixService.RequiresLowLevelHook(KeyChord.Parse("Ctrl+Enter")),
        "Zwykły Enter został błędnie utożsamiony z Enterem numerycznym.");
    Assert(
        GlobalPrefixService.RequiresLowLevelHook(KeyChord.Parse("NumpadInsert"))
        && GlobalPrefixService.RequiresLowLevelHook(KeyChord.Parse("NumpadAdd"))
        && GlobalPrefixService.RequiresLowLevelHook(KeyChord.Parse("NumpadNumLock"))
        && GlobalPrefixService.RequiresLowLevelHook(KeyChord.Parse("NumpadDecimal"))
        && !GlobalPrefixService.RequiresLowLevelHook(KeyChord.Parse("Pause")),
        "Globalny prefiks nie kieruje wszystkich fizycznych klawiszy numerycznych do haka albo błędnie kieruje tam Pause.");
    Assert(
        GlobalPrefixService.IsNumpadEnterInput(0x0D, 0x01)
        && !GlobalPrefixService.IsNumpadEnterInput(0x0D, 0x00),
        "Flaga rozszerzonego Entera nie odróżnia obu klawiszy Enter.");
    Assert(
        WindowsKeyMap.FromKeyboardInput(0x2D, extended: false) == "NumpadInsert"
        && WindowsKeyMap.FromKeyboardInput(0x2D, extended: true) == "Insert"
        && WindowsKeyMap.FromKeyboardInput(0x2E, extended: false) == "NumpadDelete"
        && WindowsKeyMap.FromKeyboardInput(0x2E, extended: true) == "Delete",
        "Numeryczne Insert i Delete są mylone z osobnym blokiem nawigacyjnym.");

    Exception? failure = null;
    var directory = Path.Combine(Path.GetTempPath(), $"amc-prefix-setting-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    var thread = new Thread(() =>
    {
        SettingsWindow? settings = null;
        ShortcutCaptureWindow? capture = null;
        ShortcutCaptureWindow? numericCapture = null;
        try
        {
            var state = new PersistedState();
            state.Settings.PrefixChord = "NumpadAdd";
            var store = new ConfigurationStore(
                Path.Combine(directory, "state.json"),
                Path.Combine(directory, "library.db"));
            settings = new SettingsWindow(state, store, SettingsTarget.Prefix);
            var prefixBox = (TextBox)settings.FindName("PrefixBox");
            var changeButton = (Button)settings.FindName("ChangePrefixButton");
            Assert(prefixBox.IsReadOnly && prefixBox.Text == "Plus numeryczny",
                "Pole prefiksu nie pokazuje stabilnej wartości tylko do odczytu.");
            Assert((AutomationProperties.GetHelpText(changeButton) ?? string.Empty)
                    .Contains("zastępuje cały poprzedni prefiks", StringComparison.Ordinal),
                "Przycisk zmiany prefiksu nie wyjaśnia reguły zastępowania.");

            capture = new ShortcutCaptureWindow(
                "Globalny prefiks",
                KeyChord.Parse("Ctrl+NumpadEnter"));
            var command = (TextBlock)capture.FindName("CommandText");
            var captured = (TextBox)capture.FindName("CapturedText");
            Assert(command.Text == "Funkcja: Globalny prefiks"
                   && captured.Text == "Ctrl+Enter numeryczny",
                "Okno przechwytywania ujawnia identyfikator techniczny lub złą nazwę klawisza.");
            numericCapture = new ShortcutCaptureWindow(
                "Globalny prefiks",
                KeyChord.Parse("NumpadInsert"));
            Assert(((TextBox)numericCapture.FindName("CapturedText")).Text == "Insert numeryczny",
                "Okno przechwytywania nie pokazuje numerycznego Inserta bez modyfikatorów.");
        }
        catch (Exception exception)
        {
            failure = exception;
        }
        finally
        {
            numericCapture?.Close();
            capture?.Close();
            settings?.Close();
        }
    });
    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();
    try
    {
        Directory.Delete(directory, recursive: true);
    }
    catch (IOException)
    {
    }
    if (failure is not null)
        throw new InvalidOperationException("Test przechwytywania globalnego prefiksu nie powiódł się.", failure);

    Console.WriteLine("OK: dostępne przechwytywanie globalnego prefiksu");
}

static void TestRadioScheduleAccessibility()
{
    Exception? failure = null;
    var thread = new Thread(() =>
    {
        RadioScheduleEditorWindow? editor = null;
        RadioSchedulesWindow? manager = null;
        try
        {
            var station = new MediaItem
            {
                Id = "radio-schedule-accessibility",
                Title = "Stacja testowa",
                Kind = MediaItemKind.Station,
                Source = "https://example.invalid/radio.mp3"
            };
            var schedule = new RadioRecordingScheduleSettings
            {
                Id = "schedule-accessibility",
                StationId = station.Id,
                StationName = station.Title,
                StreamUrl = station.Source,
                NextStartUtcTicks = DateTime.UtcNow.AddHours(1).Ticks,
                TimeZoneId = TimeZoneInfo.Local.Id,
                DurationMinutes = 30,
                FileNameTemplate = "Audycja - {data}",
                RecordingFormat = RadioRecordingFormat.Mp3,
                RecordingBitrateKbps = 192,
                Enabled = true
            };

            editor = new RadioScheduleEditorWindow(
                [station],
                schedule,
                station.Id,
                defaultRecordingFormat: RadioRecordingFormat.Mp3,
                defaultRecordingBitrateKbps: 192);
            var formatCombo = (ComboBox)editor.FindName("RecordingFormatCombo");
            var bitrateCombo = (ComboBox)editor.FindName("RecordingBitrateCombo");
            var fileNameTemplate = (TextBox)editor.FindName("FileNameTemplateTextBox");
            var fileNameMenuButton = (Button)editor.FindName("FileNameMenuButton");
            var fileNamePreview = (AccessibleStatusTextBlock)editor.FindName("FileNamePreview");
            Assert(AutomationProperties.GetName(formatCombo) == "Format tego nagrania",
                "Lista formatów nie ma jednoznacznej nazwy dostępnościowej.");
            Assert(AutomationProperties.GetName(bitrateCombo) == "Bitrate tego nagrania MP3 lub AAC",
                "Lista bitrate nie ma jednoznacznej nazwy dostępnościowej.");
            Assert(formatCombo.SelectedItem is not null && bitrateCombo.SelectedItem is not null,
                "Początkowe wartości formatu albo bitrate nie zostały wybrane.");
            Assert(formatCombo.Items.Cast<object>().All(IsUserFacingChoice),
                "Lista formatów ujawnia techniczną reprezentację obiektu.");
            Assert(bitrateCombo.Items.Cast<object>().All(IsUserFacingChoice),
                "Lista bitrate ujawnia techniczną reprezentację obiektu.");
            Assert(AutomationProperties.GetName(fileNameTemplate) == "Szablon nazwy pliku nagrania"
                   && fileNameTemplate.Text == "Audycja - {data}",
                "Edytowalny szablon nazwy pliku nie ma stabilnej nazwy albo wartości.");
            Assert(AutomationProperties.GetName(fileNameMenuButton)
                   == "Wstaw token lub wybierz gotowy szablon nazwy pliku",
                "Przycisk tokenów nazwy pliku nie ma użytkowej nazwy.");
            var fileNameMenu = fileNameMenuButton.ContextMenu;
            var fileNameMenuItems = fileNameMenu is null
                ? []
                : DescendantMenuItems(fileNameMenu.Items).ToArray();
            Assert(fileNameMenu is not null
                   && fileNameMenu.Items.OfType<MenuItem>().Count() == 2
                   && fileNameMenuItems.Length == 19
                   && fileNameMenuItems.All(item =>
                       !string.IsNullOrWhiteSpace(AutomationProperties.GetName(item))),
                "Menu nazwy pliku nie udostępnia dwóch nazwanych podmenu i wszystkich użytkowych pozycji.");
            Assert(fileNamePreview.Text.Contains("Audycja - ", StringComparison.Ordinal)
                   && fileNamePreview.Text.EndsWith(".mp3", StringComparison.Ordinal),
                "Podgląd nie pokazuje wynikowej nazwy i rozszerzenia MP3.");
            var datePickerHost = (System.Windows.Forms.Integration.WindowsFormsHost)
                editor.FindName("DatePickerHost");
            var datePicker = (System.Windows.Forms.DateTimePicker)datePickerHost.Child;
            Assert(datePicker.ShowUpDown
                   && datePicker.CustomFormat == "dd.MM.yyyy"
                   && datePicker.AccessibleRole == System.Windows.Forms.AccessibleRole.SpinButton,
                "Kalendarz nie zachowuje nawigacji po częściach daty za pomocą strzałek.");
            Assert(datePicker.AccessibleName == "Data pierwszego nagrania"
                   && (datePicker.AccessibleDescription ?? string.Empty)
                       .Contains("Wpisz kolejno dwie cyfry dnia", StringComparison.Ordinal)
                   && (datePicker.AccessibleDescription ?? string.Empty)
                       .Contains("Góra i dół zmienia", StringComparison.Ordinal),
                "Kalendarz nie objaśnia NVDA ciągłego wpisywania cyfr i obsługi strzałkami.");
            var timePickerHost = (System.Windows.Forms.Integration.WindowsFormsHost)
                editor.FindName("TimePickerHost");
            var timePicker = (System.Windows.Forms.DateTimePicker)timePickerHost.Child;
            Assert(timePicker.AccessibleName == "Godzina rozpoczęcia"
                   && (timePicker.AccessibleDescription ?? string.Empty)
                       .Contains("na przykład 2310", StringComparison.Ordinal),
                "Pole czasu nie objaśnia NVDA wpisywania czterech cyfr bez dwukropka.");

            manager = new RadioSchedulesWindow(
                [station],
                [schedule],
                [],
                station.Id,
                wakeScheduledRecordings: false,
                defaultRecordingFormat: RadioRecordingFormat.Mp3,
                defaultRecordingBitrateKbps: 192);
            manager.Show();
            DrainDispatcher(manager.Dispatcher);
            var schedulesList = (ListBox)manager.FindName("SchedulesList");
            Assert(schedulesList.SelectedItem is not null,
                "Lista harmonogramów nie wybiera pierwszego planu.");
            var selected = schedulesList.SelectedItem;
            var accessibleLabel = selected?.GetType().GetProperty("AccessibleLabel")?.GetValue(selected)?.ToString();
            Assert(!string.IsNullOrWhiteSpace(accessibleLabel)
                   && accessibleLabel.StartsWith("Stacja testowa, włączone,", StringComparison.Ordinal)
                   && !accessibleLabel.Contains("pole wyboru", StringComparison.OrdinalIgnoreCase)
                   && IsUserFacingChoice(selected!),
                "Pierwszy harmonogram nie ma stabilnej, użytkowej etykiety dostępnościowej.");
            Assert(TextSearch.GetTextPath(schedulesList) == "NavigationText"
                   && selected?.GetType().GetProperty("NavigationText")?.GetValue(selected)?.ToString()
                       == "Stacja testowa",
                "Nawigacja literowa harmonogramów nie korzysta z nazwy stacji.");
            var scheduleStatus = (AccessibleStatusTextBlock)manager.FindName("ScheduleStatus");
            var selectedBeforeToggle = schedulesList.SelectedItem;
            var selectedIndexBeforeToggle = schedulesList.SelectedIndex;
            var containerBeforeToggle = schedulesList.ItemContainerGenerator
                .ContainerFromItem(selectedBeforeToggle) as ListBoxItem;
            Assert(containerBeforeToggle is not null && containerBeforeToggle.IsKeyboardFocusWithin,
                "Pierwszy harmonogram nie otrzymuje rzeczywistego fokusu klawiatury.");
            Assert(manager.ToggleSelectedEnabled(),
                "Spacja nie ma operacji przełączającej wybrany harmonogram.");
            DrainDispatcher(manager.Dispatcher);
            selected = schedulesList.SelectedItem;
            accessibleLabel = selected?.GetType().GetProperty("AccessibleLabel")?.GetValue(selected)?.ToString();
            Assert(ReferenceEquals(selectedBeforeToggle, selected)
                   && schedulesList.SelectedIndex == selectedIndexBeforeToggle
                   && containerBeforeToggle!.IsKeyboardFocusWithin
                   && !string.IsNullOrWhiteSpace(accessibleLabel)
                   && accessibleLabel.StartsWith("Stacja testowa, wyłączone,", StringComparison.Ordinal)
                   && !accessibleLabel.Contains("harmonogram", StringComparison.OrdinalIgnoreCase)
                   && !accessibleLabel.Contains("pole wyboru", StringComparison.OrdinalIgnoreCase),
                "Wyłączenie przebudowuje wiersz albo nie odświeża stanu jego pola wyboru.");
            Assert(scheduleStatus.Text == accessibleLabel
                   && scheduleStatus.Text.StartsWith("Stacja testowa, wyłączone,", StringComparison.Ordinal)
                   && !scheduleStatus.Text.Contains("Wybierz Zapisz", StringComparison.OrdinalIgnoreCase),
                "Wyłączenie nie tworzy krótkiego komunikatu zaczynającego się od stanu i nazwy stacji.");
            Assert(manager.ToggleSelectedEnabled(),
                "Ponowna Spacja nie włącza wybranego harmonogramu.");
            DrainDispatcher(manager.Dispatcher);
            Assert(scheduleStatus.Text.StartsWith("Stacja testowa, włączone,", StringComparison.Ordinal)
                   && !scheduleStatus.Text.Contains("harmonogram", StringComparison.OrdinalIgnoreCase)
                   && !scheduleStatus.Text.Contains("pole wyboru", StringComparison.OrdinalIgnoreCase),
                "Włączenie nie tworzy krótkiego komunikatu zaczynającego się od stanu i nazwy stacji.");
        }
        catch (Exception exception)
        {
            failure = exception;
        }
        finally
        {
            manager?.Close();
            editor?.Close();
        }
    });
    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();

    if (failure is not null)
    {
        throw new InvalidOperationException("Test dostępności harmonogramów nie powiódł się.", failure);
    }

    Console.WriteLine("OK: użytkowe etykiety formatu i harmonogramów nagrywania");

    static bool IsUserFacingChoice(object value)
    {
        var text = value.ToString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(text)
               && !text.Contains('{', StringComparison.Ordinal)
               && !text.Contains("Choice", StringComparison.Ordinal)
               && !text.Contains("Settings", StringComparison.Ordinal);
    }

    static void DrainDispatcher(System.Windows.Threading.Dispatcher dispatcher)
    {
        var frame = new System.Windows.Threading.DispatcherFrame();
        dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.ApplicationIdle,
            () => frame.Continue = false);
        System.Windows.Threading.Dispatcher.PushFrame(frame);
    }

    static IEnumerable<MenuItem> DescendantMenuItems(ItemCollection items)
    {
        foreach (var item in items.OfType<MenuItem>())
        {
            yield return item;
            foreach (var child in DescendantMenuItems(item.Items)) yield return child;
        }
    }
}

static void TestSegmentedDateTimeDigitEntry()
{
    var now = new DateTime(2026, 8, 31, 12, 0, 0, DateTimeKind.Utc);
    var minimum = new DateTime(1753, 1, 1);
    var maximum = new DateTime(9998, 12, 31, 23, 59, 59);
    var dateEditor = new SegmentedDateTimeDigitEditor(SegmentedDateTimeField.Date);
    var date = new DateTime(2026, 9, 4);

    var result = dateEditor.EnterDigit(date, 0, 1, now, minimum, maximum);
    Assert(!result.IsComplete, "Dzień został zatwierdzony przed wpisaniem dwóch cyfr.");
    result = dateEditor.EnterDigit(date, 0, 5, now.AddMilliseconds(100), minimum, maximum);
    Assert(result.IsComplete && result.IsValid && result.MoveNext && result.Value.Day == 15,
        "Dwucyfrowy dzień nie został ustawiony albo nie przeszedł do miesiąca.");
    date = result.Value;

    result = dateEditor.EnterDigit(date, 1, 0, now.AddMilliseconds(200), minimum, maximum);
    Assert(!result.IsComplete, "Miesiąc został zatwierdzony przed wpisaniem dwóch cyfr.");
    result = dateEditor.EnterDigit(date, 1, 4, now.AddMilliseconds(300), minimum, maximum);
    Assert(result.IsComplete && result.IsValid && result.MoveNext && result.Value.Month == 4,
        "Miesiąc 04 nie został ustawiony jako kwiecień albo nie przeszedł do roku.");
    date = result.Value;

    foreach (var (digit, offset) in new[] { (2, 400), (0, 500), (2, 600), (7, 700) })
        result = dateEditor.EnterDigit(date, 2, digit, now.AddMilliseconds(offset), minimum, maximum);
    Assert(result.IsComplete && result.IsValid && !result.MoveNext && result.Value.Year == 2027,
        "Czterocyfrowy rok nie został ustawiony w ostatnim segmencie.");

    dateEditor.Reset();
    var monthEnd = new DateTime(2026, 1, 31);
    _ = dateEditor.EnterDigit(monthEnd, 1, 0, now, minimum, maximum);
    result = dateEditor.EnterDigit(monthEnd, 1, 4, now.AddMilliseconds(100), minimum, maximum);
    Assert(result.IsValid && result.Value == new DateTime(2026, 4, 30),
        "Zmiana miesiąca nie dopasowała dnia do końca krótszego miesiąca.");

    var timeEditor = new SegmentedDateTimeDigitEditor(SegmentedDateTimeField.Time);
    var time = new DateTime(2026, 8, 31, 8, 5, 0);
    _ = timeEditor.EnterDigit(time, 0, 2, now, minimum, maximum);
    result = timeEditor.EnterDigit(time, 0, 3, now.AddMilliseconds(100), minimum, maximum);
    Assert(result.IsValid && result.MoveNext && result.Value.Hour == 23,
        "Pierwsze dwie cyfry 2310 nie ustawiły godziny 23.");
    time = result.Value;
    _ = timeEditor.EnterDigit(time, 1, 1, now.AddMilliseconds(200), minimum, maximum);
    result = timeEditor.EnterDigit(time, 1, 0, now.AddMilliseconds(300), minimum, maximum);
    Assert(result.IsValid && !result.MoveNext && result.Value.Hour == 23 && result.Value.Minute == 10,
        "Ciąg 2310 nie ustawił czasu 23:10.");

    timeEditor.Reset();
    _ = timeEditor.EnterDigit(time, 0, 2, now, minimum, maximum);
    result = timeEditor.EnterDigit(time, 0, 9, now.AddMilliseconds(100), minimum, maximum);
    Assert(result.IsComplete && !result.IsValid && result.Value == time,
        "Nieprawidłowa godzina 29 zmieniła czas.");
    _ = timeEditor.EnterDigit(time, 0, 0, now.AddMilliseconds(200), minimum, maximum);
    result = timeEditor.EnterDigit(time, 0, 5, now.AddMilliseconds(300), minimum, maximum);
    Assert(result.IsValid && result.Value.Hour == 5,
        "Po błędzie nie rozpoczęto czystego wpisywania następnej godziny.");

    timeEditor.Reset();
    result = timeEditor.EnterDigit(time, 0, 1, now, minimum, maximum);
    result = timeEditor.EnterDigit(time, 0, 0, now.AddSeconds(4), minimum, maximum);
    Assert(!result.IsComplete,
        "Cyfra wpisana po przerwie została połączona ze starym, nieukończonym segmentem.");

    Assert(SegmentedDateTimeDigitEditor.TryGetDigit(System.Windows.Forms.Keys.D4, out var topDigit)
           && topDigit == 4
           && SegmentedDateTimeDigitEditor.TryGetDigit(System.Windows.Forms.Keys.NumPad7, out var padDigit)
           && padDigit == 7,
        "Cyfry z górnego rzędu albo klawiatury numerycznej nie są rozpoznawane jednakowo.");

    Console.WriteLine("OK: ciągłe wpisywanie segmentów daty i czasu");
}

static void TestStatePersistenceQueue()
{
    using var firstSaveStarted = new ManualResetEventSlim();
    using var releaseFirstSave = new ManualResetEventSlim();
    using var secondSaveCompleted = new ManualResetEventSlim();
    var savedValues = new System.Collections.Concurrent.ConcurrentQueue<int>();
    var saveCount = 0;
    var queue = new StatePersistenceQueue(
        state =>
        {
            var snapshot = ConfigurationStore.CreateDefaultState();
            snapshot.Settings.PrefixTimeoutMilliseconds =
                state.Settings.PrefixTimeoutMilliseconds;
            return snapshot;
        },
        state =>
        {
            var currentSave = Interlocked.Increment(ref saveCount);
            if (currentSave == 1)
            {
                firstSaveStarted.Set();
                if (!releaseFirstSave.Wait(TimeSpan.FromSeconds(5)))
                    throw new TimeoutException("Test nie zwolnił pierwszego zapisu.");
            }
            savedValues.Enqueue(state.Settings.PrefixTimeoutMilliseconds);
            if (currentSave == 2) secondSaveCompleted.Set();
        });

    var state = ConfigurationStore.CreateDefaultState();
    state.Settings.PrefixTimeoutMilliseconds = 1001;
    queue.Queue(state);
    Assert(firstSaveStarted.Wait(TimeSpan.FromSeconds(5)),
        "Kolejka nie rozpoczęła zapisu w tle.");

    var stopwatch = Stopwatch.StartNew();
    state.Settings.PrefixTimeoutMilliseconds = 1002;
    queue.Queue(state);
    state.Settings.PrefixTimeoutMilliseconds = 1003;
    queue.Queue(state);
    stopwatch.Stop();
    Assert(stopwatch.Elapsed < TimeSpan.FromSeconds(1),
        "Zajęty magazyn blokuje wątek wywołujący podczas dodawania nowszego stanu.");

    releaseFirstSave.Set();
    Assert(secondSaveCompleted.Wait(TimeSpan.FromSeconds(5)),
        "Kolejka nie zapisała najnowszego połączonego stanu.");
    state.Settings.PrefixTimeoutMilliseconds = 1004;
    Assert(queue.Flush(state, TimeSpan.FromSeconds(5), out var failure),
        $"Końcowy zapis kolejki nie powiódł się: {failure?.Message}");
    Assert(savedValues.SequenceEqual([1001, 1003, 1004]),
        "Kolejka nie zachowała pierwszego i najnowszego stanu albo nie połączyła zapisu pośredniego.");

    Console.WriteLine("OK: nieblokujący, wspólny zapis stanu sesji w tle");
}

static void TestRadioRecognitionAnnouncementPolicy()
{
    Assert(MainWindow.ShouldAnnounceRadioRecognitionResult(
            automatic: true,
            messagesEnabled: true,
            automaticRecognitionMessagesEnabled: true,
            isWindowActive: true),
        "Aktywne okno nie oznajmia włączonych automatycznych rozpoznań.");
    Assert(!MainWindow.ShouldAnnounceRadioRecognitionResult(
            automatic: true,
            messagesEnabled: true,
            automaticRecognitionMessagesEnabled: false,
            isWindowActive: true),
        "Wyłączona opcja nie wycisza automatycznych rozpoznań.");
    Assert(!MainWindow.ShouldAnnounceRadioRecognitionResult(
            automatic: true,
            messagesEnabled: true,
            automaticRecognitionMessagesEnabled: true,
            isWindowActive: false),
        "Automatyczne rozpoznanie przerywa pracę poza oknem AMC.");
    Assert(MainWindow.ShouldAnnounceRadioRecognitionResult(
            automatic: false,
            messagesEnabled: false,
            automaticRecognitionMessagesEnabled: false,
            isWindowActive: true),
        "Ręczne rozpoznanie nie odpowiada w aktywnym oknie AMC.");
    Assert(!MainWindow.ShouldAnnounceRadioRecognitionResult(
            automatic: false,
            messagesEnabled: true,
            automaticRecognitionMessagesEnabled: true,
            isWindowActive: false),
        "Ręczne rozpoznanie przerywa pracę poza oknem AMC.");
    Console.WriteLine("OK: oznajmianie rozpoznań tylko w aktywnym oknie AMC");
}

static void TestRadioRecognitionSchedulingPolicy()
{
    Assert(MainWindow.RadioRecognitionInitialDelay == TimeSpan.FromSeconds(6),
        "Pierwsze automatyczne rozpoznanie nie rozpoczyna się po sześciu sekundach.");
    Assert(MainWindow.RadioRecognitionRetryDelay == TimeSpan.FromSeconds(15),
        "Nieudane rozpoznanie nie ma szybkiej ponownej próby.");
    Assert(MainWindow.RadioRecognitionRetryDelay < MainWindow.RadioRecognitionRegularInterval,
        "Ponowna próba po niepowodzeniu nie jest szybsza od zwykłego interwału obserwowania.");
    Console.WriteLine("OK: szybkie pierwsze rozpoznanie i ponowna próba");
}

static void TestRadioRecognitionSettingAccessibility()
{
    Exception? failure = null;
    var directory = Path.Combine(Path.GetTempPath(), $"amc-recognition-setting-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    var thread = new Thread(() =>
    {
        SettingsWindow? window = null;
        try
        {
            var state = new PersistedState();
            state.Settings.Messages.AutomaticRecognitionMessages = false;
            state.Radio.AutomaticTrackRecognitionEnabled = true;
            state.Radio.AutomaticTrackRecognitionScope =
                RadioRecognitionScope.CurrentAndRecordingStations;
            var store = new ConfigurationStore(
                Path.Combine(directory, "state.json"),
                Path.Combine(directory, "library.db"));
            window = new SettingsWindow(
                state,
                store,
                SettingsTarget.RadioAutomaticTrackRecognition);
            var checkbox = (CheckBox)window.FindName("AutomaticRecognitionMessagesCheck");
            Assert(checkbox.IsChecked == false,
                "Pole automatycznych rozpoznań nie wczytuje zapisanego stanu.");
            Assert((AutomationProperties.GetHelpText(checkbox) ?? string.Empty)
                    .Contains("tylko wtedy, gdy okno AMC jest aktywne", StringComparison.Ordinal),
                "Pole nie wyjaśnia ograniczenia oznajmiania do aktywnego okna AMC.");
            var monitoringCheckbox = (CheckBox)window.FindName("AutomaticTrackRecognitionCheck");
            Assert(monitoringCheckbox.IsChecked == true,
                "Pole automatycznego obserwowania nie wczytuje zapisanego stanu.");
            Assert((AutomationProperties.GetName(monitoringCheckbox) ?? string.Empty)
                    .Contains("Automatycznie obserwuj", StringComparison.Ordinal),
                "Pole automatycznego obserwowania nie ma jednoznacznej nazwy dla NVDA.");
            Assert((AutomationProperties.GetHelpText(monitoringCheckbox) ?? string.Empty)
                    .Contains("ponownym uruchomieniu programu", StringComparison.Ordinal),
                "Pole nie wyjaśnia, że ustawienie jest trwałe.");
            var scopeCombo = (ComboBox)window.FindName("RadioRecognitionScopeCombo");
            Assert((scopeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString()
                    == nameof(RadioRecognitionScope.CurrentAndRecordingStations),
                "Pole zakresu nie wczytuje zapisanego wyboru.");
            Assert((AutomationProperties.GetName(scopeCombo) ?? string.Empty)
                    == "Zakres automatycznego rozpoznawania utworów",
                "Pole zakresu nie ma jednoznacznej nazwy dla NVDA.");
            Assert(scopeCombo.Items.OfType<ComboBoxItem>().All(item =>
                    !(AutomationProperties.GetName(item) ?? item.Content?.ToString() ?? string.Empty)
                        .Contains('{', StringComparison.Ordinal)),
                "Wariant zakresu ujawnia techniczny zapis obiektu.");
        }
        catch (Exception exception)
        {
            failure = exception;
        }
        finally
        {
            window?.Close();
        }
    });
    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();
    try
    {
        Directory.Delete(directory, recursive: true);
    }
    catch (IOException)
    {
        // A delayed SQLite handle may be released after process exit; the test
        // uses a unique temporary directory, so cleanup failure is harmless.
    }

    if (failure is not null)
    {
        throw new InvalidOperationException("Test ustawień obserwowania i oznajmiania rozpoznań nie powiódł się.", failure);
    }

    Console.WriteLine("OK: dostępne ustawienia obserwowania i oznajmiania rozpoznanych utworów");
}

static void TestRadioRecognitionHistoryFilterAccessibility()
{
    Exception? failure = null;
    var thread = new Thread(() =>
    {
        RadioRecognitionHistoryWindow? window = null;
        try
        {
            var entries = new List<RadioRecognizedTrackSettings>
            {
                new()
                {
                    Id = "one",
                    StationId = "station-one",
                    StationName = "Radio Jeden",
                    Title = "Pierwszy utwór",
                    RecognizedUtcTicks = DateTime.UtcNow.Ticks
                },
                new()
                {
                    Id = "two",
                    StationId = "station-two",
                    StationName = "Radio Dwa",
                    Title = "Drugi utwór",
                    RecognizedUtcTicks = DateTime.UtcNow.AddMinutes(-1).Ticks
                }
            };
            window = new RadioRecognitionHistoryWindow(entries);
            var combo = (ComboBox)window.FindName("StationFilterCombo");
            var filters = combo.Items.OfType<RecognitionStationFilter>().ToArray();
            Assert(filters.Length == 3 && filters[0].Label == "Wszystkie stacje",
                "Filtr historii nie zawiera wszystkich stacji i dwóch nazw stacji.");
            Assert(filters.All(filter => filter.ToString() == filter.Label
                                         && !filter.ToString().Contains('{', StringComparison.Ordinal)),
                "Filtr historii ujawnia techniczny zapis obiektu.");
            Assert((AutomationProperties.GetName(combo) ?? string.Empty)
                    == "Filtr historii według stacji",
                "Filtr historii nie ma użytkowej nazwy dla NVDA.");
            combo.SelectedItem = filters.Single(filter => filter.Label == "Radio Dwa");
            var list = (ListBox)window.FindName("HistoryList");
            Assert(list.Items.Count == 1
                   && list.Items.OfType<RecognitionHistoryRow>().Single().Entry.StationName == "Radio Dwa",
                "Wybranie stacji nie ograniczyło listy historii.");
        }
        catch (Exception exception)
        {
            failure = exception;
        }
        finally
        {
            window?.Close();
        }
    });
    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();
    if (failure is not null)
        throw new InvalidOperationException("Test filtra historii rozpoznawania nie powiódł się.", failure);

    Console.WriteLine("OK: dostępny filtr historii rozpoznawania według stacji");
}

static void TestPlaybackAudioSettingAccessibility()
{
    Exception? failure = null;
    var directory = Path.Combine(Path.GetTempPath(), $"amc-audio-setting-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    var thread = new Thread(() =>
    {
        SettingsWindow? window = null;
        ItemPlaybackOptionsWindow? itemOptions = null;
        try
        {
            var state = new PersistedState();
            state.Settings.Audio.LoudnessNormalizationEnabled = true;
            state.Settings.Audio.SmoothTrackTransitionsEnabled = true;
            state.Settings.Audio.InterTrackSilenceMilliseconds = 2000;
            var store = new ConfigurationStore(
                Path.Combine(directory, "state.json"),
                Path.Combine(directory, "library.db"));
            window = new SettingsWindow(state, store, SettingsTarget.LoudnessNormalization);

            var normalization = (CheckBox)window.FindName("LoudnessNormalizationCheck");
            var transitions = (CheckBox)window.FindName("SmoothTrackTransitionsCheck");
            var silence = (ComboBox)window.FindName("InterTrackSilenceCombo");
            Assert(normalization.IsChecked == true && transitions.IsChecked == true,
                "Opcje przetwarzania dźwięku nie wczytują zapisanego stanu.");
            Assert(silence.SelectedItem is ComboBoxItem selected
                   && selected.Tag?.ToString() == "2000"
                   && AutomationProperties.GetName(selected) == "2 sekundy ciszy",
                "Wybrana cisza nie ma stabilnej, użytkowej etykiety dla NVDA.");
            Assert((AutomationProperties.GetHelpText(silence) ?? string.Empty)
                    .Contains("w obsługiwanej sesji", StringComparison.Ordinal),
                "Lista ciszy nie wyjaśnia zakresu działania opcji.");

            itemOptions = new ItemPlaybackOptionsWindow(
                "Testowy utwór",
                ResumePositionMode.Inherit,
                1d,
                loudnessNormalizationOverride: null,
                smoothTrackTransitionsOverride: true,
                interTrackSilenceMillisecondsOverride: 500);
            var rate = (ComboBox)itemOptions.FindName("PlaybackRateBox");
            var itemNormalization = (ComboBox)itemOptions.FindName("LoudnessNormalizationBox");
            var itemTransitions = (ComboBox)itemOptions.FindName("SmoothTransitionsBox");
            var itemSilence = (ComboBox)itemOptions.FindName("InterTrackSilenceBox");
            Assert(rate.Items.Cast<object>().Any(choice =>
                    string.Equals(choice.ToString(), "1,00 razy — normalna prędkość", StringComparison.Ordinal)),
                "Neutralna prędkość nie wyjaśnia NVDA, że 1,00 razy jest wartością normalną.");
            Assert(itemNormalization.SelectedItem?.ToString()
                    == "Według folderu lub ustawienia globalnego",
                "Dziedziczenie normalizacji nie ma użytkowej etykiety.");
            Assert(itemTransitions.SelectedItem?.ToString() == "Włączone"
                   && itemSilence.SelectedItem?.ToString() == "Cisza: pół sekundy",
                "Wyjątki pliku nie są pokazane jako stabilne etykiety użytkowe.");
            Assert(itemNormalization.DisplayMemberPath == "Label"
                   && TextSearch.GetTextPath(itemNormalization) == "Label"
                   && AutomationProperties.GetName(itemNormalization) == "Normalizacja głośności",
                "Lista normalizacji nie ma pełnej semantyki UI Automation i wyszukiwania tekstowego.");
        }
        catch (Exception exception)
        {
            failure = exception;
        }
        finally
        {
            itemOptions?.Close();
            window?.Close();
        }
    });
    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();
    try
    {
        Directory.Delete(directory, recursive: true);
    }
    catch (IOException)
    {
    }
    if (failure is not null)
    {
        throw new InvalidOperationException("Test ustawień przetwarzania dźwięku nie powiódł się.", failure);
    }
    Console.WriteLine("OK: dostępne opcje normalizacji, przejścia i ciszy między utworami");
}

static void TestShazamFingerprint()
{
    const int sampleRate = 16_000;
    var samples = new short[sampleRate * 12];
    for (var index = 0; index < samples.Length; index++)
    {
        var sweep = 300d + 1_200d * index / samples.Length;
        samples[index] = (short)(9_000 * Math.Sin(2 * Math.PI * sweep * index / sampleRate)
            + 6_000 * Math.Sin(2 * Math.PI * 880 * index / sampleRate));
    }
    var first = ShazamTrackRecognitionService.CreateFingerprintForTests(samples);
    var second = ShazamTrackRecognitionService.CreateFingerprintForTests(samples);
    Assert(first.SequenceEqual(second), "Podpis akustyczny nie jest deterministyczny.");
    Assert(first.Length > 56, "Podpis akustyczny nie zawiera pików częstotliwości.");
    Assert(BinaryPrimitives.ReadUInt32LittleEndian(first) == 0xCAFE2580,
        "Podpis akustyczny ma nieprawidłowy nagłówek.");
    Assert(BinaryPrimitives.ReadUInt32LittleEndian(first.AsSpan(4)) != 0,
        "Podpis akustyczny nie zawiera sumy kontrolnej.");
    var hash = Convert.ToHexString(SHA256.HashData(first)).ToLowerInvariant();
    Assert(hash == "72f982937a4f5c167b3e2a04e496df69adb3d67fbf4518921d13c9a73e74d51d",
        $"Port podpisu akustycznego odbiega od wyniku ShazamIO: {hash}.");
    Console.WriteLine("OK: lokalny podpis akustyczny do rozpoznawania utworów");
}

static void TestRecognitionSearchLinks()
{
    var entry = new RadioRecognizedTrackSettings
    {
        Title = "Zażółć gęślą",
        Artist = "Wykonawca Testowy"
    };
    var links = new[]
    {
        RecognitionLinks.AppleMusic(entry),
        RecognitionLinks.Spotify(entry),
        RecognitionLinks.Tidal(entry),
        RecognitionLinks.YouTubeMusic(entry),
        RecognitionLinks.Discogs(entry),
        RecognitionLinks.MusicBrainz(entry)
    };
    Assert(links.All(link => Uri.TryCreate(link, UriKind.Absolute, out _)),
        "Eksport rozpoznania zawiera nieprawidłowe łącze wyszukiwania.");
    Assert(links.Distinct(StringComparer.Ordinal).Count() == links.Length,
        "Różne usługi otrzymały to samo łącze wyszukiwania.");
    Assert(links.All(link => !link.Contains(' ')),
        "Zapytanie do katalogu nie zostało prawidłowo zakodowane.");
    Console.WriteLine("OK: łącza rozpoznania do usług i katalogów muzycznych");
}

static void TestAccessiblePlaybackStatusStrip()
{
    using var status = new AccessiblePlaybackStatusStrip
    {
        AccessibleRole = System.Windows.Forms.AccessibleRole.StatusBar
    };
    var label = new System.Windows.Forms.ToolStripStatusLabel
    {
        AccessibleRole = System.Windows.Forms.AccessibleRole.StaticText
    };
    status.Items.Add(label);
    status.SpokenText = "AAC, 192 kb/s, odtwarzanie, Radio 357";
    status.CreateControl();

    Assert(status.AccessibilityObject.Role == System.Windows.Forms.AccessibleRole.StatusBar,
        "Kontrolka nie udostępnia roli paska stanu.");
    Assert(label.AccessibilityObject.Role == System.Windows.Forms.AccessibleRole.StaticText,
        "Element paska nie udostępnia roli tekstu statycznego.");
    Assert(label.AccessibilityObject.Name == status.SpokenText,
        "Element paska nie udostępnia aktualnej treści jako swojej nazwy.");
    Assert(status.AccessibilityObject.GetChildCount() == 1,
        "Pasek stanu nie udostępnia dokładnie jednego tekstowego dziecka.");
    Assert(status.AccessibilityObject.GetChild(0)?.Name == status.SpokenText,
        "Tekst paska nie jest osiągalny przez standardowe drzewo dostępności.");
    Console.WriteLine("OK: standardowy dostępny tekst paska stanu");
}

static void TestRadioPresetAccessibleLabels()
{
    var preset10 = new AccessibleMediaController.Windows.RadioPresetChoice(
        10, "10", "0", "station-10", "Radio Dziesięć", "https://example.invalid/10");
    var preset11 = new AccessibleMediaController.Windows.RadioPresetChoice(
        11, "11", "minus", null, null, null);
    var preset12 = new AccessibleMediaController.Windows.RadioPresetChoice(
        12, "12", "znak równości", "station-12", "Radio Dwanaście", "https://example.invalid/12");

    Assert(preset10.Label == "Preset numer 10, klawisz 0, skrót Ctrl+Shift+0 — Radio Dziesięć",
        "Preset 10 nie rozróżnia numeru miejsca od klawisza 0.");
    Assert(preset11.Label.StartsWith("Preset numer 11, klawisz minus, skrót Ctrl+Shift+minus", StringComparison.Ordinal),
        "Preset 11 nie ma jednoznacznej etykiety dostępnościowej.");
    Assert(preset12.Label.StartsWith("Preset numer 12, klawisz znak równości, skrót Ctrl+Shift+znak równości", StringComparison.Ordinal),
        "Preset 12 nie ma jednoznacznej etykiety dostępnościowej.");
    Assert(preset10.ToString() == preset10.Label,
        "Preset nie udostępnia użytkowej etykiety jako tekstu awaryjnego.");
    Console.WriteLine("OK: jednoznaczne etykiety presetów 10–12");
}

static void TestRadioPresetKeyboardMap()
{
    Assert(RadioPresetKeyMap.TryGetSlot(Key.D5, out var slot5) && slot5 == 5,
        "Klawisz 5 nie wskazuje presetu 5.");
    Assert(RadioPresetKeyMap.TryGetSlot(Key.D0, out var slot0) && slot0 == 10,
        "Klawisz 0 nie wskazuje wewnętrznego miejsca 10.");
    Assert(RadioPresetKeyMap.TryGetSlotFromVirtualKey(0x30, out var rawSlot0) && rawSlot0 == 10,
        "Surowy komunikat klawisza 0 nie wskazuje wewnętrznego miejsca 10.");
    Assert(RadioPresetKeyMap.TryGetSlot(Key.OemMinus, out var slotMinus) && slotMinus == 11,
        "Klawisz minus nie wskazuje presetu 11.");
    Assert(RadioPresetKeyMap.TryGetSlot(Key.OemPlus, out var slotEquals) && slotEquals == 12,
        "Klawisz znaku równości nie wskazuje presetu 12.");
    Assert(RadioPresetKeyMap.DirectShortcutLabel(10) == "0",
        "Bezpośredni skrót Ctrl+Shift+0 nie ma użytkowej etykiety Preset 0.");
    Console.WriteLine("OK: mapowanie klawiszy listy i bezpośrednich presetów");
}

static void TestMainWindowDigitShortcutRouting()
{
    var presetZero = MainWindowShortcutRouter.ResolveDigit(
        Key.D0,
        ModifierKeys.Control | ModifierKeys.Shift,
        presetsAvailable: true);
    Assert(
        presetZero.Kind == MainWindowDigitShortcutKind.Preset && presetZero.Slot == 10,
        "Ctrl+Shift+0 został błędnie skierowany do listy sesji zamiast presetu 0.");

    var sessions = MainWindowShortcutRouter.ResolveDigit(
        Key.D0,
        ModifierKeys.Control,
        presetsAvailable: true);
    Assert(
        sessions.Kind == MainWindowDigitShortcutKind.SessionList,
        "Ctrl+0 przestał otwierać listę sesji.");

    var noShiftLeak = MainWindowShortcutRouter.ResolveDigit(
        Key.D0,
        ModifierKeys.Control | ModifierKeys.Shift,
        presetsAvailable: false);
    Assert(
        noShiftLeak.Kind == MainWindowDigitShortcutKind.None,
        "Ctrl+Shift+0 nie może zostać zdegradowany do Ctrl+0.");
    Assert(
        GlobalPrefixService.IsFocusedDirectShortcutCandidate(
            KeyChord.Parse("Ctrl+Shift+0")),
        "Niskopoziomowa ochrona nie rozpoznaje Ctrl+Shift+0.");
    Assert(
        !GlobalPrefixService.IsFocusedDirectShortcutCandidate(
            KeyChord.Parse("Ctrl+0")),
        "Niskopoziomowa ochrona nie może przejąć Ctrl+0 przeznaczonego dla listy sesji.");
    Assert(
        MainWindowShortcutRouter.IsSessionListShortcut(
            Key.S,
            ModifierKeys.Control | ModifierKeys.Shift),
        "Ctrl+Shift+S nie otwiera listy sesji.");
    Assert(
        MainWindowShortcutRouter.ResolveAudioOutputSelection(Key.A, ModifierKeys.Shift)
            == CommandIds.SelectAudioOutput
        && MainWindowShortcutRouter.ResolveAudioOutputSelection(Key.F11, ModifierKeys.None) is null
        && MainWindowShortcutRouter.ResolveAudioOutputSelection(
            Key.A,
            ModifierKeys.Control | ModifierKeys.Shift) is null,
        "Shift+A nie wybiera urządzenia audio bieżącej sesji albo przejmuje błędny skrót.");
    Assert(
        GlobalPrefixService.IsFocusedDirectShortcutCandidate(
            KeyChord.Parse("Ctrl+Shift+S")),
        "Niskopoziomowa obsługa nie rozpoznaje Ctrl+Shift+S.");
    Assert(
        !MainWindowShortcutRouter.IsSessionListShortcut(Key.S, ModifierKeys.Control),
        "Ctrl+S nie może zostać przejęte jako lista sesji.");
    Assert(
        MainWindowShortcutRouter.ResolveNumberedView(Key.D1, ModifierKeys.Alt, "radio")
            == CommandIds.ViewLibrary,
        "Alt+1 w Radiu nie otwiera wszystkich zapisanych stacji.");
    Assert(
        MainWindowShortcutRouter.ResolveNumberedView(Key.D2, ModifierKeys.Alt, "radio") is null,
        "Alt+2 w Radiu nie może udawać trwałego widoku Nagrywane.");
    Assert(
        MainWindowShortcutRouter.ResolveTransientRadioView(Key.R, ModifierKeys.Alt, "radio")
            == CommandIds.ViewActiveRadioRecordings
        && MainWindowShortcutRouter.ResolveTransientRadioView(Key.R, ModifierKeys.Alt, "local") is null
        && MainWindowShortcutRouter.ResolveTransientRadioView(Key.R, ModifierKeys.None, "radio") is null,
        "Alt+R nie otwiera tymczasowego widoku Nagrywane wyłącznie w Radiu.");
    Assert(
        MainWindowShortcutRouter.ResolveNumberedView(Key.D3, ModifierKeys.Alt, "radio") is null,
        "Alt+3 w Radiu nie może otwierać pozornego widoku odtwarzanych urządzeń.");
    Assert(
        MainWindowShortcutRouter.ResolveNumberedView(Key.D1, ModifierKeys.Alt, "local")
            == CommandIds.ViewFolders
        && MainWindowShortcutRouter.ResolveNumberedView(Key.D2, ModifierKeys.Alt, "local")
            == CommandIds.ViewAllLocalFiles
        && MainWindowShortcutRouter.ResolveNumberedView(Key.D3, ModifierKeys.Alt, "local")
            == CommandIds.ViewCustomLocalOrder,
        "Alt+1–3 utraciły dotychczasowe znaczenia w Plikach lokalnych.");
    Assert(
        MainWindowNavigationPolicy.IsTransientRadioView("radio", "Nagrywane")
        && !MainWindowNavigationPolicy.IsTransientRadioView("radio", "Biblioteka")
        && !MainWindowNavigationPolicy.IsTransientRadioView("local", "Nagrywane"),
        "Polityka Escape nie rozpoznaje tymczasowego widoku Nagrywane w Radiu.");
    Assert(
        MainWindowNavigationPolicy.ShouldPreservePlaybackContext(true, "Historia odtwarzania")
        && MainWindowNavigationPolicy.ShouldPreservePlaybackContext(false, "Zakładki")
        && !MainWindowNavigationPolicy.ShouldPreservePlaybackContext(false, "Historia odtwarzania")
        && !MainWindowNavigationPolicy.ShouldPreservePlaybackContext(false, "Ulubione"),
        "Jawne odtworzenie z Ctrl+H nie zastępuje starego kontekstu Page Up i Page Down.");
    Assert(
        MainWindowShortcutRouter.ResolveRadioRecordingBookmark(
            Key.B,
            ModifierKeys.None,
            recordingContext: true) == CommandIds.AddBookmark
        && MainWindowShortcutRouter.ResolveRadioRecordingBookmark(
            Key.B,
            ModifierKeys.Shift,
            recordingContext: true) == CommandIds.AddNamedBookmark,
        "B i Shift+B nie wybierają szybkiej oraz nazwanej zakładki nagrania.");
    Assert(
        MainWindowShortcutRouter.ResolveRadioRecordingBookmark(
            Key.B,
            ModifierKeys.Control,
            recordingContext: true) is null
        && MainWindowShortcutRouter.ResolveRadioRecordingBookmark(
            Key.B,
            ModifierKeys.None,
            recordingContext: false) is null,
        "Skróty zakładek nagrania przejmują nieprawidłowy modyfikator albo kontekst.");
    Console.WriteLine("OK: skróty sesji, trwałe i tymczasowe widoki Radia oraz B i Shift+B nagrywanego pliku");
}

static void TestPlayerAudioProcessingKeyboardMap()
{
    Assert(
        MainWindowShortcutRouter.ResolvePlayerAudioProcessing(
            Key.N,
            ModifierKeys.Shift,
            playerActive: true,
            PlaybackAudioProcessingCapabilities.All) == CommandIds.ToggleLoudnessNormalization,
        "Shift+N nie przełącza normalizacji w obsługiwanym odtwarzaczu.");
    Assert(
        MainWindowShortcutRouter.ResolvePlayerAudioProcessing(
            Key.T,
            ModifierKeys.Shift,
            playerActive: true,
            PlaybackAudioProcessingCapabilities.All) == CommandIds.ToggleSmoothTrackTransitions,
        "Shift+T nie przełącza łagodnych przejść w obsługiwanym odtwarzaczu.");
    Assert(
        MainWindowShortcutRouter.ResolvePlayerAudioProcessing(
            Key.C,
            ModifierKeys.Shift,
            playerActive: true,
            PlaybackAudioProcessingCapabilities.All) == CommandIds.CycleInterTrackSilence,
        "Shift+C nie przechodzi przez czasy ciszy w obsługiwanym odtwarzaczu.");
    Assert(
        MainWindowShortcutRouter.ResolvePlayerAudioProcessing(
            Key.N,
            ModifierKeys.None,
            playerActive: true,
            PlaybackAudioProcessingCapabilities.All) is null,
        "N bez Shifta nie może zmieniać normalizacji.");
    Assert(
        MainWindowShortcutRouter.ResolvePlayerAudioProcessing(
            Key.N,
            ModifierKeys.Shift,
            playerActive: false,
            PlaybackAudioProcessingCapabilities.All) is null,
        "Shift+N nie może zmieniać normalizacji poza odtwarzaczem.");
    Assert(
        MainWindowShortcutRouter.ResolvePlayerAudioProcessing(
            Key.N,
            ModifierKeys.Shift,
            playerActive: true,
            PlaybackAudioProcessingCapabilities.None) is null,
        "Niewspierany adapter nie może udawać normalizacji.");
    Assert(
        MainWindowShortcutRouter.ResolvePlayerAudioProcessing(
            Key.T,
            ModifierKeys.Shift,
            playerActive: true,
            PlaybackAudioProcessingCapabilities.LoudnessNormalization) is null,
        "Adapter obsługujący tylko normalizację nie może udawać przejść.");
    Assert(
        MainWindowShortcutRouter.ResolvePlayerAudioProcessingFromVirtualKey(
            0x4E,
            ModifierKeys.Shift,
            playerActive: true,
            PlaybackAudioProcessingCapabilities.All) == CommandIds.ToggleLoudnessNormalization,
        "Surowy komunikat Shift+N nie przełącza normalizacji przed obsługą klawisza dostępu WPF.");
    Assert(
        MainWindowShortcutRouter.ResolvePlayerAudioProcessingFromVirtualKey(
            0x54,
            ModifierKeys.Shift,
            playerActive: true,
            PlaybackAudioProcessingCapabilities.All) == CommandIds.ToggleSmoothTrackTransitions,
        "Surowy komunikat Shift+T nie przełącza przejść przed obsługą klawisza dostępu WPF.");
    Assert(
        MainWindowShortcutRouter.ResolvePlayerAudioProcessingFromVirtualKey(
            0x43,
            ModifierKeys.Shift,
            playerActive: true,
            PlaybackAudioProcessingCapabilities.All) == CommandIds.CycleInterTrackSilence,
        "Surowy komunikat Shift+C nie zmienia ciszy przed obsługą klawisza dostępu WPF.");
    Assert(
        MainWindowShortcutRouter.ResolvePlayerAudioProcessingFromVirtualKey(
            0x43,
            ModifierKeys.None,
            playerActive: true,
            PlaybackAudioProcessingCapabilities.All) is null,
        "Surowe C bez Shifta nie może zostać przejęte jako opcja dźwięku.");
    Console.WriteLine("OK: Shift+N, Shift+T i Shift+C są chronione przed klawiszami dostępu WPF i zależą od możliwości toru");
}

static void TestPlaylistPresentation()
{
    var finite = new[]
    {
        new MediaItem { Id = "1", Title = "Pierwszy", Kind = MediaItemKind.Track, Duration = TimeSpan.FromMinutes(20) },
        new MediaItem { Id = "2", Title = "Drugi", Kind = MediaItemKind.Track, Duration = TimeSpan.FromMinutes(25) }
    };
    Assert(
        PlaylistPresentation.BuildLabel("Biskup", 2, 2, finite)
            == "Biskup, 2 elementy, łączny czas 45 min 0 s",
        "Playlista plików nie podaje łącznego czasu.");

    var live = new[]
    {
        new MediaItem { Id = "radio-1", Title = "Pierwsza", Kind = MediaItemKind.Station },
        new MediaItem { Id = "radio-2", Title = "Druga", Kind = MediaItemKind.Station }
    };
    Assert(
        PlaylistPresentation.BuildLabel("Radia", 2, 2, live)
            == "Radia, 2 elementy, transmisje na żywo",
        "Playlista radia błędnie sugeruje skończony czas trwania.");
    Console.WriteLine("OK: czas playlisty i jawna playlista transmisji na żywo");
}

static void TestGuardDoesNotBlockPositionReads()
{
    using var inner = new BlockingWaveStream();
    using var guarded = new GuardedWaveStream(inner, "blokujacy-test.wav");
    var readTask = Task.Run(() => guarded.Read(new byte[4_096], 0, 4_096));
    Assert(inner.ReadStarted.Wait(TimeSpan.FromSeconds(1)), "Testowy dekoder nie rozpoczął odczytu.");

    var stopwatch = Stopwatch.StartNew();
    _ = guarded.Position;
    stopwatch.Stop();
    Assert(stopwatch.Elapsed < TimeSpan.FromMilliseconds(100),
        "Odczyt pozycji czekał na zablokowany dekoder.");
    Assert(guarded.IsReadStalled(TimeSpan.Zero), "Nie wykryto zatrzymanego odczytu dekodera.");

    inner.AllowReadToFinish.Set();
    Assert(readTask.Wait(TimeSpan.FromSeconds(1)), "Nie zakończono testowego odczytu.");
    Console.WriteLine("OK: nadzór dekodera nie blokuje odczytu pozycji");
}

static void TestGuardRejectsAbsurdDuration()
{
    using var inner = new FixedDurationWaveStream(TimeSpan.FromDays(366));
    try
    {
        using var _ = new GuardedWaveStream(inner, "nieprawidlowa-dlugosc.wav");
        throw new InvalidOperationException("Zaakceptowano absurdalny czas trwania pliku.");
    }
    catch (InvalidDataException)
    {
        Console.WriteLine("OK: odrzucono absurdalny czas trwania dla wspólnej ścieżki formatów");
    }
}

static void TestManagedMp3Fallback()
{
    const int frameLength = 417;
    const int frameCount = 12;
    var path = Path.Combine(
        Path.GetTempPath(),
        $"amc-managed-fallback-{Guid.NewGuid():N}.mp3");
    try
    {
        byte[] frameHeader = [0xFF, 0xFB, 0x90, 0x00];
        var data = new byte[frameLength * frameCount];
        for (var frame = 0; frame < frameCount; frame++)
        {
            frameHeader.CopyTo(data, frame * frameLength);
        }
        File.WriteAllBytes(path, data);

        var builder = new Mp3FileReader.FrameDecompressorBuilder(
            waveFormat => new Mp3FrameDecompressor(waveFormat));
        using var decoder = new Mp3FileReaderBase(path, builder);
        using var reader = new WaveChannel32(decoder) { PadWithZeroes = false };
        using var guarded = new GuardedWaveStream(reader, path);
        Assert(
            guarded.WaveFormat.Encoding == WaveFormatEncoding.IeeeFloat
                && guarded.WaveFormat.BitsPerSample == 32,
            "Awaryjny dekoder nie został znormalizowany do bezpiecznych próbek float.");
        Assert(guarded.WaveFormat.SampleRate == 44_100, "Awaryjny dekoder podał złą częstotliwość.");
        Assert(guarded.TotalTime > TimeSpan.Zero, "Awaryjny dekoder nie podał czasu MP3.");
        var buffer = new byte[Math.Min(guarded.WaveFormat.AverageBytesPerSecond, 16_384)];
        Assert(guarded.Read(buffer, 0, buffer.Length) > 0, "Awaryjny dekoder nie zwrócił próbek.");
        guarded.CurrentTime = TimeSpan.FromTicks(guarded.TotalTime.Ticks / 2);
        Assert(guarded.Read(buffer, 0, buffer.Length) > 0,
            "Awaryjny dekoder nie odczytał danych po przewinięciu do środka MP3.");
        var nearEnd = guarded.TotalTime - TimeSpan.FromMilliseconds(100);
        guarded.CurrentTime = nearEnd > TimeSpan.Zero ? nearEnd : TimeSpan.Zero;
        Assert(guarded.Read(buffer, 0, buffer.Length) > 0,
            "Awaryjny dekoder nie obsłużył przewinięcia w pobliże końca MP3.");

        var sanitizedPath = Path.Combine(
            Path.GetTempPath(),
            $"amc-sanitized-mp3-{Guid.NewGuid():N}.mp3");
        try
        {
            var unusual = new byte[37 + data.Length];
            Array.Fill<byte>(unusual, 0x55, 0, 37);
            data.CopyTo(unusual, 37);
            File.WriteAllBytes(sanitizedPath, unusual);
            var unusualProbe = AccessibleMediaController.Core.LocalMedia.Mp3StructureProbe.Probe(
                sanitizedPath);
            Assert(
                WindowsMediaOutput.ShouldPreferManagedMp3ForPlayback(
                    allowManagedMp3Fallback: true,
                    mayRequireRemoteAccess: false,
                    unusualProbe),
                "Nietypowy, dostępny lokalnie MP3 nie wybiera od razu odpornego dekodera z indeksem ramek.");
            Assert(
                !WindowsMediaOutput.ShouldPreferManagedMp3ForPlayback(
                    allowManagedMp3Fallback: true,
                    mayRequireRemoteAccess: true,
                    unusualProbe),
                "Plik wymagający pobierania z chmury został skierowany do pełnego indeksowania przed pobraniem.");
            var result = WindowsMediaOutput.TryReadMetadataAsync(
                    sanitizedPath,
                    TimeSpan.FromSeconds(3))
                .GetAwaiter()
                .GetResult();
            Assert(result.Success && !result.TimedOut, "Oczyszczony strumień MP3 nie został zdekodowany.");
        }
        finally
        {
            if (File.Exists(sanitizedPath)) File.Delete(sanitizedPath);
        }
        Console.WriteLine("OK: zarządzany dekoder awaryjny MP3");
    }
    finally
    {
        if (File.Exists(path)) File.Delete(path);
    }
}

static void TestWaveMetadataAndDamagedContainers()
{
    var directory = Path.Combine(
        Path.GetTempPath(),
        $"amc-format-resilience-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        var wavePath = Path.Combine(directory, "valid.wav");
        using (var writer = new WaveFileWriter(wavePath, new WaveFormat(44_100, 16, 2)))
        {
            writer.Write(new byte[44_100 * 2 * 2 / 10]);
        }
        Assert(
            WindowsMediaOutput.TryReadMetadata(wavePath, out var duration, out var sampleRate),
            "Nie odczytano prawidłowego WAV.");
        Assert(duration > TimeSpan.Zero && sampleRate == 44_100, "WAV podał błędne parametry.");

        var damagedFiles = new Dictionary<string, byte[]>
        {
            ["cut.wav"] = "RIFF\0\0\0\0WAVE"u8.ToArray(),
            ["cut.flac"] = "fLaC\0\0\0\0"u8.ToArray(),
            ["cut.ogg"] = "OggS\0\0\0\0"u8.ToArray()
        };
        foreach (var (name, bytes) in damagedFiles)
        {
            var path = Path.Combine(directory, name);
            File.WriteAllBytes(path, bytes);
            var stopwatch = Stopwatch.StartNew();
            var result = WindowsMediaOutput.TryReadMetadataAsync(path, TimeSpan.FromSeconds(2))
                .GetAwaiter()
                .GetResult();
            stopwatch.Stop();
            Assert(!result.Success, $"Zaakceptowano ucięty kontener: {name}.");
            Assert(stopwatch.Elapsed < TimeSpan.FromSeconds(3), $"Ucięty kontener zablokował test: {name}.");
        }
        Console.WriteLine("OK: prawidłowy WAV i bezpieczne odrzucanie uciętych kontenerów");
    }
    finally
    {
        Directory.Delete(directory, true);
    }
}

static void TestLocalVideoAudioExtraction()
{
    var ffmpeg = FfmpegRadioWaveProvider.FindExecutable();
    if (ffmpeg is null)
    {
        Console.WriteLine("POMINIĘTO: próba lokalnego MP4 wymaga FFmpeg do utworzenia pliku testowego");
        return;
    }

    var path = Path.Combine(Path.GetTempPath(), $"amc-video-audio-{Guid.NewGuid():N}.mp4");
    try
    {
        var start = new ProcessStartInfo
        {
            FileName = ffmpeg,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true
        };
        foreach (var argument in new[]
        {
            "-nostdin", "-hide_banner", "-loglevel", "error", "-y",
            "-f", "lavfi", "-i", "color=c=black:s=160x90:d=1",
            "-f", "lavfi", "-i", "sine=frequency=440:duration=1:sample_rate=48000",
            "-shortest", "-c:v", "mpeg4", "-c:a", "aac", "-b:a", "128k", path
        })
        {
            start.ArgumentList.Add(argument);
        }
        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("Nie uruchomiono generatora MP4.");
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit(15_000);
        Assert(process.HasExited && process.ExitCode == 0, $"Nie utworzono MP4: {error}");
        Assert(LocalAudioFileDiscovery.IsAudioFile(path), "MP4 nie trafił do lokalnych multimediów.");
        Assert(LocalAudioFileDiscovery.IsVideoFile(path), "MP4 nie został rozpoznany jako kontener wideo.");
        Assert(
            WindowsMediaOutput.TryReadMetadata(path, out var duration, out var sampleRate),
            "Nie odczytano ścieżki audio z MP4.");
        Assert(duration > TimeSpan.Zero && sampleRate == 48_000, "MP4 podał nieprawidłowe parametry audio.");
        Console.WriteLine("OK: lokalny MP4 jest odtwarzany jako ścieżka audio");
    }
    finally
    {
        if (File.Exists(path)) File.Delete(path);
    }
}

static void TestLocalTransportStreamRecovery()
{
    var ffmpeg = FfmpegRadioWaveProvider.FindExecutable();
    if (ffmpeg is null)
    {
        Console.WriteLine("POMINIĘTO: próba odzyskiwania TS wymaga FFmpeg");
        return;
    }

    var directory = Path.Combine(
        Path.GetTempPath(),
        $"amc-ts-recovery-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    var tsPath = Path.Combine(directory, "transmisja.ts");
    var partialPath = Path.Combine(directory, "transmisja.part");
    try
    {
        var start = new ProcessStartInfo
        {
            FileName = ffmpeg,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true
        };
        foreach (var argument in new[]
        {
            "-nostdin", "-hide_banner", "-loglevel", "error", "-y",
            "-f", "lavfi", "-i", "color=c=black:s=160x90:d=2",
            "-f", "lavfi", "-i", "sine=frequency=440:duration=2:sample_rate=48000",
            "-shortest", "-c:v", "mpeg4", "-c:a", "aac", "-b:a", "128k",
            "-f", "mpegts", tsPath
        })
        {
            start.ArgumentList.Add(argument);
        }
        using (var process = Process.Start(start)
            ?? throw new InvalidOperationException("Nie uruchomiono generatora TS."))
        {
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit(15_000);
            Assert(process.HasExited && process.ExitCode == 0, $"Nie utworzono TS: {error}");
        }

        Assert(
            FfmpegLocalAudioWaveStream.TryOpen(tsPath, out var transportReader),
            "Odporny dekoder nie otworzył gotowego TS.");
        using (transportReader)
        {
            Assert(transportReader.TotalTime > TimeSpan.FromSeconds(1),
                "TS ma nieprawidłowy czas.");
            var buffer = new byte[32_768];
            Assert(transportReader.Read(buffer, 0, buffer.Length) > 0,
                "TS nie zwrócił dźwięku.");
            transportReader.CurrentTime = TimeSpan.FromMilliseconds(750);
            Assert(transportReader.Read(buffer, 0, buffer.Length) > 0,
                "Nie udało się przewinąć ścieżki audio TS.");
        }

        File.Copy(tsPath, partialPath);
        using (var partial = new FileStream(partialPath, FileMode.Open, FileAccess.Write, FileShare.Read))
        {
            partial.SetLength(Math.Max(188, partial.Length - 188 * 7));
        }
        Assert(
            FfmpegLocalAudioWaveStream.TryOpen(partialPath, out var partialReader),
            "Odporny dekoder nie otworzył ręcznie wskazanego pliku PART.");
        using (partialReader)
        {
            var buffer = new byte[32_768];
            Assert(partialReader.Read(buffer, 0, buffer.Length) > 0,
                "Niedokończony plik PART nie zwrócił dostępnego dźwięku.");
        }
        Console.WriteLine("OK: odporny odczyt audio z TS i niedokończonego PART");
    }
    finally
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }
}

static void TestFfmpegLocalFile(string path)
{
    Assert(File.Exists(path), $"Nie znaleziono pliku do testu: {path}");
    Assert(
        FfmpegLocalAudioWaveStream.TryOpen(path, out var reader),
        $"Odporny dekoder nie otworzył pliku: {path}");
    using (reader)
    {
        Assert(reader.TotalTime > TimeSpan.Zero, "Odporny dekoder nie podał czasu.");
        var buffer = new byte[32_768];
        Assert(reader.Read(buffer, 0, buffer.Length) > 0, "Początek nie zwrócił dźwięku.");
        reader.CurrentTime = TimeSpan.FromTicks(reader.TotalTime.Ticks / 2);
        Assert(reader.Read(buffer, 0, buffer.Length) > 0, "Środek pliku nie zwrócił dźwięku.");
        Console.WriteLine($"OK: rzeczywisty plik TS, czas {reader.TotalTime}");
    }
}

static void TestRadioBrowserSearchMapping()
{
    const string response = """
        [{
          "stationuuid":"abc-123",
          "name":"Radio Testowe",
          "url":"http://example.test/original",
          "url_resolved":"https://example.test/live.aac",
          "homepage":"https://example.test",
          "country":"Polska",
          "language":"polski",
          "tags":"informacje,kultura",
          "codec":"AAC",
          "bitrate":192,
          "votes":42,
          "lastcheckok":1
        }]
        """;
    using var client = new HttpClient(new FixedJsonHandler(response));
    using var catalog = new RadioBrowserClient(client);
    var stations = catalog.SearchAsync("test").GetAwaiter().GetResult();
    Assert(stations.Count == 1, "Nie scalono powtarzającego się wyniku katalogu radia.");
    var station = stations[0];
    Assert(station.Title == "Radio Testowe" && station.Kind == AccessibleMediaController.Core.Sessions.MediaItemKind.Station,
        "Nie odwzorowano nazwy lub rodzaju stacji.");
    Assert(station.Source == "https://example.test/live.aac" && station.BitrateKbps == 192,
        "Nie wybrano rozwiązanego strumienia lub bitrate stacji.");
    Assert(station.Country == "Polska" && station.Language == "polski" && station.Codec == "AAC",
        "Nie zachowano informacji katalogowych stacji.");
    Assert(!station.IsInLibrary && !station.IsFavorite,
        "Wynik wyszukiwania nie może samoczynnie trafić do Biblioteki lub Ulubionych.");
    Console.WriteLine("OK: wyszukiwanie i mapowanie katalogu Radio Browser");
}

static void TestRadioScheduleStationScope()
{
    var selected = Station("radio:selected", "Ulubiona stacja", "https://example.invalid/selected", favorite: true);
    var secondVisible = Station("radio:visible", "Druga widoczna", "https://example.invalid/visible");
    var hiddenCatalog = Station("radio:catalog", "Obcy wynik katalogu", "https://example.invalid/catalog");
    var library = Station("radio:library", "Stacja Biblioteki", "https://example.invalid/library", library: true);
    var scheduledOnly = new RadioRecordingScheduleSettings
    {
        StationId = "radio:scheduled",
        StationName = "Stacja istniejącego planu",
        StreamUrl = "https://example.invalid/scheduled"
    };

    var direct = RadioScheduleStationSelection.ForCurrentView(
        [selected, secondVisible],
        selected);
    Assert(direct.Select(item => item.Id).SequenceEqual([selected.Id, secondVisible.Id]),
        "Shift+R nie zachował dokładnego zakresu i kolejności bieżącego widoku.");
    Assert(direct.All(item => item.Id != hiddenCatalog.Id),
        "Shift+R wpuścił niewidoczny wynik katalogu Radio Browser.");

    var manager = RadioScheduleStationSelection.ForScheduleManager(
        [selected, secondVisible, hiddenCatalog, library],
        [selected],
        selected,
        [scheduledOnly]);
    Assert(manager.Any(item => item.Id == selected.Id), "Menedżer zgubił widoczną stację.");
    Assert(manager.Any(item => item.Id == library.Id), "Menedżer zgubił stację Biblioteki.");
    Assert(manager.Any(item => item.Id == scheduledOnly.StationId), "Menedżer zgubił stację istniejącego planu.");
    Assert(manager.All(item => item.Id != hiddenCatalog.Id),
        "Menedżer pokazał nieużywany, ukryty wynik katalogu Radio Browser.");
    Console.WriteLine("OK: Shift+R używa stacji z bieżącego widoku, nie całego katalogu sesji");

    static MediaItem Station(
        string id,
        string title,
        string source,
        bool favorite = false,
        bool library = false) => new()
    {
        Id = id,
        Title = title,
        Kind = MediaItemKind.Station,
        Source = source,
        IsFavorite = favorite,
        IsInLibrary = library,
        IsAvailable = true
    };
}

static void TestRadioPlaylistImport()
{
    var directory = Path.Combine(Path.GetTempPath(), $"amc-radio-import-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        var m3u = Path.Combine(directory, "stacje.m3u");
        File.WriteAllText(m3u, "#EXTM3U\n#EXTINF:-1,Radio Pierwsze\nhttps://radio.example/one.mp3\n#EXTINF:-1,Radio Drugie\nhttp://radio.example/two.aac\n");
        var m3uResult = RadioPlaylistImporter.Import(m3u);
        Assert(m3uResult.Stations.Count == 2, "Nie zaimportowano obu wpisów M3U.");
        Assert(m3uResult.Stations[0].Name == "Radio Pierwsze", "Nie zachowano nazwy stacji M3U.");

        var catalogStation = new AccessibleMediaController.Core.Sessions.MediaItem
        {
            Id = "radio:katalog",
            Title = "Pełna nazwa katalogowa",
            Kind = AccessibleMediaController.Core.Sessions.MediaItemKind.Station,
            Source = "https://radio.example/one.mp3",
            BitrateKbps = 192,
            IsInLibrary = false
        };
        var merge = RadioLibraryMerge.Apply([catalogStation], m3uResult);
        Assert(merge.Promoted.Count == 1 && merge.Added.Count == 1,
            "Import nie włączył istniejącej stacji katalogowej do Biblioteki.");
        Assert(catalogStation.IsInLibrary && catalogStation.Title == "Pełna nazwa katalogowa"
               && catalogStation.BitrateKbps == 192,
            "Scalenie importu utraciło nazwę albo parametry istniejącej stacji.");

        var pls = Path.Combine(directory, "stacje.pls");
        File.WriteAllText(pls, "[playlist]\nFile1=https://radio.example/live\nTitle1=Radio PLS\nNumberOfEntries=1\n");
        var plsResult = RadioPlaylistImporter.Import(pls);
        Assert(plsResult.Stations.Single().Name == "Radio PLS", "Nie zaimportowano nazwy PLS.");

        var xspf = Path.Combine(directory, "stacje.xspf");
        File.WriteAllText(xspf, "<playlist xmlns=\"http://xspf.org/ns/0/\"><trackList><track><title>Radio XSPF</title><location>https://radio.example/xspf</location></track></trackList></playlist>");
        var xspfResult = RadioPlaylistImporter.Import(xspf);
        Assert(xspfResult.Stations.Single().Name == "Radio XSPF", "Nie zaimportowano wpisu XSPF.");

        var hls = Path.Combine(directory, "transmisja.m3u8");
        File.WriteAllText(hls, "#EXTM3U\n#EXT-X-VERSION:3\n#EXT-X-MEDIA-SEQUENCE:1\nsegment1.aac\n");
        try
        {
            _ = RadioPlaylistImporter.Import(hls);
            throw new InvalidOperationException("Manifest HLS został błędnie zaimportowany jako lista stacji.");
        }
        catch (InvalidDataException)
        {
        }

        var json = Path.Combine(directory, "vradio.json");
        File.WriteAllText(json, """
            {"stations":[
              {"name":"Radio VRadio","streams":[{"url":"https://radio.example/vradio"}]},
              {"name":"Nieprawidłowy wpis","streams":[{"url":"tekst z prywatnymi danymi zamiast adresu"}]},
              {"name":"Duplikat","streams":[{"url":"https://radio.example/vradio"}]}
            ]}
            """);
        var jsonResult = RadioPlaylistImporter.Import(json);
        Assert(jsonResult.Stations.Count == 1, "Importer VRadio nie usunął nieprawidłowego wpisu lub duplikatu.");
        Assert(jsonResult.SkippedEntries == 2, "Importer VRadio podał złą liczbę pominiętych wpisów.");
        Console.WriteLine("OK: bezpieczny import M3U, PLS, XSPF i VRadio JSON");
    }
    finally
    {
        Directory.Delete(directory, true);
    }
}

static void TestRadioPlaylistResolution()
{
    var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    var endpoint = (IPEndPoint)listener.LocalEndpoint;
    var baseAddress = $"http://127.0.0.1:{endpoint.Port}";
    var server = Task.Run(async () =>
    {
        for (var requestIndex = 0; requestIndex < 4; requestIndex++)
        {
            using var connection = await listener.AcceptTcpClientAsync();
            await using var stream = connection.GetStream();
            using var reader = new StreamReader(
                stream,
                Encoding.ASCII,
                detectEncodingFromByteOrderMarks: false,
                leaveOpen: true);
            var requestLine = await reader.ReadLineAsync() ?? string.Empty;
            string? header;
            do
            {
                header = await reader.ReadLineAsync();
            }
            while (!string.IsNullOrEmpty(header));

            var path = requestLine.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .ElementAtOrDefault(1) ?? string.Empty;
            var (status, contentType, location, body) = path switch
            {
                "/outer.pls" => ("200 OK", "audio/x-scpls", string.Empty,
                    "[playlist]\r\nFile1=/nested/list.m3u\r\nNumberOfEntries=1\r\n"),
                "/nested/list.m3u" => ("200 OK", "audio/x-mpegurl", string.Empty,
                    "#EXTM3U\r\n../live.mp3\r\n"),
                "/redirect.pls" => ("302 Found", "text/plain", "/direct-live", string.Empty),
                "/direct-live" => ("200 OK", "audio/mpeg", string.Empty, string.Empty),
                _ => ("404 Not Found", "text/plain", string.Empty, string.Empty)
            };
            var bodyBytes = Encoding.UTF8.GetBytes(body);
            var response = new StringBuilder()
                .Append("HTTP/1.1 ").Append(status).Append("\r\n")
                .Append("Content-Type: ").Append(contentType).Append("\r\n")
                .Append("Content-Length: ").Append(bodyBytes.Length).Append("\r\n")
                .Append("Connection: close\r\n");
            if (location.Length > 0) response.Append("Location: ").Append(location).Append("\r\n");
            response.Append("\r\n");
            var headerBytes = Encoding.ASCII.GetBytes(response.ToString());
            await stream.WriteAsync(headerBytes);
            if (bodyBytes.Length > 0) await stream.WriteAsync(bodyBytes);
        }
    });

    try
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var nested = RadioStreamResolver.ResolveAsync(
                $"{baseAddress}/outer.pls",
                cancellation.Token)
            .GetAwaiter()
            .GetResult();
        Assert(nested == $"{baseAddress}/live.mp3",
            $"Nie rozwiązano zagnieżdżonej playlisty: {nested}.");

        var redirected = RadioStreamResolver.ResolveAsync(
                $"{baseAddress}/redirect.pls",
                cancellation.Token)
            .GetAwaiter()
            .GetResult();
        Assert(redirected == $"{baseAddress}/direct-live",
            $"Nie zachowano przekierowania playlisty do strumienia audio: {redirected}.");
        Assert(server.Wait(TimeSpan.FromSeconds(2)), "Testowy serwer playlist nie zakończył pracy.");
        Console.WriteLine("OK: zagnieżdżone playlisty i przekierowanie do strumienia audio");
    }
    finally
    {
        listener.Stop();
        _ = server.ContinueWith(_ => { }, TaskScheduler.Default);
    }
}

static void TestRadioAudioMetadataValidation()
{
    Assert(BassRadioWaveProvider.BitrateAttribute == 12,
        "Adapter BASS używa nieprawidłowego identyfikatora bitrate.");
    Assert(RadioAudioMetadataRules.NormalizeBitrateKbps(192) == 192,
        "Prawidłowy bitrate radia został odrzucony.");
    Assert(RadioAudioMetadataRules.NormalizeBitrateKbps(44_100) is null,
        "Częstotliwość próbkowania została błędnie zaakceptowana jako bitrate.");
    Assert(RadioAudioMetadataRules.NormalizeBitrateKbps(null) is null,
        "Brak bitrate nie powinien tworzyć wartości.");

    var audioMaster = RadioStreamMetadataProbe.ParseHlsManifest("""
        #EXTM3U
        #EXT-X-STREAM-INF:BANDWIDTH=192000,CODECS="mp4a.40.2"
        audio.m3u8
        """);
    Assert(audioMaster.BitrateKbps == 192 && audioMaster.Codec == "AAC",
        "Manifest audio HLS nie podał przepływności i kodeka.");

    var videoMaster = RadioStreamMetadataProbe.ParseHlsManifest("""
        #EXTM3U
        #EXT-X-STREAM-INF:BANDWIDTH=2500000,CODECS="avc1.4d401f,mp4a.40.2"
        video.m3u8
        """);
    Assert(videoMaster.BitrateKbps is null,
        "Przepływność obrazu HLS została błędnie podana jako przepływność radia.");

    var mediaManifest = RadioStreamMetadataProbe.ParseHlsManifest("""
        #EXTM3U
        #EXT-X-TARGETDURATION:7
        #EXTINF:6.4, no desc
        radio-audio=128000-123.ts
        """);
    Assert(mediaManifest.SegmentDurationSeconds == 6.4
        && mediaManifest.SegmentUri == "radio-audio=128000-123.ts",
        "Manifest HLS nie wskazał bezpiecznie pierwszego segmentu.");

    Assert(RadioStreamMetadataProbe.BitrateFromUri(new Uri(
            "https://audio.example/live/radio-audio%3d128000.norewind.m3u8")) == 128,
        "Nie rozpoznano bitrate zadeklarowanego w adresie wariantu HLS.");

    var adts = CreateAdtsFrames(frameLength: 100, frameCount: 4, sampleRateIndex: 4);
    var detectedAac = RadioStreamMetadataProbe.DetectAudioSample(adts);
    Assert(detectedAac?.Codec == "AAC"
        && detectedAac.SampleRateHz == 44_100
        && detectedAac.BitrateKbps is > 20 and < 50,
        "Ograniczona próbka AAC nie podała wiarygodnych parametrów.");
    Console.WriteLine("OK: walidacja bitrate radia i stała BASS");
}

static void TestRadioStreamTitleMetadata()
{
    Assert(
        RadioStreamTitleMetadata.ParseIcyText(
            "StreamTitle='Wykonawca - Tytuł';StreamUrl='';") == "Wykonawca - Tytuł",
        "Nie odczytano tytułu z metadanych ICY.");
    Assert(
        RadioStreamTitleMetadata.ParseIcyText("StreamTitle='';") is null,
        "Pusty tytuł ICY nie został wyczyszczony.");
    Assert(
        RadioStreamTitleMetadata.ParseOggTags(["ARTIST=Artysta", "TITLE=Audycja"])
            == "Artysta — Audycja",
        "Nie połączono wykonawcy i tytułu z komentarzy OGG.");
    Assert(
        RadioStreamTitleMetadata.Normalize("  Bezpieczny\r\n  tytuł\0 ") == "Bezpieczny tytuł",
        "Nie usunięto znaków sterujących z tytułu okna.");
    Console.WriteLine("OK: bezpieczne metadane bieżącego utworu radia");
}

static void TestLiveRadioMetadata(string url)
{
    var metadata = RadioMediaOutput.TryReadStreamMetadataAsync(url, TimeSpan.FromSeconds(12))
        .GetAwaiter()
        .GetResult();
    if (metadata?.BitrateKbps is not > 0)
    {
        throw new InvalidOperationException(
            "Aktywny strumień nie ujawnił wiarygodnej przepływności.");
    }
    Console.WriteLine(
        $"OK: aktywny strumień podał {metadata.BitrateKbps} kb/s, {metadata.Codec ?? "format nieznany"}");
}

static byte[] CreateAdtsFrames(int frameLength, int frameCount, int sampleRateIndex)
{
    var result = new byte[frameLength * frameCount];
    for (var frame = 0; frame < frameCount; frame++)
    {
        var offset = frame * frameLength;
        result[offset] = 0xFF;
        result[offset + 1] = 0xF1;
        result[offset + 2] = (byte)((1 << 6) | (sampleRateIndex << 2));
        result[offset + 3] = (byte)((2 << 6) | ((frameLength >> 11) & 0x03));
        result[offset + 4] = (byte)((frameLength >> 3) & 0xFF);
        result[offset + 5] = (byte)(((frameLength & 0x07) << 5) | 0x1F);
        result[offset + 6] = 0xFC;
    }
    return result;
}

static void TestLegacyRadioContentTypes()
{
    Assert(LegacyIcyMp3StreamReader.IsSupportedMp3ContentType("audio/mpeg"), "Odrzucono MIME MP3.");
    Assert(LegacyIcyMp3StreamReader.IsSupportedMp3ContentType("audio/mp3; charset=binary"), "Odrzucono MIME audio/mp3.");
    Assert(!LegacyIcyMp3StreamReader.IsSupportedMp3ContentType("audio/aacp"), "Awaryjny dekoder MP3 zaakceptował AAC+.");
    Assert(!LegacyIcyMp3StreamReader.IsSupportedMp3ContentType("audio/ogg"), "Awaryjny dekoder MP3 zaakceptował OGG.");
    Console.WriteLine("OK: awaryjny dekoder ICY nie myli AAC i OGG z MP3");
}

static void TestRadioMp3Recording()
{
    var directory = Path.Combine(
        Path.GetTempPath(),
        $"amc-radio-recording-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    var mp3Path = Path.Combine(directory, "radio-test.mp3");
    try
    {
        var format = new WaveFormat(44_100, 16, 2);
        using var recorder = RadioMp3Recorder.Start(mp3Path, format);
        var pcm = new byte[format.AverageBytesPerSecond];
        for (var frame = 0; frame < format.SampleRate; frame++)
        {
            var sample = (short)(Math.Sin(2 * Math.PI * 440 * frame / format.SampleRate) * short.MaxValue * 0.1);
            var offset = frame * format.BlockAlign;
            BinaryPrimitives.WriteInt16LittleEndian(pcm.AsSpan(offset, 2), sample);
            BinaryPrimitives.WriteInt16LittleEndian(pcm.AsSpan(offset + 2, 2), sample);
        }
        recorder.Write(pcm, 0, pcm.Length);
        var result = recorder.Stop();

        Assert(result == mp3Path && File.Exists(mp3Path), "Nie opublikowano gotowego nagrania MP3.");
        Assert(!File.Exists(mp3Path + ".amc-partial"), "Pozostał tymczasowy plik nagrania.");
        using var reader = new MediaFoundationReader(mp3Path);
        Assert(reader.TotalTime > TimeSpan.FromMilliseconds(500), "Nagranie MP3 ma nieprawidłowy czas.");
        Assert(reader.WaveFormat.SampleRate == 44_100, "Nagranie MP3 ma nieprawidłową częstotliwość.");
        using var frameStream = File.OpenRead(mp3Path);
        var firstFrame = Mp3Frame.LoadFromStream(frameStream);
        Assert(firstFrame?.BitRate == RadioMp3Recorder.DesiredBitRate,
            $"Nagranie MP3 ma bitrate {firstFrame?.BitRate ?? 0} zamiast {RadioMp3Recorder.DesiredBitRate}.");

        var aacPath = Path.Combine(directory, "radio-test.m4a");
        using (var aacRecorder = RadioMp3Recorder.Start(
                   aacPath,
                   format,
                   RadioRecordingFormat.Aac,
                   160))
        {
            aacRecorder.Write(pcm, 0, pcm.Length);
            aacRecorder.Stop();
        }
        using (var aacReader = new MediaFoundationReader(aacPath))
        {
            Assert(aacReader.TotalTime > TimeSpan.FromMilliseconds(500),
                "Nagranie M4A/AAC ma nieprawidłowy czas.");
        }

        var narrowFormat = new WaveFormat(24_000, 16, 1);
        var narrowPcm = new byte[narrowFormat.AverageBytesPerSecond];
        for (var frame = 0; frame < narrowFormat.SampleRate; frame++)
        {
            var sample = (short)(Math.Sin(2 * Math.PI * 440 * frame / narrowFormat.SampleRate)
                * short.MaxValue * 0.1);
            BinaryPrimitives.WriteInt16LittleEndian(
                narrowPcm.AsSpan(frame * narrowFormat.BlockAlign, 2),
                sample);
        }
        var narrowAacPath = Path.Combine(directory, "radio-test-24-khz.m4a");
        using (var narrowAacRecorder = RadioMp3Recorder.Start(
                   narrowAacPath,
                   narrowFormat,
                   RadioRecordingFormat.Aac,
                   128))
        {
            narrowAacRecorder.Write(narrowPcm, 0, narrowPcm.Length);
            narrowAacRecorder.Stop();
        }
        using (var narrowAacReader = new MediaFoundationReader(narrowAacPath))
        {
            Assert(narrowAacReader.TotalTime > TimeSpan.FromMilliseconds(500),
                "Nagranie M4A/AAC ze stacji 24 kHz ma nieprawidłowy czas.");
        }

        TestNarrowRadioMp3Recording(directory, 22_050, 44_100, "357");
        TestNarrowRadioMp3Recording(directory, 24_000, 48_000, "Białystok");

        var pausedWavPath = Path.Combine(directory, "radio-test-pause.wav");
        using (var pausedRecorder = RadioMp3Recorder.Start(
                   pausedWavPath,
                   format,
                   RadioRecordingFormat.Wav,
                   192))
        {
            Assert(pausedRecorder.CanPause && !pausedRecorder.IsPaused,
                "Nagrywarka kodowana nie zgłasza gotowości do pauzy.");
            pausedRecorder.Write(pcm, 0, pcm.Length);
            pausedRecorder.Pause();
            Assert(pausedRecorder.IsPaused,
                "Nagrywarka nie zapamiętała pauzy.");
            pausedRecorder.Write(pcm, 0, pcm.Length);
            Assert(Math.Abs(pausedRecorder.RecordedDuration.TotalSeconds - 1) < 0.05,
                "Dźwięk odebrany podczas pauzy został dopisany do czasu nagrania.");
            pausedRecorder.Resume();
            pausedRecorder.Write(pcm, 0, pcm.Length);
            Assert(Math.Abs(pausedRecorder.RecordedDuration.TotalSeconds - 2) < 0.05,
                "Nagrywanie nie zostało prawidłowo wznowione.");
            pausedRecorder.Stop();
        }
        using (var pausedWavReader = new WaveFileReader(pausedWavPath))
        {
            Assert(pausedWavReader.TotalTime > TimeSpan.FromMilliseconds(1900)
                   && pausedWavReader.TotalTime < TimeSpan.FromMilliseconds(2100),
                $"Pauza utworzyła nieprawidłową długość WAV: {pausedWavReader.TotalTime}.");
        }

        var wavPath = Path.Combine(directory, "radio-test.wav");
        using (var wavRecorder = RadioMp3Recorder.Start(
                   wavPath,
                   format,
                   RadioRecordingFormat.Wav,
                   192))
        {
            wavRecorder.Write(pcm, 0, pcm.Length);
            wavRecorder.Stop();
        }
        using (var wavReader = new WaveFileReader(wavPath))
        {
            Assert(wavReader.TotalTime > TimeSpan.FromMilliseconds(900),
                "Nagranie WAV ma nieprawidłowy czas.");
            Assert(wavReader.WaveFormat.SampleRate == 44_100,
                "Nagranie WAV ma nieprawidłową częstotliwość.");
        }

        if (FfmpegRadioWaveProvider.FindExecutable() is not null)
        {
            var flacPath = Path.Combine(directory, "radio-test.flac");
            using (var flacRecorder = RadioMp3Recorder.Start(
                       flacPath,
                       format,
                       RadioRecordingFormat.Flac,
                       192))
            {
                flacRecorder.Write(pcm, 0, pcm.Length);
                flacRecorder.Stop();
            }
            var flacHeader = File.ReadAllBytes(flacPath).AsSpan(0, 4);
            Assert(flacHeader.SequenceEqual("fLaC"u8), "Nagranie FLAC nie ma prawidłowego nagłówka.");

            TestOriginalRadioRecording(mp3Path, directory);
        }

        Assert(
            RadioOriginalStreamRecorder.Describe("https://example.test/live.m3u8", "AAC")
                is { Extension: ".ts", Muxer: "mpegts" },
            "HLS nie otrzymał bezkonwersyjnego kontenera transportowego.");
        Assert(
            RadioOriginalStreamRecorder.Describe("https://example.test/live", "AAC+")
                is { Extension: ".aac", Muxer: "adts" },
            "Bezpośredni AAC nie otrzymał oryginalnego kontenera ADTS.");

        var shutdownPath = Path.Combine(directory, "zamkniecie.mp3");
        using (var shutdownRecorder = RadioMp3Recorder.Start(shutdownPath, format))
        {
            shutdownRecorder.Write(pcm, 0, pcm.Length / 2);
        }
        Assert(File.Exists(shutdownPath) && !File.Exists(shutdownPath + ".amc-partial"),
            "Zamykanie programu nie zakończyło nagrania MP3.");

        var abortedPath = Path.Combine(directory, "przerwane.mp3");
        using (var abortedRecorder = RadioMp3Recorder.Start(abortedPath, format))
        {
            abortedRecorder.Write(pcm, 0, pcm.Length / 4);
            abortedRecorder.Abort();
        }
        Assert(!File.Exists(abortedPath) && !File.Exists(abortedPath + ".amc-partial"),
            "Przerwane kodowanie pozostawiło plik udający gotowe nagranie.");
        Console.WriteLine("OK: nagrywanie radia do MP3, M4A/AAC, FLAC, WAV i oryginalnego strumienia");
    }
    finally
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }
}

static void TestNarrowRadioMp3Recording(
    string directory,
    int sourceSampleRate,
    int expectedSampleRate,
    string stationName)
{
    var format = new WaveFormat(sourceSampleRate, 16, 2);
    var pcm = new byte[format.AverageBytesPerSecond];
    for (var frame = 0; frame < format.SampleRate; frame++)
    {
        var sample = (short)(Math.Sin(2 * Math.PI * 440 * frame / format.SampleRate)
            * short.MaxValue * 0.1);
        var offset = frame * format.BlockAlign;
        BinaryPrimitives.WriteInt16LittleEndian(pcm.AsSpan(offset, 2), sample);
        BinaryPrimitives.WriteInt16LittleEndian(pcm.AsSpan(offset + 2, 2), sample);
    }

    var path = Path.Combine(directory, $"radio-test-{sourceSampleRate}.mp3");
    using (var recorder = RadioMp3Recorder.Start(
               path,
               format,
               RadioRecordingFormat.Mp3,
               128))
    {
        recorder.Write(pcm, 0, pcm.Length);
        recorder.Stop();
    }

    using (var reader = new MediaFoundationReader(path))
    {
        Assert(reader.TotalTime > TimeSpan.FromMilliseconds(500),
            $"Nagranie MP3 stacji {stationName} ma nieprawidłowy czas.");
        Assert(reader.WaveFormat.SampleRate == expectedSampleRate,
            $"Nagranie MP3 stacji {stationName} ma częstotliwość {reader.WaveFormat.SampleRate} zamiast {expectedSampleRate} Hz.");
    }
    using var frameStream = File.OpenRead(path);
    var firstFrame = Mp3Frame.LoadFromStream(frameStream);
    Assert(firstFrame?.BitRate == 128_000,
        $"Nagranie MP3 stacji {stationName} ma bitrate {firstFrame?.BitRate ?? 0} zamiast 128000.");
}

static void TestOriginalRadioRecording(string sourceMp3Path, string outputDirectory)
{
    var sourceBytes = File.ReadAllBytes(sourceMp3Path);
    var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    var endpoint = (IPEndPoint)listener.LocalEndpoint;
    using var serverCancellation = new CancellationTokenSource();
    var server = Task.Run(async () =>
    {
        try
        {
            using var connection = await listener.AcceptTcpClientAsync(serverCancellation.Token);
            await using var stream = connection.GetStream();
            var request = new byte[4_096];
            var requestLength = 0;
            while (requestLength < request.Length)
            {
                var read = await stream.ReadAsync(
                    request.AsMemory(requestLength, 1),
                    serverCancellation.Token);
                if (read == 0) return;
                requestLength += read;
                if (requestLength >= 4
                    && request[requestLength - 4] == '\r'
                    && request[requestLength - 3] == '\n'
                    && request[requestLength - 2] == '\r'
                    && request[requestLength - 1] == '\n') break;
            }
            var header = Encoding.ASCII.GetBytes(
                "HTTP/1.1 200 OK\r\n" +
                "Content-Type: audio/mpeg\r\n" +
                "Connection: close\r\n\r\n");
            await stream.WriteAsync(header, serverCancellation.Token);
            while (!serverCancellation.IsCancellationRequested)
            {
                await stream.WriteAsync(sourceBytes, serverCancellation.Token);
                await stream.FlushAsync(serverCancellation.Token);
                await Task.Delay(10, serverCancellation.Token);
            }
        }
        catch (Exception exception) when (exception is OperationCanceledException
            or IOException
            or ObjectDisposedException
            or SocketException)
        {
        }
    });

    var source = $"http://127.0.0.1:{endpoint.Port}/live";
    var target = RadioOriginalStreamRecorder.Describe(source, "MP3");
    var outputPath = Path.Combine(outputDirectory, "radio-original.mp3");
    try
    {
        using var recorder = RadioOriginalStreamRecorder.Start(outputPath, source, target);
        Thread.Sleep(1_200);
        recorder.Write([], 0, 0);
        recorder.Stop();
        var saved = File.ReadAllBytes(outputPath);
        Assert(
            AccessibleMediaController.Core.LocalMedia.Mp3StructureProbe.TryFindConsecutiveFrameOffset(saved, out _),
            "Oryginalne nagranie nie zachowało prawidłowych ramek MP3.");
    }
    finally
    {
        serverCancellation.Cancel();
        listener.Stop();
        try { server.Wait(TimeSpan.FromSeconds(3)); } catch (AggregateException) { }
    }
}

static void TestRadioRecordingFolderFallback()
{
    var root = Path.Combine(Path.GetTempPath(), $"amc-radio-folder-{Guid.NewGuid():N}");
    var fallback = Path.Combine(root, "fallback");
    var blocked = Path.Combine(root, "not-a-folder");
    Directory.CreateDirectory(root);
    File.WriteAllText(blocked, "plik blokuje utworzenie folderu");
    try
    {
        var resolution = RadioRecordingFolderResolver.Resolve(blocked, fallback);
        Assert(resolution.UsedFallback, "Niedostępny folder nie uruchomił bezpiecznego fallbacku.");
        Assert(string.Equals(resolution.Path, fallback, StringComparison.OrdinalIgnoreCase),
            "Nagranie nie przeszło do wskazanego folderu zastępczego.");
        Assert(Directory.Exists(fallback), "Folder zastępczy nie został utworzony.");
        Assert(!Directory.EnumerateFiles(fallback, ".amc-write-test-*.tmp").Any(),
            "Po sprawdzeniu zapisu pozostał plik próbny.");
        Console.WriteLine("OK: niedostępny folder nagrania ma bezpieczny fallback");
    }
    finally
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}

static void TestRadioRecordingSplitControl()
{
    var control = new RadioRecordingControl();
    var split = control.SplitRecording();
    Assert(split.Kind == RadioRecordingSplitChangeKind.NotReady,
        "Podział niegotowego nagrania nie został bezpiecznie odrzucony.");
    Assert(control.CompletedPaths.Count == 0 && control.CurrentPath is null,
        "Odrzucony podział ujawnił nieistniejące pliki nagrania.");
    Console.WriteLine("OK: bezpieczne odrzucenie podziału niegotowego nagrania");
}

static void TestRadioRecordingSplitPipeline()
{
    var folder = Path.Combine(Path.GetTempPath(), "amc-radio-split-test");
    var firstPath = Path.Combine(folder, "part-1.wav");
    var backend = new TestRadioRecordingBackend(firstPath);
    var control = new RadioRecordingControl();
    control.Attach(
        backend,
        folder,
        RadioRecordingFormat.Wav,
        192,
        firstPath,
        partNumber => $"Audycja-{partNumber:00}");

    backend.RecentAudio = new RadioAudioSnapshot(
        new byte[48_000 * 2 * 2 * 3],
        new WaveFormat(48_000, 16, 2),
        TimeSpan.FromSeconds(3));
    Assert(control.TryGetRecentAudio(TimeSpan.FromSeconds(3), out var recognitionAudio)
           && recognitionAudio?.Duration == TimeSpan.FromSeconds(3),
        "Kontroler nagrania nie udostępnia bufora rozpoznawaniu bez drugiego połączenia ze stacją.");

    var firstTarget = control.CaptureBookmarkTarget();
    Assert(firstTarget is not null
           && string.Equals(firstTarget.Path, firstPath, StringComparison.OrdinalIgnoreCase)
           && firstTarget.Position == TimeSpan.FromMinutes(1),
        "Szybka zakładka nie pobrała ścieżki i czasu bieżącej części nagrania.");
    var quickBookmark = control.AddBookmark(firstTarget!);
    Assert(quickBookmark.Kind == RadioRecordingBookmarkChangeKind.Added
           && string.IsNullOrEmpty(quickBookmark.Marker.Name),
        "B nie utworzyło nienazwanej zakładki nagrania.");
    var namedBookmark = control.AddBookmark(firstTarget!, "  Ważna   rozmowa  ");
    Assert(namedBookmark.Kind == RadioRecordingBookmarkChangeKind.NameChanged
           && namedBookmark.Marker.Name == "Ważna rozmowa",
        "Shift+B nie nadało nazwy istniejącej zakładce z tej samej sekundy.");
    var duplicateBookmark = control.AddBookmark(firstTarget!, "Ważna rozmowa");
    Assert(duplicateBookmark.Kind == RadioRecordingBookmarkChangeKind.Duplicate,
        "Ponowne B w tej samej sekundzie utworzyło duplikat zakładki nagrania.");

    var pause = control.SetPaused(true);
    Assert(pause.Kind == RadioRecordingPauseChangeKind.Paused && backend.IsRecordingPaused,
        "Testowy backend nie został wstrzymany przed podziałem.");
    var split = control.SplitRecording();

    Assert(split.Kind == RadioRecordingSplitChangeKind.Split,
        $"Nie udało się podzielić rzeczywistego stanu nagrania: {split.Error}");
    Assert(string.Equals(split.CompletedPath, firstPath, StringComparison.OrdinalIgnoreCase),
        "Podział nie opublikował pierwszej części.");
    Assert(!string.Equals(split.CompletedPath, split.CurrentPath, StringComparison.OrdinalIgnoreCase),
        "Nowa część otrzymała tę samą ścieżkę co poprzednia.");
    Assert(string.Equals(
            split.CurrentPath,
            Path.Combine(folder, "Audycja-02.wav"),
            StringComparison.OrdinalIgnoreCase),
        "Nowa część nie użyła szablonu i dwucyfrowego tokenu części.");
    Assert(backend.IsRecording && backend.IsRecordingPaused,
        "Podział nie zachował stanu pauzy w nowej części.");
    Assert(control.CompletedPaths.Count == 1 && control.CurrentPath == split.CurrentPath,
        "Kontroler nie zapamiętał pierwszej i bieżącej części.");

    var repeatedSplit = control.SplitRecording();
    Assert(repeatedSplit.Kind == RadioRecordingSplitChangeKind.TooSoon,
        "Szybkie podwójne T nie zostało bezpiecznie pominięte.");
    Assert(backend.IsRecording
           && control.CompletedPaths.Count == 1
           && string.Equals(control.CurrentPath, split.CurrentPath, StringComparison.OrdinalIgnoreCase),
        "Drugie szybkie T zatrzymało albo ponownie podzieliło świeżą część nagrania.");

    var secondTarget = control.CaptureBookmarkTarget();
    Assert(secondTarget is not null
           && string.Equals(secondTarget.Path, split.CurrentPath, StringComparison.OrdinalIgnoreCase)
           && control.AddBookmark(secondTarget).Kind == RadioRecordingBookmarkChangeKind.Added,
        "Zakładka po podziale nie została przypisana do nowego pliku.");

    var finalPath = control.StopCurrentSegment(backend);
    control.Detach(backend);
    Assert(control.CompletedPaths.Count == 2 && control.CurrentPath is null,
        "Finalizacja po podziale nie zapisała dokładnie dwóch części.");
    Assert(control.Markers.Count == 3
           && control.Markers.Count(marker => string.Equals(
               marker.Path,
               firstPath,
               StringComparison.OrdinalIgnoreCase)) == 2
           && control.Markers.Count(marker => string.Equals(
               marker.Path,
               split.CurrentPath,
               StringComparison.OrdinalIgnoreCase)) == 1,
        "Zakładki i punkt pauzy nie pozostały przypisane do właściwych części.");

    var stoppingBackend = new TestRadioRecordingBackend(firstPath);
    var stoppingControl = new RadioRecordingControl();
    stoppingControl.Attach(stoppingBackend, folder, RadioRecordingFormat.Wav, 192, firstPath);
    stoppingControl.RequestStop();
    var rejectedSplit = stoppingControl.SplitRecording();
    Assert(rejectedSplit.Kind == RadioRecordingSplitChangeKind.StopRequested,
        "Żądanie zatrzymania nie wygrało z późnym podziałem nagrania.");
    Assert(string.Equals(stoppingBackend.CurrentPath, firstPath, StringComparison.OrdinalIgnoreCase),
        "Późny podział po zatrzymaniu uruchomił nową część.");
    stoppingControl.StopCurrentSegment(stoppingBackend);
    stoppingControl.Detach(stoppingBackend);
    Console.WriteLine("OK: zakładki, części i szybkie podwójne T zachowują stan nagrania");
}

static void TestRadioRecordingStagingPublication()
{
    var root = Path.Combine(Path.GetTempPath(), $"amc-radio-publish-{Guid.NewGuid():N}");
    Directory.CreateDirectory(root);
    var finalPath = Path.Combine(root, "nagranie.mp3");
    var stagingPath = RadioRecordingStagingStore.CreatePath(finalPath);
    try
    {
        File.WriteAllText(stagingPath, "nowe nagranie");
        File.WriteAllText(finalPath + ".amc-publishing", "stary plik o innej treści");
        RadioRecordingStagingStore.Publish(stagingPath, finalPath);
        Assert(File.ReadAllText(finalPath) == "nowe nagranie",
            "Publikacja pomyliła nowe nagranie ze starym plikiem chmurowym.");
        Assert(!Directory.EnumerateFiles(root, "*.amc-publishing").Any(path =>
                !string.Equals(path, finalPath + ".amc-publishing", StringComparison.OrdinalIgnoreCase)),
            "Po poprawnej publikacji pozostał unikatowy plik roboczy.");
        Console.WriteLine("OK: lokalne przygotowanie i atomowa publikacja nagrania");
    }
    finally
    {
        RadioRecordingStagingStore.TryDelete(stagingPath);
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}

static void TestScheduledRadioSegmentation()
{
    var whole = TimeSpan.FromHours(2);
    var firstWait = ScheduledRadioRecorder.CalculateNextWait(whole, 15);
    Assert(firstWait == TimeSpan.FromMinutes(15),
        "Dwugodzinny plan nie wyznaczył pierwszej części 15-minutowej.");
    Assert(ScheduledRadioRecorder.ShouldStartNextSegment(
            firstWait,
            whole - firstWait,
            15),
        "Plan nie rozpoczął następnej części po pełnych 15 minutach.");
    Assert(ScheduledRadioRecorder.CalculateNextWait(whole, 0) == whole,
        "Tryb jednego pliku nie zachował całego pozostałego czasu.");
    var finalWait = ScheduledRadioRecorder.CalculateNextWait(TimeSpan.FromMinutes(10), 15);
    Assert(finalWait == TimeSpan.FromMinutes(10)
           && !ScheduledRadioRecorder.ShouldStartNextSegment(
               finalWait,
               TimeSpan.Zero,
               15),
        "Ostatnia krótsza część próbowała uruchomić dodatkowy plik.");
    Console.WriteLine("OK: harmonogram wyznacza pełne i ostatnią krótszą część nagrania");
}

static void TestLegacyIcyMp3Stream()
{
    var mp3Path = Path.Combine(Path.GetTempPath(), $"amc-icy-source-{Guid.NewGuid():N}.mp3");
    byte[] mp3;
    try
    {
        var format = new WaveFormat(44_100, 16, 2);
        using (var recorder = RadioMp3Recorder.Start(mp3Path, format))
        {
            var pcm = new byte[format.AverageBytesPerSecond];
            for (var frame = 0; frame < format.SampleRate; frame++)
            {
                var sample = (short)(Math.Sin(2 * Math.PI * 440 * frame / format.SampleRate) * short.MaxValue * 0.1);
                var offset = frame * format.BlockAlign;
                BinaryPrimitives.WriteInt16LittleEndian(pcm.AsSpan(offset, 2), sample);
                BinaryPrimitives.WriteInt16LittleEndian(pcm.AsSpan(offset + 2, 2), sample);
            }
            recorder.Write(pcm, 0, pcm.Length);
            recorder.Stop();
        }
        mp3 = File.ReadAllBytes(mp3Path);
        Assert(
            AccessibleMediaController.Core.LocalMedia.Mp3StructureProbe.TryFindConsecutiveFrameOffset(mp3, out _),
            "Testowy plik MP3 nie zawiera dwóch rozpoznanych ramek.");
    }
    finally
    {
        if (File.Exists(mp3Path)) File.Delete(mp3Path);
    }

    var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    var endpoint = (IPEndPoint)listener.LocalEndpoint;
    var releaseServer = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var server = Task.Run(async () =>
    {
        using var connection = await listener.AcceptTcpClientAsync();
        await using var stream = connection.GetStream();
        var request = new byte[4_096];
        var requestLength = 0;
        while (requestLength < request.Length)
        {
            var read = await stream.ReadAsync(request.AsMemory(requestLength, 1));
            if (read == 0) throw new EndOfStreamException("Klient nie wysłał pełnego żądania.");
            requestLength += read;
            if (requestLength >= 4
                && request[requestLength - 4] == '\r'
                && request[requestLength - 3] == '\n'
                && request[requestLength - 2] == '\r'
                && request[requestLength - 1] == '\n') break;
        }
        Assert(
            Encoding.ASCII.GetString(request, 0, requestLength)
                .Contains("Icy-MetaData: 1", StringComparison.OrdinalIgnoreCase),
            "Klient zgodności nie poprosił o tytuł bieżącego utworu.");
        const int metadataInterval = 4_096;
        var header = Encoding.ASCII.GetBytes(
            "ICY 200 OK\r\n" +
            "Content-Type: audio/mpeg\r\n" +
            "icy-name: Stacja testowa\r\n" +
            $"icy-metaint: {metadataInterval}\r\n\r\n");
        await stream.WriteAsync(header);
        // A real radio delivers one MP3 frame across multiple TCP packets.
        // Sending one large buffer hid a regression where a short network
        // read was incorrectly treated as the permanent end of the station.
        var metadataText = Encoding.UTF8.GetBytes("StreamTitle='Artysta testowy - Utwór testowy';");
        var metadataBlocks = (metadataText.Length + 15) / 16;
        var metadata = new byte[metadataBlocks * 16];
        metadataText.CopyTo(metadata, 0);
        for (var offset = 0; offset < mp3.Length;)
        {
            var segmentLength = Math.Min(metadataInterval, mp3.Length - offset);
            for (var sent = 0; sent < segmentLength; sent += 257)
            {
                var count = Math.Min(257, segmentLength - sent);
                await stream.WriteAsync(mp3.AsMemory(offset + sent, count));
                await stream.FlushAsync();
                await Task.Delay(1);
            }
            offset += segmentLength;
            if (segmentLength == metadataInterval)
            {
                await stream.WriteAsync(new byte[] { (byte)metadataBlocks });
                await stream.WriteAsync(metadata);
            }
        }
        await releaseServer.Task;
    });

    try
    {
        using var reader = LegacyIcyMp3StreamReader.OpenAsync(
                $"http://127.0.0.1:{endpoint.Port}/;.mp3",
                CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        Assert(reader.WaveFormat.SampleRate == 44_100, "Strumień ICY podał złą częstotliwość.");
        Assert(reader.WaveFormat.Encoding == WaveFormatEncoding.IeeeFloat,
            "Strumień ICY nie został znormalizowany do bezpiecznego formatu float.");
        Assert(reader.StreamTitle == "Artysta testowy - Utwór testowy",
            "Dekoder ICY nie zachował tytułu odczytanego podczas przygotowania.");
        var buffer = new byte[16_384];
        Assert(reader.Read(buffer, 0, buffer.Length) > 0, "Strumień ICY nie zwrócił dźwięku.");
        releaseServer.TrySetResult();
        Assert(server.Wait(TimeSpan.FromSeconds(2)), "Testowy serwer ICY nie zakończył odpowiedzi.");
        Console.WriteLine("OK: starszy strumień radiowy ICY MP3");
    }
    finally
    {
        releaseServer.TrySetResult();
        listener.Stop();
    }
}

static void TestLegacyIcyCancellation()
{
    var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    var endpoint = (IPEndPoint)listener.LocalEndpoint;
    var server = Task.Run(async () =>
    {
        try
        {
            using var connection = await listener.AcceptTcpClientAsync();
            await Task.Delay(TimeSpan.FromSeconds(5));
        }
        catch (Exception exception) when (exception is ObjectDisposedException or SocketException)
        {
        }
    });
    try
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var _ = LegacyIcyMp3StreamReader.OpenAsync(
                    $"http://127.0.0.1:{endpoint.Port}/stream",
                    cancellation.Token)
                .GetAwaiter()
                .GetResult();
            throw new InvalidOperationException("Nie anulowano oczekiwania na odpowiedź radia.");
        }
        catch (OperationCanceledException)
        {
            Assert(stopwatch.Elapsed < TimeSpan.FromSeconds(2), "Anulowanie starego połączenia radia trwało zbyt długo.");
        }
        Console.WriteLine("OK: szybkie przełączanie anuluje oczekiwanie na starszy strumień");
    }
    finally
    {
        listener.Stop();
        _ = server.ContinueWith(_ => { }, TaskScheduler.Default);
    }
}

static void TestBassCancellation()
{
    Assert(BassRadioWaveProvider.IsAvailable, "Nie załadowano dołączonego dekodera BASS.");
    var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    var endpoint = (IPEndPoint)listener.LocalEndpoint;
    var server = Task.Run(async () =>
    {
        try
        {
            using var connection = await listener.AcceptTcpClientAsync();
            await Task.Delay(TimeSpan.FromSeconds(5));
        }
        catch (Exception exception) when (exception is ObjectDisposedException or SocketException)
        {
        }
    });
    try
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var _ = BassRadioWaveProvider.OpenAsync(
                    $"http://127.0.0.1:{endpoint.Port}/stream",
                    cancellation.Token)
                .GetAwaiter()
                .GetResult();
            throw new InvalidOperationException("BASS nie anulował oczekiwania na odpowiedź radia.");
        }
        catch (OperationCanceledException)
        {
            Assert(stopwatch.Elapsed < TimeSpan.FromSeconds(2),
                "Anulowanie połączenia BASS trwało zbyt długo.");
        }
        Console.WriteLine("OK: BASS jest dostępny i anuluje nieaktualne połączenie");
    }
    finally
    {
        listener.Stop();
        _ = server.ContinueWith(_ => { }, TaskScheduler.Default);
    }
}

static void TestLiveBassRadio(string source)
{
    using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
    using var reader = BassRadioWaveProvider.OpenAsync(source, cancellation.Token)
        .GetAwaiter()
        .GetResult();
    var buffer = new byte[32_768];
    var decodedBytes = 0;
    for (var attempt = 0; attempt < 4; attempt++)
    {
        var read = reader.Read(buffer, 0, buffer.Length);
        Assert(read > 0, $"BASS zakończył internetowy strumień po {decodedBytes} bajtach dźwięku.");
        decodedBytes += read;
    }
    Assert(decodedBytes > 0, "BASS nie zdekodował internetowego radia.");
    Console.WriteLine($"OK: internetowy strumień przez BASS, {reader.WaveFormat}");
}

static void TestLiveHlsRadio(string source)
{
    using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(25));
    using var reader = FfmpegRadioWaveProvider.TryOpenAsync(source, cancellation.Token)
        .GetAwaiter()
        .GetResult() ?? throw new InvalidOperationException("Nie znaleziono FFmpeg dla testu HLS.");
    var buffer = new byte[32_768];
    var read = reader.Read(buffer, 0, buffer.Length);
    Assert(read > 0, "FFmpeg nie zwrócił dźwięku z transmisji HLS.");
    Assert(reader.WaveFormat.SampleRate == 48_000 && reader.WaveFormat.Channels == 2,
        "FFmpeg zwrócił nieoczekiwany format wyjściowy.");
    Console.WriteLine($"OK: dźwięk z transmisji HLS przez FFmpeg, {reader.WaveFormat}");
}

static void TestLiveSystemRadio(string source)
{
    using var reader = new MediaFoundationReader(source);
    var buffer = new byte[32_768];
    Assert(reader.Read(buffer, 0, buffer.Length) > 0,
        "Dekoder systemowy nie zwrócił dźwięku ze zwykłego strumienia.");
    Console.WriteLine($"OK: zwykły strumień przez dekoder systemowy, {reader.WaveFormat}");
}

static void TestLiveLegacyRadio(string source)
{
    using var legacyStream = LegacyIcyAudioStream.OpenAsync(source, CancellationToken.None)
        .GetAwaiter()
        .GetResult();
    IWaveProvider reader;
    IDisposable readerLifetime;
    string decoder;
    if (legacyStream.IsOgg)
    {
        var vorbis = new LiveVorbisWaveProvider(legacyStream);
        reader = vorbis;
        readerLifetime = vorbis;
        decoder = "OGG/Vorbis";
    }
    else if (legacyStream.IsAac)
    {
        throw new NotSupportedException("Testowany starszy strumień AAC wymaga zgodnego wariantu MP3.");
    }
    else
    {
        legacyStream.Dispose();
        var mp3 = LegacyIcyMp3StreamReader.OpenAsync(source, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        reader = mp3;
        readerLifetime = mp3;
        decoder = "MP3";
    }

    using (readerLifetime)
    {
        var buffer = new byte[Math.Max(16_384, reader.WaveFormat.AverageBytesPerSecond / 10)];
        long decodedBytes = 0;
        for (var index = 0; index < 12; index++)
        {
            var read = reader.Read(buffer, 0, buffer.Length);
            Assert(read > 0, $"Internetowy strumień ICY zakończył się po {decodedBytes} bajtach dźwięku.");
            decodedBytes += read;
        }
        Console.WriteLine($"OK: internetowy strumień ICY {decoder}, {reader.WaveFormat}");
    }
}

static void TestRadioCompatibilityCandidates()
{
    var programOne = RadioStreamResolver.GetPlaybackCandidates(
        "http://stream3.polskieradio.pl:8950/;.mp3");
    Assert(
        programOne.Count == 2 && programOne[0] == "http://mp3.polskieradio.pl:8900/;.mp3",
        "Nie wybrano zgodnego MP3 dla Programu 1.");

    var programTwo = RadioStreamResolver.GetPlaybackCandidates(
        "http://stream3.polskieradio.pl:8952/;.mp3");
    Assert(
        programTwo.Count == 2 && programTwo[0].Contains(":8902/", StringComparison.Ordinal),
        "Nie wybrano zgodnego MP3 dla Programu 2.");

    var programThree = RadioStreamResolver.GetPlaybackCandidates(
        "http://stream3.polskieradio.pl:8954/;.mp3");
    Assert(
        programThree.Count == 2 && programThree[0] == "http://mp3.polskieradio.pl:8904/;.mp3",
        "Nie wybrano zgodnego MP3 dla Programu 3.");

    var hlsProgramFour = RadioStreamResolver.GetPlaybackCandidates(
        "https://stream14.polskieradio.pl/pr4/pr4.sdp/playlist.m3u8");
    Assert(
        hlsProgramFour.Count == 2
        && hlsProgramFour[0] == "https://stream14.polskieradio.pl/pr4/pr4.sdp/playlist.m3u8"
        && hlsProgramFour[1].Contains(":8906/", StringComparison.Ordinal),
        "HLS Programu 4 powinien poprzedzać awaryjny wariant MP3.");

    var hlsProgramThree = RadioStreamResolver.GetPlaybackCandidates(
        "https://stream13.polskieradio.pl/pr3/pr3.sdp/playlist.m3u8");
    Assert(
        hlsProgramThree.Count == 2
        && hlsProgramThree[0] == "https://stream13.polskieradio.pl/pr3/pr3.sdp/playlist.m3u8"
        && hlsProgramThree[1] == "http://mp3.polskieradio.pl:8904/;.mp3",
        "HLS Trójki powinien poprzedzać awaryjny wariant MP3.");

    var chopin = RadioStreamResolver.GetPlaybackCandidates(
        "http://stream3.polskieradio.pl:8960/;");
    Assert(
        chopin.Count == 2 && chopin[0] == "http://mp3.polskieradio.pl:8910/;.mp3",
        "Nie wybrano zgodnego MP3 dla Radia Chopin.");

    var eska = RadioStreamResolver.GetPlaybackCandidates(
        "https://radio.stream.smcdn.pl/icradio-p/2180-1.aac/playlist.m3u8");
    Assert(
        eska.Count == 2
        && eska[0] == "https://radio.stream.smcdn.pl/icradio-p/2180-1.aac/playlist.m3u8"
        && eska[1] == "http://ic2.smcdn.pl/2180-1.mp3",
        "HLS Eski powinien poprzedzać awaryjny wariant MP3.");

    var unrelated = "https://radio.example/live.mp3";
    var unchanged = RadioStreamResolver.GetPlaybackCandidates(unrelated);
    Assert(unchanged.Count == 1 && unchanged[0] == unrelated, "Zmieniono nieznany adres stacji.");
    Assert(
        RadioMediaOutput.ShouldPreferBass("http://mp3.polskieradio.pl:8900/;.mp3")
        && RadioMediaOutput.ShouldPreferBass("http://stream.radioemaus.pl:8000/oggstream"),
        "Nie wybrano BASS dla rozpoznanego starszego strumienia.");
    Assert(
        !RadioMediaOutput.ShouldPreferBass("https://n19a-eu.rcs.revma.com/an1ugyygzk8uv"),
        "Zwykły strumień Radia 357 nie powinien oczekiwać najpierw na BASS.");
    Console.WriteLine("OK: bezpieczne warianty zgodności znanych stacji");
}

static void TestRadioReconnectFormatCompatibility()
{
    var expected = WaveFormat.CreateIeeeFloatWaveFormat(48_000, 2);
    var same = WaveFormat.CreateIeeeFloatWaveFormat(48_000, 2);
    var differentRate = WaveFormat.CreateIeeeFloatWaveFormat(44_100, 2);
    var differentChannels = WaveFormat.CreateIeeeFloatWaveFormat(48_000, 1);
    var differentEncoding = new WaveFormat(48_000, 16, 2);

    Assert(
        RadioMediaOutput.AreCompatibleRadioFormats(expected, same),
        "Odrzucono zgodny format po ponownym połączeniu radia.");
    Assert(
        !RadioMediaOutput.AreCompatibleRadioFormats(expected, differentRate),
        "Zaakceptowano zmianę częstotliwości bez przebudowy toru radia.");
    Assert(
        !RadioMediaOutput.AreCompatibleRadioFormats(expected, differentChannels),
        "Zaakceptowano zmianę liczby kanałów bez przebudowy toru radia.");
    Assert(
        !RadioMediaOutput.AreCompatibleRadioFormats(expected, differentEncoding),
        "Zaakceptowano zmianę kodowania bez przebudowy toru radia.");
    Console.WriteLine("OK: bezpieczna zgodność formatu przy ponownym łączeniu radia");
}

static void TestCompleteOutputChainMonitor()
{
    var inner = new BlockingSampleProvider();
    var monitor = new DecoderReadMonitorSampleProvider(inner);
    var readTask = Task.Run(() => monitor.Read(new float[1_024], 0, 1_024));
    Assert(inner.ReadStarted.Wait(TimeSpan.FromSeconds(1)), "Końcowy tor dźwięku nie rozpoczął odczytu.");
    Assert(monitor.IsReadStalled(TimeSpan.Zero), "Nie wykryto zatrzymania pełnego toru dźwięku.");
    inner.AllowReadToFinish.Set();
    Assert(readTask.Wait(TimeSpan.FromSeconds(1)), "Nie zakończono testu pełnego toru dźwięku.");
    inner.Dispose();
    Console.WriteLine("OK: nadzór obejmuje pełny tor dekodera i zmiany prędkości");
}

static void TestInvalidSamplesAreSilenced()
{
    var monitor = new DecoderReadMonitorSampleProvider(new InvalidSampleProvider());
    var buffer = new float[8];
    var read = monitor.Read(buffer, 0, buffer.Length);
    Assert(read == buffer.Length, "Nie odczytano testowych próbek.");
    Assert(buffer.All(float.IsFinite), "Nieprawidłowa próbka dotarła do urządzenia audio.");
    Assert(buffer[1] == 0f && buffer[2] == 0f, "Nie zastąpiono NaN i nieskończoności ciszą.");
    Console.WriteLine("OK: nieprawidłowe próbki są bezpiecznie zastępowane ciszą");
}

static void TestPlaybackAudioProcessors()
{
    var loudSource = new PositionedConstantSampleProvider(1_000, 1, 0.90f, 10_000);
    var loudNormalizer = new LoudnessNormalizationSampleProvider(loudSource, enabled: true);
    var loudBuffer = new float[100];
    Assert(loudNormalizer.Read(loudBuffer, 0, loudBuffer.Length) == loudBuffer.Length,
        "Normalizator nie zwrócił głośnego bloku testowego.");
    Assert(loudBuffer.Max(Math.Abs) <= 0.98f && loudBuffer.Average(Math.Abs) < 0.30f,
        "Normalizator nie ograniczył głośnego utworu albo dopuścił przesterowanie.");

    var quietSource = new PositionedConstantSampleProvider(1_000, 1, 0.05f, 10_000);
    var quietNormalizer = new LoudnessNormalizationSampleProvider(quietSource, enabled: true);
    var quietBuffer = new float[100];
    quietNormalizer.Read(quietBuffer, 0, quietBuffer.Length);
    Assert(quietBuffer.Average(Math.Abs) > 0.10f && quietBuffer.Max(Math.Abs) <= 0.98f,
        "Normalizator nie podniósł bezpiecznie cichego utworu.");

    var transitionSource = new PositionedConstantSampleProvider(1_000, 1, 1f, 1_000);
    var transition = new TrackTransitionSampleProvider(
        transitionSource,
        () => transitionSource.Position,
        () => transitionSource.Duration,
        fadeDurationMilliseconds: 100);
    Assert(WindowsMediaOutput.SmoothTrackTransitionDuration == TimeSpan.FromSeconds(4),
        "Łagodne przejście nie rozpoczyna wyciszenia cztery sekundy przed końcem.");
    var transitionBuffer = new float[100];
    transition.Read(transitionBuffer, 0, transitionBuffer.Length);
    Assert(transitionBuffer[0] < transitionBuffer[^1],
        "Łagodne wejście nie zwiększa poziomu początku utworu.");
    Assert(transitionBuffer[24] < 0.20f,
        "Łagodne wejście nadal używa gwałtownej, liniowej zmiany poziomu.");
    transitionSource.PositionFrames = 900;
    transition.Read(transitionBuffer, 0, transitionBuffer.Length);
    Assert(transitionBuffer[0] > transitionBuffer[^1] && transitionBuffer[^1] <= 0.01f,
        "Łagodne wyjście nie wygasza naturalnego końca utworu.");

    var manualSource = new PositionedConstantSampleProvider(1_000, 1, 1f, 10_000)
    {
        PositionFrames = 5_000
    };
    var manualTransition = new TrackTransitionSampleProvider(
        manualSource,
        () => manualSource.Position,
        () => manualSource.Duration,
        fadeDurationMilliseconds: 100);
    manualTransition.Read(transitionBuffer, 0, transitionBuffer.Length);
    manualTransition.BeginManualFadeOut();
    manualTransition.Read(transitionBuffer, 0, transitionBuffer.Length);
    Assert(transitionBuffer[0] > transitionBuffer[^1] && transitionBuffer[^1] <= 0.01f,
        "Ręczna zmiana utworu nie wygasza poprzedniego toru.");
    Console.WriteLine("OK: normalizacja i łagodne przejścia lokalnego dźwięku");
}

static void TestFormatMetadata(string mediaPath)
{
    Assert(File.Exists(mediaPath), $"Nie istnieje plik testowy: {mediaPath}");
    var result = WindowsMediaOutput.TryReadMetadataAsync(
            mediaPath,
            TimeSpan.FromSeconds(5))
        .GetAwaiter()
        .GetResult();
    Assert(!result.TimedOut, $"Odczyt formatu przekroczył limit: {mediaPath}");
    Assert(result.Success, $"Nie odczytano formatu: {mediaPath}");
    Assert(result.Duration > TimeSpan.Zero, $"Format nie podał czasu: {mediaPath}");
    Assert(result.SampleRateHz > 0, $"Format nie podał częstotliwości: {mediaPath}");
    Console.WriteLine($"OK: {Path.GetExtension(mediaPath).ToUpperInvariant()}, {result.Duration}, {result.SampleRateHz} Hz");
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

static void TestPodcastFeedClient()
{
    const string feedXml = """
        <rss version="2.0"><channel><title>Podcast sieciowy</title>
          <item><guid>1</guid><title>Odcinek</title>
            <enclosure url="https://cdn.example.test/audio.mp3" type="audio/mpeg" />
          </item>
        </channel></rss>
        """;
    var handler = new PodcastHttpHandler(request =>
    {
        if (request.RequestUri == new Uri("https://example.test/start"))
        {
            var redirect = new HttpResponseMessage(HttpStatusCode.Redirect);
            redirect.Headers.Location = new Uri("/feed.xml", UriKind.Relative);
            return redirect;
        }
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(feedXml, Encoding.UTF8, "application/rss+xml")
        };
    });
    using var client = new PodcastFeedClient(handler, TimeSpan.FromSeconds(2));
    var feed = client.FetchAsync(new Uri("https://example.test/start"), CancellationToken.None)
        .GetAwaiter().GetResult();
    Assert(feed.Title == "Podcast sieciowy", "Klient nie odczytał kanału po bezpiecznym przekierowaniu.");
    Assert(feed.Episodes.Count == 1, "Klient nie odczytał metadanych odcinka.");
    Assert(handler.Requests.Count == 2, "Klient wykonał nieoczekiwaną liczbę żądań.");
    Assert(handler.Requests.All(uri => uri.Host == "example.test"), "Klient pobrał plik audio zamiast samych metadanych kanału.");

    Console.WriteLine("OK: ograniczony klient kanałów podcastów");
}

static void TestPodcastOpmlImportSelectionAccessibility()
{
    Exception? failure = null;
    var thread = new Thread(() =>
    {
        PodcastOpmlImportWindow? window = null;
        try
        {
            var entries = new[]
            {
                new PodcastOpmlEntry(
                    "Audycja Alfa",
                    new Uri("https://example.test/alfa.xml"),
                    new Uri("https://example.test/alfa")),
                new PodcastOpmlEntry(
                    "Podcast Beta",
                    new Uri("https://example.test/beta.xml"),
                    null)
            };
            window = new PodcastOpmlImportWindow(entries);
            window.Show();
            DrainPodcastDispatcher(window.Dispatcher);
            var list = (ListBox)window.FindName("FeedsList");
            var status = (AccessibleStatusTextBlock)window.FindName("ImportStatus");
            Assert(list.SelectionMode == SelectionMode.Single
                   && TextSearch.GetTextPath(list) == "NavigationText",
                "Lista OPML nie rozdziela fokusu od wyboru podcastów do importu.");
            Assert(window.SelectedEntries.Count == 2 && list.SelectedIndex == 0,
                "Import OPML nie rozpoczyna od wszystkich podcastów i pierwszego wiersza.");
            var first = list.SelectedItem;
            var firstLabel = first?.GetType().GetProperty("AccessibleLabel")?.GetValue(first)?.ToString();
            Assert(!string.IsNullOrWhiteSpace(firstLabel)
                   && firstLabel.Contains("zaznaczony do importu", StringComparison.Ordinal)
                   && !firstLabel.Contains('{', StringComparison.Ordinal),
                "Pierwszy kanał OPML nie ma użytkowej informacji o stanie wyboru.");
            Assert(window.ToggleCurrentEntry() && window.SelectedEntries.Count == 1,
                "Spacja nie odznacza bieżącego kanału OPML.");
            list.SelectedIndex = 1;
            Assert(window.SelectedEntries.Count == 1,
                "Sama nawigacja strzałkami zmienia wybór kanałów OPML.");
            Assert(window.ToggleCurrentEntry() && window.SelectedEntries.Count == 0,
                "Spacja nie odznacza drugiego kanału OPML.");
            window.SelectAllEntries();
            Assert(window.SelectedEntries.Count == 2
                   && status.Text == "Wybrano podcasty: 2 z 2.",
                "Ctrl+A nie zaznacza wszystkich kanałów OPML albo nie podaje liczby.");
        }
        catch (Exception exception)
        {
            failure = exception;
        }
        finally
        {
            window?.Close();
        }
    });
    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();
    if (failure is not null)
        throw new InvalidOperationException("Test dostępnego wyboru podcastów z OPML nie powiódł się.", failure);

    Console.WriteLine("OK: dostępny wybór podcastów z OPML");

    static void DrainPodcastDispatcher(System.Windows.Threading.Dispatcher dispatcher)
    {
        var frame = new System.Windows.Threading.DispatcherFrame();
        dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.ApplicationIdle,
            () => frame.Continue = false);
        System.Windows.Threading.Dispatcher.PushFrame(frame);
    }
}

sealed class BlockingWaveStream : WaveStream
{
    private readonly WaveFormat _format = WaveFormat.CreateIeeeFloatWaveFormat(48_000, 2);
    private long _position;

    public ManualResetEventSlim ReadStarted { get; } = new(false);
    public ManualResetEventSlim AllowReadToFinish { get; } = new(false);
    public override WaveFormat WaveFormat => _format;
    public override long Length => _format.AverageBytesPerSecond * 60L;
    public override long Position
    {
        get => _position;
        set => _position = Math.Clamp(value, 0, Length);
    }
    public override void Flush()
    {
    }
    public override int Read(byte[] buffer, int offset, int count)
    {
        ReadStarted.Set();
        if (!AllowReadToFinish.Wait(TimeSpan.FromSeconds(5)))
            throw new TimeoutException("Testowy odczyt nie został zwolniony.");
        return 0;
    }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            AllowReadToFinish.Set();
            ReadStarted.Dispose();
            AllowReadToFinish.Dispose();
        }
        base.Dispose(disposing);
    }
}

sealed class FixedJsonHandler(string json) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
    {
        Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        RequestMessage = request
    });
}

sealed class FixedDurationWaveStream(TimeSpan duration) : WaveStream
{
    private readonly WaveFormat _format = WaveFormat.CreateIeeeFloatWaveFormat(48_000, 2);
    private long _position;
    public override WaveFormat WaveFormat => _format;
    public override long Length => checked((long)(duration.TotalSeconds * _format.AverageBytesPerSecond));
    public override long Position
    {
        get => _position;
        set => _position = Math.Clamp(value, 0, Length);
    }
    public override void Flush()
    {
    }
    public override int Read(byte[] buffer, int offset, int count) => 0;
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

sealed class BlockingSampleProvider : ISampleProvider, IDisposable
{
    public ManualResetEventSlim ReadStarted { get; } = new(false);
    public ManualResetEventSlim AllowReadToFinish { get; } = new(false);
    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(48_000, 2);
    public int Read(float[] buffer, int offset, int count)
    {
        ReadStarted.Set();
        if (!AllowReadToFinish.Wait(TimeSpan.FromSeconds(5)))
            throw new TimeoutException("Testowy tor dźwięku nie został zwolniony.");
        return 0;
    }
    public void Dispose()
    {
        AllowReadToFinish.Set();
        ReadStarted.Dispose();
        AllowReadToFinish.Dispose();
    }
}

sealed class InvalidSampleProvider : ISampleProvider
{
    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(48_000, 2);

    public int Read(float[] buffer, int offset, int count)
    {
        Array.Fill(buffer, 0.25f, offset, count);
        if (count > 1) buffer[offset + 1] = float.NaN;
        if (count > 2) buffer[offset + 2] = float.PositiveInfinity;
        return count;
    }
}

sealed class PodcastHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public List<Uri> Requests { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Requests.Add(request.RequestUri!);
        return Task.FromResult(respond(request));
    }
}

sealed class PositionedConstantSampleProvider(
    int sampleRate,
    int channels,
    float value,
    long lengthFrames) : ISampleProvider
{
    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
    public long PositionFrames { get; set; }
    public TimeSpan Position => TimeSpan.FromSeconds(PositionFrames / (double)sampleRate);
    public TimeSpan Duration => TimeSpan.FromSeconds(lengthFrames / (double)sampleRate);

    public int Read(float[] buffer, int offset, int count)
    {
        var availableSamples = checked((lengthFrames - PositionFrames) * channels);
        var read = (int)Math.Min(Math.Max(0L, availableSamples), count);
        read -= read % channels;
        Array.Fill(buffer, value, offset, read);
        PositionFrames += read / channels;
        return read;
    }
}

sealed class TestRadioRecordingBackend(string initialPath) : IRadioRecordingBackend
{
    private int _part = 1;
    private TimeSpan _recordingDuration = TimeSpan.FromMinutes(1);

    public bool IsRecording { get; private set; } = true;
    public bool CanPauseRecording => true;
    public bool IsRecordingPaused { get; private set; }
    public TimeSpan RecordingDuration => _recordingDuration;
    public string CurrentPath { get; private set; } = initialPath;

    public RadioAudioSnapshot? RecentAudio { get; set; }

    public bool TryGetRecentAudio(TimeSpan duration, out RadioAudioSnapshot? snapshot)
    {
        snapshot = RecentAudio;
        return snapshot is not null;
    }

    public string StartRecording(
        string folder,
        RadioRecordingFormat format,
        int bitrateKbps,
        string? preferredBaseName = null)
    {
        _part++;
        CurrentPath = Path.Combine(folder, $"{preferredBaseName ?? $"part-{_part}"}.wav");
        IsRecording = true;
        IsRecordingPaused = false;
        _recordingDuration = TimeSpan.Zero;
        return CurrentPath;
    }

    public string? StopRecording()
    {
        if (!IsRecording) return null;
        IsRecording = false;
        IsRecordingPaused = false;
        return CurrentPath;
    }

    public void PauseRecording()
    {
        if (!IsRecording) throw new InvalidOperationException("Nagrywanie nie trwa.");
        IsRecordingPaused = true;
    }

    public void ResumeRecording()
    {
        if (!IsRecording) throw new InvalidOperationException("Nagrywanie nie trwa.");
        IsRecordingPaused = false;
    }
}

sealed class TestWavePlayer : IWavePlayer
{
    public event EventHandler<StoppedEventArgs>? PlaybackStopped;

    public Action? OnPlay { get; set; }
    public int PlayCount { get; private set; }
    public int PauseCount { get; private set; }
    public PlaybackState PlaybackState { get; private set; } = PlaybackState.Stopped;
    public WaveFormat OutputWaveFormat { get; private set; } = new WaveFormat(48_000, 16, 2);
    public float Volume { get; set; } = 1f;

    public void Init(IWaveProvider waveProvider)
    {
        OutputWaveFormat = waveProvider.WaveFormat;
    }

    public void Play()
    {
        PlayCount++;
        PlaybackState = PlaybackState.Playing;
        OnPlay?.Invoke();
    }

    public void Pause()
    {
        PauseCount++;
        PlaybackState = PlaybackState.Paused;
    }

    public void Stop()
    {
        PlaybackState = PlaybackState.Stopped;
        PlaybackStopped?.Invoke(this, new StoppedEventArgs());
    }

    public void Dispose()
    {
    }
}

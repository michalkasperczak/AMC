using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.LiteHost;
using AccessibleMediaController.LiteHost.Protocol;
using AccessibleMediaController.Windows.Services;

static void Check(bool result, string message)
{
    if (!result) throw new InvalidOperationException(message);
}

var item = new MediaItem
{
    Id = "spotify:track:abc123",
    ExternalId = "abc123",
    Source = "spotify:track:abc123",
    Title = "Utwór próby",
    Kind = MediaItemKind.Track,
};
var calls = new ConcurrentQueue<string>();
var snapshot = """
    {"type":"playback_state","status":"playing",
     "item":{"uri":"spotify:track:abc123",
             "decorations":{"playback":{"duration_ms":120000}}},
     "position":{"position_ms":3000}}
    """;
using (var output = new LiteSpotifySoloistOutput("unused.exe", "unused",
    args =>
    {
        calls.Enqueue(string.Join(" ", args));
        return args[0] == "now" ? (0, snapshot) : (0, "");
    }))
{
    Check(output.IsReady(), "poprawna migawka nie potwierdza gotowości");
    var started = new ManualResetEventSlim();
    output.PlaybackStarted += (_, _) => started.Set();
    output.Play(item, TimeSpan.Zero, 40, 1d);
    Check(started.Wait(TimeSpan.FromSeconds(3)), "brak potwierdzonego startu");
    Check(calls.Contains("activate"), "brak przełączenia Spotify Connect na Soloist");
    Check(calls.Contains("play spotify:track:abc123"), "brak polecenia play URI");
    Check(calls.Contains("volume 40"), "brak ustawienia głośności");
    Check(output.Position >= TimeSpan.FromSeconds(3), "pozycja odtwarzania nie jest aktualizowana");
    try
    {
        output.SetPlaybackRate(1.25d);
        throw new InvalidOperationException("Soloist przyjął nieobsługiwaną zmianę tempa");
    }
    catch (LiteRequestException) { }
    output.Pause();
    Check(calls.Contains("pause"), "brak pauzy");
    output.Stop();
}

using (var output = new LiteSpotifySoloistOutput("unused.exe", "unused",
    args => args[0] == "play" ? (3, "secret-or-raw-error") : (0, snapshot)))
{
    string? failure = null;
    output.PlaybackFailed += (_, error) => failure = error.Message;
    output.Play(item, TimeSpan.Zero, 40, 1d);
    Check(failure is not null && !failure.Contains("secret-or-raw-error"),
        "błąd CLI ujawnił techniczne dane użytkownikowi");
}

using (var output = new LiteSpotifySoloistOutput("unused.exe", "unused",
    args => args[0] == "now" ? (3, "") : (0, "")))
    Check(!output.IsReady(), "brak logowania został uznany za gotowość");

var leaveBuffering = new ManualResetEventSlim();
using (var output = new LiteSpotifySoloistOutput("unused.exe", "unused",
    args => args[0] == "now"
        ? (0, leaveBuffering.IsSet
            ? snapshot : snapshot.Replace("\"playing\"", "\"buffering\""))
        : (0, "")))
{
    var started = new ManualResetEventSlim();
    output.PlaybackStarted += (_, _) => started.Set();
    output.Play(item, TimeSpan.Zero, 40, 1d);
    Check(!started.Wait(TimeSpan.FromMilliseconds(150)),
        "buforowanie zostało przedwcześnie zgłoszone jako odtwarzanie");
    leaveBuffering.Set();
    Check(started.Wait(TimeSpan.FromSeconds(3)),
        "start po buforowaniu nie został zgłoszony");
}

var finishPlayback = new ManualResetEventSlim();
using (var output = new LiteSpotifySoloistOutput("unused.exe", "unused",
    args => args[0] == "now"
        ? (0, finishPlayback.IsSet
            ? snapshot.Replace("\"playing\"", "\"idle\"").Replace(
                "spotify:track:abc123", "")
            : snapshot)
        : (0, "")))
{
    var started = new ManualResetEventSlim();
    var ended = new ManualResetEventSlim();
    output.PlaybackStarted += (_, _) => started.Set();
    output.PlaybackEnded += (_, _) => ended.Set();
    output.Play(item, TimeSpan.Zero, 40, 1d);
    Check(started.Wait(TimeSpan.FromSeconds(3)), "nie można rozpocząć testu końca utworu");
    finishPlayback.Set();
    Check(ended.Wait(TimeSpan.FromSeconds(4)),
        "koniec utworu nie został zgłoszony kolejce AMC");
}

Check(LiteSpotifySoloistOutput.TryCreateFromSettings(null, null) is null,
    "brak konfiguracji nie wraca do Chrome");

// Konfiguracja niedostępnego ręcznie wskazanego endpointu nie może po cichu
// przełączyć już działającego transportu na domyślne wyjście. W szczególności
// nie wolno kasować zapamiętanego wyboru: późniejsza polityka odzyskania ma
// rozróżnić ręczny wybór od rzeczywiście wybranego przez użytkownika domyślnego.
using (var output = new RejectingOutputSelection())
using (var coordinator = new LiteSpotifyPlaybackCoordinator())
{
    var outputField = typeof(LiteSpotifyPlaybackCoordinator).GetField(
        "output", BindingFlags.Instance | BindingFlags.NonPublic);
    Check(outputField is not null, "test nie dotarł do rzeczywistego transportu Spotify");
    outputField!.SetValue(coordinator, output);
    using var request = JsonDocument.Parse("""
        {"deviceId":"Denon","restart":false}
        """);
    var responseObject = coordinator.SelectOutput(request.RootElement);
    Check(output.Selections.SequenceEqual(["Denon"]),
        "niedostępny ręczny wybór przełączył transport po cichu na domyślne wyjście");
    using var response = JsonDocument.Parse(JsonSerializer.Serialize(responseObject));
    Check(!response.RootElement.GetProperty("available").GetBoolean()
        && !response.RootElement.GetProperty("usingDefault").GetBoolean(),
        "odpowiedź odmowy ręcznego wyjścia udaje dostępność albo wybór domyślny");
    var selectedField = typeof(LiteSpotifyPlaybackCoordinator).GetField(
        "selectedDeviceName", BindingFlags.Instance | BindingFlags.NonPublic);
    Check(selectedField?.GetValue(coordinator) is "Denon",
        "odmowa ręcznego wyjścia utraciła zapamiętany wybór użytkownika");
}

var installedSoloist = Environment.GetEnvironmentVariable("AMC_SOLOIST_TEST_EXE");
if (!string.IsNullOrWhiteSpace(installedSoloist))
{
    var temporary = Directory.CreateTempSubdirectory("amc-soloist-test-");
    var settingsPath = Path.Combine(temporary.FullName, "spotify-soloist.json");
    try
    {
        File.WriteAllText(settingsPath, JsonSerializer.Serialize(new
        {
            version = 1,
            executable = installedSoloist,
            data_directory = Path.Combine(temporary.FullName, "empty-data"),
        }));
        Check(LiteSpotifySoloistOutput.TryCreateFromSettings(
            settingsPath, Path.Combine(temporary.FullName, "empty-data")) is null,
            "niezalogowany Soloist przejął tor odtwarzania");
    }
    finally
    {
        File.Delete(settingsPath);
        Directory.Delete(temporary.FullName);
    }
}
Console.WriteLine(string.IsNullOrWhiteSpace(installedSoloist)
    ? "Soloist adapter: 7 checks passed"
    : "Soloist adapter: 8 checks passed, including official binary fallback");

sealed class RejectingOutputSelection : ILiteSpotifyOutput
{
    internal List<string?> Selections { get; } = [];

    public event EventHandler<MediaDurationAvailableEventArgs>? DurationAvailable
    {
        add { }
        remove { }
    }
    public event EventHandler<MediaOutputFailedEventArgs>? PlaybackFailed
    {
        add { }
        remove { }
    }
    public event EventHandler<MediaPlaybackEndedEventArgs>? PlaybackEnded
    {
        add { }
        remove { }
    }
    public event EventHandler<MediaPlaybackPreparingEventArgs>? PlaybackPreparing
    {
        add { }
        remove { }
    }
    public event EventHandler<MediaPlaybackStartedEventArgs>? PlaybackStarted
    {
        add { }
        remove { }
    }

    public TimeSpan Position => TimeSpan.Zero;
    public string? TakeDefaultOutputFallbackNotice() => null;
    public void Play(MediaItem item, TimeSpan start, int volume, double rate) { }
    public void Pause() { }
    public void Resume() { }
    public void Stop() { }
    public void Seek(TimeSpan position) { }
    public void SetVolume(int volume) { }
    public void SetPlaybackRate(double rate) { }
    public Task<IReadOnlyList<LiteSpotifyChromeDevice>> GetOutputDevicesAsync() =>
        Task.FromResult<IReadOnlyList<LiteSpotifyChromeDevice>>([]);

    public Task<bool> TrySetOutputDeviceAsync(string? wanted)
    {
        Selections.Add(wanted);
        return Task.FromResult(false);
    }

    public void Dispose() { }
}

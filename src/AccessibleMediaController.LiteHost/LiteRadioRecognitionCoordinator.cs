using System.Text.Json;
using NAudio.Wave;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.LiteHost.Protocol;
using AccessibleMediaController.Windows.Services;

namespace AccessibleMediaController.LiteHost;

/// <summary>
/// Rozpoznaje dźwięk już odbierany przez Radio lub nagrywanie w tle. Do
/// adaptera trafia próbka PCM, a do Pythona tylko celowe pola wyniku.
/// </summary>
internal sealed class LiteRadioRecognitionCoordinator
{
    public const string Operation = "radio.recognition.run";
    public const string ClipInfoOperation = "radio.recognition.clipInfo";
    public const string ClipSaveOperation = "radio.recognition.clipSave";
    public const string ClipPreviewOperation = "radio.recognition.clipPreview";
    private static readonly TimeSpan SampleDuration = TimeSpan.FromSeconds(12);
    private readonly object _clipsGate = new();
    private readonly Dictionary<string, (WeakReference<RadioMediaOutput> Output, RadioAudioSampleAnchor Anchor)> _clips = new();

    private readonly RadioMediaOutput _radio;
    private readonly Func<MediaItem?> _currentStation;
    private readonly LiteRadioRecordingCoordinator _recordings;
    private readonly ITrackRecognitionService _service;
    private int _inProgress;

    public LiteRadioRecognitionCoordinator(
        RadioMediaOutput radio,
        Func<MediaItem?> currentStation,
        LiteRadioRecordingCoordinator recordings,
        ITrackRecognitionService? service = null)
    {
        _radio = radio ?? throw new ArgumentNullException(nameof(radio));
        _currentStation = currentStation ?? throw new ArgumentNullException(nameof(currentStation));
        _recordings = recordings ?? throw new ArgumentNullException(nameof(recordings));
        _service = service ?? new ShazamTrackRecognitionService();
    }

    public object Run(JsonElement args)
    {
        if (Interlocked.CompareExchange(ref _inProgress, 1, 0) != 0)
            throw new LiteRequestException("Rozpoznawanie już trwa.");
        try
        {
            var scopeText = LiteArgs.ReadText(args, "scope")
                ?? nameof(RadioRecognitionScope.CurrentStation);
            if (!Enum.TryParse<RadioRecognitionScope>(scopeText, true, out var scope)
                || !Enum.IsDefined(scope))
            {
                throw new LiteRequestException("Nieznany zakres rozpoznawania utworów.");
            }

            var inputs = CaptureInputs(scope);
            if (inputs.Count == 0)
            {
                return new
                {
                    requested = 0,
                    recognized = Array.Empty<object>(),
                    failures = Array.Empty<object>(),
                    message = "Brak wystarczającej próbki dźwięku. Poczekaj kilka sekund i spróbuj ponownie."
                };
            }

            var recognized = new List<object>();
            var failures = new List<object>();
            var lastFailure = "Nie rozpoznano utworu";
            foreach (var input in inputs)
            {
                var result = _service.RecognizeAsync(input.Snapshot, CancellationToken.None)
                    .GetAwaiter().GetResult();
                if (!result.Success)
                {
                    lastFailure = result.Error ?? "Nie rozpoznano utworu";
                    failures.Add(new
                    {
                        stationId = input.StationId,
                        stationName = input.StationName,
                        message = lastFailure
                    });
                    continue;
                }
                recognized.Add(new
                {
                    stationId = input.StationId,
                    stationName = input.StationName,
                    title = result.Title,
                    artist = result.Artist,
                    album = result.Album,
                    releaseDate = result.ReleaseDate,
                    providerUri = result.ProviderUri,
                    clipToken = RememberClip(input),
                    recognizedUtcTicks = DateTime.UtcNow.Ticks
                });
            }
            return new
            {
                requested = inputs.Count,
                recognized = recognized.ToArray(),
                failures = failures.ToArray(),
                message = recognized.Count > 0 ? string.Empty : lastFailure
            };
        }
        finally
        {
            Interlocked.Exchange(ref _inProgress, 0);
        }
    }

    private string? RememberClip(LiteRadioRecognitionInput input)
    {
        if (input.Snapshot.Anchor is not { } anchor || input.SourceOutput is not { } output)
            return null;
        var token = Guid.NewGuid().ToString("N");
        lock (_clipsGate)
        {
            if (_clips.Count >= 200) _clips.Remove(_clips.Keys.First());
            _clips[token] = (new WeakReference<RadioMediaOutput>(output), anchor);
        }
        return token;
    }

    private (RadioMediaOutput Output, RadioAudioSampleAnchor Anchor, RadioAudioClipBounds Bounds)
        GetClip(JsonElement args)
    {
        var token = LiteArgs.ReadText(args, "clipToken");
        if (string.IsNullOrWhiteSpace(token))
            throw new LiteRequestException("To rozpoznanie nie ma już dostępnej próbki dźwięku.");
        (WeakReference<RadioMediaOutput> Output, RadioAudioSampleAnchor Anchor) clip;
        lock (_clipsGate)
        {
            if (!_clips.TryGetValue(token, out clip))
                throw new LiteRequestException("Próbka dźwięku nie jest już dostępna. Rozpoznaj utwór ponownie.");
        }
        if (!clip.Output.TryGetTarget(out var output)
            || !output.TryGetClipBounds(clip.Anchor, out var bounds) || bounds is null)
            throw new LiteRequestException("Fragment zniknął z bufora albo zmienił się tor stacji.");
        return (output, clip.Anchor, bounds);
    }

    public object ClipInfo(JsonElement args)
    {
        var (_, _, bounds) = GetClip(args);
        var bytesPerSecond = bounds.BytesPerSecond;
        return new
        {
            earliestSecond = (int)Math.Ceiling((bounds.FirstByte - bounds.SampleStartByte)
                / (double)bytesPerSecond),
            latestSecond = (int)Math.Floor((bounds.LastByte - bounds.SampleStartByte)
                / (double)bytesPerSecond),
            sampleSeconds = (int)Math.Round((bounds.SampleEndByte - bounds.SampleStartByte)
                / (double)bytesPerSecond),
            message = "Granice utworu nie są znane. Odsłuchaj i wybierz dostępny fragment."
        };
    }

    public object ClipSave(JsonElement args)
    {
        var (output, anchor, bounds) = GetClip(args);
        var firstSecond = LiteArgs.ReadInt(args, "firstSecond", int.MinValue, -86400, 86400);
        var lastSecond = LiteArgs.ReadInt(args, "lastSecond", int.MinValue, -86400, 86400);
        if (firstSecond == int.MinValue || lastSecond == int.MinValue || firstSecond >= lastSecond)
            throw new LiteRequestException("Wybierz poprawny początek i koniec fragmentu.");
        var first = bounds.SampleStartByte + (long)firstSecond * bounds.BytesPerSecond;
        var last = bounds.SampleStartByte + (long)lastSecond * bounds.BytesPerSecond;
        first -= first % bounds.BlockAlign;
        last -= last % bounds.BlockAlign;
        if (first < bounds.FirstByte || last > bounds.LastByte)
            throw new LiteRequestException("Wybrany fragment nie mieści się już w buforze stacji.");
        if (last - first > 2_000_000_000L)
            throw new LiteRequestException("Fragment jest za długi dla pliku WAV. Wybierz krótszy zakres.");
        var path = LiteArgs.ReadText(args, "path");
        if (string.IsNullOrWhiteSpace(path) ||
            !string.Equals(Path.GetExtension(path), ".wav", StringComparison.OrdinalIgnoreCase))
            throw new LiteRequestException("Wybierz nazwę pliku WAV.");
        path = Path.GetFullPath(path);
        if (File.Exists(path))
            throw new LiteRequestException("Plik już istnieje. Wybierz inną nazwę.");
        try
        {
            output.SaveClip(anchor, first, last, path);
            return new { path, seconds = lastSecond - firstSecond };
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException
            or InvalidOperationException or ObjectDisposedException)
        {
            throw new LiteRequestException(error.Message);
        }
    }

    public object ClipPreview(JsonElement args)
    {
        var (output, anchor, bounds) = GetClip(args);
        var firstSecond = LiteArgs.ReadInt(args, "firstSecond", int.MinValue, -86400, 86400);
        var lastSecond = LiteArgs.ReadInt(args, "lastSecond", int.MinValue, -86400, 86400);
        if (firstSecond == int.MinValue || lastSecond == int.MinValue
            || firstSecond >= lastSecond || lastSecond - firstSecond > 6)
            throw new LiteRequestException("Wybierz od jednej do sześciu sekund odsłuchu.");
        var first = bounds.SampleStartByte + (long)firstSecond * bounds.BytesPerSecond;
        var last = bounds.SampleStartByte + (long)lastSecond * bounds.BytesPerSecond;
        first -= first % bounds.BlockAlign;
        last -= last % bounds.BlockAlign;
        if (first < bounds.FirstByte || last > bounds.LastByte)
            throw new LiteRequestException("Ten odsłuch nie mieści się już w buforze stacji.");
        var path = Path.Combine(Path.GetTempPath(), "amc-radio-preview-"
            + Guid.NewGuid().ToString("N") + ".wav");
        try
        {
            output.SaveClip(anchor, first, last, path);
            using var reader = new WaveFileReader(path);
            using var player = new WaveOutEvent();
            player.Init(reader);
            player.Play();
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(8);
            while (player.PlaybackState == PlaybackState.Playing && DateTime.UtcNow < deadline)
                Thread.Sleep(50);
            player.Stop();
            return new { seconds = lastSecond - firstSecond };
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException
            or InvalidOperationException or ObjectDisposedException)
        {
            throw new LiteRequestException(error.Message);
        }
        catch (Exception)
        {
            throw new LiteRequestException("Odsłuch na domyślnym urządzeniu dźwięku jest niedostępny.");
        }
        finally
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (IOException) { /* Plik tymczasowy może być chwilowo zajęty przez sterownik. */ }
        }
    }

    private IReadOnlyList<LiteRadioRecognitionInput> CaptureInputs(
        RadioRecognitionScope scope)
    {
        var result = new List<LiteRadioRecognitionInput>();
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (RadioRecognitionScopeRules.IncludesCurrentStation(scope))
        {
            var station = _currentStation();
            if (station is not null
                && _radio.TryGetRecentPlaybackAudio(SampleDuration, out var snapshot)
                && snapshot is not null)
            {
                Add(result, keys, new LiteRadioRecognitionInput(
                    station.Id,
                    station.Title,
                    station.Source ?? station.PublicUri ?? string.Empty,
                    snapshot,
                    _radio));
            }
        }
        if (RadioRecognitionScopeRules.IncludesRecordingStations(scope))
        {
            foreach (var input in _recordings.CaptureRecognitionInputs(SampleDuration))
                Add(result, keys, input);
        }
        return result;
    }

    private static void Add(
        ICollection<LiteRadioRecognitionInput> result,
        ISet<string> keys,
        LiteRadioRecognitionInput input)
    {
        var key = string.IsNullOrWhiteSpace(input.StationId)
            ? "url:" + input.StreamUrl.Trim()
            : "id:" + input.StationId.Trim();
        if (!keys.Add(key)) return;
        result.Add(input with
        {
            StationId = input.StationId.Trim(),
            StationName = string.IsNullOrWhiteSpace(input.StationName)
                ? "Nieznana stacja"
                : input.StationName.Trim()
        });
    }
}

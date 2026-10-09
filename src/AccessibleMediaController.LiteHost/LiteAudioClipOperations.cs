using System.Collections.Concurrent;
using System.Text.Json;
using AccessibleMediaController.Core.LocalMedia;
using AccessibleMediaController.LiteHost.Protocol;
using AccessibleMediaController.Windows.Services;

namespace AccessibleMediaController.LiteHost;

/// <summary>
/// Waski most do WSPOLNEGO eksportera fragmentow pelnego AMC. Host nie ma
/// wlasnego dekodera ani algorytmu ciecia: linkuje ten sam plik zrodlowy
/// <see cref="AudioClipExporter"/>, ktorego uzywa okno WPF.
/// </summary>
internal static class LiteAudioClipOperations
{
    internal const string ExportOperation = "audio.clipExport";
    internal const string RemoveCapabilitiesOperation = "audio.clipRemoveCapabilities";
    internal const string RemoveOperation = "audio.clipRemoveOriginal";
    internal const string CancelOperation = "audio.clipCancel";
    private const double MaximumMediaSeconds = 31_536_000d;
    private static readonly ConcurrentDictionary<string, CancellationTokenSource> Operations =
        new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, byte> PendingCancellations =
        new(StringComparer.Ordinal);

    internal static object Capabilities(JsonElement args)
    {
        var sourcePath = LiteArgs.RequirePath(args, "sourcePath");
        var ffmpeg = AudioClipExporter.IsFfmpegAvailable;
        return new
        {
            formats = new object[]
            {
                Format(
                    "original",
                    "Bez konwersji; zachowaj oryginalny kodek i jakość; granice mogą zostać dopasowane do ramki kodeka",
                    AudioClipExportFormat.OriginalStream,
                    sourcePath,
                    ffmpeg),
                Format(
                    "flac",
                    "FLAC; dokładny fragment bezstratny; większy plik",
                    AudioClipExportFormat.Flac,
                    sourcePath,
                    ffmpeg),
                Format(
                    "wav",
                    "WAV; dokładny fragment bez kompresji; największy plik",
                    AudioClipExportFormat.Wav,
                    sourcePath,
                    true)
            },
            notice = ffmpeg
                ? "Zapis bez konwersji i FLAC korzystają ze składnika FFmpeg. WAV działa niezależnie."
                : "Składnik FFmpeg nie jest dostępny, dlatego obecnie można zapisać dokładny fragment WAV."
        };
    }

    internal static object RemoveCapabilities(JsonElement args)
    {
        var sourcePath = LiteArgs.RequirePath(args, "sourcePath");
        if (!AudioClipOriginalEditor.IsAvailable)
        {
            return new
            {
                available = false,
                message = "Usuwanie fragmentu z oryginalnego pliku wymaga składnika FFmpeg."
            };
        }
        var availability = CloudFileAvailability.GetEditAvailability(sourcePath);
        if (!availability.CanEdit)
        {
            return new { available = false, message = availability.Message };
        }
        if (LocalAudioFileDiscovery.IsVideoFile(sourcePath))
        {
            return new
            {
                available = false,
                message = "Usuwanie fragmentu z oryginału nie jest jeszcze dostępne dla plików wideo. "
                    + "Ctrl+S może zapisać ich ścieżkę audio do nowego pliku."
            };
        }
        try
        {
            if ((File.GetAttributes(sourcePath) & FileAttributes.ReadOnly) != 0)
            {
                return new { available = false, message = "Plik źródłowy jest tylko do odczytu." };
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new { available = false, message = exception.Message };
        }
        return new { available = true, message = string.Empty };
    }

    internal static object Export(JsonElement args, LiteEventSink events)
    {
        var sourcePath = LiteArgs.RequirePath(args, "sourcePath");
        var destinationPath = LiteArgs.RequirePath(args, "destinationPath");
        var startSeconds = LiteArgs.ReadDouble(
            args, "startSeconds", -1d, 0d, MaximumMediaSeconds);
        var endSeconds = LiteArgs.ReadDouble(
            args, "endSeconds", -1d, 0d, MaximumMediaSeconds);
        if (startSeconds < 0d || endSeconds <= startSeconds)
            throw new LiteRequestException("Początek i koniec fragmentu są nieprawidłowe.");

        var formatValue = LiteArgs.RequireText(args, "format");
        var format = formatValue switch
        {
            "original" => AudioClipExportFormat.OriginalStream,
            "flac" => AudioClipExportFormat.Flac,
            "wav" => AudioClipExportFormat.Wav,
            _ => throw new LiteRequestException("Wybrano nieznany sposób zapisu fragmentu.")
        };
        if (format != AudioClipExportFormat.Wav && !AudioClipExporter.IsFfmpegAvailable)
            throw new LiteRequestException(
                "Ten sposób zapisu wymaga składnika FFmpeg. Możesz wybrać WAV, który działa bez niego.");

        var destinationName = Path.GetFileName(destinationPath);
        var (operationId, cancellation) = BeginOperation(args);
        var lastPercent = -5;
        var progress = new InlineProgress(value =>
        {
            var percent = Math.Clamp((int)Math.Round(value * 100d), 0, 100);
            // Bez zalewania protokolu: poczatek, kazdy kolejny prog 5% i koniec.
            if (percent < 100 && percent < lastPercent + 5) return;
            lastPercent = percent;
            events.Publish("audio.clipExportProgress", new
            {
                operationId,
                percent,
                name = destinationName
            });
        });
        try
        {
            AudioClipExporter.ExportAsync(
                    new AudioClipExportRequest(
                        sourcePath,
                        destinationPath,
                        TimeSpan.FromSeconds(startSeconds),
                        TimeSpan.FromSeconds(endSeconds),
                        format),
                    progress,
                    cancellation.Token)
                .GetAwaiter()
                .GetResult();
            return new
            {
                operationId,
                path = destinationPath,
                name = destinationName,
                format = formatValue
            };
        }
        catch (OperationCanceledException)
        {
            throw new LiteRequestException("Zapisywanie fragmentu zostało anulowane.");
        }
        finally
        {
            EndOperation(operationId, cancellation);
        }
    }

    internal static LiteAudioClipRemovalOutcome Remove(JsonElement args, LiteEventSink events)
    {
        var sourcePath = LiteArgs.RequirePath(args, "sourcePath");
        var startSeconds = LiteArgs.ReadDouble(
            args, "startSeconds", -1d, 0d, MaximumMediaSeconds);
        var endSeconds = LiteArgs.ReadDouble(
            args, "endSeconds", -1d, 0d, MaximumMediaSeconds);
        var durationSeconds = LiteArgs.ReadDouble(
            args, "sourceDurationSeconds", -1d, 0d, MaximumMediaSeconds);
        if (startSeconds < 0d
            || endSeconds <= startSeconds
            || durationSeconds <= 0d
            || endSeconds > durationSeconds)
        {
            throw new LiteRequestException("Początek i koniec fragmentu są nieprawidłowe.");
        }

        var keepBackup = LiteArgs.ReadBool(args, "keepBackup", false);
        var sourceName = Path.GetFileName(sourcePath);
        var (operationId, cancellation) = BeginOperation(args);
        var lastPercent = -5;
        var progress = new InlineProgress(value =>
        {
            var percent = Math.Clamp((int)Math.Round(value * 100d), 0, 100);
            if (percent < 100 && percent < lastPercent + 5) return;
            lastPercent = percent;
            events.Publish("audio.clipRemoveProgress", new
            {
                operationId,
                percent,
                name = sourceName
            });
        });
        try
        {
            var result = AudioClipOriginalEditor.RemoveAsync(
                    new AudioClipRemovalRequest(
                        sourcePath,
                        TimeSpan.FromSeconds(startSeconds),
                        TimeSpan.FromSeconds(endSeconds),
                        TimeSpan.FromSeconds(durationSeconds)),
                    progress,
                    cancellation.Token,
                    keepBackup)
                .GetAwaiter()
                .GetResult();
            return new LiteAudioClipRemovalOutcome(
                operationId,
                sourcePath,
                sourceName,
                result.BackupPath,
                result.Duration.TotalSeconds,
                result.SampleRateHz,
                keepBackup);
        }
        catch (OperationCanceledException)
        {
            throw new LiteRequestException("Usuwanie fragmentu zostało anulowane.");
        }
        finally
        {
            EndOperation(operationId, cancellation);
        }
    }

    internal static object Cancel(JsonElement args)
    {
        var operationId = LiteArgs.RequireText(args, "operationId");
        var found = Operations.TryGetValue(operationId, out var cancellation);
        if (found)
        {
            cancellation!.Cancel();
        }
        else
        {
            // Komenda anulowania moze wyprzedzic rejestracje zadania o kilka
            // instrukcji, gdy uzytkownik zamknie okno natychmiast po starcie.
            // Id jest unikatowe i proces zaraz sie konczy; znacznik przejmie
            // BeginOperation zamiast zgubic prosbe w tej waskiej luce.
            PendingCancellations.TryAdd(operationId, 0);
        }
        return new { operationId, cancelled = true, wasRunning = found };
    }

    private static object Format(
        string value,
        string label,
        AudioClipExportFormat format,
        string sourcePath,
        bool available) => new
        {
            value,
            label,
            extension = AudioClipExporter.SuggestedExtension(sourcePath, format),
            available
        };

    private static (string Id, CancellationTokenSource Cancellation) BeginOperation(
        JsonElement args)
    {
        var operationId = LiteArgs.ReadText(args, "operationId") ?? Guid.NewGuid().ToString("N");
        if (operationId.Length > 64
            || operationId.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-'))
        {
            throw new LiteRequestException("Identyfikator operacji fragmentu jest nieprawidłowy.");
        }
        var cancellation = new CancellationTokenSource();
        if (!Operations.TryAdd(operationId, cancellation))
        {
            cancellation.Dispose();
            throw new LiteRequestException("Operacja fragmentu o tym identyfikatorze już trwa.");
        }
        if (PendingCancellations.TryRemove(operationId, out _)) cancellation.Cancel();
        return (operationId, cancellation);
    }

    private static void EndOperation(string operationId, CancellationTokenSource cancellation)
    {
        if (Operations.TryGetValue(operationId, out var current)
            && ReferenceEquals(current, cancellation))
        {
            Operations.TryRemove(operationId, out _);
        }
        PendingCancellations.TryRemove(operationId, out _);
        cancellation.Dispose();
    }

    private sealed class InlineProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }
}

internal sealed record LiteAudioClipRemovalOutcome(
    string OperationId,
    string SourcePath,
    string SourceName,
    string BackupPath,
    double DurationSeconds,
    int SampleRateHz,
    bool KeepBackup);

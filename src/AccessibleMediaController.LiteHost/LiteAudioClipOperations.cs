using System.Text.Json;
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
    private const double MaximumMediaSeconds = 31_536_000d;

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
        var lastPercent = -5;
        var progress = new InlineProgress(value =>
        {
            var percent = Math.Clamp((int)Math.Round(value * 100d), 0, 100);
            // Bez zalewania protokolu: poczatek, kazdy kolejny prog 5% i koniec.
            if (percent < 100 && percent < lastPercent + 5) return;
            lastPercent = percent;
            events.Publish("audio.clipExportProgress", new
            {
                percent,
                name = destinationName
            });
        });
        AudioClipExporter.ExportAsync(
                new AudioClipExportRequest(
                    sourcePath,
                    destinationPath,
                    TimeSpan.FromSeconds(startSeconds),
                    TimeSpan.FromSeconds(endSeconds),
                    format),
                progress,
                CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        return new
        {
            path = destinationPath,
            name = destinationName,
            format = formatValue
        };
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

    private sealed class InlineProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }
}

using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Channels;

namespace AccessibleMediaController.Windows.Services;

internal static class DiagnosticLog
{
    private const long MaximumLogBytes = 5 * 1024 * 1024;
    private const int RetainedFiles = 5;
    private static readonly object Gate = new();
    private static Channel<string>? _channel;
    private static Task? _writerTask;
    private static string? _logDirectory;

    public static string? CurrentLogPath => _logDirectory is null
        ? null
        : Path.Combine(_logDirectory, "amc.log");

    public static void Initialize(string logDirectory)
    {
        lock (Gate)
        {
            if (_channel is not null) return;
            Directory.CreateDirectory(logDirectory);
            _logDirectory = logDirectory;
            _channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            });
            _writerTask = Task.Run(() => WriteLoopAsync(_channel.Reader, logDirectory));
        }

        var entryAssembly = Assembly.GetEntryAssembly();
        var version = entryAssembly?
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion.Split('+')[0]
            ?? entryAssembly?.GetName().Version?.ToString()
            ?? "nieznana";
        Info("application", $"Start AMC, wersja {version}, proces {Environment.ProcessId}.");
    }

    public static void Info(string area, string message) => Write("INFO", area, message);

    public static void Warning(string area, string message) => Write("WARN", area, message);

    public static void Error(string area, string message, Exception? exception = null)
    {
        var details = exception is null ? message : $"{message}{Environment.NewLine}{exception}";
        Write("ERROR", area, details);
    }

    public static void Shutdown()
    {
        Channel<string>? channel;
        Task? writer;
        lock (Gate)
        {
            channel = _channel;
            writer = _writerTask;
            _channel = null;
            _writerTask = null;
        }
        if (channel is null) return;
        channel.Writer.TryComplete();
        try
        {
            writer?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
            // Logging must never prevent application shutdown.
        }
    }

    private static void Write(string level, string area, string message)
    {
        var channel = _channel;
        if (channel is null) return;
        var line = $"{DateTimeOffset.Now:O} [{level}] [{area}] {message}";
        channel.Writer.TryWrite(line);
    }

    private static async Task WriteLoopAsync(ChannelReader<string> reader, string directory)
    {
        try
        {
            await foreach (var line in reader.ReadAllAsync())
            {
                RotateIfRequired(directory, Encoding.UTF8.GetByteCount(line) + Environment.NewLine.Length);
                await File.AppendAllTextAsync(
                    Path.Combine(directory, "amc.log"),
                    line + Environment.NewLine,
                    Encoding.UTF8);
            }
        }
        catch (Exception)
        {
            // A diagnostic facility cannot become a new failure mode.
        }
    }

    private static void RotateIfRequired(string directory, int incomingBytes)
    {
        var current = Path.Combine(directory, "amc.log");
        if (!File.Exists(current) || new FileInfo(current).Length + incomingBytes <= MaximumLogBytes) return;

        var oldest = Path.Combine(directory, $"amc.{RetainedFiles - 1}.log");
        if (File.Exists(oldest)) File.Delete(oldest);
        for (var index = RetainedFiles - 2; index >= 1; index--)
        {
            var source = Path.Combine(directory, $"amc.{index}.log");
            var destination = Path.Combine(directory, $"amc.{index + 1}.log");
            if (File.Exists(source)) File.Move(source, destination, true);
        }
        File.Move(current, Path.Combine(directory, "amc.1.log"), true);
    }
}

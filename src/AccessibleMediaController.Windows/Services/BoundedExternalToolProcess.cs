using System.Diagnostics;
using System.IO;
using System.Text;

namespace AccessibleMediaController.Windows.Services;

/// <summary>
/// Executes one owned external tool process while continuously bounding both
/// redirected output streams. A tool cannot fill a pipe or memory before its
/// result is rejected.
/// </summary>
internal static class BoundedExternalToolProcess
{
    internal sealed record Result(string StandardOutput, string StandardError, int ExitCode);

    internal static async Task<Result> RunAsync(
        ProcessStartInfo start,
        TimeSpan timeout,
        int maximumStandardOutputCharacters,
        int maximumStandardErrorCharacters,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(start);
        if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));
        if (maximumStandardOutputCharacters <= 0) throw new ArgumentOutOfRangeException(nameof(maximumStandardOutputCharacters));
        if (maximumStandardErrorCharacters <= 0) throw new ArgumentOutOfRangeException(nameof(maximumStandardErrorCharacters));

        using var process = Process.Start(start)
            ?? throw new InvalidDataException("Nie udało się uruchomić narzędzia rozdziałów.");
        process.StandardInput.Close();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        using var terminateOnDeadline = deadline.Token.Register(
            static state => KillOwnedProcess((Process)state!), process);
        var outputTask = ReadBoundedAsync(
            process.StandardOutput,
            maximumStandardOutputCharacters,
            "Wynik narzędzia rozdziałów jest zbyt duży.");
        var errorTask = ReadBoundedAsync(
            process.StandardError,
            maximumStandardErrorCharacters,
            "Diagnostyka narzędzia rozdziałów jest zbyt duża.");
        var exitTask = process.WaitForExitAsync(deadline.Token);

        try
        {
            while (!exitTask.IsCompleted)
            {
                var pending = new List<Task>(3) { exitTask };
                if (!outputTask.IsCompleted) pending.Add(outputTask);
                if (!errorTask.IsCompleted) pending.Add(errorTask);
                var completed = await Task.WhenAny(pending).ConfigureAwait(false);
                if (completed == outputTask && outputTask.IsFaulted)
                    await ThrowAfterTerminationAsync(process, outputTask, errorTask).ConfigureAwait(false);
                if (completed == errorTask && errorTask.IsFaulted)
                    await ThrowAfterTerminationAsync(process, errorTask, outputTask).ConfigureAwait(false);
            }

            await exitTask.ConfigureAwait(false);
            var output = await outputTask.ConfigureAwait(false);
            var error = await errorTask.ConfigureAwait(false);
            return new Result(output, error, process.ExitCode);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            await DrainAfterTerminationAsync(process, outputTask, errorTask).ConfigureAwait(false);
            throw new TimeoutException("Narzędzie rozdziałów nie odpowiedziało w bezpiecznym czasie.");
        }
        catch
        {
            await DrainAfterTerminationAsync(process, outputTask, errorTask).ConfigureAwait(false);
            throw;
        }
    }

    private static async Task ThrowAfterTerminationAsync(
        Process process,
        Task failed,
        Task other)
    {
        var exception = failed.Exception?.GetBaseException()
            ?? new InvalidDataException("Nie udało się odczytać danych narzędzia rozdziałów.");
        await DrainAfterTerminationAsync(process, failed, other).ConfigureAwait(false);
        System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception).Throw();
    }

    private static async Task DrainAfterTerminationAsync(Process process, params Task[] readers)
    {
        KillOwnedProcess(process);
        try
        {
            await process.WaitForExitAsync().ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            // The process can exit between the ownership check and the wait.
        }
        try
        {
            await Task.WhenAll(readers).ConfigureAwait(false);
        }
        catch
        {
            // The caller receives the first, classified failure. The remaining
            // bounded reader was only awaited to release redirected pipes.
        }
    }

    private static void KillOwnedProcess(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
        }
        catch (System.ComponentModel.Win32Exception)
        {
        }
    }

    private static async Task<string> ReadBoundedAsync(
        StreamReader reader,
        int maximumCharacters,
        string overflowMessage)
    {
        var output = new StringBuilder(Math.Min(maximumCharacters, 8 * 1024));
        var buffer = new char[8 * 1024];
        while (true)
        {
            var read = await reader.ReadAsync(buffer.AsMemory()).ConfigureAwait(false);
            if (read == 0) return output.ToString();
            if (output.Length > maximumCharacters - read)
                throw new InvalidDataException(overflowMessage);
            output.Append(buffer, 0, read);
        }
    }
}

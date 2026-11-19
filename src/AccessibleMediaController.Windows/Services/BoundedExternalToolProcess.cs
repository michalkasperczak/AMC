using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
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
        using var containment = CreateOwnedProcessJob(process);
        process.StandardInput.Close();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        using var terminateOnDeadline = deadline.Token.Register(
            static state => ((OwnedProcessJob)state!).Terminate(), containment);
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
                    await ThrowAfterTerminationAsync(process, containment, outputTask, errorTask).ConfigureAwait(false);
                if (completed == errorTask && errorTask.IsFaulted)
                    await ThrowAfterTerminationAsync(process, containment, errorTask, outputTask).ConfigureAwait(false);
            }

            await exitTask.ConfigureAwait(false);
            var output = await outputTask.WaitAsync(deadline.Token).ConfigureAwait(false);
            var error = await errorTask.WaitAsync(deadline.Token).ConfigureAwait(false);
            return new Result(output, error, process.ExitCode);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            await DrainAfterTerminationAsync(process, containment, outputTask, errorTask).ConfigureAwait(false);
            throw new TimeoutException("Narzędzie rozdziałów nie odpowiedziało w bezpiecznym czasie.");
        }
        catch
        {
            await DrainAfterTerminationAsync(process, containment, outputTask, errorTask).ConfigureAwait(false);
            throw;
        }
    }

    private static async Task ThrowAfterTerminationAsync(
        Process process,
        OwnedProcessJob containment,
        Task failed,
        Task other)
    {
        var exception = failed.Exception?.GetBaseException()
            ?? new InvalidDataException("Nie udało się odczytać danych narzędzia rozdziałów.");
        await DrainAfterTerminationAsync(process, containment, failed, other).ConfigureAwait(false);
        System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception).Throw();
    }

    private static async Task DrainAfterTerminationAsync(
        Process process,
        OwnedProcessJob containment,
        params Task[] readers)
    {
        containment.Terminate();
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

    private static OwnedProcessJob CreateOwnedProcessJob(Process process)
    {
        try
        {
            return OwnedProcessJob.Create(process);
        }
        catch
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
            throw;
        }
    }

    private sealed class OwnedProcessJob : IDisposable
    {
        private const uint JobObjectLimitKillOnJobClose = 0x00002000;
        private const int JobObjectExtendedLimitInformationClass = 9;
        private IntPtr handle;

        private OwnedProcessJob(IntPtr handle)
        {
            this.handle = handle;
        }

        internal static OwnedProcessJob Create(Process process)
        {
            var handle = CreateJobObject(IntPtr.Zero, null);
            if (handle == IntPtr.Zero)
                throw new InvalidDataException("Nie udało się utworzyć bezpiecznego kontenera narzędzia rozdziałów.");
            var job = new OwnedProcessJob(handle);
            try
            {
                var limits = new JobObjectExtendedLimitInformation
                {
                    BasicLimitInformation = new JobObjectBasicLimitInformation
                    {
                        LimitFlags = JobObjectLimitKillOnJobClose
                    }
                };
                if (!SetInformationJobObject(
                    handle,
                    JobObjectExtendedLimitInformationClass,
                    ref limits,
                    (uint)Marshal.SizeOf<JobObjectExtendedLimitInformation>()))
                    throw new InvalidDataException("Nie udało się ograniczyć procesu narzędzia rozdziałów.");
                if (!AssignProcessToJobObject(handle, process.Handle))
                    throw new InvalidDataException("Nie udało się objąć procesu narzędzia rozdziałów kontrolą.");
                return job;
            }
            catch
            {
                job.Dispose();
                throw;
            }
        }

        internal void Terminate()
        {
            var ownedHandle = Interlocked.Exchange(ref handle, IntPtr.Zero);
            if (ownedHandle != IntPtr.Zero) CloseHandle(ownedHandle);
        }

        public void Dispose() => Terminate();

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateJobObject(IntPtr jobAttributes, string? name);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetInformationJobObject(
            IntPtr job,
            int informationClass,
            ref JobObjectExtendedLimitInformation information,
            uint informationLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr handle);

        [StructLayout(LayoutKind.Sequential)]
        private struct JobObjectBasicLimitInformation
        {
            internal long PerProcessUserTimeLimit;
            internal long PerJobUserTimeLimit;
            internal uint LimitFlags;
            internal UIntPtr MinimumWorkingSetSize;
            internal UIntPtr MaximumWorkingSetSize;
            internal uint ActiveProcessLimit;
            internal IntPtr Affinity;
            internal uint PriorityClass;
            internal uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IoCounters
        {
            internal ulong ReadOperationCount;
            internal ulong WriteOperationCount;
            internal ulong OtherOperationCount;
            internal ulong ReadTransferCount;
            internal ulong WriteTransferCount;
            internal ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JobObjectExtendedLimitInformation
        {
            internal JobObjectBasicLimitInformation BasicLimitInformation;
            internal IoCounters IoInfo;
            internal UIntPtr ProcessMemoryLimit;
            internal UIntPtr JobMemoryLimit;
            internal UIntPtr PeakProcessMemoryUsed;
            internal UIntPtr PeakJobMemoryUsed;
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

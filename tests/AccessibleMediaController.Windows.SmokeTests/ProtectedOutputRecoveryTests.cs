using System.Runtime.InteropServices;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Windows.Services;
using NAudio.Wave;

internal static class ProtectedOutputRecoveryTests
{
    internal static void Run()
    {
        PreservesSilenceUntilExplicitResume();
        ResumesAutomaticallyWhenProtectionIsDisabled();
        DoesNotAutoplayAfterUserPauseOrLateStopCallback();
    }

    private static void PreservesSilenceUntilExplicitResume()
    {
        var root = Path.Combine(Path.GetTempPath(), "amc-protected-output-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var wavePath = Path.Combine(root, "same-item.wav");
            WriteWave(wavePath);
            var item = new MediaItem { Id = "same-item", Source = wavePath, Title = "Ta sama pozycja" };
            var outputs = new List<FakeWavePlayer>();
            using var factory = AudioOutputDeviceCatalog.OverrideOutputFactoryForTesting((_, _) =>
            {
                var output = new FakeWavePlayer();
                outputs.Add(output);
                // Pierwszy obiekt reprezentuje wybrany Denon; po jego utracie
                // katalog zwraca już dostępne wyjście domyślne.
                return new AudioOutputDeviceLease(
                    output,
                    device: null,
                    usesDefaultDevice: outputs.Count > 1,
                    endpointId: outputs.Count > 1 ? "default-speakers" : "denon");
            });
            using var output = new WindowsMediaOutput();
            using var started = new ManualResetEventSlim();
            using var fallback = new ManualResetEventSlim();
            output.PlaybackStarted += (_, _) => started.Set();
            output.OutputDeviceFallback += (_, _) => fallback.Set();
            output.ConfigureOutputDevice("denon");
            output.ConfigureOutputFallbackBehavior(true);

            output.Play(item, TimeSpan.FromMilliseconds(250), 37, 1.25d);
            Check(started.Wait(TimeSpan.FromSeconds(5)), "Nie uruchomiono początkowego toru tej samej pozycji.");
            Check(outputs.Count == 1 && outputs[0].PlayCalls == 1,
                "Początkowa pozycja nie uruchomiła rzeczywistego toru wyjścia.");

            outputs[0].RaiseStopped(new COMException("Urządzenie odłączone", unchecked((int)0x88890004)));
            Check(fallback.Wait(TimeSpan.FromSeconds(5)),
                "Utrata wybranego wyjścia nie zbudowała ochronnego wyjścia domyślnego.");
            Check(outputs.Count == 2 && outputs[1].PlayCalls == 0,
                "Ochrona uruchomiła dźwięk automatycznie po utracie urządzenia.");
            Check(output.LoadedItemId == item.Id,
                "Odzyskiwanie utraciło tożsamość bieżącej pozycji.");

            // To odpowiada produkcyjnemu wznowieniu z Space/Enter: publiczne
            // Play tej samej pozycji nie może wymagać zmiany stacji/utworu.
            output.Play(item, TimeSpan.FromMilliseconds(250), 37, 1.25d);
            SpinWait.SpinUntil(() => outputs[1].PlayCalls == 1, TimeSpan.FromSeconds(5));
            Check(outputs.Count == 2 && outputs[1].PlayCalls == 1,
                "Jawne wznowienie tej samej pozycji nie odzyskało dźwięku na domyślnym wyjściu.");
            Check(output.LoadedItemId == item.Id,
                "Jawne wznowienie zmieniło bieżącą pozycję.");
            output.Stop();
            output.WaitForPendingPipelineDisposalAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            Console.WriteLine("OK: utrata wyjścia pozostawia ciszę, a Play tej samej pozycji wznawia na domyślnym");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static void ResumesAutomaticallyWhenProtectionIsDisabled()
    {
        var root = Path.Combine(Path.GetTempPath(), "amc-unprotected-output-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var wavePath = Path.Combine(root, "same-item.wav");
            WriteWave(wavePath);
            var item = new MediaItem { Id = "same-item", Source = wavePath, Title = "Ta sama pozycja" };
            var outputs = new List<FakeWavePlayer>();
            using var factory = AudioOutputDeviceCatalog.OverrideOutputFactoryForTesting((_, _) =>
            {
                var replacement = new FakeWavePlayer();
                outputs.Add(replacement);
                return new AudioOutputDeviceLease(
                    replacement,
                    device: null,
                    usesDefaultDevice: outputs.Count > 1,
                    endpointId: outputs.Count > 1 ? "default-speakers" : "denon");
            });
            using var output = new WindowsMediaOutput();
            using var started = new ManualResetEventSlim();
            using var fallback = new ManualResetEventSlim();
            output.PlaybackStarted += (_, _) => started.Set();
            output.OutputDeviceFallback += (_, _) => fallback.Set();
            output.ConfigureOutputDevice("denon");
            output.ConfigureOutputFallbackBehavior(false);

            output.Play(item, TimeSpan.Zero, 37, 1d);
            Check(started.Wait(TimeSpan.FromSeconds(5)), "Nie uruchomiono początkowego toru bez ochronnej pauzy.");
            outputs[0].RaiseStopped(new COMException("Urządzenie odłączone", unchecked((int)0x88890004)));
            Check(fallback.Wait(TimeSpan.FromSeconds(5)),
                "Utrata wybranego wyjścia nie zbudowała fallbacku bez ochronnej pauzy.");
            SpinWait.SpinUntil(() => outputs.Count == 2 && outputs[1].PlayCalls == 1, TimeSpan.FromSeconds(5));
            Check(outputs.Count == 2 && outputs[1].PlayCalls == 1,
                "Fallback bez ochronnej pauzy nie wznowił odtwarzania po utracie urządzenia.");
            output.Stop();
            output.WaitForPendingPipelineDisposalAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static void DoesNotAutoplayAfterUserPauseOrLateStopCallback()
    {
        var root = Path.Combine(Path.GetTempPath(), "amc-paused-output-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var wavePath = Path.Combine(root, "same-item.wav");
            WriteWave(wavePath);
            var item = new MediaItem { Id = "same-item", Source = wavePath, Title = "Ta sama pozycja" };
            var outputs = new List<FakeWavePlayer>();
            using var factory = AudioOutputDeviceCatalog.OverrideOutputFactoryForTesting((_, _) =>
            {
                var replacement = new FakeWavePlayer();
                outputs.Add(replacement);
                return new AudioOutputDeviceLease(
                    replacement,
                    device: null,
                    usesDefaultDevice: outputs.Count > 1,
                    endpointId: outputs.Count > 1 ? "default-speakers" : "denon");
            });
            using var output = new WindowsMediaOutput();
            using var started = new ManualResetEventSlim();
            using var fallback = new ManualResetEventSlim();
            output.PlaybackStarted += (_, _) => started.Set();
            output.OutputDeviceFallback += (_, _) => fallback.Set();
            output.ConfigureOutputDevice("denon");
            output.ConfigureOutputFallbackBehavior(false);

            output.Play(item, TimeSpan.FromMilliseconds(500), 37, 1d);
            Check(started.Wait(TimeSpan.FromSeconds(5)), "Nie uruchomiono toru do testu świadomej pauzy.");
            output.Pause();
            outputs[0].RaiseStopped(new COMException("Urządzenie odłączone", unchecked((int)0x88890004)));
            Check(fallback.Wait(TimeSpan.FromSeconds(5)),
                "Pauza użytkownika nie dopuściła do zbudowania cichego fallbacku.");
            Check(outputs.Count == 2 && outputs[1].PlayCalls == 0,
                "Fallback po pauzie użytkownika wznowił dźwięk mimo _pauseRequested.");

            output.Stop();
            outputs[1].RaiseStopped(new COMException("Spóźniony callback", unchecked((int)0x88890004)));
            Thread.Sleep(300);
            Check(outputs.Count == 2,
                "Spóźniony callback po Stop otworzył nowy tor odtwarzania.");
            output.WaitForPendingPipelineDisposalAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static void WriteWave(string path)
    {
        const int sampleRate = 8_000;
        const short channels = 1;
        const short bits = 16;
        var samples = Enumerable.Range(0, sampleRate * 2)
            .Select(index => (short)(index % 40 < 20 ? 4_000 : -4_000)).ToArray();
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8.ToArray());
        writer.Write(36 + samples.Length * sizeof(short));
        writer.Write("WAVEfmt "u8.ToArray());
        writer.Write(16);
        writer.Write((short)1);
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channels * bits / 8);
        writer.Write((short)(channels * bits / 8));
        writer.Write(bits);
        writer.Write("data"u8.ToArray());
        writer.Write(samples.Length * sizeof(short));
        foreach (var sample in samples) writer.Write(sample);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class FakeWavePlayer : IWavePlayer
    {
        public event EventHandler<StoppedEventArgs>? PlaybackStopped;
        public PlaybackState PlaybackState { get; private set; } = PlaybackState.Stopped;
        public float Volume { get; set; }
        public WaveFormat OutputWaveFormat { get; private set; } = WaveFormat.CreateIeeeFloatWaveFormat(8_000, 1);
        public int PlayCalls { get; private set; }

        public void Init(IWaveProvider waveProvider)
        {
            ArgumentNullException.ThrowIfNull(waveProvider);
            OutputWaveFormat = waveProvider.WaveFormat;
        }
        public void Play() { ++PlayCalls; PlaybackState = PlaybackState.Playing; }
        public void Pause() => PlaybackState = PlaybackState.Paused;
        public void Stop() => PlaybackState = PlaybackState.Stopped;
        public void Dispose() => PlaybackState = PlaybackState.Stopped;

        public void RaiseStopped(Exception exception)
        {
            PlaybackState = PlaybackState.Stopped;
            PlaybackStopped?.Invoke(this, new StoppedEventArgs(exception));
        }
    }
}

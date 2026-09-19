using System.Reflection;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Windows.Services;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

internal static class TimeshiftRateIntegrationTests
{
    internal static void Run()
    {
        var failures = new List<string>();
        Check("Pierwsze polecenie i pauza", () =>
        {
            using var fixture = new Fixture(WaveFormat.CreateIeeeFloatWaveFormat(44100, 2));
            fixture.Write(5);
            var session = new DemoMediaSession("radio", "Próba", new[] { fixture.Item }, fixture.Radio);
            if (!session.SetPlaybackRate(1.5d) || session.PlaybackRate != 1.5d)
                throw new Exception("Realne RadioMediaOutput odrzuca w modelu pierwsze tempo przed następnym Read.");
            if (!session.SetPlaybackRate(0.75d) || session.PlaybackRate != 0.75d)
                throw new Exception("Zmiana na pauzie wymaga odczytu dźwięku, żeby model potwierdził właściwe tempo.");
        });
        foreach (var period in new[] { 0.02d, 0.05d, 0.10d, 0.20d })
        Check($"Stały powrót do live; blok {period}s", () =>
        {
            using var fixture = new Fixture(WaveFormat.CreateIeeeFloatWaveFormat(44100, 2));
            fixture.Write(3);
            fixture.Radio.SetPlaybackRate(1.5d);
            var sawFast = false;
            var reachedLive = false;
            var reaccelerations = 0;
            var notices = 0;
            var noticeAtWrongRate = false;
            var notification = typeof(TimeshiftTempoStage).GetEvent("NormalTempoResumed");
            notification?.AddEventHandler(fixture.Stage, (EventHandler)((_, _) =>
            {
                notices++;
                if (fixture.Stage!.EffectiveTempo != 1d) noticeAtWrongRate = true;
            }));
            long silent = 0, samples = 0;
            var chunk = new byte[(int)Math.Round(fixture.Stage!.WaveFormat.AverageBytesPerSecond * period)];
            for (var tick = 0; tick < (int)Math.Round(12d / period); tick++)
            {
                fixture.Write(period);
                var read = fixture.Stage.Read(chunk, 0, chunk.Length);
                var rate = fixture.Radio.PlaybackRate;
                if (rate > 1.01d)
                {
                    sawFast = true;
                    if (reachedLive) reaccelerations++;
                }
                else if (sawFast) reachedLive = true;
                for (var i = 0; i + 4 <= read; i += 4)
                {
                    samples++;
                    if (Math.Abs(BitConverter.ToSingle(chunk, i)) < 0.000001) silent++;
                }
            }
            Console.WriteLine($"REAL_BUFFER_LIVE|block={period:0.00}|fast={sawFast}|reached={reachedLive}|reaccelerations={reaccelerations}|silence={100d * silent / samples:0.000}%|behind={fixture.Radio.BehindLive.TotalSeconds:0.000}");
            if (!sawFast || !reachedLive) throw new Exception("Próba nie doszła od przyspieszonego bufora do live.");
            if (reaccelerations != 0) throw new Exception("Po samoczynnym powrocie do1x tempo samo przyspiesza ponownie.");
            if (notices != 1 || noticeAtWrongRate)
                throw new Exception($"Powrót do1x nie daje pojedynczego prawdziwego powiadomienia: {notices}.");
            if (100d * silent / samples > 5d) throw new Exception("Ciągły dopływ1x daje nadmierną ciszę po dojściu do live.");
        });
        Check("End zeruje tempo bez Read", () =>
        {
            using var fixture = new Fixture(new WaveFormat(44100, 16, 2));
            fixture.Write(5);
            fixture.Radio.SetPlaybackRate(1.5d);
            var chunk = new byte[fixture.Stage!.WaveFormat.AverageBytesPerSecond / 10];
            fixture.Stage.Read(chunk, 0, chunk.Length);
            if (fixture.Radio.PlaybackRate < 1.4d) throw new Exception("Brak dodatniej kontroli przyspieszenia.");
            fixture.Write(3, hertz: 880);
            fixture.Radio.JumpToLive();
            var afterJumpRate = fixture.Radio.PlaybackRate;
            var read = fixture.Stage.Read(chunk, 0, chunk.Length);
            var hertz = Frequency(chunk, read, fixture.Stage.WaveFormat);
            Console.WriteLine($"REAL_JUMP|rate={afterJumpRate:0.00}|hertz={hertz:0.0}");
            if (afterJumpRate != 1d || Math.Abs(hertz - 880d) > 40d)
                throw new Exception("End nie wyzerował tempa albo zostawił dźwięk sprzed przewinięcia.");
        });
        Check("Seek czyści poprzedni dźwięk", () =>
        {
            using var fixture = new Fixture(WaveFormat.CreateIeeeFloatWaveFormat(44100, 2));
            fixture.Write(5);
            fixture.Radio.SetPlaybackRate(1.5d);
            var chunk = new byte[fixture.Stage!.WaveFormat.AverageBytesPerSecond / 10];
            fixture.Stage.Read(chunk, 0, chunk.Length);
            fixture.Write(3, hertz: 880);
            fixture.Radio.Seek(TimeSpan.FromSeconds(5));
            var read = fixture.Stage.Read(chunk, 0, chunk.Length);
            var hertz = Frequency(chunk, read, fixture.Stage.WaveFormat);
            Console.WriteLine($"REAL_SEEK|hertz={hertz:0.0}");
            if (Math.Abs(hertz - 880d) > 40d)
                throw new Exception("Po Seek nadal słychać próbki przetworzone przed przewinięciem.");
        });
        Check("Brak etapu nie udaje możliwości", () =>
        {
            using var fixture = new Fixture(new WaveFormat(44100, 8, 1));
            fixture.Write(2);
            if (fixture.Stage is not null) throw new Exception("Warunek wejściowy: format niespodziewanie ma etap.");
            var output = new byte[fixture.Source.WaveFormat.AverageBytesPerSecond / 10];
            if (fixture.Source.Read(output, 0, output.Length) != output.Length || output.All(b => b == 0))
                throw new Exception("Tor bez tempa przestał podawać dźwięk.");
            if (fixture.Radio.SupportsPlaybackRate) throw new Exception("Radio nadal deklaruje wsparcie tempa mimo braku etapu.");
        });
        if (failures.Count != 0) throw new Exception(string.Join(" | ", failures));
        Console.WriteLine("OK: realny bufor, RadioMediaOutput i model sesji potwierdzają tempo oraz stabilny powrót do live");
        return;

        void Check(string name, Action test)
        {
            try { test(); }
            catch (Exception exception) { failures.Add(name + ": " + exception.Message); }
        }
    }

    private static double Frequency(byte[] audio, int count, WaveFormat format)
    {
        var crossings = 0;
        var frames = count / format.BlockAlign;
        for (var frame = 1; frame < frames; frame++)
        {
            var previous = BitConverter.ToSingle(audio, (frame - 1) * format.BlockAlign);
            var current = BitConverter.ToSingle(audio, frame * format.BlockAlign);
            if ((previous < 0 && current >= 0) || (previous >= 0 && current < 0)) crossings++;
        }
        return crossings * format.SampleRate / (2d * Math.Max(1, frames - 1));
    }

    private sealed class Fixture : IDisposable
    {
        private readonly object _buffer;
        private readonly MethodInfo _write;
        private long _frame;
        public RadioMediaOutput Radio { get; } = new(1, audible: false);
        public MediaItem Item { get; } = new() { Id = "radio-test", Title = "Jawna próbka TimeShift", Source = "https://127.0.0.1:9/no-network", Kind = MediaItemKind.Station };
        public IWaveProvider Source { get; }
        public TimeshiftTempoStage? Stage { get; }

        public Fixture(WaveFormat format)
        {
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var bufferType = typeof(RadioMediaOutput).GetNestedType("RadioTimeshiftWaveProvider", BindingFlags.NonPublic)!;
            _buffer = Activator.CreateInstance(bufferType, flags, null,
                new object[] { format, 1, (long)format.AverageBytesPerSecond * 20, (Action<Exception>)(e => throw e) }, null)!;
            Source = (IWaveProvider)_buffer;
            _write = bufferType.GetMethod("Write", flags)!;
            var behind = bufferType.GetProperty("BehindLive", flags)!;
            Stage = TimeshiftTempoStage.TryCreate(Source, () => (TimeSpan)behind.GetValue(_buffer)!);
            var pipelineType = typeof(RadioMediaOutput).GetNestedType("RadioPipeline", BindingFlags.NonPublic)!;
            var ctor = pipelineType.GetConstructors(flags).Single();
            var callbackType = ctor.GetParameters()[10].ParameterType;
            var callback = typeof(RadioMediaOutput).GetMethod("Pipeline_StreamTitleChanged", flags)!.CreateDelegate(callbackType, Radio);
            var samples = Stage is not null ? Stage.ToSampleProvider() : Source.ToSampleProvider();
            var pipeline = ctor.Invoke(new object?[] { Item, Source, new MemoryStream(), new ResolvedRadioSource(Item.Source!, false, false), _buffer, Stage, new VolumeSampleProvider(samples), null, new CancellationTokenSource(), null, callback });
            typeof(RadioMediaOutput).GetField("_pipeline", flags)!.SetValue(Radio, pipeline);
        }

        public void Write(double seconds, double hertz = 440)
        {
            var format = Source.WaveFormat;
            var frames = (int)Math.Round(format.SampleRate * seconds);
            var bytes = new byte[frames * format.BlockAlign];
            for (var f = 0; f < frames; f++, _frame++)
            {
                var value = 0.5 * Math.Sin(2 * Math.PI * hertz * _frame / format.SampleRate);
                for (var c = 0; c < format.Channels; c++)
                {
                    var offset = f * format.BlockAlign + c * format.BitsPerSample / 8;
                    if (format.Encoding == WaveFormatEncoding.IeeeFloat) BitConverter.TryWriteBytes(bytes.AsSpan(offset, 4), (float)value);
                    else if (format.BitsPerSample == 16) BitConverter.TryWriteBytes(bytes.AsSpan(offset, 2), (short)(value * 32767));
                    else bytes[offset] = (byte)(128 + 120 * value);
                }
            }
            _write.Invoke(_buffer, new object[] { bytes, 0, bytes.Length });
        }
        public void Dispose() => Radio.Dispose();
    }
}

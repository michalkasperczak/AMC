using System.IO;
using NAudio.Wave;

namespace AccessibleMediaController.Windows.Services;

/// <summary>
/// Applies a conservative, real-time gain correction to decoded floating-point
/// samples. The user volume provider belongs after this processor so changing
/// the player volume never makes the normalizer compensate in the opposite
/// direction.
/// </summary>
public sealed class LoudnessNormalizationSampleProvider : ISampleProvider
{
    private const double TargetRootMeanSquare = 0.18d;
    private const double SilenceGateRootMeanSquare = 0.003d;
    private const double MinimumGain = 0.25d;
    private const double MaximumGain = 4d;
    private const double MaximumPeak = 0.98d;
    private readonly ISampleProvider _inner;
    private int _enabled;
    private int _resetRequested = 1;
    private double _smoothedPower;
    private double _currentGain = 1d;

    public LoudnessNormalizationSampleProvider(ISampleProvider inner, bool enabled = false)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        if (inner.WaveFormat.SampleRate <= 0 || inner.WaveFormat.Channels <= 0)
        {
            throw new ArgumentException("Tor normalizacji ma nieprawidłowy format dźwięku.", nameof(inner));
        }
        WaveFormat = inner.WaveFormat;
        Enabled = enabled;
    }

    public WaveFormat WaveFormat { get; }

    public bool Enabled
    {
        get => Volatile.Read(ref _enabled) != 0;
        set
        {
            var resolved = value ? 1 : 0;
            if (Interlocked.Exchange(ref _enabled, resolved) != resolved)
            {
                Interlocked.Exchange(ref _resetRequested, 1);
            }
        }
    }

    public int Read(float[] buffer, int offset, int count)
    {
        var read = _inner.Read(buffer, offset, count);
        if (read <= 0 || !Enabled) return read;
        if (read % WaveFormat.Channels != 0)
        {
            throw new InvalidDataException("Normalizator otrzymał niepełną ramkę dźwięku.");
        }

        double power = 0d;
        var peak = 0d;
        var finiteSamples = 0;
        for (var index = offset; index < offset + read; index++)
        {
            var sample = buffer[index];
            if (!float.IsFinite(sample)) continue;
            var absolute = Math.Abs((double)sample);
            peak = Math.Max(peak, absolute);
            power += sample * (double)sample;
            finiteSamples++;
        }
        if (finiteSamples == 0) return read;

        var blockPower = power / finiteSamples;
        var blockRootMeanSquare = Math.Sqrt(blockPower);
        if (blockRootMeanSquare < SilenceGateRootMeanSquare) return read;

        var blockSeconds = read /
            (double)(WaveFormat.SampleRate * WaveFormat.Channels);
        var reset = Interlocked.Exchange(ref _resetRequested, 0) != 0
            || _smoothedPower <= 0d;
        if (reset)
        {
            _smoothedPower = blockPower;
        }
        else
        {
            var powerBlend = 1d - Math.Exp(-blockSeconds / 0.75d);
            _smoothedPower += (blockPower - _smoothedPower) * powerBlend;
        }

        var measuredRootMeanSquare = Math.Sqrt(Math.Max(_smoothedPower, double.Epsilon));
        var desiredGain = Math.Clamp(
            TargetRootMeanSquare / measuredRootMeanSquare,
            MinimumGain,
            MaximumGain);
        if (reset)
        {
            _currentGain = desiredGain;
        }
        else
        {
            var timeConstant = desiredGain < _currentGain ? 0.08d : 1.25d;
            var gainBlend = 1d - Math.Exp(-blockSeconds / timeConstant);
            _currentGain += (desiredGain - _currentGain) * gainBlend;
        }

        var peakLimitedGain = peak > 0d
            ? Math.Min(_currentGain, MaximumPeak / peak)
            : _currentGain;
        for (var index = offset; index < offset + read; index++)
        {
            if (!float.IsFinite(buffer[index])) continue;
            buffer[index] = (float)Math.Clamp(
                buffer[index] * peakLimitedGain,
                -MaximumPeak,
                MaximumPeak);
        }
        return read;
    }
}

/// <summary>
/// Adds a short fade-in, a natural fade-out near the end of a file and an
/// on-demand fade-out used when the user manually changes tracks.
/// </summary>
public sealed class TrackTransitionSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _inner;
    private readonly Func<TimeSpan> _positionProvider;
    private readonly Func<TimeSpan> _durationProvider;
    private long _framesRead;
    private long _manualFadeTotalFrames;
    private long _manualFadeRemainingFrames;
    private int _fadeDurationMilliseconds;

    public TrackTransitionSampleProvider(
        ISampleProvider inner,
        Func<TimeSpan> positionProvider,
        Func<TimeSpan> durationProvider,
        int fadeDurationMilliseconds = 0)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _positionProvider = positionProvider ?? throw new ArgumentNullException(nameof(positionProvider));
        _durationProvider = durationProvider ?? throw new ArgumentNullException(nameof(durationProvider));
        if (inner.WaveFormat.SampleRate <= 0 || inner.WaveFormat.Channels <= 0)
        {
            throw new ArgumentException("Tor przejścia ma nieprawidłowy format dźwięku.", nameof(inner));
        }
        WaveFormat = inner.WaveFormat;
        FadeDurationMilliseconds = fadeDurationMilliseconds;
    }

    public WaveFormat WaveFormat { get; }

    public int FadeDurationMilliseconds
    {
        get => Volatile.Read(ref _fadeDurationMilliseconds);
        set => Volatile.Write(ref _fadeDurationMilliseconds, Math.Clamp(value, 0, 10_000));
    }

    public void BeginManualFadeOut()
    {
        var fadeFrames = FadeFrameCount();
        Interlocked.Exchange(ref _manualFadeTotalFrames, fadeFrames);
        Interlocked.Exchange(ref _manualFadeRemainingFrames, fadeFrames);
    }

    public int Read(float[] buffer, int offset, int count)
    {
        var startPosition = SafeTime(_positionProvider());
        var read = _inner.Read(buffer, offset, count);
        if (read <= 0) return read;
        var channels = WaveFormat.Channels;
        if (read % channels != 0)
        {
            throw new InvalidDataException("Tor przejścia otrzymał niepełną ramkę dźwięku.");
        }

        var fadeFrames = FadeFrameCount();
        if (fadeFrames <= 0)
        {
            _framesRead += read / channels;
            return read;
        }

        var duration = SafeTime(_durationProvider());
        var endPosition = SafeTime(_positionProvider());
        var frames = read / channels;
        var manualRemaining = Interlocked.Read(ref _manualFadeRemainingFrames);
        var manualTotal = Interlocked.Read(ref _manualFadeTotalFrames);
        for (var frame = 0; frame < frames; frame++)
        {
            var factor = SmoothStep((_framesRead + frame + 1d) / fadeFrames);
            if (duration > TimeSpan.Zero)
            {
                var progress = (frame + 1d) / frames;
                var framePositionTicks = startPosition.Ticks
                    + (long)((endPosition.Ticks - startPosition.Ticks) * progress);
                var remainingTicks = Math.Max(0L, duration.Ticks - framePositionTicks);
                var naturalFactor = remainingTicks /
                    (double)TimeSpan.FromMilliseconds(FadeDurationMilliseconds).Ticks;
                factor = Math.Min(factor, SmoothStep(naturalFactor));
            }
            if (manualRemaining > 0 && manualTotal > 0)
            {
                var manualFactor = (manualRemaining - frame) / (double)manualTotal;
                factor = Math.Min(factor, SmoothStep(manualFactor));
            }

            var sampleOffset = offset + frame * channels;
            for (var channel = 0; channel < channels; channel++)
            {
                buffer[sampleOffset + channel] *= (float)factor;
            }
        }

        _framesRead += frames;
        if (manualRemaining > 0)
        {
            Interlocked.Exchange(
                ref _manualFadeRemainingFrames,
                Math.Max(0L, manualRemaining - frames));
        }
        return read;
    }

    private long FadeFrameCount() => checked(
        (long)WaveFormat.SampleRate * FadeDurationMilliseconds / 1000L);

    private static double SmoothStep(double value)
    {
        var clamped = Math.Clamp(value, 0d, 1d);
        return clamped * clamped * (3d - 2d * clamped);
    }

    private static TimeSpan SafeTime(TimeSpan value) =>
        value < TimeSpan.Zero ? TimeSpan.Zero : value;
}

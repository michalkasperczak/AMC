using AccessibleMediaController.Core.Playback;
using AccessibleMediaController.Core.Sessions;

internal static class PlaybackRateStateTests
{
    public static void Run()
    {
        var output = new RateOutput { Reject = true };
        var session = new DemoMediaSession("radio", "Próba bufora", Array.Empty<MediaItem>(), output);
        var errors = new List<string>();
        if (session.SetPlaybackRate(1.5d)) errors.Add("Sesja potwierdza tempo odrzucone przez wyjście.");
        if (session.PlaybackRate != 1d) errors.Add("Sesja pokazuje 1,5 mimo rzeczywistego tempa 1.");
        if (output.Calls != 1) errors.Add("Próba nie wywołała badanego wyjścia dokładnie raz.");

        output.Reject = false;
        if (!session.SetPlaybackRate(1.5d) || session.PlaybackRate != 1.5d)
            errors.Add("Przyjęte tempo nie jest widoczne w sesji.");
        output.Actual = 1d; // Engine caught up with live audio, without a UI command.
        if (session.PlaybackRate != 1d) errors.Add("Po dogonieniu transmisji sesja nadal pokazuje dawne tempo.");
        if (!session.ChangePlaybackRate(1) || output.Actual != 1.25d)
            errors.Add("Następna zmiana tempa nie zaczyna się od rzeczywistej wartości.");
        output.Supported = false;
        var calls = output.Calls;
        if (session.SetPlaybackRate(2d) || output.Calls != calls)
            errors.Add("Brak wsparcia nie zatrzymał żądania zmiany tempa.");

        var legacy = new LegacyOutput();
        var legacySession = new DemoMediaSession("local", "Próba starego wyjścia", Array.Empty<MediaItem>(), legacy);
        if (!legacySession.SetPlaybackRate(1.5d) || legacySession.PlaybackRate != 1.5d || legacy.LastRequested != 1.5d)
            errors.Add("Wyjście bez opcjonalnego odczytu stanu zmieniło zachowanie.");
        if (!legacySession.ChangePlaybackRate(-1) || legacySession.PlaybackRate != 1.25d)
            errors.Add("Stare wyjście utraciło dotychczasowe kroki tempa.");
        if (errors.Count != 0) throw new Exception(string.Join(" | ", errors));
        Console.WriteLine("OK: sesja podaje faktyczne tempo wyjścia, nie potwierdza odmowy i zachowuje starsze adaptery");
    }

    private class LegacyOutput : IMediaOutput
    {
        public string? LoadedItemId => null;
        public TimeSpan Position => TimeSpan.Zero;
        public bool Supported { get; set; } = true;
        public bool SupportsPlaybackRate => Supported;
        public double LastRequested { get; private set; } = 1d;
        public void Play(MediaItem item, TimeSpan position, int volume, double playbackRate) { }
        public void Pause() { }
        public void Stop() { }
        public void Seek(TimeSpan position) { }
        public void SetVolume(int volume) { }
        public virtual void SetPlaybackRate(double playbackRate) => LastRequested = playbackRate;
    }

    private sealed class RateOutput : LegacyOutput, IPlaybackRateStateOutput
    {
        public bool Reject { get; set; }
        public double Actual { get; set; } = 1d;
        public int Calls { get; private set; }
        public double PlaybackRate => Actual;
        public override void SetPlaybackRate(double playbackRate)
        {
            Calls++;
            if (!Reject) Actual = playbackRate;
        }
    }
}

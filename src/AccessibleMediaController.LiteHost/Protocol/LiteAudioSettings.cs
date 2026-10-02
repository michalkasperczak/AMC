using System.Text.Json;
using AccessibleMediaController.Core.Configuration;
namespace AccessibleMediaController.LiteHost.Protocol;

public static class LiteAudioSettings
{
    public static PlaybackAudioSettings Read(JsonElement args)
    {
        var settings = new PlaybackAudioSettings
        {
            LoudnessNormalizationEnabled = LiteArgs.ReadBool(args, "loudnessNormalization", false),
            SmoothTrackTransitionsEnabled = LiteArgs.ReadBool(args, "smoothTrackTransitions", false),
            InterTrackSilenceMilliseconds = LiteArgs.ReadInt(args, "interTrackSilenceMs", 0, 0, 5_000)
        };
        if (!PlaybackAudioSettingsRules.IsSupportedSilence(settings.InterTrackSilenceMilliseconds))
            throw new LiteRequestException("Nieobsługiwana długość ciszy między utworami.");
        if (args.ValueKind == JsonValueKind.Object && args.TryGetProperty("tempoAlgorithm", out var choice))
        {
            if (choice.ValueKind != JsonValueKind.Number || !choice.TryGetInt32(out var value)
                || !Enum.IsDefined(typeof(PlaybackTempoAlgorithm), value))
                throw new LiteRequestException("Nieznany algorytm tempa. Wybierz Mowę, Muzykę lub SoundTouch.");
            settings.TempoAlgorithm = (PlaybackTempoAlgorithm)value;
        }
        return settings;
    }
}

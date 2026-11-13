using System.Text.Json;
using AccessibleMediaController.LiteHost.Protocol;
using AccessibleMediaController.Windows.Services;

namespace AccessibleMediaController.LiteHost;

/// <summary>
/// Control only the official Apple Music Windows session. This cannot select a
/// catalog song; selection is a separate capability and must be verified.
/// </summary>
internal sealed class LiteAppleMusicExternalCoordinator
{
    private readonly ExternalMediaController external = new(
        "AppleInc.AppleMusicWin_", "Apple Music",
        message => Console.Error.WriteLine("[lite-host] " + message));

    public object Transport(JsonElement args) =>
        LiteAppleMusicExternalContract.ReadTransportCommand(args) switch
        {
            "state" => State(),
            "toggle" => Toggle(),
            "next" => Skip(forward: true),
            "previous" => Skip(forward: false),
            "stop" => Stop(),
            _ => throw new LiteRequestException(
                "Nieznane polecenie sterowania oryginalnym Apple Music.")
        };

    private object State()
    {
        var state = external.GetStateAsync().GetAwaiter().GetResult();
        return new
        {
            hasSession = state.HasSession,
            title = state.Title,
            artist = state.Artist,
            durationSeconds = state.Duration > TimeSpan.Zero
                ? state.Duration.TotalSeconds : (double?)null,
            positionSeconds = state.HasPosition
                ? state.Position.TotalSeconds : (double?)null,
            isPlaying = state.IsPlaying
        };
    }

    private object Toggle()
    {
        var before = external.GetStateAsync().GetAwaiter().GetResult();
        if (!before.HasSession)
            return Unhandled("Oryginalny Apple Music nie odpowiada");
        if (!external.TogglePlayPauseAsync().GetAwaiter().GetResult())
            return Unhandled("Oryginalny Apple Music nie przyjął polecenia gra lub pauza");
        ExternalPlaybackState after = before;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            Thread.Sleep(TimeSpan.FromMilliseconds(150));
            after = external.GetStateAsync().GetAwaiter().GetResult();
            if (after.HasSession && after.IsPlaying != before.IsPlaying)
                break;
        }
        if (!after.HasSession || after.IsPlaying == before.IsPlaying)
            return Unhandled("Apple Music nie potwierdziło zmiany odtwarzania");
        return new
        {
            handled = true,
            isPlaying = after.IsPlaying,
            message = after.IsPlaying ? "Odtwarzanie" : "Wstrzymano"
        };
    }

    private object Skip(bool forward)
    {
        if (!external.IsAvailableAsync().GetAwaiter().GetResult())
            return Unhandled("Oryginalny Apple Music nie odpowiada");
        var done = (forward ? external.NextAsync() : external.PreviousAsync())
            .GetAwaiter().GetResult();
        if (!done)
            return Unhandled(forward
                ? "Oryginalny Apple Music nie ma następnego utworu"
                : "Oryginalny Apple Music nie ma poprzedniego utworu");
        Thread.Sleep(TimeSpan.FromMilliseconds(500));
        var state = external.GetStateAsync().GetAwaiter().GetResult();
        var title = state.Title.Length > 0 ? state.Title
            : (forward ? "Następny utwór" : "Poprzedni utwór");
        return new { handled = true, isPlaying = state.IsPlaying, message = title };
    }

    private object Stop()
    {
        if (!external.IsAvailableAsync().GetAwaiter().GetResult())
            return Unhandled("Oryginalny Apple Music nie odpowiada");
        if (!external.StopAsync().GetAwaiter().GetResult())
            return Unhandled("Oryginalny Apple Music nie przyjął polecenia Stop");
        return new { handled = true, isPlaying = false, message = "Zatrzymano" };
    }

    private static object Unhandled(string message) => new
    {
        handled = false,
        message
    };
}

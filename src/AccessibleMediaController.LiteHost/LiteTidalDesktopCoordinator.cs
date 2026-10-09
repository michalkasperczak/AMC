using System.Text.Json;
using AccessibleMediaController.LiteHost.Protocol;
using AccessibleMediaController.Windows.Services;

namespace AccessibleMediaController.LiteHost;

/// <summary>
/// Laczy bezokienny host wx z tym samym kontrolerem oryginalnego TIDALa,
/// ktorego uzywa pelne AMC. Dzwiek nadal powstaje w TIDALu, nie w hoście.
/// </summary>
internal sealed class LiteTidalDesktopCoordinator : IDisposable
{
    private readonly object gate = new();
    private readonly TidalDesktopController desktop = new(
        message => Console.Error.WriteLine("[lite-host] " + message));
    private readonly ExternalMediaController external = new(
        message => Console.Error.WriteLine("[lite-host] " + message));
    private CancellationTokenSource? pendingPlay;

    public object Play(JsonElement args)
    {
        var command = LiteTidalDesktopContract.ReadPlayRequest(args);
        CancellationTokenSource cancellation;
        lock (gate)
        {
            pendingPlay?.Cancel();
            pendingPlay?.Dispose();
            cancellation = new CancellationTokenSource();
            pendingPlay = cancellation;
        }

        var result = desktop.PlayAsync(
                command.Request,
                command.RestartConsent,
                cancellation.Token)
            .GetAwaiter()
            .GetResult();
        return new
        {
            success = result.Success,
            wasAlreadyCurrent = result.WasAlreadyCurrent,
            needsRestartConsent = result.NeedsRestartConsent,
            message = result.Message
        };
    }

    public object Transport(JsonElement args)
    {
        var command = LiteTidalDesktopContract.ReadTransportCommand(args);
        return command switch
        {
            "toggle" => Toggle(),
            "next" => Skip(forward: true),
            "previous" => Skip(forward: false),
            _ => throw new LiteRequestException(
                "Nieznane polecenie sterowania oryginalnym TIDALem.")
        };
    }

    private object Toggle()
    {
        var before = external.GetStateAsync().GetAwaiter().GetResult();
        if (!before.HasSession)
            return Unhandled("Oryginalny TIDAL nie odpowiada");
        if (!external.TogglePlayPauseAsync().GetAwaiter().GetResult())
            return Unhandled("Oryginalny TIDAL nie przyjął polecenia gra lub pauza");

        var isPlaying = !before.IsPlaying;
        return new
        {
            handled = true,
            isPlaying,
            message = isPlaying ? "Odtwarzanie" : "Wstrzymano"
        };
    }

    private object Skip(bool forward)
    {
        if (!external.IsAvailableAsync().GetAwaiter().GetResult())
            return Unhandled("Oryginalny TIDAL nie odpowiada");
        var done = (forward ? external.NextAsync() : external.PreviousAsync())
            .GetAwaiter()
            .GetResult();
        if (!done)
        {
            return Unhandled(forward
                ? "Oryginalny TIDAL nie ma następnego utworu"
                : "Oryginalny TIDAL nie ma poprzedniego utworu");
        }

        // Jak w pelnym AMC: sesja systemowa potrzebuje chwili na nowy tytul.
        Thread.Sleep(TimeSpan.FromMilliseconds(700));
        var state = external.GetStateAsync().GetAwaiter().GetResult();
        var description = state.HasSession && state.Title.Length > 0
            ? (state.Artist.Length > 0
                ? $"{state.Title}, {state.Artist}"
                : state.Title)
            : (forward ? "Następny utwór" : "Poprzedni utwór");
        return new { handled = true, isPlaying = state.IsPlaying, message = description };
    }

    private static object Unhandled(string message) => new
    {
        handled = false,
        message
    };

    public void Dispose()
    {
        lock (gate)
        {
            pendingPlay?.Cancel();
            pendingPlay?.Dispose();
            pendingPlay = null;
        }
    }
}

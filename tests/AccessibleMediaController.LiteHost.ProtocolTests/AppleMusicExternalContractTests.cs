using System.Text.Json;
using AccessibleMediaController.LiteHost.Protocol;

namespace AccessibleMediaController.LiteHost.ProtocolTests;

internal static class AppleMusicExternalContractTests
{
    public static void Run()
    {
        if (!LiteAppleMusicExternalContract.IsEnabledForProfile("test")
            || LiteAppleMusicExternalContract.IsEnabledForProfile("public")
            || LiteAppleMusicExternalContract.IsEnabledForProfile(null))
            throw new InvalidOperationException("Apple Music nie jest ograniczone do AMC Test.");
        foreach (var command in new[] { "state", "toggle", "next", "previous", "stop" })
        {
            using var document = JsonDocument.Parse($$"""{"command":"{{command}}"}""");
            if (LiteAppleMusicExternalContract.ReadTransportCommand(document.RootElement)
                != command)
                throw new InvalidOperationException("Odrzucono znane polecenie Apple Music.");
        }

        foreach (var command in new[] { "seek", "playSelected", "launch", "" })
        {
            using var document = JsonDocument.Parse($$"""{"command":"{{command}}"}""");
            try
            {
                LiteAppleMusicExternalContract.ReadTransportCommand(document.RootElement);
                throw new InvalidOperationException("Przyjęto niedostępne polecenie Apple Music.");
            }
            catch (LiteRequestException)
            {
                // The host never accepts arbitrary song selection over SMTC.
            }
        }
    }
}

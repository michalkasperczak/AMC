namespace AccessibleMediaController.Core.Sonos;

/// <summary>
/// Publiczna konfiguracja integracji Sonos współdzielona przez główne AMC i
/// bezokienny host wxPython. Klucz Control API identyfikuje aplikację i nie
/// jest sekretem; tokeny użytkownika nadal pozostają wyłącznie w magazynie
/// DPAPI bieżącego użytkownika Windows.
/// </summary>
public static class SonosIntegrationDefaults
{
    public const string BrokerOrigin = "https://hermes.tail6caad7.ts.net/";

    public const string ControlApiKey = "b051a8f0-499c-4deb-9f66-843a752c36e4";
}

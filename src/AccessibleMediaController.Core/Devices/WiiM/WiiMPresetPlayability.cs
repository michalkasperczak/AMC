namespace AccessibleMediaController.Core.Devices.WiiM;

/// <summary>
/// Rozpoznaje presety, ktorych urzadzenie WiiM NIE POTRAFI uruchomic samo.
///
/// ZGLOSZENIE Michala (17.09.2026) i POMIAR na jego urzadzeniach (16.09.2026,
/// WiiM Pro Plus-8B96 i WiiM Sound-CED0): preset zapisany z TIDAL-a nie ma
/// adresu strumienia. Urzadzenie na pytanie o zrodlo odpowiada doslownie
/// "unknown", na komende aktywacji odpowiada "OK", przelacza sie na wejscie
/// TIDAL Connect - i zostaje zatrzymane z pustym tytulem. Nic nie gra.
///
/// Powod lezy w firmware glosnika, nie w AMC. TIDAL Connect dziala tak, ze
/// dzwiek WYPYCHA do glosnika aplikacja TIDAL; glosnik jest samym odbiornikiem
/// i nie wie, co mialby zagrac z wlasnej woli. Wszystkie pytania o TIDAL, jakie
/// da sie zadac urzadzeniu, zwracaja "nieznane polecenie".
///
/// Bez tego rozpoznania AMC wysylalo komende, dostawalo "OK" i milczalo, a nic
/// nie gralo - cisza nieodrozznialna od sukcesu. Dlatego mowimy wprost, czego
/// urzadzenie nie potrafi, zamiast zostawiac uzytkownika w ciszy.
/// </summary>
public static class WiiMPresetPlayability
{
    /// <summary>
    /// Nazwy zrodel, ktore urzadzenie odtwarza WYLACZNIE jako odbiornik -
    /// muzyke musi wypchnac osobna aplikacja na telefonie lub komputerze.
    /// </summary>
    private static readonly string[] ReceiverOnlySources =
    [
        "tidal",
        "tidalconnect",
        "spotify",
        "spotifyconnect",
        "airplay",
        "bluetooth",
    ];

    /// <summary>
    /// Zwraca powod, dla ktorego presetu nie da sie uruchomic z AMC, albo
    /// <c>null</c>, gdy preset jest zwyklym strumieniem i zagra normalnie.
    /// Zdanie jest gotowe do wypowiedzenia przez czytnik ekranu.
    /// </summary>
    public static string? DescribeUnplayableReason(WiiMPresetInformation? preset)
    {
        if (preset is null) return null;

        // Preset ze zwyklym adresem zagra - nawet gdy zrodlo nazywa sie dziwnie.
        // Adres jest tu mocniejszym dowodem niz nazwa zrodla, wiec sprawdzamy go
        // PIERWSZY: inaczej stacja z "tidal" w adresie zostalaby zablokowana bez
        // powodu.
        if (!string.IsNullOrWhiteSpace(preset.Uri)) return null;

        var source = NormalizeSource(preset.Source);
        if (source.Length == 0) return null;

        if (!ReceiverOnlySources.Contains(source)) return null;

        var label = FriendlySourceName(source);
        return $"Preset {preset.Number} to {label}. "
            + "Urządzenie samo tego nie uruchomi, bo odbiera dźwięk z aplikacji. "
            + $"Włącz odtwarzanie w aplikacji {label} i wskaż tam to urządzenie";
    }

    /// <summary>
    /// Czy preset da sie uruchomic komenda z AMC.
    /// </summary>
    public static bool CanActivate(WiiMPresetInformation? preset) =>
        DescribeUnplayableReason(preset) is null;

    private static string NormalizeSource(string? source)
    {
        if (string.IsNullOrWhiteSpace(source)) return string.Empty;
        var trimmed = source.Trim().ToLowerInvariant();
        // Urzadzenie zwraca m.in. "TIDAL Connect", "tidal_connect", "Tidal-Connect".
        var cleaned = new string(trimmed.Where(char.IsLetterOrDigit).ToArray());
        // "unknown" znaczy "urzadzenie nie podalo zrodla" - to NIE jest dowod, ze
        // preset nie zagra, wiec nie blokujemy go na tej podstawie.
        return cleaned is "unknown" or "none" ? string.Empty : cleaned;
    }

    private static string FriendlySourceName(string normalizedSource) => normalizedSource switch
    {
        "tidal" or "tidalconnect" => "TIDAL Connect",
        "spotify" or "spotifyconnect" => "Spotify Connect",
        "airplay" => "AirPlay",
        "bluetooth" => "Bluetooth",
        _ => normalizedSource,
    };
}

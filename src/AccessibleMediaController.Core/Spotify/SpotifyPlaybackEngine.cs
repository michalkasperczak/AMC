using System.Text.Json;
using System.Text.Json.Serialization;

namespace AccessibleMediaController.Core.Spotify;

/// <summary>
/// Silnik odtwarzania JEDNEJ sesji Spotify. Po scaleniu dwoch sesji w jedna
/// uzytkownik nie wybiera juz sesji, tylko silnik - a wybor musi przezyc
/// restart, bo inaczej kazde uruchomienie cofaloby go do domyslnego.
/// </summary>
public enum SpotifyPlaybackEngine
{
    /// <summary>Wbudowany host Librespot. Domyslny.</summary>
    Librespot,

    /// <summary>Web Playback SDK w WebView2.</summary>
    Sdk
}

/// <summary>
/// Reguly czytania zapisanej nazwy silnika. Plik ustawien moze pochodzic z
/// nowszego wydania, byc recznie edytowany albo uszkodzony - wtedy MUSI zadzialac
/// bezpieczny powrot do <see cref="SpotifyPlaybackEngine.Librespot"/>, a nie
/// wyjatek przy starcie. Brak pola (stary plik) to rowniez Librespot.
/// </summary>
public static class SpotifyPlaybackEngineRules
{
    public const SpotifyPlaybackEngine Default = SpotifyPlaybackEngine.Librespot;

    public static SpotifyPlaybackEngine Normalize(SpotifyPlaybackEngine engine) =>
        Enum.IsDefined(engine) ? engine : Default;

    public static SpotifyPlaybackEngine Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return Default;
        return Enum.TryParse<SpotifyPlaybackEngine>(value.Trim(), ignoreCase: true, out var parsed)
               && Enum.IsDefined(parsed)
            ? parsed
            : Default;
    }

    /// <summary>Etykieta dla czytnika ekranu - nigdy nazwa elementu enum.</summary>
    public static string Describe(SpotifyPlaybackEngine engine) => Normalize(engine) switch
    {
        SpotifyPlaybackEngine.Sdk => "Spotify Web Playback SDK",
        _ => "Librespot (wbudowany)"
    };
}

/// <summary>
/// Czyta silnik z pliku ustawien bez rzucania wyjatku na nieznanej wartosci.
/// Domyslny <see cref="JsonStringEnumConverter"/> przerwalby odczyt CALEGO
/// stanu, czyli jedna literowka w pliku kasowalaby uzytkownikowi konfiguracje.
/// </summary>
public sealed class SpotifyPlaybackEngineJsonConverter : JsonConverter<SpotifyPlaybackEngine>
{
    public override SpotifyPlaybackEngine Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) => reader.TokenType switch
        {
            JsonTokenType.String => SpotifyPlaybackEngineRules.Parse(reader.GetString()),
            JsonTokenType.Null => SpotifyPlaybackEngineRules.Default,
            JsonTokenType.Number => reader.TryGetInt32(out var number)
                && Enum.IsDefined((SpotifyPlaybackEngine)number)
                    ? (SpotifyPlaybackEngine)number
                    : SpotifyPlaybackEngineRules.Default,
            _ => SpotifyPlaybackEngineRules.Default
        };

    public override void Write(
        Utf8JsonWriter writer,
        SpotifyPlaybackEngine value,
        JsonSerializerOptions options) =>
        writer.WriteStringValue(SpotifyPlaybackEngineRules.Normalize(value).ToString());
}

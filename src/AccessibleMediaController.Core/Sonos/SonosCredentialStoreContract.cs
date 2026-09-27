using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AccessibleMediaController.Core.Sonos;

/// <summary>
/// TRWALY magazyn poswiadczen Sonos - wylacznie KONTRAKT i model. Tu nie ma
/// zadnego wejscia/wyjscia, zadnego szyfrowania i zadnego koordynatora sesji:
/// implementacja Windows (DPAPI biezacego uzytkownika) jest osobnym plikiem,
/// a wlascicielem operacji bedzie przyszly koordynator generacji.
///
/// Granice, ktorych tu nie wolno przekroczyc:
///   * access/refresh/scope sa NIEPRZEZROCZYSTE: zero trim, zero normalizacji,
///     zero wzorca - zapisujemy i odczytujemy dokladnie te znaki,
///   * rekord wiaze poswiadczenia ze SKONFIGUROWANYM origin brokera; odczyt z
///     inna konfiguracja jest ODRZUCANY i NIGDY nie przelacza adresu,
///   * brak expires_in zostaje stanem NIEZNANYM - nie wymyslamy TTL,
///   * brak refresh tokenu po pierwszym logowaniu to STAN DO ZAPISANIA, nie
///     powod, by wyprodukowac falszywy RT,
///   * zaden komunikat i zaden ToString nie wypisuje tokenu, scope ani origin,
///   * nierozpoznana wersja formatu i nieprzewidziane pola to Invalid, nigdy
///     cichy sukces.
/// </summary>
public static class SonosCredentialPolicy
{
    /// <summary>
    /// NASZ limit rozmiaru ZASZYFROWANEGO pliku. Sprawdzany PRZED alokacja
    /// bufora, zeby uszkodzony albo podmieniony plik nie wymusil duzej
    /// alokacji. To nasza granica, nie deklaracja rozmiarow DPAPI.
    /// </summary>
    public const int MaxEncryptedFileBytes = 512 * 1024;

    /// <summary>
    /// Limit JEDNEJ nieprzezroczystej wartosci (access token, scope). NIE jest
    /// osobna, wymyslona liczba: to limit CALEJ odpowiedzi brokera, ktory nasz
    /// klient JUZ przyjal (<see cref="SonosLoginClient.MaxResponseBytes"/>).
    /// Wezsze capy 8 KiB / 4 KiB odrzucalyby tokeny, ktore klient zwrocil jako
    /// Success - magazyn nie moze byc bardziej wybredny od warstwy odbioru.
    /// Pojedyncze pole i tak nie zmiesci sie w wiekszej liczbie bajtow niz cala
    /// odpowiedz, w ktorej przyszlo.
    /// </summary>
    public static readonly int MaxOpaqueValueBytes = SonosLoginClient.MaxResponseBytes;

    /// <summary>
    /// Najgorsze rozdmuchanie jednego bajtu przez NASZ enkoder zapisu. Uzywamy
    /// enkodera MNIEJ escapujacego (UnsafeRelaxedJsonEscaping), bo JSON idzie
    /// pod DPAPI na wlasny dysk, nie do HTML ani do przegladarki: escapuje tylko
    /// cudzyslow i odwrotny ukosnik, po 2 B, a znaki sterujace i tak odrzuca
    /// polityka. Domyslny enkoder zamienialby np. "&lt;" na 6 B, wiec sam
    /// podniesiony cap pola nie wystarczyl.
    /// </summary>
    private const int WorstCaseEscapeExpansion = 2;

    /// <summary>Zapas na klucze JSON-a, wersje formatu, znacznik czasu i separatory.</summary>
    private const int EnvelopeHeadroomBytes = 4 * 1024;

    /// <summary>
    /// NASZ limit zdeszyfrowanego JSON-a, WYLICZONY z limitow pol, a nie dobrany
    /// na oko: dwie nieprzezroczyste wartosci + refresh token, kazda w
    /// najgorszym escapowaniu, plus koperta. Nadal skonczony.
    /// </summary>
    public static readonly int MaxPlaintextBytes =
        EnvelopeHeadroomBytes
        + (WorstCaseEscapeExpansion
            * ((2 * MaxOpaqueValueBytes) + SonosRefreshTokenPolicy.MaxDecodedBytes));

    /// <summary>
    /// Czy NIEPRZEZROCZYSTA wartosc da sie u nas trwale zapisac: niepusta,
    /// kodowalna w SCISLYM UTF-8 (odrzucone niesparowane surogaty), bez znakow
    /// sterujacych C0/DEL/C1 i w naszym limicie bajtow. Bez trim i bez
    /// normalizacji - spacje i znaki poza ASCII zostaja nietkniete.
    /// </summary>
    public static bool IsStorableOpaqueValue(string? value, int maxBytes)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        foreach (var character in value)
        {
            if (character < 0x20 || character == 0x7F || (character >= 0x80 && character <= 0x9F))
            {
                return false;
            }
        }

        try
        {
            return new UTF8Encoding(false, throwOnInvalidBytes: true).GetByteCount(value) <= maxBytes;
        }
        catch (EncoderFallbackException)
        {
            return false;
        }
    }

    /// <summary>
    /// Scope wymaga OSOBNEJ reguly: broker zwraca rowniez scope PUSTY, a klient
    /// przyjmuje to jako Success. Pusty scope jest JAWNIE pusty - zapisujemy go
    /// jako "" i oddajemy jako "", nigdy jako null i nigdy przez trim.
    /// </summary>
    public static bool IsStorableScopeValue(string? scope) =>
        scope is not null
        && (scope.Length == 0 || IsStorableOpaqueValue(scope, MaxOpaqueValueBytes));
}

/// <summary>Rozpoznane, rozdzielne wyniki ODCZYTU trwalego magazynu.</summary>
public enum SonosCredentialReadStatus
{
    /// <summary>Odczytano i zwalidowano rekord przypisany do TEJ konfiguracji.</summary>
    Success,
    /// <summary>Nie ma jeszcze zapisanych poswiadczen. To nie blad.</summary>
    Missing,
    /// <summary>
    /// Plik istnieje, ale nie jest naszym poprawnym rekordem: nieudane
    /// odszyfrowanie, zle UTF-8, zly JSON, nieobslugiwana wersja formatu,
    /// nieprzewidziane pola, wartosci poza polityka. Odczyt NIE kasuje pliku.
    /// </summary>
    Invalid,
    /// <summary>
    /// Rekord jest poprawny, ale nalezy do INNEGO skonfigurowanego brokera.
    /// Odrzucamy go i NIE przelaczamy adresu na zapisany w pliku.
    /// </summary>
    BrokerMismatch,
    /// <summary>Nie udalo sie przeczytac pliku (blokada, uprawnienia, I/O).</summary>
    ReadFailure
}

/// <summary>Rozpoznane, rozdzielne wyniki ZAPISU trwalego magazynu.</summary>
public enum SonosCredentialWriteStatus
{
    Success,
    /// <summary>Nasze WEJSCIE nie przeszlo polityki - nic nie zostalo zapisane.</summary>
    InvalidRecord,
    /// <summary>Zapis sie nie udal. Poprzedni plik pozostaje nietkniety.</summary>
    WriteFailure
}

/// <summary>Stale, bezpieczne komunikaty. Nigdy nie cytuja tokenu, scope, origin ani sciezki.</summary>
public static class SonosCredentialMessages
{
    private static readonly IReadOnlyDictionary<SonosCredentialReadStatus, string> ReadTexts =
        new Dictionary<SonosCredentialReadStatus, string>
        {
            [SonosCredentialReadStatus.Success] = "Zapisane logowanie Sonos zostało odczytane.",
            [SonosCredentialReadStatus.Missing] = "Nie ma zapisanego logowania Sonos.",
            [SonosCredentialReadStatus.Invalid] =
                "Zapisane logowanie Sonos jest nieczytelne lub w nieobsługiwanym formacie. Zaloguj się ponownie.",
            [SonosCredentialReadStatus.BrokerMismatch] =
                "Zapisane logowanie Sonos należy do innego serwera logowania. Zaloguj się ponownie.",
            [SonosCredentialReadStatus.ReadFailure] =
                "Nie udało się odczytać zapisanego logowania Sonos."
        };

    private static readonly IReadOnlyDictionary<SonosCredentialWriteStatus, string> WriteTexts =
        new Dictionary<SonosCredentialWriteStatus, string>
        {
            [SonosCredentialWriteStatus.Success] = "Logowanie Sonos zostało zapisane.",
            [SonosCredentialWriteStatus.InvalidRecord] =
                "Logowanie Sonos nie spełnia wymagań zapisu; nic nie zostało zapisane.",
            [SonosCredentialWriteStatus.WriteFailure] =
                "Nie udało się zapisać logowania Sonos. Poprzedni zapis pozostał bez zmian."
        };

    public static string Describe(SonosCredentialReadStatus status) =>
        ReadTexts.TryGetValue(status, out var text) ? text : ReadTexts[SonosCredentialReadStatus.Invalid];

    public static string Describe(SonosCredentialWriteStatus status) =>
        WriteTexts.TryGetValue(status, out var text) ? text : WriteTexts[SonosCredentialWriteStatus.WriteFailure];
}

/// <summary>
/// TRWALY rekord poswiadczen Sonos: caly zestaw tokenow, moment ich otrzymania
/// w UTC i origin brokera, ktory je wydal. Termin waznosci jest WYLICZANY z
/// expires_in; jego brak zostaje stanem nieznanym.
/// </summary>
public sealed class SonosStoredCredentials
{
    /// <summary>JAWNA wersja formatu. Kazda inna wartosc w pliku to Invalid.</summary>
    public const int CurrentFormatVersion = 1;

    public SonosStoredCredentials(string brokerOrigin, SonosTokens tokens, DateTimeOffset receivedAtUtc)
    {
        ArgumentException.ThrowIfNullOrEmpty(brokerOrigin);
        ArgumentNullException.ThrowIfNull(tokens);
        BrokerOrigin = brokerOrigin;
        Tokens = tokens;
        ReceivedAtUtc = receivedAtUtc.ToUniversalTime();
    }

    public int FormatVersion => CurrentFormatVersion;

    /// <summary>
    /// SKONFIGUROWANY origin brokera, ktory wydal te tokeny (postac znormalizowana
    /// przez <see cref="SonosLoginBrokerConfiguration"/>). Sluzy WYLACZNIE do
    /// porownania z biezaca konfiguracja - nigdy do wyboru adresu.
    /// </summary>
    public string BrokerOrigin { get; }

    public SonosTokens Tokens { get; }

    /// <summary>Moment otrzymania tokenow, zawsze w UTC.</summary>
    public DateTimeOffset ReceivedAtUtc { get; }

    /// <summary>Czy z zapisu da sie w ogole wyliczyc termin waznosci.</summary>
    public bool IsExpiryKnown => Tokens.ExpiresInSeconds.HasValue;

    /// <summary>
    /// Wyliczony termin waznosci albo null, gdy broker nie podal expires_in.
    /// NIE podstawiamy tu zadnego domyslnego czasu zycia.
    /// </summary>
    public DateTimeOffset? ExpiresAtUtc =>
        Tokens.ExpiresInSeconds.HasValue
            ? ReceivedAtUtc.AddSeconds(Tokens.ExpiresInSeconds.Value)
            : null;

    public bool HasRefreshToken => Tokens.HasRefreshToken;

    /// <summary>Opis BEZ tokenow, BEZ scope i BEZ origin - jedyna dozwolona reprezentacja.</summary>
    public override string ToString() =>
        "Zapisane logowanie Sonos (wartości i serwer ukryte): format "
        + FormatVersion.ToString(CultureInfo.InvariantCulture)
        + ", token odświeżania "
        + (HasRefreshToken ? "obecny" : "brak")
        + ", ważność "
        + (IsExpiryKnown ? "wyliczalna" : "nieznana")
        + ".";
}

/// <summary>Wynik JEDNEGO odczytu magazynu.</summary>
public sealed class SonosCredentialReadOutcome
{
    private SonosCredentialReadOutcome(SonosCredentialReadStatus status, SonosStoredCredentials? credentials)
    {
        Status = status;
        Credentials = credentials;
    }

    public SonosCredentialReadStatus Status { get; }

    public SonosStoredCredentials? Credentials { get; }

    public bool Succeeded => Status == SonosCredentialReadStatus.Success && Credentials is not null;

    public string Message => SonosCredentialMessages.Describe(Status);

    public static SonosCredentialReadOutcome Ok(SonosStoredCredentials credentials)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        return new SonosCredentialReadOutcome(SonosCredentialReadStatus.Success, credentials);
    }

    public static SonosCredentialReadOutcome Failure(SonosCredentialReadStatus status) =>
        new(status == SonosCredentialReadStatus.Success ? SonosCredentialReadStatus.Invalid : status, null);

    public override string ToString() => "Odczyt logowania Sonos: " + Status + ", " + Message;
}

/// <summary>Wynik JEDNEGO zapisu magazynu.</summary>
public sealed class SonosCredentialWriteOutcome
{
    private SonosCredentialWriteOutcome(SonosCredentialWriteStatus status)
    {
        Status = status;
    }

    public SonosCredentialWriteStatus Status { get; }

    public bool Succeeded => Status == SonosCredentialWriteStatus.Success;

    public string Message => SonosCredentialMessages.Describe(Status);

    public static SonosCredentialWriteOutcome Ok() => new(SonosCredentialWriteStatus.Success);

    public static SonosCredentialWriteOutcome Failure(SonosCredentialWriteStatus status) =>
        new(status == SonosCredentialWriteStatus.Success ? SonosCredentialWriteStatus.WriteFailure : status);

    public override string ToString() => "Zapis logowania Sonos: " + Status + ", " + Message;
}

/// <summary>
/// Kontrakt dla PRZYSZLEGO koordynatora: jedna instancja jest wlascicielem
/// magazynu. Brak tu blokad wieloprocesowych i brak ogolnego frameworka plikow -
/// to swiadoma granica tego etapu.
/// </summary>
public interface ISonosCredentialStore
{
    /// <summary>Odczyt rekordu przypisanego do BIEZACEJ konfiguracji brokera.</summary>
    SonosCredentialReadOutcome Read();

    /// <summary>Zapis calego zestawu tokenow (atomowa podmiana, bez plaintextu na dysku).</summary>
    SonosCredentialWriteOutcome Write(SonosStoredCredentials credentials);

    /// <summary>JAWNE usuniecie. Idempotentne: brak zapisu to sukces.</summary>
    bool Delete();
}

/// <summary>
/// Serializacja rekordu do JSON-a o JAWNEJ wersji formatu i jej scisla
/// walidacja. Osobno od szyfrowania i od I/O, zeby dala sie sprawdzic bez
/// Windows. Deserializacja NIGDY nie przepuszcza nieznanej wersji ani
/// nieprzewidzianych pol i NIGDY nie wyrzuca tresci pliku w wyjatku.
/// </summary>
public static class SonosCredentialSerializer
{
    private const string ReceivedAtFormat = "O";

    private static readonly UTF8Encoding StrictUtf8 = new(false, throwOnInvalidBytes: true);

    private static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,

        // JSON trafia pod DPAPI do WLASNEGO pliku, nie do HTML ani do
        // przegladarki, wiec escapowanie HTML-owe jest zbedne i tylko
        // rozdmuchuje budzet ("<" -> 6 B). Relaxed escapuje to, co JSON
        // wymaga; znaki sterujace odrzuca wczesniej polityka.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// Zwraca bajty JSON-a albo null, gdy rekord nie przechodzi polityki. Bez
    /// wyjatku z trescia - wolajacy zamienia null na InvalidRecord.
    /// </summary>
    public static byte[]? TrySerialize(SonosStoredCredentials? credentials)
    {
        if (credentials is null || !IsStorable(credentials))
        {
            return null;
        }

        var payload = new StoredPayload
        {
            FormatVersion = SonosStoredCredentials.CurrentFormatVersion,
            BrokerOrigin = credentials.BrokerOrigin,
            ReceivedAtUtc = credentials.ReceivedAtUtc.ToUniversalTime()
                .ToString(ReceivedAtFormat, CultureInfo.InvariantCulture),
            AccessToken = credentials.Tokens.AccessToken,
            TokenType = credentials.Tokens.TokenType,
            ExpiresInSeconds = credentials.Tokens.ExpiresInSeconds,
            RefreshToken = credentials.Tokens.RefreshToken,
            Scope = credentials.Tokens.Scope
        };

        try
        {
            var bytes = StrictUtf8.GetBytes(JsonSerializer.Serialize(payload, Options));
            return bytes.Length <= SonosCredentialPolicy.MaxPlaintextBytes ? bytes : null;
        }
        catch (EncoderFallbackException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>
    /// Czy rekord wolno utrwalic. RT, jesli obecny, musi przejsc ISTNIEJACA
    /// <see cref="SonosRefreshTokenPolicy"/> - inaczej zapisalibysmy token,
    /// ktorego i tak nie wolno nam wyslac. Brak RT jest DOZWOLONY.
    /// </summary>
    public static bool IsStorable(SonosStoredCredentials? credentials)
    {
        if (credentials is null)
        {
            return false;
        }

        if (!SonosLoginBrokerConfiguration.TryCreate(credentials.BrokerOrigin, out var origin)
            || origin is null
            || !string.Equals(origin.Origin.AbsoluteUri, credentials.BrokerOrigin, StringComparison.Ordinal))
        {
            return false;
        }

        var tokens = credentials.Tokens;
        if (!SonosCredentialPolicy.IsStorableOpaqueValue(tokens.AccessToken, SonosCredentialPolicy.MaxOpaqueValueBytes))
        {
            return false;
        }

        if (!string.Equals(tokens.TokenType, SonosTokens.BearerTokenType, StringComparison.Ordinal))
        {
            return false;
        }

        if (tokens.RefreshToken is not null && !SonosRefreshTokenPolicy.IsAcceptable(tokens.RefreshToken))
        {
            return false;
        }

        if (tokens.Scope is not null && !SonosCredentialPolicy.IsStorableScopeValue(tokens.Scope))
        {
            return false;
        }

        if (tokens.ExpiresInSeconds.HasValue && !IsUsableExpiry(credentials.ReceivedAtUtc, tokens.ExpiresInSeconds.Value))
        {
            return false;
        }

        return true;
    }

    private static bool IsUsableExpiry(DateTimeOffset receivedAtUtc, int expiresInSeconds)
    {
        if (expiresInSeconds <= 0)
        {
            return false;
        }

        // Termin waznosci musi byc REPREZENTOWALNY - inaczej rekord jest niespojny.
        return (DateTimeOffset.MaxValue - receivedAtUtc).TotalSeconds > expiresInSeconds;
    }

    /// <summary>
    /// Scisla deserializacja z porownaniem origin wobec BIEZACEJ konfiguracji.
    /// Zwraca rozdzielny status; zadny komunikat ani wyjatek nie wynosi tresci.
    /// </summary>
    public static SonosCredentialReadStatus TryDeserialize(
        byte[]? plaintext,
        SonosLoginBrokerConfiguration? expectedBroker,
        out SonosStoredCredentials? credentials)
    {
        credentials = null;
        if (plaintext is null || plaintext.Length == 0 || expectedBroker is null)
        {
            return SonosCredentialReadStatus.Invalid;
        }

        if (plaintext.Length > SonosCredentialPolicy.MaxPlaintextBytes)
        {
            return SonosCredentialReadStatus.Invalid;
        }

        string text;
        try
        {
            text = StrictUtf8.GetString(plaintext);
        }
        catch (DecoderFallbackException)
        {
            return SonosCredentialReadStatus.Invalid;
        }

        StoredPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<StoredPayload>(text, Options);
        }
        catch (JsonException)
        {
            // Bez echa: komunikat nie cytuje tresci pliku.
            return SonosCredentialReadStatus.Invalid;
        }
        catch (NotSupportedException)
        {
            return SonosCredentialReadStatus.Invalid;
        }

        if (payload is null || payload.FormatVersion != SonosStoredCredentials.CurrentFormatVersion)
        {
            return SonosCredentialReadStatus.Invalid;
        }

        if (!SonosLoginBrokerConfiguration.TryCreate(payload.BrokerOrigin, out var storedOrigin) || storedOrigin is null)
        {
            return SonosCredentialReadStatus.Invalid;
        }

        if (!string.Equals(storedOrigin.Origin.AbsoluteUri, payload.BrokerOrigin, StringComparison.Ordinal))
        {
            // Plik musi zawierac postac ZNORMALIZOWANA, inaczej nie jest nasz.
            return SonosCredentialReadStatus.Invalid;
        }

        if (payload.ReceivedAtUtc is null
            || !DateTimeOffset.TryParseExact(
                payload.ReceivedAtUtc,
                ReceivedAtFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var receivedAt)
            || receivedAt.Offset != TimeSpan.Zero)
        {
            return SonosCredentialReadStatus.Invalid;
        }

        if (!SonosTokens.TryCanonicalizeTokenType(payload.TokenType, out _)
            || payload.TokenType is null
            || !string.Equals(payload.TokenType, SonosTokens.BearerTokenType, StringComparison.Ordinal))
        {
            return SonosCredentialReadStatus.Invalid;
        }

        if (!SonosCredentialPolicy.IsStorableOpaqueValue(
                payload.AccessToken,
                SonosCredentialPolicy.MaxOpaqueValueBytes))
        {
            return SonosCredentialReadStatus.Invalid;
        }

        if (payload.RefreshToken is not null && !SonosRefreshTokenPolicy.IsAcceptable(payload.RefreshToken))
        {
            return SonosCredentialReadStatus.Invalid;
        }

        if (payload.Scope is not null && !SonosCredentialPolicy.IsStorableScopeValue(payload.Scope))
        {
            return SonosCredentialReadStatus.Invalid;
        }

        if (payload.ExpiresInSeconds.HasValue && !IsUsableExpiry(receivedAt, payload.ExpiresInSeconds.Value))
        {
            return SonosCredentialReadStatus.Invalid;
        }

        SonosStoredCredentials parsed;
        try
        {
            parsed = new SonosStoredCredentials(
                payload.BrokerOrigin!,
                new SonosTokens(
                    payload.AccessToken!,
                    payload.TokenType,
                    payload.ExpiresInSeconds,
                    payload.RefreshToken,
                    payload.Scope),
                receivedAt);
        }
        catch (ArgumentException)
        {
            return SonosCredentialReadStatus.Invalid;
        }

        // Dopiero na koncu: rekord jest POPRAWNY, ale moze nalezec do innego brokera.
        if (!string.Equals(
                parsed.BrokerOrigin,
                expectedBroker.Origin.AbsoluteUri,
                StringComparison.Ordinal))
        {
            return SonosCredentialReadStatus.BrokerMismatch;
        }

        credentials = parsed;
        return SonosCredentialReadStatus.Success;
    }

    /// <summary>
    /// Postac na dysku. Nieprzewidziane pola sa ODRZUCANE, wiec obcy albo
    /// nowszy rekord nie przechodzi po cichu jako nasz.
    /// </summary>
    [JsonUnmappedMemberHandlingAttribute(JsonUnmappedMemberHandling.Disallow)]
    private sealed class StoredPayload
    {
        [JsonPropertyName("format_version")] public int? FormatVersion { get; set; }

        [JsonPropertyName("broker_origin")] public string? BrokerOrigin { get; set; }

        [JsonPropertyName("received_at_utc")] public string? ReceivedAtUtc { get; set; }

        [JsonPropertyName("access_token")] public string? AccessToken { get; set; }

        [JsonPropertyName("token_type")] public string? TokenType { get; set; }

        [JsonPropertyName("expires_in")] public int? ExpiresInSeconds { get; set; }

        [JsonPropertyName("refresh_token")] public string? RefreshToken { get; set; }

        [JsonPropertyName("scope")] public string? Scope { get; set; }
    }
}

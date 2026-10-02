using System.Text.Json;

namespace AccessibleMediaController.LiteHost.Protocol;

/// <summary>
/// Jedno zadanie od frontendu. Kontrakt: dokladnie jeden wiersz JSON,
/// obowiazkowe <c>id</c> i <c>op</c>, opcjonalne <c>args</c>.
/// </summary>
public sealed class LiteRequest
{
    public required string Id { get; init; }
    public required string Op { get; init; }
    public JsonElement Args { get; init; }
}

/// <summary>
/// Wynik odczytu jednego wiersza. Zly JSON NIE jest wyjatkiem: host musi
/// odpowiedziec bledem i czytac dalej, a nie konczyc procesu.
/// </summary>
public readonly struct LiteReadOutcome
{
    private LiteReadOutcome(LiteRequest? request, string? errorCode, string? errorMessage, string? requestId)
    {
        Request = request;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
        RequestId = requestId;
    }

    public LiteRequest? Request { get; }
    public string? ErrorCode { get; }
    public string? ErrorMessage { get; }

    /// <summary>
    /// Identyfikator odczytany z wiersza, gdy udalo sie go ustalic mimo bledu.
    /// Pozwala odpowiedziec na konkretne zadanie, a nie tylko ogolnym bledem.
    /// </summary>
    public string? RequestId { get; }

    public bool IsRequest => Request is not null;

    public static LiteReadOutcome Ok(LiteRequest request) => new(request, null, null, request.Id);

    public static LiteReadOutcome Error(string code, string message, string? requestId = null) =>
        new(null, code, message, requestId);
}

public static class LiteRequestReader
{
    /// <summary>
    /// Gorna granica dlugosci wiersza polecenia. Frontend nie ma powodu wysylac
    /// wiekszych, a bez granicy zly lub wrogi nadawca moglby wyczerpac pamiec.
    /// </summary>
    public const int MaximumLineLength = 64 * 1024;

    public static LiteReadOutcome Read(string line)
    {
        if (line.Length > MaximumLineLength)
        {
            return LiteReadOutcome.Error(
                "line_too_long",
                $"Wiersz polecenia dluzszy niz {MaximumLineLength} znakow.");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(line, new JsonDocumentOptions { MaxDepth = 32 });
        }
        catch (JsonException exception)
        {
            return LiteReadOutcome.Error("bad_json", "Niepoprawny JSON: " + exception.Message);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return LiteReadOutcome.Error("bad_request", "Wiersz polecenia musi byc obiektem JSON.");
            }

            var id = ReadRequiredString(root, "id");
            var op = ReadRequiredString(root, "op");
            if (id is null) return LiteReadOutcome.Error("bad_request", "Brak pola \"id\".");
            if (op is null) return LiteReadOutcome.Error("bad_request", "Brak pola \"op\".", id);

            var args = root.TryGetProperty("args", out var argsElement)
                ? argsElement.Clone()
                : default;
            return LiteReadOutcome.Ok(new LiteRequest { Id = id, Op = op, Args = args });
        }
    }

    private static string? ReadRequiredString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
        && value.GetString() is { Length: > 0 } text
            ? text
            : null;
}

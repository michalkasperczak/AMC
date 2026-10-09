using System.Text.Json;
using System.Text.Json.Serialization;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.LiteHost.Protocol;
using AccessibleMediaController.Windows.Services;

namespace AccessibleMediaController.LiteHost;

/// <summary>
/// Czytelnik katalogu TIDAL dla wxPython. Korzysta z tego samego klienta API,
/// tego samego odswiezania tokenu i tego samego Menedzera poswiadczen Windows
/// co glowne AMC. Profil jest otwierany wylacznie do odczytu; token nigdy nie
/// trafia do odpowiedzi protokolu.
/// </summary>
internal sealed class LiteTidalCatalogCoordinator(string statePath) : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly object _gate = new();
    private TidalIntegrationService? _integration;

    public object GetContainerItems(JsonElement args)
    {
        var request = LiteTidalCatalogContract.ReadRequest(args);
        var integration = RequireIntegration();
        var items = integration.GetContainerItemsAsync(
                request.Container,
                CancellationToken.None,
                request.ArtistSection)
            .GetAwaiter()
            .GetResult();
        return LiteTidalCatalogContract.CreateResult(
            request.Container,
            request.ArtistSection,
            items);
    }

    private TidalIntegrationService RequireIntegration()
    {
        lock (_gate)
        {
            if (_integration is not null) return _integration;
            var settings = ReadSettings();
            var integration = new TidalIntegrationService(settings);
            integration.RestoreCachedCollection(
                settings.CachedCollectionItems
                    .Select(item => item.ToMediaItem())
                    .ToArray());
            _integration = integration;
            return integration;
        }
    }

    private TidalSettings ReadSettings()
    {
        try
        {
            using var stream = new FileStream(
                statePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var document = JsonDocument.Parse(stream);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("tidal", out var tidal)
                || tidal.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException(
                    "Profil AMC nie zawiera ustawień TIDAL.");
            }
            return JsonSerializer.Deserialize<TidalSettings>(tidal.GetRawText(), JsonOptions)
                ?? throw new InvalidOperationException(
                    "Nie można odczytać ustawień TIDAL z profilu AMC.");
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (FileNotFoundException exception)
        {
            throw new InvalidOperationException(
                "Nie znaleziono profilu AMC potrzebnego do katalogu TIDAL.",
                exception);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                "Profil AMC z ustawieniami TIDAL jest uszkodzony.",
                exception);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException(
                "Nie można teraz odczytać ustawień TIDAL z profilu AMC.",
                exception);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _integration?.Dispose();
            _integration = null;
        }
    }
}

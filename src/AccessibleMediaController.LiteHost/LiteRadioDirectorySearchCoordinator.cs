using System.Diagnostics;
using System.Text.Json;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.LiteHost.Protocol;
using AccessibleMediaController.Windows.Services;

namespace AccessibleMediaController.LiteHost;

/// <summary>
/// Udostępnia wxPython publiczny katalog Radio Browser przez ten sam klient,
/// którego używa główne AMC. Protokół zawiera wyłącznie celowe metadane oraz
/// publiczne adresy HTTP(S); identyfikator katalogu pozostaje danymi modelu.
/// </summary>
internal sealed class LiteRadioDirectorySearchCoordinator : IDisposable
{
    public const string Operation = "radio.searchDirectory";
    public const string CountriesOperation = "radio.browseCountries";
    public const string StationsOperation = "radio.browseStations";
    private const int MaximumQueryLength = 300;

    private readonly RadioBrowserClient _catalog = new();

    public object BrowseCountries()
    {
        try
        {
            var countries = _catalog.BrowseCountriesAsync()
                .GetAwaiter().GetResult();
            return new
            {
                items = countries.Select(country => new
                {
                    code = country.Code,
                    name = country.Name,
                    stationCount = country.StationCount
                }).ToArray()
            };
        }
        catch (Exception exception) when (IsDirectoryFailure(exception))
        {
            throw new LiteRequestException(
                "Nie można teraz pobrać krajów Radio Browser.");
        }
    }

    public object BrowseStations(JsonElement args)
    {
        var code = LiteArgs.RequireText(args, "countryCode").Trim();
        var order = LiteArgs.RequireText(args, "order").Trim();
        if (!args.TryGetProperty("offset", out var rawOffset)
            || rawOffset.ValueKind != JsonValueKind.Number
            || !rawOffset.TryGetInt32(out var offset)
            || offset < 0)
            throw new LiteRequestException("Nieprawidłowa strona katalogu stacji.");
        try
        {
            var page = _catalog.BrowseStationsAsync(code, order, offset)
                .GetAwaiter().GetResult();
            var knownAddresses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var items = page.Items
                .Where(item => TryPublicAddress(item.Source, out _))
                .Where(item => knownAddresses.Add(NormalizeAddress(item.Source!)))
                .Select(ToPayload)
                .ToArray();
            return new { items, nextOffset = page.HasMore ? offset + 100 : (int?)null };
        }
        catch (ArgumentException)
        {
            throw new LiteRequestException("Nieprawidłowy kraj lub porządek katalogu.");
        }
        catch (Exception exception) when (IsDirectoryFailure(exception))
        {
            throw new LiteRequestException(
                "Nie można teraz pobrać stacji Radio Browser.");
        }
    }

    private static bool IsDirectoryFailure(Exception exception) =>
        exception is HttpRequestException
            or InvalidDataException
            or JsonException
            or NotSupportedException
            or TimeoutException
            or OperationCanceledException;

    public object Search(JsonElement args)
    {
        var query = NormalizeQuery(LiteArgs.RequireText(args, "query"));
        if (query.Length == 0)
            throw new LiteRequestException("Wpisz tekst do wyszukania.");

        var stopwatch = Stopwatch.StartNew();
        IReadOnlyList<MediaItem> found;
        try
        {
            found = _catalog.SearchAsync(query, CancellationToken.None)
                .GetAwaiter().GetResult();
        }
        catch (Exception exception) when (exception is HttpRequestException
            or InvalidDataException
            or JsonException
            or ArgumentException
            or NotSupportedException
            or TimeoutException
            or OperationCanceledException)
        {
            Console.Error.WriteLine(
                $"[lite-host] katalog Radio Browser: chwilowo niedostępny, "
                + $"{stopwatch.Elapsed.TotalSeconds:0.0} s");
            throw new LiteRequestException(
                "Publiczny katalog stacji radiowych jest chwilowo niedostępny.");
        }

        var knownAddresses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var items = found
            .Where(item => TryPublicAddress(item.Source, out _))
            .Where(item => knownAddresses.Add(NormalizeAddress(item.Source!)))
            .Select(ToPayload)
            .ToArray();
        Console.Error.WriteLine(
            $"[lite-host] katalog Radio Browser: {items.Length} wyników, "
            + $"{stopwatch.Elapsed.TotalSeconds:0.0} s");
        return new { items, warnings = Array.Empty<string>() };
    }

    private static object ToPayload(MediaItem item) => new
    {
        id = item.Id,
        title = string.IsNullOrWhiteSpace(item.Title)
            ? "Stacja bez nazwy"
            : item.Title.Trim(),
        kind = "station",
        remoteKind = "radioStation",
        address = item.Source,
        pageAddress = PublicAddressOrNull(item.HomepageUri),
        detail = Detail(item)
    };

    private static string Detail(MediaItem item)
    {
        var parts = new[]
        {
            Clean(item.Country),
            Clean(item.Language),
            Clean(item.Codec),
            item.BitrateKbps is > 0 ? $"{item.BitrateKbps} kb/s" : null
        };
        return string.Join(", ", parts
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.CurrentCultureIgnoreCase));
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NormalizeQuery(string query)
    {
        var normalized = string.Join(' ', query.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return normalized.Length <= MaximumQueryLength
            ? normalized
            : normalized[..MaximumQueryLength].Trim();
    }

    private static string? PublicAddressOrNull(string? value) =>
        TryPublicAddress(value, out var address) ? address!.AbsoluteUri : null;

    private static bool TryPublicAddress(string? value, out Uri? address)
    {
        address = null;
        if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var candidate)
            || candidate.Scheme is not ("http" or "https")
            || !string.IsNullOrEmpty(candidate.UserInfo))
        {
            return false;
        }
        address = candidate;
        return true;
    }

    private static string NormalizeAddress(string address)
    {
        if (!TryPublicAddress(address, out var uri)) return address.Trim();
        return uri!.GetComponents(
                UriComponents.SchemeAndServer | UriComponents.PathAndQuery,
                UriFormat.SafeUnescaped)
            .TrimEnd('/');
    }

    public void Dispose() => _catalog.Dispose();
}

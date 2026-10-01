using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace AccessibleMediaController.Core.Sonos;

/// <summary>
/// Kontrakt ODCZYTU ULUBIONYCH domu (F1a) z OFICJALNEGO Sonos Control API.
///
/// Zrodlo: definicja OpenAPI 3.0.3 "Sonos Control API (cloud)"
/// v1.56.0-alpha.1-1-gc264f93f-production-cloud, operacja
/// Favorites-GetFavorites-HouseholdId: GET /households/{householdId}/favorites,
/// schematy favoritesList / favorite / service. Wszystkie limity dlugosci i
/// liczebnosci pochodza z pol maxLength/maxItems tej definicji, nie z domyslow.
///
/// Granice tego etapu: model + walidacja odpowiedzi + jedna metoda odczytu na
/// istniejacym transporcie. NIE ma tu konta, generacji dostepu, odnawiania,
/// koordynatora, wlasciciela, UI, skrotow, presetow, AppSettings, zapisu
/// (loadFavorite ani innego POST), subskrypcji zdarzen, cache ani pollingu.
///
/// Pole imageUrl obiektow favorite i service jest w definicji OZNACZONE JAKO
/// PRZETERMINOWANE (deprecated od 1.21.0). Nie jest tu czytane ani przenoszone
/// - AMC nie pobiera okladek na tym etapie. Pola nieznane sa IGNOROWANE.
///
/// Definicja NIE podaje rodzaju materialu ulubionego (brak pola type). AMC
/// niczego nie zgaduje z nazwy ani z serwisu.
/// </summary>
public static class SonosFavoritesLimits
{
    /// <summary>favoritesList.version: maxLength 36.</summary>
    public const int MaxVersionLength = 36;

    /// <summary>favoritesList.items: maxItems 70 (gloshnik ogranicza ulubione do 70).</summary>
    public const int MaxItems = 70;

    /// <summary>favorite.id: maxLength 36.</summary>
    public const int MaxFavoriteIdLength = 36;

    /// <summary>favorite.name: maxLength 100.</summary>
    public const int MaxFavoriteNameLength = 100;

    /// <summary>favorite.description: maxLength 256, nullable, opcjonalne.</summary>
    public const int MaxFavoriteDescriptionLength = 256;

    /// <summary>service.name: maxLength 31, nullable, opcjonalne.</summary>
    public const int MaxServiceNameLength = 31;

    /// <summary>service.id: maxLength 10, nullable, opcjonalne.</summary>
    public const int MaxServiceIdLength = 10;
}

/// <summary>
/// Dostawca materialu ulubionego (schema "service"). Caly obiekt jest
/// OPCJONALNY i nullable, a oba jego pola takze - wiec brak nazwy i brak
/// identyfikatora to LEGALNA odpowiedz, nie blad.
/// </summary>
public sealed class SonosFavoriteService
{
    public SonosFavoriteService(string? name, string? id)
    {
        if (name is not null && name.Length > SonosFavoritesLimits.MaxServiceNameLength)
        {
            throw new ArgumentOutOfRangeException(nameof(name));
        }

        if (id is not null && id.Length > SonosFavoritesLimits.MaxServiceIdLength)
        {
            throw new ArgumentOutOfRangeException(nameof(id));
        }

        Name = name;
        Id = id;
    }

    public string? Name { get; }

    public string? Id { get; }

    /// <summary>Nie wypisuje tresci ze zrodla - tylko obecnosc pol.</summary>
    public override string ToString() =>
        "Serwis Sonos: nazwa "
        + (Name is null ? "brak" : "jest")
        + ", identyfikator "
        + (Id is null ? "brak" : "jest");
}

/// <summary>
/// Jedno ulubione domu (schema "favorite"). Niemutowalne.
///
/// Wymagane sa WYLACZNIE id i name; AMC dodatkowo wymaga, by nie byly puste -
/// pusty identyfikator nie da sie potem odtworzyc, a pusta nazwa nie da sie
/// przeczytac czytnikiem ekranu. To LOKALNA polityka OSTRZEJSZA od definicji
/// (OpenAPI nie stawia minLength), zapisana tu jawnie.
///
/// Wartosci zrodlowe zachowujemy DOKLADNIE: bez trim, bez obcinania, bez
/// zmiany wielkosci liter. Nie naprawiamy zrodla.
/// </summary>
public sealed class SonosFavorite
{
    public SonosFavorite(string id, string name, string? description = null,
        SonosFavoriteService? service = null, SonosResourceIdentity? resourceIdentity = null)
    {
        if (string.IsNullOrEmpty(id) || id.Length > SonosFavoritesLimits.MaxFavoriteIdLength)
        {
            throw new ArgumentException("Niepoprawny identyfikator ulubionego.", nameof(id));
        }

        if (string.IsNullOrEmpty(name) || name.Length > SonosFavoritesLimits.MaxFavoriteNameLength)
        {
            throw new ArgumentException("Niepoprawna nazwa ulubionego.", nameof(name));
        }

        if (description is not null
            && description.Length > SonosFavoritesLimits.MaxFavoriteDescriptionLength)
        {
            throw new ArgumentOutOfRangeException(nameof(description));
        }

        Id = id;
        Name = name;
        Description = description;
        Service = service;
        ResourceIdentity = resourceIdentity;
    }

    public string Id { get; }

    public string Name { get; }

    /// <summary>Opcjonalny, nullable opis. Brak znaczy "Sonos nie podal".</summary>
    public string? Description { get; }

    /// <summary>Opcjonalny, nullable dostawca materialu.</summary>
    public SonosFavoriteService? Service { get; }

    /// <summary>
    /// resource.id ulubionego (universalMusicObjectId) - tozsamosc MATERIALU,
    /// nie wiersza katalogu. Null znaczy "Sonos nie podal" albo "podal w
    /// postaci, ktorej nie rozumiemy"; w obu wypadkach tej pozycji nie da sie
    /// rozpoznac w metadanych grupy, co nie psuje samej listy.
    /// Nigdy nie porownuj <see cref="Id"/> z ObjectId - to inne klucze.
    /// </summary>
    public SonosResourceIdentity? ResourceIdentity { get; }

    /// <summary>
    /// Kontrolowane ToString: NIE wypisuje identyfikatora ani nazwy ze zrodla,
    /// bo wynik moze trafic do logu albo zgloszenia bledu.
    /// </summary>
    public override string ToString() =>
        "Ulubione Sonos: opis "
        + (Description is null ? "brak" : "jest")
        + ", serwis "
        + (Service is null ? "brak" : "jest");
}

/// <summary>
/// Lista ulubionych domu (schema "favoritesList") wraz z wersja i LITERALNYM
/// identyfikatorem domu, ktorego dotyczylo zapytanie.
///
/// Dwa ulubione o roznych identyfikatorach i TAKIEJ SAMEJ nazwie to DWIE
/// pozycje - nazwa nie jest kluczem. Powtorzony identyfikator odrzuca
/// natomiast CALA odpowiedz (patrz <see cref="SonosFavoritesOutcome"/>).
/// </summary>
public sealed class SonosFavoritesList
{
    public SonosFavoritesList(string householdId, string version, IReadOnlyList<SonosFavorite> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (string.IsNullOrEmpty(householdId))
        {
            throw new ArgumentException("Brak identyfikatora domu.", nameof(householdId));
        }

        if (string.IsNullOrEmpty(version) || version.Length > SonosFavoritesLimits.MaxVersionLength)
        {
            throw new ArgumentException("Niepoprawna wersja listy ulubionych.", nameof(version));
        }

        if (items.Count > SonosFavoritesLimits.MaxItems)
        {
            throw new ArgumentOutOfRangeException(nameof(items));
        }

        HouseholdId = householdId;
        Version = version;
        // Kopia defensywna: pozniejsza zmiana listy wolajacego nie rusza modelu.
        Items = new ReadOnlyCollection<SonosFavorite>(new List<SonosFavorite>(items));
    }

    /// <summary>Dom, o ktory PYTALISMY - literalnie, bez normalizacji.</summary>
    public string HouseholdId { get; }

    /// <summary>Ta sama wartosc, ktora Sonos podaje w naglowku ETag.</summary>
    public string Version { get; }

    public IReadOnlyList<SonosFavorite> Items { get; }

    /// <summary>Kontrolowane ToString: tylko licznik, bez tresci i bez identyfikatora domu.</summary>
    public override string ToString() =>
        "Ulubione Sonos: pozycji "
        + Items.Count.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// Wynik GET /households/{householdId}/favorites.
///
/// Dom BEZ ulubionych to SUKCES z pusta lista. Brak pola items albo items:null
/// to natomiast BLAD CALEJ odpowiedzi (InvalidResponse), bo puste ulubione i
/// milczenie uslugi to rozne informacje, a AMC nie ma prawa ich zlepiac.
/// Nie ma tu zadnego stanu "czesciowego" - albo cala lista, albo nic.
/// </summary>
public sealed class SonosFavoritesOutcome
{
    private SonosFavoritesOutcome(SonosControlApiStatus status, SonosFavoritesList? favorites)
    {
        Status = status;
        Favorites = favorites;
    }

    public SonosControlApiStatus Status { get; }

    public SonosFavoritesList? Favorites { get; }

    public bool Succeeded => Status == SonosControlApiStatus.Success && Favorites is not null;

    /// <summary>
    /// Staly komunikat ze wspolnego zestawu. NIGDY nie zawiera surowego
    /// globalError.reason, tresci odpowiedzi, tokenu ani klucza integracji.
    /// </summary>
    public string Message => SonosControlApiMessages.Describe(Status);

    internal static SonosFavoritesOutcome Ok(SonosFavoritesList favorites) =>
        new(SonosControlApiStatus.Success, favorites);

    internal static SonosFavoritesOutcome Failure(SonosControlApiStatus status) =>
        new(status == SonosControlApiStatus.Success ? SonosControlApiStatus.InvalidResponse : status, null);

    public override string ToString() =>
        "Odczyt ulubionych Sonos: "
        + Status
        + ", "
        + (Favorites is null ? "brak danych" : Favorites.ToString())
        + ", "
        + Message;
}

/// <summary>
/// WASKA granica odczytu ulubionych - dokladnie jedna operacja F1a. Pozwala
/// pozniejszym warstwom zalezec od tego kontraktu, a nie od calego klienta
/// transportu. Zadnego zapisu, zadnego konta.
/// </summary>
public interface ISonosFavoritesApi
{
    Task<SonosFavoritesOutcome> GetFavoritesAsync(
        string? accessToken, string? householdId, CancellationToken cancellationToken);
}

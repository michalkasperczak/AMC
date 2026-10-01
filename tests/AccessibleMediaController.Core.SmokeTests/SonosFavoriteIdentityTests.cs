using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AccessibleMediaController.Core.Sonos;

// TOZSAMOSC MATERIALU ulubionych Sonos (universalMusicObjectId) w Core.
// Mierzy FAKTYCZNY parser obu odpowiedzi przez syntetyczny HttpMessageHandler:
// bez sieci, bez konta, bez DPAPI, bez GUI, bez glosnika. Wszystkie
// identyfikatory, nazwy i accountId sa WYMYSLONE - nie pochodza z katalogu
// uzytkownika. Zaden komunikat nie wypisuje tresci odpowiedzi ani tokenu.
internal static class SonosFavoriteIdentityTests
{
    private const string Key = "00000000-0000-0000-0000-000000000042";
    private const string Token = "SYNTHETIC-ACCESS-ONLY";
    private const string Household = "Sonos_household-1.2-3";
    private const string Group = "RINCON_SYNTHETIC01400:7";

    // Ksztalt jak w RZECZYWISTEJ odpowiedzi: resource.type oraz resource.id z
    // trojka serviceId/objectId/accountId. Wartosci wymyslone.
    private const string FavoritesBody = """
        {"_objectType":"favoritesList","version":"v-77","items":[
          {"id":"kat-11","name":"Stacja Przykładowa","service":{"id":"999"},
           "resource":{"type":"STREAM",
             "id":{"serviceId":"999","objectId":"wymyslony:stacja/ALFA 01","accountId":"sn_000111"}}},
          {"id":"kat-12","name":"Album Przykładowy","service":{"id":"888"},
           "resource":{"type":"ALBUM",
             "id":{"serviceId":"888","objectId":"wymyslony:album/BETA","accountId":"sn_000222"}}},
          {"id":"kat-13","name":"Bez zasobu"},
          {"id":"kat-14","name":"Niepełna trójka",
           "resource":{"type":"STREAM","id":{"objectId":"wymyslony:stacja/GAMMA"}}}
        ]}
        """;

    // Zaladowana stacja: ta sama trojka co kat-11, ale INNA nazwa i rodzaj.
    private const string MetadataBody = """
        {"container":{"name":"Zupełnie inna nazwa teraz","type":"station","service":{"id":"999"},
           "id":{"serviceId":"999","objectId":"wymyslony:stacja/ALFA 01","accountId":"sn_000111"}},
         "streamInfo":"cokolwiek"}
        """;

    public static void Run()
    {
        var tests = new List<(string Name, Action Test)>();
        AddContractTests(tests);
        AddParserTests(tests);
        AddMatchingTests(tests);
        AddToleranceTests(tests);

        var failures = 0;
        foreach (var (name, test) in tests)
        {
            try { test(); Console.WriteLine("[OK] " + name); }
            catch (Exception ex)
            {
                failures++;
                Console.WriteLine("[FAIL] " + name + " (" + ex.GetType().Name + ": " + ex.Message + ")");
            }
        }

        Console.WriteLine($"Sonos tożsamość ulubionych: {tests.Count - failures}/{tests.Count}");
        if (failures != 0)
        {
            throw new InvalidOperationException(
                "Sonos tożsamość ulubionych: " + failures + " nieudanych testow.");
        }
    }

    // --- 1. Kontrakt: limity z definicji i niemutowalne pola ---
    private static void AddContractTests(List<(string Name, Action Test)> tests)
    {
        tests.Add(("limity universalMusicObjectId z definicji OpenAPI", () =>
        {
            Require(SonosResourceIdentityLimits.MaxObjectIdLength == 256, "objectId maxLength 256.");
            Require(SonosResourceIdentityLimits.MaxServiceIdLength == 20, "serviceId maxLength 20.");
            Require(SonosResourceIdentityLimits.MaxAccountIdLength == 128, "accountId maxLength 128.");
        }));

        tests.Add(("model tozsamosci jest niemutowalny i nie wypisuje wartosci", () =>
        {
            foreach (var property in typeof(SonosResourceIdentity).GetProperties())
            {
                Require(property.SetMethod is null, "Tozsamosc jest mutowalna.");
            }

            var identity = new SonosResourceIdentity("999", "wymyslony:stacja/ALFA 01", "sn_000111");
            var text = identity.ToString();
            Require(!text.Contains("sn_000111", StringComparison.Ordinal), "ToString wypisuje accountId.");
            Require(!text.Contains("ALFA", StringComparison.Ordinal), "ToString wypisuje objectId.");
        }));

        tests.Add(("obie strony porownania maja pole tozsamosci", () =>
        {
            Require(typeof(SonosFavorite).GetProperty("ResourceIdentity")?.PropertyType
                == typeof(SonosResourceIdentity), "SonosFavorite nie ma ResourceIdentity.");
            Require(typeof(SonosMetadataContainer).GetProperty("Identity")?.PropertyType
                == typeof(SonosResourceIdentity), "SonosMetadataContainer nie ma Identity.");
        }));

        tests.Add(("fabryka odrzuca przekroczony limit i pustke, nie rzuca", () =>
        {
            Require(SonosResourceIdentity.TryCreate(null, null, null) is null,
                "Brak wszystkich pol powinien dac null.");
            Require(SonosResourceIdentity.TryCreate("1", new string('o', 257), "a") is null,
                "objectId ponad 256 powinien dac null tozsamosci.");
            Require(SonosResourceIdentity.TryCreate(new string('s', 21), "o", "a") is null,
                "serviceId ponad 20 powinien dac null tozsamosci.");
            Require(SonosResourceIdentity.TryCreate("1", "o", new string('a', 129)) is null,
                "accountId ponad 128 powinien dac null tozsamosci.");
            Require(SonosResourceIdentity.TryCreate("1", "o", "a") is not null, "Poprawna trojka odrzucona.");
        }));
    }

    // --- 2. Parser: tozsamosc wychodzi z FAKTYCZNEGO odczytu odpowiedzi ---
    private static void AddParserTests(List<(string Name, Action Test)> tests)
    {
        tests.Add(("parser ulubionych wydobywa resource.id DOKLADNIE", () =>
        {
            var favorites = ReadFavorites(FavoritesBody);
            Require(favorites.Succeeded, "Odczyt ulubionych nie udal sie.");
            var first = favorites.Favorites!.Items[0];
            var identity = first.ResourceIdentity;
            Require(identity is not null, "Parser zignorowal resource.id ulubionego.");
            // Dokladny roundtrip stringow: spacja, wielkosc liter i dwukropek
            // zachowane literalnie, bez trim i bez normalizacji URI.
            Require(string.Equals(identity!.ServiceId, "999", StringComparison.Ordinal), "serviceId niezgodny.");
            Require(string.Equals(identity.ObjectId, "wymyslony:stacja/ALFA 01", StringComparison.Ordinal),
                "objectId nie jest dokladna kopia zrodla.");
            Require(string.Equals(identity.AccountId, "sn_000111", StringComparison.Ordinal), "accountId niezgodny.");
            Require(identity.IsComplete, "Pelna trojka uznana za niekompletna.");
        }));

        tests.Add(("parser metadanych wydobywa container.id DOKLADNIE", () =>
        {
            var metadata = ReadMetadata(MetadataBody);
            Require(metadata.Succeeded, "Odczyt metadanych nie udal sie.");
            var container = metadata.Metadata!.Container;
            Require(container is not null, "Brak kontenera.");
            var identity = container!.Identity;
            Require(identity is not null, "Parser zignorowal container.id.");
            Require(string.Equals(identity!.ObjectId, "wymyslony:stacja/ALFA 01", StringComparison.Ordinal),
                "objectId kontenera nie jest dokladna kopia zrodla.");
            Require(string.Equals(identity.AccountId, "sn_000111", StringComparison.Ordinal),
                "accountId kontenera niezgodny.");
        }));

        tests.Add(("identyfikator wiersza katalogu to NIE objectId", () =>
        {
            var favorite = ReadFavorites(FavoritesBody).Favorites!.Items[0];
            Require(!string.Equals(favorite.Id, favorite.ResourceIdentity!.ObjectId, StringComparison.Ordinal),
                "Test straciłby sens, gdyby oba klucze były tym samym napisem.");
            // Podstawienie klucza katalogu jako objectId NIE moze dawac zgody.
            var podrobka = new SonosResourceIdentity("999", favorite.Id, "sn_000111");
            Require(!podrobka.Matches(favorite.ResourceIdentity),
                "Porownanie przyjelo top-level favorite.Id jako tozsamosc materialu.");
        }));
    }

    // --- 3. Porownanie pelnej trojki ---
    private static void AddMatchingTests(List<(string Name, Action Test)> tests)
    {
        tests.Add(("pelna zgodnosc trojki mimo innej nazwy i rodzaju", () =>
        {
            var favorite = ReadFavorites(FavoritesBody).Favorites!.Items[0];
            var container = ReadMetadata(MetadataBody).Metadata!.Container!;
            Require(!string.Equals(favorite.Name, container.Name, StringComparison.Ordinal),
                "Fixture ma rozne nazwy po obu stronach - inaczej test nie mierzy nic.");
            Require(favorite.ResourceIdentity!.Matches(container.Identity),
                "Zgodna trojka nie zostala rozpoznana.");
            Require(SonosResourceIdentity.AreSameMaterial(container.Identity, favorite.ResourceIdentity),
                "Porownanie nie jest symetryczne.");
        }));

        tests.Add(("inny objectId przy tym samym serwisie i koncie to BRAK zgody", () =>
        {
            var favorite = ReadFavorites(FavoritesBody).Favorites!.Items[0];
            var container = ReadMetadata(MetadataBody
                .Replace("wymyslony:stacja/ALFA 01", "wymyslony:stacja/ALFA 02", StringComparison.Ordinal))
                .Metadata!.Container!;
            Require(string.Equals(favorite.ResourceIdentity!.ServiceId, container.Identity!.ServiceId,
                StringComparison.Ordinal), "Fixture mial miec ten sam serwis.");
            Require(!favorite.ResourceIdentity.Matches(container.Identity),
                "Zgodny serviceId uznano za zgodnosc tresci.");
        }));

        tests.Add(("inny serviceId to BRAK zgody", () =>
        {
            var favorite = ReadFavorites(FavoritesBody).Favorites!.Items[0];
            var container = ReadMetadata(MetadataBody
                .Replace("\"serviceId\":\"999\"", "\"serviceId\":\"777\"", StringComparison.Ordinal))
                .Metadata!.Container!;
            Require(!favorite.ResourceIdentity!.Matches(container.Identity),
                "Inny serwis uznano za zgodnosc.");
        }));

        tests.Add(("inne accountId to BRAK zgody", () =>
        {
            var favorite = ReadFavorites(FavoritesBody).Favorites!.Items[0];
            var container = ReadMetadata(MetadataBody
                .Replace("sn_000111", "sn_999999", StringComparison.Ordinal))
                .Metadata!.Container!;
            Require(!favorite.ResourceIdentity!.Matches(container.Identity),
                "Inne konto uznano za zgodnosc.");
        }));

        tests.Add(("rozna wielkosc liter i spacja to BRAK zgody (Ordinal, bez trim)", () =>
        {
            var favorite = ReadFavorites(FavoritesBody).Favorites!.Items[0];
            foreach (var zmiana in new[] { "wymyslony:stacja/alfa 01", "wymyslony:stacja/ALFA 01 " })
            {
                var container = ReadMetadata(MetadataBody
                    .Replace("wymyslony:stacja/ALFA 01", zmiana, StringComparison.Ordinal))
                    .Metadata!.Container!;
                Require(!favorite.ResourceIdentity!.Matches(container.Identity),
                    "Porownanie normalizuje napisy zamiast porownywac bajty.");
            }
        }));

        tests.Add(("niekompletna trojka to nieznana tozsamosc, nie wieloznacznik", () =>
        {
            var niepelne = ReadFavorites(FavoritesBody).Favorites!.Items[3];
            Require(niepelne.ResourceIdentity is not null, "Niepelna trojka powinna byc odczytana.");
            Require(!niepelne.ResourceIdentity!.IsComplete, "Trojka bez konta uznana za kompletna.");

            var container = ReadMetadata(MetadataBody).Metadata!.Container!;
            Require(!niepelne.ResourceIdentity.Matches(container.Identity),
                "Niekompletna tozsamosc dopasowala sie do pelnej.");
            Require(!container.Identity!.Matches(niepelne.ResourceIdentity),
                "Pelna tozsamosc dopasowala sie do niekompletnej.");
            Require(!niepelne.ResourceIdentity.Matches(niepelne.ResourceIdentity),
                "Niekompletna tozsamosc dopasowala sie do samej siebie.");
        }));

        tests.Add(("brak tozsamosci po ktorejkolwiek stronie to BRAK zgody", () =>
        {
            var bezZasobu = ReadFavorites(FavoritesBody).Favorites!.Items[2];
            Require(bezZasobu.ResourceIdentity is null, "Ulubione bez resource powinno miec null.");
            var container = ReadMetadata(MetadataBody).Metadata!.Container!;
            Require(!SonosResourceIdentity.AreSameMaterial(bezZasobu.ResourceIdentity, container.Identity),
                "Null po jednej stronie dal zgode.");
            Require(!container.Identity!.Matches(null), "Null po drugiej stronie dal zgode.");
        }));
    }

    // --- 4. Zgodnosc wstecz i granice odmowy ---
    private static void AddToleranceTests(List<(string Name, Action Test)> tests)
    {
        tests.Add(("starsza odpowiedz bez resource nadal daje pelna liste", () =>
        {
            const string stary = """
                {"version":"v-1","items":[
                  {"id":"kat-1","name":"Jeden","description":"Opis","service":{"name":"Serwis","id":"254"}},
                  {"id":"kat-2","name":"Dwa"}
                ]}
                """;
            var favorites = ReadFavorites(stary);
            Require(favorites.Succeeded, "Starsza odpowiedz przestala byc czytana.");
            Require(favorites.Favorites!.Items.Count == 2, "Zgubiono pozycje starszej odpowiedzi.");
            foreach (var item in favorites.Favorites.Items)
            {
                Require(item.ResourceIdentity is null, "Dorobiono tozsamosc tam, gdzie jej nie podano.");
            }
        }));

        tests.Add(("metadane bez container.id nadal sa czytane", () =>
        {
            const string stare = """
                {"container":{"name":"Stacja","type":"station","service":{"name":"Serwis","id":"254"}}}
                """;
            var metadata = ReadMetadata(stare);
            Require(metadata.Succeeded, "Starsze metadane przestaly byc czytane.");
            Require(string.Equals(metadata.Metadata!.Container!.Name, "Stacja", StringComparison.Ordinal),
                "Zgubiono nazwe kontenera.");
            Require(metadata.Metadata.Container.Identity is null, "Dorobiono tozsamosc kontenera.");
        }));

        tests.Add(("jedna pozycja bez tozsamosci nie kasuje reszty katalogu", () =>
        {
            var favorites = ReadFavorites(FavoritesBody);
            Require(favorites.Succeeded, "Mieszana odpowiedz zostala odrzucona w calosci.");
            Require(favorites.Favorites!.Items.Count == 4, "Zgubiono pozycje katalogu.");
            Require(favorites.Favorites.Items[1].ResourceIdentity!.IsComplete,
                "Pozycja z pelna trojka ucierpiala przez sasiada bez zasobu.");
        }));

        tests.Add(("globalne odmowy zostaja: duplikat pola w resource.id", () =>
        {
            const string duplikat = """
                {"version":"v","items":[{"id":"kat-1","name":"Jeden","resource":{"type":"STREAM",
                  "id":{"objectId":"a","objectId":"b","serviceId":"1","accountId":"sn_1"}}}]}
                """;
            Require(ReadFavorites(duplikat).Status == SonosControlApiStatus.InvalidResponse,
                "Zduplikowane pole JSON w nowej galezi nie zostalo odrzucone.");
            var invalidString = FavoritesBody.Replace("wymyslony:stacja/ALFA 01", "\\uD800", StringComparison.Ordinal);
            Require(ReadFavorites(invalidString).Status == SonosControlApiStatus.InvalidResponse,
                "Wadliwy escaped UTF-16 stal sie nieznana tozsamoscia zamiast bledu JSON.");
            var nested = new string('[', 17) + "0" + new string(']', 17);
            Require(ReadFavorites("{\"version\":\"v\",\"items\":[],\"unknown\":" + nested + "}").Status
                == SonosControlApiStatus.InvalidResponse, "Pominieto limit glebokosci calego JSON.");
        }));

        tests.Add(("wadliwa opcjonalna tozsamosc nie blokuje listy i metadanych", () =>
        {
            foreach (var resource in new[] { "42", "{\"id\":42}", "{\"id\":{\"objectId\":42}}" })
            {
                var favorites = ReadFavorites("{\"version\":\"v\",\"items\":[{\"id\":\"kat-1\",\"name\":\"Jeden\",\"resource\":" + resource + "},{\"id\":\"kat-2\",\"name\":\"Dwa\"}]}");
                Require(favorites.Succeeded && favorites.Favorites!.Items.Count == 2,
                    "Opcjonalny dodatek skasowal poprawny katalog.");
                Require(favorites.Favorites!.Items[0].ResourceIdentity is null,
                    "Z wadliwych pol utworzono tozsamosc.");
            }
            var metadata = ReadMetadata("{\"container\":{\"name\":\"Stacja\",\"id\":42}}");
            Require(metadata.Succeeded && metadata.Metadata!.Container!.Name == "Stacja"
                && metadata.Metadata.Container.Identity is null, "Dodatek zablokowal metadane.");
        }));

        tests.Add(("objectId ponad limit wylacza tylko opcjonalna tozsamosc", () =>
        {
            var zaDlugi = "{\"version\":\"v\",\"items\":[{\"id\":\"kat-1\",\"name\":\"Jeden\",\"resource\":{\"id\":{"
                + "\"objectId\":\"" + new string('o', 257) + "\"}}}]}";
            var favorites = ReadFavorites(zaDlugi);
            Require(favorites.Succeeded && favorites.Favorites!.Items.Count == 1
                && favorites.Favorites.Items[0].ResourceIdentity is null,
                "Za dlugi opcjonalny identyfikator zablokowal podstawowa liste.");
            var metadata = ReadMetadata("{\"container\":{\"name\":\"Stacja\",\"id\":{\"objectId\":\"" + new string('o', 257) + "\"}}}");
            Require(metadata.Succeeded && metadata.Metadata!.Container!.Identity is null,
                "Za dlugi opcjonalny identyfikator zablokowal metadane.");
        }));
    }

    private static SonosFavoritesOutcome ReadFavorites(string body)
    {
        using var handler = new Handler(_ => Json(body));
        using var client = new SonosControlApiClient(
            SonosControlApiConfiguration.CreateDefault(Key), handler);
        return client.GetFavoritesAsync(Token, Household, CancellationToken.None)
            .GetAwaiter().GetResult();
    }

    private static SonosGroupMetadataOutcome ReadMetadata(string body)
    {
        using var handler = new Handler(_ => Json(body));
        using var client = new SonosControlApiClient(
            SonosControlApiConfiguration.CreateDefault(Key), handler);
        return client.GetGroupMetadataAsync(Token, Group, CancellationToken.None)
            .GetAwaiter().GetResult();
    }

    private static void Require(bool ok, string why)
    {
        if (!ok)
        {
            // Komunikat opisuje WYMAGANIE, nigdy danych ani tokenu.
            throw new InvalidOperationException(why);
        }
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode code = HttpStatusCode.OK) =>
        new(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> reply) : HttpMessageHandler
    {
        private bool _disposed;

        protected override void Dispose(bool disposing) { _disposed = true; base.Dispose(disposing); }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var response = reply(request);
            response.RequestMessage ??= request;
            return Task.FromResult(response);
        }
    }
}

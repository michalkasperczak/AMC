using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AccessibleMediaController.Core.Sonos;

// F1a: ODCZYT ulubionych Sonos w kliencie Core.
// Wylacznie syntetyczne dane i wlasny HttpMessageHandler: bez sieci, bez konta,
// bez DPAPI, bez GUI. Token, klucz i identyfikatory w asercjach sa WYMYSLONE.
// Zaden komunikat testu nie wypisuje tokenu ani ciala odpowiedzi.
internal static class SonosFavoritesTests
{
    private const string Key = "00000000-0000-0000-0000-000000000042";
    private const string Token = "SYNTHETIC-ACCESS-ONLY";
    private const string Household = "Sonos_household-1.2-3";
    private const string Path = "https://api.ws.sonos.com/control/api/v1/households/"
        + Household + "/favorites";

    private const string FullBody = """
        {"version":"v-42","items":[
          {"id":"fav-1","name":"Radio Nowy Świat","description":"Stacja","service":{"name":"TuneIn","id":"254"}},
          {"id":"fav-2","name":"Ulubiona playlista","description":null,"service":null},
          {"id":"fav-3","name":"Album","imageUrl":"https://example.invalid/a.png","nieznanePole":123}
        ]}
        """;

    public static void Run()
    {
        var tests = new List<(string Name, Action Test)>();
        AddContractPresenceTests(tests);
        AddModelTests(tests);
        AddReadTests(tests);
        AddResponsePolicyTests(tests);
        AddRequestShapeTests(tests);
        AddTransportGateTests(tests);
        var failures = 0;
        foreach (var (name, test) in tests)
        {
            try { test(); Console.WriteLine("[OK] " + name); }
            catch (Exception ex) { failures++; Console.WriteLine("[FAIL] " + name + " (" + ex.GetType().Name + ": " + ex.Message + ")"); }
        }

        Console.WriteLine($"Sonos ulubione (odczyt): {tests.Count - failures}/{tests.Count}");
        if (failures != 0)
        {
            throw new InvalidOperationException("Sonos ulubione (odczyt): " + failures + " nieudanych testow.");
        }
    }

    // --- 1. Obecnosc API i kontraktu (to byl pierwszy, PADAJACY test etapu RED) ---
    private static void AddContractPresenceTests(List<(string Name, Action Test)> tests)
    {
        tests.Add(("klient Core udostepnia GetFavoritesAsync(token, householdId, ct)", () =>
        {
            var method = typeof(SonosControlApiClient).GetMethod(
                "GetFavoritesAsync",
                BindingFlags.Public | BindingFlags.Instance,
                new[] { typeof(string), typeof(string), typeof(CancellationToken) });
            Require(method is not null, "Brak publicznej metody GetFavoritesAsync.");
            Require(method!.ReturnType == typeof(Task<SonosFavoritesOutcome>),
                "GetFavoritesAsync nie zwraca Task<SonosFavoritesOutcome>.");
        }));
        tests.Add(("klient spelnia waska granice ISonosFavoritesApi", () =>
        {
            Require(typeof(ISonosFavoritesApi).IsAssignableFrom(typeof(SonosControlApiClient)),
                "SonosControlApiClient nie implementuje ISonosFavoritesApi.");
            using var handler = new Handler(_ => Json(FullBody));
            ISonosFavoritesApi api = new SonosControlApiClient(
                SonosControlApiConfiguration.CreateDefault(Key), handler);
            Require(api.GetFavoritesAsync(Token, Household, CancellationToken.None)
                .GetAwaiter().GetResult().Succeeded, "Odczyt przez granice nie udal sie.");
            ((IDisposable)api).Dispose();
        }));
        tests.Add(("limity z definicji OpenAPI, nie z domyslow", () =>
        {
            Require(SonosFavoritesLimits.MaxVersionLength == 36, "version maxLength 36.");
            Require(SonosFavoritesLimits.MaxItems == 70, "items maxItems 70.");
            Require(SonosFavoritesLimits.MaxFavoriteIdLength == 36, "favorite.id maxLength 36.");
            Require(SonosFavoritesLimits.MaxFavoriteNameLength == 100, "favorite.name maxLength 100.");
            Require(SonosFavoritesLimits.MaxFavoriteDescriptionLength == 256, "description maxLength 256.");
            Require(SonosFavoritesLimits.MaxServiceNameLength == 31, "service.name maxLength 31.");
            Require(SonosFavoritesLimits.MaxServiceIdLength == 10, "service.id maxLength 10.");
        }));
        tests.Add(("kontrakt nie wprowadza zapisu ani okladek", () =>
        {
            foreach (var name in typeof(ISonosFavoritesApi).GetMethods())
            {
                Require(name.Name == "GetFavoritesAsync", "Granica ma wiecej niz jedna operacje.");
            }

            foreach (var property in typeof(SonosFavorite).GetProperties())
            {
                Require(!property.Name.Contains("Image", StringComparison.OrdinalIgnoreCase),
                    "Model niesie przeterminowane imageUrl.");
                Require(!property.Name.Contains("Type", StringComparison.OrdinalIgnoreCase),
                    "Model zgaduje rodzaj materialu, ktorego definicja nie podaje.");
                Require(property.SetMethod is null, "Model ulubionego jest mutowalny.");
            }

            foreach (var property in typeof(SonosFavoriteService).GetProperties())
            {
                Require(!property.Name.Contains("Image", StringComparison.OrdinalIgnoreCase),
                    "Serwis niesie przeterminowane imageUrl.");
            }
        }));
    }

    // --- 2. Modele: niemutowalnosc, kopia defensywna, kontrolowane ToString ---
    private static void AddModelTests(List<(string Name, Action Test)> tests)
    {
        tests.Add(("lista trzyma KOPIE, zmiana u wolajacego nie rusza modelu", () =>
        {
            var source = new List<SonosFavorite> { new("a", "A") };
            var list = new SonosFavoritesList(Household, "v1", source);
            source.Add(new SonosFavorite("b", "B"));
            source.Clear();
            Require(list.Items.Count == 1 && list.Items[0].Id == "a", "Model podzielil liste z wolajacym.");
        }));
        tests.Add(("ToString nie wypisuje tresci, identyfikatorow ani nazw ze zrodla", () =>
        {
            var favorite = new SonosFavorite("SEKRETNE-ID", "SEKRETNA-NAZWA", "SEKRETNY-OPIS",
                new SonosFavoriteService("SEKRETNY-SERWIS", "SID"));
            var list = new SonosFavoritesList("SEKRETNY-DOM", "SEKRETNA-WERSJA", new[] { favorite });
            var outcome = ReadFavorites(FullBody);
            foreach (var text in new[]
            {
                favorite.ToString(), favorite.Service!.ToString(), list.ToString(), outcome.ToString()
            })
            {
                foreach (var forbidden in new[]
                {
                    "SEKRETNE-ID", "SEKRETNA-NAZWA", "SEKRETNY-OPIS", "SEKRETNY-SERWIS",
                    "SEKRETNY-DOM", "SEKRETNA-WERSJA", "Radio", "fav-1", "v-42", Token, Key
                })
                {
                    Require(!text.Contains(forbidden, StringComparison.Ordinal),
                        "ToString wypisal wartosc, ktorej nie wolno logowac.");
                }
            }

            Require(list.ToString().Contains("1", StringComparison.Ordinal), "Brak licznika pozycji.");
            Require(outcome.ToString().Contains("Success", StringComparison.Ordinal), "Brak statusu.");
        }));
        tests.Add(("model odrzuca puste i przekroczone wymagane wartosci", () =>
        {
            CheckThrows(() => new SonosFavorite("", "A"));
            CheckThrows(() => new SonosFavorite("a", ""));
            CheckThrows(() => new SonosFavorite(new string('a', 37), "A"));
            CheckThrows(() => new SonosFavorite("a", new string('n', 101)));
            CheckThrows(() => new SonosFavorite("a", "A", new string('d', 257)));
            CheckThrows(() => new SonosFavoriteService(new string('s', 32), null));
            CheckThrows(() => new SonosFavoriteService(null, new string('i', 11)));
            CheckThrows(() => new SonosFavoritesList(Household, "", System.Array.Empty<SonosFavorite>()));
            CheckThrows(() => new SonosFavoritesList("", "v1", System.Array.Empty<SonosFavorite>()));
            CheckThrows(() => new SonosFavoritesList(Household, new string('v', 37), System.Array.Empty<SonosFavorite>()));
            // Granice DOPUSZCZALNE - walidacja nie moze byc za ostra.
            Require(new SonosFavorite(new string('a', 36), new string('n', 100), new string('d', 256),
                new SonosFavoriteService(new string('s', 31), new string('i', 10))).Id.Length == 36,
                "Odrzucono legalna wartosc graniczna.");
        }));
        tests.Add(("niepowodzenie nie niesie danych, a Success bez listy jest niemozliwy", () =>
        {
            var failure = ReadFavorites("{}");
            Require(!failure.Succeeded && failure.Favorites is null, "Porazka niesie dane.");
            Require(failure.Status == SonosControlApiStatus.InvalidResponse, "Zly status porazki.");
            Require(failure.Message.Length > 0, "Brak komunikatu.");
        }));
    }

    // --- 3. Poprawne odczyty ---
    private static void AddReadTests(List<(string Name, Action Test)> tests)
    {
        tests.Add(("pelna odpowiedz: wersja, wszystkie pola, nieznane i imageUrl pominiete", () =>
        {
            var result = ReadFavorites(FullBody);
            Require(result.Succeeded, "Odczyt nie udal sie.");
            var list = result.Favorites!;
            Require(list.Version == "v-42", "Zla wersja.");
            Require(list.Items.Count == 3, "Zla liczba pozycji.");
            Require(list.Items[0].Id == "fav-1" && list.Items[0].Name == "Radio Nowy Świat",
                "Zle wartosci pierwszej pozycji.");
            Require(list.Items[0].Description == "Stacja", "Zly opis.");
            Require(list.Items[0].Service!.Name == "TuneIn" && list.Items[0].Service!.Id == "254",
                "Zly serwis.");
            Require(list.Items[1].Description is null && list.Items[1].Service is null,
                "null w polu opcjonalnym nie dal braku.");
            Require(list.Items[2].Description is null && list.Items[2].Service is null,
                "Brak pola opcjonalnego nie dal braku.");
        }));
        tests.Add(("minimalna pozycja: tylko wymagane id i name", () =>
        {
            var result = ReadFavorites("{\"version\":\"v\",\"items\":[{\"id\":\"i\",\"name\":\"n\"}]}");
            Require(result.Succeeded && result.Favorites!.Items.Count == 1, "Minimalna pozycja odrzucona.");
        }));
        tests.Add(("dom bez ulubionych to SUKCES z pusta lista", () =>
        {
            var result = ReadFavorites("{\"version\":\"v0\",\"items\":[]}");
            Require(result.Succeeded, "Pusta lista uznana za blad.");
            Require(result.Favorites!.Items.Count == 0 && result.Favorites.Version == "v0",
                "Pusta lista zle odczytana.");
        }));
        tests.Add(("kolejnosc pozycji zachowana wprost ze zrodla", () =>
        {
            var body = Build(new[] { "c", "a", "b", "z" });
            var items = ReadFavorites(body).Favorites!.Items;
            Require(items.Count == 4, "Zla liczba pozycji.");
            Require(items[0].Id == "c" && items[1].Id == "a" && items[2].Id == "b" && items[3].Id == "z",
                "Klient przestawil kolejnosc ulubionych.");
        }));
        tests.Add(("rozne identyfikatory o TEJ SAMEJ nazwie to dwie pozycje", () =>
        {
            var result = ReadFavorites(
                "{\"version\":\"v\",\"items\":[{\"id\":\"x1\",\"name\":\"Ta sama\"},{\"id\":\"x2\",\"name\":\"Ta sama\"}]}");
            Require(result.Succeeded && result.Favorites!.Items.Count == 2,
                "Nazwa uznana za klucz - zgubiono pozycje.");
            Require(result.Favorites!.Items[0].Id != result.Favorites.Items[1].Id, "Zle identyfikatory.");
        }));
        tests.Add(("pelne 70 pozycji przyjete, 71 odrzucone", () =>
        {
            var ids70 = new string[70];
            for (var i = 0; i < 70; i++) { ids70[i] = "f" + i; }
            Require(ReadFavorites(Build(ids70)).Favorites!.Items.Count == 70, "70 pozycji odrzucone.");
            var ids71 = new string[71];
            for (var i = 0; i < 71; i++) { ids71[i] = "f" + i; }
            Require(ReadFavorites(Build(ids71)).Status == SonosControlApiStatus.InvalidResponse,
                "71 pozycji przyjete wbrew maxItems 70.");
        }));
        tests.Add(("wartosci zrodlowe bez trim, obcinania i zmiany wielkosci liter", () =>
        {
            var result = ReadFavorites(
                "{\"version\":\" V-1 \",\"items\":[{\"id\":\" Fav_ID \",\"name\":\"  MiXeD CaSe  \"}]}");
            Require(result.Succeeded, "Odczyt nie udal sie.");
            Require(result.Favorites!.Version == " V-1 ", "Wersja zmieniona.");
            Require(result.Favorites.Items[0].Id == " Fav_ID ", "Identyfikator zmieniony.");
            Require(result.Favorites.Items[0].Name == "  MiXeD CaSe  ", "Nazwa zmieniona.");
        }));
        tests.Add(("wynik niesie LITERALNIE pytany identyfikator domu", () =>
        {
            foreach (var household in new[] { Household, "ABC_dom-9", "x.y.z" })
            {
                using var handler = new Handler(_ => Json("{\"version\":\"v\",\"items\":[]}"));
                using var client = new SonosControlApiClient(
                    SonosControlApiConfiguration.CreateDefault(Key), handler);
                var result = client.GetFavoritesAsync(Token, household, CancellationToken.None)
                    .GetAwaiter().GetResult();
                Require(result.Succeeded && result.Favorites!.HouseholdId == household,
                    "Wynik nie niesie pytanego identyfikatora domu.");
            }
        }));
    }

    // --- 4. Polityka odpowiedzi: cala odpowiedz albo nic ---
    private static void AddResponsePolicyTests(List<(string Name, Action Test)> tests)
    {
        tests.Add(("brak wymaganego version, items, id albo name odrzuca CALOSC", () =>
        {
            foreach (var body in new[]
            {
                "{\"items\":[]}",
                "{\"version\":\"v\"}",
                "{\"version\":\"v\",\"items\":[{\"name\":\"n\"}]}",
                "{\"version\":\"v\",\"items\":[{\"id\":\"i\"}]}",
                "{\"version\":\"\",\"items\":[]}",
                "{\"version\":\"v\",\"items\":[{\"id\":\"\",\"name\":\"n\"}]}",
                "{\"version\":\"v\",\"items\":[{\"id\":\"i\",\"name\":\"\"}]}"
            })
            {
                var result = ReadFavorites(body);
                Require(result.Status == SonosControlApiStatus.InvalidResponse && result.Favorites is null,
                    "Braki w wymaganych polach nie odrzucily odpowiedzi.");
            }
        }));
        tests.Add(("items:null i items zlego typu to BLAD, nie pusta lista", () =>
        {
            foreach (var body in new[]
            {
                "{\"version\":\"v\",\"items\":null}",
                "{\"version\":\"v\",\"items\":{}}",
                "{\"version\":\"v\",\"items\":\"\"}",
                "{\"version\":\"v\",\"items\":0}",
                "{\"version\":\"v\",\"items\":[null]}",
                "{\"version\":\"v\",\"items\":[\"fav-1\"]}",
                "{\"version\":null,\"items\":[]}",
                "{\"version\":7,\"items\":[]}"
            })
            {
                var result = ReadFavorites(body);
                Require(result.Status == SonosControlApiStatus.InvalidResponse,
                    "Milczenie uslugi zlepione z pusta lista albo zly typ przyjety.");
                Require(result.Favorites is null, "Porazka niesie dane.");
            }
        }));
        tests.Add(("przekroczone dlugosci wymaganych i opcjonalnych pol odrzucaja CALOSC", () =>
        {
            foreach (var body in new[]
            {
                "{\"version\":\"" + new string('v', 37) + "\",\"items\":[]}",
                "{\"version\":\"v\",\"items\":[{\"id\":\"" + new string('a', 37) + "\",\"name\":\"n\"}]}",
                "{\"version\":\"v\",\"items\":[{\"id\":\"i\",\"name\":\"" + new string('n', 101) + "\"}]}",
                "{\"version\":\"v\",\"items\":[{\"id\":\"i\",\"name\":\"n\",\"description\":\"" + new string('d', 257) + "\"}]}",
                "{\"version\":\"v\",\"items\":[{\"id\":\"i\",\"name\":\"n\",\"service\":{\"name\":\"" + new string('s', 32) + "\"}}]}",
                "{\"version\":\"v\",\"items\":[{\"id\":\"i\",\"name\":\"n\",\"service\":{\"id\":\"" + new string('x', 11) + "\"}}]}"
            })
            {
                Require(ReadFavorites(body).Status == SonosControlApiStatus.InvalidResponse,
                    "Przekroczona dlugosc z definicji przyjeta.");
            }

            // Granice DOPUSZCZALNE musza przejsc - inaczej walidacja jest za ostra.
            Require(ReadFavorites("{\"version\":\"" + new string('v', 36)
                + "\",\"items\":[{\"id\":\"" + new string('a', 36) + "\",\"name\":\"" + new string('n', 100)
                + "\",\"description\":\"" + new string('d', 256) + "\",\"service\":{\"name\":\""
                + new string('s', 31) + "\",\"id\":\"" + new string('x', 10) + "\"}}]}").Succeeded,
                "Legalne wartosci graniczne odrzucone.");
        }));
        tests.Add(("POWTORZONY identyfikator ulubionego odrzuca CALOSC bez danych", () =>
        {
            var result = ReadFavorites(
                "{\"version\":\"v\",\"items\":[{\"id\":\"dup\",\"name\":\"A\"},{\"id\":\"x\",\"name\":\"B\"},{\"id\":\"dup\",\"name\":\"C\"}]}");
            Require(result.Status == SonosControlApiStatus.InvalidResponse,
                "Powtorzony identyfikator przyjety.");
            Require(result.Favorites is null, "Powtorzony identyfikator dal czesciowa liste.");
        }));
        tests.Add(("opcjonalny service w zlym typie odrzucony, brak i null legalne", () =>
        {
            foreach (var value in new[] { "\"TuneIn\"", "7", "[]", "true" })
            {
                Require(ReadFavorites("{\"version\":\"v\",\"items\":[{\"id\":\"i\",\"name\":\"n\",\"service\":"
                    + value + "}]}").Status == SonosControlApiStatus.InvalidResponse,
                    "service zlego typu przyjety.");
            }

            Require(ReadFavorites("{\"version\":\"v\",\"items\":[{\"id\":\"i\",\"name\":\"n\",\"service\":{}}]}")
                .Favorites!.Items[0].Service is not null, "Pusty obiekt service zgubiony.");
            Require(ReadFavorites(
                "{\"version\":\"v\",\"items\":[{\"id\":\"i\",\"name\":\"n\",\"service\":{\"name\":null,\"id\":null}}]}")
                .Succeeded, "service z nullami odrzucony wbrew definicji.");
            Require(ReadFavorites(
                "{\"version\":\"v\",\"items\":[{\"id\":\"i\",\"name\":\"n\",\"description\":7}]}")
                .Status == SonosControlApiStatus.InvalidResponse, "description zlego typu przyjety.");
        }));
        tests.Add(("duplikat pola JSON i zly korzen odrzucone przez wspolny parser", () =>
        {
            foreach (var body in new[]
            {
                "{\"version\":\"v\",\"version\":\"w\",\"items\":[]}",
                "{\"version\":\"v\",\"items\":[{\"id\":\"i\",\"id\":\"j\",\"name\":\"n\"}]}",
                "{\"version\":\"v\",\"items\":[{\"id\":\"i\",\"name\":\"n\",\"service\":{\"id\":\"1\",\"id\":\"2\"}}]}",
                "[]", "\"v\"", "null", "{"
            })
            {
                Require(ReadFavorites(body).Status == SonosControlApiStatus.InvalidResponse,
                    "Duplikat pola JSON albo zly korzen przyjety.");
            }
        }));
        tests.Add(("zbyt gleboka odpowiedz odrzucona przez wspolny MaxDepth", () =>
        {
            var deep = new StringBuilder("{\"version\":\"v\",\"items\":[{\"id\":\"i\",\"name\":\"n\",\"x\":");
            for (var i = 0; i < 40; i++) { deep.Append('['); }
            deep.Append('1');
            for (var i = 0; i < 40; i++) { deep.Append(']'); }
            deep.Append("}]}");
            Require(ReadFavorites(deep.ToString()).Status == SonosControlApiStatus.InvalidResponse,
                "Bramka glebokosci JSON pominieta.");
        }));
    }

    // --- 5. Kształt zapytania: dokladnie jeden GET, zadnego POST ---
    private static void AddRequestShapeTests(List<(string Name, Action Test)> tests)
    {
        tests.Add(("dokladnie jeden GET na wlasciwy adres z naglowkami, zero POST", () =>
        {
            var posts = 0;
            using var handler = new Handler(request =>
            {
                if (request.Method != HttpMethod.Get) { posts++; }
                Require(request.Method == HttpMethod.Get, "Odczyt ulubionych nie jest GET.");
                Require(request.Content is null, "Odczyt wyslal cialo zapytania.");
                Require(request.RequestUri!.AbsoluteUri == Path, "Zly adres odczytu ulubionych.");
                Require(request.Headers.Authorization!.Scheme == "Bearer", "Brak schematu Bearer.");
                Require(request.Headers.Authorization.Parameter == Token, "Token zmieniony.");
                Require(request.Headers.GetValues("X-Sonos-Api-Key").GetEnumerator().MoveNext(),
                    "Brak naglowka klucza integracji.");
                Require(request.Headers.Accept.ToString().Contains("application/json", StringComparison.Ordinal),
                    "Brak Accept application/json.");
                return Json("{\"version\":\"v\",\"items\":[]}");
            });
            using var client = new SonosControlApiClient(
                SonosControlApiConfiguration.CreateDefault(Key), handler);
            Require(client.GetFavoritesAsync(Token, Household, CancellationToken.None)
                .GetAwaiter().GetResult().Succeeded, "Odczyt nie udal sie.");
            Require(handler.Count == 1 && posts == 0, "Nie dokladnie jeden GET bez POST.");
        }));
        tests.Add(("niepoprawny dom, token albo klucz: ZERO wyslan", () =>
        {
            foreach (var (token, household, key) in new (string?, string?, string)[]
            {
                (Token, null, Key), (Token, "", Key), (Token, "a/b", Key), (Token, "a%2Fb", Key),
                (Token, "..", Key), (Token, "dom?x", Key), (Token, "dom b", Key), (Token, "dom@x", Key),
                (Token, new string('h', 65), Key), (null, Household, Key), ("", Household, Key),
                ("token ze spacja", Household, Key), (Token, Household, "")
            })
            {
                using var handler = new Handler(_ => Json("{\"version\":\"v\",\"items\":[]}"));
                using var client = new SonosControlApiClient(
                    SonosControlApiConfiguration.CreateDefault(key), handler);
                var result = client.GetFavoritesAsync(token, household, CancellationToken.None)
                    .GetAwaiter().GetResult();
                Require(result.Status == SonosControlApiStatus.InvalidConfiguration,
                    "Niepoprawne wejscie nie dalo InvalidConfiguration.");
                Require(result.Favorites is null, "Porazka niesie dane.");
                Require(handler.Count == 0, "Niepoprawne wejscie poszlo w swiat.");
            }
        }));
        tests.Add(("dom z dozwolonymi znakami trafia w adres literalnie", () =>
        {
            using var handler = new Handler(request =>
            {
                Require(request.RequestUri!.AbsoluteUri
                    == "https://api.ws.sonos.com/control/api/v1/households/A_b-1.2/favorites",
                    "Identyfikator domu zmieniony w adresie.");
                return Json("{\"version\":\"v\",\"items\":[]}");
            });
            using var client = new SonosControlApiClient(
                SonosControlApiConfiguration.CreateDefault(Key), handler);
            Require(client.GetFavoritesAsync(Token, "A_b-1.2", CancellationToken.None)
                .GetAwaiter().GetResult().Succeeded, "Odczyt nie udal sie.");
        }));
    }

    // --- 6. Bramki transportu: nowe wejscie MUSI przez nie przechodzic ---
    private static void AddTransportGateTests(List<(string Name, Action Test)> tests)
    {
        tests.Add(("kody HTTP mapowane tak samo jak w pozostalych odczytach", () =>
        {
            foreach (var (code, expected) in new (int, SonosControlApiStatus)[]
            {
                (400, SonosControlApiStatus.RequestRejected),
                (401, SonosControlApiStatus.Unauthorized),
                (403, SonosControlApiStatus.Forbidden),
                (404, SonosControlApiStatus.NotFound),
                (429, SonosControlApiStatus.RateLimited),
                (499, SonosControlApiStatus.CommandFailed),
                (500, SonosControlApiStatus.ServiceError),
                (503, SonosControlApiStatus.ServiceError),
                (301, SonosControlApiStatus.RedirectRefused),
                (302, SonosControlApiStatus.RedirectRefused),
                (307, SonosControlApiStatus.RedirectRefused)
            })
            {
                using var handler = new Handler(_ => Json("{\"globalError\":{\"reason\":\"SUROWY-POWOD\"}}",
                    (HttpStatusCode)code));
                using var client = new SonosControlApiClient(
                    SonosControlApiConfiguration.CreateDefault(Key), handler);
                var result = client.GetFavoritesAsync(Token, Household, CancellationToken.None)
                    .GetAwaiter().GetResult();
                Require(result.Status == expected, "Zle odwzorowanie kodu HTTP " + code + ".");
                Require(result.Favorites is null, "Blad HTTP niesie dane.");
                Require(!result.Message.Contains("SUROWY-POWOD", StringComparison.Ordinal)
                    && !result.ToString().Contains("SUROWY-POWOD", StringComparison.Ordinal),
                    "Surowy globalError.reason przeciekl do komunikatu.");
            }
        }));
        tests.Add(("obcy koncowy adres odpowiedzi i brak wiadomosci zadania odrzucone", () =>
        {
            foreach (var target in new[]
            {
                "https://attacker.invalid/",
                "https://api.ws.sonos.com/control/api/v1/households/" + Household + "/groups"
            })
            {
                using var handler = new Handler(_ =>
                {
                    var response = Json("{\"version\":\"v\",\"items\":[]}");
                    response.RequestMessage = new HttpRequestMessage(HttpMethod.Get, target);
                    return response;
                });
                using var client = new SonosControlApiClient(
                    SonosControlApiConfiguration.CreateDefault(Key), handler);
                Require(client.GetFavoritesAsync(Token, Household, CancellationToken.None)
                    .GetAwaiter().GetResult().Status == SonosControlApiStatus.RedirectRefused,
                    "Obcy adres koncowy przyjety.");
            }

            using var bare = new Handler(_ => Json("{\"version\":\"v\",\"items\":[]}"), attachRequest: false);
            using var bareClient = new SonosControlApiClient(
                SonosControlApiConfiguration.CreateDefault(Key), bare);
            Require(bareClient.GetFavoritesAsync(Token, Household, CancellationToken.None)
                .GetAwaiter().GetResult().Status == SonosControlApiStatus.RedirectRefused,
                "Brak wiadomosci zadania przyjety.");
        }));
        tests.Add(("zly typ tresci i zly charset odrzucone", () =>
        {
            foreach (var (media, charset) in new[]
            {
                ("text/html", "utf-8"), ("text/plain", "utf-8"), ("application/xml", "utf-8"),
                ("application/json", "iso-8859-2")
            })
            {
                using var handler = new Handler(_ =>
                {
                    var response = new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("{\"version\":\"v\",\"items\":[]}",
                            Encoding.UTF8, media)
                    };
                    response.Content.Headers.ContentType!.CharSet = charset;
                    return response;
                });
                using var client = new SonosControlApiClient(
                    SonosControlApiConfiguration.CreateDefault(Key), handler);
                Require(client.GetFavoritesAsync(Token, Household, CancellationToken.None)
                    .GetAwaiter().GetResult().Status == SonosControlApiStatus.InvalidResponse,
                    "Zly typ tresci albo charset przyjety.");
            }

            using var noType = new Handler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new ByteArrayContent(Encoding.UTF8.GetBytes("{\"version\":\"v\",\"items\":[]}")) });
            using var noTypeClient = new SonosControlApiClient(
                SonosControlApiConfiguration.CreateDefault(Key), noType);
            Require(noTypeClient.GetFavoritesAsync(Token, Household, CancellationToken.None)
                .GetAwaiter().GetResult().Status == SonosControlApiStatus.InvalidResponse,
                "Brak typu tresci przyjety.");
        }));
        tests.Add(("zle bajty UTF-8 odrzucone", () =>
        {
            using var handler = new Handler(_ =>
            {
                var content = new ByteArrayContent(new byte[] { 0x7B, 0xFF, 0xFE, 0x7D });
                content.Headers.ContentType =
                    new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
            });
            using var client = new SonosControlApiClient(
                SonosControlApiConfiguration.CreateDefault(Key), handler);
            Require(client.GetFavoritesAsync(Token, Household, CancellationToken.None)
                .GetAwaiter().GetResult().Status == SonosControlApiStatus.InvalidResponse,
                "Niepoprawne UTF-8 przyjete.");
        }));
        tests.Add(("odpowiedz ponad 256 KiB odrzucona takze BEZ Content-Length", () =>
        {
            var big = "{\"version\":\"v\",\"items\":[],\"x\":\"" + new string('a', 300 * 1024) + "\"}";
            Require(ReadFavorites(big).Status == SonosControlApiStatus.InvalidResponse,
                "Za duza odpowiedz przyjeta (z Content-Length).");

            using var chunked = new Handler(_ =>
            {
                var content = new StreamContent(new System.IO.MemoryStream(Encoding.UTF8.GetBytes(big)));
                content.Headers.ContentType =
                    new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                content.Headers.ContentLength = null;
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
            });
            using var client = new SonosControlApiClient(
                SonosControlApiConfiguration.CreateDefault(Key), chunked);
            Require(client.GetFavoritesAsync(Token, Household, CancellationToken.None)
                .GetAwaiter().GetResult().Status == SonosControlApiStatus.InvalidResponse,
                "Za duza odpowiedz bez Content-Length przyjeta.");
        }));
        tests.Add(("anulowanie PRZED wyslaniem: ZERO wyslan", () =>
        {
            using var source = new CancellationTokenSource();
            source.Cancel();
            using var handler = new Handler(_ => Json("{\"version\":\"v\",\"items\":[]}"));
            using var client = new SonosControlApiClient(
                SonosControlApiConfiguration.CreateDefault(Key), handler);
            var result = client.GetFavoritesAsync(Token, Household, source.Token)
                .GetAwaiter().GetResult();
            Require(result.Status == SonosControlApiStatus.Canceled, "Anulowanie nie dalo Canceled.");
            Require(handler.Count == 0, "Anulowany odczyt poszedl w swiat.");
        }));
        tests.Add(("anulowanie wolajacego PODCZAS czytania ciala", () => SlowBody(true)));
        tests.Add(("deadline klienta obejmuje czytanie ciala", () => SlowBody(false)));
        tests.Add(("brak polaczenia to Unreachable, nie InvalidResponse", () =>
        {
            using var handler = new Handler(_ => throw new HttpRequestException("brak sieci"));
            using var client = new SonosControlApiClient(
                SonosControlApiConfiguration.CreateDefault(Key), handler);
            Require(client.GetFavoritesAsync(Token, Household, CancellationToken.None)
                .GetAwaiter().GetResult().Status == SonosControlApiStatus.Unreachable,
                "Brak polaczenia zle odwzorowany.");
        }));
        tests.Add(("handler wolajacego zyje po Dispose klienta", () =>
        {
            using var handler = new Handler(_ => Json("{\"version\":\"v\",\"items\":[]}"));
            using (var first = new SonosControlApiClient(
                SonosControlApiConfiguration.CreateDefault(Key), handler))
            {
                Require(first.GetFavoritesAsync(Token, Household, CancellationToken.None)
                    .GetAwaiter().GetResult().Succeeded, "Pierwszy odczyt nie udal sie.");
            }

            Require(!handler.Disposed, "Klient zabral handler wolajacego.");
            using var second = new SonosControlApiClient(
                SonosControlApiConfiguration.CreateDefault(Key), handler);
            Require(second.GetFavoritesAsync(Token, Household, CancellationToken.None)
                .GetAwaiter().GetResult().Succeeded, "Drugi odczyt nie udal sie.");
            Require(handler.Count == 2, "Zla liczba wyslan.");
        }));
    }

    private static void SlowBody(bool callerCancels)
    {
        using var caller = new CancellationTokenSource();
        using var stream = new SlowStream(callerCancels ? caller : null);
        using var handler = new Handler(_ =>
        {
            var response = Json("{\"version\":\"v\",\"items\":[]}");
            response.Content = new StreamContent(stream);
            response.Content.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            return response;
        });
        using var client = new SonosControlApiClient(SonosControlApiConfiguration.CreateDefault(Key), handler,
            TimeSpan.FromMilliseconds(callerCancels ? 5000 : 100));
        var result = client.GetFavoritesAsync(Token, Household, caller.Token)
            .WaitAsync(TimeSpan.FromSeconds(3)).GetAwaiter().GetResult();
        Require(result.Status == (callerCancels
                ? SonosControlApiStatus.Canceled : SonosControlApiStatus.Unreachable),
            "Anulowanie/deadline podczas czytania ciala zle odwzorowane.");
        Require(stream.ReadStarted && stream.Disposed && handler.Count == 1,
            "Strumien ciala nie zostal ruszony albo zwolniony.");
    }

    private static string Build(IReadOnlyList<string> ids)
    {
        var builder = new StringBuilder("{\"version\":\"v\",\"items\":[");
        for (var i = 0; i < ids.Count; i++)
        {
            if (i != 0) { builder.Append(','); }
            builder.Append("{\"id\":\"").Append(ids[i]).Append("\",\"name\":\"n").Append(i).Append("\"}");
        }

        return builder.Append("]}").ToString();
    }

    private static SonosFavoritesOutcome ReadFavorites(string body)
    {
        using var handler = new Handler(_ => Json(body));
        using var client = new SonosControlApiClient(
            SonosControlApiConfiguration.CreateDefault(Key), handler);
        return client.GetFavoritesAsync(Token, Household, CancellationToken.None)
            .GetAwaiter().GetResult();
    }

    private static void CheckThrows(Action action)
    {
        try { action(); }
        catch (ArgumentException) { return; }
        throw new InvalidOperationException("Oczekiwano odrzucenia argumentu.");
    }

    private static void Require(bool ok, string why)
    {
        if (!ok)
        {
            // Komunikat opisuje WYMAGANIE, nigdy danych syntetycznych ani tokenu.
            throw new InvalidOperationException(why);
        }
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode code = HttpStatusCode.OK) =>
        new(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class SlowStream(CancellationTokenSource? caller) : System.IO.MemoryStream
    {
        public bool ReadStarted { get; private set; }

        public bool Disposed { get; private set; }

        public override bool CanSeek => false;

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            ReadStarted = true;
            caller?.Cancel();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }

        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> reply, bool attachRequest = true)
        : HttpMessageHandler
    {
        public int Count { get; private set; }

        public bool Disposed { get; private set; }

        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            ObjectDisposedException.ThrowIf(Disposed, this);
            Count++;
            var response = reply(request);
            // Natywny transport zapisuje RequestMessage; fixture odwzorowuje ten kontrakt.
            if (attachRequest) response.RequestMessage ??= request;
            return Task.FromResult(response);
        }
    }
}

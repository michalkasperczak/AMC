using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AccessibleMediaController.Core.Sonos;

// ZALADOWANIE ULUBIONEGO do kolejki grupy (F3a): POST /groups/{groupId}/favorites,
// operacja Favorites-LoadFavorite-GroupId z definicji OpenAPI 3.0.3
// "Sonos Control API (cloud)" v1.56.0-alpha.1-1-gc264f93f-production-cloud.
//
// Wylacznie syntetyczne dane i wlasny HttpMessageHandler: bez sieci, bez konta,
// bez DPAPI, bez GUI, bez presetow. Token i klucz w asercjach sa wymyslone.
internal static class SonosFavoriteLoadTests
{
    private const string Key = "00000000-0000-0000-0000-000000000042";
    private const string Token = "SYNTHETIC-ACCESS-ONLY";
    private const string Group = "RINCON_00012345678001400:0";
    private const string FavoritesPath =
        "https://api.ws.sonos.com/control/api/v1/groups/RINCON_00012345678001400:0/favorites";

    public static void Run()
    {
        var tests = new List<(string Name, Action Test)>();
        AddContractTests(tests);
        AddHappyPathTests(tests);
        AddInvalidInputTests(tests);
        AddTransportTests(tests);
        var failures = 0;
        foreach (var (name, test) in tests)
        {
            try { test(); Console.WriteLine("[OK] " + name); }
            catch (Exception ex) { failures++; Console.WriteLine("[FAIL] " + name + " (" + ex.GetType().Name + ")"); }
        }

        Console.WriteLine($"Sonos ladowanie ulubionego: {tests.Count - failures}/{tests.Count}");
        if (failures != 0)
        {
            throw new InvalidOperationException(
                "Sonos ladowanie ulubionego: " + failures + " nieudanych testow.");
        }
    }

    // --- 1. Kontrakt: enum akcji, sciezka, charakter polecenia ---
    private static void AddContractTests(List<(string Name, Action Test)> tests)
    {
        tests.Add(("action: PELNY enum queueAction z definicji, takze PLAY_NOW", () =>
        {
            Check(SonosFavoriteQueueActions.WireValue(SonosFavoriteQueueAction.Replace) == "REPLACE");
            Check(SonosFavoriteQueueActions.WireValue(SonosFavoriteQueueAction.Append) == "APPEND");
            Check(SonosFavoriteQueueActions.WireValue(SonosFavoriteQueueAction.Insert) == "INSERT");
            Check(SonosFavoriteQueueActions.WireValue(SonosFavoriteQueueAction.InsertNext) == "INSERT_NEXT");
            Check(SonosFavoriteQueueActions.WireValue(SonosFavoriteQueueAction.PlayNow) == "PLAY_NOW");
            // Dawny rekonesans pomijal PLAY_NOW; definicja go wymienia, wiec musi byc.
            Check(Enum.GetValues<SonosFavoriteQueueAction>().Length == 5);
            foreach (var action in Enum.GetValues<SonosFavoriteQueueAction>())
            {
                Check(SonosFavoriteQueueActions.IsDefined(action));
            }

            Check(!SonosFavoriteQueueActions.IsDefined((SonosFavoriteQueueAction)99));
        }));
        tests.Add(("sciezka polecenia to favorites, BEZ przedrostka playback", () =>
        {
            Check(SonosGroupCommands.PathSuffix(SonosGroupCommand.LoadFavorite) == "favorites");
            Check(!SonosGroupCommands.PathSuffix(SonosGroupCommand.LoadFavorite).Contains("playback"));
        }));
        tests.Add(("ladowanie ulubionego ZALEZY OD STANU: brak automatycznych ponowien", () =>
            Check(SonosGroupCommands.IsStateDependent(SonosGroupCommand.LoadFavorite))));
        tests.Add(("nowa wartosc enuma dopisana na KONCU, numeracja nietknieta", () =>
        {
            // Istniejace atrapy i asercje polegaja na wartosciach liczbowych.
            Check((int)SonosGroupCommand.Play == 0);
            Check((int)SonosGroupCommand.SetRelativeVolume == 9);
            Check((int)SonosGroupCommand.LoadFavorite == 10);
        }));
        tests.Add(("paramless SendGroupCommandAsync NADAL odmawia LoadFavorite bez ID", () =>
        {
            using var handler = new Handler(_ => Json("{}"));
            using var client = new SonosControlApiClient(SonosControlApiConfiguration.CreateDefault(Key), handler);
            var result = client.SendGroupCommandAsync(Token, Group, SonosGroupCommand.LoadFavorite,
                CancellationToken.None).GetAwaiter().GetResult();
            Check(result.Status == SonosControlApiStatus.InvalidConfiguration && !result.Sent);
            Check(result.Command == SonosGroupCommand.LoadFavorite && handler.Count == 0);
        }));
        tests.Add(("waski interfejs ladowania jest ODDZIELNY od odczytu ulubionych", () =>
        {
            using var client = new SonosControlApiClient(SonosControlApiConfiguration.CreateDefault(Key));
            Check(client is ISonosFavoriteLoadApi);
            // ISonosFavoritesApi zostaje odczytem: nie dostal metody zapisu.
            foreach (var method in typeof(ISonosFavoritesApi).GetMethods())
            {
                Check(!method.Name.Contains("Load", StringComparison.Ordinal));
            }

            var methods = typeof(ISonosFavoriteLoadApi).GetMethods();
            Check(methods.Length == 1 && methods[0].Name == "LoadFavoriteAsync");
            var parameters = methods[0].GetParameters();
            // action i playOnCompletion JAWNE: zadnego parametru domyslnego.
            foreach (var parameter in parameters)
            {
                Check(!parameter.HasDefaultValue);
            }

            Check(parameters[3].ParameterType == typeof(SonosFavoriteQueueAction));
            Check(parameters[4].ParameterType == typeof(bool));
        }));
    }

    // --- 2. Jeden POST: adres, cialo, naglowki, wynik ---
    private static void AddHappyPathTests(List<(string Name, Action Test)> tests)
    {
        tests.Add(("POST favorites: jeden adres, jawne action i playOnCompletion, BEZ playModes", () =>
        {
            foreach (var action in Enum.GetValues<SonosFavoriteQueueAction>())
            {
                foreach (var play in new[] { true, false })
                {
                    var body = CheckOnePost(client => client.LoadFavoriteAsync(
                        Token, Group, "fav-1", action, play, CancellationToken.None));
                    var root = ParseBody(body);
                    Check(Field(root, "favoriteId") == "fav-1");
                    Check(Field(root, "action") == SonosFavoriteQueueActions.WireValue(action));
                    Check(root.GetProperty("playOnCompletion").ValueKind
                        == (play ? JsonValueKind.True : JsonValueKind.False));
                    // playModes POMINIETE SWIADOMIE: brak pola zachowuje tryby
                    // gloshnika, a jawne false by je WYLACZYLO.
                    Check(!root.TryGetProperty("playModes", out _));
                    Check(!body.Contains("playModes", StringComparison.Ordinal));
                    Check(!body.Contains("shuffle", StringComparison.Ordinal));
                    Check(!body.Contains("volumeAtStart", StringComparison.Ordinal));
                    // Tylko trzy pola - nic wiecej transport nie dokleja.
                    var count = 0;
                    foreach (var _ in root.EnumerateObject()) count++;
                    Check(count == 3);
                }
            }
        }));
        tests.Add(("favoriteId idzie w CIELE, nie w sciezce adresu", () =>
        {
            using var handler = new Handler(request =>
            {
                Check(request.RequestUri!.AbsoluteUri == FavoritesPath);
                Check(!request.RequestUri.AbsoluteUri.Contains("fav-1", StringComparison.Ordinal));
                Check(request.RequestUri.Query.Length == 0);
                return Json("{}");
            });
            using var client = new SonosControlApiClient(SonosControlApiConfiguration.CreateDefault(Key), handler);
            Check(client.LoadFavoriteAsync(Token, Group, "fav-1", SonosFavoriteQueueAction.Append, false,
                CancellationToken.None).GetAwaiter().GetResult().Accepted);
            Check(handler.Count == 1);
        }));
        tests.Add(("identyfikatory wymagajace ucieczki w JSON przenoszone DOKLADNIE", () =>
        {
            // F1 przyjmuje te wartosci w SonosFavorite, wiec zapis musi je
            // przeniesc bez trimowania, obcinania i bez whitelisty ASCII.
            foreach (var id in new[]
            {
                "fav\"cudzyslow", "fav\\backslash", "ulubione Ą Ż ź", "fav z spacja",
                " ", "   ", "\t", "fav\nnowa-linia", "emoji \U0001F3B5", new string('a', 36)
            })
            {
                // Ta sama wartosc jest legalna w modelu F1 (poza whitespace-only,
                // ktory F1 tez przyjmuje - IsNullOrEmpty, nie IsNullOrWhiteSpace).
                var model = new SonosFavorite(id, "nazwa");
                Check(model.Id == id);
                var body = CheckOnePost(client => client.LoadFavoriteAsync(
                    Token, Group, id, SonosFavoriteQueueAction.Replace, true, CancellationToken.None));
                // Porownanie PO odkodowaniu JSON: liczy sie wartosc, nie zapis.
                Check(Field(ParseBody(body), "favoriteId") == id);
            }
        }));
        tests.Add(("obieg GET model -> Load: kazde ulubione z odczytu nadaje sie do zaladowania", () =>
        {
            const string favoritesBody = """
                {"version":"v-42","items":[
                  {"id":"fav \"A\" \\ Ż","name":"Radio"},
                  {"id":" ","name":"Spacja"},
                  {"id":"fav-2","name":"Playlista"}]}
                """;
            using var readHandler = new Handler(_ => Json(favoritesBody));
            using var readClient = new SonosControlApiClient(
                SonosControlApiConfiguration.CreateDefault(Key), readHandler);
            var favorites = readClient.GetFavoritesAsync(Token, "Sonos_household-1.2-3", CancellationToken.None)
                .GetAwaiter().GetResult();
            Check(favorites.Succeeded && favorites.Favorites!.Items.Count == 3);
            foreach (var favorite in favorites.Favorites!.Items)
            {
                var body = CheckOnePost(client => client.LoadFavoriteAsync(
                    Token, Group, favorite.Id, SonosFavoriteQueueAction.PlayNow, true, CancellationToken.None));
                Check(Field(ParseBody(body), "favoriteId") == favorite.Id);
            }
        }));
        tests.Add(("HTTP 200 {} to PRZYJECIE, nigdy potwierdzenie muzyki", () =>
        {
            using var handler = new Handler(_ => Json("{}"));
            using var client = new SonosControlApiClient(SonosControlApiConfiguration.CreateDefault(Key), handler);
            var result = client.LoadFavoriteAsync(Token, Group, "fav-1", SonosFavoriteQueueAction.Append, true,
                CancellationToken.None).GetAwaiter().GetResult();
            Check(result.Accepted && result.Sent && !result.EffectConfirmed && !result.EffectAmbiguous);
            Check(result.StateReadRecommended && result.Command == SonosGroupCommand.LoadFavorite);
            Check(handler.Count == 1);
            CheckNoSecret(result);
        }));
        tests.Add(("granica dlugosci favoriteId: 36 z definicji przechodzi, 37 nie", () =>
        {
            Check(SonosFavoritesLimits.MaxFavoriteIdLength == 36);
            var body = CheckOnePost(client => client.LoadFavoriteAsync(Token, Group,
                new string('z', SonosFavoritesLimits.MaxFavoriteIdLength),
                SonosFavoriteQueueAction.Insert, false, CancellationToken.None));
            Check(Field(ParseBody(body), "favoriteId")!.Length == SonosFavoritesLimits.MaxFavoriteIdLength);
            CheckNoHttp(client => client.LoadFavoriteAsync(Token, Group,
                new string('z', SonosFavoritesLimits.MaxFavoriteIdLength + 1),
                SonosFavoriteQueueAction.Insert, false, CancellationToken.None));
        }));
    }

    // --- 3. Zle wejscie: zero HTTP, zadnej naprawy danych ---
    private static void AddInvalidInputTests(List<(string Name, Action Test)> tests)
    {
        tests.Add(("zly favoriteId: null, pusty, za dlugi i samotny surogat - zero HTTP", () =>
        {
            foreach (var id in new string?[]
            {
                null, string.Empty, new string('a', 37),
                "\uD800", "przed\uD800po", "\uDC00", "para\uDC00zla"
            })
            {
                var local = id;
                // Samotny surogat odrzucamy PRZED serializatorem, ktory
                // podmienilby go na znak zastepczy i wyslal cos innego.
                CheckNoHttp(client => client.LoadFavoriteAsync(Token, Group, local,
                    SonosFavoriteQueueAction.Append, false, CancellationToken.None));
            }
        }));
        tests.Add(("nieznana wartosc action: zero HTTP, zadnego domyslnego APPEND", () =>
        {
            foreach (var action in new[] { (SonosFavoriteQueueAction)5, (SonosFavoriteQueueAction)(-1) })
            {
                var local = action;
                CheckNoHttp(client => client.LoadFavoriteAsync(Token, Group, "fav-1", local, true,
                    CancellationToken.None));
            }
        }));
        tests.Add(("zly groupId: zero HTTP", () =>
        {
            foreach (var groupId in new string?[] { null, string.Empty, "a/b", "..", new string('a', 36), "a b" })
            {
                var local = groupId;
                CheckNoHttp(client => client.LoadFavoriteAsync(Token, local, "fav-1",
                    SonosFavoriteQueueAction.Append, false, CancellationToken.None));
            }
        }));
        tests.Add(("wadliwy token i brak klucza integracji: zero HTTP", () =>
        {
            foreach (var token in new string?[] { null, string.Empty, "x\r\nInjected: yes", "ą", new string('a', 65537) })
            {
                var local = token;
                CheckNoHttp(client => client.LoadFavoriteAsync(local, Group, "fav-1",
                    SonosFavoriteQueueAction.Append, false, CancellationToken.None));
            }

            using var handler = new Handler(_ => Json("{}"));
            using var client = new SonosControlApiClient(SonosControlApiConfiguration.CreateDefault(null), handler);
            var result = client.LoadFavoriteAsync(Token, Group, "fav-1", SonosFavoriteQueueAction.Append, false,
                CancellationToken.None).GetAwaiter().GetResult();
            Check(result.Status == SonosControlApiStatus.InvalidConfiguration && !result.Sent && handler.Count == 0);
        }));
        tests.Add(("anulowanie PRZED wejsciem: zero HTTP i brak niejednoznacznosci", () =>
        {
            using var handler = new Handler(_ => Json("{}"));
            using var client = new SonosControlApiClient(SonosControlApiConfiguration.CreateDefault(Key), handler);
            var result = client.LoadFavoriteAsync(Token, Group, "fav-1", SonosFavoriteQueueAction.Append, false,
                new CancellationToken(true)).GetAwaiter().GetResult();
            Check(result.Status == SonosControlApiStatus.Canceled && !result.Sent && !result.EffectAmbiguous);
            Check(handler.Count == 0);
        }));
    }

    // --- 4. Transport: statusy, brak ponowien, brak echa tresci serwera ---
    private static void AddTransportTests(List<(string Name, Action Test)> tests)
    {
        foreach (var (code, expected) in new[]
        {
            (400, SonosControlApiStatus.RequestRejected), (401, SonosControlApiStatus.Unauthorized),
            (403, SonosControlApiStatus.Forbidden), (404, SonosControlApiStatus.NotFound),
            (429, SonosControlApiStatus.RateLimited), (499, SonosControlApiStatus.CommandFailed),
            (500, SonosControlApiStatus.ServiceError), (503, SonosControlApiStatus.ServiceError)
        })
        {
            var local = code;
            var localExpected = expected;
            tests.Add(("HTTP " + local + ": dokladnie jeden POST, bez echa tresci serwera", () =>
            {
                using var handler = new Handler(_ => Json(
                    "{\"errorCode\":\"" + Token + "\",\"reason\":\"" + Key + "\"}", (HttpStatusCode)local));
                using var client = new SonosControlApiClient(SonosControlApiConfiguration.CreateDefault(Key), handler);
                var result = client.LoadFavoriteAsync(Token, Group, "fav-1", SonosFavoriteQueueAction.Append, true,
                    CancellationToken.None).GetAwaiter().GetResult();
                Check(result.Status == localExpected && !result.Accepted);
                // ZERO ponowien: polecenie nie jest idempotentne.
                Check(handler.Count == 1);
                CheckNoSecret(result);
            }));
        }

        tests.Add(("obcy koncowy adres odpowiedzi odrzucony, brak wiadomosci zadania takze", () =>
        {
            foreach (var target in new[] { "https://attacker.invalid/", FavoritesPath + "/extra" })
            {
                using var handler = new Handler(_ =>
                {
                    var response = Json("{}");
                    response.RequestMessage = new HttpRequestMessage(HttpMethod.Post, target);
                    return response;
                });
                using var client = new SonosControlApiClient(SonosControlApiConfiguration.CreateDefault(Key), handler);
                var result = client.LoadFavoriteAsync(Token, Group, "fav-1", SonosFavoriteQueueAction.Append, true,
                    CancellationToken.None).GetAwaiter().GetResult();
                Check(result.Status == SonosControlApiStatus.RedirectRefused && result.Sent && handler.Count == 1);
            }

            using var bare = new Handler(_ => Json("{}"), attachRequest: false);
            using var bareClient = new SonosControlApiClient(SonosControlApiConfiguration.CreateDefault(Key), bare);
            Check(bareClient.LoadFavoriteAsync(Token, Group, "fav-1", SonosFavoriteQueueAction.Append, true,
                CancellationToken.None).GetAwaiter().GetResult().Status == SonosControlApiStatus.RedirectRefused);
        }));
        tests.Add(("zerwane polaczenie po wyslaniu: skutek NIEROZSTRZYGNIETY", () =>
        {
            using var handler = new Handler(_ => throw new HttpRequestException(Token));
            using var client = new SonosControlApiClient(SonosControlApiConfiguration.CreateDefault(Key), handler);
            var result = client.LoadFavoriteAsync(Token, Group, "fav-1", SonosFavoriteQueueAction.Replace, true,
                CancellationToken.None).GetAwaiter().GetResult();
            // Sent to PROBA przekazania zadania, nie dowod dostarczenia.
            Check(result.Status == SonosControlApiStatus.Unreachable && result.Sent);
            Check(result.EffectAmbiguous && !result.Accepted && result.StateReadRecommended);
            Check(handler.Count == 1);
            CheckNoSecret(result);
        }));
        tests.Add(("deadline z handlerem honorujacym token: jeden POST, skutek nieznany", () =>
        {
            using var handler = new Handler(request => throw new InvalidOperationException("nieuzywane"),
                asyncReply: async (request, ct) =>
                {
                    await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
                    return Json("{}");
                });
            using var client = new SonosControlApiClient(SonosControlApiConfiguration.CreateDefault(Key), handler,
                TimeSpan.FromMilliseconds(100));
            var result = client.LoadFavoriteAsync(Token, Group, "fav-1", SonosFavoriteQueueAction.Append, true,
                CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            Check(result.Status == SonosControlApiStatus.Unreachable && result.Sent && result.EffectAmbiguous);
            Check(handler.Count == 1);
        }));
        tests.Add(("anulowanie wolajacego PO wejsciu: jeden POST, oczekiwanie przerwane", () =>
        {
            using var caller = new CancellationTokenSource();
            using var handler = new Handler(request => throw new InvalidOperationException("nieuzywane"),
                asyncReply: async (request, ct) =>
                {
                    caller.Cancel();
                    await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
                    return Json("{}");
                });
            using var client = new SonosControlApiClient(SonosControlApiConfiguration.CreateDefault(Key), handler,
                TimeSpan.FromSeconds(5));
            var result = client.LoadFavoriteAsync(Token, Group, "fav-1", SonosFavoriteQueueAction.Append, true,
                caller.Token).WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            Check(result.Status == SonosControlApiStatus.Canceled && result.Sent);
            // Anulowano oczekiwanie NASZE; polecenie zalezne od stanu moglo sie wykonac.
            Check(result.EffectAmbiguous && handler.Count == 1);
            CheckNoSecret(result);
        }));
        tests.Add(("odpowiedz 200 bez JSON nie wprowadza nowego wymogu", () =>
        {
            // Zastany WriteAsync NIE parsuje ciala POST - sprawdza status i adres.
            // Ten test utrwala istniejaca regule, nie dodaje nowej.
            using var handler = new Handler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent("nie-json", Encoding.UTF8, "text/html") });
            using var client = new SonosControlApiClient(SonosControlApiConfiguration.CreateDefault(Key), handler);
            var result = client.LoadFavoriteAsync(Token, Group, "fav-1", SonosFavoriteQueueAction.Append, true,
                CancellationToken.None).GetAwaiter().GetResult();
            Check(result.Accepted && result.Sent && handler.Count == 1);
        }));
        tests.Add(("wlasny HttpClient nie powstaje: handler wolajacego obsluguje ladowanie", () =>
        {
            using var handler = new Handler(_ => Json("{}"));
            using (var first = new SonosControlApiClient(SonosControlApiConfiguration.CreateDefault(Key), handler))
            {
                Check(first.LoadFavoriteAsync(Token, Group, "fav-1", SonosFavoriteQueueAction.Append, false,
                    CancellationToken.None).GetAwaiter().GetResult().Accepted);
            }

            Check(!handler.Disposed);
            using var second = new SonosControlApiClient(SonosControlApiConfiguration.CreateDefault(Key), handler);
            Check(second.LoadFavoriteAsync(Token, Group, "fav-1", SonosFavoriteQueueAction.Append, false,
                CancellationToken.None).GetAwaiter().GetResult().Accepted);
            Check(handler.Count == 2);
        }));
    }

    // --- pomocnicze ---

    // Jeden POST na docelowy adres z naglowkami NA ZADANIU; zwraca cialo zadania.
    private static string CheckOnePost(Func<SonosControlApiClient, Task<SonosGroupCommandOutcome>> call)
    {
        string? captured = null;
        using var handler = new Handler(request =>
        {
            Check(request.Method == HttpMethod.Post);
            Check(request.RequestUri!.AbsoluteUri == FavoritesPath);
            Check(request.Headers.Authorization?.Scheme == "Bearer");
            Check(request.Headers.Authorization?.Parameter == Token);
            Check(request.Headers.TryGetValues("X-Sonos-Api-Key", out var keys) && string.Join(",", keys) == Key);
            Check(!request.Headers.Contains("X-Sonos-Playback-Session-Id"));
            Check(request.Content!.Headers.ContentType!.MediaType == "application/json");
            Check(request.Content.Headers.ContentType.CharSet?.Trim('"').ToLowerInvariant() == "utf-8");
            captured = request.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            return Json("{}");
        });
        using var client = new SonosControlApiClient(SonosControlApiConfiguration.CreateDefault(Key), handler);
        var result = call(client).GetAwaiter().GetResult();
        Check(result.Accepted && result.Sent && !result.EffectConfirmed);
        Check(result.Command == SonosGroupCommand.LoadFavorite);
        Check(handler.Count == 1);
        Check(captured is not null);
        return captured!;
    }

    private static void CheckNoHttp(Func<SonosControlApiClient, Task<SonosGroupCommandOutcome>> call)
    {
        using var handler = new Handler(_ => Json("{}"));
        using var client = new SonosControlApiClient(SonosControlApiConfiguration.CreateDefault(Key), handler);
        var result = call(client).GetAwaiter().GetResult();
        Check(result.Status == SonosControlApiStatus.InvalidConfiguration && !result.Sent);
        Check(result.Command == SonosGroupCommand.LoadFavorite && !result.EffectAmbiguous);
        Check(handler.Count == 0);
    }

    // Wynik nie przenosi tokenu, klucza ani tresci odpowiedzi serwera.
    private static void CheckNoSecret(SonosGroupCommandOutcome result)
    {
        foreach (var text in new[] { result.Message, result.ToString() })
        {
            Check(!text.Contains(Token, StringComparison.Ordinal));
            Check(!text.Contains(Key, StringComparison.Ordinal));
            Check(!text.Contains("errorCode", StringComparison.OrdinalIgnoreCase));
            Check(!text.Contains("reason", StringComparison.OrdinalIgnoreCase));
            Check(!text.Contains("fav-1", StringComparison.Ordinal));
        }
    }

    // Scisla serializacja: cialo musi byc poprawnym JSON, a wartosci czytamy PO
    // odkodowaniu - inaczej test przyjalby recznie sklejony, zepsuty napis.
    private static JsonElement ParseBody(string body)
    {
        var document = JsonDocument.Parse(body);
        Check(document.RootElement.ValueKind == JsonValueKind.Object);
        return document.RootElement.Clone();
    }

    private static string? Field(JsonElement root, string name)
    {
        Check(root.TryGetProperty(name, out var value));
        Check(value.ValueKind == JsonValueKind.String);
        return value.GetString();
    }

    private static void Check(bool ok)
    {
        if (!ok)
        {
            throw new InvalidOperationException("Niespelniona asercja (dane syntetyczne ukryte).");
        }
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode code = HttpStatusCode.OK) =>
        new(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class Handler(
        Func<HttpRequestMessage, HttpResponseMessage> reply,
        bool attachRequest = true,
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? asyncReply = null)
        : HttpMessageHandler
    {
        public int Count { get; private set; }

        public bool Disposed { get; private set; }

        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            ObjectDisposedException.ThrowIf(Disposed, this);
            Count++;
            // Handler HONORUJE token anulowania - inaczej deadline mierzylby atrape.
            var response = asyncReply is null
                ? reply(request)
                : await asyncReply(request, cancellationToken).ConfigureAwait(false);
            // Natywny transport zapisuje RequestMessage; fixture odwzorowuje ten kontrakt.
            if (attachRequest) response.RequestMessage ??= request;
            return response;
        }
    }
}

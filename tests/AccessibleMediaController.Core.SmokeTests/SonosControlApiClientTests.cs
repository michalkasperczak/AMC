using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AccessibleMediaController.Core.Sonos;

// Wylacznie syntetyczne dane i HttpMessageHandler; bez sieci, kont i GUI.
internal static class SonosControlApiClientTests
{
    private const string Key = "00000000-0000-0000-0000-000000000042";
    private const string Token = "SYNTHETIC-ACCESS-ONLY";
    public static void Run()
    {
        var tests = new List<(string Name, Action Test)>
        {
            ("GET households: metoda, adres, naglowki, dane", Households),
            ("GET households: brak nazwy i pusta lista", EmptyAndUnnamed),
            ("origin produkcyjny", () => Check(SonosControlApiConfiguration.CreateDefault(Key).HouseholdsUri.AbsoluteUri == "https://api.ws.sonos.com/control/api/v1/households")),
            ("obcy host zabroniony", () => Check(!SonosControlApiConfiguration.TryCreate("https://attacker.invalid/control/api/v1/", Key, out _))),
            ("HTTP zabronione", () => Check(!SonosControlApiConfiguration.TryCreate("http://api.ws.sonos.com/control/api/v1/", Key, out _))),
            ("userinfo zabronione", () => Check(!SonosControlApiConfiguration.TryCreate("https://user@api.ws.sonos.com/control/api/v1/", Key, out _))),
            ("query zabronione", () => Check(!SonosControlApiConfiguration.TryCreate("https://api.ws.sonos.com/control/api/v1/?x=1", Key, out _))),
            ("identyfikator z kropka zachowany", () => Check(SonosHouseholdIdPolicy.TryEncode("Sonos_abc-123.def", out var id) && id == "Sonos_abc-123.def")),
            ("traversal zabroniony", () => Check(!SonosHouseholdIdPolicy.IsAcceptable(".."))),
            ("slash zabroniony", () => Check(!SonosHouseholdIdPolicy.IsAcceptable("a/b")))
        };
        AddTransportTests(tests);
        AddSafetyTests(tests);
        int failures = 0;
        foreach (var (name, test) in tests)
        {
            try { test(); Console.WriteLine("[OK] " + name); }
            catch (Exception ex) { failures++; Console.WriteLine("[FAIL] " + name + " (" + ex.GetType().Name + ")"); }
        }
        Console.WriteLine($"Sonos Control API: {tests.Count - failures}/{tests.Count}");
        if (failures != 0) throw new InvalidOperationException("Sonos Control API: " + failures + " nieudanych testow.");
    }

    private static void Households()
    {
        using var handler = new Handler(request =>
        {
            Check(request.Method == HttpMethod.Get && request.Content is null);
            Check(request.RequestUri!.AbsoluteUri == "https://api.ws.sonos.com/control/api/v1/households");
            Check(request.Headers.Authorization?.Scheme == "Bearer" && request.Headers.Authorization.Parameter == Token);
            Check(string.Join("", request.Headers.GetValues("X-Sonos-Api-Key")) == Key);
            Check(request.Headers.Accept.ToString() == "application/json");
            return Json("{\"households\":[{\"id\":\"Sonos_1.a\",\"name\":\"Dom\",\"swVersion\":\"90.1\"},{\"id\":\"Sonos_2\"}]}");
        });
        using var client = new SonosControlApiClient(SonosControlApiConfiguration.CreateDefault(Key), handler);
        var result = client.GetHouseholdsAsync(Token, CancellationToken.None).GetAwaiter().GetResult();
        Check(result.Succeeded && result.Households.Count == 2 && handler.Count == 1);
        Check(result.Households[0].Id == "Sonos_1.a" && result.Households[0].Name == "Dom" && result.Households[0].SoftwareVersion == "90.1");
        Check(result.Households[1].Name is null);
    }

    private static void EmptyAndUnnamed()
    {
        foreach (var body in new[] { "{}", "{\"households\":null}", "{\"households\":[]}" })
        {
            using var handler = new Handler(_ => Json(body));
            using var client = new SonosControlApiClient(SonosControlApiConfiguration.CreateDefault(Key), handler);
            var result = client.GetHouseholdsAsync(Token, CancellationToken.None).GetAwaiter().GetResult();
            Check(result.Succeeded && result.Households.Count == 0);
        }
    }

    private static void AddTransportTests(List<(string Name, Action Test)> tests)
    {
        tests.Add(("GET groups: pelne dane, partial i bez mutacji", Groups));
        tests.Add(("GET groups: pusty dom", () =>
        {
            var result = ReadGroups("{\"groups\":null,\"players\":[],\"partial\":null}");
            Check(result.Succeeded && result.Topology!.IsEmpty && !result.Topology.Partial);
        }));
        tests.Add(("GET groups: nieznany stan nie gubi urzadzen", () =>
        {
            var result = ReadGroups(GroupBody.Replace("PLAYBACK_STATE_PLAYING", "PLAYBACK_STATE_FUTURE"));
            Check(result.Succeeded && result.Topology!.Groups[0].PlaybackState == SonosPlaybackState.Unknown);
        }));
    }

    private const string GroupBody = """
        {"groups":[{"id":"g1","name":"Salon + 1","coordinatorId":"p1","playerIds":["p1","p2"],"playbackState":"PLAYBACK_STATE_PLAYING"}],
         "players":[{"id":"p1","name":"Salon","softwareVersion":"90.1","apiVersion":"1.56","minApiVersion":"1.0"},{"id":"p2","name":"Kuchnia"}],"partial":true}
        """;

    private static SonosGroupsOutcome ReadGroups(string body)
    {
        using var handler = new Handler(_ => Json(body));
        using var client = new SonosControlApiClient(SonosControlApiConfiguration.CreateDefault(Key), handler);
        return client.GetGroupsAsync(Token, "Sonos_1.a", CancellationToken.None).GetAwaiter().GetResult();
    }

    private static void Groups()
    {
        using var handler = new Handler(request =>
        {
            Check(request.Method == HttpMethod.Get && request.Content is null);
            Check(request.RequestUri!.AbsoluteUri == "https://api.ws.sonos.com/control/api/v1/households/Sonos_1.a/groups");
            Check(request.Headers.Authorization?.Parameter == Token);
            return Json(GroupBody);
        });
        using var client = new SonosControlApiClient(SonosControlApiConfiguration.CreateDefault(Key), handler);
        var result = client.GetGroupsAsync(Token, "Sonos_1.a", CancellationToken.None).GetAwaiter().GetResult();
        Check(result.Succeeded && handler.Count == 1);
        var topology = result.Topology!;
        Check(topology.Partial && topology.Groups.Count == 1 && topology.Players.Count == 2);
        var group = topology.Groups[0];
        Check(group.Id == "g1" && group.Name == "Salon + 1" && group.CoordinatorId == "p1"
            && group.PlayerIds.Count == 2 && group.PlayerIds[1] == "p2" && group.CoordinatorListed
            && group.PlaybackState == SonosPlaybackState.Playing);
        Check(topology.Players[0].ApiVersion == "1.56" && topology.Players[0].MinApiVersion == "1.0"
            && topology.Players[1].Name == "Kuchnia");
    }
    private static void AddSafetyTests(List<(string Name, Action Test)> tests)
    {
        foreach (var (code, expected) in new[] {
            (400, SonosControlApiStatus.RequestRejected), (401, SonosControlApiStatus.Unauthorized),
            (403, SonosControlApiStatus.Forbidden), (404, SonosControlApiStatus.NotFound),
            (429, SonosControlApiStatus.RateLimited), (499, SonosControlApiStatus.CommandFailed),
            (500, SonosControlApiStatus.ServiceError), (503, SonosControlApiStatus.ServiceError),
            (302, SonosControlApiStatus.RedirectRefused), (307, SonosControlApiStatus.RedirectRefused),
            (204, SonosControlApiStatus.InvalidResponse) })
            tests.Add(("HTTP " + code + ": bez echa, bez ponowienia", () =>
                CheckResponse(_ => Json(Token, (HttpStatusCode)code), expected)));
        foreach (var body in new[] { "[]", "null", "{", "{\"households\":42}",
            "{\"households\":[{}]}", "{\"households\":[{\"id\":null}]}",
            "{\"households\":[{\"id\":42}]}", "{\"households\":[{\"id\":\"a\",\"name\":42}]}",
            "{\"households\":[],\"households\":[]}",
            "{\"households\":[{\"id\":\"a\"},{\"id\":\"b\"},{\"id\":\"c\"},{\"id\":\"d\"},{\"id\":\"e\"}]}" })
        {
            var index = tests.Count;
            tests.Add(("wadliwy JSON domow " + index, () => CheckResponse(_ => Json(body), SonosControlApiStatus.InvalidResponse)));
        }
        foreach (var body in new[] { "[]", "{\"partial\":\"true\"}", "{\"groups\":[{}]}",
            "{\"players\":[null]}", "{\"groups\":42}",
            GroupBody.Replace("\"playerIds\":[\"p1\",\"p2\"]", "\"playerIds\":[null]"),
            GroupBody.Replace("\"coordinatorId\":\"p1\"", "\"coordinatorId\":null") })
        {
            var index = tests.Count;
            tests.Add(("wadliwy JSON grup " + index, () => Check(ReadGroups(body).Status == SonosControlApiStatus.InvalidResponse)));
        }
        tests.Add(("obcy koncowy adres", () => CheckResponse(_ =>
        {
            var response = Json("{}");
            response.RequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://attacker.invalid/");
            return response;
        }, SonosControlApiStatus.RedirectRefused)));
        tests.Add(("inna sciezka odpowiedzi", () => CheckResponse(_ =>
        {
            var response = Json("{}");
            response.RequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://api.ws.sonos.com/control/api/v1/players");
            return response;
        }, SonosControlApiStatus.RedirectRefused)));
        tests.Add(("nie JSON Content-Type", () => CheckResponse(_ => new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent("{}", Encoding.UTF8, "text/html") }, SonosControlApiStatus.InvalidResponse)));
        tests.Add(("nie UTF8 charset", () => CheckResponse(_ =>
        {
            var response = Json("{}"); response.Content.Headers.ContentType!.CharSet = "utf-16"; return response;
        }, SonosControlApiStatus.InvalidResponse)));
        tests.Add(("wadliwe bajty UTF8", () => CheckResponse(_ =>
        {
            var response = Json("{}"); response.Content = new ByteArrayContent(new byte[] { 0xff, 0xfe });
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            return response;
        }, SonosControlApiStatus.InvalidResponse)));
        tests.Add(("przekroczony limit bez Content-Length", () => CheckResponse(_ =>
        {
            var response = Json("{}");
            response.Content = new StreamContent(new UnknownLengthStream(Encoding.UTF8.GetBytes("{}" + new string(' ', SonosControlApiClient.MaxResponseBytes))));
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            return response;
        }, SonosControlApiStatus.InvalidResponse)));
        tests.Add(("duplikaty pozycji nie sa cicho usuwane", () =>
        {
            var result = ReadGroups(GroupBody.Replace("[\"p1\",\"p2\"]", "[\"p1\",\"p1\"]"));
            Check(result.Succeeded && result.Topology!.Groups[0].PlayerIds.Count == 2);
        }));
        foreach (var token in new string?[] { null, "", "has space", "x\r\nInjected: yes", "ą", new string('a', 65537) })
        {
            var index = tests.Count;
            tests.Add(("wadliwy token, zero HTTP " + index, () =>
            {
                using var handler = new Handler(_ => Json("{}"));
                using var client = new SonosControlApiClient(SonosControlApiConfiguration.CreateDefault(Key), handler);
                var result = client.GetHouseholdsAsync(token, CancellationToken.None).GetAwaiter().GetResult();
                Check(result.Status == SonosControlApiStatus.InvalidConfiguration && handler.Count == 0);
            }));
        }
        foreach (var key in new string?[] { null, "", "x\r\nInjected: yes", " key ", "ą" })
        {
            var index = tests.Count;
            tests.Add(("wadliwy klucz, zero HTTP " + index, () =>
            {
                using var handler = new Handler(_ => Json("{}"));
                using var client = new SonosControlApiClient(SonosControlApiConfiguration.CreateDefault(key), handler);
                var result = client.GetHouseholdsAsync(Token, CancellationToken.None).GetAwaiter().GetResult();
                Check(result.Status == SonosControlApiStatus.InvalidConfiguration && handler.Count == 0);
            }));
        }
        tests.Add(("anulowanie przed wywolaniem, zero HTTP", () =>
        {
            using var handler = new Handler(_ => Json("{}"));
            using var client = new SonosControlApiClient(SonosControlApiConfiguration.CreateDefault(Key), handler);
            var result = client.GetHouseholdsAsync(Token, new CancellationToken(true)).GetAwaiter().GetResult();
            Check(result.Status == SonosControlApiStatus.Canceled && handler.Count == 0);
        }));
        tests.Add(("deadline obejmuje czytanie ciala", () => SlowBody(false)));
        tests.Add(("anulowanie podczas czytania ciala", () => SlowBody(true)));
        tests.Add(("awaria sieci bez szczegolow bledu", () => CheckResponse(_ => throw new HttpRequestException(Token), SonosControlApiStatus.Unreachable)));
        tests.Add(("brak calej wiadomosci zadania odrzucony", () =>
        {
            using var handler = new Handler(_ => Json("{}"), attachRequest: false);
            using var client = new SonosControlApiClient(SonosControlApiConfiguration.CreateDefault(Key), handler);
            var result = client.GetHouseholdsAsync(Token, CancellationToken.None).GetAwaiter().GetResult();
            Check(result.Status == SonosControlApiStatus.RedirectRefused && handler.Count == 1);
        }));
        tests.Add(("brak adresu w odpowiedzi transportu odrzucony", () => CheckResponse(_ =>
        {
            var response = Json("{}");
            response.RequestMessage = new HttpRequestMessage();
            return response;
        }, SonosControlApiStatus.RedirectRefused)));
        tests.Add(("granice nazwy glosnika z definicji Sonos: 64/65", () =>
        {
            var allowed = ReadGroups(GroupBody.Replace("\"Salon\"", "\"" + new string('a', 64) + "\""));
            var rejected = ReadGroups(GroupBody.Replace("\"Salon\"", "\"" + new string('a', 65) + "\""));
            Check(allowed.Succeeded && allowed.Topology!.Players[0].Name.Length == 64);
            Check(rejected.Status == SonosControlApiStatus.InvalidResponse);
        }));
        tests.Add(("minApiVersion z definicji Sonos: 16/17", () =>
        {
            var allowed = ReadGroups(GroupBody.Replace("\"minApiVersion\":\"1.0\"", "\"minApiVersion\":\"" + new string('a', 16) + "\""));
            var rejected = ReadGroups(GroupBody.Replace("\"minApiVersion\":\"1.0\"", "\"minApiVersion\":\"" + new string('a', 17) + "\""));
            Check(allowed.Succeeded && allowed.Topology!.Players[0].MinApiVersion!.Length == 16);
            Check(rejected.Status == SonosControlApiStatus.InvalidResponse);
        }));
        tests.Add(("handler wolajacego pozostaje zywy po Dispose klienta", () =>
        {
            using var handler = new Handler(_ => Json("{}"));
            using (var first = new SonosControlApiClient(SonosControlApiConfiguration.CreateDefault(Key), handler))
                Check(first.GetHouseholdsAsync(Token, CancellationToken.None).GetAwaiter().GetResult().Succeeded);
            Check(!handler.Disposed);
            using var second = new SonosControlApiClient(SonosControlApiConfiguration.CreateDefault(Key), handler);
            Check(second.GetHouseholdsAsync(Token, CancellationToken.None).GetAwaiter().GetResult().Succeeded);
            Check(handler.Count == 2);
        }));
        tests.Add(("niezmiennosc modeli", () =>
        {
            var ids = new List<string> { "p1" };
            var group = new SonosGroup("g1", "Salon", "p1", ids, SonosPlaybackState.Idle);
            ids.Clear(); Check(group.PlayerIds.Count == 1);
            var groups = new List<SonosGroup> { group };
            var topology = new SonosHouseholdTopology(groups, new List<SonosPlayer>(), false);
            groups.Clear(); Check(topology.Groups.Count == 1);
        }));
    }

    private static void CheckResponse(Func<HttpRequestMessage, HttpResponseMessage> response, SonosControlApiStatus expected)
    {
        using var handler = new Handler(response);
        using var client = new SonosControlApiClient(SonosControlApiConfiguration.CreateDefault(Key), handler);
        var result = client.GetHouseholdsAsync(Token, CancellationToken.None).GetAwaiter().GetResult();
        Check(result.Status == expected && handler.Count == 1);
        Check(!result.Message.Contains(Token) && !result.ToString().Contains(Token) && !result.ToString().Contains(Key));
    }

    private static void SlowBody(bool callerCancels)
    {
        using var caller = new CancellationTokenSource();
        using var stream = new SlowStream(callerCancels ? caller : null);
        using var handler = new Handler(_ =>
        {
            var response = Json("{}"); response.Content = new StreamContent(stream);
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            return response;
        });
        using var client = new SonosControlApiClient(SonosControlApiConfiguration.CreateDefault(Key), handler,
            TimeSpan.FromMilliseconds(callerCancels ? 5000 : 100));
        var result = client.GetHouseholdsAsync(Token, caller.Token).WaitAsync(TimeSpan.FromSeconds(3)).GetAwaiter().GetResult();
        Check(result.Status == (callerCancels ? SonosControlApiStatus.Canceled : SonosControlApiStatus.Unreachable));
        Check(stream.ReadStarted && stream.Disposed && handler.Count == 1);
    }

    private sealed class UnknownLengthStream(byte[] bytes) : System.IO.MemoryStream(bytes)
    { public override bool CanSeek => false; }

    private sealed class SlowStream(CancellationTokenSource? caller) : System.IO.MemoryStream
    {
        public bool ReadStarted { get; private set; }
        public bool Disposed { get; private set; }
        public override bool CanSeek => false;
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            ReadStarted = true;
            caller?.Cancel();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }

    private static void Check(bool ok) { if (!ok) throw new InvalidOperationException("Niespelniona asercja (dane syntetyczne ukryte)."); }
    private static HttpResponseMessage Json(string body, HttpStatusCode code = HttpStatusCode.OK) => new(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> reply, bool attachRequest = true) : HttpMessageHandler
    {
        public int Count { get; private set; }
        public bool Disposed { get; private set; }
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
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

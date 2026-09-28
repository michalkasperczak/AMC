using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AccessibleMediaController.Core.Sonos;

// Odczyt stanu, metadanych i glosnosci GRUPY oraz podstawowe polecenia grupy.
// Wylacznie syntetyczne dane i wlasny HttpMessageHandler: bez sieci, bez konta,
// bez DPAPI, bez GUI. Token i klucz w asercjach sa wymyslone.
internal static class SonosGroupPlaybackTests
{
    private const string Key = "00000000-0000-0000-0000-000000000042";
    private const string Token = "SYNTHETIC-ACCESS-ONLY";
    private const string Group = "RINCON_00012345678001400:0";
    private const string GroupPath = "https://api.ws.sonos.com/control/api/v1/groups/RINCON_00012345678001400:0/";

    private const string PlaybackBody = """
        {"playbackState":"PLAYBACK_STATE_PLAYING","isDucking":false,"queueVersion":"q7",
         "itemId":"item-1","positionMillis":12345,"previousItemId":"item-0","previousPositionMillis":5,
         "playModes":{"repeat":false,"repeatOne":null,"shuffle":true,"crossfade":false},
         "availablePlaybackActions":{"canPlay":true,"canSkip":true,"canSkipToPrevious":true,
           "canSeek":true,"canPause":true,"canStop":false,"canRepeat":true,"canRepeatOne":false,
           "canCrossfade":true,"canShuffle":true}}
        """;

    private const string MetadataBody = """
        {"container":{"name":"Moja playlista","type":"playlist","service":{"name":"Serwis","id":"9"}},
         "currentItem":{"id":"item-1","track":{"type":"track","name":"Utwór","durationMillis":215000,
           "artist":{"name":"Wykonawca"},"album":{"name":"Album","artist":{"name":"Wykonawca albumu"}},
           "service":{"name":"Serwis","id":"9"}}},
         "nextItem":{"id":"item-2","track":{"name":"Następny"}},
         "streamInfo":null}
        """;

    public static void Run()
    {
        var tests = new List<(string Name, Action Test)>();
        AddPolicyTests(tests);
        AddReadTests(tests);
        AddCommandTests(tests);
        AddCommandMessageTests(tests);
        AddSafetyTests(tests);
        var failures = 0;
        foreach (var (name, test) in tests)
        {
            try { test(); Console.WriteLine("[OK] " + name); }
            catch (Exception ex) { failures++; Console.WriteLine("[FAIL] " + name + " (" + ex.GetType().Name + ")"); }
        }

        Console.WriteLine($"Sonos odtwarzanie grupy: {tests.Count - failures}/{tests.Count}");
        if (failures != 0)
        {
            throw new InvalidOperationException("Sonos odtwarzanie grupy: " + failures + " nieudanych testow.");
        }
    }

    private static void AddPolicyTests(List<(string Name, Action Test)> tests)
    {
        tests.Add(("groupId z dwukropkiem zachowany literalnie", () =>
            Check(SonosGroupIdPolicy.TryEncode(Group, out var id) && id == Group)));
        tests.Add(("groupId: slash, procent i traversal zabronione", () =>
        {
            Check(!SonosGroupIdPolicy.IsAcceptable("a/b"));
            Check(!SonosGroupIdPolicy.IsAcceptable("a%2Fb"));
            Check(!SonosGroupIdPolicy.IsAcceptable(".."));
            Check(!SonosGroupIdPolicy.IsAcceptable("a@b"));
            Check(!SonosGroupIdPolicy.IsAcceptable("a?b"));
            Check(!SonosGroupIdPolicy.IsAcceptable("a b"));
            Check(!SonosGroupIdPolicy.IsAcceptable(null));
            Check(!SonosGroupIdPolicy.IsAcceptable(string.Empty));
        }));
        tests.Add(("groupId: granica dlugosci 35/36 z definicji", () =>
        {
            Check(SonosGroupIdPolicy.IsAcceptable(new string('a', 35)));
            Check(!SonosGroupIdPolicy.IsAcceptable(new string('a', 36)));
        }));
        tests.Add(("sciezki polecen wprost z definicji", () =>
        {
            Check(SonosGroupCommands.PathSuffix(SonosGroupCommand.Play) == "playback/play");
            Check(SonosGroupCommands.PathSuffix(SonosGroupCommand.Pause) == "playback/pause");
            Check(SonosGroupCommands.PathSuffix(SonosGroupCommand.TogglePlayPause) == "playback/togglePlayPause");
            Check(SonosGroupCommands.PathSuffix(SonosGroupCommand.SkipToNextTrack) == "playback/skipToNextTrack");
            Check(SonosGroupCommands.PathSuffix(SonosGroupCommand.SkipToPreviousTrack) == "playback/skipToPreviousTrack");
            Check(SonosGroupCommands.PathSuffix(SonosGroupCommand.Seek) == "playback/seek");
            Check(SonosGroupCommands.PathSuffix(SonosGroupCommand.SeekRelative) == "playback/seekRelative");
            Check(SonosGroupCommands.PathSuffix(SonosGroupCommand.SetVolume) == "groupVolume");
            Check(SonosGroupCommands.PathSuffix(SonosGroupCommand.SetMute) == "groupVolume/mute");
            Check(SonosGroupCommands.PathSuffix(SonosGroupCommand.SetRelativeVolume) == "groupVolume/relative");
        }));
        tests.Add(("polecenia przelaczajace i wzgledne oznaczone", () =>
        {
            foreach (var command in new[] { SonosGroupCommand.TogglePlayPause, SonosGroupCommand.SkipToNextTrack,
                SonosGroupCommand.SkipToPreviousTrack, SonosGroupCommand.SeekRelative, SonosGroupCommand.SetRelativeVolume })
            {
                Check(SonosGroupCommands.IsStateDependent(command));
            }

            foreach (var command in new[] { SonosGroupCommand.Play, SonosGroupCommand.Pause,
                SonosGroupCommand.Seek, SonosGroupCommand.SetVolume, SonosGroupCommand.SetMute })
            {
                Check(!SonosGroupCommands.IsStateDependent(command));
            }
        }));
        tests.Add(("canSkipBack przeterminowane, canSkipToPrevious ma pierwszenstwo", () =>
        {
            var nowe = new SonosPlaybackActions(null, null, false, true, null, null, null, null, null, null, null);
            var stare = new SonosPlaybackActions(null, null, true, null, null, null, null, null, null, null, null);
            var brak = new SonosPlaybackActions(null, null, null, null, null, null, null, null, null, null, null);
            Check(nowe.SkipToPreviousAllowed == true);
            Check(stare.SkipToPreviousAllowed == true);
            Check(brak.SkipToPreviousAllowed is null);
        }));
        tests.Add(("glosnosc poza 0..100 odrzucona w modelu", () =>
        {
            CheckThrows(() => new SonosGroupVolume(-1, null, null));
            CheckThrows(() => new SonosGroupVolume(101, null, null));
            Check(new SonosGroupVolume(0, null, null).Volume == 0);
            Check(new SonosGroupVolume(100, null, null).Volume == 100);
        }));
    }

    private static void AddReadTests(List<(string Name, Action Test)> tests)
    {
        tests.Add(("GET playback: adres, metoda, naglowki i pelne dane", Playback));
        tests.Add(("GET playback: wymagany tylko playbackState", () =>
        {
            var result = ReadPlayback("{\"playbackState\":\"PLAYBACK_STATE_IDLE\"}");
            Check(result.Succeeded);
            var playback = result.Playback!;
            Check(playback.PlaybackState == SonosPlaybackState.Idle);
            Check(playback.PositionMillis is null && !playback.HasPosition);
            Check(playback.ItemId is null && playback.QueueVersion is null && playback.IsDucking is null);
            Check(playback.PlayModes is null && playback.AvailablePlaybackActions is null);
        }));
        tests.Add(("GET playback: brak playbackState to niezgodna odpowiedz", () =>
            Check(ReadPlayback("{\"positionMillis\":10}").Status == SonosControlApiStatus.InvalidResponse)));
        tests.Add(("GET playback: nieznany stan nie gubi odpowiedzi", () =>
        {
            var result = ReadPlayback("{\"playbackState\":\"PLAYBACK_STATE_FUTURE\",\"positionMillis\":0}");
            Check(result.Succeeded);
            var playback = result.Playback!;
            Check(playback.PlaybackState == SonosPlaybackState.Unknown);
            Check(playback.PositionMillis == 0 && playback.HasPosition);
        }));
        tests.Add(("GET playback: nieznana pozycja to nie zero", () =>
        {
            var brak = ReadPlayback("{\"playbackState\":\"PLAYBACK_STATE_PLAYING\"}").Playback!;
            var zero = ReadPlayback("{\"playbackState\":\"PLAYBACK_STATE_PLAYING\",\"positionMillis\":0}").Playback!;
            var jawnyNull = ReadPlayback("{\"playbackState\":\"PLAYBACK_STATE_PLAYING\",\"positionMillis\":null}").Playback!;
            Check(brak.PositionMillis is null && jawnyNull.PositionMillis is null && zero.PositionMillis == 0);
        }));
        tests.Add(("GET playback: brak akcji to nie same falsze", () =>
        {
            var result = ReadPlayback("{\"playbackState\":\"PLAYBACK_STATE_PLAYING\",\"availablePlaybackActions\":{\"canSkip\":false}}");
            var actions = result.Playback!.AvailablePlaybackActions!;
            Check(actions.CanSkip == false && actions.CanPause is null && actions.CanSeek is null);
            Check(actions.SkipToPreviousAllowed is null);
        }));
        tests.Add(("GET playbackMetadata: adres i pelne dane", Metadata));
        tests.Add(("GET playbackMetadata: radio bez currentItem jest legalne", () =>
        {
            var result = ReadMetadata("{\"container\":{\"name\":\"Radio\",\"type\":\"station\"},\"streamInfo\":\"Teraz gra\"}");
            Check(result.Succeeded);
            var metadata = result.Metadata!;
            Check(metadata.CurrentItem is null && metadata.CurrentTrack is null);
            Check(metadata.Container!.IsStation && metadata.StreamInfo == "Teraz gra" && !metadata.IsEmpty);
        }));
        tests.Add(("GET playbackMetadata: pusty gloshnik jest legalny", () =>
        {
            foreach (var body in new[] { "{}", "{\"container\":null,\"currentItem\":null,\"nextItem\":null}" })
            {
                var result = ReadMetadata(body);
                Check(result.Succeeded && result.Metadata!.IsEmpty && result.Metadata.CurrentItem is null);
            }
        }));
        tests.Add(("GET playbackMetadata: brak dlugosci utworu to nie zero", () =>
        {
            var result = ReadMetadata("{\"currentItem\":{\"id\":\"i\",\"track\":{\"name\":\"Bez czasu\"}}}");
            var track = result.Metadata!.CurrentTrack!;
            Check(track.DurationMillis is null && !track.HasDuration && track.Name == "Bez czasu");
        }));
        tests.Add(("GET playbackMetadata: wykonawca i album to obiekty, nie napisy", () =>
        {
            Check(ReadMetadata("{\"currentItem\":{\"track\":{\"artist\":\"Napis\"}}}").Status == SonosControlApiStatus.InvalidResponse);
            Check(ReadMetadata("{\"currentItem\":{\"track\":{\"album\":\"Napis\"}}}").Status == SonosControlApiStatus.InvalidResponse);
            Check(ReadMetadata("{\"currentItem\":{\"track\":{\"artist\":{}}}}").Status == SonosControlApiStatus.InvalidResponse);
        }));
        tests.Add(("GET groupVolume: adres i pelne dane", Volume));
        tests.Add(("GET groupVolume: wymagana glosnosc, opcjonalne muted i fixed", () =>
        {
            var result = ReadVolume("{\"volume\":0}");
            Check(result.Succeeded);
            var volume = result.Volume!;
            Check(volume.Volume == 0);
            Check(volume.Muted is null && volume.FixedVolume is null && volume.Adjustable);
            Check(ReadVolume("{\"muted\":false}").Status == SonosControlApiStatus.InvalidResponse);
        }));
        tests.Add(("GET groupVolume: stala glosnosc jest jawna", () =>
        {
            var result = ReadVolume("{\"volume\":40,\"muted\":true,\"fixed\":true}");
            Check(result.Succeeded);
            var volume = result.Volume!;
            Check(volume.FixedVolume == true && !volume.Adjustable && volume.Muted == true);
        }));
        tests.Add(("GET groupVolume: poza zakresem 0..100 odrzucone", () =>
        {
            Check(ReadVolume("{\"volume\":101}").Status == SonosControlApiStatus.InvalidResponse);
            Check(ReadVolume("{\"volume\":-1}").Status == SonosControlApiStatus.InvalidResponse);
            Check(ReadVolume("{\"volume\":\"40\"}").Status == SonosControlApiStatus.InvalidResponse);
        }));
    }

    private static void Playback()
    {
        using var handler = new Handler(request =>
        {
            Check(request.Method == HttpMethod.Get && request.Content is null);
            Check(request.RequestUri!.AbsoluteUri == GroupPath + "playback");
            Check(request.Headers.Authorization?.Scheme == "Bearer" && request.Headers.Authorization.Parameter == Token);
            Check(string.Join(string.Empty, request.Headers.GetValues("X-Sonos-Api-Key")) == Key);
            Check(request.Headers.Accept.ToString() == "application/json");
            return Json(PlaybackBody);
        });
        using var client = new SonosControlApiClient(SonosControlApiConfiguration.CreateDefault(Key), handler);
        var result = client.GetGroupPlaybackAsync(Token, Group, CancellationToken.None).GetAwaiter().GetResult();
        Check(result.Succeeded && handler.Count == 1);
        var playback = result.Playback!;
        Check(playback.PlaybackState == SonosPlaybackState.Playing && playback.PositionMillis == 12345);
        Check(playback.ItemId == "item-1" && playback.QueueVersion == "q7" && playback.IsDucking == false);
        Check(playback.PreviousItemId == "item-0" && playback.PreviousPositionMillis == 5);
        Check(playback.PlayModes!.Shuffle == true && playback.PlayModes.Repeat == false && playback.PlayModes.RepeatOne is null);
        var actions = playback.AvailablePlaybackActions!;
        Check(actions.CanPlay == true && actions.CanSeek == true && actions.CanStop == false);
        Check(actions.CanSkipToPrevious == true && actions.CanSkipBack is null && actions.SkipToPreviousAllowed == true);
    }

    private static void Metadata()
    {
        using var handler = new Handler(request =>
        {
            Check(request.Method == HttpMethod.Get && request.Content is null);
            Check(request.RequestUri!.AbsoluteUri == GroupPath + "playbackMetadata");
            return Json(MetadataBody);
        });
        using var client = new SonosControlApiClient(SonosControlApiConfiguration.CreateDefault(Key), handler);
        var result = client.GetGroupMetadataAsync(Token, Group, CancellationToken.None).GetAwaiter().GetResult();
        Check(result.Succeeded && handler.Count == 1);
        var metadata = result.Metadata!;
        Check(metadata.Container!.Name == "Moja playlista" && metadata.Container.Type == "playlist" && !metadata.Container.IsStation);
        Check(metadata.Container.Service!.Name == "Serwis" && metadata.Container.Service.Id == "9");
        Check(metadata.CurrentItem!.Id == "item-1");
        var track = metadata.CurrentTrack!;
        Check(track.Name == "Utwór" && track.Type == "track" && track.DurationMillis == 215000 && track.HasDuration);
        Check(track.ArtistName == "Wykonawca" && track.AlbumName == "Album" && track.AlbumArtistName == "Wykonawca albumu");
        Check(metadata.NextItem!.Id == "item-2" && metadata.NextItem.Track!.Name == "Następny");
        Check(metadata.StreamInfo is null);
    }

    private static void Volume()
    {
        using var handler = new Handler(request =>
        {
            Check(request.Method == HttpMethod.Get && request.Content is null);
            Check(request.RequestUri!.AbsoluteUri == GroupPath + "groupVolume");
            return Json("{\"volume\":100,\"muted\":false,\"fixed\":false}");
        });
        using var client = new SonosControlApiClient(SonosControlApiConfiguration.CreateDefault(Key), handler);
        var result = client.GetGroupVolumeAsync(Token, Group, CancellationToken.None).GetAwaiter().GetResult();
        Check(result.Succeeded && handler.Count == 1);
        Check(result.Volume!.Volume == 100 && result.Volume.Muted == false && result.Volume.FixedVolume == false);
        Check(result.Volume.Adjustable);
    }

    private static void AddCommandTests(List<(string Name, Action Test)> tests)
    {
        foreach (var (command, suffix) in new[]
        {
            (SonosGroupCommand.Play, "playback/play"),
            (SonosGroupCommand.Pause, "playback/pause"),
            (SonosGroupCommand.TogglePlayPause, "playback/togglePlayPause"),
            (SonosGroupCommand.SkipToNextTrack, "playback/skipToNextTrack"),
            (SonosGroupCommand.SkipToPreviousTrack, "playback/skipToPreviousTrack")
        })
        {
            var local = command;
            var localSuffix = suffix;
            tests.Add(("POST " + localSuffix + ": puste cialo JSON i przyjecie bez potwierdzenia skutku", () =>
            {
                using var handler = new Handler(request =>
                {
                    Check(request.Method == HttpMethod.Post);
                    Check(request.RequestUri!.AbsoluteUri == GroupPath + localSuffix);
                    Check(request.Headers.Authorization?.Parameter == Token);
                    Check(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult() == "{}");
                    Check(request.Content.Headers.ContentType!.MediaType == "application/json");
                    return Json("{}");
                });
                using var client = new SonosControlApiClient(SonosControlApiConfiguration.CreateDefault(Key), handler);
                var result = client.SendGroupCommandAsync(Token, Group, local, CancellationToken.None)
                    .GetAwaiter().GetResult();
                Check(result.Accepted && result.Sent && handler.Count == 1);
                Check(!result.EffectConfirmed && !result.EffectAmbiguous && result.StateReadRecommended);
                Check(result.Command == local);
            }));
        }

        tests.Add(("POST seek: positionMillis wymagany, itemId opcjonalny", () =>
        {
            CheckBody(client => client.SeekAsync(Token, Group, 1000, null, CancellationToken.None),
                "playback/seek", "{\"positionMillis\":1000}");
            CheckBody(client => client.SeekAsync(Token, Group, 0, "item-1", CancellationToken.None),
                "playback/seek", "{\"positionMillis\":0,\"itemId\":\"item-1\"}");
        }));
        tests.Add(("POST seek nie wymaga sesji odtwarzania", () =>
        {
            using var handler = new Handler(request =>
            {
                Check(!request.Headers.Contains("X-Sonos-Playback-Session-Id"));
                Check(request.RequestUri!.AbsoluteUri == GroupPath + "playback/seek");
                return Json("{}");
            });
            using var client = new SonosControlApiClient(SonosControlApiConfiguration.CreateDefault(Key), handler);
            Check(client.SeekAsync(Token, Group, 5, null, CancellationToken.None).GetAwaiter().GetResult().Accepted);
        }));
        tests.Add(("POST seek: ujemna pozycja odrzucona lokalnie", () =>
            CheckNoHttp(client => client.SeekAsync(Token, Group, -1, null, CancellationToken.None))));
        tests.Add(("POST seek: itemId ponad 128 znakow odrzucony lokalnie", () =>
        {
            CheckNoHttp(client => client.SeekAsync(Token, Group, 0, new string('a', 129), CancellationToken.None));
            CheckNoHttp(client => client.SeekAsync(Token, Group, 0, string.Empty, CancellationToken.None));
        }));
        tests.Add(("POST seekRelative: deltaMillis dodatnia i ujemna", () =>
        {
            CheckBody(client => client.SeekRelativeAsync(Token, Group, 15000, null, CancellationToken.None),
                "playback/seekRelative", "{\"deltaMillis\":15000}");
            CheckBody(client => client.SeekRelativeAsync(Token, Group, -15000, "item-1", CancellationToken.None),
                "playback/seekRelative", "{\"deltaMillis\":-15000,\"itemId\":\"item-1\"}");
        }));
        tests.Add(("POST groupVolume: 0..100, poza zakresem bez HTTP", () =>
        {
            CheckBody(client => client.SetGroupVolumeAsync(Token, Group, 0, CancellationToken.None),
                "groupVolume", "{\"volume\":0}");
            CheckBody(client => client.SetGroupVolumeAsync(Token, Group, 100, CancellationToken.None),
                "groupVolume", "{\"volume\":100}");
            CheckNoHttp(client => client.SetGroupVolumeAsync(Token, Group, -1, CancellationToken.None));
            CheckNoHttp(client => client.SetGroupVolumeAsync(Token, Group, 101, CancellationToken.None));
        }));
        tests.Add(("POST groupVolume/mute: oba stany", () =>
        {
            CheckBody(client => client.SetGroupMuteAsync(Token, Group, true, CancellationToken.None),
                "groupVolume/mute", "{\"muted\":true}");
            CheckBody(client => client.SetGroupMuteAsync(Token, Group, false, CancellationToken.None),
                "groupVolume/mute", "{\"muted\":false}");
        }));
        tests.Add(("POST groupVolume/relative: -100..100, poza zakresem bez HTTP", () =>
        {
            CheckBody(client => client.SetRelativeGroupVolumeAsync(Token, Group, -100, CancellationToken.None),
                "groupVolume/relative", "{\"volumeDelta\":-100}");
            CheckBody(client => client.SetRelativeGroupVolumeAsync(Token, Group, 100, CancellationToken.None),
                "groupVolume/relative", "{\"volumeDelta\":100}");
            CheckNoHttp(client => client.SetRelativeGroupVolumeAsync(Token, Group, -101, CancellationToken.None));
            CheckNoHttp(client => client.SetRelativeGroupVolumeAsync(Token, Group, 101, CancellationToken.None));
        }));
        tests.Add(("HTTP 200 to przyjecie, nie potwierdzenie skutku", () =>
        {
            using var handler = new Handler(_ => Json("{}"));
            using var client = new SonosControlApiClient(SonosControlApiConfiguration.CreateDefault(Key), handler);
            var result = client.SendGroupCommandAsync(Token, Group, SonosGroupCommand.TogglePlayPause, CancellationToken.None)
                .GetAwaiter().GetResult();
            Check(result.Accepted && !result.EffectConfirmed);
            Check(result.ToString().Contains("skutek niepotwierdzony"));
        }));
        tests.Add(("zerwane polaczenie przy poleceniu przelaczajacym: skutek nierozstrzygniety", () =>
        {
            foreach (var command in new[] { SonosGroupCommand.TogglePlayPause, SonosGroupCommand.SkipToNextTrack,
                SonosGroupCommand.SeekRelative, SonosGroupCommand.SetRelativeVolume })
            {
                using var handler = new Handler(_ => throw new HttpRequestException(Token));
                using var client = new SonosControlApiClient(SonosControlApiConfiguration.CreateDefault(Key), handler);
                var result = command == SonosGroupCommand.SeekRelative
                    ? client.SeekRelativeAsync(Token, Group, 1000, null, CancellationToken.None).GetAwaiter().GetResult()
                    : command == SonosGroupCommand.SetRelativeVolume
                        ? client.SetRelativeGroupVolumeAsync(Token, Group, 5, CancellationToken.None).GetAwaiter().GetResult()
                        : client.SendGroupCommandAsync(Token, Group, command, CancellationToken.None).GetAwaiter().GetResult();
                Check(result.Status == SonosControlApiStatus.Unreachable && result.Sent);
                Check(result.EffectAmbiguous && !result.Accepted && result.StateReadRecommended);
                Check(!result.Message.Contains(Token) && !result.ToString().Contains(Token));
                Check(handler.Count == 1);
            }
        }));
        tests.Add(("zerwane polaczenie przy poleceniu bezwzglednym: bez niejednoznacznosci", () =>
        {
            using var handler = new Handler(_ => throw new HttpRequestException(Token));
            using var client = new SonosControlApiClient(SonosControlApiConfiguration.CreateDefault(Key), handler);
            var result = client.SetGroupVolumeAsync(Token, Group, 30, CancellationToken.None).GetAwaiter().GetResult();
            Check(result.Status == SonosControlApiStatus.Unreachable && result.Sent && !result.EffectAmbiguous);
        }));
        tests.Add(("blad polecenia nie powoduje ponowienia POST", () =>
        {
            foreach (var code in new[] { 401, 403, 404, 429, 499, 500, 503 })
            {
                using var handler = new Handler(_ => Json("{}", (HttpStatusCode)code));
                using var client = new SonosControlApiClient(SonosControlApiConfiguration.CreateDefault(Key), handler);
                var result = client.SendGroupCommandAsync(Token, Group, SonosGroupCommand.Play, CancellationToken.None)
                    .GetAwaiter().GetResult();
                Check(!result.Accepted && handler.Count == 1);
            }
        }));
        tests.Add(("blad polecenia nie kasuje konta ani nie odswieza tokenu", () =>
        {
            using var handler = new Handler(_ => Json("{}", HttpStatusCode.Unauthorized));
            using var client = new SonosControlApiClient(SonosControlApiConfiguration.CreateDefault(Key), handler);
            var result = client.SendGroupCommandAsync(Token, Group, SonosGroupCommand.Play, CancellationToken.None)
                .GetAwaiter().GetResult();
            Check(result.Status == SonosControlApiStatus.Unauthorized && handler.Count == 1);
            Check(!result.ToString().Contains(Token) && !result.ToString().Contains(Key));
        }));
    }

    // Komunikat WYNIKU POLECENIA jest jedyna nadajaca sie do wypowiedzenia trescia,
    // ktora transport daje wolajacemu, wiec musi opisywac POLECENIE, a nie odczyt.
    // Wszystko idzie przez PRAWDZIWEGO SonosControlApiClient z syntetycznym
    // handlerem; sprawdzamy tylko tekst i liczbe zadan, bez wzmacniania gwarancji
    // Sent (Sent to proba przekazania do HttpClient, nie dowod opuszczenia maszyny).
    private static void AddCommandMessageTests(List<(string Name, Action Test)> tests)
    {
        tests.Add(("komunikat polecenia po HTTP 200: przyjete, wykonanie niepotwierdzone", () =>
        {
            foreach (var command in new[] { SonosGroupCommand.Play, SonosGroupCommand.Pause,
                SonosGroupCommand.TogglePlayPause, SonosGroupCommand.SkipToNextTrack })
            {
                using var handler = new Handler(request =>
                {
                    Check(request.Method == HttpMethod.Post);
                    return Json("{}");
                });
                using var client = new SonosControlApiClient(SonosControlApiConfiguration.CreateDefault(Key), handler);
                var result = client.SendGroupCommandAsync(Token, Group, command, CancellationToken.None)
                    .GetAwaiter().GetResult();
                Check(result.Accepted && handler.Count == 1);
                CheckMessageIsAboutCommand(result);
                // Nie wolno oglaszac zakonczonego ODCZYTU po wyslaniu polecenia.
                Check(!result.Message.Contains("Odczyt", StringComparison.OrdinalIgnoreCase));
                Check(!result.Message.Contains("zakończony", StringComparison.OrdinalIgnoreCase));
                Check(result.Message.Contains("przyj", StringComparison.OrdinalIgnoreCase));
                Check(result.Message.Contains("niepotwierdzon", StringComparison.OrdinalIgnoreCase));
            }

            using var muteHandler = new Handler(_ => Json("{}"));
            using var muteClient = new SonosControlApiClient(SonosControlApiConfiguration.CreateDefault(Key), muteHandler);
            var mute = muteClient.SetGroupMuteAsync(Token, Group, true, CancellationToken.None).GetAwaiter().GetResult();
            Check(mute.Accepted && !mute.Message.Contains("Odczyt", StringComparison.OrdinalIgnoreCase));
            Check(mute.Message.Contains("niepotwierdzon", StringComparison.OrdinalIgnoreCase));
        }));
        tests.Add(("komunikat polecenia po HTTP 400: polecenie odrzucone, nie zapytanie o urzadzenia", () =>
        {
            using var handler = new Handler(_ => Json("{\"errorCode\":\"" + Token + "\"}", HttpStatusCode.BadRequest));
            using var client = new SonosControlApiClient(SonosControlApiConfiguration.CreateDefault(Key), handler);
            var result = client.SetRelativeGroupVolumeAsync(Token, Group, 5, CancellationToken.None)
                .GetAwaiter().GetResult();
            Check(result.Status == SonosControlApiStatus.RequestRejected && handler.Count == 1);
            CheckMessageIsAboutCommand(result);
            Check(!result.Message.Contains("zapytanie o urządzenia", StringComparison.OrdinalIgnoreCase));
            Check(!result.Message.Contains("Odczyt", StringComparison.OrdinalIgnoreCase));
            Check(result.Message.Contains("polecenie", StringComparison.OrdinalIgnoreCase));
            Check(result.Message.Contains("odrzuc", StringComparison.OrdinalIgnoreCase));
        }));
        tests.Add(("komunikat lokalnego odrzucenia: zero HTTP i bez obwiniania domu", () =>
        {
            using var handler = new Handler(_ => Json("{}"));
            using var client = new SonosControlApiClient(SonosControlApiConfiguration.CreateDefault(Key), handler);
            var outcomes = new[]
            {
                client.SetGroupVolumeAsync(Token, Group, 101, CancellationToken.None).GetAwaiter().GetResult(),
                client.SeekAsync(Token, Group, -1, null, CancellationToken.None).GetAwaiter().GetResult(),
                client.SendGroupCommandAsync(Token, "a/b", SonosGroupCommand.Play, CancellationToken.None)
                    .GetAwaiter().GetResult()
            };
            Check(handler.Count == 0);
            foreach (var result in outcomes)
            {
                Check(result.Status == SonosControlApiStatus.InvalidConfiguration && !result.Sent);
                CheckMessageIsAboutCommand(result);
                // Powodem jest argument albo groupId, a NIE identyfikator domu.
                Check(!result.Message.Contains("domu", StringComparison.OrdinalIgnoreCase));
                Check(!result.Message.Contains("Odczyt", StringComparison.OrdinalIgnoreCase));
                Check(result.Message.Contains("nie zostało wysłane", StringComparison.OrdinalIgnoreCase));
            }
        }));
        tests.Add(("komunikat anulowania przed wyslaniem: nie wyslane, nie anulowany odczyt", () =>
        {
            using var handler = new Handler(_ => Json("{}"));
            using var client = new SonosControlApiClient(SonosControlApiConfiguration.CreateDefault(Key), handler);
            var result = client.SendGroupCommandAsync(Token, Group, SonosGroupCommand.Pause, new CancellationToken(true))
                .GetAwaiter().GetResult();
            Check(result.Status == SonosControlApiStatus.Canceled && !result.Sent && handler.Count == 0);
            CheckMessageIsAboutCommand(result);
            Check(!result.Message.Contains("Odczyt", StringComparison.OrdinalIgnoreCase));
            Check(result.Message.Contains("nie zostało wysłane", StringComparison.OrdinalIgnoreCase));
            Check(result.Message.Contains("anulowan", StringComparison.OrdinalIgnoreCase));
        }));
        tests.Add(("komunikat zerwanego polecenia przelaczajacego/wzglednego: skutek nieznany", () =>
        {
            foreach (var command in new[] { SonosGroupCommand.TogglePlayPause, SonosGroupCommand.SkipToPreviousTrack })
            {
                using var handler = new Handler(_ => throw new HttpRequestException(Token));
                using var client = new SonosControlApiClient(SonosControlApiConfiguration.CreateDefault(Key), handler);
                var result = client.SendGroupCommandAsync(Token, Group, command, CancellationToken.None)
                    .GetAwaiter().GetResult();
                Check(result.EffectAmbiguous && handler.Count == 1);
                CheckMessageIsAboutCommand(result);
                Check(!result.Message.Contains("Odczyt", StringComparison.OrdinalIgnoreCase));
                Check(result.Message.Contains("skutek nieznany", StringComparison.OrdinalIgnoreCase));
                // Sent to PROBA przekazania, wiec komunikat nie moze twierdzic, ze
                // polecenie dotarlo do Sonosa ani ze zostalo wykonane.
                Check(!result.Message.Contains("dostarcz", StringComparison.OrdinalIgnoreCase));
                Check(!result.Message.Contains("wykonane", StringComparison.OrdinalIgnoreCase));
            }

            using var seekHandler = new Handler(_ => throw new HttpRequestException(Token));
            using var seekClient = new SonosControlApiClient(SonosControlApiConfiguration.CreateDefault(Key), seekHandler);
            var relative = seekClient.SeekRelativeAsync(Token, Group, -5000, null, CancellationToken.None)
                .GetAwaiter().GetResult();
            Check(relative.EffectAmbiguous);
            Check(relative.Message.Contains("skutek nieznany", StringComparison.OrdinalIgnoreCase));
        }));
        tests.Add(("komunikaty odczytu zostaja odczytami", () =>
        {
            using var handler = new Handler(_ => Json("{\"volume\":10}"));
            using var client = new SonosControlApiClient(SonosControlApiConfiguration.CreateDefault(Key), handler);
            var read = client.GetGroupVolumeAsync(Token, Group, CancellationToken.None).GetAwaiter().GetResult();
            Check(read.Succeeded && read.Message.Contains("Odczyt", StringComparison.OrdinalIgnoreCase));
        }));
    }

    private static void CheckMessageIsAboutCommand(SonosGroupCommandOutcome result)
    {
        Check(!string.IsNullOrWhiteSpace(result.Message));
        Check(!result.Message.Contains(Token) && !result.Message.Contains(Key));
        Check(!result.ToString().Contains(Token) && !result.ToString().Contains(Key));
        Check(result.Message.Contains("Sonos", StringComparison.Ordinal));
    }

    private static void AddSafetyTests(List<(string Name, Action Test)> tests)
    {
        foreach (var (code, expected) in new[]
        {
            (400, SonosControlApiStatus.RequestRejected), (401, SonosControlApiStatus.Unauthorized),
            (403, SonosControlApiStatus.Forbidden), (404, SonosControlApiStatus.NotFound),
            (429, SonosControlApiStatus.RateLimited), (499, SonosControlApiStatus.CommandFailed),
            (500, SonosControlApiStatus.ServiceError), (503, SonosControlApiStatus.ServiceError),
            (302, SonosControlApiStatus.RedirectRefused), (204, SonosControlApiStatus.InvalidResponse)
        })
        {
            var local = code;
            var localExpected = expected;
            tests.Add(("odczyt stanu HTTP " + local + ": bez echa tresci serwera", () =>
            {
                using var handler = new Handler(_ => Json("{\"errorCode\":\"" + Token + "\",\"reason\":\"" + Key + "\"}",
                    (HttpStatusCode)local));
                using var client = new SonosControlApiClient(SonosControlApiConfiguration.CreateDefault(Key), handler);
                var result = client.GetGroupPlaybackAsync(Token, Group, CancellationToken.None).GetAwaiter().GetResult();
                Check(result.Status == localExpected && handler.Count == 1);
                Check(!result.Message.Contains(Token) && !result.ToString().Contains(Token) && !result.ToString().Contains(Key));
            }));
        }

        foreach (var groupId in new string?[] { null, string.Empty, "a/b", "..", new string('a', 36), "a b" })
        {
            var local = groupId;
            var index = tests.Count;
            tests.Add(("zly groupId, zero HTTP " + index, () =>
            {
                using var handler = new Handler(_ => Json("{}"));
                using var client = new SonosControlApiClient(SonosControlApiConfiguration.CreateDefault(Key), handler);
                Check(client.GetGroupPlaybackAsync(Token, local, CancellationToken.None).GetAwaiter().GetResult()
                    .Status == SonosControlApiStatus.InvalidConfiguration);
                Check(client.GetGroupMetadataAsync(Token, local, CancellationToken.None).GetAwaiter().GetResult()
                    .Status == SonosControlApiStatus.InvalidConfiguration);
                Check(client.GetGroupVolumeAsync(Token, local, CancellationToken.None).GetAwaiter().GetResult()
                    .Status == SonosControlApiStatus.InvalidConfiguration);
                var command = client.SendGroupCommandAsync(Token, local, SonosGroupCommand.Play, CancellationToken.None)
                    .GetAwaiter().GetResult();
                Check(command.Status == SonosControlApiStatus.InvalidConfiguration && !command.Sent);
                Check(handler.Count == 0);
            }));
        }

        foreach (var token in new string?[] { null, string.Empty, "x\r\nInjected: yes", "ą", new string('a', 65537) })
        {
            var local = token;
            var index = tests.Count;
            tests.Add(("wadliwy token, zero HTTP " + index, () =>
            {
                using var handler = new Handler(_ => Json("{}"));
                using var client = new SonosControlApiClient(SonosControlApiConfiguration.CreateDefault(Key), handler);
                Check(client.GetGroupVolumeAsync(local, Group, CancellationToken.None).GetAwaiter().GetResult()
                    .Status == SonosControlApiStatus.InvalidConfiguration);
                var command = client.SendGroupCommandAsync(local, Group, SonosGroupCommand.Pause, CancellationToken.None)
                    .GetAwaiter().GetResult();
                Check(command.Status == SonosControlApiStatus.InvalidConfiguration && !command.Sent && handler.Count == 0);
            }));
        }

        tests.Add(("brak klucza integracji: zero HTTP takze dla polecen", () =>
        {
            using var handler = new Handler(_ => Json("{}"));
            using var client = new SonosControlApiClient(SonosControlApiConfiguration.CreateDefault(null), handler);
            Check(client.SendGroupCommandAsync(Token, Group, SonosGroupCommand.Play, CancellationToken.None)
                .GetAwaiter().GetResult().Status == SonosControlApiStatus.InvalidConfiguration);
            Check(handler.Count == 0);
        }));
        tests.Add(("anulowanie przed wywolaniem: zero HTTP", () =>
        {
            using var handler = new Handler(_ => Json("{}"));
            using var client = new SonosControlApiClient(SonosControlApiConfiguration.CreateDefault(Key), handler);
            var canceled = new CancellationToken(true);
            Check(client.GetGroupPlaybackAsync(Token, Group, canceled).GetAwaiter().GetResult()
                .Status == SonosControlApiStatus.Canceled);
            var command = client.SendGroupCommandAsync(Token, Group, SonosGroupCommand.Play, canceled)
                .GetAwaiter().GetResult();
            Check(command.Status == SonosControlApiStatus.Canceled && !command.Sent && handler.Count == 0);
        }));
        tests.Add(("obcy koncowy adres odpowiedzi odrzucony", () =>
        {
            foreach (var target in new[] { "https://attacker.invalid/", GroupPath + "groupVolume" })
            {
                using var handler = new Handler(_ =>
                {
                    var response = Json("{\"playbackState\":\"PLAYBACK_STATE_IDLE\"}");
                    response.RequestMessage = new HttpRequestMessage(HttpMethod.Get, target);
                    return response;
                });
                using var client = new SonosControlApiClient(SonosControlApiConfiguration.CreateDefault(Key), handler);
                Check(client.GetGroupPlaybackAsync(Token, Group, CancellationToken.None).GetAwaiter().GetResult()
                    .Status == SonosControlApiStatus.RedirectRefused);
            }
        }));
        tests.Add(("brak wiadomosci zadania w odpowiedzi odrzucony", () =>
        {
            using var handler = new Handler(_ => Json("{\"volume\":10}"), attachRequest: false);
            using var client = new SonosControlApiClient(SonosControlApiConfiguration.CreateDefault(Key), handler);
            Check(client.GetGroupVolumeAsync(Token, Group, CancellationToken.None).GetAwaiter().GetResult()
                .Status == SonosControlApiStatus.RedirectRefused);
        }));
        tests.Add(("nie JSON, zly charset i duplikaty pol odrzucone", () =>
        {
            using var html = new Handler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent("{\"volume\":10}", Encoding.UTF8, "text/html") });
            using var clientHtml = new SonosControlApiClient(SonosControlApiConfiguration.CreateDefault(Key), html);
            Check(clientHtml.GetGroupVolumeAsync(Token, Group, CancellationToken.None).GetAwaiter().GetResult()
                .Status == SonosControlApiStatus.InvalidResponse);
            Check(ReadVolume("{\"volume\":10,\"volume\":20}").Status == SonosControlApiStatus.InvalidResponse);
            Check(ReadPlayback("{").Status == SonosControlApiStatus.InvalidResponse);
            Check(ReadMetadata("[]").Status == SonosControlApiStatus.InvalidResponse);
        }));
        tests.Add(("limity dlugosci pol z definicji Sonos", () =>
        {
            var ok = ReadMetadata("{\"currentItem\":{\"track\":{\"artist\":{\"name\":\"" + new string('a', 127) + "\"}}}}");
            var bad = ReadMetadata("{\"currentItem\":{\"track\":{\"artist\":{\"name\":\"" + new string('a', 128) + "\"}}}}");
            Check(ok.Succeeded && ok.Metadata!.CurrentTrack!.ArtistName!.Length == 127);
            Check(bad.Status == SonosControlApiStatus.InvalidResponse);
            var streamOk = ReadMetadata("{\"streamInfo\":\"" + new string('b', 256) + "\"}");
            var streamBad = ReadMetadata("{\"streamInfo\":\"" + new string('b', 257) + "\"}");
            Check(streamOk.Succeeded && streamBad.Status == SonosControlApiStatus.InvalidResponse);
            var itemOk = ReadPlayback("{\"playbackState\":\"PLAYBACK_STATE_IDLE\",\"itemId\":\"" + new string('c', 128) + "\"}");
            var itemBad = ReadPlayback("{\"playbackState\":\"PLAYBACK_STATE_IDLE\",\"itemId\":\"" + new string('c', 129) + "\"}");
            Check(itemOk.Succeeded && itemBad.Status == SonosControlApiStatus.InvalidResponse);
        }));
        tests.Add(("handler wolajacego zyje po Dispose klienta", () =>
        {
            using var handler = new Handler(_ => Json("{\"volume\":10}"));
            using (var first = new SonosControlApiClient(SonosControlApiConfiguration.CreateDefault(Key), handler))
            {
                Check(first.GetGroupVolumeAsync(Token, Group, CancellationToken.None).GetAwaiter().GetResult().Succeeded);
            }

            Check(!handler.Disposed);
            using var second = new SonosControlApiClient(SonosControlApiConfiguration.CreateDefault(Key), handler);
            Check(second.GetGroupVolumeAsync(Token, Group, CancellationToken.None).GetAwaiter().GetResult().Succeeded);
            Check(handler.Count == 2);
        }));
        tests.Add(("deadline obejmuje czytanie ciala odczytu", () => SlowBody(false)));
        tests.Add(("anulowanie wolajacego podczas czytania ciala", () => SlowBody(true)));
    }

    private static void SlowBody(bool callerCancels)
    {
        using var caller = new CancellationTokenSource();
        using var stream = new SlowStream(callerCancels ? caller : null);
        using var handler = new Handler(_ =>
        {
            var response = Json("{\"volume\":10}");
            response.Content = new StreamContent(stream);
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            return response;
        });
        using var client = new SonosControlApiClient(SonosControlApiConfiguration.CreateDefault(Key), handler,
            TimeSpan.FromMilliseconds(callerCancels ? 5000 : 100));
        var result = client.GetGroupVolumeAsync(Token, Group, caller.Token)
            .WaitAsync(TimeSpan.FromSeconds(3)).GetAwaiter().GetResult();
        Check(result.Status == (callerCancels ? SonosControlApiStatus.Canceled : SonosControlApiStatus.Unreachable));
        Check(stream.ReadStarted && stream.Disposed && handler.Count == 1);
    }

    private static SonosGroupPlaybackOutcome ReadPlayback(string body)
    {
        using var handler = new Handler(_ => Json(body));
        using var client = new SonosControlApiClient(SonosControlApiConfiguration.CreateDefault(Key), handler);
        return client.GetGroupPlaybackAsync(Token, Group, CancellationToken.None).GetAwaiter().GetResult();
    }

    private static SonosGroupMetadataOutcome ReadMetadata(string body)
    {
        using var handler = new Handler(_ => Json(body));
        using var client = new SonosControlApiClient(SonosControlApiConfiguration.CreateDefault(Key), handler);
        return client.GetGroupMetadataAsync(Token, Group, CancellationToken.None).GetAwaiter().GetResult();
    }

    private static SonosGroupVolumeOutcome ReadVolume(string body)
    {
        using var handler = new Handler(_ => Json(body));
        using var client = new SonosControlApiClient(SonosControlApiConfiguration.CreateDefault(Key), handler);
        return client.GetGroupVolumeAsync(Token, Group, CancellationToken.None).GetAwaiter().GetResult();
    }

    private static void CheckBody(
        Func<SonosControlApiClient, Task<SonosGroupCommandOutcome>> call, string suffix, string expectedBody)
    {
        using var handler = new Handler(request =>
        {
            Check(request.Method == HttpMethod.Post);
            Check(request.RequestUri!.AbsoluteUri == GroupPath + suffix);
            Check(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult() == expectedBody);
            Check(request.Content.Headers.ContentType!.MediaType == "application/json");
            Check(request.Content.Headers.ContentType.CharSet?.Trim('"').ToLowerInvariant() == "utf-8");
            return Json("{}");
        });
        using var client = new SonosControlApiClient(SonosControlApiConfiguration.CreateDefault(Key), handler);
        var result = call(client).GetAwaiter().GetResult();
        Check(result.Accepted && result.Sent && !result.EffectConfirmed && handler.Count == 1);
    }

    private static void CheckNoHttp(Func<SonosControlApiClient, Task<SonosGroupCommandOutcome>> call)
    {
        using var handler = new Handler(_ => Json("{}"));
        using var client = new SonosControlApiClient(SonosControlApiConfiguration.CreateDefault(Key), handler);
        var result = call(client).GetAwaiter().GetResult();
        Check(result.Status == SonosControlApiStatus.InvalidConfiguration && !result.Sent && handler.Count == 0);
    }

    private static void CheckThrows(Action action)
    {
        try { action(); }
        catch (ArgumentException) { return; }
        throw new InvalidOperationException("Oczekiwano odrzucenia argumentu.");
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

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> reply, bool attachRequest = true)
        : HttpMessageHandler
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

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AccessibleMediaController.Core.Sonos;

/// <summary>
/// ODCZYT stanu, metadanych i glosnosci GRUPY oraz PODSTAWOWE polecenia grupy.
///
/// Ta czesc klienta korzysta z tego samego transportu co odczyt domow i grup:
/// staly host HTTPS, brak przekierowan i ciasteczek, naglowki na POJEDYNCZYM
/// zadaniu, deadline obejmujacy naglowki i cialo, ograniczone czytanie, UTF-8
/// ze sciscie sprawdzanym kodowaniem, odrzucanie duplikatow pol JSON.
///
/// Czego tu NIE MA i byc nie moze:
///  * ZADNEGO automatycznego ponowienia POST - polecenie przelaczajace albo
///    wzgledne wykonane dwa razy da inny skutek niz raz,
///  * zadnego odswiezania tokenu ani kasowania konta przy 401 - to warstwa
///    koordynatora, nie transport,
///  * zadnej weryfikacji SKUTKU polecenia - HTTP 200 to tylko PRZYJECIE
///    zlecenia. Jawny odczyt stanu po poleceniu nalezy do koordynatora.
/// </summary>
public sealed partial class SonosControlApiClient
{
    /// <summary>GET /groups/{groupId}/playback - stan transportu grupy.</summary>
    public async Task<SonosGroupPlaybackOutcome> GetGroupPlaybackAsync(
        string? accessToken, string? groupId, CancellationToken cancellationToken)
    {
        if (!TryGroupUri(groupId, "playback", out var uri))
        {
            return SonosGroupPlaybackOutcome.Failure(SonosControlApiStatus.InvalidConfiguration);
        }

        var reply = await ReadAsync(uri!, accessToken, cancellationToken).ConfigureAwait(false);
        if (reply.Status != SonosControlApiStatus.Success)
        {
            return SonosGroupPlaybackOutcome.Failure(reply.Status);
        }

        try
        {
            using var document = Parse(reply.Body!);
            return SonosGroupPlaybackOutcome.Ok(ReadPlaybackStatus(document.RootElement));
        }
        catch (Exception ex) when (IsInvalidData(ex))
        {
            return SonosGroupPlaybackOutcome.Failure(SonosControlApiStatus.InvalidResponse);
        }
    }

    /// <summary>GET /groups/{groupId}/playbackMetadata - co gra i co bedzie grac.</summary>
    public async Task<SonosGroupMetadataOutcome> GetGroupMetadataAsync(
        string? accessToken, string? groupId, CancellationToken cancellationToken)
    {
        if (!TryGroupUri(groupId, "playbackMetadata", out var uri))
        {
            return SonosGroupMetadataOutcome.Failure(SonosControlApiStatus.InvalidConfiguration);
        }

        var reply = await ReadAsync(uri!, accessToken, cancellationToken).ConfigureAwait(false);
        if (reply.Status != SonosControlApiStatus.Success)
        {
            return SonosGroupMetadataOutcome.Failure(reply.Status);
        }

        try
        {
            using var document = Parse(reply.Body!);
            return SonosGroupMetadataOutcome.Ok(ReadMetadata(document.RootElement));
        }
        catch (Exception ex) when (IsInvalidData(ex))
        {
            return SonosGroupMetadataOutcome.Failure(SonosControlApiStatus.InvalidResponse);
        }
    }

    /// <summary>GET /groups/{groupId}/groupVolume - glosnosc, wyciszenie i stalosc.</summary>
    public async Task<SonosGroupVolumeOutcome> GetGroupVolumeAsync(
        string? accessToken, string? groupId, CancellationToken cancellationToken)
    {
        if (!TryGroupUri(groupId, "groupVolume", out var uri))
        {
            return SonosGroupVolumeOutcome.Failure(SonosControlApiStatus.InvalidConfiguration);
        }

        var reply = await ReadAsync(uri!, accessToken, cancellationToken).ConfigureAwait(false);
        if (reply.Status != SonosControlApiStatus.Success)
        {
            return SonosGroupVolumeOutcome.Failure(reply.Status);
        }

        try
        {
            using var document = Parse(reply.Body!);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException();
            }

            var volume = RequiredInt32(root, "volume");
            if (volume < SonosGroupVolume.MinVolume || volume > SonosGroupVolume.MaxVolume)
            {
                throw new JsonException();
            }

            return SonosGroupVolumeOutcome.Ok(new SonosGroupVolume(volume, Flag(root, "muted"), Flag(root, "fixed")));
        }
        catch (Exception ex) when (IsInvalidData(ex))
        {
            return SonosGroupVolumeOutcome.Failure(SonosControlApiStatus.InvalidResponse);
        }
    }

    /// <summary>
    /// POST polecenia grupy BEZ parametrow: play, pause, togglePlayPause,
    /// skipToNextTrack, skipToPreviousTrack. Cialo to pusty obiekt JSON, bo
    /// definicja nie ma dla nich zadnych pol.
    ///
    /// skipToPreviousTrack przechodzi do POPRZEDNIEGO utworu; to NIE jest
    /// skipBack (powrot na poczatek biezacego utworu) i nie wolno ich mylic.
    /// </summary>
    public Task<SonosGroupCommandOutcome> SendGroupCommandAsync(
        string? accessToken, string? groupId, SonosGroupCommand command, CancellationToken cancellationToken)
    {
        switch (command)
        {
            case SonosGroupCommand.Play:
            case SonosGroupCommand.Pause:
            case SonosGroupCommand.TogglePlayPause:
            case SonosGroupCommand.SkipToNextTrack:
            case SonosGroupCommand.SkipToPreviousTrack:
                return SendAsync(accessToken, groupId, command, "{}", cancellationToken);
            default:
                // Polecenia z parametrami maja wlasne metody, ktore te parametry
                // sprawdzaja. Nie wysylamy pustego ciala tam, gdzie definicja
                // wymaga pola - Sonos odrzucilby to jako ERROR_MISSING_PARAMETERS.
                return Task.FromResult(SonosGroupCommandOutcome.FromStatus(
                    command, SonosControlApiStatus.InvalidConfiguration, false));
        }
    }

    /// <summary>
    /// POST /groups/{groupId}/playback/seek - przeskok BEZWZGLEDNY.
    /// positionMillis jest wymagane; itemId opcjonalne (maxLength 128) i sluzy do
    /// odrzucenia przeskoku, gdy utwor zmienil sie w trakcie. Nie wymaga sesji
    /// odtwarzania - zadnego naglowka sesji tu nie ma.
    /// </summary>
    public Task<SonosGroupCommandOutcome> SeekAsync(
        string? accessToken, string? groupId, int positionMillis, string? itemId, CancellationToken cancellationToken)
    {
        if (positionMillis < 0 || !IsAcceptableItemId(itemId))
        {
            return Task.FromResult(SonosGroupCommandOutcome.FromStatus(
                SonosGroupCommand.Seek, SonosControlApiStatus.InvalidConfiguration, false));
        }

        return SendAsync(accessToken, groupId, SonosGroupCommand.Seek,
            Body("positionMillis", positionMillis, itemId), cancellationToken);
    }

    /// <summary>
    /// POST /groups/{groupId}/playback/seekRelative - przeskok WZGLEDNY.
    /// deltaMillis moze byc ujemna. Polecenie zalezy od stanu, wiec po utracie
    /// odpowiedzi skutek jest NIEROZSTRZYGNIETY i nie wolno go ponawiac.
    /// </summary>
    public Task<SonosGroupCommandOutcome> SeekRelativeAsync(
        string? accessToken, string? groupId, int deltaMillis, string? itemId, CancellationToken cancellationToken)
    {
        if (!IsAcceptableItemId(itemId))
        {
            return Task.FromResult(SonosGroupCommandOutcome.FromStatus(
                SonosGroupCommand.SeekRelative, SonosControlApiStatus.InvalidConfiguration, false));
        }

        return SendAsync(accessToken, groupId, SonosGroupCommand.SeekRelative,
            Body("deltaMillis", deltaMillis, itemId), cancellationToken);
    }

    /// <summary>
    /// POST /groups/{groupId}/groupVolume - glosnosc BEZWZGLEDNA 0..100.
    /// Definicja odrzuca wartosci powyzej 100, a ujemne interpretuje jako 0.
    /// My nie polegamy na tej interpretacji: wartosc poza zakresem jest bledem
    /// wolajacego i nie wysylamy jej wcale.
    ///
    /// Bramka fixed:true nalezy do UI; tutaj sprawdzamy tylko argument, bo stan
    /// glosnosci moze sie zmienic miedzy odczytem a poleceniem.
    /// </summary>
    public Task<SonosGroupCommandOutcome> SetGroupVolumeAsync(
        string? accessToken, string? groupId, int volume, CancellationToken cancellationToken)
    {
        if (volume < SonosGroupVolume.MinVolume || volume > SonosGroupVolume.MaxVolume)
        {
            return Task.FromResult(SonosGroupCommandOutcome.FromStatus(
                SonosGroupCommand.SetVolume, SonosControlApiStatus.InvalidConfiguration, false));
        }

        return SendAsync(accessToken, groupId, SonosGroupCommand.SetVolume,
            Body("volume", volume, null), cancellationToken);
    }

    /// <summary>POST /groups/{groupId}/groupVolume/mute - wyciszenie JAWNE, nie przelaczane.</summary>
    public Task<SonosGroupCommandOutcome> SetGroupMuteAsync(
        string? accessToken, string? groupId, bool muted, CancellationToken cancellationToken) =>
        SendAsync(accessToken, groupId, SonosGroupCommand.SetMute,
            "{\"muted\":" + (muted ? "true" : "false") + "}", cancellationToken);

    /// <summary>
    /// POST /groups/{groupId}/groupVolume/relative - zmiana WZGLEDNA -100..100.
    /// Koordynator grupy dodaje wartosc do biezacej glosnosci i przycina wynik do
    /// 0..100. Zalezy od stanu, wiec bez automatycznych ponowien.
    /// </summary>
    public Task<SonosGroupCommandOutcome> SetRelativeGroupVolumeAsync(
        string? accessToken, string? groupId, int volumeDelta, CancellationToken cancellationToken)
    {
        if (volumeDelta < -SonosGroupVolume.MaxVolume || volumeDelta > SonosGroupVolume.MaxVolume)
        {
            return Task.FromResult(SonosGroupCommandOutcome.FromStatus(
                SonosGroupCommand.SetRelativeVolume, SonosControlApiStatus.InvalidConfiguration, false));
        }

        return SendAsync(accessToken, groupId, SonosGroupCommand.SetRelativeVolume,
            Body("volumeDelta", volumeDelta, null), cancellationToken);
    }

    private async Task<SonosGroupCommandOutcome> SendAsync(
        string? accessToken, string? groupId, SonosGroupCommand command, string body, CancellationToken cancellationToken)
    {
        if (!TryGroupUri(groupId, SonosGroupCommands.PathSuffix(command), out var uri))
        {
            return SonosGroupCommandOutcome.FromStatus(command, SonosControlApiStatus.InvalidConfiguration, false);
        }

        var reply = await WriteAsync(uri!, accessToken, body, cancellationToken).ConfigureAwait(false);
        return SonosGroupCommandOutcome.FromStatus(command, reply.Status, reply.Sent);
    }

    private bool TryGroupUri(string? groupId, string suffix, out Uri? uri)
    {
        uri = null;
        if (!SonosGroupIdPolicy.TryEncode(groupId, out var encoded))
        {
            return false;
        }

        uri = new Uri(_configuration.Origin, "groups/" + encoded + "/" + suffix);
        return true;
    }

    /// <summary>
    /// Cialo polecenia sklejane RECZNIE z wartosci liczbowej i sprawdzonego
    /// itemId. Liczba idzie przez InvariantCulture, a itemId przechodzi tylko
    /// przez bramke IsAcceptableItemId, wiec nie ma tu znakow wymagajacych
    /// ucieczki w JSON. Kolejnosc pol jest ustalona, bo testy porownuja cialo
    /// doslownie.
    /// </summary>
    private static string Body(string name, int value, string? itemId)
    {
        var builder = new StringBuilder("{\"");
        builder.Append(name).Append("\":").Append(value.ToString(CultureInfo.InvariantCulture));
        if (!string.IsNullOrEmpty(itemId))
        {
            builder.Append(",\"itemId\":\"").Append(itemId).Append('"');
        }

        return builder.Append('}').ToString();
    }

    /// <summary>
    /// itemId w ciele polecenia: null znaczy "nie podano" i jest legalne.
    /// Podana wartosc musi byc niepusta, do 128 znakow (maxLength z definicji) i
    /// zlozona ze znakow, ktore nie wymagaja ucieczki w JSON - inaczej odrzucamy
    /// zamiast naprawiac. Identyfikatory Sonos mieszcza sie w tym zbiorze.
    /// </summary>
    private static bool IsAcceptableItemId(string? itemId)
    {
        if (itemId is null)
        {
            return true;
        }

        if (itemId.Length == 0 || itemId.Length > 128)
        {
            return false;
        }

        foreach (var character in itemId)
        {
            if (character < ' ' || character > '~' || character is '"' or '\\')
            {
                return false;
            }
        }

        return true;
    }

    private static SonosGroupPlaybackStatus ReadPlaybackStatus(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException();
        }

        // playbackState jest JEDYNYM wymaganym polem; nieznana wartosc enuma nie
        // uniewaznia odpowiedzi, ale BRAK pola juz tak.
        var stateText = Text(root, "playbackState", 64, true);
        SonosPlaybackStates.TryParse(stateText, out var state);
        return new SonosGroupPlaybackStatus(
            state,
            Flag(root, "isDucking"),
            Text(root, "queueVersion", 64),
            Text(root, "itemId", 128),
            OptionalInt32(root, "positionMillis"),
            Text(root, "previousItemId", 128),
            OptionalInt32(root, "previousPositionMillis"),
            ReadPlayModes(Object(root, "playModes")),
            ReadActions(Object(root, "availablePlaybackActions")));
    }

    private static SonosPlayModes? ReadPlayModes(JsonElement? modes) =>
        modes is null
            ? null
            : new SonosPlayModes(
                Flag(modes.Value, "repeat"),
                Flag(modes.Value, "repeatOne"),
                Flag(modes.Value, "shuffle"),
                Flag(modes.Value, "crossfade"));

    private static SonosPlaybackActions? ReadActions(JsonElement? actions) =>
        actions is null
            ? null
            : new SonosPlaybackActions(
                Flag(actions.Value, "canPlay"),
                Flag(actions.Value, "canSkip"),
                Flag(actions.Value, "canSkipBack"),
                Flag(actions.Value, "canSkipToPrevious"),
                Flag(actions.Value, "canSeek"),
                Flag(actions.Value, "canPause"),
                Flag(actions.Value, "canStop"),
                Flag(actions.Value, "canRepeat"),
                Flag(actions.Value, "canRepeatOne"),
                Flag(actions.Value, "canCrossfade"),
                Flag(actions.Value, "canShuffle"));

    private static SonosGroupMetadata ReadMetadata(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException();
        }

        var container = Object(root, "container");
        var show = Object(root, "currentShow");
        return new SonosGroupMetadata(
            container is null
                ? null
                : new SonosMetadataContainer(
                    Text(container.Value, "name", 100),
                    Text(container.Value, "type", 48),
                    ReadService(Object(container.Value, "service"))),
            ReadQueueItem(Object(root, "currentItem")),
            ReadQueueItem(Object(root, "nextItem")),
            show is null ? null : Text(show.Value, "name", 127, true),
            Text(root, "streamInfo", 256));
    }

    private static SonosQueueItem? ReadQueueItem(JsonElement? item) =>
        item is null
            ? null
            : new SonosQueueItem(
                Text(item.Value, "id", 128),
                ReadTrack(Object(item.Value, "track")),
                Flag(item.Value, "deleted"));

    private static SonosTrackMetadata? ReadTrack(JsonElement? track)
    {
        if (track is null)
        {
            return null;
        }

        // artist i album to OBIEKTY z wymaganym name; napis w tym miejscu jest
        // niezgodna odpowiedzia, a nie nazwa wykonawcy.
        var album = Object(track.Value, "album");
        var artist = Object(track.Value, "artist");
        return new SonosTrackMetadata(
            Text(track.Value, "type", 32),
            Text(track.Value, "name", 100),
            artist is null ? null : Text(artist.Value, "name", 127, true),
            album is null ? null : Text(album.Value, "name", 127, true),
            album is null ? null : ReadNestedArtistName(album.Value),
            ReadService(Object(track.Value, "service")),
            OptionalInt32(track.Value, "durationMillis"));
    }

    private static string? ReadNestedArtistName(JsonElement album)
    {
        var artist = Object(album, "artist");
        return artist is null ? null : Text(artist.Value, "name", 127, true);
    }

    private static SonosMetadataService? ReadService(JsonElement? service) =>
        service is null
            ? null
            : new SonosMetadataService(Text(service.Value, "name", 31), Text(service.Value, "id", 10));

    /// <summary>Zagniezdzony obiekt: brak pola i JSON null daja null, obcy typ to blad.</summary>
    private static JsonElement? Object(JsonElement parent, string name)
    {
        if (parent.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException();
        }

        if (!parent.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException();
        }

        return value;
    }

    /// <summary>bool? z JSON: brak pola i null daja null, obcy typ to blad.</summary>
    private static bool? Flag(JsonElement parent, string name)
    {
        if (parent.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException();
        }

        if (!parent.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            throw new JsonException();
        }

        return value.GetBoolean();
    }

    /// <summary>int? int32 z JSON: brak pola i null daja null (NIE zero).</summary>
    private static int? OptionalInt32(JsonElement parent, string name)
    {
        if (parent.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException();
        }

        if (!parent.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var number))
        {
            throw new JsonException();
        }

        return number;
    }

    private static int RequiredInt32(JsonElement parent, string name) =>
        OptionalInt32(parent, name) ?? throw new JsonException();

    /// <summary>
    /// POST z cialem JSON. Te same zabezpieczenia co odczyt: naglowki na
    /// pojedynczym zadaniu, deadline obejmujacy naglowki I cialo, sprawdzenie
    /// koncowego adresu, ograniczone czytanie odpowiedzi.
    ///
    /// Sent mowi, czy zadanie opuscilo aplikacje - to jedyna informacja
    /// pozwalajaca wyzej rozpoznac NIEROZSTRZYGNIETY skutek. Zadnego ponowienia
    /// tu nie ma i byc nie moze.
    /// </summary>
    private async Task<(SonosControlApiStatus Status, bool Sent)> WriteAsync(
        Uri uri, string? token, string body, CancellationToken caller)
    {
        var reply = await WriteCoreAsync(uri, token, body, readBody: false, caller).ConfigureAwait(false);
        return (reply.Status, reply.Sent);
    }

    /// <summary>
    /// WSPOLNY silnik POST. <paramref name="readBody"/> to JEDYNE rozszerzenie
    /// wobec odebranego zapisu polecen: gdy false, zachowanie jest DOKLADNIE
    /// dawne (cialo odpowiedzi w ogole nie jest czytane, bo tresc bledu jest
    /// sterowana przez serwer i nie wolno jej logowac ani oddawac echem). Gdy
    /// true - i TYLKO przy HTTP 200 - cialo przechodzi przez TEN SAM ograniczony
    /// czytnik co odczyty (<see cref="ReadLimitedJsonAsync"/>).
    ///
    /// Zaden nowy HttpClient, zadna kopia polityki hosta, biletu, przekierowan,
    /// deadline'u ani budzetu tresci. Zadnego ponowienia.
    /// </summary>
    private async Task<(SonosControlApiStatus Status, bool Sent, string? Body)> WriteCoreAsync(
        Uri uri, string? token, string body, bool readBody, CancellationToken caller)
    {
        if (caller.IsCancellationRequested)
        {
            return (SonosControlApiStatus.Canceled, false, null);
        }

        if (!IsHeaderValue(token) || !IsHeaderValue(_configuration.ApiKey))
        {
            return (SonosControlApiStatus.InvalidConfiguration, false, null);
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(caller);
        deadline.CancelAfter(_timeout);
        var ct = deadline.Token;
        var sent = false;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, uri);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            request.Headers.Add("X-Sonos-Api-Key", _configuration.ApiKey);
            request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            request.Content = new StringContent(body, new UTF8Encoding(false), "application/json");
            sent = true;
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);
            // Kazda odpowiedz musi wskazywac dokladnie wyslany adres.
            if (response.RequestMessage?.RequestUri != uri)
            {
                return (SonosControlApiStatus.RedirectRefused, true, null);
            }

            var status = MapStatus((int)response.StatusCode);
            // Ciala odpowiedzi BLEDU nie czytamy NIGDY: tresc jest sterowana przez
            // serwer i nie wolno jej logowac ani podawac echem uzytkownikowi.
            if (!readBody || status != SonosControlApiStatus.Success)
            {
                return (status, true, null);
            }

            var (ok, payload) = await ReadLimitedJsonAsync(response, ct).ConfigureAwait(false);
            return ok
                ? (SonosControlApiStatus.Success, true, payload)
                : (SonosControlApiStatus.InvalidResponse, true, null);
        }
        catch (OperationCanceledException)
        {
            return (caller.IsCancellationRequested ? SonosControlApiStatus.Canceled : SonosControlApiStatus.Unreachable, sent, null);
        }
        catch (HttpRequestException) { return (SonosControlApiStatus.Unreachable, sent, null); }
        catch (IOException) { return (SonosControlApiStatus.Unreachable, sent, null); }
        catch (DecoderFallbackException) { return (SonosControlApiStatus.InvalidResponse, sent, null); }
        catch (FormatException) { return (SonosControlApiStatus.InvalidConfiguration, false, null); }
    }
}

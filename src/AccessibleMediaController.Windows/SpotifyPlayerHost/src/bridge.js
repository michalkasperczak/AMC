// Mostek do oficjalnego Spotify Web Playback SDK.
//
// Zasady, ktorych trzeba sie tu trzymac:
// - AMC wysyla tu tylko identyfikator utworu (spotify:track:...) i krotko zyjacy
//   token konta. Chronione adresy audio nigdy nie wracaja do aplikacji.
// - Kazde polecenie ma numer (serial). Zdarzenie od POPRZEDNIEGO utworu nie moze
//   wplywac na biezacy - inaczej po szybkiej zmianie utworu czytnik przeczyta
//   czas i tytul nie tego, co gra.
// - Postep raportujemy co 500 ms, tak samo jak mostek TIDAL, zeby czas w AMC
//   szedl rownomiernie, a nie skokami.
//
// Wstrzykiwanie zaleznosci (Sdk, host) pozwala testowac kolejnosc polecen i
// bledy bez konta Spotify i bez sieci.
export function startSpotifyBridge(Sdk, host, schedule = setInterval, now = () => performance.now(), wallNow = () => Date.now()) {
  let credentials;
  let player;
  let deviceId;
  let active;
  let commandQueue = Promise.resolve();
  let latestControlSerial = 0;
  const send = message => host.chrome.webview.postMessage(message);
  const sendFor = (request, message) => send({
    ...message, trackUri: request.trackUri, requestVersion: request.version,
  });
  const isCurrent = request => request && active === request &&
    request.serial === latestControlSerial && !request.terminal;

  function replaceCredentials(value) {
    if (!value?.token?.trim() || !Number.isFinite(value.expires) || value.expires <= wallNow()) {
      credentials = undefined;
      throw new Error('Nieprawidlowe lub wygasle logowanie Spotify (invalid_token).');
    }
    credentials = { token: value.token, expires: value.expires };
  }

  function reportFailure(error, request = active) {
    if (!request || request.serial !== latestControlSerial || request.terminal) return;
    request.terminal = true;
    request.confirmed = false;
    sendFor(request, { type: 'error', ...usefulError(error) });
  }

  // SDK laduje sie sam i wola to, gdy jest gotowe. Tworzymy odtwarzacz raz.
  async function ensurePlayer() {
    if (player) return player;
    player = new Sdk.Player({
      name: 'Accessible Media Controller',
      getOAuthToken: callback => {
        if (!credentials) {
          reportFailure(new Error('Brak aktywnego logowania Spotify.'));
          return;
        }
        callback(credentials.token);
      },
      volume: 0.35,
      // Media Session API: system Windows dostaje tytul i przyciski sterowania.
      // NIE ma zwiazku z podcastami (sprawdzone w dokumentacji SDK) - jest tu,
      // bo dzieki temu sprzetowe klawisze multimedialne wiedza, co gra.
      enableMediaSession: true,
    });

    player.addListener('initialization_error', event => reportFailure(event));
    player.addListener('authentication_error', event => reportFailure(event));
    // Konto bez Premium: SDK nie zagra. Komunikat musi to powiedziec wprost,
    // bo "blad odtwarzania" nie mowi uzytkownikowi, co ma zrobic.
    player.addListener('account_error', () => reportFailure(
      new Error('Wbudowany odtwarzacz Spotify wymaga konta Premium.')));
    player.addListener('playback_error', event => reportFailure(event));

    player.addListener('player_state_changed', state => {
      if (!state || !isCurrent(active) || !active.confirmed) return;
      const grany = state.track_window?.current_track?.uri;
      if (grany && grany !== active.trackUri) return;
      sendFor(active, {
        type: 'state',
        state: state.paused ? 'NOT_PLAYING' : 'PLAYING',
        position: Number(state.position ?? 0) / 1000,
        duration: Number(state.duration ?? 0) / 1000,
      });
      // Spotify nie ma zdarzenia "ended". Koniec utworu poznajemy po tym, ze SDK
      // zatrzymalo sie na koncu przy wylaczonym automatycznym przejsciu.
      const position = Number(state.position ?? 0);
      const duration = Number(state.duration ?? 0);
      if (state.paused && duration > 0 && position >= duration - 1200) {
        active.terminal = true;
        sendFor(active, { type: 'ended', reason: 'completed', duration: duration / 1000, position: position / 1000 });
      }
    });

    player.addListener('ready', event => {
      deviceId = event?.device_id;
      send({ type: 'ready' });
    });
    player.addListener('not_ready', () => { deviceId = undefined; });

    const connected = await player.connect();
    if (!connected) throw new Error('Nie udalo sie podlaczyc odtwarzacza Spotify.');
    return player;
  }

  // Zaladowanie konkretnego utworu idzie przez Web API na nasze wlasne
  // urzadzenie SDK. Sam SDK nie ma metody "zagraj ten utwor".
  async function playTrack(trackUri, positionSeconds) {
    if (!deviceId) throw new Error('Odtwarzacz Spotify nie jest jeszcze gotowy.');
    const response = await host.fetch(
      `https://api.spotify.com/v1/me/player/play?device_id=${encodeURIComponent(deviceId)}`,
      {
        method: 'PUT',
        headers: {
          Authorization: `Bearer ${credentials.token}`,
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          uris: [trackUri],
          position_ms: Math.max(0, Math.round(positionSeconds * 1000)),
        }),
      });
    if (!response.ok && response.status !== 204) {
      const tresc = await response.text().catch(() => '');
      throw new Error(`Spotify odrzucilo odtwarzanie (HTTP ${response.status}). ${tresc}`.trim());
    }
  }

  async function handleCommand(command, serial, queuedAt) {
    if (serial !== latestControlSerial) return;
    let request = active;
    if (command.type === 'play' || command.type === 'resume') {
      request = {
        trackUri: command.trackUri ?? active?.trackUri,
        version: command.requestVersion, serial,
        confirmed: false, terminal: false,
      };
    }
    try {
      switch (command.type) {
        // ZGLOSZENIE Michala 18.09.2026: "Spotify - cisza, czasu nie odtwarza".
        //
        // ZAKLESZCZENIE, ktore to powodowalo: AMC nie wysylalo polecenia "graj",
        // dopoki mostek nie zglosi gotowosci, a gotowosc ('ready') zglaszal
        // dopiero player.connect() wewnatrz ensurePlayer() - wolany TYLKO z
        // obslugi polecenia "graj". Obie strony czekaly na siebie, AMC mowilo po
        // 45 sekundach "odtwarzacz nie odpowiedzial". Cisza bez zadnego bledu.
        //
        // Dlatego AMC wysyla najpierw "prepare" (samo logowanie, bez utworu) -
        // to podlacza odtwarzacz i wyzwala 'ready'. Utwor idzie dopiero potem.
        // Poswiadczenia sa konieczne juz tutaj, bo SDK wola getOAuthToken przy
        // podlaczaniu, a nie przy odtwarzaniu.
        case 'prepare':
          replaceCredentials(command.credentials);
          await ensurePlayer();
          break;
        case 'play': {
          const queueMs = Math.max(0, now() - queuedAt);
          active = request;
          replaceCredentials(command.credentials);
          await ensurePlayer();
          if (!isCurrent(request)) return;
          await player.setVolume(Math.min(1, Math.max(0, command.volume / 100)));
          const loadAt = now();
          await playTrack(command.trackUri, command.position);
          if (!isCurrent(request)) return;
          request.confirmed = true;
          sendFor(request, { type: 'timing', queueMs, loadMs: Math.max(0, now() - loadAt), playMs: 0 });
          sendFor(request, { type: 'state', state: 'PLAYING' });
          break;
        }
        case 'resume':
          active = request;
          replaceCredentials(command.credentials);
          await ensurePlayer();
          if (!isCurrent(request)) return;
          await player.setVolume(Math.min(1, Math.max(0, command.volume / 100)));
          await player.seek(Math.max(0, Math.round(command.position * 1000)));
          if (!isCurrent(request)) return;
          await player.resume();
          if (!isCurrent(request)) return;
          request.confirmed = true;
          sendFor(request, { type: 'state', state: 'PLAYING' });
          break;
        case 'pause':
          if (active) { active.confirmed = false; active.serial = serial; active.version = command.requestVersion; }
          if (player) await player.pause();
          break;
        case 'stop':
          active = undefined;
          if (player) await player.pause();
          break;
        case 'seek':
          if (isCurrent(active) && player) await player.seek(Math.max(0, Math.round(command.position * 1000)));
          break;
        case 'volume':
          if (player) await player.setVolume(Math.min(1, Math.max(0, command.volume / 100)));
          break;
      }
    } catch (error) {
      // Blad polecenia "prepare" nie ma zadnego utworu, do ktorego mozna go
      // przypisac, a reportFailure takie zdarzenia pomija. Zglaszamy go wiec
      // wprost - inaczej nieudane podlaczenie odtwarzacza znow konczyloby sie
      // cisza az do wyczerpania limitu czasu, zamiast konkretnym komunikatem.
      if (command.type === 'prepare') {
        send({ type: 'error', ...usefulError(error) });
        return;
      }
      reportFailure(error, request);
    }
  }

  host.chrome.webview.addEventListener('message', event => {
    const command = event.data;
    const control = ['play', 'resume', 'pause', 'stop'].includes(command.type);
    const serial = control ? ++latestControlSerial : latestControlSerial;
    const queuedAt = now();
    commandQueue = commandQueue.then(() => handleCommand(command, serial, queuedAt));
  });

  schedule(async () => {
    if (!isCurrent(active) || !active.confirmed || !player) return;
    const state = await player.getCurrentState().catch(() => null);
    if (!state || !isCurrent(active)) return;
    sendFor(active, {
      type: 'progress',
      position: Number(state.position ?? 0) / 1000,
      duration: Number(state.duration ?? 0) / 1000,
    });
  }, 500);

  return { settled: () => commandQueue };
}

export function usefulError(error) {
  const candidates = [error?.detail?.error, error?.detail?.cause, error?.detail,
    error?.error, error?.cause, error].filter(Boolean);
  const first = (...keys) => {
    for (const candidate of candidates) {
      for (const key of keys) {
        const value = candidate[key];
        if (value !== undefined && value !== null && String(value).trim()) return String(value).trim();
      }
    }
    return '';
  };
  return {
    message: first('message') || 'Nieznany blad odtwarzacza Spotify.',
    code: first('status', 'code'), id: first('name'),
  };
}

// Dependency injection keeps ordering/error tests independent of a TIDAL
// account. Only the official Player module handles protected audio.
export function startBridge(Player, host, schedule = setInterval, now = () => performance.now()) {
  let credentials;
  let active;
  let commandQueue = Promise.resolve();
  let latestControlSerial = 0;
  const credentialsListeners = new Set();
  const send = message => host.chrome.webview.postMessage(message);
  const sendFor = (request, message) => send({
    ...message, productId: request.productId, requestVersion: request.version,
  });
  const isCurrent = request => request && active === request &&
    request.serial === latestControlSerial && !request.terminal;
  const sdkMatches = request => isCurrent(request) &&
    Player.getMediaProduct()?.productId === request.productId;

  function reportFailure(error, request = active) {
    if (!request || request.serial !== latestControlSerial || request.terminal) return;
    request.terminal = true;
    request.confirmed = false;
    sendFor(request, { type: 'error', ...usefulError(error) });
  }

  Player.setCredentialsProvider({
    bus(callback) {
      credentialsListeners.add(callback);
      return () => credentialsListeners.delete(callback);
    },
    async getCredentials() {
      if (!credentials?.clientId || !credentials?.token || !credentials?.userId)
        throw new Error('Brak aktywnego logowania TIDAL.');
      return credentials;
    },
  });
  // Same empty sender as the official manual SDK demo; no media interception.
  Player.setEventSender({ sendEvent() {} });
  Player.setStreamingWifiAudioQuality('HI_RES_LOSSLESS');
  Player.setAudioAdaptiveBitrateStreaming(true);

  Player.events.addEventListener('playback-state-change', event => {
    if (!sdkMatches(active) || !active.confirmed) return;
    sendFor(active, { type: 'state', state: event.detail?.state ?? 'NOT_PLAYING' });
  });
  Player.events.addEventListener('media-product-transition', event => {
    if (!isCurrent(active) || event.detail?.mediaProduct?.productId !== active.productId) return;
    const context = event.detail?.playbackContext;
    sendFor(active, {
      type: 'transition',
      duration: Number(context?.actualDuration ?? 0),
      position: Number(context?.assetPosition ?? 0),
      assetPresentation: context?.actualAssetPresentation ?? '',
      previewReason: context?.previewReason ?? '',
      quality: context?.actualAudioQuality ?? '',
      codec: context?.codec ?? '',
      sampleRate: Number(context?.sampleRate ?? 0),
      bitDepth: Number(context?.bitDepth ?? 0),
      bandwidth: Number(context?.bandwidth ?? 0),
    });
  });
  Player.events.addEventListener('ended', event => {
    // SDK uses this event for completion, failure AND skipping/resetting.
    // Only completed is a natural end. Read the event product, not a global
    // loaded ID or getMediaProduct(), which may already have been reset.
    if (!isCurrent(active) || !active.confirmed || event.detail?.reason !== 'completed' ||
        event.detail?.mediaProduct?.productId !== active.productId) return;
    const duration = Number(Player.getPlaybackContext()?.actualDuration ?? 0);
    const position = Number(Player.getAssetPosition() ?? 0);
    active.terminal = true;
    sendFor(active, { type: 'ended', reason: 'completed', duration, position });
  });
  Player.events.addEventListener('error', event => reportFailure(event));
  host.addEventListener('error', event => reportFailure(event.error ?? event));
  host.addEventListener('unhandledrejection', event => reportFailure(event.reason));

  async function handleCommand(command, serial, queuedAt) {
    if (serial !== latestControlSerial) return;
    let request = active;
    if (command.type === 'play' || command.type === 'resume') {
      request = {
        productId: command.productId ?? active?.productId,
        version: command.requestVersion, serial,
        confirmed: false, terminal: false,
      };
    }
    try {
      switch (command.type) {
        case 'play': {
          const queueMs = Math.max(0, now() - queuedAt);
          // Official load() already resets the player, in parallel with its
          // playback-info request. An extra awaited reset delays every switch.
          // Old ended/state events remain gated by product and confirmation.
          active = request;
          credentials = {
            clientId: command.credentials.clientId,
            clientUniqueKey: 'amc-tidal-player',
            expires: command.credentials.expires,
            grantedScopes: command.credentials.scopes,
            requestedScopes: command.credentials.scopes,
            token: command.credentials.token,
            userId: command.credentials.userId,
          };
          for (const listener of credentialsListeners) {
            listener(new CustomEvent('credentials', {
              detail: { type: 'CredentialsUpdatedMessage', payload: credentials },
            }));
          }
          Player.setVolumeLevel(command.volume);
          const loadAt = now();
          await Player.load({
            productId: command.productId, productType: command.productType,
            sourceId: command.sourceId, sourceType: command.sourceType,
            referenceId: command.referenceId,
          }, command.position);
          if (!isCurrent(request)) return;
          const loadMs = Math.max(0, now() - loadAt);
          const playAt = now();
          await Player.play();
          if (!isCurrent(request)) return;
          request.confirmed = true;
          sendFor(request, { type: 'timing', queueMs, loadMs, playMs: Math.max(0, now() - playAt) });
          sendFor(request, { type: 'state', state: Player.getPlaybackState() });
          break;
        }
        case 'resume':
          active = request;
          Player.setVolumeLevel(command.volume);
          await Player.seek(command.position);
          if (!isCurrent(request)) return;
          await Player.play();
          if (!isCurrent(request)) return;
          request.confirmed = true;
          sendFor(request, { type: 'state', state: Player.getPlaybackState() });
          break;
        case 'pause':
          if (active) { active.confirmed = false; active.serial = serial; active.version = command.requestVersion; }
          await Player.pause();
          break;
        case 'stop':
          active = undefined;
          await Player.reset();
          break;
        case 'seek':
          if (isCurrent(active)) await Player.seek(command.position);
          break;
        case 'volume':
          Player.setVolumeLevel(command.volume);
          break;
      }
    } catch (error) {
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
  schedule(() => {
    if (!sdkMatches(active) || !active.confirmed) return;
    sendFor(active, {
      type: 'progress', position: Number(Player.getAssetPosition() ?? 0),
      duration: Number(Player.getPlaybackContext()?.actualDuration ?? 0),
    });
  }, 500);
  send({ type: 'ready' });
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
    message: first('userMessage', 'message') || 'Nieznany błąd odtwarzacza TIDAL.',
    code: first('errorCode', 'code'), id: first('errorId', 'name'),
  };
}

import * as Player from '@tidal-music/player';

let credentials;
let loadedProductId;
let commandQueue = Promise.resolve();
let latestControlSerial = 0;

const credentialsListeners = new Set();
const credentialsProvider = {
  bus(callback) {
    credentialsListeners.add(callback);
    return () => credentialsListeners.delete(callback);
  },
  async getCredentials() {
    if (!credentials?.clientId || !credentials?.token) {
      throw new Error('Brak aktywnego logowania TIDAL.');
    }
    return credentials;
  },
};

// The official player requires an event sender. This bridge uses the same
// intentionally empty sender as TIDAL's manual SDK demo. AMC never resolves,
// decrypts or exposes protected media addresses itself.
const eventSender = {
  sendEvent() {},
};

function send(message) {
  window.chrome.webview.postMessage(message);
}

function usefulError(error) {
  const candidates = [
    error?.detail?.error,
    error?.detail?.cause,
    error?.detail,
    error?.error,
    error?.cause,
    error,
  ].filter(Boolean);
  const first = (...selectors) => {
    for (const candidate of candidates) {
      for (const selector of selectors) {
        const value = selector(candidate);
        if (value !== undefined && value !== null && String(value).trim()) {
          return String(value).trim();
        }
      }
    }
    return '';
  };
  return {
    message: first(value => value.userMessage, value => value.message) ||
      'Nieznany błąd odtwarzacza TIDAL.',
    code: first(value => value.errorCode, value => value.code),
    id: first(value => value.errorId, value => value.name),
  };
}

function reportFailure(error) {
  const failure = usefulError(error);
  send({ type: 'error', ...failure, productId: loadedProductId ?? '' });
}

Player.setCredentialsProvider(credentialsProvider);
Player.setEventSender(eventSender);
Player.setStreamingWifiAudioQuality('HI_RES_LOSSLESS');
Player.setAudioAdaptiveBitrateStreaming(true);

Player.events.addEventListener('playback-state-change', event => {
  send({
    type: 'state',
    state: event.detail?.state ?? 'NOT_PLAYING',
    productId: loadedProductId ?? '',
  });
});

Player.events.addEventListener('media-product-transition', event => {
  const context = event.detail?.playbackContext;
  send({
    type: 'transition',
    productId: event.detail?.mediaProduct?.productId ?? loadedProductId ?? '',
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

Player.events.addEventListener('ended', () => {
  send({ type: 'ended', productId: loadedProductId ?? '' });
});

Player.events.addEventListener('error', reportFailure);
window.addEventListener('error', event => reportFailure(event.error ?? event));
window.addEventListener('unhandledrejection', event => reportFailure(event.reason));

async function handleCommand(command, controlSerial) {
  switch (command.type) {
    case 'play': {
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
        listener(
          new CustomEvent('credentials', {
            detail: { type: 'CredentialsUpdatedMessage', payload: credentials },
          }),
        );
      }
      loadedProductId = command.productId;
      Player.setVolumeLevel(command.volume);
      await Player.load(
        {
          productId: command.productId,
          productType: command.productType,
          sourceId: command.sourceId,
          sourceType: command.sourceType,
          referenceId: command.referenceId,
        },
        command.position,
      );
      if (controlSerial !== latestControlSerial) break;
      await Player.play();
      break;
    }
    case 'resume':
      await Player.play();
      break;
    case 'pause':
      await Player.pause();
      break;
    case 'stop':
      await Player.reset();
      loadedProductId = undefined;
      break;
    case 'seek':
      await Player.seek(command.position);
      break;
    case 'volume':
      Player.setVolumeLevel(command.volume);
      break;
  }
}

window.chrome.webview.addEventListener('message', event => {
  const command = event.data;
  const controlsTimeline = ['play', 'resume', 'pause', 'stop'].includes(command.type);
  const controlSerial = controlsTimeline ? ++latestControlSerial : latestControlSerial;
  commandQueue = commandQueue
    .then(() => handleCommand(command, controlSerial))
    .catch(reportFailure);
});

setInterval(() => {
  if (!loadedProductId) return;
  const context = Player.getPlaybackContext();
  send({
    type: 'progress',
    productId: loadedProductId,
    position: Number(Player.getAssetPosition() ?? 0),
    duration: Number(context?.actualDuration ?? 0),
  });
}, 500);

send({ type: 'ready' });

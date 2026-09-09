import test from 'node:test';
import assert from 'node:assert/strict';
import { startBridge, usefulError } from '../src/bridge.js';

function fixture() {
  const messages = [], calls = [];
  const events = new EventTarget();
  const webview = new EventTarget();
  webview.postMessage = message => messages.push(message);
  const host = new EventTarget();
  host.chrome = { webview };
  let product, position = 0, state = 'NOT_PLAYING', tick;
  const emit = (type, detail) => events.dispatchEvent(new CustomEvent(type, { detail }));
  const player = {
    events, setCredentialsProvider() {}, setEventSender() {},
    setStreamingWifiAudioQuality() {}, setAudioAdaptiveBitrateStreaming() {},
    getMediaProduct: () => product, getPlaybackState: () => state,
    getAssetPosition: () => position,
    getPlaybackContext: () => ({ actualDuration: 60 }),
    setVolumeLevel: volume => calls.push(['volume', volume]),
    async reset() { emit('ended', { mediaProduct: product, reason: 'skip' }); product = undefined; position = 0; },
    async load(value, offset) { calls.push(['load', value.productId]); product = value; position = offset; emit('ended', { mediaProduct: product, reason: 'error' }); },
    async play() { calls.push(['play']); state = 'PLAYING'; emit('playback-state-change', { state }); },
    async pause() { state = 'NOT_PLAYING'; emit('ended', { mediaProduct: product, reason: 'skip' }); },
    async seek(value) { calls.push(['seek', value]); position = value; },
  };
  const bridge = startBridge(player, host, callback => { tick = callback; });
  const post = command => webview.dispatchEvent(new MessageEvent('message', { data: command }));
  const play = (id = 'one', version = 1) => post({
    type: 'play', productId: id, requestVersion: version, position: 0, volume: 0.2,
    credentials: { clientId: 'test-client', token: 'test-token', userId: 'test-user', scopes: [] },
  });
  return { messages, calls, emit, player, post, play, settle: bridge.settled, tick: () => tick(), end: (reason = 'completed') => { position = 60; emit('ended', { mediaProduct: product, reason }); } };
}

test('reset/load ended cannot consume a track; confirmed natural end is emitted once', async () => {
  const f = fixture(); f.play(); await f.settle();
  assert.equal(f.messages.filter(m => m.type === 'ended').length, 0);
  f.tick(); f.end(); f.end();
  assert.equal(f.messages.filter(m => m.type === 'ended').length, 1);
  assert.deepEqual(f.messages.at(-1), { type: 'ended', reason: 'completed', duration: 60, position: 60, productId: 'one', requestVersion: 1 });
});

test('SDK ended reason=error/skip or the wrong product never advances even after playback started', async () => {
  const f = fixture(); f.play(); await f.settle();
  f.end('error'); f.end('skip'); f.emit('ended', { mediaProduct: { productId: 'old' }, reason: 'completed' });
  assert.equal(f.messages.filter(m => m.type === 'ended').length, 0);
  f.end(); assert.equal(f.messages.filter(m => m.type === 'ended').length, 1);
});

test('NotAllowedError after a premature PLAYING event fails once, never ends', async () => {
  const f = fixture();
  f.player.play = async () => {
    f.emit('playback-state-change', { state: 'PLAYING' });
    throw new DOMException("play() failed because the user didn't interact with the document first.", 'NotAllowedError');
  };
  f.play(); await f.settle(); f.end(); f.tick();
  assert.equal(f.messages.filter(m => m.type === 'state').length, 0);
  assert.equal(f.messages.filter(m => m.type === 'ended').length, 0);
  assert.equal(f.messages.filter(m => m.type === 'error').length, 1);
  assert.equal(f.messages.at(-1).id, 'NotAllowedError');
});

test('a superseded load rejection does not fail or start the replacement track', async () => {
  const f = fixture(); let rejectLoad, entered;
  const loading = new Promise(resolve => { entered = resolve; });
  const original = f.player.load;
  f.player.load = (value, position) => value.productId === 'one'
    ? new Promise((_, reject) => { rejectLoad = reject; entered(); })
    : original(value, position);
  f.play('one', 1); await loading;
  f.play('two', 2); rejectLoad(new Error('old network failure'));
  await f.settle(); f.tick();
  assert.equal(f.messages.filter(m => m.type === 'error').length, 0);
  assert.equal(f.messages.at(-1).productId, 'two');
  assert.equal(f.messages.at(-1).requestVersion, 2);
  assert.equal(f.calls.filter(c => c[0] === 'play').length, 1);
});

test('pause/stop suppress late ends; resume preserves position and volume', async () => {
  const f = fixture(); f.play(); await f.settle();
  f.post({ type: 'pause', requestVersion: 2 }); await f.settle(); f.end();
  assert.equal(f.messages.filter(m => m.type === 'ended').length, 0);
  f.post({ type: 'resume', requestVersion: 3, position: 12, volume: 0.7 }); await f.settle(); f.tick();
  assert.deepEqual(f.messages.at(-1), { type: 'progress', position: 12, duration: 60, productId: 'one', requestVersion: 3 });
  assert.ok(f.calls.some(c => c[0] === 'volume' && c[1] === 0.7));
  f.post({ type: 'stop', requestVersion: 4 }); await f.settle(); f.end();
  assert.equal(f.messages.filter(m => m.type === 'ended').length, 0);
});

test('reopening the same product uses a new version; old product transitions are ignored', async () => {
  const f = fixture(); f.play(); await f.settle(); f.play('one', 2); await f.settle();
  f.emit('media-product-transition', { mediaProduct: { productId: 'old' }, playbackContext: {} });
  assert.equal(f.messages.filter(m => m.type === 'transition').length, 0);
  f.tick(); assert.equal(f.messages.at(-1).requestVersion, 2);
});

test('SDK nested errors retain access-tier code rather than suggesting a new login', () => {
  assert.deepEqual(usefulError({ detail: { error: { errorCode: 'S3016', message: 'FULL_REQUIRES_HIGHER_ACCESS_TIER' } } }),
    { message: 'FULL_REQUIRES_HIGHER_ACCESS_TIER', code: 'S3016', id: '' });
});

# Draft: supported full-track playback and subscriber authentication in TIDAL Embed

Status: DRAFT — NOT SENT. Posting or contacting TIDAL requires the user's
separate approval. This document contains no account identifiers, credentials,
private collections, or raw logs.

We are developing Accessible Multimedia Controller (AMC), a Windows
keyboard-first player for blind users, with an NVDA-accessible native UI.
We use our own registered client and the official TIDAL SDK for metadata,
collections and playback. Playback currently yields previews.

We would like to use a supported route for full-track playback for subscribers,
without extracting audio URLs or using credentials/client IDs from other apps.

## Reproduction, 9 September 2026

1. Open https://embed.tidal.com/tracks/518279266 in Chrome on Windows.
2. Start playback. The catalogue initially shows 3:35, but playback is 0:30.
3. After the preview, use the Embed's Log in link and log in to TIDAL.
4. Reload Embed and play again. It remains a 30-second preview and shows the
   sign-up/login dialog again.
5. In the same browser profile, play https://tidal.com/track/518279266 on the
   official web player. Playback proceeds beyond 30 seconds (paused at 1:09
   of 3:35). Its current-quality indicator shows 16-bit / 44.1 kHz.

The Developer Terms describe full-length playback through Embed for subscribers.
In public embed-player commit 0f4fb0fe60981189e5150632a8df6168f3247dc6,
playback/init.js selects defaultCredentialsProvider. That provider does not
return user credentials. The separate authenticated provider appears to be
Nostr-specific. The ordinary finished-dialog login link opens tidal.com in a
new tab. We do not assume production runs this exact source revision.

## Questions

1. What is the currently supported ordinary subscriber login flow for full
   playback in Embed, without a Nostr extension or linked Nostr identity?
2. Should the above login link authenticate Embed, or does it only log in to
   the main web player? Is a required callback or documented configuration missing?
3. Is full playback supported in a visible, unmodified official Embed hosted
   by a Windows WebView2 app? What UI and login requirements apply?
4. Is there a supported host-control interface for play/pause, seeking, track
   changes, actual quality and playback status, suitable for an accessible
   native keyboard interface?
5. If the public Player SDK requires a higher access tier for full tracks,
   is there an application process available for a free open-source
   accessibility-focused desktop client?

References:

- https://developer.tidal.com/documentation/guidelines/guidelines-developer-terms
- https://developer.tidal.com/documentation/embeds/embeds-overview
- https://github.com/tidal-music/embed-player/tree/0f4fb0fe60981189e5150632a8df6168f3247dc6

Requested outcome: a documented integration route or a clear statement of the
current limitation, rather than instructions to reuse another application's
credentials or bypass playback restrictions.

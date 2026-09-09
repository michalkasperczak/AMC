# Sent: supported full-track playback and subscriber authentication in TIDAL Embed

Status: SENT on 9 September 2026, with the user's explicit approval.
Official developer forum, Q&A, posted by michalkasperczak:
https://github.com/orgs/tidal-music/discussions/384
Title: Supported full-track playback and subscriber login in Embed — accessible Windows client (AMC)
The published page and body were verified after submission. No account
credentials, private TIDAL identifiers, private collections, or raw logs were sent.
This file records the submitted question; do not post it again as a new discussion.
Updated on 9 September 2026 at the user's request to clarify AMC's accessibility
purpose and free, non-commercial nature. The saved discussion was verified after
the update. The accessibility rationale below is part of the published question.

We are developing Accessible Multimedia Controller (AMC), a Windows
keyboard-first player for blind users, with an NVDA-accessible native UI.
Project: https://github.com/michalkasperczak/AMC
We use our own registered client and the official TIDAL SDK for metadata,
collections and playback. Playback currently yields previews.

We would like to use a supported route for full-track playback for subscribers,
without extracting audio URLs or using credentials/client IDs from other apps.

## Accessibility purpose

AMC is free of charge, open source and non-commercial. It is being developed around the needs of blind and screen-reader users, with feedback from actual NVDA use. Its design goal is full keyboard-only operation, without requiring a mouse or visual navigation.

The native interface provides keyboard navigation through lists and collections, searchable commands, meaningful screen-reader labels and status announcements. Predictable focus and consistent shortcuts are especially important when browsing music, selecting a track and controlling playback. A supported full-track integration would let subscribers do these tasks in one accessible interface instead of repeatedly switching between the client and a separate web player.

Users would still authenticate with their own eligible TIDAL subscriptions. We are not requesting free access to subscription content or an exemption from playback protections. We are asking for a supported integration route that makes the service practical to use with assistive technology.

If there is an accessibility-team contact or a review process for non-commercial assistive applications, we would appreciate being directed to it.

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

Related discussion found before posting:
https://github.com/orgs/tidal-music/discussions/342. This question adds a
reproducible ordinary-Embed login comparison and an accessibility-specific
host-control question.

Requested outcome: a documented integration route or a clear statement of the
current limitation, rather than instructions to reuse another application's
credentials or bypass playback restrictions.

Thank you.

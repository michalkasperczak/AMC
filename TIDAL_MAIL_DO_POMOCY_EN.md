# Wysłany mail do pomocy TIDAL — 10 września 2026

Status: wysłano 10 września 2026 za wyraźną zgodą użytkownika. Gmail potwierdził etykietę SENT.
Prywatna korespondencja, nie publikować w repozytorium. Nie wysyłać ponownie.
Identyfikator wysłanej wiadomości Gmail: 1a08d21d6d8d8a1c.
Identyfikator wątku Gmail: 1a08cf597b167b7a.
Adres zweryfikowano na oficjalnej stronie pomocy TIDAL:
https://support.tidal.com/hc/en-us/articles/360007055198-Find-Tidal-App-Logs-on-Windows
Forum #384 przy sprawdzeniu przed przygotowaniem szkicu: Unanswered, 0 comments.

To: support@tidal.com

Subject: Accessibility-focused Windows client: only 30-second previews — request for developer support

Dear TIDAL Support,

I am a TIDAL subscriber developing Accessible Multimedia Controller (AMC), a free, open-source, non-commercial Windows application designed for blind and screen-reader users. It provides keyboard-only navigation and an NVDA-accessible interface for browsing collections and controlling playback.

Could you please forward this request to the Developer Platform team, or to the team responsible for accessibility integrations?

Our integration uses our own registered application, Authorization Code with PKCE, and the official TIDAL Web Player SDK hosted in Windows WebView2. Signing in, searching and synchronizing collections work. However, playback is limited to approximately 30-second previews.

The latest diagnostic example, recorded on 10 September 2026 at 14:33:50 local time in Poland, is "Hawkeye (2024 Remaster)":
- User credentials were supplied to and read by the SDK.
- Playback started successfully.
- The SDK reported PREVIEW with the reason FULL_REQUIRES_SUBSCRIPTION.
- The supplied duration was 29.976 seconds; no playback error was reported.

We understand that successful authentication does not itself prove playback entitlement. We need help determining whether this result is caused by an account entitlement, an application access limitation, or an integration requirement we have missed.

In a separate test on 9 September, the same account played https://tidal.com/track/518279266 beyond 30 seconds in the official web player. The corresponding official Embed remained limited to 30 seconds even after following its login link.

Could you clarify:
1. Is supported full-track playback available to an eligible subscriber through this kind of accessible desktop client?
2. Does our registered application need additional approval or access? If so, how can a free accessibility-focused project apply?
3. If the public Player SDK cannot provide full playback, is there a supported Embed authentication and host-control route for a keyboard-accessible Windows application?

We are not asking for free access to subscription content or to bypass playback protections. Users would sign in with their own eligible subscriptions.

I posted the technical question on your official developer forum on 9 September, but it has not yet received a reply:
https://github.com/orgs/tidal-music/discussions/384

Project:
https://github.com/michalkasperczak/AMC

I can provide the registered application's Client ID and redacted diagnostics privately if needed. No passwords, tokens or private logs are attached.

Thank you for helping us find a supported way to make TIDAL practical to use through an accessible, keyboard-first interface.

Best regards,
Michał
Accessible Multimedia Controller (AMC)

# AMC podcast module design

Status: implementation started in `alpha.203`. The real empty session, durable
data model, Library/New episodes/Downloads views and secure RSS/Atom parser are
in place. Network feed retrieval and subscription UI are the next stage.

## 1. A separate Podcasts session

Podcasts will be a separate session using AMC's shared player. Playback
position, speed, bookmarks, history, audio device and accessible announcements
remain common capabilities, but Podcasts will not impersonate Radio or Local
Files. Menus, the command palette and context menus must expose an operation
only where it is meaningful.

The session keeps the short name **Podcasts**, while accepted input is broader:
shows, episodes and one-off audio found on ordinary web pages. Every row keeps
an explicit user-facing kind instead of presenting all web media as a podcast.

## 2. User containers

- **Library** stores followed shows and their sources. Following a show does
  not automatically download all of its episodes.
- **New episodes** is an automatic inbox, ordered newest first, for unplayed or
  unreviewed episodes from the Library. It deliberately is not a manually
  ordered playlist. Episodes may be new, in progress, played or skipped.
- **Playlists** are durable, user-ordered episode collections and may combine
  different shows.
- **Downloads** lists files available offline. Removing a download does not
  remove its subscription or playback history.
- **History** is ordered from the most recently played episode. Delete removes
  only the history entry.

## 3. Sources and import

AMC does not depend on one directory. Discovery tries, in order: direct
RSS/Atom, an Apple Podcasts catalog result or catalog page resolved to its
public feed, a feed advertised by an ordinary page, embedded page audio, a
publisher-specific adapter (initially including Polish Radio), and finally an
explicitly invoked yt-dlp adapter. Standard RSS never depends on yt-dlp.

The result may be a show, one episode, or several detected audio items.
Ambiguous results are presented in an accessible chooser and nothing is added
or downloaded without confirmation. A standalone page item is placed in an
explicit **Web media** group rather than a fake podcast subscription.

Containers and extractors already used by the Chrome extension and media
converter will become replaceable input adapters. Fragile site-specific HTML
parsing must not enter the core. The core receives a normalized record only:
show title, episode title, author, date, description, duration, page URL,
media or manifest URL, and stable identity.

Deduplication checks feed GUID, enclosure URL, canonical URL and finally a
bounded metadata fingerprint. A CDN URL change must not by itself create a
duplicate, while genuinely different episodes with similar titles must not be
silently merged.

The Chrome extension already detects audio/video/source elements, RSS/Atom,
enclosures, Open Graph media, manifests and player resources, including Apple
Podcasts page lookup. The NVDA media converter already has bounded page parsing
and tested rules for Polish Radio and several regional radio sites. These rules
will be ported into isolated .NET adapters, not imported as Chrome- or
NVDA-dependent UI code.

## 4. Playback and downloads

An episode may stream or be explicitly downloaded. Position and speed are
stored per episode; played state is separate. Bookmarks behave like bookmarks
for durable local media. An incomplete download stays in local staging outside
cloud folders. Only a complete file is published to the user's destination,
using the same safe publication boundary as Radio recordings.

In the Podcasts session, Ctrl+D downloads selected episodes. It never downloads
an entire show archive from the show header without a separate range choice
and confirmation. Ctrl+C copies the title and public episode page; Ctrl+Shift+C
copies the title and direct enclosure/media address. On a show header the same
commands use its public page and RSS/Atom URL respectively. Temporary signed
URLs and credentials are not silently placed on the clipboard.

## 5. Privacy and resilience

Feed refreshes have time, size and redirect limits. HTML, feeds and network
metadata are untrusted data. AMC never executes them as commands, never puts
tokens in exports and never sends listening history to a source. An account
adapter stores credentials in the operating-system secret store and remains
separate from public-source adapters.

The public Apple catalog is used for discovery and resolving a public feed,
not for synchronizing a private Apple Podcasts account. Results are bounded and
cached. Other catalogs remain optional adapters and cannot become required for
an existing RSS subscription to work.

## 6. Delivery order

1. `alpha.203`: session shell, durable model, empty core views and RSS/Atom parser.
2. `alpha.205`: Ctrl+N adds a feed or media URL, Ctrl+O imports OPML, with bounded HTTP/HTTPS refresh and show/episode navigation.
3. `alpha.206`: finite HTTP playback, per-episode resume and speed, history and bookmarks.
4. `alpha.207`: Apple Podcasts catalog search and feed discovery from ordinary pages.
5. `alpha.208`: New episodes state, filtering and batch operations.
6. `alpha.209`: cancellable downloads with local staging and atomic publication.
7. `alpha.210`: episode playlists plus OPML and AMC import/export.
8. `alpha.211`: embedded web audio and publisher-specific adapters.
9. `alpha.212`: supplied and user-authored chapters.
10. Additional directories and account services remain optional adapters.

The keyboard map will be decided after the first working view. This design
document does not reserve shortcuts by itself.

Ctrl+F first returns shows. Enter opens a non-subscribing episode preview;
Backspace returns to shows and Escape closes search. Ctrl+Shift+L changes show
Library membership. Episode results retain the standard AMC queue, play-next,
favourite, playlist, playback, download and copy operations. Ctrl+K only
filters an already loaded list and never sends a network request.

`Ctrl+L` opens followed shows. Enter on a show will open its episodes rather
than trying to play the feed itself. Shared history, favourites, queue,
playlists, presets, copying and playback apply to episodes. Radio recording,
local-folder management and destructive local audio editing must not leak into
the Podcasts menus. The shortcut for the Downloads view and any numeric view
shortcuts will be selected after NVDA testing of the first populated lists.

Ctrl+I opens the **New episodes** inbox. Its options cover sorting, inclusion
of started/played episodes, the played threshold, refresh interval and whether
an individual show contributes to the inbox. Existing archive episodes remain
available inside a newly followed show but do not all become "new" by default.
The heading exposes episode count and the total known duration, marking it as
partial when some durations are unknown.

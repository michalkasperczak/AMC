# AMC podcast module design

Status: approved direction after Internet Radio has stabilised. This document
defines the data and interaction model; it does not claim that the module is
implemented yet.

## 1. A separate Podcasts session

Podcasts will be a separate session using AMC's shared player. Playback
position, speed, bookmarks, history, audio device and accessible announcements
remain common capabilities, but Podcasts will not impersonate Radio or Local
Files. Menus, the command palette and context menus must expose an operation
only where it is meaningful.

## 2. User containers

- **Library** stores followed shows and their sources. Following a show does
  not automatically download all of its episodes.
- **New episodes** is an automatic inbox, ordered newest first, for unplayed or
  unreviewed episodes from the Library. It is not a manually ordered playlist.
- **Playlists** are durable, user-ordered episode collections and may combine
  different shows.
- **Downloads** lists files available offline. Removing a download does not
  remove its subscription or playback history.
- **History** is ordered from the most recently played episode. Delete removes
  only the history entry.

## 3. Sources and import

RSS or Atom with podcast media is the first source type. AMC accepts a feed or
web-page URL and passes it to an isolated discovery adapter. The result may be
a feed, one episode, or several detected audio items. Ambiguous results are
presented in an accessible chooser and nothing is added without confirmation.

Containers and extractors already used by the Chrome extension and media
converter will become replaceable input adapters. Fragile site-specific HTML
parsing must not enter the core. The core receives a normalized record only:
show title, episode title, author, date, description, duration, page URL,
media or manifest URL, and stable identity.

Deduplication checks feed GUID, enclosure URL, canonical URL and finally a
bounded metadata fingerprint. A CDN URL change must not by itself create a
duplicate, while genuinely different episodes with similar titles must not be
silently merged.

## 4. Playback and downloads

An episode may stream or be explicitly downloaded. Position and speed are
stored per episode; played state is separate. Bookmarks behave like bookmarks
for durable local media. An incomplete download stays in local staging outside
cloud folders. Only a complete file is published to the user's destination,
using the same safe publication boundary as Radio recordings.

## 5. Privacy and resilience

Feed refreshes have time, size and redirect limits. HTML, feeds and network
metadata are untrusted data. AMC never executes them as commands, never puts
tokens in exports and never sends listening history to a source. An account
adapter stores credentials in the operating-system secret store and remains
separate from public-source adapters.

## 6. Delivery order

1. RSS/Atom, Library, New episodes, History and the shared player.
2. Downloads, offline files, durable position, speed and bookmarks.
3. Manual playlists and OPML/AMC import and export.
4. Page-URL discovery using the converter's proven containers.
5. Additional public directories and account services as separate adapters.

The keyboard map will be decided after the first working view. This design
document does not reserve shortcuts by itself.

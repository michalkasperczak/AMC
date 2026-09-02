# Music credits and catalogue metadata design

Status: approved direction. `alpha.164` adds safe search links to recognition
exports only; automatic matching and new shortcuts are not implemented yet.

## 1. Two distinct capabilities

- **Release and album** describes a particular edition: year, country, label,
  medium, catalogue number, track list and release-specific credits. Discogs is
  the primary additional catalogue.
- **Song credits** describe the underlying work independently of an edition:
  composer, lyricist, generic writer, arranger and possibly translator.
  MusicBrainz Work relationships are the primary open source.

Performer, composer and lyricist are separate roles and must never be collapsed
into one unexplained Author field.

## 2. Source priority

1. Durable identifiers: MBID, ISRC, ISWC, Discogs release ID and a service's
   native catalogue ID.
2. Explicit file tags such as Composer, Lyricist/Writer, Album, Album Artist,
   track number and date. AMC states that the value came from tags.
3. The active service's official catalogue when it exposes the exact role.
4. MusicBrainz recording-to-Work matching and composer, lyricist or writer
   relationships.
5. Discogs release, track list and edition-specific credits.

Artist and title without a stable identifier produce candidates, never a
silent match. AMC exposes source, confidence and differences and lets the user
confirm, reject or leave the result unresolved.

## 3. Accessible interaction

- `Alt+Enter` will add Credits, Album and release, Identifiers and Data sources
  sections after a match exists.
- Proposed `Ctrl+Shift+I` opens an accessible Track and release information
  window with MusicBrainz and Discogs candidates plus available service data.
  It returns to the information shortcut family with a new, explicit meaning:
  catalogue lookup and matching. `Alt+Enter` remains the properties already
  held by AMC.
- `Ctrl+D` remains Download. AMC must not use `Ctrl+Alt+D`, because it conflicts
  with the NVDA start command.
- Right Arrow on a track row may speak cached credits briefly. It never starts
  a hidden network request. Right Arrow on a folder or album still enters it;
  in the player it still seeks forward.

## 4. Network, accounts and cache

MusicBrainz reads do not require an account, but AMC must provide a meaningful
User-Agent, average no more than one request per second and cache responses.
Discogs exposes release data but database search requires authorization; its
token belongs in Windows Credential Manager. Without a token AMC may open the
ordinary Discogs web search only.

The cache stores responses, source, retrieval time and identifiers, never
passwords or tokens. Refresh is explicit and a catalogue outage cannot block
playback, lists or local tags.

## 5. YouTube Music

YouTube Music has no separate public catalogue API. `alpha.164` creates a safe
`music.youtube.com` search link. A later adapter may use the official YouTube
Data API for public video and playlist search and OAuth-authorized account
operations, then open the result in YouTube Music. AMC will not depend on
private undocumented application endpoints and does not promise full YouTube
Music library synchronisation.

## 6. Service-playlist matching

A recognized item first becomes a durable local record. Apple Music, Spotify,
TIDAL and YouTube Music adapters each return their own candidates. Only a
confirmed native identifier is added to a service playlist. An uncertain item
remains marked as requiring a choice and is never silently replaced by a song
with a similar title.

## 7. Neutral export and Library lookup

A later recognition stage must not limit a result to actions in streaming
services. Recognition history will gain a simple text export and at least one
structured format. A record should preserve the recognised track title,
artist, album, recognition time and source, plus identifiers and confidence
data when the provider supplies them. The exact text layout and final set of
formats will be decided after further recognition testing.

A single entry or selected group will also be searchable in AMC's own Library
by track, album or artist. A result without a durable identifier remains a
candidate: AMC never labels a local file as an exact match from a similar title
alone. This capability remains independent of the future Apple Music, Spotify,
TIDAL and YouTube Music adapters.

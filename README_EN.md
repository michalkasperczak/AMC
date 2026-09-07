# Accessible Media Controller — Windows prototype

Version `alpha.305` removes hidden seconds from schedule times entered in the
editor. Since the control exposes only hours and minutes, `22:00` now always
means exactly `22:00:00`. An explicitly immediate first run still begins
immediately instead of waiting for a whole-minute boundary.

Version `alpha.304` makes an empty Radio-schedule hour or minute field mean a
real zero. Clearing the selected value with Backspace can no longer leave the
previous numeric value behind invisibly when Enter saves the form. A zero total
is still rejected, while zero hours plus positive minutes is valid.

Version `alpha.303` keeps every stored recording-schedule duration unchanged,
but gives it an explicit duration label and full, naturally inflected Polish
hour/minute units in the Polish interface. Older minute values are still
converted exactly.

Version `alpha.302` replaces the Radio schedule's inconvenient total-minute
field with two accessible controls: **Hours** and **Minutes**. Existing plans
are split between these values automatically while persistence keeps the same
compatible data model. Zero hours is valid, but total duration must be at least
one minute. The schedule list now speaks long durations naturally, for example
“4 h 20 min” rather than “260 min”.

Version `alpha.301` prevents transactional audio-editing parts from leaking
into the Library. `.amc-cut-*` files are never indexed as ordinary recordings,
even when iCloud, OneDrive, Google Drive, or antivirus software briefly locks
their deletion. AMC retries cleanup after the handle is released and records a
persistent failure in its diagnostic log instead of silently ignoring it. The
complete `.amc-backup` safety copy remains intentionally preserved.

Version `alpha.300` makes manual control of an active scheduled recording
consistent. `T` keeps the schedule's folder, format, bitrate, and naming rule
when starting the next part. The first `R` stops and finalizes the current part
but remembers that occurrence until its planned end. A second `R` on the same
station resumes the schedule instead of starting a manual recording in the
global folder. This also handles a very quick double `R` while the first file
is still being finalized. If it is not resumed, the schedule advances when the
current occurrence ends. Every completed scheduled part is now also added to
the persistent **Recorded files** view.

Version `alpha.299` adds a **Recorded files** view under `Alt+Shift+R`. It can
be opened from Internet Radio or Local Files and switches to the Local Files
session, where it aggregates completed recordings from every configured
recording folder, newest first. These entries are ordinary Library files, so
playback, queue, favorites, playlists, copying, properties, bookmarks,
renaming, and deletion use the same commands as elsewhere. The index persists
without scanning or hydrating entire cloud folders. On first run AMC also
recognizes older files already indexed under a dedicated recording folder or
a schedule-specific folder. It deliberately does not backfill a shared podcast
download folder, because downloaded episodes cannot be distinguished reliably
from old radio recordings there. `Alt+R` continues to show active recordings
only.

Version `alpha.298` makes scheduled recording failures persistent. A failed
connection is logged and announced immediately; if it happened while AMC was
not active, it is announced again when the user returns. The latest dated
failure remains available on the schedule under `Ctrl+Shift+H`. Recurring
schedules keep their next occurrence, while a failed one-time schedule remains
in the manager in a disabled state for inspection or correction.

Version `alpha.297` adds `Ctrl+N` in the Radio session. It opens the accessible
**New radio station** form with **Station name** and **Stream or YouTube live
URL** fields. Saving adds the station to the Radio Library and selects it. The
existing `Insert` shortcut on the Library list remains available as an alias.

Version `alpha.296` simplifies the Radio Favorites export shortcut. In the
Radio session, `Ctrl+E` now exports an M3U playlist, while `Ctrl+O` continues
to import one. `Ctrl+Shift+O` keeps its separate meanings in Local Files and
WiiM.

Version `alpha.295` lets `T` start a new part of either a manual recording or
one started by a schedule. The command works in the recorded station's player
and in the **Active recordings** view. It does not disable the schedule or
change future occurrences; it finalises the current file and continues the
current capture in the next one. A manual split immediately before an automatic
part boundary cannot terminate the schedule.

Version `alpha.294` adds two portable workflows. In Radio, `Ctrl+E`
exports Favorites as an extended M3U in their current order; the file can be
imported again with `Ctrl+O` or used by another player. It contains only the
user-facing name and stable public address, never tokens, local identifiers,
or temporary playback URLs.

In Podcasts, `Ctrl+N` now also accepts a public YouTube video or live page.
The item is stored under **Internet media** without account sign-in, account
synchronisation, or browser cookies. AMC persists the stable page URL and
resolves a temporary audio URL only when playback starts. A finite item uses
the shared seeking, speed, History, Queue, and playlist features; `Ctrl+D` and
`Ctrl+S` save its audio as MP3 through isolated, updateable `yt-dlp` and FFmpeg
components. RSS/Atom remains independent and does not require either component.

Version `alpha.293` accepts a public URL of a currently live YouTube broadcast
in the Radio session. The stable page URL is stored like a station, while AMC
resolves it only for playback or recording, without signing in, reading browser
cookies, or persisting the temporary audio URL. This applies to listening,
manual background recording, and schedules. An ordinary video belongs in
Podcasts, where `alpha.294` stores it explicitly as **Internet media**.

The required `yt-dlp` executable is an isolated, replaceable component in
AMC's local application data. The updater retrieves the official Windows
binary and its published SHA-256 checksum, verifies both the hash and a bounded
version probe before activation, and reports FFmpeg and `yt-dlp` separately in
Settings and under **Help > Check updates and components**.

Version `alpha.292` makes automatic WiiM announcements even shorter. Changing
a stream or invoking a preset now announces only its name, for example
“Lublin”. It no longer adds the preset number or the obvious Play/Pause action.
The only automatically retained state is meaningful muting, for example
“Lublin, muted”. Full player information remains available on demand.

Version `alpha.291` shortens the announcement made when WiiM streams and
presets are changed. The focused playback button now primarily exposes the
content name and action, for example “357. Play”, without repeating the player
type, full device name, unknown state and volume. Muting remains explicit and
a preset invocation still includes its preset number. Full details remain in
the player and in on-demand information.

Version `alpha.290` improves accessible schedule-date editing. Up and Down now
announce a concise date together with its weekday: day changes use a numeric
day and month, month changes use the full month name, and year changes include
the full year. Left and Right still select and identify the edited segment.

Version `alpha.289` fixes `Shift+R` in Radio's **Active recordings** view. The
schedule editor keeps the focused recording's station selected, while its
station picker contains the complete Radio Library. The transient recording
status list no longer restricts the schedule scope.

Version `alpha.288` allows a clip to be marked from its end. In the player,
`O` may be pressed at the later position first, followed by rewinding and
pressing `I`. Each boundary persists independently for that file and survives
an AMC restart. Export or removal remains available only when the start is
actually earlier than the end; an invalid attempt does not discard a valid
boundary.

Version `alpha.247` adds contextual `Alt+Shift+Enter` options for a podcast
show and an individual episode: resume policy, speed, normalization,
transitions, and silence. A show can also use its own refresh interval and
download folder or inherit the Podcasts default. Radio may keep a separate
recording folder or explicitly reuse the Podcasts folder. Repeating Library or
Favorites on a directory search result now toggles the persisted show and
announces the correct action.

Version `alpha.246` removes a repeated podcast name from aggregate episode
lists when that same name is already part of the episode title. A distinct
author is retained, while the complete metadata remains available in
properties through `Alt+Enter`.

Version `alpha.245` rolls back the accessibility-layer changes from
`alpha.244` that could make NVDA navigation unstable and move its cursor to
help text or the status bar. Lists no longer receive long automatically exposed
keyboard descriptions, and plain `C` is no longer intercepted in the player.
The chapter list is again opened with `Ctrl+Alt+B`; Enter remains play or pause
only. Native WPF multiple selection remains available: `Shift+Arrow` selects a
range, `Ctrl+Arrow` moves focus, and `Ctrl+Space` toggles the focused item
without clearing other selections.

Version `alpha.244` introduced plain `C` and expanded list help text. It is
superseded by `alpha.245` because of the reported NVDA focus regression.

Version `alpha.243` generalises safe collection-state undo across all
sessions. Removing an item or changing its Library, Favorites, or Queue
membership from search results or a regular view is now tied to both the
session identifier and the item's stable identifier, rather than to a
temporary visible-row object. `Ctrl+Z` resolves the current record after a
refresh or view rebuild, restores state, ordering, and focus, and persists the
change through the correct session store. This is durable for Local media,
Radio, and Podcasts. Future TIDAL, Apple Music, Spotify, and WiiM adapters must
use the same contract, but may expose undo only after the remote API confirms
the original write.

Version `alpha.242` adds Spreaker as the second public Podcasts directory.
`Ctrl+F` searches Apple Podcasts and Spreaker concurrently, labels each
unsaved result with its source directory, and still verifies the actual RSS or
Atom feed before adding it. Failure of one directory does not hide results
from the other directory or the saved Library. `Ctrl+N` also accepts direct
public RSS feeds hosted by SoundCloud and Spreaker. AMC does not emulate a
SoundCloud-wide search by scraping pages because the official search API
requires a registered application and token.

Version `alpha.241` adds the first working chapter workflow for local media and
podcast episodes. In the player, `Ctrl+Alt+Shift+B` creates a named chapter,
`Ctrl+Alt+B` opens its chronological chapter list, and
`Ctrl+Shift+Left/Right` navigates chapter starts. Multiple selected chapters
can be played as a bounded selection; reaching its end never advances to the
next media item. A single chapter can be saved to a new file without modifying
the source. Downloaded episodes also inherit persistent `I`/`O` clip points and
non-destructive `Shift+X` export; a remote episode must first be explicitly
downloaded with `Ctrl+D`. Bookmarks and chapters may share one durable point,
while removing either role preserves the other.

Version `alpha.240` fixes undo after removing an entire podcast from the
Library. `Ctrl+Z` now restores both the visible item and the persisted
subscription record, so Enter can immediately open the restored show and its
retained episodes. Undo resolves the current item by its stable identifier and
therefore remains valid after the Podcasts list has been refreshed or rebuilt.
The restored state is queued for durable storage and the operation is recorded
in the diagnostic log without including podcast data.

Version `alpha.227` strengthens playback focus protection. AMC now validates
the real native Windows focus in addition to WPF's logical focus, preventing
the hosted status bar from silently taking keyboard and NVDA focus while WPF
still considers the player focused. The bar remains available through
`NVDA+End`, but no longer raises a redundant name-change event every second
that could move NVDA's navigator object. Detected focus loss is repaired and
recorded in the diagnostic log.

Version `alpha.226` fixes parent navigation in the Podcasts Library. `Ctrl+L`
may still restore the episodes of the last browsed show, but Escape or
Backspace from that list now always opens the parent show list and focuses the
correct show. It no longer depends on unrelated view-history entries. After
this explicit parent navigation, the next `Ctrl+L` remains at the parent level.

Version `alpha.225` adds three persistent orders to the **New episodes**
inbox: `Alt+1` puts newest episodes first, `Alt+2` sorts by episode title, and
`Alt+3` groups by show name with newest episodes first inside each group. The
third mode is not a custom order and never enables manual movement in this
automatic inbox. In Podcasts, `Ctrl+C` copies each selected episode's title,
full description and public page, while `Ctrl+Shift+C` copies direct audio
URLs only. Both commands cover a contiguous Shift selection and work in
search results as well.

Version `alpha.224` fixes Podcasts search-result navigation. A show result now
lands on that show in the top-level Library, while an episode lands inside its
parent show with that episode focused. The internal flat aggregate containing
every show and episode can no longer surface as a user-facing list; a legacy
saved reference to it is replaced with the last safe Podcasts Library
location. Search labels explicitly distinguish a Library show, an unfollowed
show, an Apple Podcasts directory result, and an episode with its parent show.
Plain Enter still leaves search and locates the result on the correct list; a
second Enter opens the show or starts the episode.

Version `alpha.223` repairs incomplete **New episodes** initialization left by
early Podcasts imports. For each followed show with no new entry, AMC seeds
only its newest untouched episode. It neither restores listened-to material
nor floods the Inbox with the full archive. `Ctrl+I` opens the stored Inbox
immediately, while `F5` fetches every followed feed. Refresh feedback now
separates entries discovered during that refresh from the total Inbox size,
and diagnostics name a feed that failed to refresh.

Version `alpha.222` preserves the list position when Escape returns from the
player to **New episodes**. If the episode is still new, focus returns to that
exact row. If listening made it leave the Inbox, AMC selects the nearest
episode at its former position instead of jumping to the first row. The same
safe fallback applies to other transient views and rows that indirectly wrap
the playable item.

Version `alpha.221` protects keyboard focus during playback. After an
asynchronous file, podcast, or radio update, AMC restores focus to the player
or current media list if WPF leaves it on a hidden or invalid element. Closing
the player's context menu now explicitly returns to the player. The guard does
not steal focus during Alt+Tab, an open menu, or an owned dialog. Actual
recoveries are recorded as `focus-recovery` diagnostics without adding NVDA
speech.

Version `alpha.220` fixes audio recovery after a previously selected output
device disappears. AMC retains the last safe position and, after the user
selects an available replacement, restarts the current item. An intentionally
paused or stopped session still remains silent during an ordinary device
change. Device recovery is logged without exposing the endpoint identifier.

Version `alpha.219` adds **Go to podcast** for an episode shown in New
episodes, Favorites, Queue, History, a playlist, search results or the open
player. The command opens the parent show, focuses the same episode and stores
that location for the next `Ctrl+L` Library return. It is exposed through the
relevant context menu and command palette without consuming a new default
shortcut.

Version `alpha.218` restores the exact location within the Podcasts Library.
After opening a show and selecting an episode, leaving for Queue, Favorites or
another view and pressing `Ctrl+L` returns to that show and episode instead of
the top-level podcast list. Backspace and Escape still deliberately move one
level up. The remembered location survives restart and safely falls back to
the Library when the show is no longer subscribed.

Version `alpha.217` adds public Apple Podcasts directory search without an
Apple login or private-library synchronization. In the Podcasts session,
Ctrl+F searches saved shows, episodes and bounded directory results. Opening a
directory result verifies its public RSS/Atom feed, adds the show and opens its
episodes. Alt+D opens a show or episode description as selectable read-only
text with usable links; descriptions are not spoken during normal list
navigation. Ctrl+I rebuilds the session snapshot before showing the Inbox.
Only the newest episode of a newly added show enters the Inbox, while later
refreshes mark every genuinely discovered episode as new. F5 in the Inbox
refreshes all followed shows.

Version `alpha.216` gives persistent collections one consistent sorting map.
In Library and Favorites, `Alt+1` selects added order with newest items first,
`Alt+2` selects alphabetical order, and `Alt+3` selects persistent custom
order. The choice is remembered independently for every view and session, and
`Alt+Up/Down` moves items only in custom order. Queues and playlists retain
playback/user order, albums retain track order, while Search, History, New
episodes and Recording now do not capture these shortcuts. Local Library keeps
its structural exception: `Alt+1` Folders, `Alt+2` flat alphabetical files and
`Alt+3` Custom order; local Favorites use the shared collection rule. A plain
digit can no longer be mistaken for `Alt+digit` after a rapid view change.

Version `alpha.207` fixes playback of remote Podcast episodes. The decoder now
normalizes MP3, MP4 and other finite HTTP/HTTPS media to the floating-point
format required by the shared playback-speed pipeline. The fix was verified
against real 44.1 kHz MP3 and 48 kHz MP4 episodes that failed before reaching
the audio output in `alpha.206`.

Version `alpha.206` delivered the first complete Podcasts subscription and
playback flow.
Within the Podcasts session, Ctrl+N verifies and follows a direct RSS/Atom feed,
Ctrl+O imports selected OPML feeds, F5 refreshes the current show and Ctrl+F5
refreshes the whole Library. Enter opens a show's episodes, Backspace returns,
Delete unfollows, Ctrl+C copies title and the public page when supplied by the
feed, and Ctrl+Shift+C copies title and the direct feed or enclosure URL. The client bounds time, redirects
and response size, rejects unsafe XML and never downloads episode audio during
refresh. Enter on an episode now plays its finite HTTP/HTTPS media in the shared AMC
player, which retains per-episode position and speed and integrates with
History and Bookmarks.

In the corrected OPML import, arrows only navigate, Space independently toggles
the current show and Ctrl+A includes all. Every Radio schedule row now begins
with its station name and uses that name for type-ahead; enabled or disabled is
announced immediately after it.

Every known menu shortcut is now also exposed through the UI Automation
accelerator field. **Show file in folder** selects a local or downloaded file
in Windows Explorer. A remote show or episode instead offers an explicit
browser-page command; AMC no longer guesses what “default application” should
handle the item. Ctrl+I opens **New episodes**.

Version `alpha.201` protects manual Radio recording splits from a rapid double press of `T`. The first press finalises the current part and immediately continues into a new file; another `T` during the first five seconds of that new part is safely ignored instead of stopping the recording. Every finalised part is added to the Local Library immediately while the next part continues recording. `Shift+T` keeps its existing meaning and is not a second split command.

Version `alpha.200` adds explicit removal of the marked interval from the original audio file through `Ctrl+X` in the player. The operation requires a confirmation whose default is No, stops playback, does not re-encode the audio, validates the completed result and only then replaces the source. A complete byte-identical copy receives an `.amc-backup` suffix; failure leaves the original unchanged. Cloud placeholders and video files are safely rejected.

Version `alpha.199` stores a separate fragment selection for every local file in the SQLite Library database. Start and end marks return after changing files or restarting AMC. `Shift+X` removes only the current file's selection; the source file remains untouched.

Version `alpha.198` adds directional navigation between marked fragment boundaries: `Alt+Page Up` moves to the previous cut point and `Alt+Page Down` to the next one. Boundary navigation does not wrap; `Shift+I` and `Shift+O` remain direct jumps to the start and end.

Version `alpha.197` installs and updates AMC's own separate FFmpeg 9 component for Windows x64. It uses the stable LGPL shared variant from a build provider linked by the official FFmpeg site, compares the archive with the release SHA-256 checksum, then validates the executable, version and licence variant before activation. Updates are versioned and performed in the background without removing the working copy on failure. `Shift+I` and `Shift+O` jump to the marked fragment start and end.

Version `alpha.196` adds the first safe recording-trim tool. In the Local Files player, `I` marks the start, `O` marks the end, `X` opens fragment export, and `Shift+X` clears the selection. The operation always creates a new file and never modifies its source. Exact WAV export has no extra dependency; stream-copy and exact FLAC become available when a suitable FFmpeg installation is detected.

The `alpha.195` correction separates two ways of using playback history. Explicitly opening an item from the `Ctrl+H` list makes the visible History the `Page Up/Page Down` context, just as Favorites, a folder, playlist or queue establish their own context. Temporary `Alt+Up/Down` history traversal inside the player still preserves the previously established list context.

The `alpha.194` correction separates the Radio listening position from live capture. Space can now pause and resume time-shift monitoring even while the same station is being recorded; the recorder keeps capturing live audio and `End` remains the explicit return-to-live command. Shazam fingerprint preparation now runs away from the window thread and reuses FFT buffers instead of allocating thousands of large arrays for every recognition, reducing short monitoring and UI stalls during automatic recognition.

This is the first demonstration prototype of the global-prefix media controller. It validates the keyboard, session, list, accessibility-message, profile, import and export architecture. It does not yet connect to real TIDAL or Apple Music accounts; WiiM uses its real local device API and requires no AMC account login.

This README describes the current prototype. Its single version number is stored in `Directory.Build.props`, so the core, Windows UI and published program always receive the same version. The approved development direction, target architecture and complete keyboard map are recorded in [`MEDIA_CONTROLLER_EN.md`](MEDIA_CONTROLLER_EN.md), while the next module is designed in [`PODCAST_MODULE_DESIGN_EN.md`](PODCAST_MODULE_DESIGN_EN.md). Permanent rules for successor selection after a file disappears and for manual-reorder announcements are collected as invariants in section 7.8 of that specification; future adapters and UI rewrites must not bypass them.

Starting with `alpha.165`, menus expose the command label and its shortcut to NVDA as separate UI Automation fields. The accessible name no longer contains a second copy of `Ctrl+…`, and submenu mnemonics are not spoken as stray single letters. This covers the main menu, list and player context menus, and search results; Alt access to the main menu categories is preserved.

Starting with `alpha.166`, the `Shift+R` form stores an independent format and bitrate for each one-off recording and schedule; older schedules without these fields inherit the global default. The schedule list behaves like a checklist: arrows choose an entry and Space enables or disables it. `Alt+2` shows active recordings, `Shift+Space` pauses the selected recording, and `Ctrl+Alt+R` stops the selected station, including a scheduled occurrence. `Ctrl+Alt+Shift+R` stops all recordings, while `Ctrl+Shift+H` is the sole schedule-list shortcut. Time-shift is locked only while the currently heard station is being recorded and becomes available immediately after capture stops. Every station retains its own listening volume. Recognition history under `Ctrl+Alt+S` also has a context menu for opening, copying, exporting and deleting entries.

Starting with `alpha.167`, **Settings > Messages > Announce automatically recognised tracks** separates Shazam monitoring from speech. When disabled, `Shift+S` monitoring continues to recognise tracks and store them in history without interrupting the user. When enabled, an automatic result is spoken only while the AMC window is active. Manual `S` still answers in the active window; if focus moves to another application before the request finishes, the result is stored silently. The command palette also exposes the current setting state.

Starting with `alpha.168`, **Settings > General > Playback** contains three options for local files, all disabled by default. Loudness normalisation adjusts perceived level in real time without changing the file or the user's volume and limits peaks before clipping. A one-and-a-half-second smooth transition fades a natural ending and introduces the next track; a manual change may briefly overlap the two pipelines. An explicit post-track silence choice ranges from no extra silence to five seconds and applies only after a natural end, never to pause, a manual change or Radio. The command palette reports each current value and opens the corresponding accessible control without exposing internal identifiers to NVDA.

Starting with `alpha.169`, the same controls are available directly from the **Playback** menu and the Local Files player's context menu. Loudness normalisation and smooth transitions are checkable commands whose labels report their current state, while the silence submenu selects an exact value. The default global-prefix profile adds `Shift+N` for normalisation, `T` for transitions and `C` to cycle silence; custom profiles retain their own mappings and can assign the new commands in Settings. Shortcut and command label remain separate UI Automation fields so NVDA does not repeat them.

Starting with `alpha.170`, a downloaded and pinned iCloud file is distinguished from an online-only placeholder. A local payload no longer receives five-minute cloud timeouts and can use the managed MP3 fallback. If Media Foundation cannot resume an existing pipeline after Help closes, a pause, or a return from the list, AMC discards that damaged pipeline and automatically reopens the file at the retained position. The watchdog also detects a pipeline that claims to be playing without advancing; near the duration boundary it supplies a missing end event, while an earlier stall starts recovery or stops safely with a user-facing message.

`Alt+Shift+Enter` for a local file or Library Folder now includes loudness normalisation, smooth transitions and post-track silence. Each value can inherit from the nearest folder and the global setting or provide an explicit override. Global values remain under **Settings > General > Playback — global settings** and in Playback menus whose labels now state that they are global. The speed list identifies `1.00 times — normal speed`, and every new data-bound choice exposes an intentional user-facing label to NVDA.

Starting with `alpha.171`, the active Local Files player has three direct shortcuts that require no global prefix: `Shift+N` toggles global loudness normalisation, `Shift+T` toggles global smooth transitions, and `Shift+C` selects the next global silence duration. They work only while focus is inside the local player and do not capture these keys on a list or in Radio. The player context menu shows the direct forms, while the main **Playback** menu and keyboard help continue to expose the existing global-prefix alternatives as well.

Starting with `alpha.172`, loudness normalisation, transitions and silence are exposed according to playback-output capabilities rather than a hard-coded session name. Local Files supports all three. A future service or device adapter may advertise each feature independently when it can implement it in AMC's local audio path or through an official API; the matching command then appears in **Playback**, the player's context menu and under `Shift+N`, `Shift+T` or `Shift+C`. An adapter that cannot perform an operation exposes no dead command. `Ctrl+Shift+S` is now the primary session-list shortcut, `Ctrl+0` remains an alias, and `Ctrl+Shift+0` still invokes preset 0. Shazam history remains on `Ctrl+Alt+S`.

Starting with `alpha.173`, the **Playback** menu and the local player's context menu contain a separate **Change current track options** entry. Its label reports the effective normalisation, transitions and silence together with the source of each value: the file, a folder or the global default. Global switches continue to report only the global level, so a folder override can no longer look like a failed save. The `Alt+Shift+Enter` confirmation reads back the saved values and no longer announces a false success if persistence fails. Automated coverage now includes SQLite round trips for file and folder options and the source selected for each inherited value.

Starting with `alpha.174`, direct `Shift+N`, `Shift+T` and `Shift+C` are captured at the Windows-message boundary before WPF or a screen reader can lose the Shift state and interpret the letter as a button access key. The command runs once, announces the new global state and leaves focus on the current player control. Player buttons no longer contain hidden single-letter mnemonics, so an unsupported combination cannot move focus to Back 10 seconds or Next either. These shortcuts remain limited to a player whose output advertises the corresponding capability; a file or folder override may still take precedence over the global value that the shortcut changes.

Starting with `alpha.175`, Radio has a coherent pair of numbered views: `Alt+1` opens **All stations**, the saved-station Library also available through `Ctrl+L`, while `Alt+2` opens **Recording now**. Recording now is transient: Escape or Backspace returns to the exact previous Radio view, such as Favorites, together with its retained selection; `Alt+Left/Right` continues to expose full view history. `Alt+3` deliberately remains unassigned until a real multi-device or Connect adapter can provide a list of simultaneous playback targets. `F6` remains the correct shortcut for the single current player.

Starting with `alpha.190`, the transient **Recording now** list is opened with `Alt+R`, not `Alt+2`. Escape or Backspace closes it and restores the exact originating Radio view and selection, while `Alt+1` consistently means **All stations**. Ordinary Space pauses audible monitoring only; reception, the time-shift buffer and independent recording continue. Without a capture, resume continues from the paused buffer position and End returns live. When the heard station is also being captured, Space resumes at live because manual time-shift is deliberately locked during that capture. `Shift+Space` remains the separate recording-pause command.

The `alpha.191` correction restores reliable saving and re-registration of the global prefix. Older settings could contain `CTRL-Alt-Win-F12`, while the current parser expected `+` separators; after leaving Keyboard Help, that prefix could not be registered again. AMC now normalizes the legacy spelling to `Ctrl+Alt+Windows+F12` both when loading and saving. A malformed value in imported settings no longer blocks startup or other data and safely falls back to the default prefix. An unavailable newly selected chord still leaves the previous prefix active.

The `alpha.192` correction shortens local-player history navigation: successful `Alt+Up/Down` now announces only the selected file name without repeating “History”. Empty-history and boundary messages remain explicit. Radio player help also distinguishes the three independent actions: Space pauses monitoring, `Ctrl+M` mutes audible output without pausing, and `Shift+Space` pauses recording only.

The `alpha.193` correction makes a single physical numpad key reliable as the global prefix. Windows could accept Numpad Plus through `RegisterHotKey` even while NVDA's keyboard hook consumed the physical input first. Every numpad digit and operator configured as the global prefix now uses AMC's existing exception-guarded hook; the accessible capture dialog still uses normal WPF input for unambiguous operators. `Pause` is now fully mapped as a supported prefix key. Diagnostics record the selected registration path and startup failures.

Starting with `alpha.176`, the `Shift+R` form contains an editable **Recording file name template**, a result preview and a keyboard-accessible **Insert token or choose template** menu. The default `{stacja} - {data} {czas}` pattern produces a name such as `Radio Łódź - 2026-08-31 20-15.mp3`; AMC always appends the extension that matches the schedule's recording format. Presets remain freely editable. Tokens cover the station, three date forms, year, month, day of month, Polish weekday name, time, hour, minute and a two-digit part number. Each schedule persists its own template, including recurring and segmented plans, while characters forbidden by Windows are safely replaced. Older schedules receive the default pattern and retain their previous naming behaviour.

Starting with `alpha.177`, Space in the `Ctrl+Shift+H` list actually toggles the selected schedule without opening its editor. Key handling now belongs directly to the list, and refresh restores focus to the same row instead of the generic **Scheduled radio recordings** container. NVDA receives an explicit notification containing the station, occurrence time and **enabled** or **disabled** state, while the row updates its checked state at the same time. The change still takes effect only after **Save**, allowing **Cancel** to discard it safely.

Starting with `alpha.178`, editable AMC text fields share one replace-on-entry rule. Keyboard focus selects the complete existing value, so the first typed letter or digit replaces it; pressing an arrow before typing instead allows a partial correction. A mouse click still positions the caret, and read-only fields are left unchanged. The same rule now covers the native numeric fields for total recording length and segment length in `Shift+R`, so typing `5` after entering a previous `60` produces `5`, not `605`. Date and time retain segment editing: typing replaces the selected day, month, year, hour or minute.

Starting with `alpha.179`, the global prefix is no longer typed into an ordinary text field. **Change prefix…** opens an accessible capture window where the first pressed combination replaces the complete previous value, while a separate button restores `Ctrl+Alt+Windows+F12`. AMC distinguishes the main Enter key from numeric-keypad Enter, so `Ctrl+numeric Enter` is a valid prefix. Saving probes the new prefix before releasing the old one. If Windows, NVDA or another program already owns that combination, Settings stays open, explains the conflict and keeps the previous prefix active. Capture UI and announcements expose user-facing names only, never command identifiers.

Starting with `alpha.180`, Shazam monitoring is persistent. **Settings > Radio and recording > Automatically monitor and recognise tracks while Radio is playing** and `Shift+S` control the same state and save it immediately; after restarting AMC, monitoring remains as the user left it. It may also be enabled before a station starts. The first attempt runs about six seconds after reception begins, and a missing result schedules another attempt after 15 seconds instead of waiting another full minute. The ordinary interval remains one minute after a successful match. The independent checkbox under **Messages** continues to control speech only.

Starting with `alpha.188`, **Radio and recording** settings provide three automatic-recognition scopes: the currently heard station, stations being recorded in the background, or both. `Shift+S` toggles monitoring for the stored scope, while manual `S` always targets only the heard station. A background recording is recognised from its existing private decoded-audio buffer, without opening a second station connection or changing the recorded file. The same station received by playback and recording is checked only once per cycle. `Ctrl+Alt+S` still opens one history, now with a station filter. “All sources” means only sources AMC is actually receiving, never the whole Radio catalogue.

Correction `alpha.189` prevents the shortcut-capture window from crashing on 64-bit Windows. Numpad Plus and other unambiguous keys now use the ordinary WPF path; the native hook remains only for Enter and keys that cannot otherwise be distinguished from the dedicated navigation block. The hook checks the message type before reading a key code, so pointer-sized focus and UI Automation messages cannot be mistaken for keyboard input. Both the capture window and the global-prefix hook also have a fail-safe boundary: an exception is logged, the prefix layer is cancelled and input is passed on. An AMC defect there cannot leave the keyboard-hook chain blocked or silence NVDA.

Starting with `alpha.181`, automatic persistence of playback positions,
history, Radio settings and other session state no longer performs a complete
SQLite transaction on the UI thread. One background queue stores immutable
snapshots and coalesces rapid changes while retaining the newest state; the
final snapshot is flushed during orderly shutdown. This keeps focus, arrow-key
navigation and playback responsive even when the local catalogue write is
delayed by storage, a security filter or a cloud provider. Shared source
classification distinguishes a local file, remote file and network stream, so
the off-UI rule covers iCloud, OneDrive, Google Drive, Dropbox, network shares,
and future session, streaming and download adapters. The schedule date field
remains segmented: Left and Right choose a date part, while Up and Down change
its value.

Starting with `alpha.187`, any standard numeric-keypad key can serve as the
global prefix, including without Control, Alt, Shift or Windows. This covers
digits, Enter, Plus, Minus, Multiply, Divide, decimal, separator and Num Lock.
With Num Lock off, AMC also distinguishes keypad Insert, Delete, Home, End,
Page Up, Page Down and the arrow keys from their dedicated navigation-block
counterparts. Settings and the capture window expose only user-facing names
to NVDA, such as “numeric Plus” or “numeric Insert”.

Starting with `alpha.115`, **Internet Radio** on the default `Ctrl+5` slot is AMC's first real network adapter. `Ctrl+F` searches the public Radio Browser directory; a result can be played or added to the radio's local Library or Favorites. Queue and Play Next are not part of the radio model, while playlists may group stations. `Insert` in the radio Library adds a custom station, while `F2` exposes two independent fields: station name and stream URL. The player keeps a bounded in-memory time-shift buffer: Home moves to its oldest available point, End returns to live, and `R` starts or stops an explicit background recording of the current station. `Ctrl+Alt+R` does the same from a list, `Alt+2` shows all actively recorded stations, and `Shift+R` opens timed recording or the scheduler. Direct HTTP/HTTPS streams and M3U, M3U8, PLS and XSPF lists are accepted; an HLS manifest remains a manifest for the decoder. Bookmarks, percentage jumps and playback speed are hidden for live radio because they have no durable broadcast meaning.

Starting with `alpha.116`, radio recordings are stored as MP3 instead of WAV. The target quality is 192 kbps; for an unusual sample rate the system selects the nearest supported bitrate. AMC encodes the decoded audio through Windows Media Foundation, so source MP3, AAC, OGG and other streams supported by the player follow one path without installing FFmpeg or a global codec pack. Recording first writes an AMC temporary file and publishes the `.mp3` name only after successful finalisation. Stop, station changes and application shutdown finalise the file; an encoder failure removes incomplete data without stopping radio playback.

Starting with `alpha.117`, legacy Shoutcast and Icecast servers which answer with `ICY 200 OK` have a bounded MP3 compatibility path. AMC keeps the normal system decoder as its first choice and reconnects through a small protocol client plus the managed NLayer decoder only when that path rejects the stream. This is not a station-specific exception: it also covers older direct Polish Radio endpoints, keeps normal HTTPS certificate validation, and strips ICY metadata from audio if a server sends it despite the request.

Starting with `alpha.120`, the legacy MP3 radio decoder correctly joins partial TCP reads. A network-packet boundary may occur inside an MP3 frame and does not mean that the station has ended. The regression test deliberately splits frames into small fragments, while live checks require multiple consecutive reads from Polish Radio One, Programme 2, Programme 4 and PR24. This prevents AMC from announcing that reception has started and then mistaking an ordinary packet boundary for the end of the broadcast a few milliseconds later.

Starting with `alpha.121`, the Radio Chopin AAC+ directory entry on port 8960 has a verified 192 kbps MP3 compatibility stream on port 8910. Polish Radio Programme 3 on AAC+ port 8954 has the corresponding MP3 port 8904, but on 28 August 2026 both legacy broadcaster endpoints returned `ICY 401 Service Unavailable`; AMC tries both and fails without freezing, but cannot play a source that the server has taken offline. A station is now announced as started only after the first decoded audio portion has arrived, not merely after its URL was opened.

Starting with `alpha.122`, closing a cancelled stream during a rapid station change is treated as the expected completion of its reception task. An exception caused by the buffer being released at the same time is no longer left as an unobserved task failure and cannot affect the next station or application shutdown.

Starting with `alpha.123`, a transient interruption of an already playing broadcast no longer closes the player immediately. AMC makes two reconnection attempts for the same station, checking its saved compatibility variant on each attempt, and accepts recovery only after actual decoded audio arrives. Time-shift, the output device and an active recording remain on the same pipeline when the recovered PCM format is compatible; an uncontrolled change of sample rate, channel count or encoding fails safely instead of passing invalid samples. Twenty seconds of stable reception resets the retry counter.

Starting with `alpha.125`, problematic direct radio streams use a separate BASS 2.4 decoder, which handles legacy Polish Radio ICY MP3 and Radio Emaus OGG particularly well. Ordinary stations retain the faster system decoder and BASS becomes its additional fallback; HLS stays on the existing path. A BASS failure or missing DLL automatically activates the managed compatibility paths. BASS cannot repair a source which is unavailable at the server. AMC remains a free, non-commercial project without ads, paid features or donations; AMC's source code is open, while the separate `bass.dll` retains Un4seen's proprietary licence and requires separate licensing for commercial use.

Starting with `alpha.126`, `Alt+1`, `Alt+2`, `Alt+3`, `F5` and `Ctrl+F5` are strictly local commands. In Radio or another service they cannot switch the application to Local Files, and their local View menu entries are hidden. No radio meaning is assigned to `Alt+1–3` yet; they remain reserved until real directory and station-order views are designed. Re-importing a playlist promotes a matching directory station into the Library instead of discarding it as a duplicate, preserving its richer name, codec and bitrate. HLS broadcasts containing video plus AAC have an isolated audio path through `ffmpeg` found beside AMC, through `FFMPEG_PATH`, or on `PATH`; the Jarosław and St John Vianney live feeds have been verified. A missing optional component fails cleanly. The eventual installer will provide a separately updated, licence-compatible component rather than install a global codec pack. The status bar retains its system role for `NVDA+End`, while its inner label remains the single source of current status text. Decoder-detected bitrate and sample rate are persisted; Left Arrow may perform one bounded metadata probe, but AMC never invents a value the stream does not reveal.

Starting with `alpha.127`, a batch action from search results uses one immutable selection snapshot. Favourites, Queue, Play Next and Library affect exactly the selected results and can never inherit a stale selection from the main list behind the search window. Duplicate rows for the same station are counted once by logical identifier. A selection spanning different services is rejected with a clear message because those collections belong to a specific session. Diagnostics store counts only, never station names or URLs.

Starting with `alpha.128`, BASS reads bitrate through `BASS_ATTRIB_BITRATE`, not the frequency attribute. Previously persisted impossible values such as `44100 kb/s` are discarded, while 44.1 kHz remains a separate sample rate. Playlist import with an exact matching URL promotes the existing directory record into the Library without replacing its name or richer metadata.

Starting with `alpha.129`, a real HLS manifest is tried before its known legacy MP3 fallback. In particular, the verified Programme 3 URL `https://stream13.polskieradio.pl/pr3/pr3.sdp/playlist.m3u8` is no longer preceded by the intermittent port 8904 endpoint. Radio hides Queue, Play Next, playlists, Albums, Bookmarks and other inapplicable commands from the main, list, player and search context menus and from the command palette; their shortcuts are blocked as well. A homogeneous station list no longer repeats “station” before the control's `8 of 48` position. In radio Favorites, `Ctrl+X` marks one station or a selected group and `Ctrl+V` places the whole block before the current station with one list refresh; `Alt+Up/Down` remains available for one-step adjustments.

Starting with `alpha.130`, ordinary Radio has twelve local AMC presets: `1–0`, `-` and `=`. The list is recall-only; Enter and Space start an occupied slot and can never overwrite it. Assignment accepts a slot key, Enter saves and Escape cancels. An occupied slot requires selecting the same key again before Enter; Delete followed by Enter removes only that assignment. `Ctrl+Shift+1–0/-/=` recalls an occupied slot directly. Starting with `alpha.135`, only `Ctrl+Alt+P` opens the list and `Ctrl+Alt+Shift+P` assigns a preset because `Ctrl+P` and `Ctrl+Shift+P` belong to playlists. Saving a preset keeps the station in the Radio Library, while clearing it changes neither Library nor Favorites. WiiM and future device presets remain separate source-owned collections.

The same release makes radio `Alt+Enter` contextual: Queue and Play Next are absent, while station identity, Library and Favorites state, audio parameters and links remain. Its read-only text retains character and word navigation, and a separate accessible link list opens the stream address or station homepage with Enter in an external application. Left Arrow performs a bounded MP3/AAC frame and HLS manifest probe; a declared HLS audio variant is reported without mistaking video bandwidth for radio bitrate. If a protected server refuses the independent probe, known 357 or Programme 3 source profiles provide an explicitly approximate fallback rather than presenting it as a measurement.

Starting with `alpha.131`, the preset list supports extended selection. `Ctrl+C` copies occupied station names, while `Ctrl+Shift+C` copies each name together with its stream address; empty slots are skipped and copying never changes an assignment. Local MP4, M4V, MOV, MKV, WebM, AVI, WMV, MPEG, M2TS and other recognised video containers enter the Library as media and play only their audio track without opening video. A basic AAC MP4 is covered by an automated check; less common codecs still depend on Windows Media Foundation support. The whole video file size is not reported as the audio-track bitrate. A decoder-reported 22050 Hz value is spoken precisely as `22.05 kHz`.

Starting with `alpha.132`, the status bar no longer has the extra accessible name “Playback status bar”. `NVDA+End` still reads its current content without repeating a container label. The Radio window title starts with the station name and appends the current programme or track when the active ICY/BASS stream actually exposes that metadata. The transient text is bounded, stripped of control characters and never saved as the station name. Switching, stopping or failing a station clears it immediately so late metadata from a cancelled stream cannot appear under another station. Streams or active decoders that expose no now-playing metadata leave the title at the station name.

Starting with `alpha.133`, preset positions are always identified by their numbers from 1 through 12. The list also states the actual shortcut, so the last three positions are unambiguous: “Preset 10, shortcut Ctrl+Shift+0”, “Preset 11, shortcut Ctrl+Shift+minus”, and “Preset 12, shortcut Ctrl+Shift+equals”. Starting Radio with Enter, a preset, Page Up, or Page Down now announces only the selected station name instead of repeatedly adding “Connecting” or “Playing”. Connection failures and timeouts remain announced.

Starting with `alpha.134`, the native status bar exposes one current value as one status-bar object. `NVDA+End` can find it again without repeating the text or adding a container label. `Settings > General > Playback` now contains “Focus the currently playing item after leaving the player”. It is enabled by default: after Page Up or Page Down changes playback, Escape, Shift+F6, or the Back to list button follows the playing item when it belongs to the displayed view. Radio also accepts the universal aliases `Ctrl+Alt+P` for the preset list and `Ctrl+Alt+Shift+P` to create or assign a preset; the existing `Ctrl+P` and `Ctrl+Shift+P` shortcuts remain available.

Starting with `alpha.135`, Internet Radio uses ordinary persistent AMC playlists just like other sessions. `Ctrl+P` opens station playlists and `Ctrl+Shift+P` changes membership for one or several stations, including from search results and the player. A playlist can be created, named, removed without deleting stations, and manually reordered. Presets are completely separate: `Ctrl+Alt+P` opens their list and `Ctrl+Alt+Shift+P` creates or assigns a preset. The shorter shortcuts are no longer preset aliases.

Starting with `alpha.136`, the preset list distinguishes a slot number from its physical key. The tenth entry says “Preset number 10, key 0”; the eleventh names the minus key and the twelfth the equals key. The status bar once again uses the standard Windows accessibility tree: it has the status-bar role and exactly one text child containing the current value, as expected by `NVDA+End`. The container has no extra accessible name, so the value should not be duplicated.

Starting with `alpha.137`, AMC presets also work in the **Local Files** session. `Ctrl+Alt+P` opens the active session's twelve slots from the folder list, flat Library, Favorites, or player. `Ctrl+Alt+Shift+P` assigns the selected file, the currently playing file, or a selected Library folder. `Ctrl+Shift+1–0/-/=` recalls a slot without switching sessions: a file starts playing, while a folder opens as the current Folders level. Local presets persist and are included in a full backup. Removing or detaching a target does not silently erase the assignment; recall reports that the target is unavailable.

Starting with `alpha.138`, pressing an unmodified slot key (`1–0`, minus, or equals) on the preset list moves focus directly to that slot without recalling or overwriting it. Direct `Ctrl+Shift+1–0/-/=` shortcuts are also caught at the Windows key-message boundary so that the framework or a screen reader cannot lose the `0` key. An empty slot recalled by shortcut uses the pressed key in its announcement, so `Ctrl+Shift+0` says “Preset 0 empty”; the list still identifies the same position unambiguously as “Preset number 10, key 0”.

Starting with `alpha.139`, presets are a shared capability of every AMC session. Each session owns twelve independent slots, so a TIDAL, Apple Music, WiiM, Radio or Local Files preset cannot overwrite another service and recalling a slot never changes sessions silently. A slot may target an ordinary item, a folder, an inferred local album or an AMC playlist; playable items use the other occupied presets of that session as their playback context, while containers open their contents. Assignment also works for one search result. Legacy Radio presets are migrated once into the shared store without replacing an already occupied shared slot. The operational component-update policy is recorded in [`AKTUALIZACJE_KOMPONENTOW.md`](AKTUALIZACJE_KOMPONENTOW.md).

This release also repairs startup context: the session list focuses the last-used session and the “Last used session” startup choice opens its remembered view directly. `Alt+Up/Down` works while the session-order list in Settings has focus, and every row exposes an explicit accessible label.

Since `alpha.77`, session order is editable under **Settings → General**. The same order controls `Ctrl+1–9`, the session list and `Ctrl+Page Up/Page Down`; starting with `alpha.115`, the default is Local Files, WiiM, TIDAL, Apple Music, Internet Radio. Opening a folder with `Ctrl+Shift+O` registers a persistent source and opens the one-level-at-a-time **Folders** list: Enter enters a folder or opens a file, Backspace goes to the parent level, type-ahead and `Ctrl+K` work within the visible level, while `Ctrl+F` searches the whole local session. The former separate list-reading tab is now the **List item reading** section of **Messages**.

Since `alpha.78`, the **Local Files** session exists from startup even when its library is empty, so `Ctrl+1` no longer resolves to an unassigned session. After choosing a directory with `Ctrl+Shift+O`, AMC immediately switches to the local Folders view and stays there while scanning instead of displaying another service's demonstration list.

Since `alpha.202`, lossless interval removal with `Ctrl+X` verifies the real FFmpeg packet timeline before and after the operation. Long MP3 recordings without a reliable Xing header may be reported by Windows as several seconds shorter than their actual content; AMC now preserves the real end of the file, validates the result, and only then atomically replaces the source while retaining the complete `.amc-backup` copy.

## Podcasts subscriptions in alpha 205 and playback in alpha 206

Podcasts are now a real sixth AMC session with no demo data. The session has a
separate Library and empty New episodes and Downloads views. The durable model
already covers subscriptions, episodes, source identity, dates, playback
position and speed, played/new state, downloads, favourites and queue state.
The bounded parser accepts RSS 2.0 and Atom, resolves relative enclosure URLs,
supports iTunes duration, skips entries without media and rejects DTD/external
entities. `alpha.205` adds verified direct RSS/Atom subscription with Ctrl+N,
selective OPML import with Ctrl+O, bounded HTTP/HTTPS refresh through F5 and
Ctrl+F5, and show-to-episode navigation. Refresh only retrieves metadata and
never downloads episode audio. `alpha.206` plays finite HTTP/HTTPS episodes,
retains per-episode position and speed and connects episodes to AMC's shared
History, Bookmarks, Queue and Playlists. See
[`PODCAST_MODULE_DESIGN_EN.md`](PODCAST_MODULE_DESIGN_EN.md) for the rollout.

Since `alpha.79`, a registered folder is unambiguously a **Library source**. `Ctrl+Shift+O` includes every recognised file from that folder and its subfolders in the flat Library, including known records that had previously been removed from it; **Folders** is only a hierarchical view of the same records. Delete on a file in Folders removes its Library membership while leaving the physical file visible in its real folder. Delete on a folder row removes nothing, while physically moving a file to the Recycle Bin still requires `Shift+Delete` and confirmation.

Since `alpha.80`, sources synchronize at startup, after file-system changes and on demand with `F5`. New files are added, missing files become unavailable without losing history, bookmarks or resume positions, and a file restored to the same path returns to the active Library. Delete creates a persistent exclusion, so rescanning or restarting cannot silently add the file again; immediate `Ctrl+Z` removes that exclusion. `Alt+1` opens **Library Folders**, `Alt+2` opens flat **All files**, and `Ctrl+L` returns to the most recently used Library layout. `Shift+digits` remains available for type-ahead names beginning with punctuation.

Since `alpha.81`, discovery distinguishes real directory links from Cloud Files placeholder files and directories. iCloud sources can therefore be indexed without opening and hydrating every recording, while symbolic links and junctions still cannot introduce traversal loops. A one-time migration repairs a source whose complete legacy catalogue was accidentally excluded by alpha.80. Switching from `Alt+2` to `Alt+1` now opens the selected file's actual directory and retains its selection; a standalone file outside registered sources maps to the Folders root.

Since `alpha.82`, fallback Cloud Files attribute detection also covers OneDrive when a provider rejects a link-target query. Mirrored Google Drive behaves like an ordinary folder, while streamed Google Drive is a source on a virtual drive: AMC indexes names without opening payloads, does not force bulk downloads, and retains records while Drive for desktop or its drive is temporarily unavailable. File watching is an enhancement; startup scanning and `F5` remain available for file systems that do not support it.

Since `alpha.83`, **File → Library folders** and the command palette open an accessible folder window. Its list announces reachability, active, unavailable and excluded file counts, and the full path. It can add a folder, rescan one or all folders, safely detach a folder, and export a complete AMC backup. Detaching disables future automatic synchronization only: it never deletes disk files, catalog records, favorites, queue state, history, bookmarks, or resume positions. New roots cannot duplicate, contain, or sit below another registered root. The existing `.amcbackup.json` format includes the local catalog, roots, exclusions and all of the user data above, while still excluding passwords and tokens.

The active working repository should reside on an ordinary local NTFS volume outside iCloud Drive, Google Drive, OneDrive and other synchronized directories. GitHub stores source history, while atomic user-data backups may be exported to cloud storage. On the primary test computer the canonical project path is `D:\Projekty Codex\Accessible Multimedia Controller`.

## Simplest way to run a published build

1. Open the `publish` folder.
2. Open the `AccessibleMediaController-<version>` folder with the highest version number.
3. Run `AccessibleMediaController-<version>.exe` inside it.

The published program is self-contained and includes the required .NET runtime. Keep the two SoundTouch libraries beside the EXE: they are deliberately separate, replaceable components under their licence. A user testing the ready build does not need to install the SDK or build the project.

## Build requirements

- Windows 10 or Windows 11;
- .NET 8 SDK installed by the person building the project;
- PowerShell 5.1 or later;
- NVDA, JAWS or Narrator for accessibility testing.

The project is built on Windows and checked with automated core smoke tests. Normal testing uses the ready EXE from the `publish` folder.

## Build and run

The commands below are for project contributors and people preparing a new published build.

From PowerShell in the project directory:

```powershell
dotnet build AccessibleMediaController.sln
dotnet run --project src/AccessibleMediaController.Windows
```

Run dependency-free core smoke tests:

```powershell
dotnet run --project tests/AccessibleMediaController.Core.SmokeTests
```

An optional guarded NVDA smoke-test client is available in `tests/accessibility/nvda`. It installs no add-on and accepts only a hardened, loopback-only, read-only bridge profile. It supplements rather than replaces manual and UI Automation testing.

Create a self-contained Windows x64 build including the .NET runtime:

```powershell
.\build.ps1 -Publish
```

The output is written to `publish\AccessibleMediaController-<version>`. The folder contains the EXE, two replaceable SoundTouch libraries, and licence information.

## Prototype defaults

The default global prefix is `Ctrl+Alt+Windows+F12`. It replaced earlier combinations that conflicted with NVDA or the Windows Narrator shortcut. The global layer remains experimental and alpha.13 introduces the approved key map. After the prefix:

- `1`, `2`, `3` select TIDAL, Apple Music and WiiM;
- `4–9` select later assigned sessions when available;
- `0` opens the session list;
- `Page Up` and `Page Down` select the previous or next session;
- Left and Right seek by 10 seconds;
- Up and Down change volume by 5%;
- `Ctrl+E`, `Ctrl+R`, `Ctrl+T` announce elapsed, remaining and total time;
- `U` and `Shift+U` open Favorites and toggle favorite state;
- `L` and `Shift+L` open Library and toggle membership;
- `P` and `Shift+P` open Playlists and manage membership;
- `Q` and `Shift+Q` open Queue and add or remove the item;
- `A` opens Albums while prefix then `Shift+A` remains unassigned (direct `Shift+A` selects the session audio output);
- `K` filters the current list and `Shift+K` opens the command palette;
- `F` searches the current service and `Shift+F` performs a global search;
- `D` downloads within a service and `Shift+D` is the experimental download-to-disk command.

While the AMC window is active, `Ctrl+1–9` selects a session without the global prefix, `Ctrl+Shift+S` opens the session list, `Ctrl+0` remains an alias, and `Ctrl+Page Up` or `Ctrl+Page Down` selects the previous or next session. Local view shortcuts are `Ctrl+U` for Favorites, `Ctrl+P` for Playlists, `Ctrl+L` for Library, `Ctrl+Q` for Queue and `Ctrl+Shift+A` for Albums, including while focus is in the filter box. Every arrow key retains native list behaviour in ordinary lists. Enter on a track or station starts it and opens the player; `Ctrl+Enter` toggles the selection without leaving the list. `F6`, the Now Playing command, or prefix then `N` opens the player without changing the selection.

In the player, Left/Right seeks by 10 seconds, Shift+Left/Right by 30 seconds, Ctrl+Left/Right by one minute, Up/Down changes volume by 5%, and Shift+Up/Down by 1%. `Shift+,` slows playback, `Shift+.` speeds it up, and `Ctrl+.` restores 1.00×. Available rates are 0.50–2.00× in 0.25 steps, with tempo changed independently of pitch. Home seeks to the beginning, End to 10 seconds before the end, and digits `0–9` seek to `0–90%` of the duration in 10% steps. `Ctrl+J` opens Jump to time: a number alone means minutes, `minutes:seconds` gives a precise position, and three parts mean `hours:minutes:seconds`. `Ctrl+Shift+J` opens the separate Jump to percentage dialog and accepts `0–100`. Both shortcuts and the digits work only in the player. `Ctrl+Shift+E/R/T` reports time, and Escape returns to the exact previous list and item. A playing list item starts with “Playing”, while one paused in progress starts with “Paused”. `Ctrl+K` focuses a filter that only narrows the already loaded list. `Ctrl+F` opens a window named concisely “Search TIDAL”, or the equivalent for the current service, while `Ctrl+Shift+F` opens “Search all services”. Enter submits the query and another Enter opens the selected result without automatically activating a single match. Search results also accept `Ctrl+Enter` to play or pause the selection, `Shift+Enter` for Queue, `Ctrl+Shift+Enter` for Play Next, `Ctrl+Shift+U` for Favorites and `Alt+Enter` for information. These direct actions keep the results window open and preserve result focus; their announcement always ends with the service name. Pressing plain Enter opens the result and moves focus to the main list. That one item's accessible name temporarily starts with the service name, so NVDA reads the service and item in one uninterrupted focus announcement. The same happens after global Search is closed with Escape following a direct action, even if the selected service was already active. Moving to another item removes the extra prefix. The native list exposes the result and its position without a “Search results” prefix or a separate live result-count message. Escape closes the window. `Ctrl+Shift+K` opens the accessible list of all AMC commands. Typing filters it immediately, Down moves to the results, Enter executes the selected command and Escape closes the palette. Names can be entered without Polish diacritics, and rows expose both the window shortcut and the active prefix-layer shortcut.

`Ctrl+O`, or **File → Open audio files**, adds one or more local files to the persistent **Local Files** session. `Ctrl+Shift+O`, or **File → Open folder with audio files**, registers a persistent Library source, includes recognised files from that folder and its subfolders, and opens the accessible Folders view. Neither command starts playback automatically, and loading the same path again does not create a duplicate.

Plain Enter performs the primary action: on a track or station it ensures the selection is playing and opens the player, while on an album, playlist or artist it opens the contents. If the current item is already playing, Enter only shows the player and does not pause it. `Ctrl+Enter` invokes **Play or pause** without opening the item: a new item starts, the playing item pauses, and the paused item resumes. Search remains open. `Space` controls only what is actually playing, independently of the current selection. A session has one playback pipeline, so a new track replaces the previous one; audio does not overlap. The main-window title starts with the current module, followed by the playing or paused item, the session and full AMC version, for example, “Queue — Broadcast — Local media — AMC…”. It does not follow an item that is merely focused in the browser. Merely opening a search result without starting playback does not change the current-track title. A Search window title describes its scope, for example, “Search TIDAL — AMC”. Homogeneous views do not repeat the resource kind: Albums omits “album” and Playlists omits “playlist”. Library and Favorites retain the kind because they may mix resources. Favorites belong to the current service or local library, while Queue belongs to the active playback session, so their ordinary rows do not repeat the service name. Starting with `alpha.26`, entering a view does not also raise a separate Status live-region summary. The first item receives a one-time view prefix such as “Albums, Strange”, while an empty list is named “Favorites, empty list”. This gives the screen reader one focus event instead of competing summary and list announcements.

Starting with `alpha.27`, the same rule applies when switching sessions from the media list with `Ctrl+1–9` or `Ctrl+Page Up/Page Down`. The slot and service become a one-time prefix of the selected item, for example “2, Apple Music, Green Horizon”, instead of a separate Status announcement. A duration in that label belongs to the selected track, album, or playlist; it is not a total for the current list.

Starting with `alpha.28`, submitted queries are kept in persistent history: separate for every service and for global Search. Each scope retains up to 20 unique entries with the newest first; reusing a query moves it to the front rather than adding a duplicate. When the search field is empty, Down selects the newest entry, further Down presses move to older entries, and Up moves toward newer entries and then back to an empty field. A query is retained even when it returns no results.

Starting with `alpha.29`, `Ctrl+Shift+K` opens a working command palette rather than a reserved placeholder. It includes commands with no dedicated shortcut, matches multiple name or shortcut fragments, and ignores case and Polish diacritics.

Starting with `alpha.30`, “Play or pause” uses the same toggling rule for `Ctrl+Enter`, the button and the context menu. `Space` independently controls current playback. The palette exposes window and prefix-layer shortcuts, and an invalid type-ahead continuation no longer strands the user on an empty command list.

Starting with `alpha.31`, the palette includes every active destination and operation in Settings. Entries for profiles, prefix configuration, list order, import, export, message templates and planned updates only open the correct tab and focus the relevant control. Only two safe options can be toggled directly: accessibility messages and detailed keyboard hints. Their labels always state the current value and the effect of Enter, and a short forced confirmation remains audible even when ordinary messages have just been disabled.

Starting with `alpha.32`, the event list on the **Messages** tab exposes only friendly names such as “Session changed”. Technical placeholders such as `{slot}` and `{service}` remain available in the separate template edit field where they are needed for customization.

Starting with `alpha.33`, the local session uses real Windows audio output. Enter and `Ctrl+Enter` start or pause the selected file, while Space, seek, volume and time commands control the same pipeline. `Alpha.34` introduced local time bindings, but NVDA testing showed that they did not reach the application reliably. In `alpha.35`, `Ctrl+E`, `Ctrl+R` and `Ctrl+T` were captured at the window-message boundary. In `alpha.36`, experimental arrow-key transport moved from the main list into the player so that every arrow retains its natural behaviour in ordinary lists. The implementation uses codecs available to Windows and shared output, installs no global codec pack and does not silence the screen reader. The local list currently lasts only until the application exits.

In `alpha.37`, Escape from the player continues to return to the last browsed position rather than the current track. The current track's state is visible and accessible without moving the selection. Window-level time shortcuts move to `Ctrl+Shift+E/R/T`; `Ctrl+E/R/T` after the global prefix remain unchanged.

In `alpha.38`, the **Messages** tab includes an **Announce position after seeking** option. Disabling it silences automatic time feedback after Arrow, Home and End seeking without disabling deliberate `Ctrl+Shift+E/R/T` time queries. `Ctrl+Shift+G` toggles the option at any time, and the command palette exposes its current state and the effect of Enter. The preference persists across application restarts.

In `alpha.39`, digits `0–9` in the player seek to `0%, 10%, …, 90%` of the track duration. Numpad digits work with Num Lock enabled. Digits keep their native item-navigation behaviour on ordinary lists, while `Ctrl+digit` still selects a session. Percentage seeking follows the seek-announcement preference; when duration is unknown, AMC reports that the action is unavailable instead of guessing.

In `alpha.40`, a digit seek announces only its percentage by default, for example “50%”; the exact position remains available through `Ctrl+Shift+E`. The **Messages** tab offers **Percentage only**, **Time only**, and **Percentage and time**. The existing toggle and `Ctrl+Shift+G` now cover automatic time and volume feedback. Disabling them does not silence playback, pause, errors, or explicit time commands.

In `alpha.41`, `Ctrl+Shift+G` is a reversible master mute for automatic player messages. It does not destroy the individual selection of four categories: digit seeking, Arrow seeking, volume, and playback/pause. Percentage announcements can therefore remain enabled while the other three groups are silent. Errors and explicit time queries stay audible. A real status bar at the bottom exposes the service, state, title, position and total duration, volume, and bitrate through `NVDA+End`. Local-file bitrate is an estimate from file size and duration; sources without that metadata say “unavailable”. The bar updates without automatically interrupting speech.

In `alpha.42`, the status bar is moved to the actual bottom edge because `NVDA+End` locates it there. Dynamic text is exposed by the direct UI Automation status-bar item. The release also adds `Ctrl+G` for Jump to time and a separate Jump to percentage command. Both are available from the Playback menu, player buttons and command palette.

In `alpha.43`, the WPF bar that NVDA still failed to locate in manual testing is replaced by a native Windows status-bar control. Exact time through `Ctrl+G` and exact percentage through the menu or palette work throughout the main window and affect the currently playing item. Digits `0–9` remain player-only so ordinary lists keep numeric item navigation. Invalid time or percentage input now raises an active accessibility notification while focus remains in the selected edit field.

In `alpha.44`, a compatibility target is added for the exact `NVDA+End` lookup. NVDA does not scan the control tree; it probes the object at the window's lower-left point, where WPF left the frame rather than the bar. The Windows layer now keeps an almost invisible native `msctls_statusbar32` object at that point and gives it the same changing text as the visible bar. It cannot receive focus, does not enter Tab or Alt+Tab, and follows window movement, resizing and maximisation.

In `alpha.45`, `Ctrl+J` means Jump to time and `Ctrl+Shift+J` means Jump to percentage. Both shortcuts and digits `0–9` work only in the player. `F6` opens the common player view for every local, streaming, radio and device session. A source with no known duration can still use playback and its other available functions, but not time- or percentage-based seeking.

In `alpha.46`, the almost invisible helper bar from `alpha.44–45` is withdrawn. Manual testing showed that, as a separate window, it could take over NVDA's context at startup, block reading and shortcuts, and disappear only after `Alt+F4`. The current version has no second status window and no helper object that can receive focus. The main window exposes its own client-area bounds to automation so an `NVDA+End` probe can reach the real embedded bar at the bottom. Regardless of that experiment's result, status can always be read safely through **Playback → Read playback status** or the command palette; the command does not move focus.

In `alpha.47`, only the status bar's inner label exposes the complete announcement. The container no longer repeats the same accessible name, so `NVDA+End` should read the content once. The order is optimised for quick listening: bitrate, state, position and total duration, volume, title, service.

In `alpha.48`, the status bar omits volume; its order is bitrate, state, position and total duration, title, and service. Volume remains available in the deliberately invoked “Read playback status” command. Helper announcements emit one UI Automation notification containing the real message. They no longer also raise a live-region event, which could intermittently make some screen-reader transitions announce the technical name “Program status”.

In `alpha.49`, local playback uses NAudio, shared-mode WASAPI and SoundTouch. `Shift+,` and `Shift+.` change tempo without changing pitch, while `Ctrl+.` restores normal speed. The value belongs to the local session and remains active when the track changes. Demonstration services without an audio output report that rate control is unavailable instead of pretending to change it. Shared output continues to coexist with NVDA. SoundTouch is shipped as two replaceable libraries beside the EXE, with notices and full licence texts in the same folder.

In `alpha.50`, the status bar begins directly with audio parameters such as “about 192 kb/s, 48 kHz”, without a redundant “bitrate” label. `Ctrl+I` opens item information and `Ctrl+Shift+I` reports full playback status. Their prefix-layer counterparts are `I` and `Shift+I`. Extended technical information stays in the menu and command palette without a fixed shortcut.

In `alpha.51`, the live status bar omits “about” and omits unavailable audio parameters. The old `Ctrl+I`, `Ctrl+Shift+I`, prefix `I` and `Shift+I` commands are removed. A single `Alt+Enter` opens the accessible **Properties and information** text window. Each session independently remembers its view, filter, selection and active player. Filter and search no longer open over the player, while Favorites, Library, Queue and playlist actions target the item that is actually playing. Escape returns to the list from which F6 most recently opened the player. The radio import/export plan covers M3U/M3U8, PLS, XSPF and VRadio Favorites; private source files are not committed.

In `alpha.52`, Queue consistently treats both an ordinary queued item and a Play Next item as present: repeating `Shift+Enter`, Delete, or the matching menu action clears both flags instead of accidentally adding the item again. `Shift+Up/Down` extends list selection; Favorites, Library, Queue, Play Next, playlist and Delete actions apply to the complete selection, and one `Ctrl+Z` undoes the whole batch. At natural end of a local file, playback first chooses a Play Next item, then Queue, then the following item in the loaded list, without wrapping the final item. Volume is now software gain inside AMC's own stream and must not alter NVDA or system volume. Session-switch focus feedback includes the restored view.

In `alpha.53`, `Alt+Enter` uses an ordinary accessible list instead of a read-only text field. Arrows move through individual rows, the first row begins directly with Title, `Ctrl+C` copies selected rows, `Ctrl+A` selects all, and the button still copies the complete sectioned text. `Ctrl+Shift+C` copies a local file's full path; a service adapter copies only its canonical public link, never a private or temporarily signed playback URL. `Alt+Left/Right` works throughout the main browser rather than only while the list has focus and announces the direction and destination view. View history remains experimental pending another manual test.

In `alpha.54`, `Alt+Enter` is a native read-only Windows text control. Arrow keys move the caret by character and line, `Ctrl+Arrow` moves by word, and the variants with `Shift` select an arbitrary text range; standard `Ctrl+A` and `Ctrl+C` work. Copy all announces completion and keeps the dialog open, while Escape or Close dismisses it. A complete local path appears immediately after the service name. The list and player context menus expose the matching item actions: Queue, Play Next, Favorites, Library, playlists, information, and copying the name or safe path/public link. Toggle action labels reflect current state.

In `alpha.55`, properties have three stable parts: item/source identity, **In application** state, and **Technical** data. Duration moves into the technical section beside format, size, bitrate and sample rate. Each context-menu item's accessible name contains its shortcut, so NVDA can report it both while moving through the menu and when the focused item is reviewed again, without duplication. `Alt+Left/Right` history is deliberately maintained per session and its feedback names the current service; it never changes sessions.

In `alpha.56`, history feedback is ordered as direction, destination view, session—for example “Back, Queue, Local media”. The Messages settings page can disable only the Back/Forward direction without disabling `Alt+Left/Right`; destination view and session still precede the item, for example, “Queue, Local media, Broadcast”. The same option is reachable from the command palette. The master accessibility-messages switch suppresses that context as well. Player automatic-message settings remain independent because they cover time, seeking, volume and playback rather than view navigation. NVDA's explicit review of the player object continues to include title, kind, service, state, rate, focused button and the short player instructions; this is an intentional overview of the player surface.

In `alpha.57`, the local library becomes persistent. AMC stores the file list, Favorites, Library and Queue membership, current file, volume, rate and a separate resume position for every file in `%AppData%\AccessibleMediaController\state.json`. Position is written at most once every 15 seconds and on clean shutdown; restoration never starts audio automatically. File size and modification time prevent an old position from being applied to replaced media. The application package remains portable for now, while user data stays independent from package replacement or updates. Files loaded in an older version must be opened once more on the first `alpha.57` run because earlier builds did not store the list.

In `alpha.58`, AMC is single-instance: launching it again brings forward the current window or deepest open dialog instead of starting another process that could write the same state. Search, Settings, Properties and command palette remain modal dialogs without separate taskbar buttons because they give NVDA a predictable focus boundary; the player is a main-window view, not another window. System-tray operation is reserved for an optional setting that will be off by default: minimizing may then hide the window, while `Alt+F4` continues to exit. The window title begins with the current item followed by module and session. `Page Up` and `Page Down` in the player start the previous or next item without wrapping at list boundaries. OGG/Vorbis uses NAudio.Vorbis/NVorbis, and a local position is additionally saved on pause, track change, typed seek and window deactivation.

In `alpha.59`, the session is named **Local files**, and the generic “Media” module is omitted from the title and added speech context when it would be redundant. On the main local list, `Left Arrow` speaks a compact set of cached technical details without a dialog, while `Right Arrow` opens the item action menu, including “Open in default app” and the Windows “Open with…” picker. `Ctrl+Shift+C` places both Unicode path text and the standard Windows file-drop list on the clipboard, so the same copy can paste a path into an editor or physical files into Explorer and Total Commander; multiple selection produces multiple paths and files. `Delete` on the main local list removes entries from AMC only, never from disk, and `Ctrl+Z` restores them. Removing every entry safely detaches the empty session; adding files or undoing the removal recreates it.

In `alpha.60`, Left and Right expose quick information and the action menu in every **Local files** view, not only the default catalogue. `Ctrl+Shift+E/R/T` works both on lists and in the player. Every session has persistent playback history under `Ctrl+H`; it is newest-first, contains no duplicates, and marks the last or paused item without moving focus at startup. In the player, `Alt+Down` selects an older played item and `Alt+Up` a newer one, restoring its remembered position. `Page Up/Down` still follows the source list, while `Alt+Left/Right` remains a separate in-memory view history. After explicit confirmation, `Shift+Delete` moves selected local files to the Windows Recycle Bin and removes them from AMC; `Ctrl+Z` cannot undo that physical operation. Plain `Delete` still leaves the disk untouched. `Ctrl+C` copies all selected names on separate lines, while `Ctrl+Shift+C` retains paths and Windows `FileDrop`. Session-change focus context now follows **slot and session → restored view → item**.

In `alpha.61`, Right Arrow on a local list no longer opens AMC's full context menu. It invokes the Windows **Open with…** picker directly, allowing a one-time choice such as foobar2000 or a default-app change when supported by the installed Windows version. `Shift+Delete` also works inside the open player: after confirmation it stops and releases the current file before handing it to the Recycle Bin. Removed identifiers are dropped from both persistent history and its active navigation snapshot. Saving no longer retains an orphaned last-item identifier after the entire local session is removed, and normalization cleans older stale entries. Moving to the Recycle Bin remains a synchronous Windows shell operation and can take a moment on an iCloud-backed drive.

In `alpha.62`, Left Arrow always attempts to include the local file's average bitrate. If playback has not stored metadata yet, AMC opens only the selected file for metadata reading, obtains duration and sample rate, estimates `kb/s` from size and duration, and persists the result. It does not scan the entire library. Other quick details remain available when a file is missing or cannot be decoded. `Alt+F4` now has one explicit Windows meaning: in the main window it closes the whole application even while the player view is visible. `Escape` and `Shift+F6` remain the commands for returning to the list.

In `alpha.63`, a restored position is visible and used before the audio engine first opens the file. Right Arrow invokes the Windows application picker directly even when the extension has no valid association. Physical `Shift+Delete` sends files to the Recycle Bin only from a list selection. In the local player, `Delete` removes the current AMC entry, leaves the file on disk, announces the next item and can be undone with `Ctrl+Z`.

In `alpha.64`, the system Open with action and its Right Arrow binding are removed because manual NVDA testing found that the picker lost readable focus. Right Arrow again retains the list's standard behavior. Open in default application remains available for correctly associated files. All other `alpha.63` changes, including position restoration and safe player `Delete`, remain intact.

In `alpha.65`, Open with returns for testing after correcting focus-event ordering. AMC completes Right Arrow or menu processing before it opens the Windows application picker. Separate tests of Right Arrow and the menu will allow at least the working entry point to remain if the issue is specific to the shortcut.

In `alpha.66`, after both `alpha.65` entry points failed, Open with is launched in a separate Windows shell process so the system can establish normal foreground focus outside the WPF thread. This is the final test variant; the feature will be removed if NVDA still cannot read the picker. Folders are now the primary planned local view, Bookmarks precede simple nondestructive A–B assembly, and the future NVDA controller remains a thin layer over the shared core.

In `alpha.67`, Open with is permanently removed after negative tests of all three variants. Right Arrow retains ordinary list behavior, while the local-file menu still contains stable Open in default application. Windows manages association changes outside AMC. All other functions and the `alpha.66` development plan remain unchanged.

In `alpha.68`, persistent Bookmarks are operational. In the open player, `B` stores the current position, `Shift+Page Up` and `Shift+Page Down` move among bookmarks in that same item, and `Ctrl+B` opens one global list across all sessions. Each row announces title, time and service; Enter selects the owning session, starts the item and seeks to the saved position. `Delete` removes only the bookmark and `Shift+Delete` is blocked in this view to prevent accidental file deletion. Bookmarks are stored in `state.json` and full `*.amcbackup.json` exports. Plain `B` retains type-ahead navigation on ordinary lists.

In `alpha.69`, repeated `Shift+Page Up` and `Shift+Page Down` presses navigate sequentially from the last reached bookmark even while playback advances. A separate “Announce bookmark navigation” setting under Messages silences the automatic time announcement without disabling the shortcuts. The no-earlier/no-later boundary remains audible, and navigation deliberately never crosses into another media item.

In `alpha.70`, `Ctrl+Shift+B` opens an accessible name field in the player and stores a named bookmark. If a quick bookmark already exists within the same second, it receives the supplied name instead of creating a duplicate. The `Ctrl+B` list announces the name first, before the media title, time and service, and includes it in filtering and type-ahead. Names persist in application state and full backups.

In `alpha.71`, the global list presents the current item's bookmarks first, ordered from the beginning to the end of the media. Remaining entries are grouped by session and media title, with chronological order inside each item. Enter on a bookmark focuses the player's main button directly, and Escape restores the same bookmark row. Player focus is scheduled at the loaded-view priority so it cannot remain on the hidden list or a heading.

In `alpha.72`, every row in the global Bookmarks list starts with the file or media title, followed by the bookmark's local creation date and its position in the media. An optional bookmark name remains part of the row. `Shift+Page Up/Down` navigation inside the open media is shorter: it announces only the position, or the name and position for a named bookmark. `Ctrl+C` in the Bookmarks list copies the visible descriptions of all selected bookmarks instead of only the source item's title.

The same release unifies clipboard actions in both search windows: `Ctrl+C` copies the title, while `Ctrl+Shift+C` copies the real file and full path or the service result URI. `Ctrl+X` on ordinary local lists and local search results puts real files on the Windows clipboard with the move effect. The shortcut itself never deletes data; the move happens only after `Ctrl+V` in a destination folder. When AMC regains focus, it removes records whose old paths actually disappeared. Cutting is unavailable in the Bookmarks list and for streaming items.

In `alpha.73`, the search-results list uses extended selection. `Shift+Arrow` selects multiple results, `Ctrl+C` copies their titles on separate lines, and `Ctrl+Shift+C` exposes local results as real files plus full paths while copying URIs for service results. The query-history entries remain ordinary text. `Ctrl+X` and `Ctrl+V` are blocked in search results.

`Ctrl+V` in local lists acts as an accessible alternative to drag and drop. In Media and Library it imports supported audio files into the AMC catalogue, in Queue it also queues them, and in Favorites it also marks them as favorites. Files stay in their existing folders—AMC records their real paths and creates no hidden copy. Paste is unavailable in Playback History, Bookmarks, the player and streaming sessions. An internal paste after `Ctrl+X` cancels the Windows move intent so a later accidental paste outside AMC cannot move the file unexpectedly.

In `alpha.74`, Bookmarks is a transient view that must be opened explicitly. `Ctrl+B` remembers the originating session, view and selected item, and Escape returns to that exact context. After opening a bookmark with Enter, the first Escape returns to the same bookmark row and the second returns to the list from which `Ctrl+B` was invoked. If the Bookmarks filter contains text, the first Escape clears the filter and leaves the view open. AMC no longer restores the Bookmarks list silently at startup; an older persisted Bookmarks state is normalized to the session's default list. `Shift+Page Up/Down` has two unambiguous scopes: in the player it moves among bookmarks of the current item, while on every ordinary list it retains standard extended page selection and never opens the Bookmarks view.

In `alpha.75`, Left Arrow announces concise item information both in ordinary lists and in search results. For a local file it includes available format, artist, duration, bitrate, sample rate and file size. A streaming session uses the same order, but AMC reports only metadata actually supplied by that service adapter and neither invents nor estimates stream parameters. Focus remains on the selected row.

In `alpha.76`, `Shift+Delete` uses the modern Windows Shell `IFileOperation` interface instead of the legacy deletion mechanism. This matters for placeholder files managed by iCloud Drive and other cloud providers. After confirmation, the operation still targets the system Recycle Bin exclusively and does not perform a permanent deletion. A failure for one file cannot close AMC or remove its catalogue record, and is announced with a diagnostic Windows error code. Other items that were recycled successfully are removed safely from the AMC catalogue.

In `alpha.84`, explicitly leaving the player with `Escape`, `Shift+F6`, or the **Back to list** button pauses audio by default. This rule can be disabled in General Settings. Starting with the `alpha.205` correction, navigating directly to Queue, Library, or another view is not treated as an explicit exit and preserves playback. Likewise, `Ctrl+digit` and `Ctrl+Page Up/Down` switch sessions without changing their audio. It is independent from resume-position storage: local files remember their position by default, while each Library folder can explicitly **Remember playback position**, **Always start from the beginning**, or **Follow the global setting**. Files added individually with `Ctrl+O` use the global rule. Ordinary lists retain standard arrow-key behaviour; seeking and volume remain player-only controls.

Starting with `alpha.215`, returning from the player no longer focuses the list container before its selected item. NVDA receives one deliberately ordered name: **item and playback state → list**, for example, “Radio 24, paused, list”. Focus still lands directly on the correct item and cannot fall into the filter field while the list is rebuilt.

In `alpha.85`, the source-manager list exposes only its prepared human-readable source label to NVDA. The technical record representation, identifier and property names are no longer surfaced through UI Automation.

In `alpha.86`, `Ctrl+F5` opens **Library folders** while `F5` refreshes those folders. On a local list, `F2` changes only the persistent title displayed by AMC and never touches the file. `Shift+F2` renames the real file on disk while preserving its extension, stable identity, Favorites, Queue, History, Bookmarks and resume position. Existing targets, invalid names and Windows reserved names are rejected without overwriting. If the file was loaded, AMC stops it and releases the handle before renaming while retaining the position for later resume.

In `alpha.87`, the local Library has three explicit layouts: `Alt+1` for **Library Folders**, `Alt+2` for **All files alphabetically**, and `Alt+3` for **Custom order**. Only Custom order accepts `Alt+Up/Down` to move one file or a contiguous Shift-selected block. The order is persistent, new files are appended, and the operation never changes disk folders, names or paths. An active filter blocks reordering. `Ctrl+K` filters only the current list; Escape clears it and returns to the list, while changing view, folder or session also clears it automatically. A filter is not restored after restarting AMC.

In `alpha.88`, `Ctrl+Shift+A` provides a real local **Albums** view even for collections without complete tags. A folder with at least two direct audio files carrying distinct leading track numbers such as `01`, `02`, `1 -` or `2.` becomes an album. Its directory name is the album title, while its immediate parent is the artist when the layout sits below a registered source. The album row reports its track count. Enter opens naturally numbered tracks and Escape returns to the same album. Four-digit date prefixes, a single-file folder and an unnumbered loose-recording directory are not classified as albums. Embedded album-tag reading and a manual “Treat folder as album” override remain later extensions; alpha.88 implements the conservative folder fallback without modifying files.

In `alpha.89`, the list from which an item is started becomes that session's persistent **playback context**. `Page Up`, `Page Down` and automatic continuation therefore stay within Favorites, an opened album, the current folder, Queue or Custom order. Browsing elsewhere does not replace that context until an item is started from the new list. History and Bookmarks are locators rather than separate queues. A temporary `Ctrl+K` filter does not shrink playback to its visible matches. `Alt+Up/Down` also stores manual Favorite order; it remains unavailable in Folders, All files alphabetically, Albums, History and search results.

`Alt+Shift+Enter` opens accessible **Item playback options**. A local file can override resume-position policy and playback rate; inheritance uses the folder or global resume rule and the session rate. The device field currently identifies the safe shared Windows default and remains disabled until the audio-output module is implemented; EQ may later share this surface. `Alt+Enter` remains read-only information and reports the effective rules. A local track recognised as part of an album also offers **Go to album** and **Go to artist**. An `F2` Library alias changes only the title displayed by AMC: album playback order still follows track numbers in physical file names, allowing clean custom labels without changing the disc sequence.

From `alpha.90`, `Alt+Shift+Enter` also works on a folder or album. Folder settings apply to every file below that folder, while a nested folder may define a more specific override. An individual file has highest priority, followed by the nearest folder, the registered Library source and the global setting. Options are stored by full path but never move, modify or open files. Individual-file lookup also falls back to its path, so a refreshed Library identifier no longer causes a false missing-settings message.

In `alpha.91`, every clipboard write shares handling for temporary Windows clipboard contention. `Ctrl+C`, `Ctrl+Shift+C`, copying from search and properties, and `Ctrl+X` retry briefly while NVDA, Total Commander, Ditto or another clipboard manager is reading the clipboard. Success is announced only after data has actually been stored. If contention persists, AMC remains responsive and reports a clear message with the system error code instead of failing silently or losing later shortcuts.

In `alpha.92`, selection fields in **Item or folder playback options** expose only their user-facing labels to UI Automation. NVDA should no longer announce class names or representations such as `ResumeChoice { Value = ... }`; the fix covers both resume-policy and playback-rate choices.

In `alpha.93`, the Library catalogue, folder sources, exclusions, orders, History and Bookmarks move from the large JSON state file to a local **SQLite** database. The first launch performs a transactional migration, verifies the stored item count and retains `state.pre-sqlite-migration.json` as the pre-migration copy. The database is `%LocalAppData%\AccessibleMediaController\library.db`; settings and profiles remain in `%AppData%\AccessibleMediaController\state.json`, while the full `.amcbackup.json` export remains one portable format.

Opening a decoder and disposing the previous audio pipeline now happen away from the UI thread. Scanning, Folder view, All files, Albums, quick Left-arrow information and `Alt+Enter` never open a cloud placeholder payload, so they do not download a whole folder or even the selected item. Only an explicit request to play one file may ask iCloud Drive, OneDrive or Google Drive to hydrate it. AMC announces the cloud download, keeps focus, menus and shortcuts responsive, allows cancellation, and reports a two-minute timeout instead of blocking the window. Rotating diagnostics are stored in `%LocalAppData%\AccessibleMediaController\logs`, retaining at most five files of approximately 5 MB each.

In `alpha.94`, local `Ctrl+Shift+C` is additionally intercepted at the window-message boundary, like the previously protected `Ctrl+Z` and time shortcuts. This applies to the list and player but never to text fields. Diagnostics now distinguish whether the shortcut reached AMC, how many items were selected, whether the write succeeded after retries, or whether Windows kept the clipboard locked; only format names are logged, never clipboard content.

In `alpha.95`, undoing removal of a local catalogue record also restores its exact position in **Custom order**. This works for one file and a selected block. Folders and All files continue to derive position from hierarchy and name, while a genuinely new file — rather than one restored by `Ctrl+Z` — is appended to Custom order.

`alpha.96` extends the same guarantee to collection-only removal. Before a change, AMC records the item's position in **Favorites** or local **Custom order**; `Ctrl+Z` restores both membership and the exact position. An item from the middle is no longer treated as new and appended.

In `alpha.97`, Playlists are persistent per-session collections rather than demo rows. `Ctrl+P` opens them; Insert creates one, F2 renames it, Delete removes the playlist itself, and Enter opens its items. Inside a playlist, Delete removes only references, `Alt+Up/Down` stores custom order, and Page Up, Page Down plus natural continuation remain in that playlist. `Ctrl+Shift+P` on one item or a selected block opens the accessible membership manager: Space toggles membership, a mixed state denotes partial membership, `Ctrl+K` focuses its filter, Enter saves, and Escape cancels. The command is also available from search results. Creation, rename, deletion, membership and ordering participate in `Ctrl+Z`, persist in SQLite and are included in a full `.amcbackup.json`; none of these actions deletes a media file from disk.

In `alpha.98`, Queue has a durable user order in every session. In Queue, `Alt+Up/Down` moves one item or a selected block; `Ctrl+Z` restores its exact previous position, and the layout survives restart in SQLite and the full AMC backup. Items marked Play next are always shown and played before the regular queue. They can be reordered within either priority group, but a single move cannot mix the two groups. Natural track completion consumes items in exactly the visible order: Play next first, then the regular Queue, before returning to the earlier playback context.

In `alpha.99`, `Ctrl+Q` retains one combined Queue while making its priority part explicit to NVDA: entry reports Play-next and remaining counts, and every priority row begins with “Next”. An item leaves the waiting list when playback starts. In the player, Page Up and Page Down follow the remembered Queue sequence even when AMC entered it automatically after a Library track ended. Page Up can revisit an already played Queue item, while manual navigation does not cause consumed entries to repeat later. A Queue entered from another view resumes that earlier playback context when exhausted; a Queue started directly ends without replaying its own consumed entries.

In `alpha.100`, cutting, moving or deleting the current file can no longer make the player select the first item in the entire Library. AMC retains the exact playback source: Queue, folder, album, Favorites, playlist, Library, search results, or any other current or future view. It selects the next available item only from that context. The same rule applies to changes found by folder monitoring, AMC clipboard cuts, Delete and Shift+Delete. Playback remains paused until Space is pressed. If the source view has no successor, AMC does not substitute an unrelated file and reports that no next item is available.

After a manual `Alt+Up/Down` reorder, `alpha.100` gives NVDA a relational announcement such as “Moved up, above [title]” or “Moved down, below [title]”. A selected block also reports the number of moved items. The named item is the neighbour displaced by the operation, so the new position is clear without reviewing the list again.

In `alpha.101`, the user-facing name of technical folder sources is consistently **Library folders**. `Ctrl+F5` opens that window and `F5` refreshes the folders. The selected-folder combo offers three clear policies: **Remember playback position**, **Always start from the beginning**, and **Follow the global setting**. General Settings and `Alt+Shift+Enter` use the same decision names, while inherited item and nested-folder choices still identify their actual parent level.

In `alpha.102`, `Ctrl+F5` focuses the first selected Library folder directly, so NVDA announces the complete row instead of only the list name. “Folder available” means that the folder root is currently reachable. **Active** files belong to the Library and are visible to the system, including cloud placeholders whose payload has not yet been downloaded. **Unavailable** files are remembered records whose path is currently unreachable, for example after a move or while a cloud provider is disconnected. **Excluded** paths were deliberately removed from the Library with plain `Delete`; the disk file remains, but `F5` and restart do not add it back.

In `alpha.103`, **Library folders** can open an accessible list of unavailable records for the selected folder and manually select one or many for **Forget in AMC**. The program never performs this operation automatically. Its confirmation explains that the Library record and its Favorites, Queue, playlist, History, Bookmark and resume-position relationships will be removed from AMC, while no disk file is changed. Cloud placeholders visible to the operating system are not treated as unavailable. A forgotten file that later reappears is indexed as a new record.

In `alpha.104`, persistent Playback History under `Ctrl+H` behaves like a regular newest-first list. Multiple entries can be selected, copied, added to Queue or Favorites, and used through `Ctrl+Shift+P` to create or update a playlist. `Delete` removes only the selected History entries and never changes the Library, playlists, Queue, Favorites or disk files. `Shift+Delete` remains a separate confirmed operation that physically moves a local file to the Recycle Bin. History cannot be reordered manually because playback time defines its order.

In `alpha.105`, `F1` opens an accessible searchable shortcut reference divided into sections. Every row exposes only its user-facing command name, current shortcut and context, never an internal identifier. Executable entries may be invoked with Enter through the same guarded command router as the command palette. `Ctrl+F1` enables Keyboard Help: subsequent key combinations are described but never executed until `Ctrl+F1` or `Escape` is pressed. `?` opens the reference only outside text-entry controls.

In `alpha.106`, the local Ogg/Vorbis decoder normalizes the timeline of excerpts cut from continuous live streams. Such a file can retain a very large starting sample number which NVorbis must not interpret as the duration of the standalone recording. AMC subtracts the stream origin without converting or modifying the file, reports the real duration, seeks relative to the excerpt and stops at its physical end. A dedicated Windows smoke test constructs this condition independently of the user's collection and guards against the CPU-consuming loop returning.

In `alpha.107`, `Ctrl+F1` Keyboard Help uses a concise context consisting only of the active session name, such as `Local files`, `WiiM` or `TIDAL`. It no longer repeats a view such as Folders or Queue. The Left Arrow description is explicit: in Local files it announces file size and bitrate; in other sessions it announces bitrate and the element's other available parameters.

In `alpha.108`, one shared guard validates every local decoder before playback. Status and screen-reader position reads no longer wait on the decoder itself. Eight seconds without read progress stops only the faulty item, keeps AMC responsive and records a `decoder-watchdog` entry with the source path. Left Arrow obtains missing metadata in the background with a five-second limit, so a damaged file or slow cloud provider cannot block the main window. No media file is modified or deleted. NVDA's `watchdog.waitForFreezeRecovery` log entry reports detection of another component's freeze; it does not cause that freeze.

In `alpha.109`, a folder row can never acquire a fake Favourite, Queue, or Play Next state. Enter still opens the folder. `Shift+Enter`, `Ctrl+Shift+Enter`, `Ctrl+Shift+U`, and playlist management operate on all currently available, indexed audio files in that folder and its subfolders. Repeating a toggle removes the same contents, the announcement names the folder and file count, and `Ctrl+Z` reverses the whole batch as one operation. An empty folder changes nothing. The operation uses only AMC's catalogue: it does not open media, read metadata, or hydrate cloud placeholders. `Ctrl+Shift+L` does not apply to a folder container because a registered folder already defines Library contents; an individual file can be changed after opening the folder.

Since `alpha.110`, a partially consumed folder has unambiguous toggle semantics. If any of its files remains in Queue, Play Next, or Favourites, the folder command and context menu offer removal and clear the remaining folder contents from that collection. Only when none of the folder's files belongs to the collection does the next command add all contents. Playing or manually removing one queued file can therefore no longer make the folder command unexpectedly re-add everything.

Since `alpha.111`, seeking in a slow or multi-hour file runs away from the window thread. This matters especially for streamed Google Drive files, which may fetch data only after a jump of several hours even when Windows exposes no ordinary placeholder flag. AMC recognizes common Google Drive, iCloud, OneDrive, and Dropbox locations, keeps focus and shortcuts responsive while waiting, and coalesces a burst of seeks into the latest requested position. The status bar reports that target instead of the decoder's stale position. Slow seeks are timed in the diagnostic log; a two-minute watchdog safely stops an item that never completes. Ordinary local files still seek without a noticeable delay.

Since `alpha.112`, that protection is adaptive. Files under iCloud, OneDrive, Google Drive, Dropbox, Box Drive, pCloud, MEGA, Proton Drive, Nextcloud, ownCloud, Sync, and network shares receive longer fetch limits and are not quarantined until restart after a transient network failure. Local MP3 files are preflighted by reading at most 1 MiB around the audio start; an ID3-declared size never controls allocation. If the system decoder rejects or stalls on a structurally recognised local MP3 up to 512 MiB, AMC makes one recovery attempt with the managed NLayer decoder while retaining position, volume, and speed. Larger and remote files stay on the streaming decoder, so fallback cannot trigger a full scan or bulk cloud download. The credible-duration ceiling is raised from 30 to 365 days. Invalid or unsupported media produces a short user-facing message while technical details remain in the log.

Since `alpha.113`, every explicitly opened local or cloud item receives an additional container preflight bounded to 64 KiB. It recognises RIFF/RF64/BW64 WAVE, AIFF/AIFC, FLAC, Ogg Vorbis and Opus, MP4/M4A/MOV/3GP, WMA/ASF, Matroska/WebM, AVI, and ADTS/ADIF AAC headers. The preflight never walks every block, never trusts declared block sizes, and does not replace the decoder; it records obvious truncation or an extension mismatch in the log only. Ogg is routed to the built-in Vorbis reader only when it actually carries Vorbis, while Ogg Opus is left to the system path. A failure in any format now detaches the bad pipeline rather than leaving it apparently reusable. The final output stage validates complete channel frames and replaces NaN or infinite samples with silence before they reach the audio device. Library discovery also recognises common Media Foundation containers; actual AC-3, Opus, and less common codec availability still depends on the Windows version and installed components.

Since `alpha.114`, cloud state is also obtained from the Windows Cloud Files API using only attributes and the reparse tag, without opening file data. This covers compliant providers independently of their folder name; path and virtual-drive recognition remains a fallback for Google Drive and older clients. Repeated failures of one remote source receive an increasing 10-second to 5-minute retry delay, preventing a pile-up of blocked decoder workers without permanently quarantining a recovered cloud file. MP3 preflight now understands huge ID3 tags, invalid tag versions, leading junk, and verified free-format frame spacing. Media Foundation can receive a seekable view beginning exactly at the confirmed audio frame, with no whole-file copy and no application size ceiling; NLayer remains limited to local files up to 512 MiB because it may scan the complete stream. Initial RIFF/WAVE/AVI, AIFF, FLAC, Ogg, MP4, and ADTS AAC declarations are also checked against the physical length. Diagnostics remain log-only and the decoder remains authoritative.

From `alpha.89`, `Delete` is the only removal key. `Backspace` never deletes an item: in Folders it opens the parent directory, inside an album or another container it moves up one level, and in the player it returns to the list while applying the normal pause policy. Text fields retain ordinary character deletion, while a top-level list announces that no parent level exists. `Alt+Left/Right` remains visited-view history and does not replace parent semantics.

The current demonstration catalogue may present combined test results. A real TIDAL adapter will be an isolated module: `Ctrl+Shift+F` may initiate its query, but TIDAL content will not be mixed into one list with content from similar services. AMC opens a separate, attributed TIDAL results view while retaining the shared commands.

The first YouTube stage works without account synchronisation. In Podcasts,
Ctrl+N deliberately adds one public video or live page to **Internet media**;
Radio accepts a currently live public broadcast. AMC does not copy the account
or the full YouTube interface. OAuth remains an optional later extension if
account subscriptions, playlists, or likes become necessary. Resolution and
explicit MP3 saving are isolated behind updateable components, use no browser
cookies, and never become a dependency of ordinary RSS. Public YouTube search
and an official visible-player adapter remain possible later additions.

`Ctrl+Z` successively undoes membership changes in Favorites, Library and Queue as well as the Play Next state; a restored item is selected again when it belongs to the current view. Inside the filter box, `Ctrl+Z` retains the standard text-editing Undo behavior. The command is also available from the **Edit** menu. The extra English “Undo” has been traced to the NVDA Global Commands Extension's Clipboard command announcement feature rather than AMC. `Ctrl+N` and `Ctrl+A` remain reserved for the standard New and Select All actions. Typing one or more unmodified letters on the list jumps to the semantic primary name and never runs a command: the track title, artist name, album title, or playlist, station or device name. Matching remains independent of the configured field-reading order. Empty lists contain directional navigation and announce that they are empty instead of moving focus to action buttons or the menu. The Now Playing view contains 17 fixed items for type-ahead tests; the demonstration Library intentionally contains only its two member tracks.

On the **Lists and reading** tab, `Alt+Up/Down` moves the selected field, keeps focus on the selected row, and announces its new relationship and the full order. The current-order preview precedes the movement buttons. **Add to queue**, **Play next** and **Favorites** act as toggles; repeating the command removes the item and the context-menu label reflects its current state. Their default announcements include the affected item name and customized templates are preserved during migration. Before removal, focus is anchored on the list control and restored after WPF layout finishes; it then moves to the nearest item or remains on the empty list. In the main window, `Escape` clears an active filter and returns focus to the media list; without a filter it still returns from the filter box or a main action button to the list. `Alt+F4` closes the active window: from Search it returns to the main window, while from the main window it exits the application. Menus keep their standard hierarchical behavior, with each `Escape` closing one level.

The **Messages** tab includes a **Show detailed keyboard hints at fields and lists** option covering the filter plus current-service and global search. It is off by default. Result help deliberately mentions only arrows, Enter and Escape; the other direct actions remain available through the context menu and documentation without lengthening every result announcement.

Opening Settings always focuses the selected General tab. Left and Right Arrow change category, while Tab enters the controls on the selected page. Both Save and Cancel restore focus to the selected item in the main media list; the saved announcement is raised only after list focus has been restored.

The prefix, timeout and every command are configurable. Users can also choose whether startup opens the media list or the session list. The protected built-in profile is refreshed with the application version; editable user profiles retain their own mappings.

## Import and export

The program recognizes three file types:

- `*.amckeys.json` — one keyboard map;
- `*.amcsettings.json` — application settings without keyboard maps;
- `*.amcbackup.json` — complete backup containing settings, profiles, sessions, bookmarks, playlists and message templates.

Passwords, tokens and login data are never exported.

Working settings and profiles are stored in `%AppData%\AccessibleMediaController\state.json`. The local Library catalogue and its relationships are stored in `%LocalAppData%\AccessibleMediaController\library.db`. A full `.amcbackup.json` export still combines both sets into one portable file.

## Updates

The project includes a separate update-service interface plus settings for channel, background download and installation on exit. No update server is configured yet.

`Alpha` builds remain portable for now. Before the public beta, AMC will gain a signed per-user MSIX/App Installer distribution plus a small health and rollback supervisor. Mandatory code, libraries and the .NET runtime update as one compatible set; an update never interrupts playback, replaces a running NVDA add-on or modifies user data. Failed download, verification or migration leaves the current version and a safety copy of the data usable.

The planned **Help** menu provides a hierarchical accessible shortcut reference under `F1` and optional `?`, a `Ctrl+F1` keyboard-learning mode, manual update checking, full version information and a future public repository. The reference is generated from the active keyboard profile and shared command catalogue; activating an entry uses the same guarded path as the command palette. `Ctrl+F1` only describes the captured key and never executes its command. Development builds expose no empty links.

The final updater should provide a self-contained per-user installation, update the application and service adapters, verify signatures and SHA-256, install atomically with rollback, preserve configuration and credentials, and avoid stealing focus or interrupting playback.

## Playlists and presets in alpha 140

A finite-media playlist row announces its item count and **total duration**. If only part of the duration is known, the label says so explicitly. A radio playlist never pretends to have a finite duration and is labelled as **live streams**.

Enter opens the playlist. `Ctrl+Enter` plays its first available item and uses the complete playlist as the Page Up/Page Down context. The playlist context menu distinguishes opening from playback and explicitly names bulk operations on its contents: Queue, Play next, Favorites and Library. Queue and Play next remain unavailable in Internet Radio. `Alt+Enter` shows properties of the playlist itself instead of an unrelated current item. In Local Files, `Alt+1/2/3` also works from inside a playlist and switches to Folders, All files or Custom order.

Direct preset keys `Ctrl+Shift+1–0`, minus and equals are handled at the Win32 message boundary. Modifier state is read directly from Windows, fixing the specific case in which a Polish keyboard layout or screen reader could lose Shift for the `0` key.

Local video containers, including MP4, MKV, WebM, MOV, AVI and MPEG, are already supported as audio sources.

## Preset 0 and Radio scheduling in alpha 141

`Ctrl+0` and `Ctrl+Shift+0` now have disjoint command routes. The former still opens the session list, while the latter invokes preset slot ten and calls it “Preset 0” in a direct-key announcement. AMC tracks modifier state from raw Windows messages and never lets a temporary WPF omission of Shift degrade the chord to `Ctrl+0`. A regression test covers the complete routing decision rather than only mapping key `0` to a slot number.

In **Internet Radio**, `Ctrl+Shift+H` opens the accessible **Radio recording schedule**. Insert creates an entry, Enter edits it, Space enables or disables it, and Delete removes it; every row states the station, next start, duration, file division, recurrence and state. A plan may run once, every day or on selected weekdays, may last from one minute to one week, may use one file or timed parts, has its own recording format and bitrate, and has an optional output folder plus a three-state wake rule. Recording uses a separate inaudible pipeline, so it never changes the station being heard; multiple plans may run concurrently.

A schedule retains the stable station identifier and a safe URL snapshot. Editing a custom station updates linked schedules. Starting AMC inside an active interval records only its remainder; a fully missed one-shot is removed and a recurring plan advances to the next valid day. An unavailable per-entry folder safely falls back to the general recording folder. Active recording prevents idle sleep and the nearest wake-enabled entry owns one Windows wake timer. Wake works while AMC remains running during sleep; closing AMC does not leave a hidden system task behind.

## Discoverable Radio recording and preset 0 in alpha 142

While the AMC window is foreground, `Ctrl+Shift+0` has an additional low-level keyboard path. This lets preset ten work even when NVDA or an add-on does not pass that chord to the normal WPF message queue. Capture is limited to exact `Ctrl+Shift+0` and the active AMC window; `Ctrl+0` still opens the session list, and shortcuts in other applications are unaffected.

In the open Radio player, `R` starts and stops capture of the station currently being heard. Since `alpha.153` it uses a separate inaudible pipeline: Page Up, Page Down, Enter and presets may change listening without interrupting capture. During a manual capture, `T` finalises the current part and immediately starts another file; it also works for the selected station in the **Recording** view. `Ctrl+Alt+R` starts or stops the selected station without opening the player, and different stations may be recorded concurrently. `Alt+2` opens **Recording**, which combines active manual and scheduled captures; `R` in that view stops the selected manual capture. `Shift+R` opens **Timed recording and Radio schedule**, while `Ctrl+Shift+H` opens the complete schedule list. A separate `Shift+T` is unnecessary.

## Radio recording settings in alpha 143

Settings has a dedicated **Radio and recording** tab. It selects the default system or a custom folder, `MP3`, `M4A (AAC)` or `WAV`, a lossy-format bitrate, and the default wake behaviour for schedules. MP3 at 192 kb/s remains the initial setting. WAV stores decoded PCM without compression and therefore creates much larger files.

Every schedule explicitly chooses the **general folder from application settings** or **a different folder for this entry**. Wake behaviour remains three-state: inherit the general setting, always wake or never wake; the inheritance label also states the current general value. If the entry-specific or custom general folder is unavailable, AMC performs a real write probe and falls back through the general and system folders instead of losing the recording.

Manual and scheduled capture use the same decoded path as playback. This includes MP3, AAC, OGG and HLS whenever the stream can be played by an available AMC decoder. HLS sources that need FFmpeg also need that component during a scheduled recording. AMC does not offer a misleading HLS “original format”, because a live transmission consists of changing segments rather than one stable source file. An `.amc-partial` working file receives its final name only after the encoder closes correctly.

Wake is optional and requires AMC to remain running while the computer sleeps. Hardware, Windows power-plan settings and battery policy retain the final decision over whether the machine resumes.

## Station scope and recording state in alpha 144

`Shift+R` opened from a list no longer uses the session's complete internal catalogue. Its station field contains only stations from the current view—such as current Favorites, History or an open playlist—in the same order, with the highlighted station initially selected. Old hidden Radio Browser search results cannot appear by themselves. The complete schedule manager additionally retains Library stations and stations referenced by existing entries.

An actively captured station has a short **recording** state after its name in every list. When it is also playing, NVDA announces the station name first and then the **recording, playing** states. `Alt+Enter` has a separate Recording section with manual or scheduled kind, start time, manual or planned end, format and destination file or folder. Ordinary navigation therefore remains short while complete data is available on demand. `Alt+Shift+Enter` is not reassigned and retains its item-options role in services that expose those options.

## View behaviour after preset activation in alpha 145

A preset that represents a playable item starts playback in the background by default and does not take focus away from the current list. This applies to every session, both from the preset list and through direct `Ctrl+Shift+1–0/-/=` shortcuts. If the player was already open, it remains open and shows the new item.

`Settings > General > Playback` contains **Open the player after activating a preset**. Enabling it makes a preset enter the player. Escape then returns to the view and position from which the preset was invoked; the separate follow-playback-on-player-exit option still applies independently. A folder, album or playlist preset remains a container and opens its contents rather than pretending to play in the background.

## FLAC and original-stream capture in alpha 146

Radio recording now also offers **FLAC, lossless** and **Original stream, no conversion**. FLAC receives the decoded PCM used by AMC and encodes it losslessly. Original mode opens a separate inaudible connection and copies compressed packets without re-encoding them. Direct MP3 remains MP3, AAC is stored as AAC, OGG or Opus as OGG, and FLAC as FLAC. An unknown codec uses a safe Matroska audio container.

HLS is not one finished file, so AMC never stores the M3U8 manifest as a recording. Audio segments are joined and losslessly remuxed into an MPEG transport-stream file. The codec is unchanged, but this is not a byte-for-byte copy of the manifest and segments. ICY metadata is not inserted between audio frames. FLAC and original mode require FFmpeg; a missing component produces a clear failure and no file that pretends to be complete. Bitrate is selectable only for MP3 and AAC.

## Recording folders in alpha 147

`Settings > Radio and recording` now has one unambiguous **Default recording folder**. Its read-only field shows the complete remembered path, and **Browse…** opens the standard folder picker without requiring a preceding radio-button choice. A legacy empty value is initially resolved to the real `Music\AMC — Radio recordings` path and saving the settings makes that choice explicit.

Each schedule has one folder-mode combo with **Default recording folder** and **Different folder**. The latter enables a path field and **Browse…** only for that entry. An empty path in an older entry continues to mean inheritance, while an existing entry-specific folder is preserved. Changing the default affects inheriting schedules but never overwrites an entry-specific folder.

## Accessible schedule editing in alpha 148

One list replaces seven separate weekday controls. Arrow keys move from Monday through Sunday, while Space checks or clears the current day and announces its new state. The list is enabled only for **Selected weekdays** recurrence. Every row has an explicit accessible name such as “Tuesday, checked”, so UI Automation never exposes an object representation.

The schedule list toggles its selected entry with Space or the **Enable or disable** button. A disabled entry remains stored but cannot start capture or wake the computer. A change takes effect after **Save**; cancelling keeps the previous state. Disabling a currently recording entry stops that capture when the changes are saved.

The quick `Shift+R` option is now **Start the first recording immediately after saving**. When checked, the first date and time are ignored and the entry is necessarily enabled. A one-off entry runs once; a recurring entry calculates later occurrences from the actual immediate-start time. Clearing the option makes the entered first date and time effective. The per-entry folder combo contains **Default recording folder** and **User folder**; only the latter enables its path picker.

## Schedule time and focus in alpha 149

A new entry has one explicit **First recording** combo with **Immediately after saving** and **At the selected time**. The latter enables native Windows date and time controls. In the date control, Left and Right select day, month or year, while Up and Down change the selected part. The time control works the same way for hours and minutes. Duration is a numeric field limited to 1 through 10080 minutes, so malformed text cannot be stored as a schedule time.

Tab follows the user-facing form order. Escape also works inside the hosted native controls and cancels editing, while Enter saves the entry. A new entry starts with focus on the station; an existing entry starts on its date; after add, edit or delete, focus returns to the schedule list.

The complete `*.amcbackup.json` backup already contains the Radio Library and Favorites as well as recording schedules. [`PROJEKT_IMPORTU_EKSPORTU_RADIA.md`](PROJEKT_IMPORTU_EKSPORTU_RADIA.md) specifies a separate safe sharing format; dedicated import and export commands are not part of alpha 149 yet.

## Start and wake labels in alpha 150

The new-schedule combo is now simply labelled **Record** and contains **Immediately** and **Later**. “Immediately” means after choosing Save; “Later” enables the date and time controls. The implementation-oriented phrase “after saving” is no longer part of the value itself. The wake control is consistently named **Wake the computer for this schedule**.

## Date and time segment announcements in alpha 151

The hosted date and time controls now explicitly send the selected part and its current value to NVDA. Left and Right announce day, month, year, hour or minutes. After Up or Down changes a value, the same part's new value is announced, for example “Month: 8, August” or “Minutes: 35”. `NVDA+Up Arrow` is no longer required, and date and time still occupy one Tab stop each.

## Concise date and time values in alpha 152

Left and Right still identify the selected part, for example “Minutes: 45”. While actually changing it with Up or Down, AMC now announces only the new value — “46”, “47” — without repeating “Minutes”, “Day”, “Month”, “Year” or “Hour”.

## Independent captures and the Recording view in alpha 153

A manual Radio capture uses a separate inaudible connection. `R` controls the current station in the player, `Ctrl+Alt+R` works on a selected list station, while Page Up, Page Down, Enter and presets change listening without ending capture. Different stations may be recorded concurrently. `Alt+2` in Radio opens **Recording** with manual captures and active schedules; `R` stops the selected manual capture there. Escape from the player explicitly anchors focus on the list. Original mode lets FFmpeg select the audio stream, so a direct ICY input without an early typed `0:a:0` index is not rejected.

## Predictable low-rate encoding in alpha 154

Stations decoded at 22.05 or 24 kHz are no longer silently written by the system encoder as 80 kb/s MP3 after 128 kb/s was selected. For lossy MP3 and AAC output only, AMC uses a high-quality resampler to normalise such input to 44.1 or 48 kHz before encoding, so the result honours the selected bitrate. This does not invent detail absent from the source. FLAC, WAV and Original recording continue to preserve the source rate. The Original option now explicitly says that HLS produces a `.ts` file.

## Resilient TS playback and fragment recovery in alpha 155

Local `.ts`, `.mts` and `.m2ts` use a tolerant FFmpeg decoder that extracts only the audio track. AMC can therefore play broadcasts captured between video key frames even when Media Foundation rejects the container, while seeking remains available. `Ctrl+O` has a separate **Incomplete recordings to recover** filter for `.part`, `.partial` and `.amc-partial`; AMC attempts to play the available portion and ends at its actual boundary. Such files are not indexed automatically from folders because they may still be growing or incomplete.

## Safe recording termination in alpha 156

Starting with `alpha.166`, `Ctrl+Alt+Shift+R` stops all currently active manual and scheduled recordings regardless of the open session. A single recording stops immediately; multiple recordings require explicit confirmation whose default answer is No. Every received fragment is finalised, while a recurring schedule keeps only its next occurrence. Closing AMC during recording likewise reports the active count and requires confirmation. Manual captures do not resume after the next launch. If the current scheduled occurrence is still inside its time window, however, AMC starts a new file and records the remaining portion; an expired window is never replayed late.

Starting with `alpha.157`, `Shift+Space` pauses or resumes capture of the station selected in the list or open in the player. Plain Space remains listening pause. MP3, M4A/AAC, FLAC and WAV omit audio received while paused, and every pause creates an AMC Bookmark in the completed recording. Original stream copy does not offer pause because preserving packets would require splitting and safely joining containers. An imported PLS, M3U or XSPF is resolved to its direct stream before original capture, while genuine HLS remains a manifest.

Starting with `alpha.158`, network-list resolution also handles nested PLS, M3U, M3U8 and XSPF files, relative addresses, and redirects from a playlist address straight to audio. A loop or more than four list levels is rejected safely. A response identified as direct audio is not consumed and misparsed as text, while a genuine HLS manifest still goes straight to the decoder. This covers Original capture of Tyflo Podcast through `listen.pls` and other stations using the same wrappers.

Starting with `alpha.159`, `Ctrl+M` mutes or restores listening in the current session, while `Ctrl+Shift+M` applies to every AMC session, including playback already running in the background or started later. The layers are independent: releasing global mute does not unmute a session that was muted individually. Adjusting volume clears individual mute but not global mute. These shortcuts do not alter Windows, NVDA or other applications and do not interrupt recording. Mute itself is not retained after restart, while the chosen numeric volume values are.

Starting with `alpha.160`, `Ctrl+Shift+H` opens Radio's complete recording schedule list. It is the primary shortcut shown by menus, the palette and help. Starting with `alpha.166`, the former `Ctrl+Alt+Shift+R` alias stops all recordings, so only `Ctrl+Shift+H` opens the scheduler. Invoking it outside Radio reports where the function is available and never changes session silently.

Starting with `alpha.161`, a Radio list row and the Radio playback status announce the station name first and append state afterwards, for example “Radio 357, recording”, “Radio 357, recording paused, playing”, or “Radio 357, paused”. This lets the listener identify the station before hearing its changing playback and recording states.

Starting with `alpha.162`, `T` in the Radio player or **Recording** view closes the current manual-capture file and continues the same station in a new file. `Shift+R` contains a **File layout** combo with **One file** and **Split into parts**; the latter enables the duration of one part in minutes. The plan's total duration remains the single final deadline, so a two-hour plan split every 15 minutes produces eight parts, while a late resume records only the remaining window.

Starting with `alpha.163`, HLS begins at the live edge and is decoded at real-time pace, so a short capture cannot ingest older manifest segments in a burst. MP3, AAC, FLAC, WAV and Original output is first completed in a local staging directory outside iCloud, OneDrive and Google Drive. Only a closed file is copied under an `.amc-publishing` name and atomically given its final name; a failed cloud publication retains a recoverable local copy. A stop request wins over a concurrent split and cannot start an empty next part.

This release also adds optional music recognition to Radio. `S` in the player recognises the portion currently heard from the time-shift buffer without opening a second station connection. `Shift+S` toggles monitoring, while `Ctrl+Alt+S` opens a persistent newest-first history. The same track on the same station within 30 minutes is not added repeatedly. History stores station, title, artist, album, release date and recognition time and supports extended selection, copying, deletion, and JSON or CSV export. Exports include Apple Music, Spotify and TIDAL searches; exact catalogue-ID matching belongs to the future official service adapters. Only an acoustic fingerprint is sent to the recognition provider, not the recording or station URL. Starting with `alpha.180`, the monitoring state is persistent and also has a checkbox in Radio settings.

Starting with `alpha.164`, rich copy and recognition-history exports also include YouTube Music plus Discogs and MusicBrainz catalogue searches. These are explicit search URLs, not a claim that AMC has silently selected the correct edition. The future controlled matching and credits model is defined in [`MUSIC_CREDITS_AND_CATALOG_DESIGN_EN.md`](MUSIC_CREDITS_AND_CATALOG_DESIGN_EN.md).

## Podcast attachments in alpha 269

AMC distinguishes playable RSS/Atom media from artwork, documents, and other
attachments. If an entry contains both artwork and audio, audio is selected;
an image alone is no longer opened as an episode. At startup AMC removes legacy
episode records that unambiguously point to artwork while retaining the
subscription. A feed without playable audio or video now receives an explicit
message instead of a generic playback failure.

## Portable downloaded-podcast names in alpha 270

`Ctrl+D` and the name suggested by `Ctrl+S` now share one safe naming rule.
Quotation marks and apostrophes are removed, while commas, colons, slashes,
and other problematic separators become a readable hyphen. AMC also removes
control characters and trailing dots or spaces, protects Windows reserved
names, and shortens unusually long titles without losing the extension. The
result is portable between Windows and macOS while retaining Polish and other
Unicode letters.

Only newly written disk files are affected. The original episode title remains
unchanged in the Library, description, and metadata, and existing downloads
are not renamed automatically.

## Extended WiiM control in alpha 271 and 272

The WiiM session now controls input, physical output, equalizer, repeat mode,
shuffle, and sleep timer through accessible labelled dialogs opened in the
player with `I`, `O`, `E`, `R`, `S`, and `T`. `Shift+A` selects the active
device. `Ctrl+Alt+P` opens the twelve native WiiM presets, while
`Ctrl+Shift+1–9/0/-/=` activates them directly. Page Up and Page Down move
between occupied slots without playback inside the list; `Alt+Page Up/Down`
activates an adjacent occupied slot. Starting with `alpha.272`, AMC restores the
starting point after restart by matching the current stream URL first and then
using the last preset activated through AMC. It does not guess while Spotify
Connect or TIDAL Connect is active. `Ctrl+P` retains its shared Playlists
meaning and is not captured by WiiM.

In `alpha.273`, the player surface, window title, and status bar also expose
the current programme or track when the device or station supplies it.
`Alt+D` speaks the compact now-playing information without opening a dialog.
AMC merges WiiM player status, `getMetaInfo`, and bounded UPnP metadata, while
filtering technical placeholders and playlist-manifest names.

In `alpha.275`, WiiM volume control in the open player no longer depends on
focus remaining on one particular player control. Up/Down and Shift+Up/Down
accumulate rapid presses and AMC reports when the device does not confirm the
requested value, for example when fixed output volume is enabled. Combinations
using the NVDA modifier remain available to the screen reader.

In `alpha.274`, `Ctrl+Alt+Shift+P` in the WiiM session maps an existing device
preset to one of AMC's twelve local `Ctrl+Shift+1–0/-/=` shortcuts. The mapping
is stored separately for each device, may differ from the native preset number,
and initially preserves the former one-to-one behavior. Removing a mapping
removes only the AMC shortcut and never changes or deletes the hardware preset.
`Ctrl+Alt+P` continues to show and activate the native device list.

The manufacturer's local API does not expose writing native presets. AMC can
therefore read and activate them safely and map them to local shortcuts, but it
never presents that mapping as a device change. Writing, overwriting, and
deleting hardware presets still belong in WiiM Home.

The next capability-gated stages—direct URL/M3U playback, UPnP events,
multi-room groups, and the device-dependent hardware queue—are specified in
`PROJEKT_WIIM_PL.md`.

Starting with `alpha.287`, WiiM device details also expose the current
multi-room role and group name. If a leader's firmware provides its read-only
member list, AMC shows each follower's friendly name, volume, mute state, and
local IP address. This stage is strictly informational: AMC does not create or
dissolve groups, and an unsupported optional member-list call cannot block the
rest of WiiM control.

In `alpha.272`, the Podcasts File menu also offers **Export podcast library to
OPML…**. This portable subscription backup round-trips through `Ctrl+O` but
does not contain media files, listening progress, chapters, or episode
playlists. The full AMC backup continues to retain those application-specific
records.

## Per-session audio output in alpha 204

**Shift+A**, **Playback > Select audio device for the current session…** and the
player context menu enumerate real active Windows outputs. The choice persists
independently for Local Files, Internet Radio and Podcasts. Changing it restarts
only the audible session pipeline without changing Queue, History or the current
item; independent Radio recordings continue in the background.

AMC keeps shared-mode WASAPI so NVDA and other applications remain audible.
The user can follow the Windows default dynamically or retain a specific output.
If the retained device is disconnected, AMC temporarily falls back to the
system default without deleting the preference. Accessible labels expose only
friendly names, never technical device identifiers. Per-item, folder or station
output overrides remain a later stage.

The `alpha.205` correction also retains the latest Space-key request while an
output pipeline is being switched asynchronously. If a slower external endpoint
is still starting, the pause request is not lost and the new pipeline remains
paused. The same guard covers Radio, Podcasts and Local Files.

`Shift+A` targets the current session rather than globally changing every audio
pipeline. It is available to each adapter that actually outputs sound through
AMC: currently Local Files, Internet Radio and Podcasts. A future device session
such as WiiM will use the same command to select an adapter-provided playback
target instead of pretending that the external streamer is a Windows sound card.

## Current limitations

- TIDAL and Apple Music remain demonstration sessions. WiiM has a real adapter for discovery, state, transport, volume, mute, and device presets. Local Files plays real media and persists its catalogue, while Internet Radio searches and plays real public streams and persists its own Library and Favorites.
- Public station, show and episode pages can open in the browser; account-based
  official-application integration remains a later stage.
- Music downloading and DRM handling are not implemented; `D` and `Shift+D` only announce that the commands are unavailable.
- The updater does not yet download packages.
- Windows is the first target. macOS, VoiceOver and Siri are later stages.
- `Ctrl+Alt+Windows+F12` is the prototype prefix. It registered successfully on the test computer, but Windows-key combinations must be verified on every target computer.

## Structure

- `src/AccessibleMediaController.Core` — commands, profiles, configuration, sessions and update interface;
- `src/AccessibleMediaController.Windows` — WPF, UI Automation, global prefix and accessible windows;
- `tests/AccessibleMediaController.Core.SmokeTests` — dependency-free logic checks;
- `tests/accessibility/nvda` — optional guarded NVDA smoke-test client;
- `MEDIA_CONTROLLER_PL.md` and `MEDIA_CONTROLLER_EN.md` — complete concept specification.

## Concise active-recordings view

Starting with `alpha.286`, the Radio `Recording` view no longer repeats the
obvious “Internet Radio” session name whenever focus enters the list. A session
switch announces the slot, view, station and recording state. Other Radio views
retain the full session name where it remains useful context.

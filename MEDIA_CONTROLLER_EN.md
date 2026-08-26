# Accessible Media Controller — Project Concept

Document version: 0.7, current project plan

Updated: 26 August 2026

## 1. Project goal

The project is an accessible media controller with one shared core for commands, sessions and services and several equal interaction methods. It will work through both a classic window and a configurable keyboard prefix used without switching to the window.

The first planned integrations are:

- TIDAL;
- Apple Music;
- WiiM.

Spotify, Sonos, Bluesound/BluOS and devices based on Frontier Smart are planned for later stages. The architecture will also accommodate internet radio and local media. Standard local playback will not require foobar2000; an optional adapter may later expose its specialised formats, DSP or hardware support. Free Radio may be used as a design and implementation reference after its code and licence have been reviewed.

The program will not depend on NVDA. The first windowed version targets Windows using WPF and UI Automation and must work with NVDA, JAWS and Narrator. An optional NVDA add-on will be built later as a small client rather than as the home of service logic.

A future macOS edition will have a native Swift/AppKit interface using NSAccessibility and VoiceOver. Shared logic will remain in the cross-platform .NET core and will be exposed through a stable command/event boundary. Shortcuts and the global prefix will have platform-specific implementations.

## 2. Hybrid interaction model

The application combines three methods of interaction:

1. A **prefix command layer** for quick background operations.
2. **Simple accessible windows** for browsing search results, albums, playlists, the library and settings.
3. **Optional thin integrations**, primarily an NVDA add-on, using the same running core.

The initial product is not intended to have a large interface that remains open at all times. Complex data must nevertheless appear in a normal window because speech messages alone are not suitable for browsing dozens of albums or playlists.

Every application operation will be represented by an internal command that can be invoked from:

- the prefix layer;
- a local shortcut in a window;
- a context menu;
- a future optional NVDA add-on, native macOS client or external control interface.

The NVDA add-on does not contain service adapters, playback libraries or tokens. It sends short commands to a local AMC process and receives events and announcement text. No terminal command is involved. Windows will use a local named pipe; macOS will use a suitable local mechanism such as a Unix domain socket or native bridge.

The core runs only when needed. It can start with the window or on demand from the add-on, remain available without a visible window through the system tray, and stop on an explicit user command. Starting with the operating system remains optional.

On macOS, a normal accessible window remains mandatory and the prefix is only an additional control path. External automation tools such as Keyboard Maestro may later call a public command or an `amc://` URL, but they are not required for the application to work.

## 3. Application prefix

### 3.1. Behaviour

1. The user presses the global prefix.
2. The application temporarily enters its command layer.
3. The next key or key combination is interpreted by the application.
4. `Escape` cancels the layer.
5. The layer expires after a configurable timeout, proposed default: 3 seconds.

Pressing the prefix again may announce the current session, for example “TIDAL”. This behaviour should also be configurable.

### 3.2. Choosing the prefix

The prefix remains configurable. The current prototype uses `Ctrl+Alt+Windows+F12`, but this is not the final choice. The target Windows implementation detects the prefix through low-level physical keyboard capture and does not expose a choice between multiple technical registration modes. This makes it possible to distinguish the main `Enter` from `Numpad Enter` through the scan code and extended-key flag, and to handle Num Lock consistently.

The user may assign any supported physical key combination. An empty value disables the global prefix without disabling shortcuts in the active application window. Once AMC detects the configured combination, it suppresses its events so that the focused application does not execute another command at the same time. The application must still detect and clearly report cases where the operating system, a higher-integrity process or a screen reader captures the combination before AMC receives it.

The main candidates for the future default are `Ctrl+Numpad Enter` and bare `Numpad Enter`. The former interferes less with ordinary confirmation in other applications; the latter is faster but globally removes the standard meaning of Numpad Enter. The final choice requires testing with NVDA, JAWS, Ditto and popular clipboard managers. Candidates include:

| Candidate | Advantage | Risk |
| --- | --- | --- |
| `Ctrl+Numpad Enter` | short, physically distinctive and less intrusive | requires low-level distinction between the two Enter keys |
| `Numpad Enter` | very fast and convenient with one hand | takes over standard confirmation by that key in every application |
| `Ctrl+Alt+Windows+F12` | distinctive and did not conflict on the test computer | four keys; requires a registration test |
| `Ctrl+Alt+Space` | relatively short | possible conflict with other applications or input methods |
| `Ctrl+Shift+Windows+Space` | clearly separates the application from Free Radio | long; Windows combinations may be reserved by the operating system |
| `Ctrl+Windows+\` | short and distinctive | Windows modifier combinations require a registration test |
| `Ctrl+Shift+Windows+P` | easy association with “prefix” | four keys |
| `F13–F24` | very low conflict risk | requires a programmable keyboard or a remapped extra key |

Settings should provide a “Test prefix” function. The program saves the binding only after a successful physical-detection test and warns if it is already in use or does not reach AMC.

Microsoft states that shortcuts containing the Windows key are reserved for operating-system use, so availability cannot be assumed. The application must test the actual combination on the current computer: <https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey>.

## 4. Sessions

### 4.1. Selecting a session

After the prefix, plain digits directly select sessions. In the active window their counterparts are `Ctrl+digit`. Session order is configurable. Digits are language-independent and preserve letters for commands.

Proposed defaults:

| Command after the prefix | Session or action |
| --- | --- |
| `1` | TIDAL |
| `2` | Apple Music |
| `3` | WiiM |
| `4–9` | additional services or devices |
| `0` | list all sessions |
| `Page Up` | previous available session |
| `Page Down` | next available session |

After a change, the application gives a short message such as “3, WiiM”. If a slot is unused, it says “Session 4 unassigned”. In the active window, `Ctrl+1–9` selects a session, `Ctrl+0` opens the session list, and `Ctrl+Page Up` / `Ctrl+Page Down` move to the previous or next session.

### 4.2. Session persistence

- The selected session remains active for subsequent commands.
- The service does not need to be specified every time.
- After selecting a session, the command layer may remain active for approximately 2 seconds so that an action can immediately follow.

Examples:

- prefix, `1` — select TIDAL;
- prefix, `F` — search the current session, which is TIDAL;
- prefix, `3`, `Space` — select WiiM and play or pause;
- prefix, `2`, `P` — select Apple Music and open Playlists.

It remains to be decided whether the session persists across application restarts. A safe option is to restore the previous session but announce it on first use of the prefix.

## 5. Letter commands

### 5.1. General rule

- in the active window, the primary binding is usually `Ctrl+letter`;
- after the prefix, the same letter without `Ctrl` invokes the command;
- `Shift` means a related action, expanded scope, or an operation on the current item;
- not every letter needs an immediate Shift variant;
- every assignment is editable in Settings.

A Shift command must not perform an irreversible operation without confirmation.

The prefix therefore replaces `Ctrl` instead of requiring another Ctrl chord after activation. This preserves combinations such as `Ctrl+E`, `Ctrl+R` and `Ctrl+T` inside the layer for additional information commands. Window and prefix input invoke the same command identifiers in the core and do not duplicate feature logic.

The program may change the interface and message language, but it should not automatically remap shortcuts. The default set remains stable and users can create custom profiles. The Polish mnemonic `U` for Ulubione/Favorites is accepted alongside the established `L` for Library; avoiding conflicts and preserving muscle memory matter more than using one language for every mnemonic.

### 5.2. Approved primary map

| Function | Window shortcut | Key after the prefix |
| --- | --- | --- |
| Library | `Ctrl+L` | `L` |
| add to or remove from Library | `Ctrl+Shift+L` | `Shift+L` |
| Favorites | `Ctrl+U` | `U` |
| add to or remove from Favorites | `Ctrl+Shift+U` | `Shift+U` |
| Playlists | `Ctrl+P` | `P` |
| choose playlists and change membership | `Ctrl+Shift+P` | `Shift+P` |
| Queue | `Ctrl+Q` | `Q` |
| add to queue | `Ctrl+Shift+Q` | `Shift+Q` |
| filter the currently loaded list | `Ctrl+K` | `K` |
| AMC command palette | `Ctrl+Shift+K` | `Shift+K` |
| search the current service or source | `Ctrl+F` | `F` |
| search all enabled services and sources | `Ctrl+Shift+F` | `Shift+F` |
| download or retain inside the service | `Ctrl+D` | `D` |
| download to disk when the service permits it | `Ctrl+Shift+D` | `Shift+D` |
| Albums | `Ctrl+Shift+A` | `A` |
| Properties and information for the current or selected item | `Alt+Enter` | no fixed binding |

Filtering only processes data already present in the current list and sends no service request. Current search may query the active service, while global search queries all enabled sources that permit combined presentation. An adapter may require a separate results view; this applies to the real TIDAL adapter, whose content must not be mixed into one list with similar services. The command palette is an accessible, filterable list that also contains commands with no shortcut.

`Shift+U` is a toggle only when the adapter can reliably determine current state. The program says either “Added to Favorites” or “Removed from Favorites”. If state is unknown, the application must not guess and should present explicit menu actions.

Albums are an important and frequently used view, so they receive a shortcut. This is a deliberate exception to full symmetry: the prefix layer uses plain `A`, while the window uses `Ctrl+Shift+A`, because `Ctrl+A` must retain the standard Select All action. `Ctrl+Alt+A` is avoided because `Ctrl+Alt` can act as AltGr and conflict with typing the Polish character “ą”. `L` remains Library, `B` remains reserved for possible future Bookmarks, and `Shift+A` after the prefix is currently unassigned.

Other previously approved layer commands retain `R` for Radio, `M` for Mixes, `H` for History, `N` for Now Playing and `O` for Outputs. The former three information commands are replaced by a single **Properties and information** command on `Alt+Enter`, with no default prefix-layer binding. It opens an accessible read-only text window with Item, Playback, Technical and Source sections. Ctrl+A and Ctrl+C retain their standard meanings; Escape or Enter closes the window and restores focus. The keymap editor resolves conflicts, and a command may remain unbound while still being available through the menu and palette.

Downloads are not part of the core first version. They are enabled per adapter only after the official capabilities, licence and service rules have been checked. Download-to-disk remains experimental and disabled by default.

### 5.3. Time information

Time information remains available as three separate commands so that the user never has to listen to unnecessary data:

| Command after the prefix | Default information |
| --- | --- |
| `Ctrl+E` | elapsed time, for example “1:23” |
| `Ctrl+R` | remaining time, for example “2:57” |
| `Ctrl+T` | total item duration, for example “4:20” |

These are information commands, so Ctrl prevents a collision with the plain `R` Radio view command. By default, only the value is spoken. Every invocation gives the current value; speech must not interrupt the user every second. A braille display and accessible status area may show continuously updated time without automatic speech.

## 6. Transport and arrow commands

Proposed prefix-layer bindings:

| Command after the prefix | Action |
| --- | --- |
| `Space` | play / pause |
| `Left Arrow` | seek backward 10 seconds |
| `Right Arrow` | seek forward 10 seconds |
| `Up Arrow` | increase volume, 5% by default |
| `Down Arrow` | decrease volume, 5% by default |
| `Ctrl+Left Arrow` | previous track or item |
| `Ctrl+Right Arrow` | next track or item |
| `Shift+Left Arrow` | seek backward 60 seconds |
| `Shift+Right Arrow` | seek forward 60 seconds |
| `Shift+Up Arrow` | increase volume by 1% |
| `Shift+Down Arrow` | decrease volume by 1% |
| `Ctrl+Home` | beginning of the track |
| `Ctrl+End` | move near the end of the track, 10 seconds before the end by default |
| `Page Up` | previous session |
| `Page Down` | next session |

Commands unsupported by a session must not be silently ignored. The program should say, for example, “Seeking is not available for WiiM”.

The offset for moving near the end is configurable. The application does not seek to the exact end because doing so could immediately advance to the next track.

After the prefix, all arrow keys control global playback. In an ordinary list, every arrow key retains native list behaviour and remains available for future selection and playlist operations. Without the prefix, transport arrows work only in the player view.

### 6.1. Player view

The player is a view inside the main window rather than a separate modal window. Enter on a track or station ensures that the item is playing and opens this view; an already playing item is not toggled to pause. `Ctrl+Enter` retains its direct list action and does not open the player. `F6`, the Now Playing command, or prefix then `N` shows the player without starting the current selection. Escape returns to the list and item from which the player was **most recently** opened. Each session independently remembers its browser view, focused item per view, filter, and whether the player was active; switching sessions restores that session's own surface. In lists, the current track gains an accessible “Playing” or “Paused” prefix so its state can be identified without abandoning the browsed position.

The player is a single-item surface. `Ctrl+K`, `Ctrl+F` and `Ctrl+Shift+F` do not open filtering or search from it and instead briefly state that those functions are available on lists. View shortcuts deliberately leave the player and open Favorites, Playlists, Library, Albums or Queue. Current-item actions remain available: Favorites, Library, Queue, Play next, playlist membership and `Alt+Enter`.

In the player, Left/Right seeks by 10 seconds, Shift+Left/Right by 30 seconds, Ctrl+Left/Right by one minute, Up/Down changes volume by 5%, Shift+Up/Down by 1%, Home seeks to the beginning and End near the end. Digits `0–9` seek to `0%, 10%, …, 90%` of the duration; this includes numpad digits with Num Lock enabled. The default digit-seek announcement is the percentage alone; the user may select time or percentage and time instead. The binding is player-only, so digits retain native item navigation on lists and `Ctrl+digit` selects a session. Percentage seeking is unavailable when duration is unknown. Alt+Arrow and Ctrl+Shift+Arrow remain unassigned until a concrete need is agreed. Tab moves through real play, seek, volume and return buttons. Session-dependent capabilities such as radio recording will later appear as conditional controls and commands and have no fixed shortcut yet. Player bindings will eventually be configurable alongside the prefix profile.

`Ctrl+J` opens a separate Jump to time dialog. A number alone means minutes, two parts mean minutes and seconds, and three mean hours, minutes and seconds. `Ctrl+Shift+J` opens Jump to percentage and accepts `0–100`. Both shortcuts work only in the player, like the quick digit seeks. Selecting either command from the menu or palette outside the player announces the `F6` instruction and leaves the position unchanged. This split avoids guessing whether `35` means minutes or percent. Prefix equivalents remain subject to the complete prefix conflict review.

Playback tempo follows YouTube's map: `Shift+,` slows down, `Shift+.` speeds up, and `Ctrl+.` restores normal 1.00×. The initial range is 0.50–2.00× in 0.25 steps. Tempo changes independently of pitch. The value belongs to the playback session and remains active when its track changes; an adapter that cannot provide this capability must explicitly report it as unavailable. The window shortcuts operate in the player, while any prefix-layer bindings remain a separate keyboard-map decision.

### 6.2. Resume positions, Bookmarks and global resources

Starting with `alpha.57`, a separate local position is stored for every file together with a fingerprint containing file size and modification time. State is written no more often than once every 15 seconds and on a clean shutdown without modifying the media file. Selecting a file again starts it at its remembered position, but application startup never starts audio automatically. A later preference will decide whether short music tracks resume in the same way as long recordings and podcasts. Streaming retains a position only when the official adapter exposes it; AMC does not invent hidden synchronization.

A Bookmark is a local AMC record containing a stable session identifier, item identifier, position and optional name. It can therefore point to a local file or to a streaming position when the adapter can reopen the same item and seek. Bookmarks form a global application index, but they neither copy nor take ownership of provider content. Starting with `alpha.68`, `B` in the player creates a quick bookmark, `Shift+Page Up/Down` moves among bookmarks in the current item, and `Ctrl+B` opens the global list. Starting with `alpha.70`, `Ctrl+Shift+B` creates a named bookmark. Enter on a row selects its session, opens the item and seeks. `Delete` removes the bookmark record, while `Shift+Delete` cannot delete the source file from this view. Plain `B` on other lists remains type-ahead navigation.

Favorites and playlists remain owned by a specific service or the local library because their state, permissions and identifiers come from that adapter. AMC may expose an aggregated **All Favorites** view and AMC-owned **Collections** containing references from several services, but it will not claim that these are one synchronized provider Favorites list. Queue belongs to the active playback target; global insertion is meaningful only when the host can reliably hand the next item across services or devices.

Shared-mode WASAPI remains the Windows default so AMC can coexist with NVDA. Exclusive, bit-perfect and native DSD output are not part of the base path. Any later advanced mode must be explicit, reversible and must never silently take the screen reader's audio. foobar2000 may later act as an external adapter; its components are not treated as embeddable libraries without separately verified source and licensing.

## 7. Browser window

### 7.1. Lists rather than trees

The primary view will be a standard accessible list or table rather than an expandable tree.

Reasons:

- Enter is explicit and predictable;
- a screen reader does not need to announce many expansion levels;
- restoring position after a refresh is easier;
- the same component can present artists, albums, tracks, playlists and devices.

The order of information in an item's accessible label is configurable. A user may choose title, artist, duration and item type, or put the artist before the title. Fields without a value are skipped. The same order applies to lists and current-item announcements and remains independent from the keyboard profile.

### 7.1.1. Identity, primary name and navigation key

Each row separates three values that must never be conflated:

1. **Technical identity** targets the correct resource when an action is performed.
2. **Semantic primary name** is the default basis for sorting and type-ahead.
3. **Accessible label** contains fields announced by the screen reader in the user's configured order.

No title, artist name or other presentation text is guaranteed to be unique. A resource key includes at least the adapter identifier, account or library namespace, resource kind and provider-native identifier. A separate occurrence key includes the list context and occurrence identifier or position because one track can appear more than once in a queue or playlist. Identical titles from TIDAL, Apple Music and Spotify remain separate results; service and kind are announced as disambiguating metadata but never replace technical identity.

Every view has an explicit presentation descriptor: accepted resource kinds, primary name, available sort fields, current sort, type-ahead key and duplicate disambiguators. Defaults are:

| View or resource kind | Default type-ahead key |
| --- | --- |
| tracks, Queue, History and Now Playing | track title |
| artists | artist name |
| albums and releases | album title; artist and year disambiguate duplicates |
| playlists and mixes | playlist or mix name |
| radio stations and presets | station or preset name |
| shows and podcasts | show name |
| episodes, chapters and music videos | episode, chapter or video title |
| audiobooks | audiobook title |
| devices, rooms and groups | user-assigned name |
| audio inputs and outputs | accessible input or output name |
| single-kind search results | the primary name appropriate to that kind |
| mixed or global results | each result's primary name; kind and service disambiguate rows |

The presentation descriptor also decides whether kind and service are announced. A homogeneous list does not repeat its kind on every row: an opened album omits “track”, and the Albums view omits “album”. A mixed list retains the kind. A single-session list does not repeat the service; a combined search result from services that permit aggregation places it after the item fields. An adapter that requires isolation, in particular TIDAL, opens a separate results list. After plain Enter is pressed or global Search is closed with Escape, the destination service becomes a one-time prefix of the first main-list label, for example, “TIDAL, Anna Kowalska, Edge of Silence”. Further navigation does not repeat the service.

Favorites belong to a specific adapter, account or local library in the current session. `Ctrl+U` shows that session's Favorites and rows do not repeat the service. The first stage does not create separate “global AMC Favorites”. The model still retains the full source key so that a future optional view can aggregate native Favorites from several services without copying them between accounts; such a view will announce the service on every result.

Queue belongs to the active playback session or target, such as a computer, device or zone. It can mix sources only when both adapters and the playback target support that operation. `Ctrl+Q` shows the current session's Queue, so the service is not repeated on every row. There is no single Queue combining concurrently active playback devices.

The field-reading order never changes the navigation key. Sorting by a text field changes the key by default—for example, “Sort: artist. Type-ahead: artist”. Numeric or temporal sorting such as duration, track number or date added retains the primary name unless the user explicitly chooses otherwise. A missing field falls back to the primary name instead of removing the row from type-ahead.

A search query may inspect several fields and aliases, while navigation through the returned results uses one predictable key from the rules above. A service adapter declares available fields and capabilities; the UI never assumes that all services expose identical resource kinds or metadata.

### 7.2. Navigation

| Key in the window | Action |
| --- | --- |
| `Up/Down Arrow` | previous / next item |
| `Home`, `End` | first / last item |
| `Page Up`, `Page Down` | move by pages |
| typing letters | quickly move to an item beginning with the typed sequence; letters typed in quick succession build a phrase, while repeating one letter cycles through matches |
| `Enter` | open an artist, album or playlist; perform the default action on a track |
| `Ctrl+Enter` | play or pause the selection without opening it |
| `Space` | pause or resume what is actually playing, independently of the selection |
| `Shift+Enter` | add the selection to the queue |
| `Ctrl+Shift+Enter` | play next |
| `Delete` | remove from the current playlist, queue, Favorites or library when the action is unambiguous |
| `Backspace` | move up one level; never remove an item |
| `Ctrl+Z` | undo the last membership change; inside a text field, undo text editing |
| `Alt+Left Arrow` | previous view |
| `Alt+Right Arrow` | next view when available |
| `Alt+Enter` | item information |
| no default binding | open the item in the official service application; the command remains in the context menu and palette until the prefix is reviewed |
| `Application key` or `Shift+F10` | context menu |

Enter performs the primary action for the item type: it toggles playback of a track, station or preset, while opening the contents of an album, playlist or artist. `Ctrl+Enter` invokes “Play or pause” without opening: it starts a new selection, pauses the current one, or resumes it. In the future the same rule will play a whole album or playlist without opening it. `Space` controls what is actually playing and does not depend on the current selection. Consistent with file-manager conventions, `Alt+Enter` remains Item Information or Properties; it does not open an external application.

Plain letters in a list never execute AMC commands. They navigate using the current view's explicit key. Single-letter commands work only after the global prefix layer has been successfully activated.

### 7.3. Reloading

- The old list remains visible until new data arrives.
- The program briefly announces “Loading” but does not repeat it for every data page.
- On completion it announces a result such as “24 tracks”.
- After opening an album or playlist it gives a concise summary, for example, “Album: Abbey Road, The Beatles. 17 tracks, 47 minutes 23 seconds”.
- Returning to a previous view restores the previously selected item.
- Refreshing must not unnecessarily return focus to the beginning of the list.

### 7.4. Search and query history

`Ctrl+F` opens current-service search and `Ctrl+Shift+F` opens global search. The window title and the edit field's accessible name identify the scope concisely: “Search TIDAL” or “Search all services”. Enter in the field submits the query and focuses the first result; another Enter opens that result. Direct actions keep the results open, and their announcements always end with the service name. After plain Enter is pressed, main-list focus exposes the service and selected item as one announcement. The same rule applies when global Search is closed with Escape after a direct action, even if the selected service was already active. The service name is a temporary prefix of that item's accessible name and disappears after the selection changes. Global search is an orchestrating operation, not a promise of one mixed list: an adapter declares combined results, isolated results or no search. TIDAL results are presented separately with required attribution.

After a successful query, the native list exposes the result label and its position, for example, “1 of 3”, without an added “Search results” prefix. The visible result count is not raised as a separate live announcement. When detailed hints are enabled, short help about arrows, Enter and Escape is attached to the selected item, so it follows the result name instead of preceding it. Direct actions remain discoverable through the context menu and documentation. No results remains an explicit announcement.

Search history will be local and separated by scope: one history for each service and another for global search. With an empty field, Down Arrow will open up to 20 most recent unique queries, newest first. Arrows only select a query and Enter submits it; selection alone never starts a search. Repeating a query moves it to the beginning instead of creating a duplicate. The user can clear history and disable recording. History contains no tokens, is not synchronized, and is not included in exports without separate informed consent.

After the shared metadata model is extended, plain Left and Right Arrow on result lists and other browsing lists will move through available fields for the focused resource, such as title, artist, album, composer, year and service. The adapter declares available fields and the UI skips missing values. This is one cross-view mechanism rather than a separate implementation for every service.

### 7.5. Long lists and data paging

Letter navigation keeps the existing semantic rule: it searches the primary name appropriate to the current view, such as track title, artist name or album title, independently of the screen reader's configured field order. Letters operate only among already loaded items. Full service-catalogue lookup remains the job of `Ctrl+F`, so typing a letter never launches a series of unpredictable network requests.

Long catalogues and result sets are fetched in pages, provisionally 100–200 items at a time. An adapter translates the service-specific mechanism into one shared page result: items, an opaque continuation token, whether another page exists, and an optional total result count. The UI does not assume that a service knows the total or supports numbered pages.

On the final loaded item, plain Down Arrow or Page Down starts fetching the next page. Focus remains on the same item, identified by its stable ID, while loading. AMC briefly announces “Loading more items” and then, for example, “Loaded 100 more, 200 total”; the next Down Arrow reaches the first newly appended item. A failed fetch does not change selection and ends with an accessible message that permits retrying.

A real **Load more** button may follow the list as a Tab-accessible alternative. It is not a fake media row inside the list, never participates in letter navigation, and disappears or becomes unavailable when no further data exists. Activating the button moves focus to the first newly appended item after loading succeeds.

Loading more appends to the existing collection rather than unnecessarily replacing the entire list source. Updates are batched and the screen reader receives only the final state. When item order is unchanged, only labels are updated. Focus and scroll position are restored by stable ID rather than a raw row number. When a total is known, the message may say “200 of 1,843 loaded”; otherwise, “200 loaded, more available”.

[WinZapp_Python](https://github.com/gabrielhhaber/WinZapp_Python) is the behavioural reference for stable focus, paging and batched accessibility events. AMC implements these ideas independently in .NET and WPF; it neither copies GPL-3.0 code nor changes technology because of this reference.

### 7.6. Local Library and real folders

The local Library separates the **AMC catalogue** from physical storage. Reference mode is the default: AMC stores a stable record and canonical path but neither copies nor renames the file and creates no hidden private copy. `Ctrl+O` adds selected files this way. `Ctrl+Shift+O` registers the selected folder as a persistent **Library source** and includes every recognised file from it and its subfolders. Sources synchronize at startup, after file-system events and explicitly through `F5`. A missing file is marked unavailable rather than deleted from the catalogue, preserving its history, bookmarks and resume position.

A user may register several sources: an ordinary local directory, external drive, network share, or folder managed by iCloud Drive or OneDrive. AMC does not implement its own synchronization for such a source. The cloud provider transfers data, while the local adapter recognizes file availability and Cloud Files placeholders. Indexing alone should not hydrate an entire cloud collection; playback may request the selected file and must announce waiting or failure. Reparse points are controlled so scanning cannot enter cycles.

The **Folders** view mirrors the real hierarchy without a problematic multi-level TreeView. It is a standard list for the current level: directories and files are ordinary rows, Enter enters a directory or opens a file, Backspace or an explicit Parent Folder command goes up one level, and type-ahead searches only the visible level. The flat **Library** remains a parallel view of all records. Albums, Artists and Genres are derived from available tags but never replace Folders or hide files with incomplete metadata.

Removing a Library source removes only AMC references owned solely by that source and never deletes the physical directory. `Delete` excludes a record from the active Library and persists that exclusion, while confirmed `Shift+Delete` uses the system Recycle Bin. Restoring a file at the same path restores the same record's availability. A rename or move observed while AMC is running preserves record identity; fingerprint matching for a move performed while the application is closed remains a later stage. An optional **managed AMC Library** may later copy or move imports into one selected directory as an explicitly enabled mode. That directory may also live in cloud storage, but synchronization remains the provider's job rather than AMC's.

The index database, live state and working files remain local in AppData and are never opened concurrently by a cloud synchronizer. Atomic exports of settings, playlists, bookmarks and full AMC backups may be stored in cloud folders. The source repository, `.git`, `obj`, `bin` and active publish directory also remain outside synchronized folders; GitHub provides source history.

### 7.7. Consistency contract for lists, search and adapters

One interface semantics applies to local files, radio, podcasts, devices and streaming services. An adapter supplies data and declares capabilities; it does not invent its own shortcuts, field order or focus behavior. The shared presentation layer builds ordinary lists, search results, context menus, the command palette and announcements.

The contract includes at least:

- identical meanings for Enter, `Ctrl+Enter`, Space, Escape, the context menu, copying, Queue, Favorites, Library and playlists wherever the adapter declares that capability;
- the same semantic primary name, type-ahead, multi-selection, focus restoration and paging in both the main list and search results;
- the same configurable field order; a missing value is omitted rather than replaced with a guessed value;
- Left Arrow as shared concise information, `Alt+Enter` as full properties, and `Ctrl+C` plus `Ctrl+Shift+C` as the title and public location or real local file respectively;
- direct actions in Search that keep the window open when the same action exists in the main list and needs no subsequent modal dialog; playlist selection closes Search and opens its dedicated manager;
- service attribution in mixed and global results without unnecessary repetition in a homogeneous single-session list;
- shared announcements for loading, no data, unavailable capability, failure, partial success and list boundaries.

Consistency never means pretending that capabilities are identical. If a service does not return bitrate, cannot seek, has no queue or requires isolated results, its adapter explicitly declares that absence or limitation. The UI retains the same binding and responds “Unavailable in this service”, or hides the unavailable action according to user preference; it never performs a different operation under that key. Every real adapter must pass the shared contract tests for lists, search, focus, announcements, errors, paging and every declared action before entering a stable release.

### 7.8. Playback-context and manual-order invariants

The following rules are permanent project contracts rather than notes for one prototype release:

- starting an item stores a stable ordered snapshot of its playable source view; this applies to Library, folder, album, Favorites, Queue, playlist, direct playback from search results and future adapter views;
- merely browsing another view does not change the active context; only deliberately starting an item from a new playable list replaces it;
- if the current file is cut, moved, excluded by Delete, physically removed by Shift+Delete or becomes unavailable during folder synchronization, every code path uses the same recovery algorithm;
- recovery selects only the first still-available item after the removed item in the remembered context; it never falls back to the first record of the session, Library, disk catalogue or another list;
- the selected successor remains paused until an explicit playback command such as Space; when no successor exists, the session has an explicit no-current-item state and playback cannot start a substitute file;
- Queue retains an additional temporary context: it first selects the next Queue entry and, after an automatically entered Queue is exhausted, resumes with the next item of the earlier source view; a directly opened Queue ends without crossing into another collection;
- Playback History and the Bookmarks list remain semantic links under their separately approved rules; opening such a link does not automatically turn that list into a next-file queue;
- `Alt+Up/Down` changes only user-ordered lists. On success, the announcement states direction and the post-move neighbour: “Moved up, above [title]” or “Moved down, below [title]”; a contiguous block also reports its item count;
- boundary, non-contiguous-selection and mixed-Queue-group messages must never imply that an order change succeeded; after success, focus and the entire selection remain on the moved item or block;
- a service adapter must use stable identifiers and this shared logic. If the remote API cannot reorder items, AMC reports that limitation rather than pretending the operation succeeded.

Every change to catalogue refresh, removal, clipboard handling, Queue, lists or adapters must preserve these invariants and pass their regression tests.

## 8. Playlist selection

`Shift+P` in the prefix layer, or local `Ctrl+Shift+P`, opens a small modal window containing:

- a filter field;
- all playlists for the current service, each announced as “contains” or “does not contain”;
- Space to toggle membership in the selected playlist;
- Enter to apply all changes;
- Insert or `Ctrl+N` to create a playlist, F2 to rename it, and Delete to remove the playlist without deleting media;
- `Ctrl+K` to focus the playlist filter; Escape first clears a non-empty filter and otherwise cancels the dialog;
- Escape to cancel;
- exact focus restoration after completion.

Settings may later define a default playlist such as “Listen Later”. A separate command can add to it without opening the chooser. Standard `Shift+P` should present the chooser by default.

## 9. Local window shortcuts

Local shortcuts are independent of the prefix layer but invoke the same internal commands.

Approved primary bindings:

| Local shortcut | Action |
| --- | --- |
| `Ctrl+K` | filter only the currently loaded list |
| `Ctrl+F` | search the current service or source |
| `Ctrl+Shift+F` | search all enabled services and sources |
| `Ctrl+Shift+K` | open the AMC command palette |
| `Ctrl+L` | open Library |
| `Ctrl+U` | open Favorites |
| `Ctrl+P` | open Playlists |
| `Ctrl+Q` | open Queue |
| `Ctrl+Shift+A` | open Albums |
| `Ctrl+C` | copy the selected item's display name |
| `Ctrl+Shift+C` | copy the complete local path or the item's public service link |
| `Ctrl+Shift+U` | add to or remove from Favorites |
| `Ctrl+Shift+P` | open playlist selection and change membership |
| `Ctrl+Shift+Q` | add to the queue |
| `Ctrl+Shift+L` | add to the library |
| `Ctrl+O` | open one or more local audio files |
| `Ctrl+Shift+O` | open a folder of audio files, including subfolders |
| `Alt+1` on a local list | show Library Folders |
| `Alt+2` on a local list | show All files alphabetically |
| `Alt+3` on a local list | show persistent Custom order |
| `Alt+Up/Down` in Custom order | move a file or a contiguous selected block by one position |
| `F5` in the local session | rescan every available source |
| `Ctrl+F5` | open Library folders |
| `F2` on a local list | change only the persistent title displayed by AMC |
| `Shift+F2` on a local list | rename the real file on disk while preserving its extension and AMC data |
| `F6` | open the player view |
| `Left/Right Arrow` in the player | seek backward or forward by 10 seconds |
| `Shift+Left/Right Arrow` in the player | seek backward or forward by 30 seconds |
| `Ctrl+Left/Right Arrow` in the player | seek backward or forward by one minute |
| `Up/Down Arrow` in the player | change volume by 5% |
| `Shift+Up/Down Arrow` in the player | change volume by 1% |
| `Home` in the player | seek to the beginning |
| `End` in the player | seek to 10 seconds before the end |
| `0–9` in the player | seek to 0–90% of the duration in 10% steps |
| `Ctrl+J` in the player | enter and seek to an exact time |
| `Ctrl+Shift+J` in the player | enter a percentage from 0 to 100 and seek to it |
| `Ctrl+Shift+E`, `Ctrl+Shift+R`, `Ctrl+Shift+T` | report elapsed, remaining or total time |
| `Ctrl+Shift+G` | temporarily toggle all automatic player feedback |
| `Ctrl+D` | download offline within the service when supported |
| `Ctrl+Shift+D` | download to a local file; experimental and disabled by default |
| `Delete` | remove from the current playlist, queue, Favorites or library, with confirmation or Undo |
| `Backspace` | move to the parent level; inside a text field, delete a character |
| `Ctrl+Z` | undo the last membership change in Favorites, Library or Queue, or the Play Next state |
| `Ctrl+A` | select all items when the view permits it |

Every local shortcut is configurable. Download commands must not be active until their corresponding module is deliberately enabled.

`Ctrl+1–9` selects a session, `Ctrl+0` opens the session list, and `Ctrl+Page Up` / `Ctrl+Page Down` select the previous or next session.

## 10. Context menu

A context menu is required. It should display only actions available for the selected item type and current service, while preserving a stable and predictable order:

1. Play or pause.
2. Play next.
3. Add to queue.
4. Show album, playlist or artist contents — when applicable.
5. Add to or remove from Favorites.
6. Add to playlist.
7. Add to library.
8. Share or copy link.
9. Download — only when available and enabled.
10. Remove from the current view — when applicable.
11. Information.
12. Open in the official service application.

Each context-menu item should display its current shortcut. Destructive actions should be separated by a divider.

## 11. Keyboard settings

Settings must allow every binding and behaviour to be changed:

- global prefix;
- command-layer timeout;
- session-selection bindings;
- letter and `letter` / `Shift+letter` pairs;
- transport commands;
- local window shortcuts;
- commands with no assigned shortcut;
- Enter behaviour for each item type;
- default playlist;
- persistence of the last session.

The default map is symmetrical: `Ctrl+key` in the active window corresponds to `key` after the prefix, while `Ctrl+Shift+key` corresponds to `Shift+key`. Explicit exceptions are Albums — `Ctrl+Shift+A` in the window and `A` after the prefix because of standard `Ctrl+A` — and the information commands `Ctrl+E`, `Ctrl+R` and `Ctrl+T` inside the layer. Every exception is documented and checked for conflicts.

Interface language is independent of the shortcut profile. Changing language must not rearrange the keyboard. The first version provides only a Windows profile; a macOS profile will be designed with the later macOS edition.

The program may store multiple keyboard profiles. The user selects the active profile, creates an editable copy of the default profile, names it and switches profiles in Settings. The built-in profile remains protected so that a known recovery point is always available.

Required shortcut editor functions:

- capture a new key combination;
- detect duplicates inside the application;
- attempt to detect global conflicts;
- completely disable a command;
- restore defaults for one command or a whole section;
- separate import and export of one keyboard map;
- separate import and export of configuration without keyboard maps;
- import and export of a complete backup containing settings, all profiles, session order and message templates.

No export may contain passwords, tokens or login data. Proposed extensions are `.amckeys.json`, `.amcsettings.json` and `.amcbackup.json`.

The Settings window should provide these categories: General, Language, Prefix and Commands, Sessions and Services, Playback, Lists, Messages and Accessibility, and Advanced. `Ctrl+F` searches settings by name.

## 12. Messages, speech and braille

Every message should be:

- written to a visible, accessible status area;
- emitted as a system accessibility notification;
- available to speech and a braille display through the active screen reader.

Windows UI Automation is the primary mechanism for the first version. The program must not require NVDA. The macOS Accessibility API will be addressed only when work on the Mac edition begins.

Default messages should be brief. The first version will not provide separate Brief, Normal and Detailed profiles. Instead, the user can edit each message template, disable it, or restore its default. A separate global detailed-keyboard-hints option covers the filter plus current-service and global search; it is off by default, does not alter event-message templates, and mentions only arrows, Enter and Escape on search results. Automatic player feedback has a `Ctrl+Shift+G` master switch plus four independently retained categories: digit seeking, Arrow seeking, volume, and playback/pause. Master mute does not alter those category selections. It does not cover explicit time queries, errors or unavailable messages. Digit seeking additionally offers percentage only, time only, or both values.

Message settings:

- separate controls for session, playback, volume, loading and error messages;
- editable text with placeholders such as `{service}`, `{title}`, `{elapsed}`, `{remaining}` and `{total}`;
- buttons to hear an example and restore the brief default template;
- optional earcons instead of selected spoken messages;
- suppression of repeated messages;
- a command to repeat the last message;
- a future direct NVDA module as an optional extension only.

Examples of brief messages:

- “TIDAL”.
- “Added to Favorites: Edge of Silence”.
- “Already in Favorites”.
- “Added to: Listen Later”.
- “WiiM volume: 35%”.
- “Loading album”.
- “24 tracks”.
- “Command unavailable for this session”.
- time commands default to only “1:23”, “2:57” or “4:20”.

Global operations should include the service name when confusion is possible, for example, “Apple Music: added to playlist”.

## 13. Functional architecture

### 13.1. One core, multiple clients

Target solution layout:

- **AMC.Core (.NET)** — UI-independent models, stable command identifiers, sessions, queue, Favorites, Library, playback, configuration, undo history and domain messages;
- **AMC.Host (.NET)** — a running process that owns state, connections, adapters, authentication, cache and the local communication endpoint;
- **AMC.Windows (WPF)** — native Windows window, UI Automation, menus, lists, focus, global prefix and system tray;
- **AMC.NVDA (Python)** — an optional thin add-on that registers NVDA gestures, sends commands to AMC.Host and presents replies, without adapters, large libraries or login data;
- **AMC.macOS (Swift/AppKit)** — a future native window, NSAccessibility, VoiceOver, menus and macOS-specific shortcut implementation;
- **AMC.Adapters.*** — independent modules for music services, devices, radio and local playback;
- **AMC.Update** — updates for the application and compatible adapters.

WPF remains Windows-only. The shared .NET core has no dependency on WPF, NVDA or a platform-specific accessibility API. The native macOS client does not need to load .NET classes directly; it communicates with the host through a versioned command/event contract. A direct binary bridge can be added later if measurements justify it, without changing the command model.

FastSMRW is an architectural reference: one portable core, thin native front ends and an optional control layer that does not require switching to the window. AMC adopts that principle while retaining its own technology, contract and command map.

Clients submit commands such as `favorites.toggle`, `session.next` or `search.current`; the host publishes state events and presentation-ready data. No terminal command is executed. Local communication is asynchronous, versioned, restricted to the current user and resilient to client disconnects.

Local IPC latency is negligible compared with service requests and device responses. A client may retain only a small non-secret snapshot of the last state for immediate reading. Tokens and secrets remain in the host and the operating-system credential store: Windows Credential Manager or macOS Keychain.

### 13.2. Adapters and capabilities

Each adapter declares capabilities instead of pretending that all services are identical. Examples include search, Favorites, Library, playlists, queue, in-service downloads, legal export to disk, seeking, volume, presets, grouping and real-time events. An unavailable command is disabled or produces an explicit message.

The resource-kind catalogue remains extensible. In addition to the current track, album, artist, playlist, station and device, it anticipates shows or podcasts, episodes, audiobooks, chapters, music videos, mixes, presets, inputs, outputs, rooms and device groups. This reflects real service differences: Spotify exposes shows, episodes and audiobooks among other kinds; Apple Music also exposes music videos and stations; TIDAL has its own catalogue resources for tracks, albums, artists and playlists; and WiiM exposes devices, groups, inputs, queues and presets. An unknown future kind retains its native identifier and primary name and can be presented as a generic item without losing identity.

The model distinguishes:

- **sources and catalogues** — TIDAL, Spotify, Apple Music, internet radio and the local library;
- **devices and playback targets** — the local computer, WiiM, Sonos, Bluesound/BluOS and Frontier Smart;
- a **session** — the current combination of source, account, queue and playback target, for example “TIDAL on living-room WiiM”.

Initial real authentication opens the system browser and uses the service's official method. The core must support OAuth with PKCE, callback handling, token refresh, cancellation, sign-out and revoked permissions. Integrations that require a secret or public callback may need a small controlled web backend. Apple Music may have different platform details on Windows and macOS while exposing the same capability set to the core.

Local devices may require discovery through mDNS, SSDP/UPnP or HTTP. Device discovery, authentication and control do not belong in the window UI or NVDA add-on.

#### 13.2.1. Official integration scope

The catalogue source and playback target are independent adapters. Selecting WiiM, BluOS or Sonos exposes the devices, inputs, presets and playback state made available by that ecosystem; it does not automatically expose every service catalogue shown by the manufacturer's own app. A session combines both sides only when an official playback handoff exists, for example “Spotify on living-room WiiM”.

- **WiiM**: its public local HTTPS API covers device information, playback status and metadata, transport, seek, volume, mute, repeat, EQ, alarms, inputs, outputs and 12 presets. It identifies Spotify Connect and TIDAL Connect modes but does not document service catalogue browsing or WiiM Home's universal search. The first WiiM adapter is therefore a device and preset adapter, not a substitute TIDAL API. The WiiM Home app also presents an aggregate surface containing presets, recently played content and per-service Favorites, but AMC does not assume that every such list belongs to the device or is available through the local API. [HTTP API for WiiM Products](https://www.wiimhome.com/pdf/HTTP%20API%20for%20WiiM%20Products.pdf), [WiiM Home App User Guide](https://www.wiimhome.com/pdf/WiiM%20Home%20App%20User%20Guide.pdf).
- **BluOS/Bluesound**: its local HTTP/XML API additionally exposes browsing and search of player-configured sources, including TIDAL, pagination, contextual actions, Favorites, queue management, presets and groups. It is the first candidate for a device session that can intermediate both catalogue access and playback. [BluOS Custom Integration API 1.7](https://bluos.io/wp-content/uploads/2025/06/BluOS-Custom-Integration-API_v1.7.pdf).
- **Spotify**: the Web API covers search, library, playlists, queue, current playback and Spotify Connect devices, including playback transfer and transport control. Player functions require Premium, and restricted devices reject commands. The first real OAuth implementation uses Authorization Code with PKCE and handles Development Mode limits and reauthorization. [Spotify Web API](https://developer.spotify.com/documentation/web-api), [Spotify scopes](https://developer.spotify.com/documentation/web-api/concepts/scopes), [Spotify quota modes](https://developer.spotify.com/documentation/web-api/concepts/quota-modes).
- **Apple Music**: Apple Music API exposes the catalogue and personal library, search, albums, songs, artists, playlists, videos, stations, ratings and Favorites, recommendations and history. Playback uses native MusicKit for Swift on macOS; MusicKit on the Web requires a separate accessibility and integration experiment on Windows. [Apple Music API](https://developer.apple.com/documentation/applemusicapi), [MusicKit](https://developer.apple.com/musickit/).
- **TIDAL**: its API and OAuth 2.1 can expose catalogue and authorized user resources, while playback must use the official TIDAL Player module. Public TIDAL Connect is limited to device partners. TIDAL remains an important separate AMC module, but requires isolated, attributed results, an Open in TIDAL action, minimal data retention and Production Mode review. AMC does not mix TIDAL content with similar services or expose recording or stream export. [TIDAL authorization](https://developer.tidal.com/documentation/api-sdk/api-sdk-authorization), [TIDAL Developer Terms](https://developer.tidal.com/documentation/guidelines/guidelines-developer-terms), [TIDAL Design Guidelines](https://developer.tidal.com/documentation/guidelines/guidelines-design-guidelines), [TIDAL Connect](https://developer.tidal.com/documentation/connect).
A layer similar to an accessible WhatsApp client, wrapping TIDAL Web and improving keyboard navigation, is technically possible only as a cautious experiment. It must not automatically extract the catalogue, playlists or listening history from the DOM because the official terms prohibit scraping and automated indexing of TIDAL. AMC may provide an emergency action that opens the official web player and transfers the user to it; the proper adapter uses the official API and Player module. An experimental accessibility overlay is not treated as the primary adapter without written confirmation from TIDAL.

#### 13.2.2. Provider-specific Favorites and history

AMC normalises commands and navigation, but it does not force provider data into identical semantics. The `favorites.open` and `favorites.toggle` commands are handled by the current service adapter, which preserves native meaning and identifiers. A future aggregate view is only a presentation of clearly attributed provider items; it does not create shared Favorites state or automatically copy items between accounts.

- **TIDAL** represents Favorites as typed user collections: tracks, albums, artists, playlists and supported videos are maintained separately. Adding or removing an item modifies the corresponding TIDAL account collection. Semantically, this is closer to a saved collection or the Apple Music Library than to an additional “like” flag. [TIDAL API Reference](https://tidal-music.github.io/tidal-api-reference/).
- **Apple Music** separates Library membership from Favorite state. A resource may be in the Library without being a Favorite. Favoriting a song also adds it to the system Favorite Songs playlist, while albums and playlists can be filtered by Favorite state. AMC does not equate Favorites with ratings or mere Library membership. [Add Resource to Favorites](https://developer.apple.com/documentation/applemusicapi/add-resource-to-favorites), [Library Albums attributes](https://developer.apple.com/documentation/applemusicapi/libraryalbums/attributes-data.dictionary).
- **Local files** retain separate AMC Favorites. They are not uploaded to TIDAL or Apple Music.

History has two explicitly separate sources:

1. **Played through AMC** — persistent local history maintained separately for every session and service. It contains only playback started or observed by AMC and is not uploaded to a provider.
2. **Provider history** — additional views exposed only when an official API supplies them. Apple Music separates recently played tracks, other resources, stations and heavy rotation; Recently Added is Library activity rather than playback history. [Apple Music History](https://developer.apple.com/documentation/applemusicapi/history), [Recently Played Tracks](https://developer.apple.com/documentation/applemusicapi/get-v1-me-recent-played-tracks). AMC does not assume that TIDAL exposes an equivalent complete account history until the official API and granted scopes are confirmed in a working adapter.

These histories are never merged without source attribution and never overwrite one another. Absence of provider history does not disable AMC's local history.

The **TIDAL Favorites** view is not one flat list. Its first level contains **Tracks**, **Albums**, **Artists**, **Playlists** and, when exposed for the account, **Videos**. Enter opens a category, Escape moves up one level, and type-ahead and filtering apply only to the visible level. `favorites.toggle` routes the selected resource to its correct typed collection. User-owned playlists and merely favorited playlists retain ownership metadata and do not become the same state.

The shared core model distinguishes a **saved collection**, **Library membership** and a **Favorite flag**. TIDAL may map saved collection and Favorites to the same native operation, while Apple Music keeps Library and Favorites separate. `library.open` must not manufacture a duplicate TIDAL data set: after testing a real account it will either open the provider-native parent collection surface or explicitly route to TIDAL Favorites.

After the local interface is stable, the next real integrations are **WiiM** as a device and preset adapter, followed by **TIDAL** and then **Apple Music** as catalogue and account adapters. **BluOS/Bluesound** remains planned but is deferred until real hardware is available for testing. WiiM does not replace the TIDAL or Apple Music API; a session may later connect a source to a playback target only through an officially supported handoff.

- **YouTube**: the first adapter is on demand and does not synchronise an account. Public video, live-stream and playlist search uses the YouTube Data API with a project key. AMC's local YouTube Library is not a copy of the service: it contains items deliberately added to Favorites, local AMC playlists and local playback history. These are sufficient for basic use without login. OAuth remains an optional later extension, started only when the user deliberately wants account subscriptions, playlists or likes; lack of login does not restrict the basic adapter. Playback and transport use the visible official IFrame Player API. AMC presents metadata, results and commands in a podcast-like accessible interface but does not hide the player in the background. Under the API policies, the official adapter does not download content, separate audio, or record parts of videos or live streams. Recording remains limited to sources that explicitly permit it, such as a direct radio stream or a user-owned local file. [YouTube Data API](https://developers.google.com/youtube/v3/getting-started), [YouTube IFrame Player API](https://developers.google.com/youtube/iframe_api_reference), [YouTube API Services Developer Policies](https://developers.google.com/youtube/terms/developer-policies).

  Extraction tools such as yt-dlp do not become the official YouTube adapter or a required AMC dependency. Their YouTube support needs frequent fixes as the player, JavaScript and PO-token requirements change; formats may disappear without warning, and account cookies can expose an account to restrictions. We may later investigate a separate, disabled-by-default Media Tools module for sources that may legally be saved. Such a module runs outside the main process, updates independently, receives neither OAuth tokens nor cookies from official adapters, and its failure cannot disrupt search or playback. [yt-dlp update channels](https://github.com/yt-dlp/yt-dlp/blob/master/README.md#update-channels), [YouTube extractor notes](https://github.com/yt-dlp/yt-dlp/wiki/Extractors).

- **Sonos**: its OAuth cloud Control API discovers households, groups and players, reports state, controls playback, seek and volume, and loads Sonos Favorites and playlists. It does not replace catalogue APIs for existing music services, needs a public HTTPS callback and has a higher integration cost. It stays in scope after WiiM, Spotify, TIDAL, BluOS, Apple Music, radio and local media. [Sonos Control API](https://docs.sonos.com/reference/about-control-api), [Sonos authorization](https://docs.sonos.com/docs/authorize).
- **Frontier Smart**: the manufacturer confirms NetRemote API and SDK access for hardware partners but does not publish a complete supported consumer integration reference. A stable adapter requires partner access; any community adapter is explicitly experimental. [Frontier AURIA](https://www.frontiersmart.com/product/auria/), [Frontier customer area](https://www.frontiersmart.com/customer-area/).

Each adapter separately declares search scope, result-presentation policy, Favorites read/write, Library, playlists, playback, queue operations, playback targets, transport, seek, volume, inputs, presets, groups, in-service offline storage and legal export. The UI never guesses unavailable capabilities.

### 13.3. Local media and radio

The local playback module will eventually cover files and folders, metadata, Library, queue, common formats, output selection, gapless playback and ReplayGain. Shared audio output is the Windows default so that NVDA and other system sounds are not muted. Exclusive output may later appear as an advanced feature with an explicit warning.

The first step implemented in `alpha.33` separates a platform-neutral media-output interface in the core from the Windows implementation. `Ctrl+O` loads files into a non-persistent local session, while the Windows system player provides playback, pause, position and volume through shared output. `Alpha.34` adds recursive folder opening through `Ctrl+Shift+O`, natural name ordering, duplicate suppression, and local `Ctrl+E`, `Ctrl+R` and `Ctrl+T`. This is not yet the complete Library: list persistence, gapless playback, ReplayGain, output selection and optional codecs remain later stages.

Internet radio is a separate core adapter and uses the same sessions, Favorites, history and transport commands. It should support direct streams, M3U/M3U8, PLS and XSPF, station metadata, reconnect and search. M3U8 import distinguishes an ordinary station list from an HLS manifest containing `#EXT-X-` tags. Free Radio mechanisms may be reused after code and licence review without moving the full playback engine into the NVDA process. Radio recording may be a deliberately started local private-use feature: it records an available direct stream without bypassing DRM, never starts automatically, does not apply to TIDAL, Spotify or Apple Music, and leaves compliance with applicable local law to the user.

WiiM Home also provides **Open Network Stream**: individual direct radio or podcast URLs, M3U, M3U8 and PLS import, editing, sorting, preset assignment and M3U export. Public documentation describes this as a WiiM Home app feature but does not establish a supported local API for two-way synchronisation of the complete list. AMC therefore owns a portable stream list, may send a selected URL directly to a WiiM player and may exchange lists with WiiM Home through M3U. It does not inspect private app storage or promise automatic synchronisation unless the vendor exposes a supported API. [Using the Open Network Stream Service](https://faq.wiimhome.com/en/support/solutions/articles/72000636011-tutorial-using-the-open-network-stream-service).

The WiiM adapter consequently separates four surfaces: **device presets**, **WiiM Recently Played** only to the extent confirmed by an API, **service Favorites** owned by their respective accounts, and **AMC Network Streams**. If WiiM Home Recently Played is not programmatically available, AMC shows its own history of playback it initiated on WiiM without presenting that as the manufacturer's complete app history.

The **source is authoritative** principle applies. Whenever an official device or service API can read and modify presets, history, queue, Favorites, playlists, streams or configuration, AMC operates on those source-owned data instead of creating a parallel collection that must be configured again on a phone and computer. Device data are authoritative for device resources and settings, while the service account is authoritative for the user's catalogue. AMC retains only a safe cache and local information not exposed by the source. The cache preserves native identifiers and never overwrites the source after reconnecting without first checking its current state.

An adapter declares read and write support separately for every category. With read-only access, AMC presents source state without pretending to synchronise it. With full access, a change made in AMC is written to the device or service and becomes visible in the official app as well. A clearly labelled local layer and portable import/export are used only when no supported API exists. If WiiM later exposes the Open Network Stream collection, that collection becomes authoritative and M3U remains an interchange and backup mechanism.

VRadio Favorites JSON is planned as another radio import/export adapter. It maps stations, stable identifiers, fallback streams and Favorites groups into AMC's neutral model. The importer tries strict JSON first and uses controlled recovery only for recognized, safely repairable defects. Suspicious URLs and damaged records are skipped with a report, while tokens and signed addresses are never logged. Export recreates a VRadio-compatible structure only from the stations deliberately selected by the user. The private file used for format analysis is not included in the repository or test fixtures.

### 13.4. Testing and responsibility

- core logic has unit tests that do not launch a window;
- the client–host contract has compatibility and versioning tests;
- adapters have contract tests against fakes and separate, deliberately invoked tests using real accounts and devices;
- keymaps are automatically checked for duplicates, unbound commands and conflicts with standard reservations;
- WPF receives keyboard, UI Automation, focus and announcement testing with NVDA, JAWS and Narrator;
- macOS receives separate VoiceOver and Apple accessibility-tool testing;
- an adapter failure must not hang the screen reader or damage configuration for other services.

An NVDA bridge may be used only as a separate optional development tool. It is not an AMC component and is not shipped to users. The test profile is disabled by default, listens only on `127.0.0.1`, requires a random token with no default value, and exposes only window-title, focus-object and navigator-object reads. It must not move focus, speak, display messages, read logs, reload add-ons or restart NVDA. Reports never persist field values or descriptions, the current line or the screen-reader log.

Such a bridge provides a single snapshot of information recognized by NVDA; it does not capture spoken output or prove Tab order, shortcuts, browse mode or braille behaviour. It therefore supplements UI Automation tests and the manual NVDA, JAWS and Narrator matrix. The public `nvda-mcp-bridge` 0.2.0 project is only a reference: it requires hardening before use, and its GPL-2.0 code remains outside AMC source and distribution.

### 13.5. Distribution, libraries, components and updates

The current single EXE is about 162 MB primarily because it is a self-contained publication that includes the .NET runtime. It does not yet contain future services or a complete codec set. Splitting it into many files can make the launcher smaller without necessarily reducing the total installed footprint. Reliable startup without manual library installation takes priority; differential updates and optional components reduce network transfer.

The Windows model is:

1. The primary installed release is a signed MSIX package with a small App Installer file. Installation is per user and requires no manual library copying. App Installer checks on launch and in the background, while MSIX downloads only changed package blocks. A separate technical spike must validate packaged WPF, global keyboard capture, AMC.Host, IPC, OAuth and the tray integration.
2. A portable package remains an additional option for advanced users. It is self-contained but does not silently install system-managed updates and clearly states who is responsible for keeping it current.
3. The public release targets the current .NET LTS. As of this document date, .NET 8 support ends on 10 November 2026 and .NET 10 LTS is supported through 14 November 2028, so migration to .NET 10 happens before the first public release. The self-contained package receives runtime security fixes through AMC releases.
4. Stable and Beta have separate identities and metadata. Switching channels is an explicit user action rather than an accidental version change.

The application package contains one compatible set: AMC.Windows, AMC.Host, AMC.Core, essential built-in adapters and the required .NET runtime. Independently updated components include service and device adapters, an optional local playback engine, optional codecs, non-secret catalogue data and the thin NVDA add-on. Every component manifest contains at least:

- a stable identifier and version;
- platform and architecture;
- minimum and maximum compatible host API versions;
- dependencies and required/optional status;
- size, SHA-256 and signed metadata;
- licence, source location and external-component inventory;
- release channel and update criticality.

The user application never runs `dotnet restore`, NuGet, an installer script or a command downloaded from the Internet. NuGet libraries are selected during release builds, use pinned versions and `packages.lock.json`, and CI uses locked restore, vulnerability audit, licence inventory and an SBOM. A release is produced only from a reviewed, reproducible dependency set.

The component repository uses a mature TUF implementation, or a solution with equivalent properties, rather than a home-grown cryptographic protocol. A trusted root is embedded in the signed application, root keys remain offline, and release keys can be revoked and rotated. HTTPS is mandatory but does not replace signatures. SHA-256 alone detects corruption; signed and timely metadata also protects against substitution, rollback, freeze and mix-and-match attacks.

The update sequence is:

1. check signed metadata in the background without a spoken announcement;
2. choose a complete compatible set for the platform, channel and API version;
3. download into a temporary directory with size limits, resume and metered-network awareness;
4. verify signatures, versions, sizes, SHA-256, dependencies and licences before exposing files;
5. stage the new version beside the active version without overwriting loaded libraries;
6. activate after application exit or at the user's chosen time, never during playback;
7. health-check AMC.Host and automatically roll back if the new version fails to start or respond;
8. retain at least one previous working version and clean older files only after a successful start.

Configuration, the user's library, cache and credentials stay separate from program files. Updates must not remove or replace them. Configuration uses a versioned schema and one-way migration with a safety copy. The NVDA add-on is staged separately and activated only after a safe NVDA restart; the updater does not replace files inside a running screen reader.

For codecs on Windows, AMC first detects and uses Media Foundation capabilities and codecs legitimately installed in the operating system. AMC does not install global codec packs or replace system libraries. A missing format may be supplied by an optional isolated AMC component. If FFmpeg is selected, it is a clearly identified DLL package built to a documented LGPL configuration without GPL or `nonfree` parts, accompanied by the required licence information, corresponding source and an independent update path. A user-supplied engine or codec may be an advanced feature, run outside the main process and clearly marked as unmanaged by AMC.

Updates check and download in the background by default but install at a safe shutdown. They do not steal focus, interrupt speech or playback, or show repeated dialogs. Users may disable automatic download, select a channel, defer installation and inspect an accessible log containing version, size, components, verification result and rollback reason. A critical security release may require updating, but it must always explain this clearly in an accessible window.

On macOS, the client, host and platform components receive a separately signed and notarized distribution that follows Apple mechanisms. Component manifest and compatibility rules remain shared, while installation and platform signing are native to the operating system.

Technical basis for this decision: [.NET publishing modes](https://learn.microsoft.com/en-us/dotnet/core/deploying/), [.NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy), [MSIX and differential updates](https://learn.microsoft.com/en-us/windows/msix/overview), [App Installer automatic updates](https://learn.microsoft.com/en-us/windows/msix/app-installer/auto-update-and-repair--overview), [the TUF specification](https://theupdateframework.github.io/specification/latest/), [NuGet dependency locking](https://learn.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files), [Windows codec support](https://learn.microsoft.com/en-us/windows/apps/develop/media-authoring-processing/supported-codecs) and [FFmpeg licensing requirements](https://ffmpeg.org/legal.html).

## 14. First-version scope

The current Windows prototype should first stabilise:

1. A registered and configurable prefix.
2. A command layer with timeout and cancellation.
3. Three sample sessions, initially even as demonstration modules.
4. Session selection after the prefix with `1–9`, session list on `0`, and sequential switching with `Page Up` and `Page Down`, mirrored by `Ctrl` shortcuts in the active window.
5. Configurable mapping for a small set of letter commands.
6. Messages through system accessibility plus a visible status area.
7. One common list window with Enter, Back and a context menu.
8. A shortcut editor, conflict detection and an accessible command palette.
9. Brief editable message templates, including separate elapsed, remaining and total time commands.
10. Switchable keyboard profiles with a protected default profile.
11. Three import and export types: keyboard map, configuration and complete backup.
12. An automatic-update interface, a signed demonstration manifest and an atomic-rollback test, initially without a public distribution server.
13. Separation of the core from WPF and preparation of the future AMC.Host contract.

The first prototype and initial working release target Windows only. macOS, VoiceOver and possible Siri support are later stages.

State of `alpha.25`: filtering and searching are separate—`Ctrl+K` narrows the current list, while `Ctrl+F` and `Ctrl+Shift+F` open dedicated query and results windows. The local field and title say “Search TIDAL”, or the corresponding service name, while the global variant says “Search all services”. After plain Enter is pressed, the service name becomes a temporary prefix of the main-list item's accessible name, so the whole context is read in one focus announcement. After a direct action, Search remembers the specific last result; Escape applies the same one-time label even if the service did not change relative to when Search was opened. Moving to another item removes the prefix. Global results still place the service after the item fields. Albums and Playlists are homogeneous views: they filter the relevant resource kind and omit the repeated words “album” and “playlist”; Library, Favorites and other mixed views retain the kind. Favorites belong to the current service or local library and Queue belongs to the active playback session, so ordinary rows in those views do not repeat the service. A result is read without a “Search results” prefix or a duplicate count announcement; optional item help mentions only arrows, Enter and Escape. The main-window title starts with the currently playing item, service and view; merely opening another result without playback does not change it. Settings opens with focus on the General tab; Save and Cancel restore focus to the selected item in the main list. Enter on the current track alternates playback and pause, while `Ctrl+Enter` always means Play now; a core test verifies that repeating Play does not toggle Pause. Type-ahead uses the resource's semantic primary name rather than the first field of the accessible label. The core and UI take one version from `Directory.Build.props`, and the portable publication is produced as one unambiguously named EXE. The main remaining first-stage work is local search history, an accessible command palette, low-level capture of a configurable prefix, and candidate-prefix testing with NVDA, JAWS and clipboard managers. Search currently uses the demonstration catalogue; real network queries arrive with service adapters.

State of `alpha.26`: entering Albums, Playlists, Favorites, Library or Queue, and using view history, no longer raises a separate Status live-region summary. The first item receives a one-time view-name prefix, for example “Albums, Strange”, while an empty list is named “Favorites, empty list”. Moving to another item removes the prefix. Integration decisions now separate source from playback target, retain real TIDAL as an important isolated-results module, begin real adapters with WiiM and Spotify, use BluOS's broader device-side capabilities, and defer the higher-cost Sonos integration. Radio includes deliberately started recording of direct streams for private use.

State of `alpha.27`: switching sessions from the list with `Ctrl+1–9` or `Ctrl+Page Up/Page Down` no longer races with the focus announcement. The session slot and service name become a one-time prefix of the selected item and disappear after arrow navigation. A duration in the label remains the duration of that item; no separate list-count and aggregate-duration summary is sent. Hiding item duration, if desired, is a list-field setting decision rather than part of view-announcement logic.

State of `alpha.28`: Search has persistent local history separated per service and for the global scope. Each scope keeps at most 20 unique newest-first queries; repeating an existing query moves it to the front. Down from an empty field begins history browsing, further Down presses move to older entries, and Up returns through newer entries to an empty field. History belongs to the complete state backup but not to the ordinary configuration export. The next small UI stage remains the accessible command palette on `Ctrl+Shift+K`.

State of `alpha.29`: `Ctrl+Shift+K`, or prefix then `Shift+K`, opens an accessible command palette. The edit field filters while typing, Down moves to the native list, Enter executes the selected command, and Escape returns to the main list. The palette also includes unbound commands, exposes active prefix-profile shortcuts and accepts multi-fragment searches without requiring Polish diacritics. Playback remains unambiguous: Enter on the current playing track toggles Pause, while `Ctrl+Enter` always enforces Play.

State of `alpha.30`: “Play or pause” replaces the former “Play now”. `Ctrl+Enter`, the button and the first context-menu item use one selection-aware toggling rule, while `Space` independently controls current playback. The palette exposes both the window shortcut and the prefix-layer shortcut. Typing from the list first extends the phrase, then tries the new character as a fresh query, and clears the filter when neither form matches.

State of `alpha.31`: the palette exposes every active Settings destination, including exact controls for keyboard profiles, bindings, list fields, import, export, messages and planned updates. Navigation without changing a value is the default safety rule. The only deliberate exceptions are direct toggles for accessibility messages and detailed keyboard hints. Each dynamic label states the current value and Enter action, the value is persisted immediately, and a forced confirmation bypasses the global suppression of ordinary messages.

State of `alpha.32`: the message-event list exposes friendly event names only through UI Automation. Raw template text and placeholders are presented in the separate edit field, preventing NVDA from appending `{slot}` or `{service}` to a list-item name while preserving full template editing.

State of `alpha.33`: `Ctrl+O` and the File menu open multiple local audio files in a temporary session assigned to the first free slot starting at 4. Selection alone never starts sound. Enter, `Ctrl+Enter`, Space, seek, volume and time-information commands control real Windows audio output. The `IMediaOutput` boundary stays in the core and `WindowsMediaOutput` stays in the platform layer, so extracting AMC.Host later does not require moving playback logic into WPF.

State of `alpha.34`: `Ctrl+Shift+O` opens a folder and available subfolders without starting playback. Discovery runs away from the UI thread, skips inaccessible directories and reparse-point loops, filters recognised extensions and naturally orders numbered names. The version also introduced local `Ctrl+E`, `Ctrl+R` and `Ctrl+T`, but manual NVDA testing showed that standard WPF handling did not receive them reliably; the fix moves to `alpha.35`. The official-service-application shortcut is deliberately unset until the whole prefix is reviewed. `Alpha.33` feedback confirmed the need for separately suppressible transport announcements; that remains a planned message-settings category.

State of `alpha.35`: after standard WPF key handling failed manual testing, `Ctrl+E`, `Ctrl+R` and `Ctrl+T` are captured earlier at the window-message boundary while text boxes retain normal editing commands. The main list handles Left/Right seeking, Shift minute seeks, Ctrl+Up/Down volume and beginning/near-end commands without the prefix. The palette exposes these active shortcuts. Plain Up/Down remains list navigation, and the context menu no longer announces the occupied `Ctrl+Shift+O` for “Open in official application”.

State of `alpha.36`: the experimental `alpha.35` transport bindings are removed from ordinary lists and moved into the first accessible player view in the same window. Enter opens the player, `Ctrl+Enter` acts in place, F6 shows current playback, and Escape restores the previous item. The player exposes real buttons and periodically updates title, artist, session, state and time without automatically speaking every second. The main window title and accessible name contain the full version number.

State of `alpha.37`: Escape from the player preserves the last browsed position, while the current item is marked “Playing” or “Paused” in the list. Session switching no longer updates the hidden Status live region before its merged focus announcement. Local time information moves to `Ctrl+Shift+E/R/T`; prefix-layer `Ctrl+E/R/T` remain unchanged. The player seeks by 10 seconds without a modifier, 30 seconds with Shift and one minute with Ctrl. Ordinary lists still retain every arrow, while Space, `Ctrl+Enter` and the prefix provide playback control without opening the player.

State of `alpha.38`: the “Announce position after seeking” option on the Messages tab separates automatic seek feedback from explicit time commands. Disabling it covers transport Arrows, Home and End, while `Ctrl+Shift+E/R/T` still respond. The state persists, appears as a safe command-palette toggle, and can be switched directly with `Ctrl+Shift+G`, which always gives a brief confirmation.

State of `alpha.39`: digits `0–9` in the player seek to `0–90%` of the duration. Numpad digits also work with Num Lock enabled, without changing digit behaviour on lists or `Ctrl+digit` session selection. The seek respects the automatic-position-announcement setting, and unknown duration produces an explicit unavailable message. The command palette exposes all ten percentage positions. The adapter plan now records an official YouTube integration without initial account synchronisation and without downloading, audio extraction or recording of YouTube content.

State of `alpha.40`: the default digit-seek announcement is the percentage alone. The Messages tab selects percentage only, time only, or percentage and time, and the command palette opens that control directly. `Ctrl+Shift+G` and the shared checkbox silence both automatic time values and volume values after a change. Playback, pause, error messages and explicit time commands remain audible.

State of `alpha.41`: master `Ctrl+Shift+G` preserves separate choices for digit-seek, Arrow-seek, volume and playback/pause messages. Each category has its own checkbox and command-palette entry. A non-live status bar at the bottom of the main window is read through `NVDA+End`: service, state, title, position and total duration, volume and bitrate. Local bitrate is marked as an estimate; missing metadata is never replaced by an invented value.

State of `alpha.42`: the status bar moves from an inset panel to the actual bottom edge of the window, matching how `NVDA+End` locates it. Its direct child exposes the changing text through UI Automation. `Ctrl+G` opens an exact-time input and a separate command accepts a `0–100` percentage; both appear in the Playback menu, player and command palette.

State of `alpha.43`: after the negative NVDA test, the WPF bar is replaced by a native Windows status bar hosted at the bottom edge. Exact time and percentage are global commands for the current playback session and remain available while browsing a list; digits `0–9` stay local to the player. Seek-dialog validation raises an active error notification, selects the invalid value and keeps focus in the edit field.

State of `alpha.44`: inspection of NVDA's implementation shows that, without an app module, `NVDA+End` probes only the object at the lower-left pixel of the window bounds. A native bar hosted inside WPF still did not cover the frame. The Windows layer therefore creates a non-activating, almost transparent `msctls_statusbar32` object across the possible Win32 and DWM lower-left bounds. It carries the current bar text, remains outside navigation and follows the owner window.

State of `alpha.45`: the final local player mapping uses `Ctrl+J` for an entered time and `Ctrl+Shift+J` for an entered percentage. Both commands and the digit seeks are player-only. On a list the keys are not captured; selecting a seek command from the menu or palette explains that `F6` opens the player. `F6` is the common Now Playing entry for local media, streaming, radio and device sessions, although duration-dependent seeking remains unavailable for a live source with no known duration.

State of `alpha.46`: the separate almost invisible compatibility window from `alpha.44–45` is removed completely because manual testing showed it taking over NVDA's startup context and blocking reading and keyboard input until `Alt+F4` closed it. A status bar must not be implemented as another top-level or helper window. The main window exposes its own client-area bounds to automation, while the real status bar remains an embedded, non-live control. `NVDA+End` is a compatibility test rather than a usability dependency: Playback and the command palette always contain “Read playback status”, which announces service, state, title, time, volume and bitrate without moving focus.

State of `alpha.47`: the status-bar container no longer duplicates its label's full accessible name. The single announcement is ordered for a quick parameter check: bitrate, state, position and total duration, volume, title, and service last. The explicit “Read playback status” command uses the same formatting.

State of `alpha.48`: the status-bar reading omits volume and keeps this order: bitrate, state, position and total duration, title, service. The explicit full-status command can still include volume. The shared announcement control now raises one UI Automation notification carrying the message text; the parallel `LiveRegionChanged` event is removed because it could intermittently expose the static “Program status” name instead. The control's automation name is its current text, so manual object inspection does not expose that technical label either.

State of `alpha.49`: local playback moves to NAudio 2.2.1, shared-mode WASAPI and SoundTouch.Net 2.3.2. `Shift+,` and `Shift+.` select 0.50–2.00× in 0.25 steps, and `Ctrl+.` restores normal speed. SoundTouch changes tempo independently of pitch. Rate is session state and survives a track change and the core rebuild performed after saving settings. Sessions without a supporting output report unavailability. To comply with LGPL replacement requirements, the SoundTouch assemblies remain separate files beside the EXE together with the complete licence text and source reference; NAudio is MIT-licensed. Publication is therefore a clearly versioned folder rather than a single file. Technical basis: [NAudio](https://github.com/naudio/NAudio), [SoundTouch.Net](https://github.com/owoudenberg/soundtouch.net), [YouTube shortcuts](https://support.google.com/youtube/answer/7631406).

State of `alpha.50`: the status bar begins directly with audio values instead of the redundant word “bitrate”, for example “about 192 kb/s, 48 kHz”. Bitrate remains explicitly estimated when derived from file size and duration, while sample rate comes directly from NAudio's source format. `Ctrl+I` and prefix `I` open item information. `Ctrl+Shift+I` and prefix `Shift+I` report full playback status. Extended technical information remains in the menu and command palette without a fixed shortcut.

State of `alpha.51`: the status bar removes “about” and omits the entire audio-parameter segment when the source provides none. `Ctrl+I`, `Ctrl+Shift+I`, prefix `I` and `Shift+I`, and the three old information commands are removed. One `Alt+Enter` **Properties and information** command replaces them with accessible text and does not expose tokens or signed URLs. Filter and search do not open from the player, while current-item actions remain available. View, filter, selection and player-active state are remembered per session. Escape returns to the location from which F6 or the player was most recently invoked. The radio plan now includes M3U/M3U8, PLS, XSPF and VRadio Favorites import/export.

State of `alpha.52`: Queue is the logical union of queued and Play Next state, but every removal command clears both so an item cannot remain visible with a contradictory “added” message. Ordinary lists support extended `Shift+Arrow` selection; membership, playlist and Delete actions cover the whole selection and history records one undoable batch. Natural end of a local file chooses Play Next, then Queue, then the following item in the loaded list, without wrapping the last item. Volume is software gain inside AMC before shared WASAPI and does not control NVDA's audio session. Session switching also reports the restored view. The main title deliberately continues to begin with the currently playing item rather than the browser selection; its later view segment distinguishes Queue, Favorites, and other surfaces.

State of `alpha.53`: `Alt+Enter` information is a row list rather than a multiline edit control because manual NVDA testing could not reliably read the previous control. The first row is the title; the redundant and confusing Basic information heading is removed. `Ctrl+Shift+C` distinguishes a complete local path from a service's canonical public link. The data model keeps that public link separate from a potentially private or short-lived playback source. `Alt+Left/Right` history works throughout the main browser and explicitly announces Back or Forward plus its destination; it remains experimental and can be removed if another test finds that it adds more complexity than value.

State of `alpha.54`: after manual testing, the row list is replaced with a native read-only Windows `RichTextBox`. It exposes text, caret and selection ranges to a screen reader, enabling character, word and line review plus arbitrary-range copying. This is the standalone equivalent of the browsable-message idea available to NVDA add-ons: AMC does not call `ui.browseableMessage`, because that API lives inside an NVDA add-on and would tie a normal application window to one screen reader. The decision follows the text-range model in [NVDA's technical architecture](https://github.com/nvaccess/nvda/blob/master/projectDocs/design/technicalDesignOverview.md). Enter on Copy all is no longer intercepted as a close request: copying announces success and leaves the window open. A local path is placed in the initial information block. The player gains a current-item context menu, while the list menu adds Library and both copy operations; stateful labels explicitly say Add or Remove.

State of `alpha.55`: shared properties are ordered as identity/source, **In application**, and **Technical**. The first contains title, artist, kind, service and local path; the second contains current playback and all membership state; the third contains duration, format, size, bitrate and sample rate. Context menus no longer rely on a separate accelerator attribute that NVDA omitted when reviewing the current focus. The shortcut is part of the item's accessible name while the visual `InputGestureText` remains unchanged. View history is defined as session-local: each service has independent Back and Forward stacks, and session changes use only the dedicated session commands. History feedback names the session so the boundary is audible.

State of `alpha.56`: the history focus prefix is ordered as **direction → destination view → session**, for example “Back, Queue, Local media”. `MessageSettings.HistoryMessages` controls only the added Back/Forward feedback and the empty-history message; it does not alter the stacks or shortcuts. The option has a checkbox on Messages, its own settings target and a command-palette entry, so configuration export includes it. The master `Messages.Enabled` takes precedence. It is intentionally independent of `SeekMessages`, whose scope is automatic player transport feedback. Explicit NVDA object review of the player remains intentionally rich and is not an automatic application announcement.

State of `alpha.57`: when `HistoryMessages` is disabled, history still exposes **destination view → session → item** and removes only the Back/Forward word; disabling the master `Messages.Enabled` switch removes the complete additional context. The main-window title has the stable order **module → playing or paused item → session → application and version**. The local library, memberships, current file, volume, rate and per-file positions are durable state and part of a full backup. A file fingerprint prevents resuming replaced content, while restoration never starts playback automatically. User data remains in AppData independently of the portable application folder; MSIX/App Installer remains a separate distribution stage. A raw 48 kHz AAC/ADTS sample was successfully recognized and decoded by the current NAudio–SoundTouch pipeline; a manual application test will determine whether the reported silence was specific to a file or an interface action.

State of `alpha.58`: after manual testing, the title order is **playing or paused item → module → session → application and version**, making content easier to identify in the task switcher. A second AMC launch does not create another process; it brings the deepest visible window of the first instance forward, preventing concurrent `state.json` writes. Modal dialogs are retained only for bounded tasks, while the player remains a view. Optional tray operation will be off by default and will not change the meaning of `Alt+F4`. `Page Up` starts the previous item and `Page Down` the next without wrapping; `Shift+Page Up/Down` remains reserved for bookmarks within the current file. All bookmarks will also appear in a global Bookmarks index, but player navigation will not jump into a different file by itself. Escape returns from the player to the list without stopping audio because it is navigation rather than transport. OGG/Vorbis receives an explicit NAudio.Vorbis/NVorbis decoder. Local position is also saved at transport boundaries and when the window loses activation. Folder hierarchy, safe library removal and moving a local file to the Recycle Bin form the next separate stage.

State of `alpha.59`: the visible session name is shortened to **Local files**. The internal default-view identifier may remain “Media”, but it is omitted from the title and NVDA context when redundant. On that session's main list, Left speaks cached format, artist, duration, audio parameters and size, while Right opens the standard action menu with “Open” and “Open with…”. This quick read never scans file contents synchronously. Local `Ctrl+Shift+C` writes both `UnicodeText` and Windows `FileDrop`, including multiple selections; service adapters still copy only canonical public URLs. `Delete` in the default local catalogue removes records from AMC without touching the file system and participates in the same chronological `Ctrl+Z` history as Favorites, Library and Queue changes. The final item can also be removed: an empty session is detached without a fake placeholder, and undo restores its order and remembered positions.

State of `alpha.60`: every session has persistent playback history capped at 500 unique items. Replaying an item moves it to the front instead of adding a duplicate. `Ctrl+H` opens its ordinary newest-first view. The last played item is marked on lists, but restoring the application neither starts it nor changes selection. In the player, `Alt+Down` moves to an older entry and `Alt+Up` to a newer one; every item resumes at its remembered position. The navigation snapshot remains stable even though playing an entry makes it newest. `Page Up/Down` still follows the source list. `Alt+Left/Right` view history remains a separate per-session in-memory stack. Physical `Shift+Delete` is limited to local sources, requires confirmation, releases an open file, moves successful entries to the Windows Recycle Bin, and removes their history records; it does not create a misleading `Ctrl+Z` operation. Plain `Delete` remains reversible catalogue removal. Side arrows work in all local views, explicit time commands also work in the player, `Ctrl+C` covers all selected names, and session switching speaks the slot and session before the restored module.

State of `alpha.61`: Right Arrow is now a direct shortcut for the Windows `openas` picker rather than a replacement for AMC's context menu; Left still speaks quick information. `Shift+Delete` is available on both the list and the player. In the latter it targets the actually open item, confirms intent, stops playback, and releases the file handle before the Recycle Bin operation. Deletion clears both persistent identifiers and the active `Alt+Up/Down` snapshot. Local state clears `CurrentItemId` when its session no longer exists, and history normalization retains only identifiers present in the local catalogue. Recycle Bin latency can come from the synchronous Windows shell and iCloud synchronization; destructive work is not moved to a background thread without a separate design for cancellation, duplicate-command locking, and shell prompts.

State of `alpha.62`: Left Arrow fills missing metadata for only the selected file. The probe uses the same NAudio/Vorbis reader selection as playback without starting audio. Average bitrate is estimated as file bits divided by duration, so VBR reports an average rather than an instantaneous value. Duration, sample rate, and `kb/s` are persisted in local state. A failed probe does not suppress extension, artist, or size. The main window explicitly handles `Alt+F4` before the player layer and calls `Close`; because the player is a view rather than another window, closing is not two-stage. A modal dialog keeps standard Windows behavior, so its own `Alt+F4` closes that dialog first.

State of `alpha.63`: a remembered local-file position remains session state even when the decoder has not opened a source after application restart. Time queries, F6 and the first resume therefore use the stored position instead of the empty audio output's technical zero. Right Arrow now calls the documented Windows `SHOpenWithDialog` directly and no longer depends on an existing extension association. On Windows 10 and later this dialog opens one file with the selected application; default-app changes are managed by Windows Settings. Physical `Shift+Delete` remains list-only. In the local player, `Delete` removes the current record from AMC, leaves the file on disk, pauses it and announces the next item or destination session. `Ctrl+Z` can undo this catalogue operation.

State of `alpha.64`: manual NVDA testing showed that the system `SHOpenWithDialog` takes activation away from AMC but does not establish a readable focus in the application picker. Focus returned only after `Alt+Tab`. The Open with function and its Right Arrow action have therefore been removed completely. Right Arrow retains the list control's standard semantics, while the menu still offers Open in default application when the file has a working association. AMC will not maintain a parallel and inevitably incomplete Windows application picker.

State of `alpha.65`: after clarifying that a useful function should not be discarded because of one NVDA focus problem, Open with returns as a controlled experiment. The system dialog is no longer opened inside the `PreviewKeyDown` or menu Click call stack. AMC first completes WPF key and focus processing, then opens the native picker when the dispatcher becomes idle. Right Arrow and the menu item use this same deferred path. Testing both entry points will determine whether to keep the complete feature, retain only its menu entry, or investigate another system mechanism.

State of `alpha.66`: `alpha.65` produced the same unreadable focus from Right Arrow and the menu, proving that the trigger was not the cause. The final experimental variant launches the system `OpenAs_RunDLL` entry point in a separate `rundll32.exe` process. AMC no longer owns the picker or blocks its WPF thread, allowing Windows to give the shell process ordinary foreground focus. If NVDA testing remains negative, Open with will be removed without further workarounds and stable opening in the default application will remain.

State of `alpha.67`: the separate `alpha.66` shell process also failed to open a usable picker; Right Arrow produced no visible or spoken operation. After three tested implementations, Open with is permanently removed from Right Arrow and from both list and player menus. Right Arrow retains the list control's standard semantics. Stable Open in default application remains in the local-file menu, while choosing or changing associations is left to Windows outside AMC.

State of `alpha.68`: the persistent Bookmarks index stores session, item, title, position and creation time. `B` in the player adds a quick bookmark without a dialog, and adding again within the same second does not create a duplicate. `Shift+Page Up/Down` stays within the current item and never jumps into another file. `Ctrl+B` opens one accessible list across all sessions with filtering, type-ahead, Enter activation and safe deletion. Full backups include Bookmarks; settings-only exports deliberately do not.

State of `alpha.69`: bookmark navigation keeps a short-lived anchor at the last reached bookmark, so repeated `Shift+Page Up/Down` presses cannot select that same bookmark again merely because playback advanced. Any other command clears the anchor and the next jump is calculated from the real position. A separate message setting silences successful bookmark jumps while retaining first/last boundary feedback. Portable bookmark exchange is specified in `PROJEKT_IMPORTU_EKSPORTU_ZAKLADEK.md`; this version continues to provide bookmarks through the safe full backup.

State of `alpha.70`: `Ctrl+Shift+B` opens one accessible modal name field for the player's current position. Names are limited to 200 characters, normalized to single spaces and stored with each bookmark. A bookmark within the same second receives the supplied name instead of being duplicated. The global list reads **bookmark name → media title → time → session**; unnamed quick bookmarks retain the previous format.

State of `alpha.71`: the global list no longer uses creation date as playback order. Bookmarks for the currently selected item come first in ascending media-position order. Remaining records are grouped stably by session and media title, then ordered by position. Sorting every item by time alone is deliberately avoided because it would interleave unrelated files. Enter retains the return-row identifier, shows the player and focuses its main button; Escape rebuilds the list, reselects that identifier and restores list focus.

State of `alpha.72`: creation date is presented as information but still does not control playback order. A global row uses this order: file or media title, local bookmark creation date, media position, optional name, session and the “bookmark” kind. In the open player, `Shift+Page Up/Down` announces only the position, or the name and position. Copying from the global list preserves the full visible description of every selected row.

Windows clipboard behavior in `alpha.72`: `Ctrl+C` means a textual title, while `Ctrl+Shift+C` means physical files plus textual full paths for local sources, or a URI for a service item. Both actions are also available on search results without closing the search window. `Ctrl+X` is a real system operation that prepares local files for moving and works in ordinary lists and on local search results, but not in Bookmarks or for streams. AMC does not remove a record on cut alone; after an actual `Ctrl+V` and reactivation, it removes only records whose old paths no longer exist.

State of `alpha.73`: search results support multi-selection and bulk copying of titles or physical files, paths and URIs. Cut and paste are explicitly blocked in results; this does not affect the search text box, where ordinary text clipboard behavior remains standard. In regular local lists, `Ctrl+V` imports clipboard files as existing sources without copying their data into an application directory. Allowed destinations are Media, Library, Queue and Favorites; the last two also set the corresponding membership. Playback History and Bookmarks are not import targets. An internal paste converts a Windows cut into an ordinary clipboard copy so the file cannot be moved later by an accidental external paste.

State of `alpha.74`: the global Bookmarks index is a transient view rather than a persistent session start location. Explicit `Ctrl+B` remembers the source session, view and selected item. The sequence Enter → player → Escape returns to that bookmark row, and the next Escape restores the remembered source context. An active filter takes precedence, so the first Escape only clears it. A previous run ending on the Bookmarks list is normalized to Media on the next launch; an open player may still be restored, but leaving it leads to the ordinary list. `Shift+Page Up/Down` is handled as bookmark navigation only while focus is within the player. On lists it remains WPF's standard extended page selection and cannot change the current view.

State of `alpha.75`: the Left Arrow quick-information action is shared by ordinary lists and search results in every session. Its order consists of available title, local format, artist, duration, compact audio parameters and local file size. For a file, AMC may read header metadata and estimate bitrate from size and duration. For streaming, it may announce only values returned by the adapter; a missing value must never be replaced with an assumption about the service's quality. The action opens no dialog and retains focus.

State of `alpha.76`: physical `Shift+Delete` uses COM `IFileOperation` with explicit Recycle Bin flags because the legacy mechanism did not reliably support Cloud Files reparse points, including existing iCloud Drive placeholders. AMC first closes the playback pipeline when it owns a selected file, then submits each file to the Recycle Bin. A catalogue, history or view record is removed only after confirmed success. Failure or cancellation preserves both record and file, cannot terminate the application process, and is reported with its HRESULT. Partial success for a multi-selection is permitted and reported.

State of `alpha.77`: `Ctrl+Shift+O` registers the selected directory as a persistent source and opens an accessible **Folders** view implemented as a standard single-level list rather than a multi-level TreeView. Subfolders precede files; Enter enters a folder or opens a file, Backspace goes up, type-ahead and `Ctrl+K` apply to the visible level, and `Ctrl+F` searches the complete local session. Sources and the current folder level are persisted, while the flat Library remains available in parallel. Session order is editable in General Settings and is the single source of truth for `Ctrl+1–9`, the session list and `Ctrl+Page Up/Page Down`; the default is Local Files, WiiM, TIDAL, Apple Music. List-field ordering now lives in a List item reading section on the Messages tab without changing its persisted model, export behavior or command-palette targets.

Correction in `alpha.78`: the logical Local Files session is created at startup even when it contains no media. Its assigned shortcut therefore always resolves, while item-dependent commands report an empty session. Choosing a source through `Ctrl+Shift+O` switches to the local Folders view and persists the source before asynchronous discovery begins, so another service's demonstration list is no longer left visible during scanning.

Correction in `alpha.79`: Folders and Library are not separate data catalogues. A registered folder is a Library source, Folders presents its physical hierarchy, and `Ctrl+L` presents a flat alphabetical set of the same records. Repeating `Ctrl+Shift+O` restores known files that had previously been removed from the Library. Delete on a file in Folders removes only Library membership and leaves the file on disk; `Ctrl+Z` restores membership. Delete on a folder row removes neither the source nor the directory. Source management will use a separate unambiguous command, while the physical Recycle Bin remains exclusive to confirmed `Shift+Delete`.

Correction in `alpha.80`: Delete stores a persistent exclusion, so the watcher, `F5` and restarting cannot silently add an item again; immediate `Ctrl+Z` removes the exclusion. Sources are scanned at startup and watched while AMC runs. An unavailable source is not mistaken for an empty folder, while missing files retain their data and return when they reappear at the same path. `Alt+1` selects Library Folders, `Alt+2` selects flat All files, `Ctrl+L` opens the last local layout, and `F5` forces a complete scan. Shift plus a digit is not a view shortcut: the punctuation produced by the keyboard layout participates in type-ahead navigation.

Correction in `alpha.81`: `ReparsePoint` no longer automatically means a link. Cloud Files placeholder files and directories with no link target are indexed by name and attributes without opening their contents; real symbolic links and junctions remain excluded. Migration repairs a source for which alpha.80 accidentally converted every legacy record into an exclusion. Moving from flat `Alt+2` to Folders with `Alt+1` sets the level to the selected file's parent directory and preserves its stable ID. Queue remains active-session state: opening a local source switches to Local Files but must not clear another session's Queue.

Correction in `alpha.82`: directory classification checks a real link target first and uses `Offline`, `RecallOnOpen`, `RecallOnDataAccess`, `Pinned` and `Unpinned` only when a provider rejects that query. This protects OneDrive and other Cloud Files implementations without allowing ordinary junction traversal. Mirrored Google Drive is an ordinary local source. In Stream mode its virtual drive must be available, but temporary disappearance is a source-scan failure rather than evidence that every file was deleted. Discovery reads names, attributes and extensions only; payload hydration may start only after explicit playback or metadata access for a selected item. The design follows [Microsoft Cloud Files](https://learn.microsoft.com/windows/win32/cfapi/cloud-files-functions) and [Google Drive: Stream or mirror files](https://support.google.com/drive/answer/13401938).

Correction in `alpha.83`: folder roots have a dedicated accessible manager opened from the File menu or command palette. Each status exposes reachability, active, unavailable and persistently excluded records, plus the full path. Registration rejects identical, nested and mutually containing roots so a file cannot have ambiguous ownership. Safe detachment removes only the automatic synchronization registration; catalog records and every relationship remain untouched, and no disk file is changed. The single `.amcbackup.json` safety format includes the local catalog, roots, exclusions, favorites, queues, history, bookmarks, resume positions and settings, but no credentials. A second catalog-only backup format is intentionally avoided.

Correction in `alpha.84`: the player view is the transport-control boundary. By default, `Escape`, `Shift+F6`, the back button and direct navigation to another view pause audio and reveal the ordinary list; a General setting can deliberately allow background playback instead. Pausing does not itself mean resetting. A separate policy controls durable local-file resume positions: the global default is enabled, and each registered folder source may inherit it or force remembering or starting from the beginning. Individually added files inherit the global rule. Ordinary lists do not acquire seeking or volume shortcuts.

Correction in `alpha.85`: every data model used directly as an accessible list item must also return its friendly label from `ToString()`, because WPF UI Automation may ignore `DisplayMemberPath`. The source manager no longer exposes the class name, identifier or record property names to NVDA.

Decision in `alpha.86`: `F5` refreshes local sources and `Ctrl+F5` opens Library Manager. On a local list, `F2` sets a persistent catalogue alias without changing the path; entering the file name without its extension again removes the alias distinction. `Shift+F2` performs a real on-disk rename while always preserving the extension and stable record identity. The path update retains membership states, History, Bookmarks and resume position. The operation never overwrites an existing target and releases a previously loaded file through a safe audio-output stop.

Decision in `alpha.87`: the local Library separates data layout from sorting. `Alt+1` shows real Folders, `Alt+2` always derives All files alphabetically, and `Alt+3` shows persisted Custom order. `Alt+Up/Down` works only in the third view and moves one item or a contiguous block; an active filter blocks it so hidden records cannot move unpredictably. The first Custom order starts alphabetically, then retains manual edits and appends new records. It is AMC metadata and never modifies the file system. A future service adapter may route the same command to a remote order only when the official API explicitly supports it; other views must not pretend to persist remote changes.

The `Ctrl+K` filter is a short-lived narrowing of the current context, not sorting and not a service query. Escape removes it and restores list focus in one step. Moving to another view, folder or session also clears it, and AMC never restores it after restart. Search results remain a separate window and do not offer manual reordering.

Implementation in `alpha.88`: local `Ctrl+Shift+A` derives Albums with a conservative folder heuristic. A candidate contains at least two direct audio files, at least two distinct track numbers, and numbering on at least half its files. One- or two-digit prefixes are accepted when followed by the end of the name, a space, hyphen, underscore, dot or closing parenthesis. Three- and four-digit prefixes, including years in recording names, are rejected. An album row is a container rather than a fake file: Enter opens its tracks, Escape returns to the album, and the menu does not offer file actions on the container itself. Track actions remain ordinary Library operations. This first implementation opens no file payload and hydrates no cloud placeholder; it analyses persisted paths only. Tag reading and a manual classification override remain later stages.

Decision in `alpha.89`: view order and playback order are separate, explicit concepts. Starting an item from Favorites, Queue, an opened Album, the current Folder, All files or Custom order stores the full unfiltered list of identifiers as the session's **playback context**. `Page Up`, `Page Down` and end-of-file continuation all use that same context. Merely browsing another view does not replace it; starting an item from the new list does. History, Bookmarks and search results remain locators rather than hidden result playlists. Explicit Play next and Queue items take priority, after which playback resumes at the item following the original position in the remembered context.

`Alt+Up/Down` is meaningful only on lists with user-owned order: Custom order, Favorites, an open editable Playlist and Queue. From `alpha.98`, all four variants are implemented. Folders retain disk hierarchy, All files stays alphabetical, Album follows track numbers, and History and search preserve their semantic order. Reordering never changes a disk file. A service adapter writes remote order only where the official API supports it; otherwise AMC order is explicitly local metadata.

Item options are separate from information. `Alt+Enter` remains read-only text, while `Alt+Shift+Enter` opens editable **Item playback options**. Local resume policy is hierarchical: global setting, folder-source override, individual-item override. Playback rate has a session rule plus an optional item override; moving to another item restores that item's value or the session value. Per-item output and EQ have reserved model space but stay disabled until the output layer can enumerate devices and switch shared WASAPI safely without losing NVDA speech.

The `alpha.90` extension gives playback settings the hierarchy `file > nearest folder > Library source > global setting`. A folder override is separate AMC metadata identified by a normalized full path; it neither creates another source nor changes disk structure, and it also covers future files discovered below that path. An `inherit` value skips that level, so a folder speed override does not prevent its resume rule from being inherited from a parent. File records are matched first by stable identifier and, as a safe fallback, by normalized case-insensitive path.

Clipboard operations in `alpha.91` pass through one Windows STA layer. Text, `UnicodeText`, `FileDrop` and `Preferred DropEffect` writes use time-bounded retries for `CLIPBRD_E_CANT_OPEN` and the corresponding WPF exceptions. This layer creates no second clipboard queue and changes no data semantics: `Ctrl+C` remains text, `Ctrl+Shift+C` carries text plus files, and `Ctrl+X` additionally sets the move effect. A success announcement is produced only after `SetDataObject(..., copy: true)` succeeds; persistent contention is reported while selection and focus stay unchanged.

Choice objects in the options dialog must have a stable textual representation equal to their visible label. WPF `DisplayMemberPath` alone does not cover every UI Automation path, so `alpha.92` also provides a text-search path and an explicit `ToString()` result for resume-policy and rate choices. The model enum or numeric value remains separate from the accessibility announcement.

The `alpha.93` durable-data layer uses embedded SQLite. Separate tables hold local records, folder sources and rules, exclusions, custom order, local-session state, Bookmarks, History, Favorite order, from `alpha.97` Playlists with their ordered items, and from `alpha.98` each session's Queue order; indexes cover title, path and view membership. UI settings, keyboard profiles, query history and session navigation remain in a small JSON file. Migration from the prior state is a single transaction, verifies the item count and retains the input copy. The full export remains independent of the database format and can still be imported on another computer.

Cloud Files hydration is a playback operation, never an indexing operation. The scanner reads names, extensions and attributes only; views and information commands do not open placeholder payloads. Explicit playback of one record prepares its decoder on a worker thread, announces download state and assigns a request generation: cancellation, a track change or timeout invalidates the result so a late file cannot begin playing. The process log records open stages, device failures, unhandled exceptions and detected periods of UI non-response, but is never synchronized to a cloud folder.

Critical window shortcuts may receive an additional handler at the Win32 message boundary when WPF, a hybrid control or screen-reader event ordering proves unstable. `alpha.94` uses this path for `Ctrl+Shift+C` on lists and in the player while preserving ordinary editing inside text fields. The local log records only command arrival, data-format names and the result of bounded clipboard retries, allowing a shortcut conflict to be distinguished from a clipboard lock without storing copied content.

Undo history for complete removal of a local record also contains the identifiers' positions in Custom order. `alpha.95` restores those positions after reinserting records rather than allowing normalization to treat them as new files. Multiple positions are restored in ascending order to retain the block's internal arrangement. This does not change the rule that genuinely new records are appended.

Collection-membership history also stores a snapshot of item positions. From `alpha.96`, this covers Favorites in every session and local Custom order. Undo restores membership first, then exact positions, and only then refreshes the view. This prevents list normalization after `Delete` from discarding the position and later appending the restored item.

The `alpha.97` implementation defines a playlist as a named, ordered list of stable item identifiers owned by one session. `Ctrl+P` shows playlist containers; Enter opens contents, while Escape or Backspace returns to the playlist list. Starting a track records the whole unfiltered content as playback context. Delete at the top level removes the playlist after confirmation, while inside it removes references only. The `Ctrl+Shift+P` manager supports single and multiple selection, mixed membership, creation, rename, deletion and filtering without exposing technical object representations through UI Automation. Every mutation shares `Ctrl+Z` history, and a full backup carries playlists independently of the database format.

The `alpha.98` implementation stores a separate Queue order per session as stable item identifiers. A newly queued item is appended, missing references are removed during normalization, and order no longer follows the Library catalogue accidentally. Play next remains a priority layer above the regular queue; both groups retain their own relative order, so a mixed selected block cannot be moved. The same model drives the visible list, end-of-track continuation, `Alt+Up/Down`, `Ctrl+Z`, restart and full export; refreshing a local source resynchronizes the session without changing positions.

Correction in `alpha.99`: Play next is not a separate view or a second durable collection, but the priority part of one Queue. `Ctrl+Q` reports both counts and each priority row has a short “Next” prefix. The core maintains a separate temporary navigation context for the active Queue, created both when a user starts an item from `Ctrl+Q` and when playback diverts into Queue after another context ends. Page Up/Page Down therefore cannot accidentally fall back to Library order. An item leaves the waiting set when playback begins but its identifier remains in the temporary context so Page Up can revisit it. Consumed entries are not selected again by automatic continuation. Once both priority and regular parts are empty, AMC either resumes the saved source context or ends a directly started Queue.

The `alpha.100` safeguard preserves the pre-change playback-context snapshot for every operation that removes the current record from a session catalogue. This includes folder monitoring, clipboard cut and move, Delete, and physical Shift+Delete. The core searches only for the next available identifier in the source snapshot, whether the source was Queue, Library, a folder, album, Favorites, playlist, search results, or a future service-adapter view. It also returns correctly from an automatically entered Queue to its earlier view. A successor is selected but is not started automatically. If no successor exists, the session enters an explicit no-current-item state; Space cannot then play the first catalogue file or an item from another view.

Manual reordering reports both direction and the post-move relationship to the immediate neighbour: “Moved up, above [title]” or “Moved down, below [title]”. A contiguous selected block includes its item count and the relation applies to the whole block. This rule is shared by Custom order, Favorites, Queue and editable playlists, including future service adapters that support user ordering.

Terminology correction in `alpha.101`: the technical model still stores synchronization roots as folder sources, but the user interface consistently calls them **Library folders**. `Ctrl+F5` opens **Library folders**, while `F5` refreshes them. A selected folder offers **Remember playback position**, **Always start from the beginning**, and **Follow the global setting**. General Settings and `Alt+Shift+Enter` use the same names for the two explicit decisions; an inherited item or nested-folder option still states that a parent folder may take precedence over the global setting. This presentation-only change does not alter the SQLite schema or stable identifiers and requires no Library migration.

Accessibility correction in `alpha.102`: opening **Library folders** focuses the first selected row container rather than the list control itself. Its accessible label separates root reachability (“folder available” or “folder unavailable — records retained”) from record counts. An active record belongs to the Library and its path is visible; a cloud placeholder remains active without forced payload hydration. An unavailable record was previously known but its path cannot currently be reached. An excluded record was removed from the Library with `Delete` and is deliberately skipped by synchronization even though its physical file may still exist.

Cleanup rule in `alpha.103`: unavailability never deletes a record automatically. For a selected Library folder, a separate extended-selection list exposes unavailable files. **Forget in AMC** requires a warning whose default is No and atomically removes the catalogue record and its identifier from Favorites, Queue, playlists, History, Bookmarks, Custom order, playback context and remembered position. It invokes no file-system operation and does not interrupt current playback. A placeholder visible to the operating system is not eligible. If a forgotten file later reappears, scanning creates a new record rather than restoring removed relationships.

“Go to album” and “Go to artist” are relationship navigation rather than text searches. For a local file, `alpha.89` uses its inferred album directory and parent artist directory. An `F2` title alias does not alter the disk file or album sort key: AMC may display a clean title without `01`, while sequence still follows the physical file-name number. A streaming adapter will later supply stable related album and artist identifiers. Matching a local file to a service catalogue remains a separate, more expensive on-demand feature.

`Backspace` has hierarchical semantics and is independent of removal. `Delete` removes from the current collection, while `Shift+Delete` is a separate confirmed operation on the physical file. `Backspace` moves to the parent: the parent directory, the Albums list from album contents, the source list from the player, or the parent of a future service container. It changes nothing at a top level. Text fields retain normal character deletion. `Alt+Left/Right` visited-view history may follow a different path and remains a separate mechanism.

Local Library priority: a real **Folders** hierarchy will be the primary view because a user's collection may not contain complete tags. A flat Library remains as a parallel view of every imported file. Artist, Album and Genre views may later be derived from metadata but are not a usability prerequisite. Favorites, Queue, History, Playlists and Bookmarks reference the same records regardless of source view.

The local **Albums** view must not depend exclusively on complete tags. A consistent album tag takes precedence, but its absence activates conservative folder inference. A folder containing at least two direct audio files with distinct recognised track numbers such as `01`, `02`, `1 -` or `2.` is an album, and its directory name becomes the album title. In a `source\artist\album\tracks` layout, the immediate parent directory may be presented as the artist. Tracks are ordered by tagged track number first, then a number parsed from the file name, and finally by natural file-name order. A missing title tag leaves the extensionless file name visible; AMC does not rewrite names or tags automatically. Conflicting tags must not merge separate albums, while a loose recording directory without track numbering is not silently classified as one. Single-file albums, multi-disc layouts and unusual directories will later receive an explicit “Treat folder as album” override. Streaming adapters use the service's album resources and never apply the folder heuristic.

Simple audio assembly is a later stage after Bookmarks and Folders. Its first scope is nondestructive A–B markers, selection preview and exporting the selection to a new file. A segment list can then create a new output from several sources. Originals are never overwritten. Lossless cutting and joining will use mature format-specific tools; transcoding must be explicit, and output must be completed through a temporary file and atomic finalization.

An optional NVDA controller inspired by Free Radio will be a thin add-on sending shared commands to the AMC core. It will not duplicate the complete nested Library: it will cover transport, volume, sessions, radio presets and flat Queue, Favorites and History views, while complex Folders and search open the main window on the relevant item. The safe prefix profile remains the default; a direct `Ctrl+Windows` profile is optional and may take over system gestures only after explicit enablement.

Local catalogue and ordering: the Library is neither a playlist nor a mirror of one folder. It is a catalogue of sources with stable identity, path and derived views. Derived orders such as title, artist, album, folder, date added or last played remain deterministic sort modes. Separate Custom order is user metadata and never changes disk-file order. The same keys do not pretend to reorder artist, album or search-result views.

Planned sequence of later stages:

1. Stabilise the main window, lists, filter, queue, focus and approved keyboard map.
2. Run an MSIX/App Installer distribution spike, migrate to .NET 10 LTS and prototype signed component metadata and failure rollback.
3. Extract AMC.Host and a local command–event contract with a demonstration adapter.
4. Use WiiM as the first real test of discovery, commands, volume, inputs and presets.
5. Use Spotify for the first OAuth login, catalogue and Connect playback-transfer test.
6. Add TIDAL as a separate catalogue adapter with an isolated results view and official playback module.
7. Add a thin NVDA add-on using only the host contract.
8. Add internet radio, deliberate direct-stream recording and basic local media.
9. Add YouTube as an official public-search and visible-player adapter with local Favorites, playlists and history; account synchronisation remains an optional later extension.
10. Add BluOS/Bluesound as a richer adapter for devices and player-configured sources.
11. Add Apple Music and the native MusicKit path for macOS.
12. Build a native Swift/AppKit macOS prototype after the contract and Windows behaviour have stabilised.
13. Add Frontier Smart after supported API access; keep Sonos as a later standalone cloud integration.

## 15. Open decisions

1. Whether the default prefix is `Ctrl+Numpad Enter` or bare `Numpad Enter`, and the command-layer timeout.
2. How per-item output and EQ should be presented after the audio-output module is implemented; individual resume and speed overrides work from `alpha.89`.
3. Whether a “Listen Later” playlist exists from the beginning.
4. Which messages use speech and which use earcons.
5. Editing existing bookmark names; named-bookmark creation works since `alpha.70`, while the global view and full-backup export work since `alpha.68`.
6. Default offset for “near the end”; currently 10 seconds.
7. Final application name and package identifiers on each platform.

## 16. Ongoing documentation rule

This document is a design draft rather than a closed specification. Every approved change should be applied to the Polish and English versions in parallel. Code, settings and documentation must use stable command identifiers that do not depend on the display language or selected key bindings.

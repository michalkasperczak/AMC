# Accessible Media Controller — Project Concept

Document version: 0.5, current project plan

Updated: 13 August 2026

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

Filtering only processes data already present in the current list and sends no service request. Current search may query the active service, while global search merges results from all enabled sources. The command palette is an accessible, filterable list that also contains commands with no shortcut.

`Shift+U` is a toggle only when the adapter can reliably determine current state. The program says either “Added to Favorites” or “Removed from Favorites”. If state is unknown, the application must not guess and should present explicit menu actions.

Albums are an important and frequently used view, so they receive a shortcut. This is a deliberate exception to full symmetry: the prefix layer uses plain `A`, while the window uses `Ctrl+Shift+A`, because `Ctrl+A` must retain the standard Select All action. `Ctrl+Alt+A` is avoided because `Ctrl+Alt` can act as AltGr and conflict with typing the Polish character “ą”. `L` remains Library, `B` remains reserved for possible future Bookmarks, and `Shift+A` after the prefix is currently unassigned.

Other previously approved layer commands retain `R` for Radio, `M` for Mixes, `H` for History, `N` for Now Playing, `I` for Information and `O` for Outputs. Their window counterparts should eventually use `Ctrl` plus the same letter when doing so does not break standard text or system behaviour. The keymap editor resolves conflicts, and a command may remain unbound while still being available through the menu and palette.

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

Prefix-layer arrows are reserved for global playback control. In an active list window, ordinary arrow keys navigate items without the prefix.

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
| `Ctrl+Enter` | play the selection now |
| `Shift+Enter` | add the selection to the queue |
| `Ctrl+Shift+Enter` | play next |
| `Backspace` or `Delete` | remove from the current playlist, queue, Favorites or library when the action is unambiguous |
| `Ctrl+Z` | undo the last membership change; inside a text field, undo text editing |
| `Alt+Left Arrow` | previous view |
| `Alt+Right Arrow` | next view when available |
| `Alt+Enter` | item information |
| `Ctrl+Shift+O` | open the item in the official service application |
| `Application key` or `Shift+F10` | context menu |

Enter performs the primary action for the item type: it plays a track, station or preset, while opening the contents of an album, playlist or artist. `Ctrl+Enter` also plays a whole album or playlist immediately. Consistent with file-manager conventions, `Alt+Enter` remains Item Information or Properties; it does not open an external application.

Plain letters in a list never execute AMC commands. They navigate using the current view's explicit key. Single-letter commands work only after the global prefix layer has been successfully activated.

### 7.3. Reloading

- The old list remains visible until new data arrives.
- The program briefly announces “Loading” but does not repeat it for every data page.
- On completion it announces a result such as “24 tracks”.
- After opening an album or playlist it gives a concise summary, for example, “Album: Abbey Road, The Beatles. 17 tracks, 47 minutes 23 seconds”.
- Returning to a previous view restores the previously selected item.
- Refreshing must not unnecessarily return focus to the beginning of the list.

## 8. Playlist selection

`Shift+P` in the prefix layer, or the local Manage Playlists command, opens a small modal window containing:

- a filter field;
- all playlists for the current service, each announced as “contains” or “does not contain”;
- optionally, recently used playlists at the beginning;
- Space to toggle membership in the selected playlist;
- Enter to apply all changes;
- `Ctrl+N` to create a new playlist;
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
| `Ctrl+Shift+C` | copy the item's service link |
| `Ctrl+Shift+U` | add to or remove from Favorites |
| `Ctrl+Shift+P` | open playlist selection and change membership |
| `Ctrl+Shift+Q` | add to the queue |
| `Ctrl+Shift+L` | add to the library |
| `Ctrl+D` | download offline within the service when supported |
| `Ctrl+Shift+D` | download to a local file; experimental and disabled by default |
| `Backspace` or `Delete` | remove from the current playlist, queue, Favorites or library, with confirmation or Undo |
| `Ctrl+Z` | undo the last membership change in Favorites, Library or Queue, or the Play Next state |
| `Ctrl+Shift+O` | open the item in the official service application |
| `F2` | rename a playlist when supported |
| `Ctrl+A` | select all items when the view permits it |

Every local shortcut is configurable. Download commands must not be active until their corresponding module is deliberately enabled.

`Ctrl+1–9` selects a session, `Ctrl+0` opens the session list, and `Ctrl+Page Up` / `Ctrl+Page Down` select the previous or next session.

## 10. Context menu

A context menu is required. It should display only actions available for the selected item type and current service, while preserving a stable and predictable order:

1. Play now.
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

Default messages should be brief. The first version will not provide separate Brief, Normal and Detailed profiles. Instead, the user can edit each message template, disable it, or restore its default.

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
- “Added to Favorites”.
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

### 13.3. Local media and radio

The local playback module will eventually cover files and folders, metadata, Library, queue, common formats, output selection, gapless playback and ReplayGain. Shared audio output is the Windows default so that NVDA and other system sounds are not muted. Exclusive output may later appear as an advanced feature with an explicit warning.

Internet radio is a separate core adapter and uses the same sessions, Favorites, history and transport commands. It should support direct streams, M3U/PLS, station metadata, reconnect and search. Free Radio mechanisms may be reused after code and licence review without moving the full playback engine into the NVDA process.

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

State of `alpha.17`: filtering and searching are separate—`Ctrl+K` narrows the current list, while `Ctrl+F` and `Ctrl+Shift+F` open dedicated query and results windows. A result can be opened or directly played, queued, marked Play Next or Favorite, and inspected. Type-ahead uses the resource's semantic primary name rather than the first field of the accessible label; this is the title in the current track list. The core and UI take one version from `Directory.Build.props`, and the portable publication is produced as one unambiguously named EXE. The main remaining first-stage work is an accessible command palette, low-level capture of a configurable prefix, and candidate-prefix testing with NVDA, JAWS and clipboard managers. Search currently uses the demonstration catalogue; real network queries arrive with service adapters.

Planned sequence of later stages:

1. Stabilise the main window, lists, filter, queue, focus and approved keyboard map.
2. Run an MSIX/App Installer distribution spike, migrate to .NET 10 LTS and prototype signed component metadata and failure rollback.
3. Extract AMC.Host and a local command–event contract with a demonstration adapter.
4. Use WiiM as the first real test of discovery, commands, volume and presets.
5. Add the first OAuth login and catalogue adapter: TIDAL or Spotify.
6. Add a thin NVDA add-on using only the host contract.
7. Add internet radio and basic local media.
8. Add Apple Music and further devices: Sonos, Bluesound/BluOS and Frontier Smart.
9. Build a native Swift/AppKit macOS prototype after the contract and Windows behaviour have stabilised.

## 15. Open decisions

1. Whether the default prefix is `Ctrl+Numpad Enter` or bare `Numpad Enter`, and the command-layer timeout.
2. Whether the application remembers the session after restart.
3. Whether a “Listen Later” playlist exists from the beginning.
4. Which messages use speech and which use earcons.
5. Exact scope of local playback, radio and optional foobar2000 integration.
6. Default offset for “near the end”; currently 10 seconds.
7. Final application name and package identifiers on each platform.

## 16. Ongoing documentation rule

This document is a design draft rather than a closed specification. Every approved change should be applied to the Polish and English versions in parallel. Code, settings and documentation must use stable command identifiers that do not depend on the display language or selected key bindings.

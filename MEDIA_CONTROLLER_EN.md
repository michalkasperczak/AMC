# Accessible Media Controller — Project Concept

Document version: 0.3, discussion draft

Date: 7 August 2026

## 1. Project goal

The project is an accessible application designed to run primarily in the background. Its main interaction model is a configurable keyboard prefix followed by either a session selector or a command executed in the current session.

The first planned integrations are:

- TIDAL;
- Apple Music;
- WiiM.

The first version will not replace foobar2000 or Free Radio. The architecture should remain open to additional modules.

The program will not be an NVDA add-on. The first version will target Windows only and should work with NVDA, JAWS and Narrator. The architecture should not unnecessarily prevent a later macOS edition, but macOS bindings, VoiceOver, Siri and other Apple-platform elements will be designed only after the Windows version has been refined. Direct integration with a particular screen reader may later be offered as an optional extension, but it must not be the foundation of the application.

## 2. Hybrid interaction model

The application combines two methods of interaction:

1. A **prefix command layer** for quick background operations.
2. **Simple accessible windows** for browsing search results, albums, playlists, the library and settings.

The initial product is not intended to have a large interface that remains open at all times. Complex data must nevertheless appear in a normal window because speech messages alone are not suitable for browsing dozens of albums or playlists.

Every application operation will be represented by an internal command that can be invoked from:

- the prefix layer;
- a local shortcut in a window;
- a context menu;
- a future optional plug-in or external control interface.

## 3. Application prefix

### 3.1. Behaviour

1. The user presses the global prefix.
2. The application temporarily enters its command layer.
3. The next key or key combination is interpreted by the application.
4. `Escape` cancels the layer.
5. The layer expires after a configurable timeout, proposed default: 3 seconds.

Pressing the prefix again may announce the current session, for example “TIDAL”. This behaviour should also be configurable.

### 3.2. Choosing the prefix

The prefix remains configurable. Starting with prototype `0.1.0-alpha.5`, the Windows default is `Ctrl+Alt+Windows+F12`. It registered successfully on the test computer and replaced the Enter-based combination that overlapped with the Windows Narrator shortcut. Candidates include:

| Candidate | Advantage | Risk |
| --- | --- | --- |
| `Ctrl+Alt+Windows+F12` | distinctive and did not conflict on the test computer | four keys; requires a registration test |
| `Ctrl+Alt+Space` | relatively short | possible conflict with other applications or input methods |
| `Ctrl+Shift+Windows+Space` | clearly separates the application from Free Radio | long; Windows combinations may be reserved by the operating system |
| `Ctrl+Windows+\` | short and distinctive | Windows modifier combinations require a registration test |
| `Ctrl+Shift+Windows+P` | easy association with “prefix” | four keys |
| `F13–F24` | very low conflict risk | requires a programmable keyboard or a remapped extra key |

Settings should provide a “Test prefix” function. The program saves the binding only after successful registration and warns if it is already in use.

Microsoft states that shortcuts containing the Windows key are reserved for operating-system use, so availability cannot be assumed. The application must test the actual combination on the current computer: <https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey>.

## 4. Sessions

### 4.1. Selecting a session

After the prefix, `Ctrl+digit` combinations directly select services. Service order is configurable. Digits are language-independent and preserve plain letters for commands.

Proposed defaults:

| Command after the prefix | Session or action |
| --- | --- |
| `Ctrl+1` | TIDAL |
| `Ctrl+2` | Apple Music |
| `Ctrl+3` | WiiM |
| `Ctrl+4–9` | additional services or devices |
| `Ctrl+0` | list all sessions |
| `Page Up` | previous available session |
| `Page Down` | next available session |

After a change, the application gives a short message such as “3, WiiM”. If a slot is unused, it says “Session 4 unassigned”. Direct `Ctrl+letter` bindings such as `Ctrl+W` may be configured as optional aliases, but are not required in the default profile.

### 4.2. Session persistence

- The selected session remains active for subsequent commands.
- The service does not need to be specified every time.
- After selecting a session, the command layer may remain active for approximately 2 seconds so that an action can immediately follow.

Examples:

- prefix, `Ctrl+1` — select TIDAL;
- prefix, `S` — search the current session, which is TIDAL;
- prefix, `Ctrl+3`, `Space` — select WiiM and play or pause;
- prefix, `Ctrl+2`, `P` — select Apple Music and open Playlists.

It remains to be decided whether the session persists across application restarts. A safe option is to restore the previous session but announce it on first use of the prefix.

## 5. Letter commands

### 5.1. General rule

- `letter` opens a view, list or category;
- `Shift+letter` performs the associated action on the current or selected item;
- not every letter needs an immediate Shift variant;
- every assignment is editable in Settings.

A Shift command must not perform an irreversible operation without confirmation.

The program may change the interface and message language, but it should not automatically remap shortcuts. The default binding set is shared across languages; for example, `F` remains the Favorites command in the Polish interface. This preserves muscle memory and makes bilingual documentation easier to use. Users may still create custom profiles.

### 5.2. Initial command map

| Key after the prefix | Function | `Shift+key` | Related function |
| --- | --- | --- | --- |
| `F` | Favorites | `Shift+F` | toggle: add to or remove from Favorites |
| `P` | Playlists | `Shift+P` | open playlist selection and change item membership |
| `S` | Search the current session | `Shift+S` | search all supported services, future feature |
| `L` | Library | `Shift+L` | add the item to or remove it from the library |
| `Q` | Queue | `Shift+Q` | add the item to the queue |
| `A` | Albums | `Shift+A` | add the selected album to the library when supported |
| `R` | Track/artist radio or recommendations | `Shift+R` | start radio from the selected item |
| `M` | Mixes and recommendations | `Shift+M` | to be decided; initially unassigned |
| `H` | History | `Shift+H` | initially unassigned |
| `N` | Now Playing | `Shift+N` | open the current item in the official service application |
| `I` | Item information | `Shift+I` | extended information such as performers and credits |
| `O` | Outputs and devices | `Shift+O` | open output selection for the current session |
| `D` | Downloads / offline content | `Shift+D` | download within the service only when officially supported |
| `?` or `F1` | Current-layer help | — | — |

`Shift+F` is a toggle when the service adapter can reliably determine the current state. The program says either “Added to Favorites” or “Removed from Favorites”. If the state is unknown, the application must not guess and should instead present explicit menu actions.

Downloading is not part of the core first version. `D` and related bindings remain design reservations until the capabilities and rules of each service are verified.

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
| `Ctrl+End` | end of the track when supported; otherwise do nothing |
| `Page Up` | previous session |
| `Page Down` | next session |

Commands unsupported by a session must not be silently ignored. The program should say, for example, “Seeking is not available for WiiM”.

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

### 7.2. Navigation

| Key in the window | Action |
| --- | --- |
| `Up/Down Arrow` | previous / next item |
| `Home`, `End` | first / last item |
| `Page Up`, `Page Down` | move by pages |
| typing letters | move to an item beginning with the typed sequence; repeat one letter to cycle through matches |
| `Enter` | open an artist, album or playlist; perform the default action on a track |
| `Ctrl+Enter` | play the selection now |
| `Shift+Enter` | add the selection to the queue |
| `Ctrl+Shift+Enter` | play next |
| `Backspace` or `Delete` | remove from the current playlist, queue, Favorites or library when the action is unambiguous |
| `Alt+Left Arrow` | previous view |
| `Alt+Right Arrow` | next view when available |
| `Alt+Enter` | item information |
| `Ctrl+Shift+O` | open the item in the official service application |
| `Application key` or `Shift+F10` | context menu |

Enter performs the primary action for the item type: it plays a track, station or preset, while opening the contents of an album, playlist or artist. `Ctrl+Enter` also plays a whole album or playlist immediately. Consistent with file-manager conventions, `Alt+Enter` remains Item Information or Properties; it does not open an external application.

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

Initial proposal:

| Local shortcut | Action |
| --- | --- |
| `Ctrl+F` | find or filter the current list |
| `Ctrl+C` | copy the selected item's display name |
| `Ctrl+Shift+C` | copy the item's service link |
| `Ctrl+Shift+F` | add to or remove from Favorites |
| `Ctrl+Shift+P` | open playlist selection and change membership |
| `Ctrl+Shift+Q` | add to the queue |
| `Ctrl+Shift+L` | add to the library |
| `Ctrl+D` | download offline within the service when supported |
| `Ctrl+Shift+D` | download to a local file; experimental and disabled by default |
| `Backspace` or `Delete` | remove from the current playlist, queue, Favorites or library, with confirmation or Undo |
| `Ctrl+Shift+O` | open the item in the official service application |
| `F2` | rename a playlist when supported |
| `Ctrl+A` | select all items when the view permits it |

Every local shortcut is configurable. Download commands must not be active until their corresponding module is deliberately enabled.

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

The program should separate:

- the **command engine** — stable function identifiers;
- the **prefix engine** — sequence capture and timeout;
- the **session manager** — TIDAL, Apple Music and WiiM;
- **service adapters** — translation of common commands into service-specific API operations;
- the **browser window** — a common list component for every service;
- the **accessibility message system**;
- **settings and shortcut profiles**;
- an **update system for the application and service adapters**.

Each service adapter declares its capabilities. The application can therefore know that WiiM supports volume and presets but does not support adding an album to a library.

The final distribution should be self-contained and include its required runtime. The updater runs per user without administrator rights, checks in the background, downloads signed packages only, verifies SHA-256, installs atomically and supports rollback. Updating must not overwrite profiles, configuration or login data, steal focus or interrupt playback. The user selects Stable or Beta and may disable automatic checking, downloading or installation.

## 14. First-version scope

The first prototype should contain:

1. A registered and configurable prefix.
2. A command layer with timeout and cancellation.
3. Three sample sessions, initially even as demonstration modules.
4. Session selection with `Ctrl+1–9`, a session list on `Ctrl+0`, and sequential switching with `Page Up` and `Page Down`.
5. Configurable mapping for a small set of letter commands.
6. Messages through system accessibility plus a visible status area.
7. One common list window with Enter, Back and a context menu.
8. A shortcut editor or at least a configuration file before full Settings is implemented.
9. Brief editable message templates, including separate elapsed, remaining and total time commands.
10. Switchable keyboard profiles with a protected default profile.
11. Three import and export types: keyboard map, configuration and complete backup.
12. An automatic-update interface, initially without a distribution server.

The first prototype and initial working release target Windows only. macOS, VoiceOver and possible Siri support are later stages.

Proposed integration order:

1. WiiM as a straightforward test of commands, volume and presets.
2. TIDAL: authentication, search, albums, Favorites and playlists.
3. Apple Music: a prepared adapter and handoff documentation for the person maintaining an Apple Developer account.

## 15. Open decisions

1. Final default prefix.
2. Command-layer timeout.
3. Whether the application remembers the session after restart.
4. Whether a “Listen Later” playlist exists from the beginning.
5. Which messages use speech and which use earcons.
6. Whether local downloading belongs in the project at all.
7. Application name.

## 16. Ongoing documentation rule

This document is a design draft rather than a closed specification. Every approved change should be applied to the Polish and English versions in parallel. Code, settings and documentation must use stable command identifiers that do not depend on the display language or selected key bindings.

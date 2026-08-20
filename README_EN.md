# Accessible Media Controller — Windows prototype

This is the first demonstration prototype of the global-prefix media controller. It validates the keyboard, session, list, accessibility-message, profile, import and export architecture. It does not yet connect to real TIDAL, Apple Music or WiiM accounts.

This README describes the current prototype. Its single version number is stored in `Directory.Build.props`, so the core, Windows UI and published program always receive the same version. The approved development direction, target architecture and complete keyboard map are recorded in [`MEDIA_CONTROLLER_EN.md`](MEDIA_CONTROLLER_EN.md).

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
- `A` opens Albums while `Shift+A` remains unassigned;
- `K` filters the current list and `Shift+K` opens the command palette;
- `F` searches the current service and `Shift+F` performs a global search;
- `D` downloads within a service and `Shift+D` is the experimental download-to-disk command.

While the AMC window is active, `Ctrl+1–9` selects a session without the global prefix, `Ctrl+0` opens the session list, and `Ctrl+Page Up` or `Ctrl+Page Down` selects the previous or next session. Local view shortcuts are `Ctrl+U` for Favorites, `Ctrl+P` for Playlists, `Ctrl+L` for Library, `Ctrl+Q` for Queue and `Ctrl+Shift+A` for Albums, including while focus is in the filter box. Every arrow key retains native list behaviour in ordinary lists. Enter on a track or station starts it and opens the player; `Ctrl+Enter` toggles the selection without leaving the list. `F6`, the Now Playing command, or prefix then `N` opens the player without changing the selection.

In the player, Left/Right seeks by 10 seconds, Shift+Left/Right by 30 seconds, Ctrl+Left/Right by one minute, Up/Down changes volume by 5%, and Shift+Up/Down by 1%. `Shift+,` slows playback, `Shift+.` speeds it up, and `Ctrl+.` restores 1.00×. Available rates are 0.50–2.00× in 0.25 steps, with tempo changed independently of pitch. Home seeks to the beginning, End to 10 seconds before the end, and digits `0–9` seek to `0–90%` of the duration in 10% steps. `Ctrl+J` opens Jump to time: a number alone means minutes, `minutes:seconds` gives a precise position, and three parts mean `hours:minutes:seconds`. `Ctrl+Shift+J` opens the separate Jump to percentage dialog and accepts `0–100`. Both shortcuts and the digits work only in the player. `Ctrl+Shift+E/R/T` reports time, and Escape returns to the exact previous list and item. A playing list item starts with “Playing”, while one paused in progress starts with “Paused”. `Ctrl+K` focuses a filter that only narrows the already loaded list. `Ctrl+F` opens a window named concisely “Search TIDAL”, or the equivalent for the current service, while `Ctrl+Shift+F` opens “Search all services”. Enter submits the query and another Enter opens the selected result without automatically activating a single match. Search results also accept `Ctrl+Enter` to play or pause the selection, `Shift+Enter` for Queue, `Ctrl+Shift+Enter` for Play Next, `Ctrl+Shift+U` for Favorites and `Alt+Enter` for information. These direct actions keep the results window open and preserve result focus; their announcement always ends with the service name. Pressing plain Enter opens the result and moves focus to the main list. That one item's accessible name temporarily starts with the service name, so NVDA reads the service and item in one uninterrupted focus announcement. The same happens after global Search is closed with Escape following a direct action, even if the selected service was already active. Moving to another item removes the extra prefix. The native list exposes the result and its position without a “Search results” prefix or a separate live result-count message. Escape closes the window. `Ctrl+Shift+K` opens the accessible list of all AMC commands. Typing filters it immediately, Down moves to the results, Enter executes the selected command and Escape closes the palette. Names can be entered without Polish diacritics, and rows expose both the window shortcut and the active prefix-layer shortcut.

`Ctrl+O`, or **File → Open audio files**, opens one or more local files. `Ctrl+Shift+O`, or **File → Open folder with audio files**, loads recognised files from that folder and its subfolders and naturally orders numbered names. Neither command starts playback automatically. Files receive a temporary **Local media** session in the first free slot starting at 4, and loading the same path again does not create a duplicate.

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

The current demonstration catalogue may present combined test results. A real TIDAL adapter will be an isolated module: `Ctrl+Shift+F` may initiate its query, but TIDAL content will not be mixed into one list with content from similar services. AMC opens a separate, attributed TIDAL results view while retaining the shared commands.

The planned YouTube module starts with public search and the official visible player, without account synchronisation. Its local Library contains only items deliberately added to Favorites, AMC-owned playlists and local playback history; it does not copy the whole account or the full YouTube interface. OAuth remains an optional later extension if there is a real need for account subscriptions, playlists or likes. The official adapter will not download, isolate audio from, or record content played from YouTube. Any experimental media-saving tools remain a separate, isolated and independently updated module for sources that permit saving; they are neither part of the core nor a requirement for YouTube. The browsing experience may resemble podcasts, but playback remains in the official YouTube player.

`Ctrl+Z` successively undoes membership changes in Favorites, Library and Queue as well as the Play Next state; a restored item is selected again when it belongs to the current view. Inside the filter box, `Ctrl+Z` retains the standard text-editing Undo behavior. The command is also available from the **Edit** menu. The extra English “Undo” has been traced to the NVDA Global Commands Extension's Clipboard command announcement feature rather than AMC. `Ctrl+N` and `Ctrl+A` remain reserved for the standard New and Select All actions. Typing one or more unmodified letters on the list jumps to the semantic primary name and never runs a command: the track title, artist name, album title, or playlist, station or device name. Matching remains independent of the configured field-reading order. Empty lists contain directional navigation and announce that they are empty instead of moving focus to action buttons or the menu. The Now Playing view contains 17 fixed items for type-ahead tests; the demonstration Library intentionally contains only its two member tracks.

On the **Lists and reading** tab, `Alt+Up/Down` moves the selected field, keeps focus on the selected row, and announces its new relationship and the full order. The current-order preview precedes the movement buttons. **Add to queue**, **Play next** and **Favorites** act as toggles; repeating the command removes the item and the context-menu label reflects its current state. Their default announcements include the affected item name and customized templates are preserved during migration. Before removal, focus is anchored on the list control and restored after WPF layout finishes; it then moves to the nearest item or remains on the empty list. In the main window, `Escape` clears an active filter and returns focus to the media list; without a filter it still returns from the filter box or a main action button to the list. `Alt+F4` closes the active window: from Search it returns to the main window, while from the main window it exits the application. Menus keep their standard hierarchical behavior, with each `Escape` closing one level.

The **Messages** tab includes a **Show detailed keyboard hints at fields and lists** option covering the filter plus current-service and global search. It is off by default. Result help deliberately mentions only arrows, Enter and Escape; the other direct actions remain available through the context menu and documentation without lengthening every result announcement.

Opening Settings always focuses the selected General tab. Left and Right Arrow change category, while Tab enters the controls on the selected page. Both Save and Cancel restore focus to the selected item in the main media list; the saved announcement is raised only after list focus has been restored.

The prefix, timeout and every command are configurable. Users can also choose whether startup opens the media list or the session list. The protected built-in profile is refreshed with the application version; editable user profiles retain their own mappings.

## Import and export

The program recognizes three file types:

- `*.amckeys.json` — one keyboard map;
- `*.amcsettings.json` — application settings without keyboard maps;
- `*.amcbackup.json` — complete backup containing settings, profiles, sessions, bookmarks and message templates.

Passwords, tokens and login data are never exported.

The working configuration is stored in `%AppData%\AccessibleMediaController\state.json`.

## Updates

The project includes a separate update-service interface plus settings for channel, background download and installation on exit. No update server is configured yet.

The final updater should provide a self-contained per-user installation, update the application and service adapters, verify signatures and SHA-256, install atomically with rollback, preserve configuration and credentials, and avoid stealing focus or interrupting playback.

## Current limitations

- TIDAL, Apple Music and WiiM remain demonstration sessions. Local files play real media and persist their catalogue and state in AppData.
- Opening official applications is only a demonstration announcement.
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

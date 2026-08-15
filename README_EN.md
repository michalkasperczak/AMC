# Accessible Media Controller — Windows prototype

This is the first demonstration prototype of the global-prefix media controller. It validates the keyboard, session, list, accessibility-message, profile, import and export architecture. It does not yet connect to real TIDAL, Apple Music or WiiM accounts.

This README describes the current prototype. Its single version number is stored in `Directory.Build.props`, so the core, Windows UI and published program always receive the same version. The approved development direction, target architecture and complete keyboard map are recorded in [`MEDIA_CONTROLLER_EN.md`](MEDIA_CONTROLLER_EN.md).

## Simplest way to run a published build

1. Open the `publish` folder.
2. Select `AccessibleMediaController-<version>.exe` with the highest version number.
3. Press Enter or double-click it.

The published file is self-contained and includes the required .NET runtime. A user testing the ready build does not need to install the SDK or build the project.

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

The output is written to one `publish\AccessibleMediaController-<version>.exe` file.

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
- `K` filters the current list and `Shift+K` is reserved for the command palette;
- `F` searches the current service and `Shift+F` performs a global search;
- `D` downloads within a service and `Shift+D` is the experimental download-to-disk command.

While the AMC window is active, `Ctrl+1–9` selects a session without the global prefix, `Ctrl+0` opens the session list, and `Ctrl+Page Up` or `Ctrl+Page Down` selects the previous or next session. Local view shortcuts are `Ctrl+U` for Favorites, `Ctrl+P` for Playlists, `Ctrl+L` for Library, `Ctrl+Q` for Queue and `Ctrl+Shift+A` for Albums, including while focus is in the filter box. `Ctrl+K` focuses a filter that only narrows the already loaded list. `Ctrl+F` opens a window named concisely “Search TIDAL”, or the equivalent for the current service, while `Ctrl+Shift+F` opens “Search all services”. Enter submits the query and another Enter opens the selected result without automatically activating a single match. Search results also accept `Ctrl+Enter` to play now, `Shift+Enter` for Queue, `Ctrl+Shift+Enter` for Play Next, `Ctrl+Shift+U` for Favorites and `Alt+Enter` for information. These direct actions keep the results window open and preserve result focus; their announcement always ends with the service name. Pressing plain Enter opens the result and moves focus to the main list. That one item's accessible name temporarily starts with the service name, so NVDA reads the service and item in one uninterrupted focus announcement. The same happens after global Search is closed with Escape following a direct action, even if the selected service was already active. Moving to another item removes the extra prefix. The native list exposes the result and its position without a “Search results” prefix or a separate live result-count message. Escape closes the window. `Ctrl+Shift+K` announces that the future command palette is unavailable.

Plain Enter on a track or station starts a newly selected item. Repeating it on the same item alternates Pause and Resume. `Ctrl+Enter` and **Play now** always ensure that the selected item is playing and never act as Pause. A session has one playback pipeline, so a new track replaces the previous one; audio does not overlap. The main-window title starts with the currently playing item, followed by the service and view. Merely opening a search result without starting playback does not change the current-track title. A Search window title describes its scope, for example, “Search TIDAL — AMC”. Homogeneous views do not repeat the resource kind: Albums omits “album” and Playlists omits “playlist”. Library and Favorites retain the kind because they may mix resources. Favorites belong to the current service or local library, while Queue belongs to the active playback session, so their ordinary rows do not repeat the service name.

`Ctrl+Z` successively undoes membership changes in Favorites, Library and Queue as well as the Play Next state; a restored item is selected again when it belongs to the current view. Inside the filter box, `Ctrl+Z` retains the standard text-editing Undo behavior. The command is also available from the **Edit** menu. The extra English “Undo” has been traced to the NVDA Global Commands Extension's Clipboard command announcement feature rather than AMC. `Ctrl+N` and `Ctrl+A` remain reserved for the standard New and Select All actions. Typing one or more unmodified letters on the list jumps to the semantic primary name and never runs a command: the track title, artist name, album title, or playlist, station or device name. Matching remains independent of the configured field-reading order. Empty lists contain directional navigation and announce that they are empty instead of moving focus to action buttons or the menu. The Now Playing view contains 17 fixed items for type-ahead tests; the demonstration Library intentionally contains only its two member tracks.

On the **Lists and reading** tab, `Alt+Up/Down` moves the selected field, keeps focus on the selected row, and announces its new relationship and the full order. The current-order preview precedes the movement buttons. **Add to queue**, **Play next** and **Favorites** act as toggles; repeating the command removes the item and the context-menu label reflects its current state. Their default announcements include the affected item name and customized templates are preserved during migration. Before removal, focus is anchored on the list control and restored after WPF layout finishes; it then moves to the nearest item or remains on the empty list. In the main window, `Escape` clears an active filter and returns focus to the media list; without a filter it still returns from the filter box or a main action button to the list. `Alt+F4` closes the active window: from Search it returns to the main window, while from the main window it exits the application. Menus keep their standard hierarchical behavior, with each `Escape` closing one level.

The **Messages** tab includes a **Show detailed keyboard hints at fields and lists** option covering the filter plus current-service and global search. It is off by default. Result help deliberately mentions only arrows, Enter and Escape; the other direct actions remain available through the context menu and documentation without lengthening every result announcement.

Opening Settings always focuses the selected General tab. Left and Right Arrow change category, while Tab enters the controls on the selected page. Both Save and Cancel restore focus to the selected item in the main media list; the saved announcement is raised only after list focus has been restored.

The prefix, timeout and every command are configurable. Users can also choose whether startup opens the media list or the session list. The protected built-in profile is refreshed with the application version; editable user profiles retain their own mappings.

## Import and export

The program recognizes three file types:

- `*.amckeys.json` — one keyboard map;
- `*.amcsettings.json` — application settings without keyboard maps;
- `*.amcbackup.json` — complete backup containing settings, profiles, sessions and message templates.

Passwords, tokens and login data are never exported.

The working configuration is stored in `%AppData%\AccessibleMediaController\state.json`.

## Updates

The project includes a separate update-service interface plus settings for channel, background download and installation on exit. No update server is configured yet.

The final updater should provide a self-contained per-user installation, update the application and service adapters, verify signatures and SHA-256, install atomically with rollback, preserve configuration and credentials, and avoid stealing focus or interrupting playback.

## Current limitations

- All three services are demonstration sessions.
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

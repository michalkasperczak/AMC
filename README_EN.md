# Accessible Media Controller — Windows prototype

This is the first demonstration prototype of the global-prefix media controller. It validates the keyboard, session, list, accessibility-message, profile, import and export architecture. It does not yet connect to real TIDAL, Apple Music or WiiM accounts.

This README describes the current prototype. Its single version number is stored in `Directory.Build.props`, so the core, Windows UI and published program always receive the same version. The approved development direction, target architecture and complete keyboard map are recorded in [`MEDIA_CONTROLLER_EN.md`](MEDIA_CONTROLLER_EN.md).

## Simplest start — no commands to type

1. Extract the complete archive to a regular folder.
2. In File Explorer, select `ZBUDUJ_I_URUCHOM.cmd` and press Enter or double-click it.
3. Wait for the result message. After a successful build, the application starts automatically.

The file checks for the .NET 8 SDK. If it is missing, it attempts to install the official Microsoft package with Windows Package Manager (`winget`), then restores dependencies, builds the project, runs the checks, and creates one self-contained `publish\AccessibleMediaController-<version>.exe` file. The first run may take several minutes, requires an Internet connection, and may display a Windows installation consent prompt.

If the build fails, the script automatically opens `build-log.txt` in Notepad with the details. No terminal commands need to be typed.

## Build requirements

- Windows 10 or Windows 11;
- .NET 8 SDK — `ZBUDUJ_I_URUCHOM.cmd` can install it automatically;
- PowerShell 5.1 or later;
- NVDA, JAWS or Narrator for accessibility testing.

The project is built on Windows and checked with automated core smoke tests. The simplest route is the File Explorer launcher described above.

## Build and run

The commands below are only for people who prefer a manual build. For regular use, `ZBUDUJ_I_URUCHOM.cmd` is enough.

From PowerShell in the project directory:

```powershell
dotnet build AccessibleMediaController.sln
dotnet run --project src/AccessibleMediaController.Windows
```

Run dependency-free core smoke tests:

```powershell
dotnet run --project tests/AccessibleMediaController.Core.SmokeTests
```

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

While the AMC window is active, `Ctrl+1–9` selects a session without the global prefix, `Ctrl+0` opens the session list, and `Ctrl+Page Up` or `Ctrl+Page Down` selects the previous or next session. Local view shortcuts are `Ctrl+U` for Favorites, `Ctrl+P` for Playlists, `Ctrl+L` for Library, `Ctrl+Q` for Queue and `Ctrl+Shift+A` for Albums, including while focus is in the filter box. `Ctrl+K` focuses a filter that only narrows the already loaded list. `Ctrl+F` opens a separate current-service search window and `Ctrl+Shift+F` opens the all-services search window. Enter submits the query and another Enter opens the selected result without automatically activating a single match. Search results also accept `Ctrl+Enter` to play now, `Shift+Enter` for Queue, `Ctrl+Shift+Enter` for Play Next, `Ctrl+Shift+U` for Favorites and `Alt+Enter` for information. Escape closes the window. `Ctrl+Shift+K` announces that the future command palette is unavailable.

`Ctrl+Z` successively undoes membership changes in Favorites, Library and Queue as well as the Play Next state; a restored item is selected again when it belongs to the current view. Inside the filter box, `Ctrl+Z` retains the standard text-editing Undo behavior. The command is also available from the **Edit** menu. The extra English “Undo” has been traced to the NVDA Global Commands Extension's Clipboard command announcement feature rather than AMC. `Ctrl+N` and `Ctrl+A` remain reserved for the standard New and Select All actions. Typing one or more unmodified letters on the list jumps to a matching item title and never runs a command. Matching remains title-based regardless of the configured field-reading order. Empty lists contain directional navigation and announce that they are empty instead of moving focus to action buttons or the menu. The Now Playing view contains 17 fixed items for type-ahead tests; the demonstration Library intentionally contains only its two member tracks.

On the **Lists and reading** tab, `Alt+Up/Down` moves the selected field, keeps focus on the selected row, and announces its new relationship and the full order. The current-order preview precedes the movement buttons. **Add to queue** and **Play next** act as toggles; repeating the command removes the item and the context-menu label reflects its current state. Their default announcements include the affected item name and customized templates are preserved during migration. Before removal, focus is anchored on the list control and restored after WPF layout finishes; it then moves to the nearest item or remains on the empty list. In the main window, `Escape` clears an active filter and returns focus to the media list; without a filter it still returns from the filter box or a main action button to the list. Menus keep their standard hierarchical behavior, with each `Escape` closing one level.

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
- `MEDIA_CONTROLLER_PL.md` and `MEDIA_CONTROLLER_EN.md` — complete concept specification.

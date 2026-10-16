# Architecture

The generated skin has a Control toolbar, a hidden resident Catalog and one skin per note. Native `Meter=TextEdit` meters edit text directly; no WPF input overlay is used.

Lua writes Unicode drafts as value files. A short-lived C# helper merges changed fields under a per-store mutex. Operations reload the latest store, preserving unrelated edits. Files are replaced atomically where supported; pre-change JSON revisions are kept.

The catalog reads a small generated index and current text/drafts. Eight pooled row sets display groups and notes. The toolbar opens/hides resident Lua directly, avoiding helper startup. Hidden regular updates are disabled. Settings, confirmation and management operations may still launch the helper and incur startup overhead.

String labels persist through the `Text` option with literal character references. `SetText` alone can be erased when other options force a reread. Native TextEdit uses its own direct text API.

## Native positioning

`PitoNoteSnap.dll` subclasses only its registered note windows on Rainmeter's UI thread. It evaluates geometry during a native move loop, after Rainmeter processes `WM_WINDOWPOSCHANGING` and before the position is applied. There is no global hook, polling timer, worker thread or separate snapping process. Built-in `SnapEdges` is disabled for note skins to prevent competing four-edge adjustments.

The geometry uses visible paper rectangles (excluding the 8 px shadow margin, scaled by the skin's actual scale), actual dimensions and the current monitor work area. Only visible notes in the same configuration are targets. Matching top/left edges, adjacency and equal spacing are candidates; there is no matching right/bottom-edge alignment between notes. Per-axis hysteresis stabilizes a captured target. Ctrl/Shift releases it. Programmatic placement and text selection do not initiate snapping.

Resize operations explicitly record and preserve the physical top-left while the live meters resize. Saved anchors are normalized to `(0,0)` without moving the window. Resize growth is capped at the available work area; completing a resize persists dimensions without a skin refresh.

The C# executable embeds the DLL and extracts it as a runtime asset. Rainmeter loads it from the per-user settings `Plugins` directory; the installer deploys it while Rainmeter is closed and backs up any prior copy. Native tests use a separate Rainmeter configuration, private plugin directory and explicitly enabled diagnostic commands. These exercise the native positioning callback without synthesizing user input; an actual mouse-drag feel still needs interactive testing.

| Source | Responsibility |
| --- | --- |
| `Core.cs`, `NoteData.cs` | Validation, independent settings, persistence, skin generation |
| `NativeEditing.cs` | Native meters and draft/autosave Lua |
| `NativeCatalog.cs`, `NativeCatalog.lua`, `ControlList.lua` | Catalog, search, filtering and row pooling |
| `Native.cs` | Rainmeter IPC and resize drag observation |
| `native/PitoNoteSnap.cpp`, `native/SnapGeometry.h` | Native note-window lifecycle and pure snapping geometry |
| `BuildNative.ps1`, `native/SnapGeometryChecks.cpp`, `NativeSnapChecks.cs` | Native compilation and isolated positioning checks |
| `NoteActions.cs`, `DeleteNotes.cs`, `NoteCatalog.cs` | Collapse, duplicate, deletion and management |
| `App.cs`, `SettingsView.cs`, `PopupTheme.cs`, `NoteListView.cs` | Settings and WPF management |
| `LogoAssets.cs`, `Brand.cs` | Code-defined logo, icon and version |
| `*Checks.cs` | Isolated regression/integration tests |

Build embeds the catalog Lua into `PitoNote.exe`; initialization generates runtime assets. Edit `source` and rebuild, not generated installed INI/Lua.

Schema remains version 3. Missing snapping options deserialize to enabled, 10 px distance and 12 px gap, including existing independent note snapshots. English UI does not translate user content or change appearance choices. Windows/per-user fonts are resolved locally; no font is bundled.

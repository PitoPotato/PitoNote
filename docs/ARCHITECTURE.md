# Architecture

The generated skin has a Control toolbar, a hidden resident Catalog and one skin per note. Native `Meter=TextEdit` meters edit text directly; no WPF input overlay is used.

Lua writes Unicode drafts as value files. A short-lived C# helper merges changed fields under a per-store mutex. Operations reload the latest store, preserving unrelated edits. Files are replaced atomically where supported; pre-change JSON revisions are kept.

The catalog reads a small generated index and current text/drafts. Eight pooled row sets display groups and notes. The toolbar opens/hides resident Lua directly, avoiding helper startup. Hidden regular updates are disabled. Settings, confirmation and management operations may still launch the helper and incur startup overhead.

String labels persist through the `Text` option with literal character references. `SetText` alone can be erased when other options force a reread. Native TextEdit uses its own direct text API.

| Source | Responsibility |
| --- | --- |
| `Core.cs`, `NoteData.cs` | Validation, independent settings, persistence, skin generation |
| `NativeEditing.cs` | Native meters and draft/autosave Lua |
| `NativeCatalog.cs`, `NativeCatalog.lua`, `ControlList.lua` | Catalog, search, filtering and row pooling |
| `Native.cs` | Rainmeter IPC and resize drag observation |
| `NoteActions.cs`, `DeleteNotes.cs`, `NoteCatalog.cs` | Collapse, duplicate, deletion and management |
| `App.cs`, `SettingsView.cs`, `PopupTheme.cs`, `NoteListView.cs` | Settings and WPF management |
| `LogoAssets.cs`, `Brand.cs` | Code-defined logo, icon and version |
| `*Checks.cs` | Isolated regression/integration tests |

Build embeds the catalog Lua into `PitoNote.exe`; initialization generates runtime assets. Edit `source` and rebuild, not generated installed INI/Lua.

Schema remains version 3. English UI does not translate user content or change appearance choices. Windows/per-user fonts are resolved locally; no font is bundled.

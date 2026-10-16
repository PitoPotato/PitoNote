<p align="center"><img src="assets/brand/PitoNote.svg" width="80" alt="PitoNote logo"></p>

# PitoNote

Native desktop sticky notes for Rainmeter, with direct editing, rounded cards and independent settings for every note.

Version **3.5.0** · Windows x64 · English interface · Local storage

## Features

- Edit titles and plain text directly on the Rainmeter note. No floating editor overlay.
- Resize each note by dragging its bottom-right corner; move it by dragging an empty area.
- Smart snapping follows visible top/left edges, actual note sizes and even gaps. Resizing keeps the top-left fixed.
- Choose any installed Windows font, including fonts installed for the current user.
- Customize body/title fonts independently: size, weight, italic style and alignment.
- Adjust colors, opacity, padding, corner radius, border and shadow. Seven built-in palettes.
- Hide icons, show them on hover, or keep them visible. Right-click actions remain available.
- Complete, hide, archive, duplicate, collapse or delete a note. Deletion asks for confirmation and keeps a recovery backup.
- Open **All notes**: a resident native Rainmeter panel with search, filters and per-note actions. Opening the list does not start the settings helper.
- Set defaults for future notes without changing existing notes. Each note has its own settings window.
- Automatic Unicode drafts, revision history and JSON import/export. No account, telemetry or cloud service.

This is a Rainmeter skin with a small .NET Framework helper for settings, data operations and resizing, not a standalone notes app. Text is currently plain text: rich text and interactive checklists are not implemented.

Smart snapping runs inside Rainmeter through the bundled `PitoNoteSnap.dll` module, without a separate resident process or global input hooks. The installer deploys this module automatically.

## Requirements

- Windows 10/11, 64-bit, with .NET Framework 4.x and WPF.
- **Rainmeter 5.0.0.5293 RC1 or newer**, including native `Meter=TextEdit`. Rainmeter 4.x is not supported.

The required Rainmeter build is a release candidate. See [Rainmeter 5.0 changes](https://www.rainmeter.net/changes-5.0/) and [TextEdit documentation](https://docs-v5.rainmeter.net/manual/meters/textedit/).

## Install

1. Install the required Rainmeter version and start it once.
2. Extract **PitoNote-3.5.0-windows.zip** from the repository's Releases page.
3. Close PitoNote settings windows. Open PowerShell in the extracted `PitoNote` folder and run:

   ```powershell
   .\Install.ps1
   ```

   If Windows blocks downloaded scripts, inspect them first and unblock only scripts you trust. Do not disable your machine-wide execution policy.

4. Use **+** on the PitoNote toolbar to create a note.

The installer supports clean installs and upgrades. Upgrades keep notes, drafts, settings and Rainmeter positions. It temporarily closes Rainmeter so pending edits can save. Backups are kept under `%LOCALAPPDATA%\PitoNote\InstallBackups`. Other skins are not removed.

If the toolbar is hidden, open **Rainmeter → Manage → PitoNote → Control → Control.ini → Load**. See [installation and recovery](docs/INSTALLATION.md) for details.

## Everyday use

| Where | Action |
| --- | --- |
| Toolbar **+** | Create a note |
| Toolbar list icon | Open/hide All notes |
| Toolbar gear | New-note defaults, management and backup |
| Note title or body | Click to edit directly |
| Note bottom-right corner | Drag to resize |
| Hold Ctrl or Shift while dragging | Temporarily bypass smart snapping |
| Note gear / right-click → Note settings | Customize this note only |
| Note **×** | Hide without deleting |
| Right-click → Delete note… | Confirm deletion; keep a recovery backup |
| Right-click → Collapse note | Keep only the title bar |

Titles support 160 characters; bodies 100,000. Native TextEdit handles selection, copy/paste, undo and scrolling. **Enter** adds a body line or confirms a title. Drafts save approximately every second while editing and again when editing ends. In settings, **Ctrl+S** saves; **Revert** restores saved settings.

General appearance settings apply only to notes created afterward. To change an existing note, open that note's settings.

Under **Desktop → Snap & align**, enable/disable smart snapping, set the capture distance (2–30 px), and adjust the gap (0–80 px) for each note. Distances follow Rainmeter's display scaling. Transparent shadow margins are excluded; hidden notes are not snap targets. Top/left alignment takes priority on near ties, and a slightly wider release distance prevents jitter. Notes do not rearrange automatically when you install or change settings.

## Build and test

Build on Windows x64 with the .NET Framework C# compiler/WPF assemblies and either **Visual Studio C++ Build Tools with a Windows SDK** or **MinGW-w64 g++**. `BuildNative.ps1` detects the installed compiler. No Node.js, NuGet restore or third-party Rainmeter plugin is required. End users of the Windows ZIP do not need a compiler.

```powershell
.\Build.ps1
.\Test.ps1
# Optional native tests in a separate Rainmeter instance:
.\Test.ps1 -IncludeNative
# Clean source snapshot and Windows release ZIP:
.\Release.ps1
```

The helper is generated at `@Resources\PitoNote.exe`, with the native module at `@Resources\Plugins\x64\PitoNoteSnap.dll`. Tests use new private directories, not installed notes. Standard tests include C++ geometry checks and do not need Rainmeter installed; native tests require the version above. Non-English fixtures intentionally verify Unicode support.

GitHub Actions builds, tests and packages on Windows without automatically publishing a release. See [architecture](docs/ARCHITECTURE.md), [contributing](CONTRIBUTING.md) and [publishing](docs/PUBLISHING.md).

## Data and privacy

Installed notes live in `@Resources\data.json`, `Content`, `Drafts` and `Backups`. These may contain sensitive text. **Do not upload your installed skin directory to GitHub.**

`Release.ps1` uses an explicit source-file allowlist and never copies from an installed skin. It excludes real notes, drafts, backups, logs, screenshots and machine-specific Rainmeter configuration. The recipient initializes an empty store on installation. Do not use Git as a note backup system.

The helper is unsigned. Run only builds you trust; you can inspect/build the source yourself. Rainmeter, fonts and .NET Framework are not bundled or installed by this project.

## Credits and license

A substantially reworked derivative of [Notes++ by raziEiL](https://github.com/raziEiL/Notes-pp), originally also credited to Eclectic-Tech and killall-q. See [NOTICE.md](NOTICE.md) for attribution and changes.

Distributed under **[CC BY-NC-SA 4.0](LICENSE)**, preserving the original skin's license. Attribution, noncommercial and share-alike terms apply. This is source-available software with a noncommercial restriction, not an OSI-style permissive license. No affiliation with or endorsement by Rainmeter, Microsoft or the upstream authors is implied.

# Changelog

## 3.5.0

- Replaced four-edge Rainmeter snapping with visible-paper top/left alignment, dimension-aware adjacency and equal-gap placement between notes.
- Added per-note smart snapping, capture distance and spacing options. Ctrl/Shift temporarily bypass snapping; hysteresis reduces jitter.
- Kept the top-left fixed while resizing and limited growth to available monitor space instead of shifting the note.
- Bundled a small native module running only in Rainmeter, without a resident helper, timers or global hooks. The installer backs up/deploys the module.
- Added isolated C++ geometry and native window integration checks, preserving existing notes, settings and positions.

## 3.4.0

- English UI throughout notes, menus, catalog, settings, accessibility labels, errors and confirmations.
- Existing note text, fonts, positions and settings remain unchanged.
- Clean-install/upgrade support with recoverable backups and no fixed drive/user path.
- Clean source/Windows packaging, checksums, privacy audit, Windows CI and English documentation.
- Retained upstream attribution/license. Tests no longer require an optional third-party font.

## 3.3.1

- Fixed blank catalog titles, previews, group labels and counts after meter updates.
- Added literal-text and Unicode catalog rendering-option checks.

## 3.3.0

- Resident native Rainmeter catalog with search, status filters, pooled rows and per-note actions.

## 3.2.0

- PitoNote logo, duplication, collapsed title-bar mode and improved list updates.

## 3.0–3.1

- Native direct editing, independent settings, recoverable deletion and hidden-note management.
- Rebranded modified skin and Rainmeter configurations as PitoNote.

# Installation and recovery

Install Rainmeter 5.0.0.5293 RC1 or newer and start it once. Use Windows x64 with .NET Framework 4.x/WPF. Rainmeter 4.x is unsupported.

Extract the Windows ZIP outside your installed skin. Close PitoNote settings and run `Install.ps1` in PowerShell. Administrator access is not normally needed. Inspect downloaded scripts before unblocking them; do not change the machine-wide execution policy.

The installer reads `SkinPath` from `%APPDATA%\Rainmeter\Rainmeter.ini`, backs up the configuration and validates a staging skin before replacing files. On upgrades it preserves notes/drafts and keeps the prior skin outside Skins. Unrelated skins, including original Notes++, remain in place.

Backups default to `%LOCALAPPDATA%\PitoNote\InstallBackups`. Choose another directory outside the installed skin if needed:

```powershell
.\Install.ps1 -BackupDirectory 'E:\MyBackups\PitoNote'
```

## Manual clean install

Do not overwrite existing data with an empty release. For a clean install only, copy the release's `PitoNote` folder to Rainmeter's Skins directory, run `@Resources\PitoNote.exe --initialize` once, refresh Rainmeter and load `PitoNote\Control\Control.ini` in Manage. Use the script for upgrades. The source archive needs `Build.ps1` first.

## Data

Paths are relative to the installed `PitoNote\@Resources`:

- `data.json`: note store/settings, schema version 3.
- `Content`: Unicode title/body files.
- `Drafts`: text captured before merging into the store.
- `Backups`: revisions; `Backups\Deleted` contains deletion recovery files.
- `Catalog.tsv`: generated index, not the authoritative store.

General settings → Data → Export data creates a JSON export. Import / restore replaces the **entire** collection after confirmation and keeps a previous backup; it does not merge collections.

`data-before-delete.json` snapshots **all** notes before deletion. Importing it restores the whole snapshot, not one note. Export current data first. `deleted-note.json` contains one note and is not a full-store import file.

## Rollback

1. Export current notes if you need changes made after upgrading.
2. Close settings and exit Rainmeter so edits finish saving.
3. Find the install backup printed by the installer. Keep both current and previous files.
4. Move the current `Skins\PitoNote` aside, then copy `Before` to its original config folder name.
5. Restore the backed-up `Rainmeter.ini` only to restore the old layout for **all** skins. Otherwise load the restored toolbar through Manage.
6. Restart Rainmeter. An older store restores notes as they were at that time.

## Limits

The script targets standard installed Rainmeter and its roaming configuration. Portable Rainmeter needs manual setup. Rich text, interactive checkboxes and cloud sync are not implemented. This release is a ZIP with a PowerShell installer, not `.rmskin`; the executable is unsigned.

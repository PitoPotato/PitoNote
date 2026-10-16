# Publishing to GitHub

Use the **clean repository snapshot**, not your installed skin or parent workspace, as the repository root.

## First push

1. Review `LICENSE`/`NOTICE.md`. Keep CC BY-NC-SA 4.0 and attribution; do not choose an incompatible license template.
2. Build, run `Test.ps1 -IncludeNative`, then `Release.ps1`. Inspect the clean snapshot, source/Windows ZIPs, manifest and SHA-256 sums.
3. Optionally add screenshots with invented notes. No private desktop screenshots are included.
4. Create an empty GitHub repository named `PitoNote`. Suggested description: **Native desktop sticky notes for Rainmeter with direct editing and per-note customization.** Topics: `rainmeter`, `sticky-notes`, `windows`, `lua`, `csharp`.

In the clean repository snapshot:

```powershell
git init -b main
git add .
git status --short
git diff --cached --stat
git commit -m "Prepare PitoNote 3.4.0 public release"
```

Add the remote URL copied from your own empty GitHub repository and push `main`. No account-specific URL is assumed, and scripts do not create or push repositories.

## Release

After CI passes, create tag `v3.4.0` and a GitHub Release. Attach `PitoNote-3.4.0-windows.zip`, `PitoNote-3.4.0-source.zip`, `SHA256SUMS.txt` and `MANIFEST.sha256` from the same packaging run. Use `CHANGELOG.md` for release notes. State the Rainmeter RC requirement and unsigned-helper limitation.

Actions uses read-only permissions to build/test/package on Windows. It does not create tags or publish releases. Enable private vulnerability reporting in repository settings if desired.

## Privacy checklist

- No real note text, data stores, drafts, backups or deletion snapshots in the source tree.
- No Rainmeter configuration, usernames/local paths, logs, private screenshots, tokens or keys.
- No generated runtime files committed. The Windows ZIP includes the built helper and generated logo, not personal data; initialization creates an empty store on the recipient's machine.
- Tests use invented content and private output directories; they do not install the skin.
- Re-run packaging after edits. Never add files copied from the installed skin.

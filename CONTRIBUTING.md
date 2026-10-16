# Contributing

Open an issue before a large change. Keep direct note editing native to Rainmeter. General defaults must not silently change existing notes; settings windows must not become content editors.

Build on Windows x64 with .NET Framework/WPF using `Build.ps1`. Run `Test.ps1`; with the supported Rainmeter build installed, also run `Test.ps1 -IncludeNative`. Run `Release.ps1` to verify public packaging.

Use new private test directories. Never test deletion, imports or migration against real notes. Preserve Unicode and treat note text as data, not executable Rainmeter/Lua syntax. Keep UI copy English; non-English test fixtures are intentional.

For UI changes, check settings at 1000×600 and 852×460, long labels/titles, keyboard focus, high contrast and hidden-icon mode. Do not add global management controls to individual notes.

Do not include real exports, private screenshots, usernames/paths, credentials, binaries or generated data in a pull request. Contributions are distributed under the existing CC BY-NC-SA 4.0 license. Preserve upstream attribution.

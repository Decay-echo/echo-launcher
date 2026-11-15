# Echo Launcher

The program players download to play on our Echo VR servers. Design and the JSON shapes it reads: echo-stage
report 29 (Linux host, `~/echo-stage/reports/29-launcher-design.txt`); the owner's decisions: report 31.

What it does, on `Launch Echo on our servers`:

1. points the game at our backend: the six host lines of `_local\config.json` (saved once first as
   `config.original-backup.json`);
2. installs the **mod loader** into `bin\win10\amd_ags_x64.dll` (the game's own file is kept as
   `amd_ags_x64_orig.dll`; see `linux-host/game/amd_ags_proxy/ags_client.cpp`) and the chosen mode's DLLs into
   `<game>\mods\`, downloading only what the manifest's sha256 says has changed, with `.bak` and rollback;
3. writes `<game>\mods\mode.txt` with the mode's DLL names;
4. starts `echovr.exe`.

`Back to official EchoVRCE` restores the config, puts the game's own `amd_ags_x64.dll` back and writes `none`.
No injector, no watcher process, no Startup entry: the old "EchoVRCE Switcher" had all three and its `inject.exe`
is flagged by Defender; the launcher retires an old install on first run (`Migration.cs`).

It reads three documents from `https://get.bootnet.app` (overridable in Settings for tests):
`/status.json` (the status panel), `/mods/manifest.json` (modes and files), `/launcher/latest.json`
(self-update: downloaded and verified, applied when the player clicks `Update ready: restart`).

## Build

C# WinForms on .NET Framework 4.8 (on every Windows 10/11), compiled with the Roslyn compiler from the installed
.NET SDK against the 4.8 reference assemblies. No NuGet, no runtime to ship:

```
.\build.ps1             # -> build\EchoLauncher.exe
.\build.ps1 -Version 1.0.1
```

Unsigned: SmartScreen shows "unknown publisher" on first run until the file earns reputation. Say so in the
download post.

## Release (owner approves each one by name)

1. Build; note the sha256 the build prints. Build the client loader too
   (`cmake --build C:\Users\briar\gb\ags --config Release --target amd_ags_x64_client`) and the client mod DLLs.
2. `gh release create vX.Y.Z` in the launcher repo with `EchoLauncher.exe`, `amd_ags_x64.dll` (the client loader)
   and the mod DLLs as assets.
3. On the Linux host, the publisher writes `/srv/echo-get/launcher/latest.json` and `/srv/echo-get/mods/manifest.json`
   from the release URLs (it fetches each asset and fills sha256/size; it refuses any `dest` outside `bin/win10/`
   and `mods/`).

## Test without a release

`test\make-test-site.ps1` builds a folder with `status.json`, `launcher/latest.json`, `mods/manifest.json` and the
files from the local builds, and serves it on `http://127.0.0.1:8787`; point the launcher's Settings at that URL.

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

## Trust (since 1.0.1)

`latest.json` and `manifest.json` are **signed on the owner's PC** (RSA-3072, SHA-256, PKCS#1 v1.5, over the exact
bytes served; the base64 signature travels next to each file as `<file>.sig`). The public key is compiled into the
exe (`EchoLauncher/SigningKey.cs`, made by `tools/pubkey-to-cs.ps1` from `echo-launcher-signing.pub`); the private
key stays in `%USERPROFILE%\.echo-launcher\signing\` on the owner's PC (plus one offline copy) and never on any
server. The launcher refuses an unsigned or tampered file, a manifest whose version goes backwards, and any URL that
is not `https://get.bootnet.app/` or `https://github.com/Decay-echo/echo-launcher/releases/download/` (a
`http://127.0.0.1` base URL set by hand in Settings is the one exception, for the local test site). Every binary is
SHA-256-checked against the signed manifest before it touches the game folder; a self-update keeps the previous exe
as `EchoLauncher.exe.old`. `latest.json`'s `min_supported` makes an update mandatory.

## Release (owner approves each one by name)

1. `.\build.ps1 -Version X.Y.Z`; build the client loader
   (`cmake --build C:\Users\briar\gb\ags --config Release --target amd_ags_x64_client`) and the mod DLLs; put the
   ten files in `build\release-assets\`.
2. `gh release create vX.Y.Z build\release-assets\* --repo Decay-echo/echo-launcher --title "Echo Launcher X.Y.Z"`.
3. `.\release\publish.ps1 -Tag vX.Y.Z -Upload`: writes `release\vX.Y.Z\{latest,manifest}.json` from the assets
   (sizes checked against the release), signs both (`release\sign.ps1`), and SFTPs the four files to the server's
   inbox as `echopub` (LAN only, key `~\.ssh\echopub`). The gatekeeper there fetches every asset again, checks
   hashes, sizes, shapes and signatures, and promotes to `/launcher/` and `/mods/`.
4. Command-line modes for shortcuts and tests: `EchoLauncher.exe --launch <mode id> [--game <echovr.exe>]
   [--no-start]`, `EchoLauncher.exe --official [--game <echovr.exe>]`; results in
   `%LOCALAPPDATA%\EchoLauncher\last-command.txt`.

## Test without a release

`test\make-test-site.ps1` builds a folder with `status.json`, `launcher/latest.json`, `mods/manifest.json` (both
signed with the owner's key, plus a tampered copy the launcher must refuse) and the files from the local builds, and
serves it on `http://127.0.0.1:8787`; point the launcher's Settings at that URL, or run
`build\EchoLauncher.exe --selftest http://127.0.0.1:8787 <scratch folder>` (36 checks).

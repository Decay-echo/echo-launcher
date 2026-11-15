# Echo Launcher

One small program that gets Echo VR onto our community servers, keeps the game mods current, and launches the
game. Windows 10/11, no install, nothing else to download.

## Get it

1. Download **EchoLauncher.exe** from the latest release on the right.
2. Run it. Windows may show a blue "Windows protected your PC" page the first few times anyone runs a new,
   unsigned program: click **More info**, then **Run anyway**. (The file is open source; this repository is it.)
3. The launcher finds your Echo VR folder. If it does not, click **Find game...** and pick `echovr.exe`
   (inside your Echo folder, under `bin\win10`).
4. Pick a **game mode** and click **Launch Echo on our servers**.
5. First time only: the game shows a four-character code. In our Discord, type `/link-headset` and give it
   that code. Launch again and you are in.

**Back to official EchoVRCE** puts everything back exactly as it was. The launcher never touches anything
outside your Echo folder and its own settings folder (`%LOCALAPPDATA%\EchoLauncher`).

## What it does to your game folder

- `_local\config.json`: points the game at our servers. Your original is saved as
  `config.original-backup.json` first and restored by **Back to official**.
- `bin\win10\amd_ags_x64.dll`: replaced by our **mod loader** (the game's own file is kept as
  `amd_ags_x64_orig.dll` and everything it does is forwarded to it). The loader reads `mods\mode.txt` and loads
  the mode's mod DLLs from `mods\` when the game starts. No injector, no background process.
- `mods\`: the mod DLLs for the modes, verified by SHA-256 against the server's manifest on every launch.

## Modes

**Normal** is stock physics and no powers, but you still see other players' mods. The others (Gravity, Super
Speed, Tiny, Strength, Stretch, Blink, Spider-Man) come from the server's manifest, so new ones appear in the
dropdown without a new launcher. Modded play happens on servers that allow it; the status panel shows which
servers are up and how many are playing.

## Problems

Click **Report a problem**: it writes a zip to your desktop with the launcher's log and the game's mod logs
(no passwords, nothing personal), which you can post in Discord.

## For developers

`launcher/` is the C# WinForms source (.NET Framework 4.8, built with the Roslyn compiler from a .NET SDK, see
`launcher/README.md`). `loader/` is the mod loader (`amd_ags_x64.dll`, C++, MSVC). The server side, the mods'
sources and the game-server mods live in the main project repository.

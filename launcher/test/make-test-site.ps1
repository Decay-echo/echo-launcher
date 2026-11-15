# Builds a local stand-in for get.bootnet.app + the GitHub release from the local builds, in launcher\test\site:
#   status.json (copied live from get.bootnet.app when reachable, else a sample), launcher/latest.json,
#   mods/manifest.json, files/<name> for every DLL and the launcher exe. Then serves it on http://127.0.0.1:8787
# (python -m http.server) unless -NoServe. Point the launcher's Settings at that URL, or run the self-test:
#   .\build\EchoLauncher.exe --selftest http://127.0.0.1:8787 C:\Users\briar\gb\launcher-selftest
param([switch]$NoServe, [string]$LatestVersion = '1.0.0', [int]$Port = 8787)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$site = Join-Path $PSScriptRoot 'site'
foreach ($d in @("$site\files", "$site\launcher", "$site\mods")) { New-Item -ItemType Directory -Force $d | Out-Null }
$base = "http://127.0.0.1:$Port"

$clientLoader = 'C:\Users\briar\gb\ags\client\Release\amd_ags_x64.dll'
$modDir = 'C:\Users\briar\gb\Release'
$exe = Join-Path $root 'build\EchoLauncher.exe'

function Entry([string]$name, [string]$kind, [string]$dest, [string]$src, [string]$version) {
    Copy-Item $src "$site\files\$name" -Force
    $fi = Get-Item "$site\files\$name"
    [ordered]@{ name = $name; kind = $kind; dest = $dest; url = "$base/files/$name"; sha256 = (Get-FileHash $fi.FullName -Algorithm SHA256).Hash.ToLower(); size = $fi.Length; version = $version }
}
$stamp = (Get-Date).ToString('yyyy.MM.dd')
$files = @()
$files += Entry 'amd_ags_x64.dll' 'forwarder' 'bin/win10/amd_ags_x64.dll' $clientLoader $stamp
foreach ($m in 'echo_viewer_client', 'echo_gravity_client', 'echo_speed_client', 'echo_strength_client', 'echo_stretch_client', 'echo_blink_client', 'echo_web_client') {
    if (Test-Path "$modDir\$m.dll") { $files += Entry "$m.dll" 'mod' "mods/$m.dll" "$modDir\$m.dll" $stamp }
}
if (Test-Path "$modDir\echo_tiny_public.dll") { $files += Entry 'echo_tiny_client.dll' 'mod' 'mods/echo_tiny_client.dll' "$modDir\echo_tiny_public.dll" $stamp }
$modes = @(
    [ordered]@{ id = 'normal';   name = 'Normal (stock physics)';    dlls = @('echo_viewer_client.dll');   note = 'Stock physics and no powers, but you still see other players'' mods.' },
    [ordered]@{ id = 'gravity';  name = 'Gravity';                   dlls = @('echo_gravity_client.dll');  note = 'Discs and players fall. Works with the server-side gate.' },
    [ordered]@{ id = 'speed';    name = 'Super Speed';               dlls = @('echo_speed_client.dll');    note = 'x2 thrusters, no speed cap, 4 boosts in a row.' },
    [ordered]@{ id = 'tiny';     name = 'Tiny (experimental)';       dlls = @('echo_tiny_client.dll');     note = 'You are 3x smaller in your own headset.' },
    [ordered]@{ id = 'strength'; name = 'Strength (experimental)';   dlls = @('echo_strength_client.dll'); note = 'Faster throws, longer stuns.' },
    [ordered]@{ id = 'stretch';  name = 'Stretch (experimental)';    dlls = @('echo_stretch_client.dll');  note = 'Hold X / A for 5x arms.' },
    [ordered]@{ id = 'blink';    name = 'Blink (experimental)';      dlls = @('echo_blink_client.dll');    note = 'A = invisible 5 s, X = blink where you look.' },
    [ordered]@{ id = 'web';      name = 'Spider-Man (experimental)'; dlls = @('echo_web_client.dll');      note = 'Hold X / A to web and swing.' }
)
$have = $files | ForEach-Object { $_.name }
$modes = @($modes | Where-Object { $m = $_; @($m.dlls | Where-Object { $have -notcontains $_ }).Count -eq 0 })
$manifest = [ordered]@{ version = 1; generated = (Get-Date).ToUniversalTime().ToString('o'); discord = 'https://discord.gg/REPLACE-ME'; backend = 'echo.bootnet.app:7350'; modsync = 'echo.bootnet.app:7400'; files = $files; modes = $modes }
$manifest | ConvertTo-Json -Depth 6 | Set-Content "$site\mods\manifest.json" -Encoding UTF8

if (Test-Path $exe) {
    Copy-Item $exe "$site\files\EchoLauncher.exe" -Force
    $efi = Get-Item "$site\files\EchoLauncher.exe"
    [ordered]@{ version = $LatestVersion; url = "$base/files/EchoLauncher.exe"; sha256 = (Get-FileHash $efi.FullName -Algorithm SHA256).Hash.ToLower(); size = $efi.Length; notes = 'local test build'; published = (Get-Date).ToString('yyyy-MM-dd'); min_supported = '1.0.0' } |
        ConvertTo-Json | Set-Content "$site\launcher\latest.json" -Encoding UTF8
}
try { Invoke-WebRequest -Uri 'https://get.bootnet.app/status.json' -TimeoutSec 10 -OutFile "$site\status.json" } catch {
    '{"ts":"2026-11-15T00:00:00Z","backend":"up","modsync":"up","motd":"local test","servers":[{"name":"a","up":true,"address":"echo.bootnet.app:6796","port":6796,"players":0,"max":16,"mode":"stock","match":null}]}' | Set-Content "$site\status.json" -Encoding UTF8
}
"site: $site  ($($files.Count) files, $($modes.Count) modes)"
Get-Content "$site\mods\manifest.json" | Select-Object -First 14
if (-not $NoServe) {
    "serving $base (python http.server); stop it with Stop-Process on the python process"
    Start-Process -FilePath 'python' -ArgumentList "-I -m http.server $Port --bind 127.0.0.1 --directory `"$site`"" -WindowStyle Hidden -PassThru | ForEach-Object { "server pid $($_.Id)" }
}

# Builds a local stand-in for get.bootnet.app + the GitHub release from the local builds, in launcher\test\site:
#   status.json (copied live from get.bootnet.app when reachable, else a sample), launcher/latest.json,
#   mods/manifest.json, files/<name> for every DLL and the launcher exe. Then serves it on http://127.0.0.1:8787
# (python -m http.server) unless -NoServe. Point the launcher's Settings at that URL, or run the self-test:
#   .\build\EchoLauncher.exe --selftest http://127.0.0.1:8787 C:\Users\briar\gb\launcher-selftest
param([switch]$NoServe, [string]$LatestVersion = '1.0.0', [int]$Port = 8787,
      [string]$SigningKey = "$env:USERPROFILE\.echo-launcher\signing\echo-launcher-signing.pem")
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$site = Join-Path $PSScriptRoot 'site'
foreach ($d in @("$site\files", "$site\launcher", "$site\mods", "$site\mods-tampered")) { New-Item -ItemType Directory -Force $d | Out-Null }
$base = "http://127.0.0.1:$Port"
. (Join-Path $root 'release\sign.ps1')   # Sign-File

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
$modes = Get-Content (Join-Path $root 'release\modes.json') -Raw | ConvertFrom-Json
$have = $files | ForEach-Object { $_.name }
$modes = @($modes | Where-Object { $m = $_; @($m.dlls | Where-Object { $have -notcontains $_ }).Count -eq 0 })
$manifest = [ordered]@{ version = 1; generated = (Get-Date).ToUniversalTime().ToString('o'); backend = 'echo.bootnet.app:7350'; modsync = 'echo.bootnet.app:7400'; files = $files; modes = $modes }
$manifest | ConvertTo-Json -Depth 6 | Set-Content "$site\mods\manifest.json" -Encoding UTF8

if (Test-Path $exe) {
    Copy-Item $exe "$site\files\EchoLauncher.exe" -Force
    $efi = Get-Item "$site\files\EchoLauncher.exe"
    [ordered]@{ version = $LatestVersion; url = "$base/files/EchoLauncher.exe"; sha256 = (Get-FileHash $efi.FullName -Algorithm SHA256).Hash.ToLower(); size = $efi.Length; notes = 'local test build'; published = (Get-Date).ToString('yyyy-MM-dd'); min_supported = '1.0.0' } |
        ConvertTo-Json | Set-Content "$site\launcher\latest.json" -Encoding UTF8
}
Sign-File "$site\mods\manifest.json" $SigningKey
if (Test-Path "$site\launcher\latest.json") { Sign-File "$site\launcher\latest.json" $SigningKey }
# a tampered copy with the GOOD signature: the launcher must refuse it
Copy-Item "$site\mods\manifest.json.sig" "$site\mods-tampered\manifest.json.sig" -Force
[IO.File]::WriteAllText("$site\mods-tampered\manifest.json", ([IO.File]::ReadAllText("$site\mods\manifest.json") + " "))
try { Invoke-WebRequest -Uri 'https://get.bootnet.app/status.json' -TimeoutSec 10 -OutFile "$site\status.json" } catch {
    '{"ts":"2026-11-15T00:00:00Z","backend":"up","modsync":"up","motd":"local test","servers":[{"name":"a","up":true,"address":"echo.bootnet.app:6796","port":6796,"players":0,"max":16,"mode":"stock","match":null}]}' | Set-Content "$site\status.json" -Encoding UTF8
}
"site: $site  ($($files.Count) files, $($modes.Count) modes)"
Get-Content "$site\mods\manifest.json" | Select-Object -First 14
if (-not $NoServe) {
    "serving $base (python http.server); stop it with Stop-Process on the python process"
    Start-Process -FilePath 'python' -ArgumentList "-I -m http.server $Port --bind 127.0.0.1 --directory `"$site`"" -WindowStyle Hidden -PassThru | ForEach-Object { "server pid $($_.Id)" }
}

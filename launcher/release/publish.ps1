# Publishes a release to the update channel, from the owner's PC:
#   .\publish.ps1 -Tag v1.0.1 [-AssetsDir <folder with the release files>] [-Notes "..."] [-ManifestVersion N] [-Upload]
# 1. Builds latest.json and manifest.json from the files in AssetsDir (sha256/size) and the release's download URLs;
#    the modes come from modes.json ("normal" first). ManifestVersion defaults to the live one + 1.
# 2. Signs both with the owner's private key (sign.ps1 -> <file>.sig); the launcher refuses anything unsigned.
# 3. -Upload: SFTP the four files into the server's inbox as the echopub user (LAN only); the gatekeeper there fetches
#    every asset from GitHub, re-checks hashes, sizes, shapes and the signatures, and promotes on OK.
# The files also land in release\<tag>\ for the record. Run AFTER `gh release create <tag> <assets>` succeeded.
param(
    [Parameter(Mandatory = $true)][string]$Tag,
    [string]$AssetsDir = '',
    [string]$Version = '',
    [string]$Notes = '',
    [string]$MinSupported = '1.0.0',
    [int]$ManifestVersion = 0,
    [switch]$Upload,
    [string]$SigningKey = "$env:USERPROFILE\.echo-launcher\signing\echo-launcher-signing.pem",
    [string]$SftpHost = '192.168.1.150',
    [string]$SftpUser = 'echopub',
    [string]$SftpKey = "$env:USERPROFILE\.ssh\echopub",
    [string]$Repo = 'Decay-echo/echo-launcher'
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'sign.ps1')
if (-not $Version) { $Version = $Tag.TrimStart('v') }
if (-not $AssetsDir) { $AssetsDir = Join-Path $PSScriptRoot "..\build\release-assets" }
if (-not (Test-Path $AssetsDir)) { throw "assets folder not found: $AssetsDir" }
$base = "https://github.com/$Repo/releases/download/$Tag/"
$out = Join-Path $PSScriptRoot $Tag
New-Item -ItemType Directory -Force $out | Out-Null

# the release must exist and carry exactly these files
$release = gh release view $Tag --repo $Repo --json assets 2>$null | ConvertFrom-Json
if (-not $release) { throw "release $Tag not found on $Repo; create it first (gh release create $Tag <files>)" }
$remote = @{}; foreach ($a in $release.assets) { $remote[$a.name] = $a.size }

$modes = Get-Content (Join-Path $PSScriptRoot 'modes.json') -Raw | ConvertFrom-Json
$files = @()
foreach ($f in Get-ChildItem $AssetsDir -File | Where-Object { $_.Extension -eq '.dll' } | Sort-Object Name) {
    if (-not $remote.ContainsKey($f.Name)) { throw "$($f.Name) is not an asset of release $Tag" }
    if ($remote[$f.Name] -ne $f.Length) { throw "$($f.Name): local size $($f.Length) differs from the release asset ($($remote[$f.Name]))" }
    $kind = if ($f.Name -eq 'amd_ags_x64.dll') { 'forwarder' } else { 'mod' }
    $dest = if ($kind -eq 'forwarder') { 'bin/win10/amd_ags_x64.dll' } else { "mods/$($f.Name)" }
    $files += [ordered]@{ name = $f.Name; kind = $kind; dest = $dest; url = "$base$($f.Name)"; sha256 = (Get-FileHash $f.FullName -Algorithm SHA256).Hash.ToLower(); size = $f.Length; version = $Version }
}
if (-not ($files | Where-Object { $_.kind -eq 'forwarder' })) { throw 'no amd_ags_x64.dll (the mod loader) among the assets' }
$have = $files | ForEach-Object { $_.name }
$modes = @($modes | Where-Object { $m = $_; @($m.dlls | Where-Object { $have -notcontains $_ }).Count -eq 0 })
if ($modes[0].id -ne 'normal') { throw 'modes.json must keep "normal" first and its DLL must be among the assets' }

if ($ManifestVersion -le 0) {
    try { $live = Invoke-RestMethod -Uri 'https://get.bootnet.app/mods/manifest.json' -TimeoutSec 15; $ManifestVersion = [int]$live.version + 1 }
    catch { throw 'could not read the live manifest to pick the next version; pass -ManifestVersion' }
}
$manifest = [ordered]@{ version = $ManifestVersion; generated = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ'); backend = 'echo.bootnet.app:7350'; modsync = 'echo.bootnet.app:7400'; files = $files; modes = $modes }
$manifestPath = Join-Path $out 'manifest.json'
[IO.File]::WriteAllText($manifestPath, ($manifest | ConvertTo-Json -Depth 6), (New-Object Text.UTF8Encoding $false))

$exe = Join-Path $AssetsDir 'EchoLauncher.exe'
if (-not (Test-Path $exe)) { throw "EchoLauncher.exe not in $AssetsDir" }
if ($remote['EchoLauncher.exe'] -ne (Get-Item $exe).Length) { throw 'EchoLauncher.exe: local size differs from the release asset' }
$latest = [ordered]@{ version = $Version; url = "${base}EchoLauncher.exe"; sha256 = (Get-FileHash $exe -Algorithm SHA256).Hash.ToLower(); size = (Get-Item $exe).Length; notes = $Notes; published = (Get-Date).ToString('yyyy-MM-dd'); min_supported = $MinSupported }
$latestPath = Join-Path $out 'latest.json'
[IO.File]::WriteAllText($latestPath, ($latest | ConvertTo-Json), (New-Object Text.UTF8Encoding $false))

Sign-File $manifestPath $SigningKey
Sign-File $latestPath $SigningKey
Get-ChildItem $AssetsDir -File | ForEach-Object { "$((Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLower())  $($_.Name)" } | Set-Content (Join-Path $out 'SHA256SUMS') -Encoding ascii
"wrote $out : manifest.json (v$ManifestVersion, $($files.Count) files, $($modes.Count) modes) + latest.json ($Version), both signed"

if ($Upload) {
    $batch = Join-Path $out 'sftp-batch.txt'
    @("cd echo-get/inbox", "put `"$latestPath`"", "put `"$latestPath.sig`"", "put `"$manifestPath`"", "put `"$manifestPath.sig`"", "bye") | Set-Content $batch -Encoding ascii
    & sftp -o BatchMode=yes -i $SftpKey -b $batch "$SftpUser@$SftpHost"
    if ($LASTEXITCODE -ne 0) { throw "sftp upload failed ($LASTEXITCODE)" }
    "uploaded to $SftpUser@$SftpHost (inbox); the gatekeeper promotes it within ~10 s if everything verifies"
}

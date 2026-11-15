# Builds EchoLauncher.exe with nothing installed beyond what this PC already has: the Roslyn C# compiler that
# ships inside the .NET SDK, against the .NET Framework 4.8 reference assemblies. The result runs on any Windows
# 10/11 (4.8 is built in), is one file of a few hundred KB, and needs no runtime download.
#   .\build.ps1            -> launcher\build\EchoLauncher.exe
#   .\build.ps1 -Version 1.0.1
param([string]$Version = '')
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$src = Join-Path $root 'EchoLauncher'
$out = Join-Path $root 'build'
New-Item -ItemType Directory -Force $out | Out-Null

$csc = Get-ChildItem 'C:\Program Files\dotnet\sdk\*\Roslyn\bincore\csc.dll' | Sort-Object FullName -Descending | Select-Object -First 1
if (-not $csc) { throw 'csc.dll not found: install the .NET SDK (any version) or build with C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe (C# 5 only)' }
$refDir = 'C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8'
if (-not (Test-Path $refDir)) { $refDir = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319' }
$refs = @('mscorlib', 'System', 'System.Core', 'System.Windows.Forms', 'System.Drawing', 'System.Net.Http',
          'System.IO.Compression', 'System.IO.Compression.FileSystem', 'System.Web.Extensions', 'System.Management',
          'System.Xml', 'System.Runtime.Serialization') | ForEach-Object { "-r:$refDir\$_.dll" }
$facades = Join-Path $refDir 'Facades'
if (Test-Path $facades) { $refs += "-r:$facades\System.Runtime.dll"; $refs += "-r:$facades\System.Threading.Tasks.dll" }

if ($Version) {
    $prog = Join-Path $src 'Program.cs'
    (Get-Content $prog -Raw) -replace 'public const string Version = "[^"]+";', "public const string Version = `"$Version`";" | Set-Content $prog -NoNewline -Encoding UTF8
}
$ver = (Select-String -Path (Join-Path $src 'Program.cs') -Pattern 'Version = "([^"]+)"').Matches[0].Groups[1].Value
$asmInfo = Join-Path $out 'AssemblyInfo.cs'
@"
using System.Reflection;
[assembly: AssemblyTitle("Echo Launcher")]
[assembly: AssemblyProduct("Echo Launcher")]
[assembly: AssemblyDescription("Points Echo VR at the community servers, keeps the mods current, launches the game.")]
[assembly: AssemblyVersion("$ver.0")]
[assembly: AssemblyFileVersion("$ver.0")]
[assembly: AssemblyInformationalVersion("$ver")]
"@ | Set-Content $asmInfo -Encoding UTF8

$exe = Join-Path $out 'EchoLauncher.exe'
$sources = @(Get-ChildItem (Join-Path $src '*.cs') | ForEach-Object { $_.FullName }) + $asmInfo
$args = @('-nologo', '-noconfig', '-nostdlib+', '-target:winexe', '-platform:anycpu', '-optimize+', '-deterministic',
          '-langversion:7.3', '-warn:4', '-nowarn:1591', "-win32manifest:$(Join-Path $root 'app.manifest')",
          "-out:$exe") + $refs + $sources
& dotnet $csc.FullName @args
if ($LASTEXITCODE -ne 0) { throw "csc failed with exit code $LASTEXITCODE" }
$fi = Get-Item $exe
"built $($fi.FullName): $($fi.Length) bytes, version $ver, sha256 $((Get-FileHash $exe -Algorithm SHA256).Hash.ToLower())"

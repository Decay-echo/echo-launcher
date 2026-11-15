# Turns the signing PUBLIC key (PEM, from `openssl pkey -pubout`) into EchoLauncher/SigningKey.cs, the key the
# launcher compiles in (RSAKeyValue XML, which RSACryptoServiceProvider.FromXmlString reads on .NET Framework 4.8).
#   .\pubkey-to-cs.ps1 [-Pub <echo-launcher-signing.pub>] [-Out <SigningKey.cs>]
param(
    [string]$Pub = "$env:USERPROFILE\.echo-launcher\signing\echo-launcher-signing.pub",
    [string]$Out = (Join-Path $PSScriptRoot '..\EchoLauncher\SigningKey.cs')
)
$ErrorActionPreference = 'Stop'
$openssl = 'C:\Program Files\Git\usr\bin\openssl.exe'
if (-not (Test-Path $openssl)) { $openssl = (Get-Command openssl -ErrorAction Stop).Source }
$mod = (& $openssl rsa -pubin -in $Pub -modulus -noout 2>$null) -replace '^Modulus=', ''
if (-not $mod -or $mod.Length -ne 768) { throw "unexpected modulus (expected 3072 bits) from $Pub" }
$bytes = [byte[]]::new($mod.Length / 2)
for ($i = 0; $i -lt $bytes.Length; $i++) { $bytes[$i] = [Convert]::ToByte($mod.Substring(2 * $i, 2), 16) }
$xml = "<RSAKeyValue><Modulus>$([Convert]::ToBase64String($bytes))</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>"
$sha = (Get-FileHash $Pub -Algorithm SHA256).Hash.ToLower()
@"
// Echo Launcher -- the update channel's signing PUBLIC key (RSA-3072), generated on the owner's PC and converted by
// launcher/tools/pubkey-to-cs.ps1 from echo-launcher-signing.pub. The private half never leaves the owner's PC.
// sha256 of echo-launcher-signing.pub: $sha
namespace EchoLauncher
{
    public static partial class Signing
    {
        public const string PublicKeyXml = "$xml";
    }
}
"@ | Set-Content $Out -Encoding UTF8
"wrote $Out (public key sha256 $sha)"

# Sign-File: detached RSA-3072 / SHA-256 / PKCS#1 v1.5 signature over the exact bytes of a file, written next to it
# as <file>.sig (base64 of the raw 384-byte signature). This is what EchoLauncher.exe verifies (Signing.cs) and what
# the server's gatekeeper checks before serving. The private key lives on the owner's PC only.
function Sign-File([string]$Path, [string]$KeyPem) {
    if (-not (Test-Path $KeyPem)) { throw "signing key not found: $KeyPem" }
    $openssl = 'C:\Program Files\Git\usr\bin\openssl.exe'
    if (-not (Test-Path $openssl)) { $openssl = (Get-Command openssl -ErrorAction Stop).Source }
    $raw = "$Path.sig.raw"
    & $openssl dgst -sha256 -sign $KeyPem -out $raw $Path
    if ($LASTEXITCODE -ne 0) { throw "openssl failed signing $Path" }
    $bytes = [IO.File]::ReadAllBytes($raw)
    if ($bytes.Length -ne 384) { Remove-Item $raw -Force; throw "unexpected signature length $($bytes.Length) for $Path" }
    # verify with the public half before trusting it, so a wrong key is caught here and not by every player
    $pub = [IO.Path]::ChangeExtension($KeyPem, '.pub')
    if (Test-Path $pub) {
        $v = & $openssl dgst -sha256 -verify $pub -signature $raw $Path 2>&1
        if ("$v" -notmatch 'Verified OK') { Remove-Item $raw -Force; throw "self-verification failed for $Path : $v" }
    }
    Remove-Item $raw -Force
    [IO.File]::WriteAllText("$Path.sig", [Convert]::ToBase64String($bytes))
    "signed $(Split-Path $Path -Leaf) (384-byte signature, verified)"
}

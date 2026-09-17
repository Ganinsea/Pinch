$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSCommandPath
$icon = Join-Path $root 'assets\pinch-transparent.ico'
$backup = Join-Path $root 'assets\pinch-transparent.ico.b64'
if (-not (Test-Path -LiteralPath $icon)) {
    $encoded = [IO.File]::ReadAllText($backup, [Text.Encoding]::ASCII)
    [IO.File]::WriteAllBytes($icon, [Convert]::FromBase64String($encoded))
}
$expected = [Convert]::FromBase64String([IO.File]::ReadAllText($backup, [Text.Encoding]::ASCII))
$sha = [Security.Cryptography.SHA256]::Create()
try { $hash = [BitConverter]::ToString($sha.ComputeHash($expected)).Replace('-','') } finally { $sha.Dispose() }
if ((Get-FileHash -LiteralPath $icon -Algorithm SHA256).Hash -ne $hash) { throw 'Icon differs from the tracked backup. Update both assets intentionally.' }

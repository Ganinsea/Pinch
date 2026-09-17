$ErrorActionPreference = 'Stop'
$exe = Join-Path $env:LOCALAPPDATA 'Pinch\PinchUninstall.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw 'The Pinch uninstaller is not installed.' }
$process = Start-Process -FilePath $exe -PassThru -Wait
if ($process.ExitCode -ne 0) { throw "Uninstall could not start: $($process.ExitCode)" }

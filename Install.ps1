param([switch]$Quiet)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSCommandPath
& (Join-Path $root 'Build-Installer.ps1')
$setup = Join-Path $root 'dist\PinchSetup.exe'
if ($Quiet) { $process = Start-Process -FilePath $setup -ArgumentList '--silent' -WindowStyle Hidden -PassThru -Wait }
else { $process = Start-Process -FilePath $setup -PassThru -Wait }
if ($process.ExitCode -ne 0) { throw "Installation failed: $($process.ExitCode)" }

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSCommandPath
& (Join-Path $root 'Build-Installer.ps1')
. (Join-Path $root 'scripts\Build-Common.ps1')
$sources = @('AppInfo.cs','ImageCompressor.cs','RuntimeState.cs','FloatForm.cs','InstallerCore.cs','PinchSettings.cs','SettingsForm.cs') | ForEach-Object { Join-Path $root "src\$_" }
$exe = Join-Path $root 'tests\bin\Pinch.Tests.exe'
Invoke-PinchCompile -Output $exe -Kind exe -Main PinchTests -Sources ($sources + (Join-Path $root 'tests\Pinch.Tests.cs') + (Join-Path $root 'tests\Settings.Tests.cs'))
$output = Join-Path $root 'tests\output'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$process = Start-Process -FilePath $exe -ArgumentList ('"' + $root + '"') -WindowStyle Hidden -RedirectStandardOutput (Join-Path $output 'results.txt') -RedirectStandardError (Join-Path $output 'errors.txt') -PassThru -Wait
Get-Content -LiteralPath (Join-Path $output 'results.txt') -Encoding UTF8
if ($process.ExitCode -ne 0) { Get-Content -LiteralPath (Join-Path $output 'errors.txt') -Encoding UTF8; throw "Tests failed: $($process.ExitCode)" }

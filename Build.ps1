$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSCommandPath
. (Join-Path $root 'scripts\Build-Common.ps1')
& (Join-Path $root 'Restore-Assets.ps1')
$sources = @('AppInfo.cs','ImageCompressorFloat.cs','ImageCompressor.cs','RuntimeState.cs','FloatForm.cs','PinchSettings.cs','SettingsForm.cs') | ForEach-Object { Join-Path $root "src\$_" }
Invoke-PinchCompile -Output (Join-Path $root 'bin\Pinch.exe') -Sources $sources

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSCommandPath
& (Join-Path $root 'Build.ps1')
. (Join-Path $root 'scripts\Build-Common.ps1')
$common = @('AppInfo.cs','RuntimeState.cs','InstallerCore.cs') | ForEach-Object { Join-Path $root "src\$_" }
$uninstaller = Join-Path $root 'bin\PinchUninstall.exe'
Invoke-PinchCompile -Output $uninstaller -Sources ($common + (Join-Path $root 'src\PinchUninstall.cs'))
$payload = Join-Path $root 'bin\Pinch.exe'
$guide = Join-Path $root 'Pinch-User-Guide.md'
$setup = Join-Path $root 'dist\PinchSetup.exe'
Invoke-PinchCompile -Output $setup -Sources ($common + (Join-Path $root 'src\PinchSetup.cs')) -Resources @("$payload,Pinch.exe", "$uninstaller,PinchUninstall.exe", "$guide,PinchGuide.md")
$version = (Get-Item -LiteralPath $payload).VersionInfo.ProductVersion
$release = [ordered]@{version=$version;builtAt=(Get-Date).ToString('o');files=@()}
foreach ($path in @($payload,$uninstaller,$setup)) {
    $release.files += [ordered]@{name=[IO.Path]::GetFileName($path);bytes=(Get-Item -LiteralPath $path).Length;sha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash}
}
$release | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $root 'dist\release.json') -Encoding UTF8
Copy-Item -LiteralPath (Join-Path $root 'Pinch-User-Guide.md') -Destination (Join-Path $root 'dist\Pinch-User-Guide.txt') -Force

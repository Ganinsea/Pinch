param([Parameter(Mandatory=$true)][string]$Destination)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$destinationFull=[IO.Path]::GetFullPath($Destination)
if(Test-Path -LiteralPath $destinationFull){throw 'Public export destination must be new.'}
New-Item -ItemType Directory -Path $destinationFull | Out-Null
$rootFiles=@('.gitignore','Build.ps1','Build.bat','Build-Installer.ps1','Build-Installer.bat','Install.ps1','Install.bat','Uninstall.ps1','Uninstall.bat','Restore-Assets.ps1','Run.bat','Test.ps1','Pinch-User-Guide.md')
foreach($name in $rootFiles){Copy-Item -LiteralPath (Join-Path $root $name) -Destination (Join-Path $destinationFull $name)}
foreach($name in @('src','assets')){Copy-Item -LiteralPath (Join-Path $root $name) -Destination (Join-Path $destinationFull $name) -Recurse}
foreach($name in @('tests/Pinch.Tests.cs','tests/Settings.Tests.cs','tests/Test-BuildFailure.ps1','scripts/Build-Common.ps1','scripts/Export-GitHub.ps1')){
 $dest=Join-Path $destinationFull $name
 New-Item -ItemType Directory -Path (Split-Path -Parent $dest) -Force | Out-Null
 Copy-Item -LiteralPath (Join-Path $root $name) -Destination $dest
}
Copy-Item -LiteralPath (Join-Path $root 'tests/fixtures') -Destination (Join-Path $destinationFull 'tests/fixtures') -Recurse
New-Item -ItemType Directory -Path (Join-Path $destinationFull 'docs') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $root 'docs/github') -Destination (Join-Path $destinationFull 'docs/github') -Recurse
foreach($name in @('README.md','HANDOFF.md','AGENTS.md')){Copy-Item -LiteralPath (Join-Path $root "docs/github/$name") -Destination (Join-Path $destinationFull $name)}
Copy-Item -LiteralPath (Join-Path $root 'docs/github/upgrade-2.3.0.md') -Destination (Join-Path $destinationFull 'docs/upgrade-2.3.0.md')
$release=Get-Content -LiteralPath (Join-Path $root 'dist/release.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$releaseDir=Join-Path $destinationFull ('releases/v'+$release.version)
New-Item -ItemType Directory -Path $releaseDir -Force | Out-Null
foreach($item in $release.files){
 $folder=if($item.name -eq 'PinchSetup.exe'){'dist'}else{'bin'}
 $source=Join-Path (Join-Path $root $folder) $item.name
 if((Get-FileHash -LiteralPath $source).Hash -ne $item.sha256){throw "Release hash mismatch: $($item.name)"}
 Copy-Item -LiteralPath $source -Destination (Join-Path $releaseDir $item.name)
}
Copy-Item -LiteralPath (Join-Path $root 'dist/release.json') -Destination (Join-Path $releaseDir 'release.json')
Copy-Item -LiteralPath (Join-Path $root 'Pinch-User-Guide.md') -Destination (Join-Path $releaseDir 'Pinch-User-Guide.txt')
Add-Content -LiteralPath (Join-Path $destinationFull '.gitignore') -Value "`r`n/state/`r`n/.shuttle/`r`n.env`r`n.env.*`r`n*.pem`r`n*.key" -Encoding UTF8
$files=@(Get-ChildItem -LiteralPath $destinationFull -File -Recurse -Force)
[pscustomobject]@{destination=$destinationFull;files=$files.Count;bytes=($files|Measure-Object Length -Sum).Sum;version=$release.version} | ConvertTo-Json

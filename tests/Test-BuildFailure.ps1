$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $PSScriptRoot
$probe = Join-Path $PSScriptRoot ('output\build-failure-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $probe | Out-Null
foreach($name in @('src','scripts','assets','Build.ps1','Build-Installer.ps1','Restore-Assets.ps1','Pinch-User-Guide.md')) {
    Copy-Item -LiteralPath (Join-Path $project $name) -Destination $probe -Recurse
}
# Fault injection is confined to a new disposable source copy.
$source = Join-Path $probe 'src\ImageCompressorFloat.cs'
[IO.File]::WriteAllText($source,"#error EXPECTED_TEST_FAILURE`r`n" + [IO.File]::ReadAllText($source),[Text.Encoding]::UTF8)
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $probe 'Build-Installer.ps1')
$code = $LASTEXITCODE
if($code -eq 0 -or (Test-Path -LiteralPath (Join-Path $probe 'dist\PinchSetup.exe'))) { throw 'Invalid source was published.' }
$result = [ordered]@{test='compile-failure-stops-installer';exitCode=$code;installerCreated=$false;passed=$true;probe=$probe}
$result | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $project 'tests\output\build-failure-result.json') -Encoding UTF8
Write-Host 'PASS compilation failure stops installer publication'

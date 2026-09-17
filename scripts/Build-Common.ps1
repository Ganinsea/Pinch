$ErrorActionPreference = 'Stop'
function Invoke-PinchCompile {
    param([string]$Output, [string[]]$Sources, [string[]]$Resources = @(), [string]$Main = '', [string]$Kind = 'winexe')
    $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    if (-not (Test-Path -LiteralPath $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
    if (-not (Test-Path -LiteralPath $csc)) { throw 'The .NET Framework compiler is missing.' }
    $parent = Split-Path -Parent $Output
    New-Item -ItemType Directory -Path $parent -Force | Out-Null
    $staging = Join-Path $parent ('.build-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $staging | Out-Null
    $temporary = Join-Path $staging ([IO.Path]::GetFileName($Output))
    try {
        $arguments = @('/nologo', "/target:$Kind", '/optimize+', '/platform:anycpu', "/out:$temporary",
            "/win32icon:$(Join-Path $root 'assets\pinch-transparent.ico')", "/win32manifest:$(Join-Path $root 'src\app.manifest')",
            '/reference:System.dll', '/reference:System.Core.dll', '/reference:System.Drawing.dll',
            '/reference:System.Windows.Forms.dll', '/reference:System.Xml.dll', '/reference:System.Xml.Linq.dll')
        if ($Main) { $arguments += "/main:$Main" }
        foreach ($resource in $Resources) { $arguments += "/resource:$resource" }
        & $csc @arguments @Sources
        if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $temporary)) { throw "Compilation failed: $Output (exit $LASTEXITCODE)" }
        if (Test-Path -LiteralPath $Output) { [IO.File]::Replace($temporary, $Output, (Join-Path $staging 'previous.exe')) }
        else { [IO.File]::Move($temporary, $Output) }
        Write-Host "Built: $Output"
    }
    finally {
        $resolved = [IO.Path]::GetFullPath($staging)
        if ($resolved.StartsWith([IO.Path]::GetFullPath($parent).TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase) -and (Split-Path $resolved -Leaf) -like '.build-*') {
            Remove-Item -LiteralPath $resolved -Recurse -Force
        }
    }
}

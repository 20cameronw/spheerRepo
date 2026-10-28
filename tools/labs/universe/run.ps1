<#
.SYNOPSIS
  Runs the universe console on the .NET 8 runtime of the Unity editor (builds first when a source changed).
.DESCRIPTION
  Commands (details in tools\labs\README.md):
    test [--filter text] [--list]        the Core and universe NUnit tests (the same sources as the plain .NET CI project)
    render [dir]                         density map 30,000 ly, home galaxy zoom, systems 2,000 ly, tier rings (4 PNGs; default out\universe\png)
    gallery [file]                       contact sheet of galaxy kinds (default out\universe\png\galaxy_gallery.png)
    density cx cy size px file           one density PNG: centre, width in ly, pixels, output file
    inspect [region|biomes|planned]      statistics of the generator around the origin
  Relative paths in the arguments are relative to the directory you call this from; the defaults go to tools\labs\out\universe\png.
.EXAMPLE
  powershell -NoProfile -ExecutionPolicy Bypass -File tools\labs\universe\run.ps1 render
  powershell -NoProfile -ExecutionPolicy Bypass -File tools\labs\universe\run.ps1 test --filter Determinism
#>
. (Join-Path $PSScriptRoot "..\lab-common.ps1")
$ctx = Get-LabContext $PSScriptRoot
& (Join-Path $PSScriptRoot "build.ps1")

$caller = (Get-Location).Path
$work = Join-Path $ctx.Out "universe"
$png = Join-Path $work "png"
New-Item -ItemType Directory -Force -Path $png | Out-Null

function Resolve-OutputPath([string]$Path, [bool]$IsFile) {
    if (-not [System.IO.Path]::IsPathRooted($Path)) { $Path = Join-Path $caller $Path }
    $parent = $(if ($IsFile) { Split-Path -Parent $Path } else { $Path })
    if ($parent) { New-Item -ItemType Directory -Force -Path $parent | Out-Null }
    return $Path
}

$runnerArgs = @($args)
if ($runnerArgs.Count -gt 0) {
    switch ($runnerArgs[0]) {
        "render" { if ($runnerArgs.Count -gt 1) { $runnerArgs[1] = Resolve-OutputPath $runnerArgs[1] $false } else { $runnerArgs += $png } }
        "gallery" { if ($runnerArgs.Count -gt 1) { $runnerArgs[1] = Resolve-OutputPath $runnerArgs[1] $true } else { $runnerArgs += (Join-Path $png "galaxy_gallery.png") } }
        "density" { if ($runnerArgs.Count -gt 5) { $runnerArgs[5] = Resolve-OutputPath $runnerArgs[5] $true } }
    }
}

Push-Location $work
try {
    & $ctx.Dotnet (Join-Path $work "bin\UniverseRunner.dll") @runnerArgs
    $code = $LASTEXITCODE
}
finally { Pop-Location }
exit $code

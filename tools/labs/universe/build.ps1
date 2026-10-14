<#
.SYNOPSIS
  Builds the universe lab from the repository sources, in place (nothing is copied into git, everything goes to tools\labs\out\universe\bin).
.DESCRIPTION
  Frontier.Core         Scripts\Core\**\*.cs the way Unity compiles the asmdef (netstandard 2.1, C# 9, warnings are errors)
  Frontier.Core.Tests   Tests\EditMode\Core and Tests\EditMode\Universe (the NUnit sources of Unity and of the plain .NET CI project)
  UniverseRunner        tools\labs\universe\runner (console: render, gallery, density, inspect, test), on the editor's .NET 8 runtime
  Steps whose inputs did not change are skipped; -Force rebuilds everything.
.EXAMPLE
  powershell -NoProfile -ExecutionPolicy Bypass -File tools\labs\universe\build.ps1 [-Target all|core|tests|runner] [-Force]
#>
param(
    [ValidateSet("all", "core", "tests", "runner")][string]$Target = "all",
    [switch]$Force
)
. (Join-Path $PSScriptRoot "..\lab-common.ps1")
$ctx = Get-LabContext $PSScriptRoot
$bin = Join-Path $ctx.Out "universe\bin"
New-Item -ItemType Directory -Force -Path $bin | Out-Null
$flags = Get-CommonCscFlags
$scripts = @((Join-Path $PSScriptRoot "build.ps1"), (Join-Path $PSScriptRoot "..\lab-common.ps1"))
$compiled = 0

$coreDll = Join-Path $bin "Frontier.Core.dll"
$testsDll = Join-Path $bin "Frontier.Core.Tests.dll"
$runnerDll = Join-Path $bin "UniverseRunner.dll"

if ($Target -eq "all" -or $Target -eq "core") {
    $src = Get-CsFiles @($ctx.CoreDir)
    if ($Force -or -not (Test-UpToDate $coreDll ($src + $scripts))) {
        Invoke-Csc $ctx "Frontier.Core (Scripts\Core, netstandard2.1, C# 9)" ($flags + @("/target:library", "/warnaserror+", "/warn:4", "/out:$coreDll", "/r:$($ctx.NetStd)") + $src)
        $compiled++
    }
}

if ($Target -eq "all" -or $Target -eq "tests") {
    $src = Get-CsFiles @((Join-Path $ctx.TestsDir "Core"), (Join-Path $ctx.TestsDir "Universe"))
    if ($Force -or -not (Test-UpToDate $testsDll ($src + $coreDll + $scripts))) {
        $shared = Get-SharedRuntimeRefs $ctx
        Invoke-Csc $ctx "Frontier.Core.Tests (Tests\EditMode\Core + Universe)" ($flags + @("/target:library", "/warn:4", "/out:$testsDll", "/r:$coreDll", "/r:$($ctx.NUnit)") + $shared + $src)
        $compiled++
    }
}

if ($Target -eq "all" -or $Target -eq "runner") {
    $src = Get-CsFiles @((Join-Path $PSScriptRoot "runner"))
    if ($Force -or -not (Test-UpToDate $runnerDll ($src + $coreDll + $testsDll + $scripts))) {
        $shared = Get-SharedRuntimeRefs $ctx
        Invoke-Csc $ctx "UniverseRunner" ($flags + @("/target:exe", "/warn:4", "/out:$runnerDll", "/r:$coreDll", "/r:$testsDll", "/r:$($ctx.NUnit)") + $shared + $src)
        $compiled++
    }
}

Copy-Item $ctx.NUnit (Join-Path $bin "nunit.framework.dll") -Force
Copy-Item (Join-Path $PSScriptRoot "runner\UniverseRunner.runtimeconfig.json") (Join-Path $bin "UniverseRunner.runtimeconfig.json") -Force
if ($compiled -gt 0) { Write-Host "== universe lab built ($Target): $bin" }

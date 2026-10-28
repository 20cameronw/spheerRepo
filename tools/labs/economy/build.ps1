<#
.SYNOPSIS
  Builds the economy lab from the repository sources, in place (nothing is copied into git, everything goes to tools\labs\out\economy).
.DESCRIPTION
  Frontier.Core            Scripts\Core\**\*.cs the way Unity compiles the asmdef (netstandard 2.1, C# 9, warnings are errors)
  Frontier.Economy         Scripts\Economy\**\*.cs the same way, referencing Frontier.Core
  Frontier.Economy.Tests   Tests\EditMode\Economy (the NUnit sources of Unity and of the plain .NET CI project)
  EconomyRunner            tools\labs\economy\runner plus the PNG canvas of the universe lab (console: test, sim, export, report, ...)
  The console runs on the .NET 8 runtime that ships with the Unity editor, so no SDK is needed. Steps whose inputs did not change are
  skipped; -Force rebuilds everything.
.EXAMPLE
  powershell -NoProfile -ExecutionPolicy Bypass -File tools\labs\economy\build.ps1 [-Target all|core|economy|tests|runner] [-Force]
#>
param(
    [ValidateSet("all", "core", "economy", "tests", "runner")][string]$Target = "all",
    [switch]$Force
)
. (Join-Path $PSScriptRoot "..\lab-common.ps1")
$ctx = Get-LabContext $PSScriptRoot
$root = Join-Path $ctx.Out "economy"
$bin = Join-Path $root "bin"
New-Item -ItemType Directory -Force -Path $bin | Out-Null
$flags = Get-CommonCscFlags
$scripts = @((Join-Path $PSScriptRoot "build.ps1"), (Join-Path $PSScriptRoot "..\lab-common.ps1"))
$compiled = 0

$coreDll = Join-Path $bin "Frontier.Core.dll"
$economyDll = Join-Path $bin "Frontier.Economy.dll"
$testsDll = Join-Path $bin "Frontier.Economy.Tests.dll"
$runnerDll = Join-Path $bin "EconomyRunner.dll"

if ($Target -eq "all" -or $Target -eq "core") {
    $src = Get-CsFiles @($ctx.CoreDir)
    if ($Force -or -not (Test-UpToDate $coreDll ($src + $scripts))) {
        Invoke-Csc $ctx "Frontier.Core (Scripts\Core, netstandard2.1, C# 9)" ($flags + @("/target:library", "/warnaserror+", "/warn:4", "/out:$coreDll", "/r:$($ctx.NetStd)") + $src)
        $compiled++
    }
}

if ($Target -eq "all" -or $Target -eq "economy") {
    $src = Get-CsFiles @($ctx.EconomyDir)
    if ($Force -or -not (Test-UpToDate $economyDll ($src + $coreDll + $scripts))) {
        Invoke-Csc $ctx "Frontier.Economy (Scripts\Economy, netstandard2.1, C# 9)" ($flags + @("/target:library", "/warnaserror+", "/warn:4", "/out:$economyDll", "/r:$($ctx.NetStd)", "/r:$coreDll") + $src)
        $compiled++
    }
}

if ($Target -eq "all" -or $Target -eq "tests") {
    $src = Get-CsFiles @((Join-Path $ctx.TestsDir "Economy"))
    if ($Force -or -not (Test-UpToDate $testsDll ($src + $coreDll + $economyDll + $scripts))) {
        $shared = Get-SharedRuntimeRefs $ctx
        Invoke-Csc $ctx "Frontier.Economy.Tests (Tests\EditMode\Economy)" ($flags + @("/target:library", "/warn:4", "/out:$testsDll", "/r:$coreDll", "/r:$economyDll", "/r:$($ctx.NUnit)") + $shared + $src)
        $compiled++
    }
}

if ($Target -eq "all" -or $Target -eq "runner") {
    $src = Get-CsFiles @((Join-Path $PSScriptRoot "runner"))
    # The PNG canvas (5x7 font, encoder) is the universe lab's own file, compiled in unmodified.
    $png = Join-Path $ctx.Labs "universe\runner\PngWriter.cs"
    $src += $png
    if ($Force -or -not (Test-UpToDate $runnerDll ($src + $coreDll + $economyDll + $testsDll + $scripts))) {
        $shared = Get-SharedRuntimeRefs $ctx
        Invoke-Csc $ctx "EconomyRunner" ($flags + @("/target:exe", "/warn:4", "/out:$runnerDll", "/r:$coreDll", "/r:$economyDll", "/r:$testsDll", "/r:$($ctx.NUnit)") + $shared + $src)
        $compiled++
    }
}

Copy-Item $ctx.NUnit (Join-Path $bin "nunit.framework.dll") -Force
Copy-Item (Join-Path $PSScriptRoot "runner\EconomyRunner.runtimeconfig.json") (Join-Path $bin "EconomyRunner.runtimeconfig.json") -Force
# The integration command reads its template from the folder above the program (see runner\IntegrationDoc.cs).
Copy-Item (Join-Path $PSScriptRoot "INTEGRATION.template.md") (Join-Path $root "INTEGRATION.template.md") -Force
if ($compiled -gt 0) { Write-Host "== economy lab built ($Target): $bin" }

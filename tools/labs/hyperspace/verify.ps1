<#
.SYNOPSIS
  Builds unchanged Hyperspace sources with Unity's bundled compiler, verifies all nine golden digests on CoreCLR and Mono,
  and optionally runs the complete engine-free Hyperspace NUnit suite on CoreCLR. No SDK installation is required.
.EXAMPLE
  powershell -NoProfile -ExecutionPolicy Bypass -File tools\labs\hyperspace\verify.ps1 -Tests
#>
param([switch]$Tests)
. (Join-Path $PSScriptRoot "..\lab-common.ps1")
$ctx = Get-LabContext $PSScriptRoot
$bin = Join-Path $ctx.Out "hyperspace\bin"
New-Item -ItemType Directory -Force -Path $bin | Out-Null
$flags = Get-CommonCscFlags
$core = Join-Path $bin "Frontier.Core.dll"
$test = Join-Path $bin "Frontier.Core.Tests.dll"
$runner = Join-Path $bin "HyperspaceCheck.exe"
$src = Get-CsFiles @($ctx.CoreDir)
Invoke-Csc $ctx "Frontier.Core (warnings as errors)" ($flags + @("/target:library", "/warnaserror+", "/out:$core", "/r:$($ctx.NetStd)") + $src)
$src = Get-CsFiles @((Join-Path $ctx.TestsDir "Hyperspace"))
$monoRefs = @("/r:$($ctx.MonoLib)\mscorlib.dll", "/r:$($ctx.MonoLib)\System.dll",
    "/r:$($ctx.MonoLib)\System.Core.dll", "/r:$($ctx.MonoLib)\Facades\netstandard.dll")
Invoke-Csc $ctx "Hyperspace NUnit tests" ($flags + $monoRefs + @("/target:library", "/out:$test", "/r:$core", "/r:$($ctx.NUnit)") + $src)
Invoke-Csc $ctx "HyperspaceCheck" ($flags + $monoRefs + @("/target:exe", "/out:$runner", "/r:$core", "/r:$test",
    (Join-Path $PSScriptRoot "HyperspaceCheck.cs"), (Join-Path $PSScriptRoot "..\universe\runner\TestRunner.cs")))
Copy-Item $ctx.NUnit (Join-Path $bin "nunit.framework.dll") -Force
Copy-Item (Join-Path $PSScriptRoot "..\universe\runner\UniverseRunner.runtimeconfig.json") (Join-Path $bin "HyperspaceCheck.runtimeconfig.json") -Force
$clr = @(& $ctx.Dotnet $runner digest)
if ($LASTEXITCODE -ne 0) { throw "CoreCLR golden digests failed" }
$oldPath = $env:MONO_PATH
$oldCfg = $env:MONO_CFG_DIR
try {
    $env:MONO_PATH = $ctx.MonoLib + ";" + $ctx.MonoLib + "\Facades"
    $env:MONO_CFG_DIR = $ctx.MonoEtc
    $mono = @(& $ctx.Mono $runner digest)
    if ($LASTEXITCODE -ne 0) { throw "Mono golden digests failed" }
}
finally { $env:MONO_PATH = $oldPath; $env:MONO_CFG_DIR = $oldCfg }
$clr | ForEach-Object { Write-Host $_ }
if (($clr -join "`n") -ne ($mono -join "`n")) { throw "CoreCLR/Mono digests differ" }
Write-Host "PASS: all nine golden digests identical on CoreCLR and Unity Mono"
if ($Tests) {
    & $ctx.Dotnet $runner test
    if ($LASTEXITCODE -ne 0) { throw "Hyperspace tests failed" }
}

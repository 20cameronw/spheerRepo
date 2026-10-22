<#
.SYNOPSIS
  Cross-runtime determinism check of the economy: catalog, obstacle layouts, shard curve, score components and three simulated players
  must be bit-identical on the .NET 8 CoreCLR and on Unity's own Mono.
.DESCRIPTION
  Compiles monocheck\MonoCheck.cs against the lab's Frontier.Core.dll and Frontier.Economy.dll (netstandard 2.1) and runs it on
    1) the .NET 8 CoreCLR shipped with the Unity editor, and
    2) the editor's Mono runtime (what the Test Runner uses),
  then compares the output line by line (tuning digest, catalog digest, 600 obstacle layouts, shard skip curve, score, three 300 day simulations).
  The tuning digest also has to equal docs\economy\data\manifest.json (a unit test checks that). Exit code 0 only when both runtimes agree.
.EXAMPLE
  powershell -NoProfile -ExecutionPolicy Bypass -File tools\labs\economy\crosscheck.ps1
#>
. (Join-Path $PSScriptRoot "..\lab-common.ps1")
$ctx = Get-LabContext $PSScriptRoot
& (Join-Path $PSScriptRoot "build.ps1") -Target core
& (Join-Path $PSScriptRoot "build.ps1") -Target economy

$bin = Join-Path $ctx.Out "economy\bin"
$out = Join-Path $ctx.Out "economy\mono"
New-Item -ItemType Directory -Force -Path $out | Out-Null

& $ctx.Dotnet $ctx.Csc /nologo /nostdlib+ /noconfig /langversion:9.0 /optimize+ /target:exe "/out:$out\MonoCheck.exe" "/r:$($ctx.NetStd)" "/r:$bin\Frontier.Core.dll" "/r:$bin\Frontier.Economy.dll" "$PSScriptRoot\monocheck\MonoCheck.cs"
if ($LASTEXITCODE -ne 0) { throw "csc failed for MonoCheck" }
Copy-Item "$bin\Frontier.Core.dll" $out -Force
Copy-Item "$bin\Frontier.Economy.dll" $out -Force
Copy-Item "$PSScriptRoot\runner\EconomyRunner.runtimeconfig.json" "$out\MonoCheck.runtimeconfig.json" -Force

$core = (& $ctx.Dotnet "$out\MonoCheck.exe") | Where-Object { $_ -notlike "runtime:*" }
$env:MONO_PATH = $ctx.MonoLib + ";" + $ctx.MonoLib + "\Facades"
$env:MONO_CFG_DIR = $ctx.MonoEtc
Push-Location $out
try { $mn = (& $ctx.Mono --debug MonoCheck.exe) | Where-Object { $_ -notlike "runtime:*" } }
finally {
    Pop-Location
    Remove-Item Env:MONO_PATH
    Remove-Item Env:MONO_CFG_DIR
}

"CoreCLR (.NET 8):"; $core | ForEach-Object { "  " + $_ }
"Unity Mono:"; $mn | ForEach-Object { "  " + $_ }
$a = ($core -join "`n").Trim([char]0xFEFF)
$b = ($mn -join "`n").Trim([char]0xFEFF)
if ($a -eq $b) { "RESULT: IDENTICAL on both runtimes" } else { "RESULT: DIFFERENT"; exit 1 }

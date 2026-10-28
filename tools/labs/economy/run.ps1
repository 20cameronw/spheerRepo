<#
.SYNOPSIS
  Runs the economy console on the .NET 8 runtime of the Unity editor (builds first when a source changed).
.DESCRIPTION
  Commands (details in tools\labs\README.md):
    test [--filter text] [--list]                the Economy NUnit tests (the same sources as Unity and the plain .NET CI project)
    tables [all|res|rec|str|tool|upg|rsr|slots]  quick text dumps of the catalog
    sim [casual|regular|hardcore|all] [days=N] [seed=N] [stats=1] [goals=1] [brief=1] [log=1] [key=value ...]   balance simulator
    reach                                        which part of the map each jump level opens
    shards [archetype] [adShare]                 weekly shard income of a simulated free player
    export [dir]                                 the versioned JSON (default tools\labs\out\economy\data; the committed copy is docs\economy\data)
    report [dir]                                 ECONOMY.md (default tools\labs\out\economy\reports)
    codex [dir]                                  ATLAS_TEXT.md, ICON_BRIEF.md, TUNING_KEYS.md
    charts [dir]                                 the four balance charts as PNG
    integration                                  INTEGRATION.md into tools\labs\out\economy
  Relative paths in the arguments are relative to the directory you call this from. docs\economy publishes everything: see publish-docs.ps1.
.EXAMPLE
  powershell -NoProfile -ExecutionPolicy Bypass -File tools\labs\economy\run.ps1 sim all days=730
  powershell -NoProfile -ExecutionPolicy Bypass -File tools\labs\economy\run.ps1 test --filter Travel
#>
. (Join-Path $PSScriptRoot "..\lab-common.ps1")
$ctx = Get-LabContext $PSScriptRoot
& (Join-Path $PSScriptRoot "build.ps1")
& $ctx.Dotnet (Join-Path $ctx.Out "economy\bin\EconomyRunner.dll") @args
exit $LASTEXITCODE

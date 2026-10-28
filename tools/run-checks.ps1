<#
.SYNOPSIS
  Runs the five editor check suites that gate every change (planet sizes, storage, combat balance, icons, panel layout).
.DESCRIPTION
  Each suite is a Frontier.EditorTools.<Name>.Run method started through run-unity.ps1 (named mutex, watchdog). A suite passes
  when Unity exits with 0 and the suite printed its "Frontier: ... passed" line. Compile errors and CS warnings from Frontier code
  are reported too. Logs: SpheerURP\Logs\check-<suite>.log. Exit code 0 only when all selected suites pass.
.EXAMPLE
  powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\run-checks.ps1
  powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\run-checks.ps1 -Only Storage,Layout
#>
param(
    # Suites to run, comma separated (Planet, Storage, Combat, Icon, Layout). Default: all five.
    [string[]]$Only = @()
)

$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot
$logDir = Join-Path $repo "SpheerURP\Logs"
New-Item -ItemType Directory -Force -Path $logDir | Out-Null

# Layout is an editor window that exits by itself, so it must not get -quit.
$suites = @(
    @{ Name = "Planet"; Class = "FrontierPlanetChecks"; NoQuit = $false },
    @{ Name = "Storage"; Class = "FrontierStorageChecks"; NoQuit = $false },
    @{ Name = "Combat"; Class = "FrontierCombatChecks"; NoQuit = $false },
    @{ Name = "Icon"; Class = "FrontierIconChecks"; NoQuit = $false },
    @{ Name = "Layout"; Class = "FrontierLayoutChecks"; NoQuit = $true }
)

# With -File, "-Only Storage,Layout" arrives as ONE string, so split it ourselves and validate.
$Only = @($Only | ForEach-Object { $_ -split "[,;\s]+" } | Where-Object { $_ })
$valid = @($suites | ForEach-Object { $_.Name })
foreach ($name in $Only) { if ($valid -notcontains $name) { throw "Unknown suite '$name'. Valid suites: $($valid -join ', ')" } }

$failed = 0
foreach ($s in $suites) {
    if ($Only.Count -gt 0 -and $Only -notcontains $s.Name) { continue }
    $log = Join-Path $logDir ("check-" + $s.Name + ".log")
    $method = "Frontier.EditorTools." + $s.Class + ".Run"
    if ($s.NoQuit) { $out = & "$PSScriptRoot\run-unity.ps1" -Method $method -NoQuit -Log $log }
    else { $out = & "$PSScriptRoot\run-unity.ps1" -Method $method -Log $log }
    $exit = $LASTEXITCODE
    $passedLines = @($out | Where-Object { $_ -match "^Frontier: .*passed" }).Count
    $compile = @(Select-String -Path $log -Pattern "error CS|warning CS" -ErrorAction SilentlyContinue | ForEach-Object { $_.Line.Trim() } | Select-Object -Unique)
    $ok = ($exit -eq 0) -and ($passedLines -gt 0) -and ($compile.Count -eq 0)
    if (-not $ok) { $failed++ }
    Write-Host ("{0,-8} {1,-5} exit={2} passed-lines={3} compile-diagnostics={4}   {5}" -f $s.Name, $(if ($ok) { "PASS" } else { "FAIL" }), $exit, $passedLines, $compile.Count, $log)
    if (-not $ok) {
        $out | Where-Object { $_ -match "Exception|error|failed" -and $_ -notmatch "Licensing|DirectoryNotFound" } | Select-Object -First 8 | ForEach-Object { Write-Host ("         " + $_) }
        $compile | Select-Object -First 8 | ForEach-Object { Write-Host ("         " + $_) }
    }
}

if ($failed -gt 0) { Write-Host "$failed check suite(s) failed."; exit 1 }
Write-Host "All selected check suites passed."
exit 0

<#
.SYNOPSIS
  Runs the Unity Test Framework (EditMode or PlayMode) in batch mode, then prints a pass/fail summary from the NUnit XML.
.DESCRIPTION
  Uses the same recipe as run-unity.ps1 (named mutex so only one editor opens the project, watchdog, filtered log), but starts Unity
  with -runTests instead of -executeMethod and never passes -quit (the test runner exits by itself). Exit code: 0 when every test
  passed or was skipped, 1 when a test failed, 2 when no results were produced (compile error, crash, timeout), 3 when no test ran.
.EXAMPLE
  powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\run-tests.ps1 -Platform EditMode
  powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\run-tests.ps1 -Platform EditMode -IncludeSlow      # everything, including the balance simulator
  powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\run-tests.ps1 -Platform EditMode -Category Slow     # only the slow tests
  powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\run-tests.ps1 -Platform PlayMode
  powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\run-tests.ps1 -Platform EditMode -TestFilter "Frontier.Universe.Tests.DeterminismTests" -OutputMatch "digest"
.NOTES
  Never pass -nographics: PlayMode tests render. Results: SpheerURP\TestResults\<platform>.xml, log: SpheerURP\Logs\tests-<platform>.log
  (both git-ignored). Set UNITY_PATH to override the editor location (see run-unity.ps1).
  Tests marked [Category("Slow")] (the balance simulator fixtures of Frontier.Economy, about a minute in the editor's Mono) are skipped
  unless -IncludeSlow or -Category is given; the plain .NET CI project and the Unity CI workflow always run them.
#>
param(
    [Parameter(Position = 0)][ValidateSet("EditMode", "PlayMode")][string]$Platform = "EditMode",
    # Test assemblies to run (comma separated); default is Frontier.Tests.<Platform>.
    [string[]]$Assemblies = @(),
    # Unity -testFilter: semicolon separated test names (full or partial) or regular expressions (prefix with '!' to exclude).
    [string]$TestFilter = "",
    # Unity -testCategory: semicolon separated NUnit categories ("Slow" runs only the slow tests). Without it the default is every test
    # except the ones marked Slow.
    [string]$Category = "",
    # Also run the tests marked [Category("Slow")].
    [switch]$IncludeSlow,
    [string]$Results = "",
    [string]$Log = "",
    [int]$TimeoutSec = 1800,
    # Print every test (not only failures and skips).
    [switch]$ShowAll,
    # Show the lines of the Unity log (where the tests' console output ends up) that match this regular expression, for example
    # "digest" to see the golden hashes of the universe tests.
    [string]$OutputMatch = ""
)

$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repo "SpheerURP"
# With -File, "-Assemblies A,B" arrives as ONE string, so split it ourselves.
$Assemblies = @($Assemblies | ForEach-Object { $_ -split "[,;]" } | Where-Object { $_ })
if ($Assemblies.Count -eq 0) { $Assemblies = @("Frontier.Tests.$Platform") }
if (-not $Results) { $Results = Join-Path $project "TestResults\$Platform.xml" }
if (-not $Log) { $Log = Join-Path $project "Logs\tests-$Platform.log" }
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Results) | Out-Null
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Log) | Out-Null
if (Test-Path $Results) { Remove-Item $Results -Force }

$extra = @("-runTests", "-testPlatform", $Platform, "-assemblyNames", ("`"" + ($Assemblies -join ";") + "`""), "-testResults", "`"$Results`"")
if ($TestFilter) { $extra += @("-testFilter", "`"$TestFilter`"") }
if ($Category) { $extra += @("-testCategory", "`"$Category`"") }
elseif (-not $IncludeSlow) { $extra += @("-testCategory", "`"!Slow`"") }

Write-Host "Unity $Platform tests: $($Assemblies -join ', ')"
if (-not $Category -and -not $IncludeSlow) { Write-Host "(tests marked Slow are skipped; add -IncludeSlow to run everything)" }
$unityLines = & "$PSScriptRoot\run-unity.ps1" -NoQuit -ExtraArgs $extra -TimeoutSec $TimeoutSec -Log $Log -Filter "error CS|Scripts have compiler errors|Compilation failed|Unable to find|No tests"
$unityExit = $LASTEXITCODE
$unityLines | Where-Object { $_ -match "error CS|compiler errors|Compilation failed|Unable to find|No tests" } | Select-Object -First 30 | ForEach-Object { Write-Host $_ }

if (-not (Test-Path $Results)) {
    Write-Host "NO RESULTS: Unity exited with $unityExit and wrote no test results (compile error, crash or timeout). Log: $Log"
    exit 2
}

$doc = New-Object System.Xml.XmlDocument
$doc.Load($Results)
$cases = @($doc.SelectNodes("//test-case"))
$counts = @{ Passed = 0; Failed = 0; Skipped = 0; Inconclusive = 0 }
$failed = @()
foreach ($c in $cases) {
    $r = [string]$c.result
    if ($r -like "Failed*") { $counts.Failed++ }
    elseif ($r -eq "Passed") { $counts.Passed++ }
    elseif ($r -eq "Skipped") { $counts.Skipped++ }
    else { $counts.Inconclusive++ }
}

$seconds = 0.0
$root = $doc.SelectSingleNode("/test-run")
if ($root -and $root.duration) { [void][double]::TryParse([string]$root.duration, [Globalization.NumberStyles]::Float, [Globalization.CultureInfo]::InvariantCulture, [ref]$seconds) }

foreach ($c in $cases) {
    $r = [string]$c.result
    $name = [string]$c.fullname
    if ($r -like "Failed*") {
        Write-Host ("FAILED   " + $name)
        $msg = $c.SelectSingleNode("failure/message")
        if ($msg) { ($msg.InnerText.Trim() -split "`n") | Select-Object -First 12 | ForEach-Object { Write-Host ("         " + $_.TrimEnd()) } }
        $stack = $c.SelectSingleNode("failure/stack-trace")
        if ($stack) { ($stack.InnerText.Trim() -split "`n") | Select-Object -First 4 | ForEach-Object { Write-Host ("           at " + ($_.Trim() -replace '^at\s+', '')) } }
    }
    elseif ($r -eq "Skipped" -or $r -eq "Inconclusive") {
        $reason = $c.SelectSingleNode("reason/message")
        $why = ""
        if ($reason) { $why = " - " + $reason.InnerText.Trim() }
        Write-Host ("SKIPPED  " + $name + $why)
    }
    elseif ($ShowAll) {
        Write-Host ("passed   " + $name)
    }
}

# Console output of tests goes to the Unity log (the NUnit XML has no <output>): show the lines that match.
if ($OutputMatch -and (Test-Path $Log)) {
    Write-Host ""
    Write-Host "Test output matching '$OutputMatch':"
    Select-String -Path $Log -Pattern $OutputMatch | ForEach-Object { $_.Line.Trim() } | Select-Object -Unique | Select-Object -First 200 | ForEach-Object { Write-Host ("  " + $_) }
}

$total = $cases.Count
Write-Host ""
Write-Host ("{0}: {1} passed, {2} failed, {3} skipped, {4} inconclusive (of {5}) in {6:N1}s" -f $Platform, $counts.Passed, $counts.Failed, $counts.Skipped, $counts.Inconclusive, $total, $seconds)
Write-Host "Results: $Results"
if ($counts.Failed -gt 0) { exit 1 }
if ($total -eq 0) { Write-Host "No tests ran: check the assembly names and filters."; exit 3 }
exit 0

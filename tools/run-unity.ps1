<#
.SYNOPSIS
  Runs Unity in batch mode for this project with a watchdog, a cross-process lock and a filtered result summary.
.EXAMPLE
  ./tools/run-unity.ps1 -Method Frontier.EditorTools.FrontierPlanetChecks.Run
  ./tools/run-unity.ps1 -Method Frontier.EditorTools.ScreenshotHarness.Run -NoQuit -ExtraArgs @('-frontierShotDir','out')
.NOTES
  Only one Unity process can open the project at a time, so runs are serialised through a named mutex.
  Do not pass -nographics for anything that renders (the screenshot harness).
  Set UNITY_PATH to override the editor location.
#>
param(
    [string]$Method = "",
    [string[]]$ExtraArgs = @(),
    [int]$TimeoutSec = 900,
    [string]$Log = "",
    [switch]$NoQuit,
    [string]$Filter = "error CS|Frontier:|Frontier harness|Exception|Shader error|Scripts have compiler errors|Test Run|Failed|Passed"
)

$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repo "SpheerURP"
$versionLine = Select-String -Path (Join-Path $project "ProjectSettings\ProjectVersion.txt") -Pattern "m_EditorVersion:\s*(\S+)"
$version = $versionLine.Matches[0].Groups[1].Value
$unity = $env:UNITY_PATH
if (-not $unity) { $unity = "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe" }
if (-not (Test-Path $unity)) { throw "Unity $version not found at '$unity'. Install it or set UNITY_PATH." }
if (-not $Log) { $Log = Join-Path $env:TEMP ("frontier-unity-" + [guid]::NewGuid().ToString("N").Substring(0, 8) + ".log") }

$unityArgs = @("-batchmode", "-projectPath", "`"$project`"", "-logFile", "`"$Log`"")
if (-not $NoQuit) { $unityArgs += "-quit" }
if ($Method) { $unityArgs += @("-executeMethod", $Method) }
$unityArgs += $ExtraArgs

$mutex = New-Object System.Threading.Mutex($false, "Global\FrontierUnityBatch")
if (-not $mutex.WaitOne([TimeSpan]::FromMinutes(30))) { throw "Another Unity batch run held the lock for 30 minutes." }
try {
    $proc = Start-Process -FilePath $unity -ArgumentList $unityArgs -PassThru
    if (-not $proc.WaitForExit($TimeoutSec * 1000)) {
        Stop-Process -Id $proc.Id -Force
        Write-Host "Unity timed out after $TimeoutSec s. Log: $Log"
        $exit = 124
    } else {
        $exit = $proc.ExitCode
    }
} finally {
    $mutex.ReleaseMutex()
}

if (Test-Path $Log) {
    Select-String -Path $Log -Pattern $Filter | ForEach-Object { $_.Line.Trim() } | Select-Object -Unique | Select-Object -First 80
}
Write-Host "Unity exit code: $exit   Log: $Log"
exit $exit

<#
.SYNOPSIS
  Captures every Frontier screen on virtual phone/tablet devices and writes PNGs plus report.json (UI lint).
.EXAMPLE
  ./tools/capture-screens.ps1
  ./tools/capture-screens.ps1 -Devices phone-1080x1920 -Only hud,shop
#>
param(
    [string]$Out = "",
    [string]$Devices = "",
    [string]$Only = "",
    [int]$TimeoutSec = 1200
)

$repo = Split-Path -Parent $PSScriptRoot
if (-not $Out) { $Out = Join-Path $repo ("SpheerURP\Screens\" + (Get-Date -Format "yyyyMMdd-HHmmss")) }
$extra = @("-frontierShotDir", "`"$Out`"")
if ($Devices) { $extra += @("-frontierShotDevices", $Devices) }
if ($Only) { $extra += @("-frontierShotOnly", $Only) }

& "$PSScriptRoot\run-unity.ps1" -Method Frontier.EditorTools.ScreenshotHarness.Run -NoQuit -ExtraArgs $extra -TimeoutSec $TimeoutSec
$code = $LASTEXITCODE
Write-Host "Screens: $Out"
exit $code

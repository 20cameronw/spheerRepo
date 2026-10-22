<#
.SYNOPSIS
  Builds the Frontier scene into a debug-signed Android development APK.
.NOTES
  Requires this project's Unity version with Android Build Support, SDK/NDK and OpenJDK.
  Never enables services or changes bundle identifiers, signing credentials or release settings.
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$Output,
    [int]$TimeoutSec = 1800,
    [string]$Log = ""
)
$ErrorActionPreference = "Stop"
$outputPath = [System.IO.Path]::GetFullPath($Output)
if ([System.IO.Path]::GetExtension($outputPath) -ine ".apk") { throw "Output must end in .apk." }
if (Test-Path -LiteralPath $outputPath) { throw "Output already exists; choose a new APK path." }
& (Join-Path $PSScriptRoot "run-unity.ps1") -Method Frontier.EditorTools.FrontierPlayerBuild.AndroidDevelopment `
    -TimeoutSec $TimeoutSec -Log $Log -ExtraArgs @("-buildTarget", "Android", "-frontierBuildPath", "`"$outputPath`"")
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
if (!(Test-Path -LiteralPath $outputPath -PathType Leaf)) { throw "Unity exited without producing the APK." }
$artifact = Get-Item -LiteralPath $outputPath
if ($artifact.Length -eq 0) { throw "Unity produced an empty APK." }
$hash = (Get-FileHash -LiteralPath $outputPath -Algorithm SHA256).Hash
Write-Host "Development APK: $outputPath"
Write-Host "APK bytes: $($artifact.Length)  SHA256: $hash"
exit 0

<#
.SYNOPSIS
  Shared helpers of the console labs: dot-sourced by tools\labs\<lab>\build.ps1, run.ps1 and crosscheck.ps1.
.DESCRIPTION
  The labs compile the repository's own sources IN PLACE with the toolchain that ships with the Unity editor, so no .NET SDK is needed and
  the result is what Unity compiles: Roslyn (Data\DotNetSdkRoslyn\csc.dll), the netstandard 2.1 reference assemblies, C# 9, warnings as
  errors for the game assemblies. The consoles run on the editor's .NET 8 runtime (Data\NetCoreRuntime); the cross-checks also run on its
  Mono. The NUnit assembly is the one of the project's package cache (SpheerURP\Library\PackageCache), nothing is copied into git.
  Set UNITY_PATH to the Unity.exe to override the editor location (the same variable tools\run-unity.ps1 uses).
  Everything the labs build or write goes to tools\labs\out (git-ignored).
#>
$ErrorActionPreference = "Stop"

function Get-LabContext {
    param([Parameter(Mandatory = $true)][string]$LabDir)
    $repo = (Resolve-Path (Join-Path $LabDir "..\..\..")).Path
    $project = Join-Path $repo "SpheerURP"
    $versionLine = Select-String -Path (Join-Path $project "ProjectSettings\ProjectVersion.txt") -Pattern "m_EditorVersion:\s*(\S+)"
    $version = $versionLine.Matches[0].Groups[1].Value
    $unityExe = $env:UNITY_PATH
    if (-not $unityExe) { $unityExe = "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe" }
    if (-not (Test-Path $unityExe)) { throw "Unity $version not found at '$unityExe'. Install it or set UNITY_PATH (the same variable tools\run-unity.ps1 uses)." }
    $data = Join-Path (Split-Path -Parent $unityExe) "Data"
    $shared = (Get-ChildItem (Join-Path $data "NetCoreRuntime\shared\Microsoft.NETCore.App") -Directory | Sort-Object Name -Descending | Select-Object -First 1).FullName
    $nunit = Get-ChildItem (Join-Path $project "Library\PackageCache\com.unity.ext.nunit@*\net40\unity-custom\nunit.framework.dll") -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $nunit) { throw "nunit.framework.dll not found in SpheerURP\Library\PackageCache. Open the project in Unity once (or run tools\run-tests.ps1) so the package cache exists." }
    $frontier = Join-Path $project "Assets\Frontier"
    return [pscustomobject]@{
        Repo       = $repo
        Project    = $project
        CoreDir    = Join-Path $frontier "Scripts\Core"
        EconomyDir = Join-Path $frontier "Scripts\Economy"
        TestsDir   = Join-Path $frontier "Tests\EditMode"
        Labs       = Join-Path $repo "tools\labs"
        Out        = Join-Path $repo "tools\labs\out"
        Docs       = Join-Path $repo "docs"
        UnityData  = $data
        Dotnet     = Join-Path $data "NetCoreRuntime\dotnet.exe"
        Csc        = Join-Path $data "DotNetSdkRoslyn\csc.dll"
        NetStd     = Join-Path $data "NetStandard\ref\2.1.0\netstandard.dll"
        Shared     = $shared
        Mono       = Join-Path $data "MonoBleedingEdge\bin\mono.exe"
        MonoLib    = Join-Path $data "MonoBleedingEdge\lib\mono\unityjit-win32"
        MonoEtc    = Join-Path $data "MonoBleedingEdge\etc"
        NUnit      = $nunit.FullName
    }
}

# Flags every lab compile shares (what Unity passes for an asmdef assembly, plus deterministic output and portable symbols).
function Get-CommonCscFlags {
    return @("/nologo", "/nostdlib+", "/noconfig", "/langversion:9.0", "/optimize+", "/deterministic", "/debug:portable", "/utf8output", "/nowarn:1701,1702")
}

# References to every managed assembly of the shared .NET 8 runtime, by simple name (the console programs and the NUnit tests need them).
function Get-SharedRuntimeRefs {
    param([Parameter(Mandatory = $true)]$Context)
    $refs = @("/lib:" + $Context.Shared)
    foreach ($f in Get-ChildItem $Context.Shared -Filter *.dll) {
        try { [System.Reflection.AssemblyName]::GetAssemblyName($f.FullName) | Out-Null; $refs += ("/r:" + $f.Name) } catch { }
    }
    return $refs
}

function Get-CsFiles {
    param([Parameter(Mandatory = $true)][string[]]$Dirs)
    $files = @()
    foreach ($d in $Dirs) { $files += @(Get-ChildItem $d -Recurse -File | Where-Object { $_.Extension -eq ".cs" } | Sort-Object FullName | ForEach-Object { $_.FullName }) }
    return $files
}

# True when Output exists and no input file is newer than it (the labs skip the compile then).
function Test-UpToDate {
    param([string]$Output, [string[]]$Inputs)
    if (-not (Test-Path $Output)) { return $false }
    $built = (Get-Item $Output).LastWriteTimeUtc
    foreach ($i in $Inputs) { if ((Get-Item $i).LastWriteTimeUtc -gt $built) { return $false } }
    return $true
}

function Invoke-Csc {
    param([Parameter(Mandatory = $true)]$Context, [Parameter(Mandatory = $true)][string]$What, [Parameter(Mandatory = $true)][string[]]$CscArgs)
    Write-Host "== csc: $What"
    & $Context.Dotnet $Context.Csc @CscArgs
    if ($LASTEXITCODE -ne 0) { throw "csc failed for $What (exit $LASTEXITCODE)" }
}

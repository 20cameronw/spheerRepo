<#
.SYNOPSIS
  Builds labelled contact sheets from a screenshot-harness output folder so many screens can be reviewed at once.
.EXAMPLE
  ./tools/contact-sheet.ps1 -Dir SpheerURP\Screens\baseline\phone-1080x1920 -Columns 7 -Rows 2 -ThumbWidth 280
#>
param(
    [Parameter(Mandatory = $true)][string]$Dir,
    [int]$Columns = 6,
    [int]$Rows = 2,
    [int]$ThumbWidth = 300,
    [string]$Out = ""
)

Add-Type -AssemblyName System.Drawing
$files = Get-ChildItem $Dir -Filter *.png | Where-Object { $_.Name -match '^\d\d-' } | Sort-Object Name
if (-not $files) { throw "No harness PNGs in $Dir" }
if (-not $Out) { $Out = Join-Path $Dir "_sheets" }
New-Item -ItemType Directory -Force $Out | Out-Null

$first = [System.Drawing.Image]::FromFile($files[0].FullName)
$aspect = $first.Height / [double]$first.Width
$first.Dispose()
$tw = $ThumbWidth
$th = [int]($tw * $aspect)
$label = 26
$pad = 8
$perSheet = $Columns * $Rows
$sheetCount = [math]::Ceiling($files.Count / $perSheet)
$font = New-Object System.Drawing.Font("Segoe UI", 12, [System.Drawing.FontStyle]::Bold)

for ($s = 0; $s -lt $sheetCount; $s++) {
    $w = $Columns * ($tw + $pad) + $pad
    $h = $Rows * ($th + $label + $pad) + $pad
    $bmp = New-Object System.Drawing.Bitmap $w, $h
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.Clear([System.Drawing.Color]::FromArgb(24, 24, 28))
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $brush = [System.Drawing.Brushes]::White
    for ($i = 0; $i -lt $perSheet; $i++) {
        $idx = $s * $perSheet + $i
        if ($idx -ge $files.Count) { break }
        $col = $i % $Columns; $row = [math]::Floor($i / $Columns)
        $x = $pad + $col * ($tw + $pad); $y = $pad + $row * ($th + $label + $pad)
        $img = [System.Drawing.Image]::FromFile($files[$idx].FullName)
        $g.DrawImage($img, $x, $y + $label, $tw, $th)
        $img.Dispose()
        $g.DrawString($files[$idx].BaseName, $font, $brush, $x, $y + 3)
    }
    $g.Dispose()
    $path = Join-Path $Out ("sheet-{0:00}.png" -f ($s + 1))
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host $path
}

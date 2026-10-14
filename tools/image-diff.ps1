<#
.SYNOPSIS
  Compares two screenshot-harness runs per shot (mean absolute RGB difference, 0-255) to spot visual regressions.
.EXAMPLE
  ./tools/image-diff.ps1 -A SpheerURP/Screens/baseline/phone-1080x1920 -B SpheerURP/Screens/post-purge/phone-1080x1920
#>
param(
    [Parameter(Mandatory = $true)][string]$A,
    [Parameter(Mandatory = $true)][string]$B,
    [int]$Step = 3,
    [double]$Warn = 6.0
)

Add-Type -AssemblyName System.Drawing
if (-not ('FrontierImageDiff' -as [type])) {
    $cp = New-Object System.CodeDom.Compiler.CompilerParameters
    [void]$cp.ReferencedAssemblies.Add("System.Drawing.dll")
    Add-Type -TypeDefinition @"
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
public static class FrontierImageDiff {
    static byte[] Read(Bitmap bmp, out int stride) {
        var r = new Rectangle(0, 0, bmp.Width, bmp.Height);
        var d = bmp.LockBits(r, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        stride = d.Stride;
        var bytes = new byte[d.Stride * bmp.Height];
        Marshal.Copy(d.Scan0, bytes, 0, bytes.Length);
        bmp.UnlockBits(d);
        return bytes;
    }
    public static double Mean(string a, string b, int step) {
        using (var ia = new Bitmap(a)) using (var ib = new Bitmap(b)) {
            if (ia.Width != ib.Width || ia.Height != ib.Height) return -1;
            int sa, sb;
            byte[] ba = Read(ia, out sa), bb = Read(ib, out sb);
            double sum = 0; long n = 0;
            for (int y = 0; y < ia.Height; y += step)
                for (int x = 0; x < ia.Width; x += step) {
                    int i = y * sa + x * 3;
                    sum += Math.Abs(ba[i] - bb[i]) + Math.Abs(ba[i + 1] - bb[i + 1]) + Math.Abs(ba[i + 2] - bb[i + 2]);
                    n += 3;
                }
            return sum / n;
        }
    }
}
"@ -CompilerParameters $cp
}

$files = Get-ChildItem $A -Filter *.png | Where-Object { $_.Name -match '^\d\d-' } | Sort-Object Name
foreach ($f in $files) {
    $other = Join-Path $B $f.Name
    if (-not (Test-Path $other)) { Write-Host ("{0,-28} missing in B" -f $f.Name); continue }
    $d = [FrontierImageDiff]::Mean($f.FullName, $other, $Step)
    $flag = if ($d -gt $Warn) { "  <-- differs" } else { "" }
    Write-Host ("{0,-28} {1,6:N2}{2}" -f $f.Name, $d, $flag)
}

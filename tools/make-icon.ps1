# Draws the Prisma logo at several sizes and writes assets\prisma.ico, assets\prisma-256.png and assets\prisma-logo.svg.
# Needs only Windows PowerShell 5.1 (System.Drawing). The geometry (0..100 box) is mirrored in src\Ui.cs (Ui.Logo)
# and in the SVG below; keep the three in sync when changing the design.
param([string]$OutDir = (Join-Path $PSScriptRoot '..\assets'))
Set-StrictMode -Version 3
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
New-Item -ItemType Directory -Force $OutDir | Out-Null
$OutDir = (Resolve-Path $OutDir).Path

# ---- design constants (0..100 coordinates) ----
$Prism = @(@(50, 20), @(22, 72), @(78, 72))
$Enter = @(33.8, 50)       # where the white beam meets the left edge
$Split = @(66.7, 51)       # where the colours leave the right edge
$FanX = 97
$FullColors = @('FF4D6D', 'FFB84D', '6CE08A', '4DC9FF', '9B6BFF'); $FullY = @(26, 39, 52, 65, 78)
$SimpleColors = @('FF4D6D', '6CE08A', '4DB8FF');                    $SimpleY = @(28, 52, 76)

function Argb([int]$a, [string]$hex) {
    [System.Drawing.Color]::FromArgb($a, [Convert]::ToInt32($hex.Substring(0, 2), 16), [Convert]::ToInt32($hex.Substring(2, 2), 16), [Convert]::ToInt32($hex.Substring(4, 2), 16))
}

function New-Logo([int]$size, [bool]$simple) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'; $g.PixelOffsetMode = 'HighQuality'; $g.InterpolationMode = 'HighQualityBicubic'
    $g.Clear([System.Drawing.Color]::Transparent)
    $k = $size / 100.0
    $pt = { param($x, $y) New-Object System.Drawing.PointF ([single]($x * $k)), ([single]($y * $k)) }

    # tile
    $r = 22 * $k * 2
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc(0, 0, $r, $r, 180, 90); $path.AddArc($size - $r, 0, $r, $r, 270, 90)
    $path.AddArc($size - $r, $size - $r, $r, $r, 0, 90); $path.AddArc(0, $size - $r, $r, $r, 90, 90); $path.CloseFigure()
    $rect = New-Object System.Drawing.RectangleF 0, 0, $size, $size
    $bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, (Argb 255 '0D1533'), (Argb 255 '2A2160'), 45.0
    $g.FillPath($bg, $path)
    $g.SetClip($path)

    $wScale = if ($simple) { 1.7 } else { 1.0 }
    $tri = @((& $pt $Prism[0][0] $Prism[0][1]), (& $pt $Prism[1][0] $Prism[1][1]), (& $pt $Prism[2][0] $Prism[2][1]))
    $glassRect = New-Object System.Drawing.RectangleF (22 * $k), (20 * $k), (56 * $k), (52 * $k)
    $glass = New-Object System.Drawing.Drawing2D.LinearGradientBrush $glassRect, (Argb 95 'FFFFFF'), (Argb 28 'FFFFFF'), 90.0
    $g.FillPolygon($glass, [System.Drawing.PointF[]]$tri)

    function Line($x1, $y1, $x2, $y2, $color, [double]$w) {
        $pen = New-Object System.Drawing.Pen $color, ([single]($w * $k))
        $pen.StartCap = 'Round'; $pen.EndCap = 'Round'
        $g.DrawLine($pen, (& $pt $x1 $y1), (& $pt $x2 $y2)); $pen.Dispose()
    }

    # beams: white in, softer white inside, colours out
    Line 4 57 $Enter[0] $Enter[1] (Argb 245 'FFFFFF') (3.6 * $wScale)
    Line $Enter[0] $Enter[1] $Split[0] $Split[1] (Argb 140 'FFFFFF') (2.6 * $wScale)
    $cols = if ($simple) { $SimpleColors } else { $FullColors }
    $ys = if ($simple) { $SimpleY } else { $FullY }
    for ($i = 0; $i -lt $cols.Count; $i++) { Line $Split[0] $Split[1] $FanX $ys[$i] (Argb 255 $cols[$i]) (3.4 * $wScale) }

    # prism outline on top
    $edgeWidth = 3.2; if ($simple) { $edgeWidth = 5.0 }
    $edge = New-Object System.Drawing.Pen (Argb 235 'FFFFFF'), ([single]($edgeWidth * $k))
    $edge.LineJoin = 'Round'
    $g.DrawPolygon($edge, [System.Drawing.PointF[]]$tri)
    $g.Dispose()
    return $bmp
}

# ---- PNGs and ICO ----
# Sizes below 256 are stored as classic 32-bit bitmaps (DIB + AND mask) because older Windows APIs only read
# PNG-compressed icon entries at 256 px. The 256 px entry is a PNG.
function Get-Dib([System.Drawing.Bitmap]$bmp) {
    $w = $bmp.Width; $h = $bmp.Height
    $rect = New-Object System.Drawing.Rectangle 0, 0, $w, $h
    $data = $bmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $stride = $data.Stride
    $buf = New-Object byte[] ($stride * $h)
    [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $buf, 0, $buf.Length)
    $bmp.UnlockBits($data)
    $ms = New-Object System.IO.MemoryStream
    $bw = New-Object System.IO.BinaryWriter $ms
    $bw.Write([uint32]40); $bw.Write([int32]$w); $bw.Write([int32](2 * $h)); $bw.Write([uint16]1); $bw.Write([uint16]32)
    $bw.Write([uint32]0); $bw.Write([uint32]($w * $h * 4)); $bw.Write([int32]0); $bw.Write([int32]0); $bw.Write([uint32]0); $bw.Write([uint32]0)
    for ($y = $h - 1; $y -ge 0; $y--) { $bw.Write($buf, $y * $stride, $w * 4) }   # bottom-up BGRA rows
    $maskRow = [int]([Math]::Floor(($w + 31) / 32)) * 4
    $bw.Write((New-Object byte[] ($maskRow * $h)))                                  # AND mask: all zero (alpha does the work)
    $bw.Flush(); [byte[]]$out = $ms.ToArray(); $bw.Close(); $ms.Dispose()
    return ,$out
}

$sizes = 16, 24, 32, 48, 64, 128, 256
$pngs = @{}
foreach ($s in $sizes) {
    $bmp = New-Logo $s ($s -le 32)
    if ($s -ge 256) {
        $ms = New-Object System.IO.MemoryStream
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $pngs[$s] = $ms.ToArray()
        $bmp.Save((Join-Path $OutDir 'prisma-256.png'), [System.Drawing.Imaging.ImageFormat]::Png)
        $ms.Dispose()
    }
    else { $pngs[$s] = Get-Dib $bmp }
    $bmp.Dispose()
}
$icoPath = Join-Path $OutDir 'prisma.ico'
$fs = [System.IO.File]::Create($icoPath)
$bw = New-Object System.IO.BinaryWriter $fs
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
foreach ($s in $sizes) {
    $dim = if ($s -ge 256) { 0 } else { $s }
    $bw.Write([byte]$dim); $bw.Write([byte]$dim); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([uint16]1); $bw.Write([uint16]32); $bw.Write([uint32]([byte[]]$pngs[$s]).Length); $bw.Write([uint32]$offset)
    $offset += $pngs[$s].Length
}
foreach ($s in $sizes) { $bw.Write([byte[]]$pngs[$s]) }
$bw.Close(); $fs.Close()

# ---- SVG (same geometry) ----
$lines = for ($i = 0; $i -lt $FullColors.Count; $i++) {
    '    <line x1="{0}" y1="{1}" x2="{2}" y2="{3}" stroke="#{4}" stroke-width="3.4"/>' -f $Split[0], $Split[1], $FanX, $FullY[$i], $FullColors[$i]
}
$svg = @"
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100" width="256" height="256" role="img" aria-label="Prisma logo">
  <defs>
    <linearGradient id="bg" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#0D1533"/><stop offset="1" stop-color="#2A2160"/></linearGradient>
    <linearGradient id="glass" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#fff" stop-opacity=".37"/><stop offset="1" stop-color="#fff" stop-opacity=".11"/></linearGradient>
  </defs>
  <rect width="100" height="100" rx="22" fill="url(#bg)"/>
  <polygon points="50,20 22,72 78,72" fill="url(#glass)"/>
  <g stroke-linecap="round" fill="none">
    <line x1="4" y1="57" x2="$($Enter[0])" y2="$($Enter[1])" stroke="#fff" stroke-opacity=".96" stroke-width="3.6"/>
    <line x1="$($Enter[0])" y1="$($Enter[1])" x2="$($Split[0])" y2="$($Split[1])" stroke="#fff" stroke-opacity=".55" stroke-width="2.6"/>
$($lines -join "`n")
  </g>
  <polygon points="50,20 22,72 78,72" fill="none" stroke="#fff" stroke-opacity=".92" stroke-width="3.2" stroke-linejoin="round"/>
</svg>
"@
[System.IO.File]::WriteAllText((Join-Path $OutDir 'prisma-logo.svg'), $svg, (New-Object System.Text.UTF8Encoding($false)))
Write-Host "Wrote $icoPath ($($sizes -join ', ') px), prisma-256.png and prisma-logo.svg in $OutDir"

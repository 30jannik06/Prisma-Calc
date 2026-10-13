# Draws assets\social-preview.png (1280 x 640, the size GitHub recommends for a repository social preview).
# Everything important stays inside an 80 px safe border. Uses the logo and one README screenshot.
# Windows PowerShell 5.1 + System.Drawing only. Upload the result under Settings > General > Social preview.
param([string]$Out = (Join-Path (Split-Path $PSScriptRoot -Parent) 'assets\social-preview.png'))
Set-StrictMode -Version 2
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = Split-Path $PSScriptRoot -Parent
$logoPath = Join-Path $root 'assets\prisma-256.png'
$shotPath = Join-Path $root 'docs\images\standard-aurora-dark.png'
foreach ($f in $logoPath, $shotPath) { if (-not (Test-Path $f)) { throw "Missing $f" } }

function Argb([int]$a, [string]$hex) {
    [System.Drawing.Color]::FromArgb($a, [Convert]::ToInt32($hex.Substring(0, 2), 16), [Convert]::ToInt32($hex.Substring(2, 2), 16), [Convert]::ToInt32($hex.Substring(4, 2), 16))
}
function RoundRect([single]$x, [single]$y, [single]$w, [single]$h, [single]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90); $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90); $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90); $p.CloseFigure()
    return $p
}
function Blob($g, [single]$cx, [single]$cy, [single]$radius, [string]$hex, [int]$alpha) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddEllipse($cx - $radius, $cy - $radius, $radius * 2, $radius * 2)
    $br = New-Object System.Drawing.Drawing2D.PathGradientBrush $path
    $br.CenterColor = Argb $alpha $hex
    $br.SurroundColors = [System.Drawing.Color[]]@((Argb 0 $hex))
    $g.FillPath($br, $path)
}

$W = 1280; $H = 640
$bmp = New-Object System.Drawing.Bitmap $W, $H, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = 'AntiAlias'; $g.InterpolationMode = 'HighQualityBicubic'; $g.PixelOffsetMode = 'HighQuality'; $g.TextRenderingHint = 'AntiAliasGridFit'

# background: same gradient and colour blobs as the Aurora theme
$bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush (New-Object System.Drawing.Rectangle 0, 0, $W, $H), (Argb 255 '0B1026'), (Argb 255 '2A2160'), 25.0
$g.FillRectangle($bg, 0, 0, $W, $H)
Blob $g 120 40 520 '00A8CC' 150
Blob $g 1180 640 560 'D63FB5' 130
Blob $g 760 230 420 '7A4DFF' 120

$white = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
$soft = New-Object System.Drawing.SolidBrush (Argb 215 'E4E7F5')
$fontTitle = New-Object System.Drawing.Font 'Segoe UI Semibold', 78, ([System.Drawing.FontStyle]::Regular), ([System.Drawing.GraphicsUnit]::Pixel)
$fontSub = New-Object System.Drawing.Font 'Segoe UI', 31, ([System.Drawing.FontStyle]::Regular), ([System.Drawing.GraphicsUnit]::Pixel)
$fontLine = New-Object System.Drawing.Font 'Segoe UI', 21, ([System.Drawing.FontStyle]::Regular), ([System.Drawing.GraphicsUnit]::Pixel)
$fontChip = New-Object System.Drawing.Font 'Segoe UI Semibold', 20, ([System.Drawing.FontStyle]::Regular), ([System.Drawing.GraphicsUnit]::Pixel)

# left column (x 80..620)
$logo = [System.Drawing.Image]::FromFile($logoPath)
$g.DrawImage($logo, 80, 92, 112, 112); $logo.Dispose()
$g.DrawString('Prisma', $fontTitle, $white, 74, 208)

# spectrum bar under the title (same five colours as the logo)
$cols = 'FF4D6D', 'FFB84D', '6CE08A', '4DC9FF', '9B6BFF'
for ($i = 0; $i -lt 5; $i++) {
    $b = New-Object System.Drawing.SolidBrush (Argb 255 $cols[$i])
    $g.FillPath($b, (RoundRect (82 + $i * 40) 316 34 6 3))
}
$g.DrawString('Liquid-glass calculator for Windows', $fontSub, $white, 80, 340)
$g.DrawString(('Scientific ' + [char]0x00B7 + ' Programmer ' + [char]0x00B7 + ' Graphing'), $fontLine, $soft, 80, 394)
$g.DrawString(('IPv4/IPv6 subnets ' + [char]0x00B7 + ' Unit converters'), $fontLine, $soft, 80, 428)

# chips
$chipX = 80
foreach ($t in 'No dependencies', 'No network access', 'MIT') {
    $size = $g.MeasureString($t, $fontChip)
    $cw = [single]($size.Width + 26)     # not $w: PowerShell variable names are case-insensitive ($W is the image width)
    $path = RoundRect $chipX 490 $cw 44 14
    $g.FillPath((New-Object System.Drawing.SolidBrush (Argb 40 'FFFFFF')), $path)
    $g.DrawPath((New-Object System.Drawing.Pen (Argb 110 'FFFFFF'), 1.5), $path)
    $g.DrawString($t, $fontChip, $white, $chipX + 11, 498)
    $chipX += $cw + 12
}

# right: screenshot in a rounded glass frame (x 640..1200, y 96..544)
$shot = [System.Drawing.Image]::FromFile($shotPath)
$sh = 448.0; $sw = [Math]::Round($shot.Width * $sh / $shot.Height); $sx = 1200 - $sw; $sy = 96
for ($i = 14; $i -ge 1; $i--) {            # soft shadow
    $g.FillPath((New-Object System.Drawing.SolidBrush (Argb 7 '000000')), (RoundRect ($sx - $i + 4) ($sy - $i + 14) ($sw + 2 * $i) ($sh + 2 * $i) (18 + $i)))
}
$clip = RoundRect $sx $sy $sw $sh 18
$state = $g.Save()
$g.SetClip($clip)
$g.DrawImage($shot, $sx, $sy, $sw, $sh)
$g.Restore($state)
$g.DrawPath((New-Object System.Drawing.Pen (Argb 140 'FFFFFF'), 2), $clip)
$shot.Dispose()

$g.Dispose()
New-Item -ItemType Directory -Force (Split-Path $Out) | Out-Null
$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
$kb = [int]((Get-Item $Out).Length / 1024)
Write-Host "Wrote $Out ($W x $H, $kb KB)"
if ($kb -gt 1000) { Write-Warning 'GitHub accepts social previews up to 1 MB.' }

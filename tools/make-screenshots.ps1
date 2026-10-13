# Refreshes the README screenshots in docs\images from the built program (dist\Prisma.exe).
# It starts Prisma several times with a throw-away settings file (your own settings are not touched), fills the
# views through UI Automation, photographs the window with PrintWindow and closes it again.
# Run build.ps1 first. Leave the mouse alone while it runs (about one minute).
Set-StrictMode -Version 2
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing, UIAutomationClient, UIAutomationTypes
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System; using System.Drawing; using System.Runtime.InteropServices;
public class Cap {
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int attr, out RECT r, int size);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
  // The window as Windows draws it, cropped to its visible frame (no invisible resize border, nothing from the desktop).
  public static Bitmap Grab(IntPtr h) {
    RECT w; GetWindowRect(h, out w);
    RECT v; if (DwmGetWindowAttribute(h, 9, out v, 16) != 0) v = w;
    var full = new Bitmap(w.R - w.L, w.B - w.T);
    using (var g = Graphics.FromImage(full)) { IntPtr dc = g.GetHdc(); PrintWindow(h, dc, 2); g.ReleaseHdc(dc); }
    var crop = new Rectangle(v.L - w.L, v.T - w.T, v.R - v.L, v.B - v.T);
    return full.Clone(crop, full.PixelFormat);
  }
}
'@
[void][Cap]::SetProcessDPIAware()

$root = Split-Path $PSScriptRoot -Parent
$exe = Join-Path $root 'dist\Prisma.exe'
if (-not (Test-Path $exe)) { Write-Error 'dist\Prisma.exe is missing. Run build.ps1 first.'; exit 1 }
$out = Join-Path $root 'docs\images'
$work = Join-Path $root '.tmp'
New-Item -ItemType Directory -Force $out, $work | Out-Null
$stamp = Get-Date -Format HHmmss
$DIV = [string][char]0x00F7; $MUL = [string][char]0x00D7

function Find-ByName($win, $name) {
    $c = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $name)
    $win.FindFirst('Descendants', $c)
}
function Click($win, $name) {
    $b = Find-ByName $win $name
    if (-not $b) { throw "button '$name' not found" }
    $b.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Start-Sleep -Milliseconds 120
}
function Edits($win) {
    $c = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Edit)
    @($win.FindAll('Descendants', $c))
}
function Shoot($file, $style, $theme, $mode, [scriptblock]$act) {
    $ini = Join-Path $work "shot-$stamp-$style-$mode.ini"
    Set-Content $ini "Style=$style`nGlass=1`nAnimate=0`nCorners=Soft" -Encoding ascii
    $p = Start-Process $exe -ArgumentList '--language', 'en', '--theme', $theme, '--mode', $mode, '--settings', $ini, '--log', (Join-Path $work "shot-$stamp.log") -PassThru
    try {
        Start-Sleep -Seconds 3
        $win = [System.Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle)
        if ($act) { & $act $win }
        Start-Sleep -Milliseconds 900
        [void][Cap]::SetForegroundWindow($p.MainWindowHandle); Start-Sleep -Milliseconds 300
        $bmp = [Cap]::Grab($p.MainWindowHandle)
        $path = Join-Path $out $file
        $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
        Write-Host ("{0}  {1}x{2}" -f $file, $bmp.Width, $bmp.Height)
        $bmp.Dispose()
    }
    finally { $p.CloseMainWindow() | Out-Null; [void]$p.WaitForExit(5000) }
}

Shoot 'standard-aurora-dark.png' 'Aurora' 'Dark' 'Standard' {
    param($w)
    foreach ($k in '1', '2', '+', '3', '4', '5', '=') { Click $w $k }
    foreach ($k in '9', $MUL, '8', '=') { Click $w $k }
    foreach ($k in '2', '0', '0', '+', '1', '0', '%', '=') { Click $w $k }
    Click $w 'History'
}
Shoot 'scientific-sunset-light.png' 'Sunset' 'Light' 'Scientific' {
    param($w)
    foreach ($k in 'sin', '3', '0', ')', '+', '7', $MUL, '2') { Click $w $k }
}
Shoot 'graphing-forest-dark.png' 'Forest' 'Dark' 'Graphing' {
    param($w)
    $e = Edits $w
    $vals = 'sin(x)', 'x^2/4-2', 'cos(2x)/2'
    for ($i = 0; $i -lt $vals.Count; $i++) { $e[$i].GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($vals[$i]); Start-Sleep -Milliseconds 200 }
    Click $w 'Zoom +'; Click $w 'Zoom +'
}
Shoot 'converter-graphite-light.png' 'Graphite' 'Light' 'Length' {
    param($w)
    $e = Edits $w
    $e[0].GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue('42.195')
}
Shoot 'settings-ocean-dark.png' 'Ocean' 'Dark' 'Settings' $null
Write-Host "Screenshots written to $out"

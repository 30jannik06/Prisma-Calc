# Checks the toolchain. This project has no downloaded dependencies.
$ErrorActionPreference = 'Stop'
$fw = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
foreach ($f in 'csc.exe', 'WPF\PresentationFramework.dll', 'WPF\PresentationCore.dll', 'WPF\WindowsBase.dll') {
    if (-not (Test-Path (Join-Path $fw $f))) { Write-Error "Missing $f in $fw. Windows needs the .NET Framework 4.x developer files."; exit 1 }
}
Write-Host "Toolchain OK: $fw (csc.exe + WPF). Nothing to download."

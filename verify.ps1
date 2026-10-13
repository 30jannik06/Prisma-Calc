# Checks that the files in a folder (default: dist) still match SHA256SUMS.txt. Changes nothing.
param([string]$Folder = (Join-Path $PSScriptRoot 'dist'))
Set-StrictMode -Version 3
$ErrorActionPreference = 'Stop'
$list = Join-Path $Folder 'SHA256SUMS.txt'
if (-not (Test-Path -LiteralPath $list)) { Write-Error "No SHA256SUMS.txt in $Folder"; exit 2 }
$bad = 0
foreach ($line in Get-Content -LiteralPath $list) {
    if ($line -notmatch '^([0-9a-f]{64}) \*(.+)$') { Write-Host "SKIP  malformed line: $line"; $bad++; continue }
    $want = $Matches[1]; $name = $Matches[2]
    $path = Join-Path $Folder $name
    if (-not (Test-Path -LiteralPath $path)) { Write-Host "MISSING $name"; $bad++; continue }
    $got = (Get-FileHash -Algorithm SHA256 -LiteralPath $path).Hash.ToLowerInvariant()
    if ($got -eq $want) { Write-Host "OK      $name" } else { Write-Host "CHANGED $name"; $bad++ }
}
if ($bad -gt 0) { Write-Host "$bad problem(s)."; exit 1 }
Write-Host 'All files match.'

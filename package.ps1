# Copies the program, README, licence and security notes into dist\, writes SHA256SUMS.txt and zips it (local only).
Set-StrictMode -Version 3
$ErrorActionPreference = 'Stop'
$d = Join-Path $PSScriptRoot 'dist'
if (-not (Test-Path (Join-Path $d 'Prisma.exe'))) { Write-Error 'Run build.ps1 first.'; exit 1 }
foreach ($f in 'README.md', 'LICENSE', 'SECURITY.md') { Copy-Item (Join-Path $PSScriptRoot $f) $d -Force }
foreach ($f in 'prisma.ico', 'prisma-logo.svg') { Copy-Item (Join-Path $PSScriptRoot "assets\$f") $d -Force }

$names = 'Prisma.exe', 'README.md', 'LICENSE', 'SECURITY.md', 'prisma.ico', 'prisma-logo.svg'
$sums = foreach ($n in $names) { $h = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $d $n)).Hash.ToLowerInvariant(); "$h *$n" }
Set-Content -LiteralPath (Join-Path $d 'SHA256SUMS.txt') -Value $sums -Encoding ascii

$zip = Join-Path $PSScriptRoot 'Prisma.zip'
if (Test-Path $zip) { Remove-Item -LiteralPath $zip }
$items = ($names + 'SHA256SUMS.txt') | ForEach-Object { Join-Path $d $_ }
Compress-Archive -LiteralPath $items -DestinationPath $zip
Write-Host "Packaged $zip"
Write-Host ("SHA-256 of the zip: " + (Get-FileHash -Algorithm SHA256 -LiteralPath $zip).Hash.ToLowerInvariant())

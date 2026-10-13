# Runs the self-tests (no window is shown, no account or device needed).
$ErrorActionPreference = 'Stop'
$exe = Join-Path $PSScriptRoot 'dist\Prisma.exe'
$outFile = Join-Path $PSScriptRoot 'dist\selftest-output.txt'
$p = Start-Process $exe -ArgumentList '--selftest' -Wait -PassThru -RedirectStandardOutput $outFile -WindowStyle Hidden
Get-Content $outFile
Write-Host "Exit code: $($p.ExitCode)"
exit $p.ExitCode

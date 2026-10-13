# Builds the test program from tests\*.cs and runs all self-tests.
# The tests live outside Prisma.exe on purpose: the shipped program carries no test code. The test program loads the
# real dist\Prisma.exe as a library, so it tests exactly the code and inspects exactly the file that is released.
# Run build.ps1 first. Output: build\tests\selftest-output.txt
Set-StrictMode -Version 3
$ErrorActionPreference = 'Stop'
$global:LASTEXITCODE = 0
$root = $PSScriptRoot

$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$env:TEMP = Join-Path $root '.tmp'; $env:TMP = $env:TEMP
New-Item -ItemType Directory -Force $env:TEMP | Out-Null

$prisma = Join-Path $root 'dist\Prisma.exe'
if (-not (Test-Path $prisma)) { Write-Error 'dist\Prisma.exe is missing. Run build.ps1 first.'; exit 1 }

$dir = Join-Path $root 'build\tests'
New-Item -ItemType Directory -Force $dir | Out-Null
$copy = Join-Path $dir 'Prisma.exe'
Copy-Item -LiteralPath $prisma -Destination $copy -Force
$h1 = (Get-FileHash -Algorithm SHA256 -LiteralPath $prisma).Hash
$h2 = (Get-FileHash -Algorithm SHA256 -LiteralPath $copy).Hash
if ($h1 -ne $h2) { Write-Error 'The test copy of Prisma.exe differs from dist\Prisma.exe.'; exit 1 }
Write-Host "Testing dist\Prisma.exe (SHA-256 $($h1.Substring(0, 16))...)"

$fw = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$wpf = Join-Path $fw 'WPF'
$testExe = Join-Path $dir 'Prisma.Tests.exe'
$sources = @(Get-ChildItem (Join-Path $root 'tests') -Filter *.cs | ForEach-Object { $_.FullName })
if ($sources.Count -lt 4) { Write-Error 'Test sources are missing.'; exit 1 }
if (Test-Path $testExe) { Remove-Item -LiteralPath $testExe -Force }
& (Join-Path $fw 'csc.exe') /nologo /target:exe /codepage:65001 /optimize+ /warn:4 /warnaserror+ "/out:$testExe" "/r:$copy" `
    "/r:$wpf\PresentationFramework.dll" "/r:$wpf\PresentationCore.dll" "/r:$wpf\WindowsBase.dll" `
    /r:System.Xaml.dll /r:System.dll /r:System.Core.dll `
    $sources
if ($LASTEXITCODE -or -not (Test-Path $testExe)) { Write-Error "Building the tests failed ($LASTEXITCODE)"; exit 1 }

$prev = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
$lines = & $testExe 2>&1
$code = $LASTEXITCODE
$ErrorActionPreference = $prev
$lines | Set-Content -LiteralPath (Join-Path $dir 'selftest-output.txt') -Encoding utf8
$lines | ForEach-Object { Write-Host $_ }
Write-Host "Exit code: $code"
exit $code

# Builds dist\Prisma.exe from src\*.cs with the csc.exe that ships with Windows.
# Hardened build: warnings are errors, ASLR/DEP/high-entropy ASLR on, manifest embedded (runs as the normal user).
Set-StrictMode -Version 3
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$global:LASTEXITCODE = 0

# Telemetry off, temp files inside this folder
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$env:TEMP = Join-Path $root '.tmp'; $env:TMP = $env:TEMP
New-Item -ItemType Directory -Force $env:TEMP, (Join-Path $root 'dist') | Out-Null

& (Join-Path $root 'fetch-deps.ps1'); if ($LASTEXITCODE) { exit $LASTEXITCODE }

$fw = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$wpf = Join-Path $fw 'WPF'
$out = Join-Path $root 'dist\Prisma.exe'
$manifest = Join-Path $root 'src\app.manifest'
$icon = Join-Path $root 'assets\prisma.ico'
$sources = @(Get-ChildItem (Join-Path $root 'src') -Filter *.cs | ForEach-Object { $_.FullName })
if (-not (Test-Path $manifest)) { Write-Error 'src\app.manifest is missing.'; exit 1 }
if (-not (Test-Path $icon)) { Write-Error 'assets\prisma.ico is missing. Run tools\make-icon.ps1.'; exit 1 }
if ($sources.Count -lt 5) { Write-Error 'Source files are missing.'; exit 1 }
if (Test-Path $out) { Remove-Item $out -Force }

& (Join-Path $fw 'csc.exe') /nologo /target:winexe /codepage:65001 /optimize+ /debug- /warn:4 /warnaserror+ /highentropyva+ "/win32manifest:$manifest" "/win32icon:$icon" "/resource:$icon,Prisma.ico" "/out:$out" `
    "/r:$wpf\PresentationFramework.dll" "/r:$wpf\PresentationCore.dll" "/r:$wpf\WindowsBase.dll" `
    /r:System.Xaml.dll /r:System.dll /r:System.Core.dll `
    $sources
if ($LASTEXITCODE -or -not (Test-Path $out)) { Write-Error "Build failed ($LASTEXITCODE)"; exit 1 }
Write-Host "Built $out"

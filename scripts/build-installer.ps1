param(
    [string]$Version = '1.0.0'
)

$ErrorActionPreference = 'Stop'

$dotnetRoot = 'C:\Users\Lenovo\AppData\Local\Microsoft\dotnet'
$dotnetExe = Join-Path $dotnetRoot 'dotnet.exe'
$isccCandidates = @(
    'C:\Program Files (x86)\Inno Setup 6\ISCC.exe',
    'C:\Program Files\Inno Setup 6\ISCC.exe',
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')
)
$iscc = $isccCandidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
$repoRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repoRoot 'SuratGenerator.sln'
$desktopProject = Join-Path $repoRoot 'SuratGenerator.Desktop\SuratGenerator.Desktop.csproj'
$publishDir = Join-Path $repoRoot 'artifacts\Release\SuratGenerator'
$issPath = Join-Path $repoRoot 'installer\SuratGenerator.iss'
$iconPath = Join-Path $repoRoot 'SuratGenerator.Desktop\Resources\Icons\SuratGenerator.ico'
$setupPath = Join-Path $repoRoot 'artifacts\SuratGenerator-Setup.exe'

$env:DOTNET_ROOT = $dotnetRoot
$env:DOTNET_ROOT_X64 = $dotnetRoot

function Invoke-Checked {
    param(
        [string]$FilePath,
        [string[]]$Arguments
    )

    Write-Host "Running: $FilePath $($Arguments -join ' ')"
    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code ${LASTEXITCODE}: $FilePath $($Arguments -join ' ')"
    }
}

if (-not (Test-Path -LiteralPath $dotnetExe -PathType Leaf)) {
    Write-Host "ERROR: dotnet.exe was not found at: $dotnetExe"
    exit 1
}
if (-not (Test-Path -LiteralPath $solution -PathType Leaf)) {
    Write-Host "ERROR: solution was not found at: $solution"
    exit 1
}
if (-not (Test-Path -LiteralPath $desktopProject -PathType Leaf)) {
    Write-Host "ERROR: desktop project was not found at: $desktopProject"
    exit 1
}
if (-not (Test-Path -LiteralPath $issPath -PathType Leaf)) {
    Write-Host "ERROR: Inno Setup script was not found at: $issPath"
    exit 1
}
if ([string]::IsNullOrWhiteSpace($iscc)) {
    Write-Host 'ERROR: Inno Setup 6 compiler was not found in any known location.'
    Write-Host 'Install Inno Setup 6 and run this script again.'
    exit 1
}

& (Join-Path $PSScriptRoot 'create-icon.ps1')
if (-not (Test-Path -LiteralPath $iconPath -PathType Leaf)) {
    throw "Icon was not created at: $iconPath"
}

Invoke-Checked -FilePath $dotnetExe -Arguments @('restore', $solution)
Invoke-Checked -FilePath $dotnetExe -Arguments @('build', $solution, '-c', 'Release', '-p:Platform=x64')

if (Test-Path -LiteralPath $publishDir) {
    Write-Host "Cleaning publish output: $publishDir"
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}
Invoke-Checked -FilePath $dotnetExe -Arguments @(
    'publish', $desktopProject,
    '-c', 'Release',
    '-f', 'net10.0-windows10.0.19041.0',
    '-r', 'win-x64',
    '--self-contained', 'true',
    '-p:PlatformTarget=x64',
    '-o', $publishDir
)

Invoke-Checked -FilePath $iscc -Arguments @("/DMyAppVersion=$Version", $issPath)

if (-not (Test-Path -LiteralPath $setupPath -PathType Leaf)) {
    throw "Setup executable was not found at: $setupPath"
}

$setupFile = Get-Item -LiteralPath $setupPath
Write-Host "Setup created: $($setupFile.FullName) ($($setupFile.Length) bytes)"

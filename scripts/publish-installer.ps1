<#
.SYNOPSIS
  Publish Blocks Plant (Desktop + API + Web) as self-contained win-x64 and build a Windows installer.

.DESCRIPTION
  1. dotnet publish all three apps into dist\publish\{Desktop,backend,web}
  2. If Inno Setup (ISCC) is available, compile installer\blocks-plant.iss → dist\BlocksPlant-Setup.exe
  3. Always also create dist\BlocksPlant-win-x64.zip and copy scripts\Install.ps1 for zip installs

.EXAMPLE
  .\scripts\publish-installer.ps1
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SkipInstaller,
    [switch]$SkipZip
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $Root

$PublishRoot = Join-Path $Root "dist\publish"
$DistDir = Join-Path $Root "dist"
$ZipPath = Join-Path $DistDir "BlocksPlant-win-x64.zip"

function Write-Step([string]$Message) {
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Find-ISCC {
    $candidates = @(
        (Get-Command iscc -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source),
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 5\ISCC.exe"
    ) | Where-Object { $_ -and (Test-Path $_) }
    return $candidates | Select-Object -First 1
}

Write-Step "Cleaning publish output"
if (Test-Path $PublishRoot) {
    Remove-Item $PublishRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $PublishRoot -Force | Out-Null
New-Item -ItemType Directory -Path $DistDir -Force | Out-Null

$projects = @(
    @{ Name = "Desktop"; Csproj = "desktop\BlocksPlant.Desktop.csproj"; Out = "Desktop" },
    @{ Name = "API";     Csproj = "backend\BlocksPlant.Api.csproj";     Out = "backend" },
    @{ Name = "Web";     Csproj = "web\BlocksPlant.Web.csproj";         Out = "web" }
)

foreach ($p in $projects) {
    $outDir = Join-Path $PublishRoot $p.Out
    Write-Step "Publishing $($p.Name) → $outDir"
    & dotnet publish $p.Csproj `
        -c $Configuration `
        -r $Runtime `
        --self-contained true `
        -p:PublishSingleFile=false `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -o $outDir
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed for $($p.Csproj) (exit $LASTEXITCODE)"
    }
}

# Ensure empty App_Data\logos exists for API uploads after install
$logosDir = Join-Path $PublishRoot "backend\App_Data\logos"
New-Item -ItemType Directory -Path $logosDir -Force | Out-Null
$keep = Join-Path $logosDir ".gitkeep"
if (-not (Test-Path $keep)) {
    Set-Content -Path $keep -Value ""
}

Write-Step "Copying Install.ps1 into publish root (for zip installs)"
Copy-Item (Join-Path $Root "scripts\Install.ps1") (Join-Path $PublishRoot "Install.ps1") -Force

if (-not $SkipZip) {
    Write-Step "Creating zip $ZipPath"
    if (Test-Path $ZipPath) { Remove-Item $ZipPath -Force }
    Compress-Archive -Path (Join-Path $PublishRoot "*") -DestinationPath $ZipPath -CompressionLevel Optimal
    Write-Host "Zip ready: $ZipPath"
}

$iscc = Find-ISCC
if ($SkipInstaller) {
    Write-Host "Skipping installer (-SkipInstaller)."
}
elseif ($iscc) {
    Write-Step "Building Inno Setup installer with $iscc"
    $iss = Join-Path $Root "installer\blocks-plant.iss"
    & $iscc $iss
    if ($LASTEXITCODE -ne 0) {
        throw "ISCC failed (exit $LASTEXITCODE)"
    }
    $setup = Join-Path $DistDir "BlocksPlant-Setup.exe"
    if (Test-Path $setup) {
        Write-Host "Installer ready: $setup" -ForegroundColor Green
    }
}
else {
    Write-Host ""
    Write-Host "Inno Setup (ISCC) not found - zip package only." -ForegroundColor Yellow
    Write-Host "Install Inno Setup 6, then re-run this script to produce dist\BlocksPlant-Setup.exe"
    Write-Host "  https://jrsoftware.org/isdl.php"
    Write-Host "  or: choco install innosetup -y"
}

Write-Step "Done"
Write-Host "Published layout: $PublishRoot"
Write-Host "  Desktop\BlocksPlant.Desktop.exe"
Write-Host "  backend\BlocksPlant.Api.exe"
Write-Host "  web\BlocksPlant.Web.exe"
if (Test-Path $ZipPath) { Write-Host "Zip: $ZipPath" }
$setupOut = Join-Path $DistDir "BlocksPlant-Setup.exe"
if (Test-Path $setupOut) { Write-Host "Setup: $setupOut" }

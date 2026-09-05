<#
.SYNOPSIS
  Install Blocks Plant from a published folder (or extracted zip) into Program Files.

.DESCRIPTION
  Copies Desktop, backend, and web siblings to:
    C:\Program Files\BlocksPlant\
      Desktop\
      backend\
      web\
  Creates Start Menu and Desktop shortcuts named "Blocks Plant".

  Run elevated (Administrator) for Program Files install.

.EXAMPLE
  # From extracted zip root (contains Desktop, backend, web):
  .\Install.ps1

  # Custom destination:
  .\Install.ps1 -InstallRoot "D:\Apps\BlocksPlant"
#>
[CmdletBinding()]
param(
    [string]$SourceRoot = $PSScriptRoot,
    [string]$InstallRoot = "${env:ProgramFiles}\BlocksPlant",
    [switch]$NoDesktopShortcut,
    [switch]$Launch
)

$ErrorActionPreference = "Stop"

function Test-IsAdmin {
    $id = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($id)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

# Allow running when copied next to publish folders, or from scripts\ against dist\publish
$desktopSrc = Join-Path $SourceRoot "Desktop"
$backendSrc = Join-Path $SourceRoot "backend"
$webSrc = Join-Path $SourceRoot "web"

if (-not (Test-Path (Join-Path $desktopSrc "BlocksPlant.Desktop.exe"))) {
    $alt = Join-Path (Split-Path -Parent $SourceRoot) "dist\publish"
    if (Test-Path (Join-Path $alt "Desktop\BlocksPlant.Desktop.exe")) {
        $SourceRoot = $alt
        $desktopSrc = Join-Path $SourceRoot "Desktop"
        $backendSrc = Join-Path $SourceRoot "backend"
        $webSrc = Join-Path $SourceRoot "web"
    }
}

foreach ($pair in @(
    @{ Path = $desktopSrc; Label = "Desktop\BlocksPlant.Desktop.exe" },
    @{ Path = (Join-Path $backendSrc "BlocksPlant.Api.exe"); Label = "backend\BlocksPlant.Api.exe" },
    @{ Path = (Join-Path $webSrc "BlocksPlant.Web.exe"); Label = "web\BlocksPlant.Web.exe" }
)) {
    $check = if ($pair.Path.EndsWith(".exe")) { $pair.Path } else { Join-Path $pair.Path "BlocksPlant.Desktop.exe" }
    if (-not (Test-Path $check)) {
        throw "Missing published files at '$SourceRoot'. Expected $($pair.Label). Run scripts\publish-installer.ps1 first."
    }
}

if ($InstallRoot -like "${env:ProgramFiles}*" -and -not (Test-IsAdmin)) {
    throw "Installing to Program Files requires an elevated PowerShell (Run as Administrator)."
}

Write-Host "Installing Blocks Plant to $InstallRoot" -ForegroundColor Cyan
New-Item -ItemType Directory -Path $InstallRoot -Force | Out-Null

$dirs = @(
    @{ Src = $desktopSrc; Dest = "Desktop" },
    @{ Src = $backendSrc; Dest = "backend" },
    @{ Src = $webSrc; Dest = "web" }
)

foreach ($d in $dirs) {
    $dest = Join-Path $InstallRoot $d.Dest
    Write-Host "  Copying $($d.Dest)…"
    if (Test-Path $dest) {
        Remove-Item $dest -Recurse -Force
    }
    Copy-Item $d.Src $dest -Recurse -Force
}

$exe = Join-Path $InstallRoot "Desktop\BlocksPlant.Desktop.exe"
if (-not (Test-Path $exe)) {
    throw "Install failed - Desktop exe missing at $exe"
}

function New-Shortcut([string]$ShortcutPath, [string]$TargetPath, [string]$WorkingDir) {
    $wsh = New-Object -ComObject WScript.Shell
    $sc = $wsh.CreateShortcut($ShortcutPath)
    $sc.TargetPath = $TargetPath
    $sc.WorkingDirectory = $WorkingDir
    $sc.Description = "Blocks Plant POS"
    $sc.Save()
}

$startMenu = Join-Path $env:ProgramData "Microsoft\Windows\Start Menu\Programs\Blocks Plant"
New-Item -ItemType Directory -Path $startMenu -Force | Out-Null
New-Shortcut (Join-Path $startMenu "Blocks Plant.lnk") $exe (Join-Path $InstallRoot "Desktop")

if (-not $NoDesktopShortcut) {
    $desk = [Environment]::GetFolderPath("CommonDesktopDirectory")
    if (-not (Test-IsAdmin)) {
        $desk = [Environment]::GetFolderPath("Desktop")
    }
    New-Shortcut (Join-Path $desk "Blocks Plant.lnk") $exe (Join-Path $InstallRoot "Desktop")
}

Write-Host ""
Write-Host "Installed successfully." -ForegroundColor Green
Write-Host "  App:      $exe"
Write-Host "  API:      $(Join-Path $InstallRoot 'backend\BlocksPlant.Api.exe')"
Write-Host "  Web:      $(Join-Path $InstallRoot 'web\BlocksPlant.Web.exe')"
Write-Host ""
Write-Host "First run: sign in as owner / owner123 (change the password after)."
Write-Host "Local ports: API http://localhost:5118  Web http://localhost:5137"
Write-Host "If another PC on the LAN needs the API, allow inbound TCP 5118 in Windows Firewall."

if ($Launch) {
    Start-Process -FilePath $exe -WorkingDirectory (Join-Path $InstallRoot "Desktop")
}

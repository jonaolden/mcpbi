#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Package Tabular MCP Server releases for distribution

.DESCRIPTION
    This script creates distribution packages from the published builds,
    including ZIP files and proper directory structures for easy deployment.

.PARAMETER OutputPath
    The output directory for packaged releases (default: ./releases)

.PARAMETER Version
    Version string to include in package names (default: auto-detected from assembly)

.PARAMETER IncludeSource
    Include source code in the release packages

.EXAMPLE
    .\scripts\package-releases.ps1
    
.EXAMPLE
    .\scripts\package-releases.ps1 -OutputPath "C:\Releases" -Version "1.0.0"
#>

param(
    [string]$OutputPath = "./releases",
    [string]$Version = "",
    [switch]$IncludeSource = $false
)

# Set error action preference
$ErrorActionPreference = "Stop"

Write-Host "=== Tabular MCP Server Release Packager ===" -ForegroundColor Cyan

# Function to get version from assembly
function Get-AssemblyVersion {
    $assemblyPath = "./pbi-local-mcp/bin/Release/net8.0/pbi-local-mcp.dll"
    if (Test-Path $assemblyPath) {
        $assembly = [System.Reflection.Assembly]::LoadFile((Resolve-Path $assemblyPath).Path)
        return $assembly.GetName().Version.ToString()
    }
    return "1.0.0.0"
}

# Auto-detect version if not provided
if (-not $Version) {
    try {
        $Version = Get-AssemblyVersion
        Write-Host "Auto-detected version: $Version" -ForegroundColor Green
    }
    catch {
        $Version = "1.0.0"
        Write-Host "Could not detect version, using default: $Version" -ForegroundColor Yellow
    }
}

# Clean version string (remove .0 if present at end)
$CleanVersion = $Version -replace '\.0$', ''

# Create output directory
if (-not (Test-Path $OutputPath)) {
    New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
    Write-Host "Created output directory: $OutputPath" -ForegroundColor Green
}

# Define single release configuration
$packageName = "mcpbi-v$CleanVersion"
$packagePath = Join-Path $OutputPath $packageName
$zipPath = "$packagePath.zip"
$sourcePath = "./publish/single-file"

Write-Host "`nPackaging MCPBI Release..." -ForegroundColor Yellow

# Check if source path exists
if (-not (Test-Path $sourcePath)) {
    Write-Error "Source path not found: $sourcePath"
    exit 1
}

# Create package directory
if (Test-Path $packagePath) {
    Remove-Item $packagePath -Recurse -Force
}
New-Item -ItemType Directory -Path $packagePath -Force | Out-Null

# Copy published files
$appPath = Join-Path $packagePath "app"
New-Item -ItemType Directory -Path $appPath -Force | Out-Null

# Copy and rename main executable
$sourceMcpbiPath = Join-Path $sourcePath "pbi-local-mcp.exe"
$destMcpbiPath = Join-Path $appPath "mcpbi.exe"
if (Test-Path $sourceMcpbiPath) {
    Copy-Item $sourceMcpbiPath $destMcpbiPath -Force
    Write-Host "  Copied mcpbi.exe" -ForegroundColor Green
} else {
    Write-Error "Main executable not found: $sourceMcpbiPath"
    exit 1
}

# Copy CLI discovery tool
$sourceCliPath = Join-Path $sourcePath "pbi-local-mcp.DiscoverCli.exe"
$destCliPath = Join-Path $appPath "pbi-local-mcp.DiscoverCli.exe"
if (Test-Path $sourceCliPath) {
    Copy-Item $sourceCliPath $destCliPath -Force
    Write-Host "  Copied pbi-local-mcp.DiscoverCli.exe" -ForegroundColor Green
} else {
    Write-Warning "Discovery CLI not found: $sourceCliPath"
}
    
# Create documentation structure
$docsPath = Join-Path $packagePath "docs"
New-Item -ItemType Directory -Path $docsPath -Force | Out-Null

# Copy documentation files
$docFiles = @("README.md", "DEPLOYMENT.md")
foreach ($docFile in $docFiles) {
    if (Test-Path $docFile) {
        $destPath = Join-Path $docsPath (Split-Path $docFile -Leaf)
        Copy-Item $docFile $destPath -Force
        Write-Host "  Copied $docFile" -ForegroundColor Gray
    }
}

# Create package-specific README
$packageReadme = @"
# MCPBI - Power BI Tabular MCP Server

Version: $Version
Build: Single-file, self-contained executables

## Description
Complete portable package with .NET runtime included. No additional dependencies required.

## Quick Start Option A: Automatic Discovery

1. **Configure Power BI Connection:**
   ``````cmd
   cd app
   pbi-local-mcp.DiscoverCli.exe
   ``````
   Follow the prompts to detect your Power BI instance and create the `.env` file.

2. **Configure VS Code MCP Integration:**
   Add to your `mcp.json`:
   ``````json
   {
     "mcpServers": {
       "mcpbi-dev": {
         "command": "C:\\path\\to\\app\\mcpbi.exe",
         "args": []
       }
     }
   }
   ``````

## Quick Start Option B: Manual Port Configuration

If you already know your Power BI port (visible in Tabular Editor):

``````json
{
  "mcpServers": {
    "mcpbi-dev": {
      "command": "C:\\path\\to\\app\\mcpbi.exe",
      "args": ["--port", "12345"]
    }
  }
}
``````

## Files Included

- **mcpbi.exe** - Main MCP server (single executable, ~150MB)
- **pbi-local-mcp.DiscoverCli.exe** - Power BI discovery tool
- **docs/** - Complete documentation

## Requirements

- Windows 10/11
- Power BI Desktop with a PBIX file open
- VS Code with MCP support

No .NET installation required - runtime is included.

## Documentation

- See `docs/DEPLOYMENT.md` for detailed installation
- See `docs/README.md` for general project information

## Support

For issues and questions, please refer to the project documentation.

Package created: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
"@

$packageReadme | Out-File -FilePath (Join-Path $packagePath "README.txt") -Encoding UTF8
    
# Create installation script for Windows
$installScript = @"
@echo off
echo === MCPBI - Power BI Tabular MCP Server Installation ===
echo.
echo This script will help you set up the MCPBI server.
echo.
echo Step 1: Ensure Power BI Desktop is running with a PBIX file open
pause
echo.
echo Step 2: Running Power BI discovery...
cd /d "%~dp0app"
pbi-local-mcp.DiscoverCli.exe
echo.
if %ERRORLEVEL% EQU 0 (
    echo Setup completed successfully!
    echo.
    echo Next steps:
    echo 1. Copy this path to your mcp.json: %~dp0app\mcpbi.exe
    echo 2. Restart VS Code
    echo 3. See docs\DEPLOYMENT.md for detailed instructions
) else (
    echo Setup failed. Please check:
    echo - Power BI Desktop is running
    echo - A PBIX file is open
    echo - No firewall is blocking the connection
)
echo.
pause
"@

$installScript | Out-File -FilePath (Join-Path $packagePath "install.bat") -Encoding ASCII
    
# Create PowerShell installation script
$psInstallScript = @"
#!/usr/bin/env pwsh
Write-Host "=== MCPBI - Power BI Tabular MCP Server Installation ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "This script will help you set up the MCPBI server." -ForegroundColor Green
Write-Host ""

Write-Host "Step 1: Checking prerequisites..." -ForegroundColor Yellow

# Check if Power BI Desktop processes are running
`$pbiProcesses = Get-Process -Name "PBIDesktop" -ErrorAction SilentlyContinue
if (`$pbiProcesses.Count -eq 0) {
    Write-Host "WARNING: Power BI Desktop is not running!" -ForegroundColor Red
    Write-Host "Please start Power BI Desktop and open a PBIX file, then run this script again." -ForegroundColor Yellow
    Read-Host "Press Enter to exit"
    exit 1
} else {
    Write-Host "Found `$(`$pbiProcesses.Count) Power BI Desktop instance(s) running." -ForegroundColor Green
}

Write-Host ""
Write-Host "Step 2: Running Power BI discovery..." -ForegroundColor Yellow

# Change to app directory and run discovery
Set-Location "`$PSScriptRoot\app"
& ".\pbi-local-mcp.DiscoverCli.exe"

if (`$LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "Setup completed successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Yellow
    Write-Host "1. Copy this path to your mcp.json: `$PSScriptRoot\app\mcpbi.exe" -ForegroundColor White
    Write-Host "2. Restart VS Code" -ForegroundColor White
    Write-Host "3. See docs\DEPLOYMENT.md for detailed instructions" -ForegroundColor White
} else {
    Write-Host ""
    Write-Host "Setup failed. Please check:" -ForegroundColor Red
    Write-Host "- Power BI Desktop is running" -ForegroundColor Yellow
    Write-Host "- A PBIX file is open" -ForegroundColor Yellow
    Write-Host "- No firewall is blocking the connection" -ForegroundColor Yellow
}

Write-Host ""
Read-Host "Press Enter to continue"
"@

$psInstallScript | Out-File -FilePath (Join-Path $packagePath "install.ps1") -Encoding UTF8

# Include source code if requested
if ($IncludeSource) {
    Write-Host "  Including source code..." -ForegroundColor Cyan
    $sourcePath = Join-Path $packagePath "source"
    New-Item -ItemType Directory -Path $sourcePath -Force | Out-Null
    
    # Copy source files (excluding build artifacts and packages)
    $sourceItems = @(
        "pbi-local-mcp",
        "pbi-local-mcp.DiscoverCli",
        "*.sln",
        "*.md",
        ".gitignore"
    )
    
    foreach ($item in $sourceItems) {
        if (Test-Path $item) {
            Copy-Item $item (Join-Path $sourcePath (Split-Path $item -Leaf)) -Recurse -Force
        }
    }
}

# Create ZIP package
Write-Host "`nCreating ZIP archive..." -ForegroundColor Cyan
if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

Compress-Archive -Path "$packagePath\*" -DestinationPath $zipPath -CompressionLevel Optimal

# Calculate package size
$zipSize = [math]::Round((Get-Item $zipPath).Length / 1MB, 2)
$folderSize = [math]::Round((Get-ChildItem $packagePath -Recurse | Measure-Object -Property Length -Sum).Sum / 1MB, 2)

Write-Host "Package created: $zipPath" -ForegroundColor Green
Write-Host "ZIP size: $zipSize MB, Extracted size: $folderSize MB" -ForegroundColor Gray

# Copy executables directly to Releases directory for easy access
Write-Host "`nCopying executables to Releases directory..." -ForegroundColor Cyan

$directMcpbiPath = Join-Path $OutputPath "mcpbi.exe"
if (Test-Path $destMcpbiPath) {
    Copy-Item $destMcpbiPath $directMcpbiPath -Force
    Write-Host "  Copied mcpbi.exe" -ForegroundColor Green
}

$directCliPath = Join-Path $OutputPath "pbi-local-mcp.DiscoverCli.exe"
if (Test-Path $destCliPath) {
    Copy-Item $destCliPath $directCliPath -Force
    Write-Host "  Copied pbi-local-mcp.DiscoverCli.exe" -ForegroundColor Green
}

# Create release info
$releaseInfo = @"
# MCPBI - Power BI Tabular MCP Server Release v$CleanVersion

Generated: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")

## Quick Access Files

For immediate use without extraction:
- **mcpbi.exe** - Main MCP server (~150MB, single-file executable)
- **pbi-local-mcp.DiscoverCli.exe** - Power BI discovery tool

## Release Package

- **File**: mcpbi-v$CleanVersion.zip ($zipSize MB)
- **Type**: Single-file, self-contained executables
- **Includes**: Complete .NET runtime (no installation required)

## Installation

### Quick Start
1. Extract `mcpbi-v$CleanVersion.zip` to your preferred location
2. Run `install.bat` or `install.ps1` for guided setup
3. Add the executable path to your VS Code `mcp.json`

### Alternative: Direct Use
Use the standalone executables directly from the releases directory.

## Package Contents

- `app/mcpbi.exe` - Main MCP server
- `app/pbi-local-mcp.DiscoverCli.exe` - Discovery tool
- `docs/` - Complete documentation
- `install.bat` - Windows batch installation script
- `install.ps1` - PowerShell installation script
- `README.txt` - Quick start instructions

## Requirements

- Windows 10/11
- Power BI Desktop with a PBIX file open
- Visual Studio Code with MCP support

**No .NET installation required** - Runtime is included in the executables.

## Support

See the included documentation for detailed instructions:
- `docs/DEPLOYMENT.md` - Complete deployment guide
- `docs/README.md` - Project overview
- `README.txt` - Quick start guide

## Build Information

- Build Type: Single-file, self-contained
- Runtime: .NET 8.0 (included)
- Platform: Windows x64
- Compression: Optimal
"@

$releaseInfo | Out-File -FilePath (Join-Path $OutputPath "RELEASE-INFO.md") -Encoding UTF8

Write-Host "`n=== Release Packaging Complete ===" -ForegroundColor Green
Write-Host "Output directory: $OutputPath" -ForegroundColor Cyan
Write-Host "Release package: $zipPath" -ForegroundColor Cyan
Write-Host "Release info: $(Join-Path $OutputPath "RELEASE-INFO.md")" -ForegroundColor Cyan

Write-Host "`nDirect access executables:" -ForegroundColor Yellow
Write-Host "  mcpbi.exe" -ForegroundColor White
Write-Host "  pbi-local-mcp.DiscoverCli.exe" -ForegroundColor White

Write-Host "`nPackaging completed successfully!" -ForegroundColor Green
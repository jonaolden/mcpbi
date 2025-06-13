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

# Define release configurations
$configurations = @(
    @{
        Name = "win-x64-selfcontained"
        DisplayName = "Windows x64 Self-Contained"
        SourcePath = "./publish/win-x64"
        Description = "Complete package with .NET runtime included"
    },
    @{
        Name = "win-x64-framework-dependent"
        DisplayName = "Windows x64 Framework-Dependent"
        SourcePath = "./publish/win-x64-framework-dependent"
        Description = "Requires .NET 8.0 Runtime on target machine"
    },
    @{
        Name = "win-x64-portable"
        DisplayName = "Windows x64 Portable (Single File)"
        SourcePath = "./publish/win-x64-single-file"
        Description = "Single executable file, easiest distribution"
    }
)

# Package each configuration
foreach ($config in $configurations) {
    $packageName = "tabular-mcp-server-$($config.Name)-v$CleanVersion"
    $packagePath = Join-Path $OutputPath $packageName
    $zipPath = "$packagePath.zip"
    
    Write-Host "`nPackaging $($config.DisplayName)..." -ForegroundColor Yellow
    
    # Check if source path exists
    if (-not (Test-Path $config.SourcePath)) {
        Write-Warning "Source path not found: $($config.SourcePath). Skipping..."
        continue
    }
    
    # Create package directory
    if (Test-Path $packagePath) {
        Remove-Item $packagePath -Recurse -Force
    }
    New-Item -ItemType Directory -Path $packagePath -Force | Out-Null
    
    # Copy published files
    $appPath = Join-Path $packagePath "app"
    Copy-Item $config.SourcePath $appPath -Recurse -Force
    
    # Create documentation structure
    $docsPath = Join-Path $packagePath "docs"
    New-Item -ItemType Directory -Path $docsPath -Force | Out-Null
    
    # Copy documentation files
    $docFiles = @("README.md", "DEPLOYMENT.md", "docs/Installation.md")
    foreach ($docFile in $docFiles) {
        if (Test-Path $docFile) {
            $destPath = Join-Path $docsPath (Split-Path $docFile -Leaf)
            Copy-Item $docFile $destPath -Force
        }
    }
    
    # Create package-specific README
    $packageReadme = @"
# Tabular MCP Server - $($config.DisplayName)

Version: $Version
Package: $($config.Name)

## Description
$($config.Description)

## Quick Start

1. **Setup Power BI Connection**
   ``````
   cd app
   pbi-local-mcp.exe discover-pbi
   ``````

2. **Configure VS Code**
   Add to your `.vscode/mcp.json`:
   ``````json
   {
     "servers": {
       "tabular-mcp": {
         "type": "stdio",
         "command": "[PATH_TO_PACKAGE]/app/pbi-local-mcp.exe",
         "envFile": "[PATH_TO_PACKAGE]/app/.env"
       }
     }
   }
   ``````

3. **Test Connection**
   - Restart VS Code
   - Open a workspace with MCP configuration
   - The server should be available for Power BI operations

## Documentation
- See `docs/DEPLOYMENT.md` for detailed installation instructions
- See `docs/Installation.md` for prerequisites and setup
- See `docs/README.md` for general project information

## Support
For issues and questions, please refer to the project documentation or create an issue in the project repository.

Package created: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
"@
    
    $packageReadme | Out-File -FilePath (Join-Path $packagePath "README.txt") -Encoding UTF8
    
    # Create installation script for Windows
    $installScript = @"
@echo off
echo === Tabular MCP Server Installation ===
echo.
echo This script will help you set up the Tabular MCP Server.
echo.
echo Step 1: Ensure Power BI Desktop is running with a PBIX file open
pause
echo.
echo Step 2: Running Power BI discovery...
cd /d "%~dp0app"
pbi-local-mcp.exe discover-pbi
echo.
if %ERRORLEVEL% EQU 0 (
    echo Setup completed successfully!
    echo.
    echo Next steps:
    echo 1. Copy the path: %~dp0app\pbi-local-mcp.exe
    echo 2. Add MCP configuration to VS Code
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
Write-Host "=== Tabular MCP Server Installation ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "This script will help you set up the Tabular MCP Server." -ForegroundColor Green
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
& ".\pbi-local-mcp.exe" discover-pbi

if (`$LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "Setup completed successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Yellow
    Write-Host "1. Copy this path: `$PSScriptRoot\app\pbi-local-mcp.exe" -ForegroundColor White
    Write-Host "2. Add MCP configuration to VS Code (.vscode/mcp.json)" -ForegroundColor White
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
    Write-Host "  Creating ZIP archive..." -ForegroundColor Cyan
    if (Test-Path $zipPath) {
        Remove-Item $zipPath -Force
    }
    
    Compress-Archive -Path "$packagePath\*" -DestinationPath $zipPath -CompressionLevel Optimal
    
    # Calculate package size
    $zipSize = [math]::Round((Get-Item $zipPath).Length / 1MB, 2)
    $folderSize = [math]::Round((Get-ChildItem $packagePath -Recurse | Measure-Object -Property Length -Sum).Sum / 1MB, 2)
    
    Write-Host "  Package created: $zipPath" -ForegroundColor Green
    Write-Host "  ZIP size: $zipSize MB, Extracted size: $folderSize MB" -ForegroundColor Gray
}

# Create combined release info
$releaseInfo = @"
# Tabular MCP Server Release v$CleanVersion

Generated: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")

## Available Packages

"@

foreach ($config in $configurations) {
    $packageName = "tabular-mcp-server-$($config.Name)-v$CleanVersion"
    $zipPath = Join-Path $OutputPath "$packageName.zip"
    
    if (Test-Path $zipPath) {
        $zipSize = [math]::Round((Get-Item $zipPath).Length / 1MB, 2)
        $releaseInfo += @"

### $($config.DisplayName)
- **File**: `$packageName.zip` ($zipSize MB)
- **Description**: $($config.Description)
- **Best for**: $(
    switch ($config.Name) {
        "win-x64-selfcontained" { "Users without .NET 8.0 installed" }
        "win-x64-framework-dependent" { "Development environments with .NET 8.0" }
        "win-x64-portable" { "Simple distribution and deployment" }
    }
)

"@
    }
}

$releaseInfo += @"

## Installation

1. Download the appropriate package for your needs
2. Extract the ZIP file to your preferred location
3. Run `install.bat` (Windows) or `install.ps1` (PowerShell) for guided setup
4. Follow the instructions in `docs/DEPLOYMENT.md` for VS Code configuration

## Package Contents

Each package contains:
- `/app/` - The compiled application and dependencies
- `/docs/` - Complete documentation
- `install.bat` - Windows batch installation script
- `install.ps1` - PowerShell installation script
- `README.txt` - Package-specific instructions

## Requirements

- Windows 10/11
- Power BI Desktop
- Visual Studio Code with MCP support
$(if (-not $configurations[1]) { "" } else { "- .NET 8.0 Runtime (for framework-dependent package only)" })

## Support

For detailed installation and configuration instructions, see the documentation files included in each package.
"@

$releaseInfo | Out-File -FilePath (Join-Path $OutputPath "RELEASE-INFO.md") -Encoding UTF8

Write-Host "`n=== Release Packaging Complete ===" -ForegroundColor Green
Write-Host "Output directory: $OutputPath" -ForegroundColor Cyan
Write-Host "Release info: $(Join-Path $OutputPath "RELEASE-INFO.md")" -ForegroundColor Cyan

# List created packages
Write-Host "`nCreated packages:" -ForegroundColor Yellow
Get-ChildItem $OutputPath -Filter "*.zip" | ForEach-Object {
    $size = [math]::Round($_.Length / 1MB, 2)
    Write-Host "  $($_.Name) ($size MB)" -ForegroundColor White
}

Write-Host "`nPackaging completed successfully!" -ForegroundColor Green
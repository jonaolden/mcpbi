# Tabular MCP Server Release v1.0.0

Generated: 2025-06-12 19:43:30

## Available Packages

### Windows x64 Self-Contained
- **File**: $packageName.zip (37.1 MB)
- **Description**: Complete package with .NET runtime included
- **Best for**: Users without .NET 8.0 installed

### Windows x64 Framework-Dependent
- **File**: $packageName.zip (5.49 MB)
- **Description**: Requires .NET 8.0 Runtime on target machine
- **Best for**: Development environments with .NET 8.0

### Windows x64 Portable (Single File)
- **File**: $packageName.zip (33.21 MB)
- **Description**: Single executable file, easiest distribution
- **Best for**: Simple distribution and deployment

## Installation

1. Download the appropriate package for your needs
2. Extract the ZIP file to your preferred location
3. Run install.bat (Windows) or install.ps1 (PowerShell) for guided setup
4. Follow the instructions in docs/DEPLOYMENT.md for VS Code configuration

## Package Contents

Each package contains:
- /app/ - The compiled application and dependencies
- /docs/ - Complete documentation
- install.bat - Windows batch installation script
- install.ps1 - PowerShell installation script
- README.txt - Package-specific instructions

## Requirements

- Windows 10/11
- Power BI Desktop
- Visual Studio Code with MCP support
- .NET 8.0 Runtime (for framework-dependent package only)

## Support

For detailed installation and configuration instructions, see the documentation files included in each package.

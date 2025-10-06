# Tabular MCP Server - Deployment Guide

This guide provides comprehensive instructions for deploying the Tabular MCP Server.

## Quick Start with Pre-built Executable (Recommended)

The fastest way to get started is using the pre-built executable from the `Releases/` directory:

### Prerequisites
- Windows OS (Windows 10/11 recommended)
- Power BI Desktop with a PBIX file open
- Visual Studio Code with MCP extension support

### Quick Start Steps
1. Ensure Power BI Desktop is running with a PBIX file open
2. Run `Releases\pbi-local-mcp.DiscoverCli.exe` to set up Power BI connection
3. Configure VS Code with the MCP configuration (see Installation section below)

## Development Setup

For development and testing, you can run the server directly from source code:

### Prerequisites
- .NET 8.0 SDK installed
- Power BI Desktop with a PBIX file open
- Visual Studio Code with MCP extension support

### Quick Start from Source
1. Clone this repository
2. Run `dotnet run --project pbi-local-mcp/pbi-local-mcp.csproj discover-pbi` to set up Power BI connection
3. Configure VS Code with the provided MCP configuration (see Installation section below)

## Creating Distribution Builds

To create deployable builds for distribution, use the automated publish script:

```cmd
# Run the complete publish process (builds and packages everything)
scripts\publish.bat
```

This creates:
- Single-file, self-contained executables (no .NET installation required)
- Complete release package with documentation
- Ready-to-distribute ZIP file

**Manual build (if needed):**
```cmd
# MCP Server
dotnet publish pbi-local-mcp/pbi-local-mcp.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o ./publish/single-file

# Discovery CLI
dotnet publish pbi-local-mcp.DiscoverCli/pbi-local-mcp.DiscoverCli.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o ./publish/single-file
```

## Installation Instructions

### Prerequisites
- Windows OS (Windows 10/11 recommended)
- .NET 8.0 SDK (for development only - **not required for pre-built executables**)
- Power BI Desktop installed and running with a PBIX file open
- Visual Studio Code with MCP support

### Step 1: Choose Your Setup Method

#### Option A: Development Setup (Recommended for Contributors)
1. Clone this repository to your preferred location
2. Follow the setup instructions in [`docs/Installation.md`](docs/Installation.md)

#### Option B: Pre-built Executable Deployment (Recommended for End Users)
1. Run `scripts\publish.bat` or download a release package
2. Extract to your target location (e.g., `C:\Tools\tabular-mcp\`)
3. Run `install.bat` for guided setup
4. Note the path to `mcpbi.exe` for VS Code configuration

### Step 2: Configure Power BI Connection
1. Open Command Prompt or PowerShell in your project/deployment folder
2. Run the discovery command:
   ```cmd
   # For development setup
   dotnet run --project pbi-local-mcp/pbi-local-mcp.csproj discover-pbi
   
   # For pre-built executable (recommended)
   Releases\pbi-local-mcp.DiscoverCli.exe
   ```
3. Follow the prompts to select your Power BI instance and database
4. This will create a `.env` file with your connection settings

### Step 3: Configure VS Code MCP Integration
Create or update your VS Code MCP configuration file (`.vscode/mcp.json`):

#### For Pre-built Executable (Recommended for End Users):
```json
{
  "mcpServers": {
    "mcpbi-dev": {
      "command": "C:\\Tools\\tabular-mcp\\app\\mcpbi.exe",
      "args": []
    }
  }
}
```

#### For Development Setup (Contributors):
```json
{
  "mcpServers": {
    "mcpbi-dev": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "${workspaceFolder}/pbi-local-mcp/pbi-local-mcp.csproj"
      ],
      "env": {
        "DOTNET_ENVIRONMENT": "Development"
      }
    }
  }
}
```

### Step 4: Test the Installation
1. Restart VS Code
2. Open a workspace that includes the MCP configuration
3. The Tabular MCP Server should now be available in VS Code's MCP-enabled features

## Environment Configuration

The server uses environment variables for configuration. These are typically set via a `.env` file:

```env
PBI_PORT=your_powerbi_port
PBI_DB_ID=your_database_id
```

You can also set these manually if needed:
- `PBI_PORT`: The port number where Power BI Desktop is listening (usually auto-detected)
- `PBI_DB_ID`: The database ID of your Power BI model (usually auto-detected)

## Command Line Options

The server supports several command line arguments:

```cmd
# Pre-built executables
pbi-local-mcp.DiscoverCli.exe                           # Discovery tool
mcpbi.exe --port 12345                                  # Start with specific port
mcpbi.exe --db-id "your-database-id"                    # Start with specific DB ID
mcpbi.exe --help                                        # Show help

# Development setup
dotnet run --project pbi-local-mcp/pbi-local-mcp.csproj -- --port 12345
dotnet run --project pbi-local-mcp/pbi-local-mcp.csproj -- --db-id "your-database-id"
dotnet run --project pbi-local-mcp/pbi-local-mcp.csproj -- --help
```

## Troubleshooting

### Common Issues

1. **Connection Refused Error**
   - Ensure Power BI Desktop is running with a PBIX file open
   - Check that the port and database ID are correct
   - Try running the discovery command again

2. **Permission Errors**
   - Run VS Code or the command prompt as Administrator
   - Check that Windows Firewall isn't blocking the connection

3. **Missing Dependencies**
   - Pre-built executables include all dependencies (no .NET installation required)
   - For development, ensure .NET 8.0 SDK is installed
   - Download SDK from: https://dotnet.microsoft.com/download/dotnet/8.0

4. **MCP Server Not Found in VS Code**
   - Verify the path in `.vscode/mcp.json` is correct
   - Ensure the executable has proper permissions
   - Check VS Code's MCP extension is installed and enabled

### Logging and Diagnostics

The server provides detailed logging. To enable verbose logging:
1. Set environment variable: `DOTNET_LOG_LEVEL=Debug`
2. Check the console output for detailed error messages

## Distribution

### Release Packages

The `releases/` directory contains pre-built executables ready for distribution:

- `mcpbi.exe` - Main MCP server (single-file, ~150MB)
- `pbi-local-mcp.DiscoverCli.exe` - Discovery CLI tool
- `mcpbi-v{version}.zip` - Complete package with documentation and installation scripts

### Creating Installation Packages

The automated publish script creates a ready-to-distribute ZIP package:

1. **ZIP Package**: Run `scripts\publish.bat` to create `releases\mcpbi-v{version}.zip`
2. **Portable Package**: Extract the ZIP to any location - no installation required
3. **Custom Installer**: Use the ZIP contents with tools like Inno Setup or WiX for MSI creation

### Version Management

Build information is managed through:
- Project file version properties in [`pbi-local-mcp.csproj`](pbi-local-mcp/pbi-local-mcp.csproj)
- Use semantic versioning for releases
- Include version in package names for clarity

## Security Considerations

- The server only accepts connections from localhost
- DAX query validation is performed to prevent malicious queries
- Connection is read-only to the Power BI model
- No sensitive data is logged or stored

## Performance Notes

- **Development setup**: Fast compilation and startup for development/testing
- **Pre-built executables**: Single-file, self-contained with all dependencies
  - Startup: ~2-3 seconds (extraction overhead)
  - Disk space: ~150MB per executable
  - Deployment: Easiest - just copy one .exe file
  - No runtime installation required

## Next Steps

After successful deployment:
1. Explore the available MCP tools for Power BI analysis
2. Review the documentation for advanced configuration options
3. Consider automation scripts for bulk deployments
4. Set up monitoring and logging for production environments
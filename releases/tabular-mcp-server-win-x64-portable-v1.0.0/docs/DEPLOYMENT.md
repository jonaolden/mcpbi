# Tabular MCP Server - Deployment Guide

This guide provides comprehensive instructions for deploying the Tabular MCP Server in different environments.

## Published Builds

The project has been published in three different configurations:

### 1. Self-Contained (Recommended for Distribution)
- **Location**: `./publish/win-x64/`
- **Description**: Includes the .NET runtime, no external dependencies required
- **Size**: ~150MB
- **Use Case**: Distribution to users who may not have .NET 8.0 installed

### 2. Framework-Dependent
- **Location**: `./publish/win-x64-framework-dependent/`
- **Description**: Requires .NET 8.0 Runtime to be installed on target machine
- **Size**: ~50MB
- **Use Case**: Development environments or where .NET 8.0 is already installed

### 3. Single-File Executable (Easiest Distribution)
- **Location**: `./publish/win-x64-single-file/`
- **Description**: Everything packaged into a single executable file
- **Size**: ~150MB (single file)
- **Use Case**: Simplest distribution method, just copy one .exe file

## Quick Start

### Option 1: Single-File Executable (Recommended)
1. Copy the entire `./publish/win-x64-single-file/` folder to your target location
2. Run `pbi-local-mcp.exe discover-pbi` to set up Power BI connection
3. Configure VS Code with the provided MCP configuration

### Option 2: Self-Contained
1. Copy the entire `./publish/win-x64/` folder to your target location
2. Run `pbi-local-mcp.exe discover-pbi` to set up Power BI connection
3. Configure VS Code with the provided MCP configuration

### Option 3: Framework-Dependent
1. Ensure .NET 8.0 Runtime is installed on target machine
2. Copy the entire `./publish/win-x64-framework-dependent/` folder to your target location
3. Run `pbi-local-mcp.exe discover-pbi` to set up Power BI connection
4. Configure VS Code with the provided MCP configuration

## Installation Instructions

### Prerequisites
- Windows OS (Windows 10/11 recommended)
- Power BI Desktop installed and running with a PBIX file open
- Visual Studio Code with MCP extension support

### Step 1: Choose Your Deployment Method
Select one of the published builds based on your requirements:
- **Single-file**: Best for simple distribution
- **Self-contained**: Best for users without .NET 8.0
- **Framework-dependent**: Smallest size if .NET 8.0 is available

### Step 2: Deploy the Application
1. Create a folder for the MCP server (e.g., `C:\Tools\tabular-mcp\`)
2. Copy the contents of your chosen publish folder to this location
3. Note the path to the executable for VS Code configuration

### Step 3: Configure Power BI Connection
1. Open Command Prompt or PowerShell in the deployment folder
2. Run the discovery command:
   ```cmd
   pbi-local-mcp.exe discover-pbi
   ```
3. Follow the prompts to select your Power BI instance and database
4. This will create a `.env` file with your connection settings

### Step 4: Configure VS Code MCP Integration
Create or update your VS Code MCP configuration file (`.vscode/mcp.json`):

#### For Single-File Deployment:
```json
{
  "servers": {
    "tabular-mcp": {
      "type": "stdio",
      "command": "C:\\Tools\\tabular-mcp\\pbi-local-mcp.exe",
      "envFile": "C:\\Tools\\tabular-mcp\\.env"
    }
  }
}
```

#### For Other Deployments:
```json
{
  "servers": {
    "tabular-mcp": {
      "type": "stdio",
      "command": "C:\\Tools\\tabular-mcp\\pbi-local-mcp.exe",
      "envFile": "C:\\Tools\\tabular-mcp\\.env"
    }
  }
}
```

### Step 5: Test the Installation
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
# Discover Power BI instances and configure connection
pbi-local-mcp.exe discover-pbi

# Run with specific port (overrides .env file)
pbi-local-mcp.exe --port 12345

# Run with specific database ID (overrides .env file)
pbi-local-mcp.exe --db-id "your-database-id"

# Show help
pbi-local-mcp.exe --help
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
   - For framework-dependent builds, ensure .NET 8.0 Runtime is installed
   - Download from: https://dotnet.microsoft.com/download/dotnet/8.0

4. **MCP Server Not Found in VS Code**
   - Verify the path in `.vscode/mcp.json` is correct
   - Ensure the executable has proper permissions
   - Check VS Code's MCP extension is installed and enabled

### Logging and Diagnostics

The server provides detailed logging. To enable verbose logging:
1. Set environment variable: `DOTNET_LOG_LEVEL=Debug`
2. Check the console output for detailed error messages

## Distribution

### Creating Installation Packages

For distribution to end users, consider creating:

1. **ZIP Package**: Compress the chosen publish folder
2. **Installer**: Use tools like Inno Setup or WiX to create an MSI installer
3. **Portable Package**: Use the single-file build for maximum portability

### Version Management

Each build includes version information:
- Check the `.xml` documentation file for version details
- Use semantic versioning for releases
- Include version in package names for clarity

## Security Considerations

- The server only accepts connections from localhost
- DAX query validation is performed to prevent malicious queries
- Connection is read-only to the Power BI model
- No sensitive data is logged or stored

## Performance Notes

- **Single-file builds**: Slightly slower startup due to extraction overhead
- **Self-contained builds**: Fastest startup, larger disk footprint
- **Framework-dependent builds**: Fastest startup, smallest footprint with .NET installed

## Next Steps

After successful deployment:
1. Explore the available MCP tools for Power BI analysis
2. Review the documentation for advanced configuration options
3. Consider automation scripts for bulk deployments
4. Set up monitoring and logging for production environments
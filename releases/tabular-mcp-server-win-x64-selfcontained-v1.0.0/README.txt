# Tabular MCP Server - Windows x64 Self-Contained

Version: 1.0.0.0
Package: win-x64-selfcontained

## Description
Complete package with .NET runtime included

## Quick Start

1. **Setup Power BI Connection**
   ```
   cd app
   pbi-local-mcp.exe discover-pbi
   ```

2. **Configure VS Code**
   Add to your .vscode/mcp.json:
   ```json
   {
     "servers": {
       "tabular-mcp": {
         "type": "stdio",
         "command": "[PATH_TO_PACKAGE]/app/pbi-local-mcp.exe",
         "envFile": "[PATH_TO_PACKAGE]/app/.env"
       }
     }
   }
   ```

3. **Test Connection**
   - Restart VS Code
   - Open a workspace with MCP configuration
   - The server should be available for Power BI operations

## Documentation
- See docs/DEPLOYMENT.md for detailed installation instructions
- See docs/Installation.md for prerequisites and setup
- See docs/README.md for general project information

## Support
For issues and questions, please refer to the project documentation or create an issue in the project repository.

Package created: 2025-06-12 19:42:35

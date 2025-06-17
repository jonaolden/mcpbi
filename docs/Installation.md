# Installation

## Requirements

- Power BI Desktop (with a PBIX file open for discovery)
- Windows OS
- Visual Studio Code (for MCP server integration)
- [.NET 8.0 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (for development setup only)

## Quick Installation (Recommended)

### Option A: Pre-built Executable
The fastest way to get started:

1. **Run Power BI Discovery** to detect Power BI Desktop and populate the `.env` file:
   ```cmd
   Releases\pbi-local-mcp.DiscoverCli.exe
   ```
   Follow prompts to select the instance and database. This writes `PBI_PORT` and `PBI_DB_ID` to `.env` in the project root.

2. **Configure MCP server in VS Code:**
   Add a `.vscode/mcp.json` file:
   ```json
   {
     "mcpServers": {
       "MCPBI": {
         "command": "Releases/mcpbi.exe",
         "args": [],
         "disabled": false,
         "alwaysAllow": []
       }
     }
   }
   ```
   This automatically loads the `.env` file and starts the MCP server for VS Code integration.

### Option B: Development Setup
For development and testing:

**Additional Requirements:**
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

1. **Clone the repository** and open it in your terminal.
2. **Run InstanceDiscovery** to detect Power BI Desktop and populate the `.env` file:
   ```sh
   dotnet run --project pbi-local-mcp/pbi-local-mcp.csproj discover-pbi
   ```
   Follow prompts to select the instance and database. This writes `PBI_PORT` and `PBI_DB_ID` to `.env` in the project root.

3. **Configure MCP server in VS Code:**
   Add a `.vscode/mcp.json` file:
   ```json
   {
     "servers": {
       "tabular-mcp": {
         "type": "stdio",
         "command": "dotnet",
         "envFile": "${workspaceFolder}/.env",
         "args": [
           "run",
           "--project",
           "${workspaceFolder}/pbi-local-mcp/pbi-local-mcp.csproj"
         ]
       }
     }
   }
   ```
   This loads the `.env` file and starts the MCP server for VS Code integration.

## Manual Setup

**If you already know your port and database ID:**
Create `.env` in the project root with:
```
PBI_PORT=<your_pbi_instance_port>
PBI_DB_ID=<your_pbi_database_id>
```
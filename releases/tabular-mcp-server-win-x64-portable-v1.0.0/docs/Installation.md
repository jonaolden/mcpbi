# Installation

## Requirements

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Power BI Desktop (with a PBIX file open for discovery)
- Windows OS
- Visual Studio Code (for MCP server integration)

## Installation

1. **Clone the repository** and open it in your terminal.
2. **Run InstanceDiscovery** to detect Power BI Desktop and populate the `.env` file:
   ```sh
   dotnet run --project pbi-local-mcp/pbi-local-mcp.csproj discover-pbi
   ```
   Follow prompts to select the instance and database. This writes `PBI_PORT` and `PBI_DB_ID` to `.env` in the project root.

 **OR: Manual setup (if you already know your port and db id):**  
   Create `pbi-local-mcp/.env` with:
   ```
   PBI_PORT=<your_pbi_instance_port>
   PBI_DB_ID=<your_pbi_database_id>
   ```

4. **Configure MCP server in VS Code:**  
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
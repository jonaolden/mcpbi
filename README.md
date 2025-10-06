# Tabular MCP Server *AKA MCPBI*
This is a Model Context Protocol (MCP) server for locally running Tabular Models, i.e. PowerBI models running on PowerBI Desktop. 

This server allows MCP-enabled LLM clients to communicate with your tabular models and help you debug, analyse and compose DAX queries. 

*Example: Copilot querying Tabular Model via MCP*

# How it works 

It connects to a local running instance of Tabular models using the [AdomdConnection in ADOMD.NET](https://learn.microsoft.com/en-us/analysis-services/adomd/multidimensional-models-adomd-net-client/connections-in-adomd-net?view=asallproducts-allversions). 

Using this connection, the server then allows clients to execute [DAX-queries](https://www.sqlbi.com/articles/execute-dax-queries-through-ole-db-and-adomd-net/) and retrieve model metadata (using DMV queries) through pre-defined tools for high accuracy, as well as custom DAX queries for debugging and development.

This MCP server enables communication between clients and Power BI tabular models via ADOMD.NET, supporting both predefined metadata queries and flexible DAX queries with full DEFINE block capabilities for advanced analysis.

## Tools

### ListMeasures
List all measures in the model with essential information (name, table, data type, visibility), optionally filtered by table name. Use GetMeasureDetails for full DAX expressions.

### GetMeasureDetails
Get details for a specific measure by name.

### ListTables
List all tables in the model.

### GetTableDetails
Get details for a specific table by name.

### GetTableColumns
Get columns for a specific table by name.

### GetTableRelationships
Get relationships for a specific table by name.

### PreviewTableData
Preview data from a table (top N rows).

### RunQuery
Execute a DAX query. Supports complete DAX queries with DEFINE blocks, EVALUATE statements, or simple expressions. **Enhanced with structured error responses** - returns detailed error information instead of throwing exceptions for better MCP protocol compatibility.

### ValidateDaxSyntax
Validate DAX syntax and identify potential issues with enhanced error analysis.

### AnalyzeQueryPerformance
Analyze query performance characteristics and identify potential bottlenecks.

## Installation

### Quick Start with Prebuilt Binaries (Recommended)

The fastest way to get started is using prebuilt executables - no .NET installation required:

1. **Download the latest release** from the `Releases/` directory or GitHub releases page
2. **Extract to your preferred location** (e.g., `C:\Tools\tabular-mcp\`)
3. **Run Power BI Discovery** to configure connection:
   ```cmd
   Releases\pbi-local-mcp.DiscoverCli.exe
   ```
   Follow prompts to detect your Power BI instance and create the `.env` file.

4. **Configure VS Code MCP** by adding to `.vscode/mcp.json`:
   ```json
   {
     "mcpServers": {
       "mcpbi": {
         "command": "C:\\path\\to\\Releases\\mcpbi.exe",
         "args": []
       }
     }
   }
   ```

5. **Restart VS Code** - the Tabular MCP Server is now ready to use!

### Development Setup from Source

For contributors and advanced users who want to build from source:

See [`docs/setup.md`](docs/setup.md) for detailed requirements and installation instructions.

**Quick Start:**
```sh
git clone <repository-url>
cd tabular-mcp
dotnet build
dotnet run --project pbi-local-mcp/pbi-local-mcp.csproj discover-pbi
```

For complete deployment options and troubleshooting, see [`DEPLOYMENT.md`](DEPLOYMENT.md).

## License
MIT
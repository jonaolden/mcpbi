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

## 🆕 Latest Improvements

### Enhanced Error Handling System
- **Structured Error Responses**: All DAX query errors now return structured JSON objects instead of throwing exceptions, ensuring better compatibility with MCP protocol
- **Intelligent Error Classification**: Automatically categorizes errors (Syntax Error, Column Reference Error, Table Reference Error, Connection Error, etc.)
- **Contextual Suggestions**: Provides specific troubleshooting recommendations based on error type and query content
- **Detailed Error Messages**: Includes comprehensive diagnostics with query information, error classification, and step-by-step suggestions

### Improved MCP Protocol Compatibility  
- **Logging Optimization**: All console output redirected to stderr to prevent interference with JSON-RPC communication
- **Better Serialization**: Error responses designed for seamless JSON serialization through MCP protocol
- **Enhanced Debugging**: Detailed error information helps developers quickly identify and resolve DAX query issues

## How to install
See [`docs/Installation.md`](docs/Installation.md) for requirements and installation instructions.

### Quick Start a) with Pre-built Executables
For the fastest setup, you can use the pre-built executable from the `Releases` section.

1. **Configure Power BI Connection:**
   ```cmd
   Releases\pbi-local-mcp.DiscoverCli.exe
   ```
   Follow the prompts to detect your Power BI instance and create the `.env` file.

2. **Configure VS Code MCP Integration:**
   Configure `mcp.json` with:
   ```json
  {
    "mcpServers": {
      "MCPBI": {
        "command": "C:\\dir\\to\\mcpbi.exe",
        "args": []
      }
    }
  }
   ```

### Quick Start b) with port as argument
Or if you already know which port you are running PowerBI Tabular model on (visible from Tabular Editor for instance)

   Configure `mcp.json` with:
   ```json
  {
    "mcpServers": {
      "MCPBI": {
        "command": "C:\\dir\\to\\mcpbi.exe",
        "args": ["--port","12345"]
      }
    }
  }
   ```



## Testing
See [`resources/testing.md`](resources/testing.md)

## License
MIT
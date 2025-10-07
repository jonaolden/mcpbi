# MCPBI - Tabular Model MCP Server
This is a Model Context Protocol (MCP) server for locally running Tabular Models, i.e. PowerBI models running on PowerBI Desktop. 

This server allows MCP-enabled LLM clients to communicate with your tabular models and help you debug, analyse and compose DAX queries. 

*Example: Copilot querying Tabular Model via MCP*

![roocodedemo](https://github.com/user-attachments/assets/5dd8b5b3-0d30-4876-b8c9-87233f336e6f)

# How it works 

It connects to a local running instance of Tabular models using the [AdomdConnection in ADOMD.NET](https://learn.microsoft.com/en-us/analysis-services/adomd/multidimensional-models-adomd-net-client/connections-in-adomd-net?view=asallproducts-allversions). 

Using this connection, the server then allows clients to execute [DAX-queries](https://www.sqlbi.com/articles/execute-dax-queries-through-ole-db-and-adomd-net/) and retrieve model metadata (using DMV queries) through pre-defined tools for high accuracy, as well as custom DAX queries for debugging and development.

This MCP server enables communication between clients and Power BI tabular models via ADOMD.NET, supporting both predefined metadata queries and flexible DAX queries with full DEFINE block capabilities for advanced analysis.

## Tools

MCPBI provides 10 tools that enable LLM clients to explore, analyze, and debug your Power BI models:

### Model Exploration Tools

**ListTables**
- Lists all tables in your model with metadata (storage mode, visibility, lineage tags)
- **Use case:** Quickly discover available tables when starting development or understanding model structure

**GetTableDetails**
- Retrieves detailed metadata for a specific table
- **Use case:** Understand table properties, expressions, and configuration before writing DAX

**GetTableColumns**
- Lists all columns in a table with data types, formats, and properties
- **Use case:** Identify available columns and their characteristics when building measures or queries

**GetTableRelationships**
- Shows all relationships connected to a table (cardinality, filter direction, active status)
- **Use case:** Understand data model connections for correct DAX context and filter propagation

### Measure Management Tools

**ListMeasures**
- Lists all measures with names, tables, data types, and visibility (optionally filtered by table)
- **Use case:** Discover existing measures to avoid duplication or find patterns to follow

**GetMeasureDetails**
- Retrieves complete measure definition including full DAX expression
- **Use case:** Study existing measure logic, troubleshoot calculations, or prepare for refactoring

### Data Exploration Tools

**PreviewTableData**
- Returns top N rows from a table
- **Use case:** Verify data structure, check sample values, or validate data loads during development

### DAX Development Tools

**RunQuery**
- Executes DAX queries including complete DEFINE blocks, EVALUATE statements, or simple expressions
- Returns structured results or detailed error information for MCP compatibility
- **Use case:** Test measures, debug calculations, analyze data, or validate query results before implementing in reports

**ValidateDaxSyntax**
- Validates DAX expressions and provides complexity metrics
- Returns syntax errors, warnings, and recommendations for best practices
- **Use case:** Catch syntax errors early, get improvement suggestions, and assess expression complexity before deployment

**AnalyzeQueryPerformance**
- Analyzes query execution time and performance characteristics
- Provides performance ratings, complexity factors, and optimization suggestions
- **Use case:** Identify slow queries, understand bottlenecks, and optimize DAX for better report performance

## How These Tools Help with Power BI Development

### 1. **Model Discovery**
Use [`ListTables`], [`GetTableColumns`], and [`GetTableRelationships`] to quickly understand an unfamiliar model's structure without manually clicking through Power BI Desktop.

### 2. **DAX Assistance**
LLM clients can use [`ListMeasures`] and [`GetMeasureDetails`] to learn your existing DAX patterns and suggest consistent new measures that follow your naming conventions and calculation styles.

### 3. **Debugging**
Combine [`RunQuery`] with [`ValidateDaxSyntax`] to iteratively test and refine DAX expressions with immediate feedback on syntax and results.

### 4. **Performance Optimization**
Use [`AnalyzeQueryPerformance`] to identify slow queries, then iterate improvements with [`RunQuery`] to verify performance gains.

## Installation
# Setup Instructions

## Requirements

- Power BI Desktop (with a PBIX file open for discovery)
- Windows OS
- Visual Studio Code (for MCP server integration)
- [.NET 8.0 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)

### Setup from Prebuilt Release (Recommended)

1. **Download the release** from the [Releases](releases/) directory or GitHub releases page and extract to your preferred location.

2. **Open Power BI Desktop** with a PBIX file you want to work with.

3. **Get PowerBI instance information** 

There are several ways to detect Power BI Desktop instances.
The easiest is to open Tabular Editor and check the port in the connection string.

<img width="317" height="63" alt="Tabular Editor" src="https://github.com/user-attachments/assets/eca039c7-5ca4-4fc6-a957-7684a971a01e" />

   
Simply add the port (63717 in example above) to your MCP server configuration in the next step (you can ignore the database ID, as this server connects to the default model).   
If you don't have Tabular Editor, you can use the included discovery tool to find the running instance and database.

   Open PowerShell, navigate to the release directory, and run:
   ```powershell
   cd path\to\release
   .\pbi-local-mcp.DiscoverCli.exe
   ```
   
   **Note**: In PowerShell, you must use `.\` prefix to run executables from the current directory.
   
   Follow the prompts to:
   - Select the Power BI Desktop instance (identified by port)
   - Choose the database/model to connect to
   
   This creates a `.env` file with `PBI_PORT` and `PBI_DB_ID` in the release directory, which you can reference in your MCP configuration or ignore if you specify the port directly.

4. **Configure MCP server** in your editor. For VS Code with Roo, create/edit `.roo/mcp.json`:
   ```json
   {
     "mcpServers": {
       "MCPBI": {
         "type": "stdio",
         "command": "path\\to\\release\\mcpbi.exe",
         "cwd": "path\\to\\release",
         "args": ["--port", "YOUR_PBI_PORT"],
         "disabled": false,
         "alwaysAllow": [
           "ListTables",
           "GetTableDetails",
           "GetTableColumns",
           "GetTableRelationships",
           "ListMeasures",
           "GetMeasureDetails",
           "PreviewTableData",
           "RunQuery",
           "ValidateDaxSyntax",
           "AnalyzeQueryPerformance"
         ]
       }
     }
   }
   ```
   Replace `path\\to\\release` with your actual release directory path and `YOUR_PBI_PORT` with the port number from PBI instance.


### Setup from Source (For Development)

1. **Clone the repository**:
   ```sh
   git clone <repository-url>
   cd tabular-mcp
   ```

2. **Build the project**:
   ```sh
   dotnet build
   ```

3. **Open Power BI Desktop** with a PBIX file.

4. **Run discovery** to create `.env` file:
   ```sh
   dotnet run --project pbi-local-mcp/pbi-local-mcp.csproj discover-pbi
   ```
   Follow prompts to select instance and database.

5. **Configure MCP server** in `.roo/mcp.json`:
   ```json
   {
     "mcpServers": {
       "mcpbi-dev": {
         "type": "stdio",
         "command": "dotnet",
         "cwd": "path\\to\\tabular-mcp",
         "envFile": "path\\to\\tabular-mcp\\.env",
         "args": [
           "exec",
           "path\\to\\tabular-mcp\\pbi-local-mcp\\bin\\Debug\\net8.0\\pbi-local-mcp.dll"
         ],
         "disabled": false,
         "alwaysAllow": [
           "ListTables",
           "GetTableDetails",
           "GetTableColumns",
           "GetTableRelationships",
           "ListMeasures",
           "GetMeasureDetails",
           "PreviewTableData",
           "RunQuery",
           "ValidateDaxSyntax",
           "AnalyzeQueryPerformance"
         ]
       }
     }
   }
   ```
   Replace `path\\to\\tabular-mcp` with your actual repository path.


### Configuration Notes
- **Use either port or envFile**: You can specify the Power BI port directly in `args` or use `envFile` to load from `.env`.
- **Port argument**: The `--port` argument in the release configuration connects to the specific Power BI Desktop instance on that port
- **envFile**: The development setup uses `envFile` to automatically load `PBI_PORT` and `PBI_DB_ID` from `.env`
- **alwaysAllow**: Lists all tools that can be used without requiring user approval for each invocation
- **Working directory**: The `cwd` parameter sets the working directory where the `.env` file is located

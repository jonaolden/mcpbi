# Tabular MCP

This project provides a local Model Context Protocol (MCP) implementation for Power BI tabular models, enabling tools to interact with Power BI Desktop instances.

## Features

- **MCP Server:** Implements the Model Context Protocol for communication with compatible clients.
- **Power BI Instance Discovery:** A utility to find running Power BI Desktop instances and their connection details.
- **DAX Querying:** Supports various DAX queries for schema, metadata, and data retrieval (e.g., `INFO.*` functions, `EVALUATE`).
- **Structured Error Handling:** Provides detailed error messages and logs issues to `stderr`.
- **Configuration via `.env` file:** Simplifies setting up the connection to Power BI.

## Project Structure

- [`pbi-local-mcp/`](pbi-local-mcp/) - Main .NET project directory.
  - [`pbi-local-mcp/pbi-local-mcp.csproj`](pbi-local-mcp/pbi-local-mcp.csproj) - Project file.
  - [`pbi-local-mcp/Program.cs`](pbi-local-mcp/Program.cs) - Main program entry point and MCP tool definitions.
  - [`pbi-local-mcp/TabularService.cs`](pbi-local-mcp/TabularService.cs) - Service class for ADOMD.NET interactions.
  - [`pbi-local-mcp/PbiInstanceDiscovery.cs`](pbi-local-mcp/PbiInstanceDiscovery.cs) - Logic for discovering PBI instances.
- [`pbi-local-mcp/pbi-local-mcp.Tests/`](pbi-local-mcp/pbi-local-mcp.Tests/) - Unit tests for the project.
- [`resources/`](resources/) - Supporting resources, including Python scripts and documentation.

## Getting Started

1.  **Clone the repository.**
2.  **Ensure Requirements are met.** (See below)
3.  **Build the project:**
    ```sh
    dotnet build tabular-mcp.sln
    ```
4.  **(Optional) Discover Power BI Instance:**
    If you have Power BI Desktop running with a PBIX file open, you can use the discovery tool to automatically create a `.env` file with the correct port and database ID.
    ```sh
    dotnet run --project pbi-local-mcp/pbi-local-mcp.csproj discover-pbi
    ```
    Follow the on-screen prompts. This will create/update a `.env` file in the root of the `pbi-local-mcp` project directory.
5.  **Configure `.env` file (if not using discovery):**
    Create a file named `.env` in the `pbi-local-mcp` project directory (e.g., `pbi-local-mcp/.env`) with the following content:
    ```env
    PBI_PORT=<your_pbi_instance_port>
    PBI_DB_ID=<your_pbi_database_id>
    ```
    Replace `<your_pbi_instance_port>` and `<your_pbi_database_id>` with the actual values from your running Power BI Desktop instance. You can find these using the Python script in `resources/discover_pbix.py` or other diagnostic tools.
6.  **Run the MCP server:**
    ```sh
    dotnet run --project pbi-local-mcp/pbi-local-mcp.csproj
    ```
    The server will start and listen for MCP client connections via stdin/stdout.

## Configuration

The MCP server requires the following environment variables to be set, typically via a `.env` file in the `pbi-local-mcp` project directory:

- `PBI_PORT`: The port number of the local Power BI Desktop Analysis Services instance.
- `PBI_DB_ID`: The database ID (GUID) of the Power BI model.

The `discover-pbi` command can help automate the creation of this file.

## Requirements

- [.NET 7.0 SDK](https://dotnet.microsoft.com/download/dotnet/7.0)
- Power BI Desktop (required for the server to connect to a model).
- For the `discover-pbi` feature to work, Power BI Desktop must be running with a PBIX file open.

## Development

- Source code is primarily in the `pbi-local-mcp` directory.
- The solution file is `tabular-mcp.sln`.
- Build artifacts are generated in `bin/` and `obj/` subdirectories within each project.
- The project uses `/warnaserror` for builds.

## License

Specify your license here.

## Retrieving .env Variables

Running the discovery tool writes `PBI_PORT` and `PBI_DB_ID` into the `.env` file in the workspace root.

Use the following CLI command:
```
dotnet run --project pbi-local-mcp/pbi-local-mcp.csproj --no-build --no-restore -- RunInteractive
```
(or simply `dotnet run --project pbi-local-mcp/pbi-local-mcp.csproj`).

The tool will prompt you to select a Power BI instance and database, then write:
```
PBI_PORT=<port>
PBI_DB_ID=<database Id>
```
into `.env` in the workspace root.

## Configuring the MCP Server in VS Code

To configure the MCP server, add a `.vscode/mcp.json` file (or similar) with the following content:

```json
{
  "servers": {
    "MCPBI": {
      "type": "stdio",
      "command": "dotnet",
      "cwd": "${workspaceFolder}",
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

This setup loads the `.env` file to supply `PBI_PORT` and `PBI_DB_ID` to the server.

## Running Tests

To run all unit tests:

```sh
dotnet test tabular-mcp.sln
```

## License

Specify your license here.
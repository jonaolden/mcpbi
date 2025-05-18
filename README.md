# Tabular MCP

This project provides a local Model Context Protocol (MCP) implementation for Power BI tabular models, enabling tools to interact with Power BI Desktop instances.

## Features

- **MCP Server:** Implements the Model Context Protocol for communication with compatible clients.
- **Power BI Instance Discovery:** Utility to find running Power BI Desktop instances and their connection details.
- **DAX Querying:** Supports DAX queries for schema, metadata, and data retrieval (e.g., `INFO.*` functions, `EVALUATE`).
- **Structured Error Handling:** Provides detailed error messages and logs issues to `stderr`.
- **Configuration via `.env` file:** Simplifies setting up the connection to Power BI.

## Available Tools

- **InstanceDiscovery:** Finds running Power BI Desktop instances and writes connection info to `.env`.
- **MCP Server:** Main server that communicates with MCP clients via stdin/stdout.
- **DAX Query Tool:** Executes DAX queries against the connected Power BI model.
- **Error Logging:** Captures and logs errors for troubleshooting.

Each tool is invoked via CLI or as part of the MCP server workflow. See [`docs/Installation.md`](docs/Installation.md) for setup and usage.

## Project Structure

- [`pbi-local-mcp/`](pbi-local-mcp/) - Main .NET project directory.
  - [`pbi-local-mcp/pbi-local-mcp.csproj`](pbi-local-mcp/pbi-local-mcp.csproj) - Project file.
  - [`pbi-local-mcp/Program.cs`](pbi-local-mcp/Program.cs) - Main program entry point and MCP tool definitions.
  - [`pbi-local-mcp/TabularService.cs`](pbi-local-mcp/TabularService.cs) - Service class for ADOMD.NET interactions.
  - [`pbi-local-mcp/PbiInstanceDiscovery.cs`](pbi-local-mcp/PbiInstanceDiscovery.cs) - Logic for discovering PBI instances.
- [`pbi-local-mcp/pbi-local-mcp.Tests/`](pbi-local-mcp/pbi-local-mcp.Tests/) - Unit tests for the project.
- [`resources/`](resources/) - Supporting resources, including Python scripts and documentation.

## Getting Started

See [`docs/Installation.md`](docs/Installation.md) for requirements and installation instructions.

## Configuration

The MCP server requires the following environment variables, typically set via a `.env` file in the `pbi-local-mcp` directory:

- `PBI_PORT`: The port number of the local Power BI Desktop Analysis Services instance.
- `PBI_DB_ID`: The database ID (GUID) of the Power BI model.

The `InstanceDiscovery` tool can automate the creation of this file.

## Development

- Source code is primarily in the `pbi-local-mcp` directory.
- The solution file is `tabular-mcp.sln`.
- Build artifacts are generated in `bin/` and `obj/` subdirectories within each project.
- The project uses `/warnaserror` for builds.


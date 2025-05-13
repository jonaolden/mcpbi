# Tabular MCP

This project provides a local Model Context Protocol (MCP) implementation for Power BI tabular models.

## Project Structure

- [`pbi-local-mcp/`](pbi-local-mcp/) - Main .NET project directory
  - [`pbi-local-mcp/pbi-local-mcp.csproj`](pbi-local-mcp/pbi-local-mcp.csproj) - Project file
  - [`pbi-local-mcp/Program.cs`](pbi-local-mcp/Program.cs) - Main program entry point

## Getting Started

1. **Clone the repository**
2. **Build the project**
   ```sh
   dotnet build pbi-local-mcp/pbi-local-mcp.csproj
   ```
3. **Run the application**
   ```sh
   dotnet run --project pbi-local-mcp/pbi-local-mcp.csproj
   ```

## Requirements

- [.NET 7.0 SDK](https://dotnet.microsoft.com/download/dotnet/7.0)
- Power BI (for integration scenarios)

## Development

- Source code is in the `pbi-local-mcp` directory.
- Build artifacts are generated in `bin/` and `obj/` directories.

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
PBI_PORT=&lt;port&gt;
PBI_DB_ID=&lt;database Id&gt;
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
dotnet test
```
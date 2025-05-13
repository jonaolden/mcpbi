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
## Running Tests

To run all unit tests:

```sh
dotnet test
```
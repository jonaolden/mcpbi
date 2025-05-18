## INSTALLATION

1. Run InstanceDiscovery to populate environment variables

2. Set up MCP server config json

VSCODE:


{
    "servers": {
      "tabular-mcp": {
        "type": "stdio",
        "command": "dotnet",
        "envFile": "<your-mcp-dir>.env",
        "args": [
          "run",
          "--project",
          "<your-mcp-dir>pbi-local-mcp\\pbi-local-mcp.csproj"
        ]
      }
    }
  }

example: 


{
    "servers": {
      "tabular-mcp": {
        "type": "stdio",
        "command": "dotnet",
        "envFile": "C:\\dev\\powerbi\\tabular-mcp\\.env",
        "args": [
          "run",
          "--project",
          "C:\\dev\\powerbi\\tabular-mcp\\pbi-local-mcp\\pbi-local-mcp.csproj"
        ]
      }
    }
  }
  
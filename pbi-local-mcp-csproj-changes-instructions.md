# Required Changes to pbi-local-mcp.csproj

The project file needs the following changes to support all functionality:

1. Keep all existing PropertyGroup settings (TargetFramework, OutputType, etc.)

2. Add these required NuGet packages:

For PowerBI/Analysis Services:
- Microsoft.AnalysisServices.AdomdClient (Version 19.96.1.0)
- System.Management (Version 8.0.0) - Required for instance discovery

For MCP Protocol:
- ModelContextProtocol (Version 0.2.0-preview.1)

For Infrastructure:
- Microsoft.Extensions.Hosting (Version 8.0.0)
- Microsoft.Extensions.Logging.Console (Version 8.0.0)

For Observability:
- OpenTelemetry (Version 1.7.0)
- OpenTelemetry.Exporter.OpenTelemetryProtocol (Version 1.7.0)
- OpenTelemetry.Extensions.Hosting (Version 1.7.0)

3. Keep the existing ItemGroup that excludes test files from the main project build

4. The packages should be organized in the ItemGroup with appropriate XML comments indicating their purpose.

These changes will resolve the missing dependencies causing errors in:
- InstanceDiscovery.cs (System.Management)
- Server.cs (OpenTelemetry)
- All MCP protocol related functionality
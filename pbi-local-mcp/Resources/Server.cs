using System.CommandLine;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

using Microsoft.AnalysisServices.AdomdClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

using pbi_local_mcp.Configuration;
using pbi_local_mcp.Core;

namespace pbi_local_mcp.Resources;

/// <summary>
/// Handles server configuration and startup for the Power BI Model Context Protocol
/// </summary>
public class ServerConfigurator
{
    private readonly ILogger<ServerConfigurator> _logger;

    /// <summary>
    /// Initializes a new instance of the ServerConfigurator class
    /// </summary>
    /// <param name="logger">Logger instance</param>
    public ServerConfigurator(ILogger<ServerConfigurator> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Configures and runs the MCP server
    /// </summary>
    /// <param name="args">Command line arguments</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public async Task RunAsync(string[] args)
    {
        _logger.LogInformation("Configuring MCP server...");

        // Load .env file as fallback first (won't override existing values)
        LoadEnvFile(".env");

        // Parse command-line arguments (will override .env values if provided)
        await ProcessCommandLineArgumentsAsync(args);

        // Add diagnostic logging to confirm environment variable values after both .env and command-line processing
        _logger.LogInformation("DIAGNOSTIC - Final config values: PBI_PORT={Port}, PBI_DB_ID={DbId}",
            Environment.GetEnvironmentVariable("PBI_PORT"),
            Environment.GetEnvironmentVariable("PBI_DB_ID"));

        var builder = Host.CreateApplicationBuilder(args);

        // Configure logging via centralized extension
        builder.Logging.ConfigureMcpLogging();

        _logger.LogInformation("Starting MCP server configuration...");

        // Configure services
        builder.Services
            .AddMemoryCache()
            .Configure<PowerBiConfig>(config =>
            {
                config.Port = Environment.GetEnvironmentVariable("PBI_PORT") ?? "";
                config.DbId = Environment.GetEnvironmentVariable("PBI_DB_ID") ?? "";
                _logger.LogInformation("PowerBI Config - Port: {Port}, DbId: {DbId}",
                    config.Port,
                    string.IsNullOrEmpty(config.DbId) ? "[Not Set]" : "[Configured]");
            })
            .AddSingleton<ITabularConnection>(serviceProvider =>
            {
                var config = serviceProvider.GetRequiredService<IOptions<PowerBiConfig>>().Value;
                var logger = serviceProvider.GetRequiredService<ILogger<TabularConnection>>();
                return new TabularConnection(config, logger);
            })
            .AddSingleton<PowerBiResourceProvider>()
            .AddSingleton<DaxTools>();

        _logger.LogInformation("Core services registered.");

        // Configure MCP Server
        var mcp = builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithToolsFromAssembly()
            .WithListResourcesHandler(async (context, cancellationToken) =>
            {
                var resourceProvider = context.Services?.GetRequiredService<PowerBiResourceProvider>();
                var logger = context.Services?.GetService<ILogger<ServerConfigurator>>();
                
                if (resourceProvider == null)
                {
                    logger?.LogWarning("PowerBiResourceProvider not available");
                    return new ModelContextProtocol.Protocol.ListResourcesResult { Resources = [] };
                }
                
                logger?.LogDebug("Handling ListResources request");
                var resources = await resourceProvider.ListResourcesAsync(cancellationToken);
                
                return new ModelContextProtocol.Protocol.ListResourcesResult
                {
                    Resources = resources.Select(r => new ModelContextProtocol.Protocol.Resource
                    {
                        Uri = r.Uri,
                        Name = r.Uri.Split('/').Last(),
                        Description = r.Description,
                        MimeType = "application/json"
                    }).ToList()
                };
            })
            .WithReadResourceHandler(async (context, cancellationToken) =>
            {
                var resourceProvider = context.Services?.GetRequiredService<PowerBiResourceProvider>();
                var logger = context.Services?.GetService<ILogger<ServerConfigurator>>();
                
                if (resourceProvider == null)
                {
                    throw new Exception("PowerBiResourceProvider not available");
                }
                
                var uri = context.Params?.Uri ?? throw new Exception("Resource URI is required");
                logger?.LogDebug("Handling ReadResource request for URI: {Uri}", uri);
                
                var content = await resourceProvider.ReadResourceAsync(uri, cancellationToken);
                
                return new ModelContextProtocol.Protocol.ReadResourceResult
                {
                    Contents =
                    [
                        new ModelContextProtocol.Protocol.TextResourceContents
                        {
                            Uri = uri,
                            MimeType = "application/json",
                            Text = System.Text.Json.JsonSerializer.Serialize(content)
                        }
                    ]
                };
            })
            .AddListToolsFilter(next => async (context, cancellationToken) =>
            {
                // Call the next filter in the pipeline
                var result = await next(context, cancellationToken);
                
                var logger = context.Services?.GetService<ILogger<ServerConfigurator>>();
                var resourceProvider = context.Services?.GetService<PowerBiResourceProvider>();
                
                if (resourceProvider == null || logger == null)
                {
                    logger?.LogWarning("ResourceProvider or Logger not available in context");
                    return result;
                }
                
                // Get interface names with fallback
                var interfaceNames = await GetInterfaceNamesWithFallbackAsync(resourceProvider, logger, cancellationToken);
                
                // Inject enum values into ListFunctions tool schema
                if (result.Tools != null)
                {
                    foreach (var tool in result.Tools)
                    {
                        if (tool.Name == "ListFunctions")
                        {
                            InjectEnumIntoSchema(tool, "interfaceName", interfaceNames, logger);
                            break;
                        }
                    }
                }
                
                return result;
            });

        // Startup banner (replaces prior temporary diagnostic reflection block)
        try
        {
            var asm = typeof(ServerConfigurator).Assembly;
            string version = asm.GetName().Version?.ToString() ?? "n/a";
            string hash = "n/a";
            try
            {
                var path = asm.Location;
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    using var sha = SHA256.Create();
                    using var fs = File.OpenRead(path);
                    hash = Convert.ToHexString(sha.ComputeHash(fs)).Substring(0, 12);
                }
            }
            catch
            {
                // swallow hash errors silently
            }

            _logger.LogInformation("Startup Assembly={Assembly} Version={Version} HashPrefix={Hash} (hash truncated)",
                asm.GetName().Name, version, hash);
        }
        catch (Exception bannerEx)
        {
            _logger.LogWarning(bannerEx, "Failed to emit startup assembly banner");
        }

        _logger.LogInformation("MCP server configured with tools from assembly.");

        await builder.Build().RunAsync();
    }

    /// <summary>
    /// Static helper to run the server
    /// </summary>
    public static async Task RunServerAsync(string[] args)
    {
        var loggerFactory = LoggerFactory.Create(b =>
        {
            b.ConfigureMcpLogging();
        });

        var logger = loggerFactory.CreateLogger<ServerConfigurator>();
        var server = new ServerConfigurator(logger);
        await server.RunAsync(args);
    }

    /// <summary>
    /// Loads environment variables from a file as fallback values (won't override existing values)
    /// </summary>
    /// <param name="path">Path to the environment file</param>
    private void LoadEnvFile(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        foreach (var line in File.ReadAllLines(path))
        {
            var parts = line.Split('=', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2)
            {
                var key = parts[0].Trim();
                var value = parts[1].Trim();

                // Only set if the environment variable doesn't already exist (fallback behavior)
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                {
                    Environment.SetEnvironmentVariable(key, value);
                    _logger.LogDebug("Set fallback .env variable {Key}={Value}", key, value);
                }
                else
                {
                    // Log when .env values are being skipped due to existing values
                    _logger.LogDebug("Skipping .env variable {Key}={Value} - already set to {ExistingValue}",
                        key, value, Environment.GetEnvironmentVariable(key));
                }
            }
        }
    }

    /// <summary>
    /// Processes command-line arguments and sets environment variables accordingly
    /// </summary>
    /// <param name="args">Command line arguments</param>
    /// <returns>A task representing the asynchronous operation</returns>
    private async Task ProcessCommandLineArgumentsAsync(string[] args)
    {
        var portOption = new Option<string?>(
            name: "--port",
            description: "PowerBI port number to connect to");

        var rootCommand = new RootCommand("PowerBI Tabular MCP Server")
        {
            portOption
        };

        var parseResult = rootCommand.Parse(args);
        var portValue = parseResult.GetValueForOption(portOption);

        if (!string.IsNullOrWhiteSpace(portValue))
        {
            _logger.LogInformation("Port argument provided: {Port}", portValue);

            // Validate port number
            if (!int.TryParse(portValue, out var port) || port < 1 || port > 65535)
            {
                throw new ArgumentException($"Invalid port number: {portValue}. Must be between 1 and 65535.");
            }

            // Auto-discover database for the given port
            var databaseId = await DiscoverDatabaseForPortAsync(port);
            if (string.IsNullOrEmpty(databaseId))
            {
                throw new InvalidOperationException($"No accessible databases found on port {port}");
            }

            // Set environment variables (these will override .env file values)
            Environment.SetEnvironmentVariable("PBI_PORT", portValue);
            Environment.SetEnvironmentVariable("PBI_DB_ID", databaseId);

            _logger.LogInformation("Auto-discovered database {DatabaseId} on port {Port}", databaseId, port);
        }
    }

    /// <summary>
    /// Discovers the first available database on the specified port
    /// </summary>
    /// <param name="port">The port to check for databases</param>
    /// <returns>The ID of the first database found, or null if none found</returns>
    private async Task<string?> DiscoverDatabaseForPortAsync(int port)
    {
        try
        {
            var connectionString = $"Data Source=localhost:{port}";
            using var conn = new AdomdConnection(connectionString);
            await Task.Run(() => conn.Open()).ConfigureAwait(false);

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM $SYSTEM.DBSCHEMA_CATALOGS";

            using var reader = await Task.Run(() => cmd.ExecuteReader()).ConfigureAwait(false);
            if (await Task.Run(() => reader.Read()).ConfigureAwait(false))
            {
                var databaseId = reader["CATALOG_NAME"]?.ToString();
                if (!string.IsNullOrEmpty(databaseId))
                {
                    return databaseId;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to discover databases on port {Port}", port);
        }

        return null;
    }

    /// <summary>
    /// Gets interface names from the resource provider with indefinite caching and fallback values.
    /// </summary>
    private static readonly string[] FallbackInterfaceNames =
    [
        "DATETIME", "FILTER", "LOGICAL", "MATH", "STATISTICAL",
        "TEXT", "INFORMATION", "PARENT_CHILD", "TIME_INTELLIGENCE"
    ];

    private static IEnumerable<string>? _cachedInterfaceNames;
    private static readonly object _cacheLock = new();

    private static Task<IEnumerable<string>> GetInterfaceNamesWithFallbackAsync(
        PowerBiResourceProvider resourceProvider,
        ILogger logger,
        CancellationToken ct)
    {
        // Return cached value if available (indefinite cache)
        if (_cachedInterfaceNames != null)
        {
            return Task.FromResult(_cachedInterfaceNames);
        }

        lock (_cacheLock)
        {
            // Double-check after acquiring lock
            if (_cachedInterfaceNames != null)
            {
                return Task.FromResult(_cachedInterfaceNames);
            }

            try
            {
                // Query the resource provider synchronously within lock
                var result = resourceProvider.ReadResourceAsync("powerbi://functions/interface-names", ct)
                    .GetAwaiter()
                    .GetResult();

                if (result is IEnumerable<string> names)
                {
                    var namesList = names.ToList();
                    if (namesList.Count > 0)
                    {
                        _cachedInterfaceNames = namesList;
                        logger.LogDebug("Successfully loaded {Count} interface names from resource provider", namesList.Count);
                        return Task.FromResult(_cachedInterfaceNames);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to load interface names from resource provider, using fallback values");
            }

            // Use fallback values
            _cachedInterfaceNames = FallbackInterfaceNames;
            logger.LogInformation("Using fallback interface names (resource query failed or returned empty)");
            return Task.FromResult(_cachedInterfaceNames);
        }
    }

    /// <summary>
    /// Injects enum values into a tool's parameter schema.
    /// </summary>
    private static void InjectEnumIntoSchema(
        Tool tool,
        string parameterName,
        IEnumerable<string> enumValues,
        ILogger logger)
    {
        try
        {
            // Check if InputSchema has a value (JsonElement.ValueKind != Undefined)
            if (tool.InputSchema.ValueKind == JsonValueKind.Undefined || tool.InputSchema.ValueKind == JsonValueKind.Null)
            {
                logger.LogWarning("Tool {ToolName} has no InputSchema, cannot inject enum", tool.Name);
                return;
            }

            // Parse the InputSchema as JsonNode for manipulation
            var schemaNode = JsonNode.Parse(tool.InputSchema.GetRawText());
            if (schemaNode == null)
            {
                logger.LogWarning("Failed to parse InputSchema for tool {ToolName}", tool.Name);
                return;
            }

            // Navigate to properties -> parameterName
            var properties = schemaNode["properties"];
            if (properties == null)
            {
                logger.LogWarning("Tool {ToolName} InputSchema has no 'properties' node", tool.Name);
                return;
            }

            var parameter = properties[parameterName];
            if (parameter == null)
            {
                logger.LogWarning("Tool {ToolName} has no parameter '{ParameterName}'", tool.Name, parameterName);
                return;
            }

            // Add enum array to the parameter
            var enumArray = new JsonArray();
            foreach (var value in enumValues)
            {
                enumArray.Add(JsonValue.Create(value));
            }
            parameter["enum"] = enumArray;

            // Serialize back to JsonElement via JsonDocument
            using var doc = JsonDocument.Parse(schemaNode.ToJsonString());
            tool.InputSchema = doc.RootElement.Clone();
            
            logger.LogDebug("Successfully injected {Count} enum values into {ToolName}.{ParameterName}",
                enumValues.Count(), tool.Name, parameterName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to inject enum into tool {ToolName} parameter {ParameterName}",
                tool.Name, parameterName);
        }
    }
}

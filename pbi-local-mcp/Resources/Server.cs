using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
        LoadEnvFile(".env");

        var builder = Host.CreateApplicationBuilder(args);

        // Configure logging first - separate MCP and debug logging
        builder.Logging.AddConsole(options =>
        {
            options.LogToStandardErrorThreshold = LogLevel.Warning; // Only errors to stderr
        });
        builder.Logging.SetMinimumLevel(LogLevel.Debug);

        _logger.LogInformation("Starting MCP server configuration...");

        // Configure services
        builder.Services
            .Configure<PowerBiConfig>(config =>
            {
                config.Port = Environment.GetEnvironmentVariable("PBI_PORT") ?? "";
                config.DbId = Environment.GetEnvironmentVariable("PBI_DB_ID") ?? "";
                _logger.LogInformation("PowerBI Config - Port: {Port}, DbId: {DbId}", config.Port, config.DbId);
            })
            .AddSingleton<ITabularConnection>(serviceProvider =>
            {
                var config = serviceProvider.GetRequiredService<IOptions<PowerBiConfig>>().Value;
                var logger = serviceProvider.GetRequiredService<ILogger<TabularConnection>>();
                return new TabularConnection(config, logger);
            })
            .AddSingleton<DaxTools>();

        _logger.LogInformation("Core services registered.");

        // Configure MCP Server
        var mcp = builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithToolsFromAssembly();

        _logger.LogInformation("MCP server configured with tools from assembly.");

        await builder.Build().RunAsync();
    }

    /// <summary>
    /// Static helper to run the server
    /// </summary>
    public static async Task RunServerAsync(string[] args)
    {
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        var logger = loggerFactory.CreateLogger<ServerConfigurator>();
        var server = new ServerConfigurator(logger);
        await server.RunAsync(args);
    }

    /// <summary>
    /// Loads environment variables from a file
    /// </summary>
    /// <param name="path">Path to the environment file</param>
    private static void LoadEnvFile(string path)
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
                Environment.SetEnvironmentVariable(parts[0].Trim(), parts[1].Trim());
            }
        }
    }
}

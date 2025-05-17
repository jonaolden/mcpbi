using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using pbi_local_mcp;
using pbi_local_mcp.Configuration;
using pbi_local_mcp.Core;

public static class Server
{
    public static async Task RunServerAsync(string[] args)
    {
        Console.Error.WriteLine(">>> MCP Server: Starting up");
        LoadEnvFile(".env");

        // MCP Server startup using ModelContextProtocol SDK
        // See: resources/documentation/mcp_csharp_sdk.md
        var builder = Host.CreateApplicationBuilder(args);
        
        // Configure logging
        builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

        // Configure services
        builder.Services
            // Add configuration
            .Configure<PowerBiConfig>(config =>
            {
                config.Port = Environment.GetEnvironmentVariable("PBI_PORT") ?? "";
                config.DbId = Environment.GetEnvironmentVariable("PBI_DB_ID") ?? "";
            })
            // Add services
            .AddSingleton<ITabularConnection, TabularConnection>()
            .AddSingleton<Tools>()
            // Add MCP server
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithToolsFromAssembly();

        await builder.Build().RunAsync();
    }

    private static void LoadEnvFile(string path)
    {
        if (!File.Exists(path)) return;
        foreach (var line in File.ReadAllLines(path))
        {
            var parts = line.Split('=', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2)
                Environment.SetEnvironmentVariable(parts[0].Trim(), parts[1].Trim());
        }
    }
}

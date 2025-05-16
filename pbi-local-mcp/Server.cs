// Program.cs 
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using pbi_local_mcp;

public class Program
{
    public static async Task Main(string[] args)
    {
        Console.Error.WriteLine(">>> MCP Server: Starting up");
        LoadEnvFile(".env");

        if (args.Length > 0 &&
            (args[0].Equals("discover-pbi", StringComparison.OrdinalIgnoreCase) ||
             args[0].Equals("InstanceDiscovery", StringComparison.OrdinalIgnoreCase)))
        {
            InstanceDiscovery.RunInteractive();
            return;
        }

        Console.Error.WriteLine(">>> MCP Server: Server mode is not implemented in this build.");
        Console.Error.WriteLine(">>> Exiting.");
    }

    internal static IHostBuilder CreateHostBuilder(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

        var tools = ToolCatalogue.All; // centralised below

        // MCP server code removed due to missing dependencies.
        // Provide a minimal stub or CLI entrypoint.
        Console.WriteLine("Server mode is not implemented in this build.");
        return null!;
    }

    // Minimal local CallToolResponse for compatibility
    public class CallToolResponse
    {
        public List<Content> Content { get; set; } = new();
    }
    public class Content
    {
        public string Type { get; set; } = "";
        public string Text { get; set; } = "";
    }

    private static CallToolResponse Error(string msg) =>
        new CallToolResponse
        {
            Content = new List<Content>
            {
                new Content
                {
                    Type = "application/json",
                    Text = JsonSerializer.Serialize(new { error = msg })
                }
            }
        };

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

internal static class ToolCatalogue
{
    public class Tool
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public JsonElement InputSchema { get; set; }
    }

    public static readonly List<Tool> All = new()
    {
        new Tool
        {
            Name = "listMeasures",
            Description = "Returns a list of measures and their information.",
            InputSchema = JsonSerializer.Deserialize<JsonElement>(
                """
                {
                  "type": "object",
                  "properties": {
                    "tableName": { "type": "string", "description": "Optional table name to filter measures" }
                  }
                }
                """)
        },
        new Tool
        {
            Name = "getMeasureDetails",
            Description = "Returns details of a specific measure.",
            InputSchema = JsonSerializer.Deserialize<JsonElement>(
                """
                {
                  "type": "object",
                  "properties": {
                    "measureName": { "type": "string", "description": "Name of the measure" }
                  },
                  "required": ["measureName"]
                }
                """)
        },
        new Tool
        {
            Name = "listTables",
            Description = "Returns a list of all tables in the model.",
            InputSchema = JsonSerializer.Deserialize<JsonElement>(
                """
                {
                  "type": "object",
                  "properties": {}
                }
                """)
        },
        new Tool
        {
            Name = "getTableDetails",
            Description = "Returns details of a specific table.",
            InputSchema = JsonSerializer.Deserialize<JsonElement>(
                """
                {
                  "type": "object",
                  "properties": {
                    "tableName": { "type": "string", "description": "Name of the table" }
                  },
                  "required": ["tableName"]
                }
                """)
        },
        new Tool
        {
            Name = "getTableColumns",
            Description = "Returns details of all columns in a specific table.",
            InputSchema = JsonSerializer.Deserialize<JsonElement>(
                """
                {
                  "type": "object",
                  "properties": {
                    "tableName": { "type": "string", "description": "Name of the table" }
                  },
                  "required": ["tableName"]
                }
                """)
        },
        new Tool
        {
            Name = "getTableRelationships",
            Description = "Returns details of all relationships in a specific table.",
            InputSchema = JsonSerializer.Deserialize<JsonElement>(
                """
                {
                  "type": "object",
                  "properties": {
                    "tableName": { "type": "string", "description": "Name of the table" }
                  },
                  "required": ["tableName"]
                }
                """)
        },
        new Tool
        {
            Name = "previewTableData",
            Description = "Returns a preview of the data in a specific table.",
            InputSchema = JsonSerializer.Deserialize<JsonElement>(
                """
                {
                  "type": "object",
                  "properties": {
                    "tableName": { "type": "string", "description": "Name of the table" },
                    "topN": { "type": "number", "description": "Number of rows to return (default 10)" }
                  },
                  "required": ["tableName"]
                }
                """)
        },
        new Tool
        {
            Name = "evaluateDAX",
            Description = "Returns the result of a DAX expression.",
            InputSchema = JsonSerializer.Deserialize<JsonElement>(
                """
                {
                  "type": "object",
                  "properties": {
                    "expression": { "type": "string", "description": "DAX expression to evaluate" },
                    "topN": { "type": "number", "description": "Number of rows to return (default 10)" }
                  },
                  "required": ["expression"]
                }
                """)
        }
    };
}

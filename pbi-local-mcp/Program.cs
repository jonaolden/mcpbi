using Microsoft.AnalysisServices.AdomdClient;
// using Microsoft.AnalysisServices.Tabular; // For MeasureDetails - Fully qualified usage
using System.Data;
using System.ComponentModel;
// using System.Collections.Generic; // For IEnumerable<Dictionary<string, object?>> - Analyzer claims unnecessary
using System.Runtime.CompilerServices; // For CallerMemberName
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using ModelContextProtocol.Protocol.Types; // Required for Implementation class
using pbi_local_mcp; // Added for TabularService

/// <summary>
/// Main program class for the pbi-local MCP server.
/// Handles application startup, configuration, and hosts the MCP server.
/// </summary>
public class Program
{
    /// <summary>
    /// The main entry point for the application.
    /// Parses command-line arguments to either run in discovery mode or start the MCP server.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    public static async Task Main(string[] args)
    {
        LoadEnvFile(".env"); // Load environment variables from .env at startup

        if (args.Length > 0 && args[0].Equals("discover-pbi", StringComparison.OrdinalIgnoreCase))
        {
            PbiInstanceDiscovery.RunInteractive();
            return;
        }

        var host = CreateHostBuilder(args);
        await host.RunAsync();
    }

    /// <summary>
    /// Creates and configures the host builder for the MCP server.
    /// Sets up logging, MCP server services, and transport.
    /// </summary>
    /// <param name="args">Command-line arguments passed to the host builder.</param>
    /// <returns>An <see cref="IHost"/> instance.</returns>
    public static IHost CreateHostBuilder(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Logging.AddConsole(consoleLogOptions =>
        {
            // Configure all logs to go to stderr
            consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
        });
        builder.Services.AddMcpServer(options =>
        {
            options.ServerInfo = new Implementation() // Uses ModelContextProtocol.Protocol.Types.Implementation
            {
                Name = "pbi-local",
                Version = "1.0.0"
            };
        })
        .WithStdioServerTransport()
        .WithToolsFromAssembly();

        return builder.Build();
    }

    /// <summary>
    /// Loads environment variables from a specified .env file.
    /// Lines in the file should be in KEY=VALUE format.
    /// </summary>
    /// <param name="filePath">The path to the .env file.</param>
    public static void LoadEnvFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            // .env file not found; silently continue
            return;
        }

        foreach (var line in File.ReadAllLines(filePath))
        {
            var parts = line.Split('=', 2);
            if (parts.Length == 2)
            {
                var key = parts[0].Trim();
                var value = parts[1].Trim();
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }
}

/// <summary>
/// Provides a collection of MCP tools for interacting with a local Power BI instance.
/// These tools allow querying schema, data, and metadata from a Power BI model.
/// Environment variables PBI_PORT and PBI_DB_ID must be set for these tools to function.
/// </summary>
[McpServerToolType]
public static class PbiLocalTools
{
    private static readonly TabularService _tabularService = new();

    private static string GetConnectionString()
    {
        var pbiPort = Environment.GetEnvironmentVariable("PBI_PORT");
        var pbiDbId = Environment.GetEnvironmentVariable("PBI_DB_ID");

        if (string.IsNullOrEmpty(pbiPort))
        {
            throw new InvalidOperationException("Environment variable PBI_PORT is not set or is empty.");
        }
        if (string.IsNullOrEmpty(pbiDbId))
        {
            throw new InvalidOperationException("Environment variable PBI_DB_ID is not set or is empty.");
        }

        return $"Data Source=localhost:{pbiPort};" +
               $"Initial Catalog={pbiDbId};" +
               "Integrated Security=SSPI;Provider=MSOLAP;";
    }

    /// <summary>
    /// Retrieves a schema snapshot of the connected Power BI model, focusing on columns.
    /// </summary>
    /// <returns>A <see cref="DataTable"/> containing column schema information, or an error object.</returns>
    [McpServerTool(Name = "schema_snapshot"), Description("full table+column list (JSON)")]
    public static object Schema()
    {
        try
        {
            string connectionString = GetConnectionString();
            using var conn = new AdomdConnection(connectionString);
            conn.Open();
            var schemaDataSet = conn.GetSchemaDataSet(AdomdSchemaGuid.Columns, new object[] { });
            if (schemaDataSet != null && schemaDataSet.Tables.Count > 0)
            {
                return schemaDataSet.Tables[0];
            }
            return new { error = "SchemaError: Could not retrieve schema information (no tables in dataset)." };
        }
        catch (InvalidOperationException ex) // From GetConnectionString
        {
            Console.Error.WriteLine($"[{DateTime.UtcNow:O}] [Schema] Configuration Error: {ex.GetType().Name} - {ex.Message}{Environment.NewLine}{ex.StackTrace}");
            return new { error = $"ConfigurationError: {ex.Message}" };
        }
        catch (AdomdException ex)
        {
            Console.Error.WriteLine($"[{DateTime.UtcNow:O}] [Schema] ADOMD Error: {ex.GetType().Name} - {ex.Message}{Environment.NewLine}{ex.StackTrace}");
            return new { error = $"AdomdError: {ex.Message}" };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[{DateTime.UtcNow:O}] [Schema] Unexpected Error: {ex.GetType().Name} - {ex.Message}{Environment.NewLine}{ex.StackTrace}");
            return new { error = $"UnexpectedError: {ex.Message}" };
        }
    }

    /// <summary>
    /// Fetches details for a specific measure within a given table.
    /// </summary>
    /// <param name="table">The name of the table containing the measure.</param>
    /// <param name="measure">The name of the measure.</param>
    /// <returns>An object containing the measure's DAX expression, description, and modified time, or an error object.</returns>
    [McpServerTool(Name = "measure_details"), Description("{ dax, description, modified }")]
    public static object MeasureDetails(string table, string measure)
    {
        try
        {
            string connectionString = GetConnectionString();
            using var srv = new Microsoft.AnalysisServices.Tabular.Server();
            srv.Connect(connectionString);

            // Ensure there is at least one database and it has a model
            if (srv.Databases.Count == 0 || srv.Databases[0].Model == null)
            {
                Console.Error.WriteLine($"[{DateTime.UtcNow:O}] [MeasureDetails] Model Error: No databases or model found for connection.");
                return new { error = "ModelError: No databases or model found in the connected Power BI instance." };
            }
            var model = srv.Databases[0].Model;

            if (!model.Tables.Contains(table))
            {
                return new { error = $"Table '{table}' not found." };
            }
            var tableObj = model.Tables[table];

            if (!tableObj.Measures.Contains(measure))
            {
                return new { error = $"Measure '{measure}' not found in table '{table}'." };
            }
            var m = tableObj.Measures[measure];
            return new { dax = m.Expression, m.Description, m.ModifiedTime };
        }
        catch (InvalidOperationException ex) // From GetConnectionString
        {
            Console.Error.WriteLine($"[{DateTime.UtcNow:O}] [MeasureDetails] Configuration Error: {ex.GetType().Name} - {ex.Message}{Environment.NewLine}{ex.StackTrace}");
            return new { error = $"ConfigurationError: {ex.Message}" };
        }
        catch (AdomdException ex) // Covers connection issues, etc. from srv.Connect
        {
            Console.Error.WriteLine($"[{DateTime.UtcNow:O}] [MeasureDetails] Tabular Object Model Error (Connection/Access): {ex.GetType().Name} - {ex.Message}{Environment.NewLine}{ex.StackTrace}");
            return new { error = $"TabularModelConnectionError: {ex.Message}" };
        }
        catch (Exception ex) // General errors from TOM interactions or other issues
        {
            Console.Error.WriteLine($"[{DateTime.UtcNow:O}] [MeasureDetails] Unexpected Error: {ex.GetType().Name} - {ex.Message}{Environment.NewLine}{ex.StackTrace}");
            return new { error = $"UnexpectedError: An error occurred while fetching measure details: {ex.Message}" };
        }
    }

    // INFO.VIEW Functions (can be used in calculations)

    private static bool IsSyntaxError(Exception ex)
    {
        // Check current exception message
        if (ex.Message?.IndexOf("syntax", StringComparison.OrdinalIgnoreCase) >= 0) return true;
        if (ex.Message?.IndexOf("parser", StringComparison.OrdinalIgnoreCase) >= 0) return true;

        // Check inner exception message
        var innerEx = ex.InnerException;
        if (innerEx != null)
        {
            if (innerEx.Message?.IndexOf("syntax", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (innerEx.Message?.IndexOf("parser", StringComparison.OrdinalIgnoreCase) >= 0) return true;
        }
        return false;
    }

    private static async Task<object> HandleTabularServiceCallAsync(
        Func<string, string?, Task<IEnumerable<Dictionary<string, object?>>>> serviceCallAsync,
        string? daxFilterExpression,
        [CallerMemberName] string callerName = "")
    {
        string connectionString;
        try
        {
            connectionString = GetConnectionString();
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine($"[{DateTime.UtcNow:O}] [{callerName}] Configuration Error: {ex.GetType().Name} - {ex.Message}{Environment.NewLine}{ex.StackTrace}");
            return new { error = $"ConfigurationError: {ex.Message}" };
        }

        try
        {
            return await serviceCallAsync(connectionString, daxFilterExpression);
        }
        catch (UnauthorizedAccessException ex) // This is thrown by TabularService
        {
            Console.Error.WriteLine($"[{DateTime.UtcNow:O}] [{callerName}] Authorization Error: {ex.GetType().Name} - {ex.Message}{Environment.NewLine}{ex.StackTrace}");
            return new { error = "InsufficientPermissions: The caller lacks the required semantic model admin or workspace admin permissions." };
        }
        catch (AdomdErrorResponseException ex) // Specific DAX execution errors from ADOMD
        {
            Console.Error.WriteLine($"[{DateTime.UtcNow:O}] [{callerName}] DAX Execution Error (AdomdErrorResponseException): {ex.GetType().Name} - {ex.Message}{Environment.NewLine}{ex.StackTrace}");
            if (IsSyntaxError(ex))
            {
                return new { error = $"InvalidDaxFilterExpression: {ex.Message}" };
            }
            return new { error = $"DaxExecutionError: {ex.Message}" };
        }
        catch (AdomdException ex) // General AdomdException for other ADOMD client issues
        {
            Console.Error.WriteLine($"[{DateTime.UtcNow:O}] [{callerName}] ADOMD Client Error: {ex.GetType().Name} - {ex.Message}{Environment.NewLine}{ex.StackTrace}");
            return new { error = $"AdomdClientError: {ex.Message}" };
        }
        catch (Exception ex) // Generic catch-all
        {
            if (IsSyntaxError(ex))
            {
                Console.Error.WriteLine($"[{DateTime.UtcNow:O}] [{callerName}] DAX Syntax Error (Generic): {ex.GetType().Name} - {ex.Message}{Environment.NewLine}{ex.StackTrace}");
                return new { error = $"InvalidDaxFilterExpression: {ex.Message}" };
            }
            Console.Error.WriteLine($"[{DateTime.UtcNow:O}] [{callerName}] Unexpected Error: {ex.GetType().Name} - {ex.Message}{Environment.NewLine}{ex.StackTrace}");
            return new { error = $"UnexpectedError: An unexpected error occurred. Details: {ex.Message}" };
        }
    }

    /// <summary>
    /// Retrieves information about tables in the model using INFO.VIEW.TABLES().
    /// Optionally filters the results using a DAX expression.
    /// </summary>
    /// <param name="daxFilterExpression">An optional DAX boolean expression to filter the results.</param>
    /// <returns>A collection of table information, or an error object.</returns>
    [McpServerTool(Name = "InfoViewTables"), Description("Returns information about the tables in the model")]
    public static async Task<object> InfoViewTables(string? daxFilterExpression = null)
    {
        return await HandleTabularServiceCallAsync(
            (cs, filter) => _tabularService.InfoViewTablesAsync(cs, filter),
            daxFilterExpression);
    }

    /// <summary>
    /// Retrieves information about columns in the model using INFO.VIEW.COLUMNS().
    /// Optionally filters the results using a DAX expression.
    /// </summary>
    /// <param name="daxFilterExpression">An optional DAX boolean expression to filter the results.</param>
    /// <returns>A collection of column information, or an error object.</returns>
    [McpServerTool(Name = "InfoViewColumns"), Description("Returns information about the columns in the model")]
    public static async Task<object> InfoViewColumns(string? daxFilterExpression = null)
    {
        return await HandleTabularServiceCallAsync(
            (cs, filter) => _tabularService.InfoViewColumnsAsync(cs, filter),
            daxFilterExpression);
    }

    /// <summary>
    /// Retrieves information about measures in the model using INFO.VIEW.MEASURES().
    /// Optionally filters the results using a DAX expression.
    /// </summary>
    /// <param name="daxFilterExpression">An optional DAX boolean expression to filter the results.</param>
    /// <returns>A collection of measure information, or an error object.</returns>
    [McpServerTool(Name = "InfoViewMeasures"), Description("Returns information about the measures in the model")]
    public static async Task<object> InfoViewMeasures(string? daxFilterExpression = null)
    {
        return await HandleTabularServiceCallAsync(
            (cs, filter) => _tabularService.InfoViewMeasuresAsync(cs, filter),
            daxFilterExpression);
    }

    /// <summary>
    /// Retrieves information about relationships in the model using INFO.VIEW.RELATIONSHIPS().
    /// Optionally filters the results using a DAX expression.
    /// </summary>
    /// <param name="daxFilterExpression">An optional DAX boolean expression to filter the results.</param>
    /// <returns>A collection of relationship information, or an error object.</returns>
    [McpServerTool(Name = "InfoViewRelationships"), Description("Returns information about the relationships in the model")]
    public static async Task<object> InfoViewRelationships(string? daxFilterExpression = null)
    {
        return await HandleTabularServiceCallAsync(
            (cs, filter) => _tabularService.InfoViewRelationshipsAsync(cs, filter),
            daxFilterExpression);
    }

    // INFO Functions (DAX queries only)

    /// <summary>
    /// Retrieves detailed metadata about columns in the model using INFO.COLUMNS().
    /// Optionally filters the results using a DAX expression.
    /// </summary>
    /// <param name="daxFilterExpression">An optional DAX boolean expression to filter the results.</param>
    /// <returns>A collection of column metadata, or an error object.</returns>
    [McpServerTool(Name = "InfoColumns"), Description("Returns detailed metadata about columns in the model")]
    public static async Task<object> InfoColumns(string? daxFilterExpression = null)
    {
        return await HandleTabularServiceCallAsync(
            (cs, filter) => _tabularService.InfoColumnsAsync(cs, filter),
            daxFilterExpression);
    }

    /// <summary>
    /// Retrieves detailed metadata about measures in the model using INFO.MEASURES().
    /// Optionally filters the results using a DAX expression.
    /// </summary>
    /// <param name="daxFilterExpression">An optional DAX boolean expression to filter the results.</param>
    /// <returns>A collection of measure metadata, or an error object.</returns>
    [McpServerTool(Name = "InfoMeasures"), Description("Returns detailed metadata about measures in the model")]
    public static async Task<object> InfoMeasures(string? daxFilterExpression = null)
    {
        return await HandleTabularServiceCallAsync(
            (cs, filter) => _tabularService.InfoMeasuresAsync(cs, filter),
            daxFilterExpression);
    }

    /// <summary>
    /// Retrieves detailed metadata about relationships in the model using INFO.RELATIONSHIPS().
    /// Optionally filters the results using a DAX expression.
    /// </summary>
    /// <param name="daxFilterExpression">An optional DAX boolean expression to filter the results.</param>
    /// <returns>A collection of relationship metadata, or an error object.</returns>
    [McpServerTool(Name = "InfoRelationships"), Description("Returns detailed metadata about relationships in the model")]
    public static async Task<object> InfoRelationships(string? daxFilterExpression = null)
    {
        return await HandleTabularServiceCallAsync(
            (cs, filter) => _tabularService.InfoRelationshipsAsync(cs, filter),
            daxFilterExpression);
    }

    /// <summary>
    /// Retrieves information about annotations in the model using INFO.ANNOTATIONS().
    /// Optionally filters the results using a DAX expression.
    /// </summary>
    /// <param name="daxFilterExpression">An optional DAX boolean expression to filter the results.</param>
    /// <returns>A collection of annotation information, or an error object.</returns>
    [McpServerTool(Name = "InfoAnnotations"), Description("Returns information about annotations in the model")]
    public static async Task<object> InfoAnnotations(string? daxFilterExpression = null)
    {
        return await HandleTabularServiceCallAsync(
            (cs, filter) => _tabularService.InfoAnnotationsAsync(cs, filter),
            daxFilterExpression);
    }

    /// <summary>
    /// Retrieves a preview of data from a specified table.
    /// </summary>
    /// <param name="table">The name of the table to preview.</param>
    /// <param name="topN">The maximum number of rows to return.</param>
    /// <returns>A <see cref="DataTable"/> containing the preview data, or an error object.</returns>
    [McpServerTool(Name = "preview"), Description("rows JSON")]
    public static object Preview(string table, int topN = 20)
    {
        try
        {
            string connectionString = GetConnectionString();
            using var conn = new AdomdConnection(connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"EVALUATE TOPN({topN}, '{table}')"; // Note: Table names with spaces might need escaping like 'My Table'
            var dt = new DataTable();
            new AdomdDataAdapter(cmd).Fill(dt);
            return dt;
        }
        catch (InvalidOperationException ex) // From GetConnectionString
        {
            Console.Error.WriteLine($"[{DateTime.UtcNow:O}] [Preview] Configuration Error: {ex.GetType().Name} - {ex.Message}{Environment.NewLine}{ex.StackTrace}");
            return new { error = $"ConfigurationError: {ex.Message}" };
        }
        catch (AdomdErrorResponseException ex) // Specific DAX execution errors
        {
            Console.Error.WriteLine($"[{DateTime.UtcNow:O}] [Preview] DAX Execution Error: {ex.GetType().Name} - {ex.Message}{Environment.NewLine}{ex.StackTrace}");
            return new { error = $"DaxExecutionError: {ex.Message}" };
        }
        catch (AdomdException ex) // General ADOMD errors
        {
            Console.Error.WriteLine($"[{DateTime.UtcNow:O}] [Preview] ADOMD Error: {ex.GetType().Name} - {ex.Message}{Environment.NewLine}{ex.StackTrace}");
            return new { error = $"AdomdError: {ex.Message}" };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[{DateTime.UtcNow:O}] [Preview] Unexpected Error: {ex.GetType().Name} - {ex.Message}{Environment.NewLine}{ex.StackTrace}");
            return new { error = $"UnexpectedError: {ex.Message}" };
        }
    }

    /// <summary>
    /// Evaluates a given DAX expression.
    /// </summary>
    /// <param name="expression">The DAX expression to evaluate. Should be a table expression.</param>
    /// <returns>A <see cref="DataTable"/> containing the results of the DAX expression, or an error object if evaluation fails.</returns>
    [McpServerTool(Name = "evaluate_dax"), Description("rows JSON or { error }")]
    public static object Eval(string expression)
    {
        try
        {
            string connectionString = GetConnectionString();
            using var conn = new AdomdConnection(connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"EVALUATE ({expression})"; // Consider potential for injection if expression is not controlled. For internal tool use, this might be acceptable.
            var dt = new DataTable();
            new AdomdDataAdapter(cmd).Fill(dt);
            return dt;
        }
        catch (InvalidOperationException ex) // From GetConnectionString
        {
            Console.Error.WriteLine($"[{DateTime.UtcNow:O}] [Eval] Configuration Error: {ex.GetType().Name} - {ex.Message}{Environment.NewLine}{ex.StackTrace}");
            return new { error = $"ConfigurationError: {ex.Message}" };
        }
        catch (AdomdErrorResponseException ex) // Specific DAX execution errors
        {
            Console.Error.WriteLine($"[{DateTime.UtcNow:O}] [Eval] DAX Execution Error: {ex.GetType().Name} - {ex.Message}{Environment.NewLine}{ex.StackTrace}");
            return new { error = $"DaxExecutionError: {ex.Message}" };
        }
        catch (AdomdException ex) // General ADOMD errors
        {
            Console.Error.WriteLine($"[{DateTime.UtcNow:O}] [Eval] ADOMD Error: {ex.GetType().Name} - {ex.Message}{Environment.NewLine}{ex.StackTrace}");
            return new { error = $"AdomdError: {ex.Message}" };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[{DateTime.UtcNow:O}] [Eval] Unexpected Error: {ex.GetType().Name} - {ex.Message}{Environment.NewLine}{ex.StackTrace}");
            return new { error = $"UnexpectedError: {ex.Message}" };
        }
    }
/// <summary>
        /// Returns dependencies in the model, filtered by any combination of key columns.
        /// </summary>
        /// <param name="objectType">Optional filter for OBJECT_TYPE.</param>
        /// <param name="objectName">Optional filter for OBJECT.</param>
        /// <param name="referencedObjectType">Optional filter for REFERENCED_OBJECT_TYPE.</param>
        /// <param name="referencedTable">Optional filter for REFERENCED_TABLE.</param>
        /// <param name="referencedObject">Optional filter for REFERENCED_OBJECT.</param>
        /// <returns>Dependency rows matching the filters, or all if no filters are provided.</returns>
        [McpServerTool(Name = "InfoDependencies"), Description("Returns dependencies in the model, filtered by OBJECT_TYPE, OBJECT, REFERENCED_OBJECT_TYPE, REFERENCED_TABLE, REFERENCED_OBJECT")]
        public static async Task<object> InfoDependencies(
            string? objectType = null,
            string? objectName = null,
            string? referencedObjectType = null,
            string? referencedTable = null,
            string? referencedObject = null)
        {
            string connectionString = GetConnectionString();
            var result = await _tabularService.InfoDependenciesAsync(
                connectionString,
                objectType,
                objectName,
                referencedObjectType,
                referencedTable,
                referencedObject
            );
            return result;
        }
}

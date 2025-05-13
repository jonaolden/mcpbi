using Microsoft.AnalysisServices.AdomdClient;
using Microsoft.AnalysisServices.Tabular;
using System.Data;
using System.ComponentModel;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using ModelContextProtocol.Protocol; // Required for McpServerTool attribute
using ModelContextProtocol.Protocol.Types; // Required for Implementation class

public class Program
{
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
                // Optionally log: Console.WriteLine($"Loaded from .env: {key}={value}");
            }
        }
    }
}

[McpServerToolType]
public static class PbiLocalTools
{
    private static string ConnStr() =>
        $"Data Source=localhost:{Environment.GetEnvironmentVariable("PBI_PORT")};" +
        $"Initial Catalog={Environment.GetEnvironmentVariable("PBI_DB_ID")};" +
        "Integrated Security=SSPI;Provider=MSOLAP;";

    [McpServerTool(Name = "schema_snapshot"), Description("full table+column list (JSON)")]
    public static object Schema()
    {
        using var conn = new AdomdConnection(ConnStr());
        conn.Open();
        // Using AdomdSchemaGuid.Columns as GetSchemaDataSet expects a Guid
        return conn.GetSchemaDataSet(AdomdSchemaGuid.Columns, new object[] { }).Tables[0];
    }

    [McpServerTool(Name = "list_relationships"), Description("array { from, to, cardinality, direction }")]
    public static object Relationships()
    {
        const string dax = "EVALUATE INFO.VIEW.RELATIONSHIPS()";

        using var conn = new AdomdConnection(ConnStr());
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = dax;
        var dt = new DataTable();
        new AdomdDataAdapter(cmd).Fill(dt);

        // Print column names for debugging
        Console.WriteLine($"[Relationships()] Source table columns: {string.Join(", ", dt.Columns.Cast<System.Data.DataColumn>().Select(c => c.ColumnName))}");

        var result = new DataTable();
        result.Columns.Add("ID", typeof(int));
        result.Columns.Add("Name", typeof(string));
        result.Columns.Add("Relationship", typeof(string));
        result.Columns.Add("Model", typeof(string));
        result.Columns.Add("IsActive", typeof(bool));
        result.Columns.Add("CrossFilteringBehavior", typeof(string));
        result.Columns.Add("RelyOnReferentialIntegrity", typeof(bool));
        result.Columns.Add("FromTable", typeof(string));
        result.Columns.Add("FromColumn", typeof(string));
        result.Columns.Add("FromCardinality", typeof(string));
        result.Columns.Add("ToTable", typeof(string));
        result.Columns.Add("ToColumn", typeof(string));
        result.Columns.Add("ToCardinality", typeof(string));
        result.Columns.Add("State", typeof(string));
        result.Columns.Add("SecurityFilteringBehavior", typeof(string));

        foreach (DataRow sourceRow in dt.Rows)
        {
            var row = result.NewRow();
            // Always copy these exact columns
            if (dt.Columns.Contains("[ID]")) row["ID"] = sourceRow["[ID]"];
            if (dt.Columns.Contains("[Name]")) row["Name"] = sourceRow["[Name]"];
            if (dt.Columns.Contains("[Relationship]")) row["Relationship"] = sourceRow["[Relationship]"];
            if (dt.Columns.Contains("[Model]")) row["Model"] = sourceRow["[Model]"];
            if (dt.Columns.Contains("[IsActive]")) row["IsActive"] = sourceRow["[IsActive]"];
            if (dt.Columns.Contains("[CrossFilteringBehavior]")) row["CrossFilteringBehavior"] = sourceRow["[CrossFilteringBehavior]"];
            if (dt.Columns.Contains("[RelyOnReferentialIntegrity]")) row["RelyOnReferentialIntegrity"] = sourceRow["[RelyOnReferentialIntegrity]"];
            if (dt.Columns.Contains("[FromTable]")) row["FromTable"] = sourceRow["[FromTable]"];
            if (dt.Columns.Contains("[FromColumn]")) row["FromColumn"] = sourceRow["[FromColumn]"];
            if (dt.Columns.Contains("[FromCardinality]")) row["FromCardinality"] = sourceRow["[FromCardinality]"];
            if (dt.Columns.Contains("[ToTable]")) row["ToTable"] = sourceRow["[ToTable]"];
            if (dt.Columns.Contains("[ToColumn]")) row["ToColumn"] = sourceRow["[ToColumn]"];
            if (dt.Columns.Contains("[ToCardinality]")) row["ToCardinality"] = sourceRow["[ToCardinality]"];
            if (dt.Columns.Contains("[State]")) row["State"] = sourceRow["[State]"];
            if (dt.Columns.Contains("[SecurityFilteringBehavior]")) row["SecurityFilteringBehavior"] = sourceRow["[SecurityFilteringBehavior]"];
            result.Rows.Add(row);
        }

        // Print a sample row for debugging
        if (result.Rows.Count > 0)
        {
            Console.WriteLine($"[Relationships()] First row values: {string.Join(", ", result.Columns.Cast<System.Data.DataColumn>().Select(c => $"{c.ColumnName}='{result.Rows[0][c]}'"))}");
        }

        return result;
    }

    [McpServerTool(Name = "measure_details"), Description("{ dax, description, modified }")]
    public static object MeasureDetails(string table, string measure)
    {
        try
        {
            using var srv = new Microsoft.AnalysisServices.Tabular.Server();
            srv.Connect(ConnStr());

            if (!srv.Databases[0].Model.Tables.Contains(table))
            {
                return new { error = $"Table '{table}' not found." };
            }
            var tableObj = srv.Databases[0].Model.Tables[table];

            if (!tableObj.Measures.Contains(measure))
            {
                return new { error = $"Measure '{measure}' not found in table '{table}'." };
            }
            var m = tableObj.Measures[measure];
            return new { dax = m.Expression, m.Description, m.ModifiedTime };
        }
        catch (Exception ex) // Catch other potential exceptions
        {
            return new { error = $"An error occurred while fetching measure details: {ex.Message}" };
        }
    }

    [McpServerTool(Name = "preview"), Description("rows JSON")]
    public static object Preview(string table, int topN = 20)
    {
        using var conn = new AdomdConnection(ConnStr()); conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"EVALUATE TOPN({topN}, '{table}')";
        var dt = new DataTable();
        new AdomdDataAdapter(cmd).Fill(dt);
        return dt;
    }

    [McpServerTool(Name = "evaluate_dax"), Description("rows JSON or { error }")]
    public static object Eval(string expression)
    {
        using var conn = new AdomdConnection(ConnStr()); conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"EVALUATE ({expression})";
        try
        {
            var dt = new DataTable();
            new AdomdDataAdapter(cmd).Fill(dt);
            return dt;
        }
        catch (AdomdErrorResponseException ex)
        {
            return new { error = ex.Message };
        }
    }
}

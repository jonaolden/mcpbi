using Microsoft.AnalysisServices.AdomdClient;
using Microsoft.AnalysisServices.Tabular;
using System.Data;
using System.ComponentModel;
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

        var host = builder.Build();
        await host.RunAsync();
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
        const string sql = @"SELECT f.NAME AS FromTable, fc.NAME AS FromCol,
                                t.NAME AS ToTable,   tc.NAME AS ToCol,
                                r.CARDINALITY, r.CROSSFILTER_DIRECTION
                         FROM $SYSTEM.TMSCHEMA_RELATIONSHIPS r
                         JOIN $SYSTEM.TMSCHEMA_TABLES f  ON f.ID  = r.FROM_TABLE_ID
                         JOIN $SYSTEM.TMSCHEMA_COLUMNS fc ON fc.ID = r.FROM_COLUMN_ID
                         JOIN $SYSTEM.TMSCHEMA_TABLES t  ON t.ID  = r.TO_TABLE_ID
                         JOIN $SYSTEM.TMSCHEMA_COLUMNS tc ON tc.ID = r.TO_COLUMN_ID;";
        using var conn = new AdomdConnection(ConnStr());
        conn.Open();
        var cmd = conn.CreateCommand(); cmd.CommandText = sql;
        var dt = new DataTable();
        new AdomdDataAdapter(cmd).Fill(dt);
        return dt;
    }

    [McpServerTool(Name = "measure_details"), Description("{ dax, description, modified }")]
    public static object MeasureDetails(string table, string measure)
    {
        using var srv = new Microsoft.AnalysisServices.Tabular.Server(); 
        srv.Connect(ConnStr());
        var m = srv.Databases[0].Model.Tables[table].Measures[measure];
        return new { dax = m.Expression, m.Description, m.ModifiedTime };
    }

    [McpServerTool(Name = "preview"), Description("rows JSON")]
    public static object Preview(string table, int topN = 20)
    {
        using var conn = new AdomdConnection(ConnStr()); conn.Open();
        var cmd = conn.CreateCommand();
        cmd.CommandText = $"EVALUATE TOPN({topN}, '{table}')";
        var dt = new DataTable();
        new AdomdDataAdapter(cmd).Fill(dt);
        return dt;
    }

    [McpServerTool(Name = "evaluate_dax"), Description("rows JSON or { error }")]
    public static object Eval(string expression)
    {
        using var conn = new AdomdConnection(ConnStr()); conn.Open();
        var cmd = conn.CreateCommand();
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

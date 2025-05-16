// TabularConnection.cs
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.AnalysisServices.AdomdClient;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("pbi-local-mcp.Tests")]
namespace pbi_local_mcp;

/// <summary>
/// Encapsulates ADOMD.NET calls against a local Power BI semantic model.
/// </summary>
public class TabularConnection
{
    // ---------- connection string builder --------------------------------------
    private static string GetConnectionString()
    {
        var port = Environment.GetEnvironmentVariable("PBI_PORT")
                   ?? throw new InvalidOperationException("PBI_PORT not set");
        var db   = Environment.GetEnvironmentVariable("PBI_DB_ID")
                   ?? throw new InvalidOperationException("PBI_DB_ID not set");
        return $"Data Source=localhost:{port};Initial Catalog={db};Integrated Security=SSPI;Provider=MSOLAP;";
    }

    // ---------- generic helpers -------------------------------------------

    public async Task<IEnumerable<Dictionary<string, object?>>> ExecAsync(string dax)
    {
        var rows = new List<Dictionary<string, object?>>();
        await Task.Run(() =>
        {
            Console.WriteLine($"[DAX Verbose] Executing DAX query:{Environment.NewLine}{dax}");
            using var conn = new AdomdConnection(GetConnectionString());
            conn.Open();
            using var cmd = new AdomdCommand(dax, conn);
            using var rdr = cmd.ExecuteReader();

            while (rdr.Read())
            {
                var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < rdr.FieldCount; i++)
                    row[rdr.GetName(i)] = rdr.IsDBNull(i) ? null : rdr.GetValue(i);
                rows.Add(row);
            }
        });
        return rows;
    }

    public Task<IEnumerable<Dictionary<string, object?>>> ExecInfoAsync(string func, string? filterExpr)
    {
        var dax = string.IsNullOrEmpty(filterExpr)
            ? $"EVALUATE {func}()"
            : $"EVALUATE FILTER({func}(), {filterExpr})";
        return ExecAsync(dax);
    }

    public Task<IEnumerable<Dictionary<string, object?>>> ExecDaxAsync(string dax) =>
        ExecAsync(GetConnectionString(), dax);

    // Overload to match ExecAsync signature for ExecDaxAsync
    private static async Task<IEnumerable<Dictionary<string, object?>>> ExecAsync(string connStr, string dax)
    {
        var rows = new List<Dictionary<string, object?>>();
        await Task.Run(() =>
        {
            Console.WriteLine($"[DAX Verbose] Executing DAX query:{Environment.NewLine}{dax}");
            using var conn = new AdomdConnection(connStr);
            conn.Open();
            using var cmd = new AdomdCommand(dax, conn);
            using var rdr = cmd.ExecuteReader();

            while (rdr.Read())
            {
                var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < rdr.FieldCount; i++)
                    row[rdr.GetName(i)] = rdr.IsDBNull(i) ? null : rdr.GetValue(i);
                rows.Add(row);
            }
        });
        return rows;
    }
}

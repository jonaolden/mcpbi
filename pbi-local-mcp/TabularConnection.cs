using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.AnalysisServices.AdomdClient;
using pbi_local_mcp.Configuration;
using pbi_local_mcp.Core;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("pbi-local-mcp.Tests")]
namespace pbi_local_mcp;

/// <summary>
/// Encapsulates ADOMD.NET calls against a local Power BI semantic model.
/// </summary>
public class TabularConnection : ITabularConnection
{
    private readonly PowerBiConfig _config;
    private readonly string _connectionString;

    public TabularConnection(PowerBiConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _config.Validate();
        _connectionString = BuildConnectionString();
    }

    private string BuildConnectionString() =>
        $"Data Source=localhost:{_config.Port};Initial Catalog={_config.DbId};Integrated Security=SSPI;Provider=MSOLAP;";

    // ---------- ITabularConnection implementation -------------------------------------------

    public async Task<IEnumerable<Dictionary<string, object?>>> ExecAsync(string dax)
    {
        return await ExecuteQueryAsync(dax);
    }

    public Task<IEnumerable<Dictionary<string, object?>>> ExecDaxAsync(string dax) =>
        ExecAsync(dax);

    public async Task<IEnumerable<Dictionary<string, object?>>> ExecInfoAsync(string func, string? filterExpr)
    {
        var dax = string.IsNullOrEmpty(filterExpr)
            ? $"EVALUATE {func}()"
            : $"EVALUATE FILTER({func}(), {filterExpr})";
        return await ExecAsync(dax);
    }

    // ---------- private implementation helpers -------------------------------------------

    private async Task<IEnumerable<Dictionary<string, object?>>> ExecuteQueryAsync(string dax)
    {
        var rows = new List<Dictionary<string, object?>>();
        await Task.Run(() =>
        {
            Console.WriteLine($"[DAX Verbose] Executing DAX query:{Environment.NewLine}{dax}");
            using var conn = new AdomdConnection(_connectionString);
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

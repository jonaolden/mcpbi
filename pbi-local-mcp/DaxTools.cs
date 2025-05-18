using System.ComponentModel;

using ModelContextProtocol.Server;

namespace pbi_local_mcp;

/// <summary>
/// DAX Tools exposed as MCP server tools.
/// </summary>
[McpServerToolType]
public static class DaxTools
{
    private static TabularConnection CreateConnection()
    {
        var port = Environment.GetEnvironmentVariable("PBI_PORT");
        var dbId = Environment.GetEnvironmentVariable("PBI_DB_ID");
        var config = new Configuration.PowerBiConfig { Port = port ?? "", DbId = dbId ?? "" };
        config.Validate();
        return new TabularConnection(config);
    }

    [McpServerTool, Description("List all measures in the model, optionally filtered by table name.")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public static async Task<object> ListMeasures(string? tableName = null)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        var tabular = CreateConnection();
        var dmv = "SELECT * FROM $SYSTEM.TMSCHEMA_MEASURES";
        if (!string.IsNullOrEmpty(tableName))
        {
            dmv += $" WHERE TableID = '{tableName}'";
        }
        var result = await tabular.ExecAsync(dmv, QueryType.DMV);
        return result;
    }

    [McpServerTool, Description("Get details for a specific measure by name.")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public static async Task<object> GetMeasureDetails(string measureName)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        var tabular = CreateConnection();
        var dmv = $"SELECT * FROM $SYSTEM.TMSCHEMA_MEASURES WHERE [NAME] = '{measureName}'";
        var result = await tabular.ExecAsync(dmv, QueryType.DMV);
        return result;
    }

    [McpServerTool, Description("List all tables in the model.")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public static async Task<object> ListTables()
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        var tabular = CreateConnection();
        var dmv = "SELECT * FROM $SYSTEM.TMSCHEMA_TABLES";
        var result = await tabular.ExecAsync(dmv, QueryType.DMV);
        return result;
    }

    [McpServerTool, Description("Get details for a specific table by name.")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public static async Task<object> GetTableDetails(string tableName)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        var tabular = CreateConnection();
        var dmv = $"SELECT * FROM $SYSTEM.TMSCHEMA_TABLES WHERE [NAME] = '{tableName}'";
        var result = await tabular.ExecAsync(dmv, QueryType.DMV);
        return result;
    }

    [McpServerTool, Description("Get columns for a specific table by name.")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public static async Task<object> GetTableColumns(string tableName)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        var tabular = CreateConnection();
        var tableIdQuery = $"SELECT [ID] FROM $SYSTEM.TMSCHEMA_TABLES WHERE [NAME] = '{tableName}'";
        var tableIdResult = await tabular.ExecAsync(tableIdQuery, QueryType.DMV);
        var tableId = tableIdResult is IEnumerable<Dictionary<string, object?>> rows && rows.Any()
            ? rows.First()["ID"]?.ToString()
            : null;

        if (string.IsNullOrEmpty(tableId))
            throw new Exception($"Table ID not found for table '{tableName}'");

        var dmv = $"SELECT * FROM $SYSTEM.TMSCHEMA_COLUMNS WHERE [TABLEID] = '{tableId}'";
        var result = await tabular.ExecAsync(dmv, QueryType.DMV);
        return result;
    }

    [McpServerTool, Description("Get relationships for a specific table by name.")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public static async Task<object> GetTableRelationships(string tableName)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        var tabular = CreateConnection();
        var tableIdQuery = $"SELECT [ID] FROM $SYSTEM.TMSCHEMA_TABLES WHERE [NAME] = '{tableName}'";
        var tableIdResult = await tabular.ExecAsync(tableIdQuery, QueryType.DMV);
        var tableId = tableIdResult is IEnumerable<Dictionary<string, object?>> rows && rows.Any()
            ? rows.First()["ID"]?.ToString()
            : null;

        if (string.IsNullOrEmpty(tableId))
            throw new Exception($"Table ID not found for table '{tableName}'");

        var dmv = $"SELECT * FROM $SYSTEM.TMSCHEMA_RELATIONSHIPS WHERE [FROMTABLEID] = '{tableId}' OR [TOTABLEID] = '{tableId}'";
        var result = await tabular.ExecAsync(dmv, QueryType.DMV);
        return result;
    }

    [McpServerTool, Description("Preview data from a table (top N rows).")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public static async Task<object> PreviewTableData(string tableName, int topN = 10)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        var tabular = CreateConnection();
        var dax = $"EVALUATE TOPN({topN}, '{tableName}')";
        var result = await tabular.ExecAsync(dax);
        return result;
    }

    [McpServerTool, Description("Evaluate a DAX expression, optionally limiting to top N rows.")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public static async Task<object> EvaluateDAX(string dax, int topN = 10)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        var tabular = CreateConnection();
        string query = dax.Trim();
        bool isTableExpr = false;

        if (query.StartsWith("EVALUATE", StringComparison.OrdinalIgnoreCase))
        {
            isTableExpr = false;
        }
        else if (query.StartsWith("'") || query.StartsWith("SELECTCOLUMNS", StringComparison.OrdinalIgnoreCase) ||
                 query.StartsWith("ADDCOLUMNS", StringComparison.OrdinalIgnoreCase) ||
                 query.StartsWith("SUMMARIZE", StringComparison.OrdinalIgnoreCase) ||
                 query.StartsWith("FILTER", StringComparison.OrdinalIgnoreCase) ||
                 query.StartsWith("VALUES", StringComparison.OrdinalIgnoreCase) ||
                 query.StartsWith("ALL", StringComparison.OrdinalIgnoreCase))
        {
            isTableExpr = true;
        }
        else
        {
            isTableExpr = false;
        }

        if (topN > 0 && isTableExpr)
        {
            query = $"EVALUATE TOPN({topN}, {query})";
        }
        else if (topN > 0 && !query.StartsWith("EVALUATE", StringComparison.OrdinalIgnoreCase) && !isTableExpr)
        {
            query = $"EVALUATE ROW(\"Value\", {query})";
        }
        else if (!query.StartsWith("EVALUATE", StringComparison.OrdinalIgnoreCase) && isTableExpr)
        {
            query = $"EVALUATE {query}";
        }

        var result = await tabular.ExecAsync(query, QueryType.DAX);
        return result;
    }
}
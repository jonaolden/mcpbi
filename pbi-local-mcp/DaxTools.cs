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
        string dmv;

        if (!string.IsNullOrEmpty(tableName))
        {
            // First, get the actual Table ID (integer) from the table name
            var tableIdQuery = $"SELECT [ID] FROM $SYSTEM.TMSCHEMA_TABLES WHERE [NAME] = '{tableName}'";
            var tableIdResult = await tabular.ExecAsync(tableIdQuery, QueryType.DMV);

            if (tableIdResult is IEnumerable<Dictionary<string, object?>> rows && rows.Any())
            {
                var tableIdObj = rows.First()["ID"];
                if (tableIdObj != null && int.TryParse(tableIdObj.ToString(), out int actualTableId))
                {
                    dmv = $"SELECT * FROM $SYSTEM.TMSCHEMA_MEASURES WHERE [TableID] = {actualTableId}";
                }
                else
                {
                    // Table name found but ID is not an int or is null - likely indicates an issue or no measures for a non-standard table.
                    throw new Exception($"Invalid or non-integer Table ID for table '{tableName}'. Please check the table configuration.");
                }
            }
            else
            {
                // Table name not found, so no measures for this table.
                return new List<Dictionary<string, object?>>();
            }
        }
        else
        {
            dmv = "SELECT * FROM $SYSTEM.TMSCHEMA_MEASURES";
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
    /// <summary>
    /// Execute a DAX query. Supports complete DAX queries with DEFINE blocks or simple expressions.
    /// </summary>
    /// <param name="dax">The DAX query to execute. Can be a complete query with DEFINE block, an EVALUATE statement, or a simple expression.</param>
    /// <param name="topN">Maximum number of rows to return for table expressions (default: 10). Ignored for complete queries.</param>
    /// <returns>Query execution result</returns>
    /// <exception cref="ArgumentException">Thrown when query validation fails</exception>
    /// <exception cref="Exception">Thrown when query execution fails</exception>
    [McpServerTool, Description("Execute a DAX query. Supports complete DAX queries with DEFINE blocks, EVALUATE statements, or simple expressions.")]
    public static async Task<object> RunQuery(string dax, int topN = 10)
    {
        var tabular = CreateConnection();
        string query = dax.Trim();

        try
        {
            // If it's a complete DAX query with DEFINE, execute as-is
            if (query.StartsWith("DEFINE", StringComparison.OrdinalIgnoreCase))
            {
                ValidateCompleteDAXQuery(query);
                var result = await tabular.ExecAsync(query, QueryType.DAX);
                return result;
            }

            // If it's already an EVALUATE statement, execute as-is
            if (query.StartsWith("EVALUATE", StringComparison.OrdinalIgnoreCase))
            {
                var result = await tabular.ExecAsync(query, QueryType.DAX);
                return result;
            }

            // Otherwise, wrap in appropriate EVALUATE statement
            var evaluateStatement = ConstructEvaluateStatement(query, topN);
            var result2 = await tabular.ExecAsync(evaluateStatement, QueryType.DAX);
            return result2;
        }
        catch (Exception ex)
        {
            throw new Exception($"Error executing DAX query: {ex.Message}\n\nQuery:\n{query}", ex);
        }
    }

    /// <summary>
    /// Validates a complete DAX query with DEFINE block according to DAX specification
    /// </summary>
    /// <param name="query">The complete DAX query to validate</param>
    /// <exception cref="ArgumentException">Thrown when validation fails</exception>
    private static void ValidateCompleteDAXQuery(string query)
    {
        // Must contain at least one EVALUATE statement (per DAX specification)
        if (!query.Contains("EVALUATE", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Complete DAX queries with DEFINE must include at least one EVALUATE statement.");
        }

        // Basic parentheses balance check
        var openParens = query.Count(c => c == '(');
        var closeParens = query.Count(c => c == ')');
        if (openParens != closeParens)
        {
            throw new ArgumentException($"DAX query has unbalanced parentheses: {openParens} open, {closeParens} close.");
        }

        // Basic bracket balance check for table/column references
        var openBrackets = query.Count(c => c == '[');
        var closeBrackets = query.Count(c => c == ']');
        if (openBrackets != closeBrackets)
        {
            throw new ArgumentException($"DAX query has unbalanced brackets: {openBrackets} open, {closeBrackets} close.");
        }
    }

    /// <summary>
    /// Constructs an EVALUATE statement based on the query and topN value.
    /// </summary>
    /// <param name="query">The core query expression.</param>
    /// <param name="topN">Maximum number of rows to return (default: 10).</param>
    /// <returns>The constructed EVALUATE statement.</returns>
    private static string ConstructEvaluateStatement(string query, int topN)
    {
        query = query.Trim();
        bool isCoreQueryTableExpr = query.StartsWith("'") || // Table constructor or table name
                                    query.StartsWith("SELECTCOLUMNS", StringComparison.OrdinalIgnoreCase) ||
                                    query.StartsWith("ADDCOLUMNS", StringComparison.OrdinalIgnoreCase) ||
                                    query.StartsWith("SUMMARIZE", StringComparison.OrdinalIgnoreCase) ||
                                    query.StartsWith("FILTER", StringComparison.OrdinalIgnoreCase) ||
                                    query.StartsWith("VALUES", StringComparison.OrdinalIgnoreCase) ||
                                    query.StartsWith("ALL", StringComparison.OrdinalIgnoreCase);

        if (isCoreQueryTableExpr)
        {
            return topN > 0 ? $"EVALUATE TOPN({topN}, {query})" : $"EVALUATE {query}";
        }
        else
        {
            return $"EVALUATE ROW(\"Value\", {query})";
        }
    }

}
using System.ComponentModel;

using ModelContextProtocol.Server;

namespace pbi_local_mcp;

/// <summary>
/// Types of DEFINE entities for DAX queries.
/// </summary>
public enum DefinitionType
{
    /// <summary>
    /// A measure definition (MEASURE)
    /// </summary>
    MEASURE,
    /// <summary>
    /// A variable definition (VAR)
    /// </summary>
    VAR,
    /// <summary>
    /// A table definition (TABLE)
    /// </summary>
    TABLE,
    /// <summary>
    /// A column definition (COLUMN)
    /// </summary>
    COLUMN
}

/// <summary>
/// Represents a DEFINE entity for DAX queries.
/// </summary>
public class Definition
{
    /// <summary>
    /// The type of the definition (MEASURE, VAR, TABLE, COLUMN)
    /// </summary>
    public DefinitionType Type { get; set; }
    /// <summary>
    /// The name of the definition
    /// </summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// The table name (required for MEASURE and COLUMN types)
    /// </summary>
    public string? TableName { get; set; }
    /// <summary>
    /// The DAX expression for the definition
    /// </summary>
    public string Expression { get; set; } = string.Empty;
}

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
                    // Return empty or handle as an error. For now, returning empty if ID is invalid.
                    return new List<Dictionary<string, object?>>();
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
    /// Execute a DAX query with optional DEFINE block definitions
    /// </summary>
    /// <param name="dax">The DAX query expression to execute</param>
    /// <param name="definitions">Optional collection of DEFINE block definitions</param>
    /// <param name="topN">Maximum number of rows to return (default: 10)</param>
    /// <returns>Query execution result</returns>
    [McpServerTool, Description("Execute a DAX query with optional DEFINE block definitions.")]
    public static async Task<object> RunQuery(
        string dax,
        IEnumerable<Definition>? definitions = null,
        int topN = 10)
    {
        var tabular = CreateConnection();
        string query = dax.Trim();

        // Validate definitions if provided
        if (definitions != null)
        {
            foreach (var def in definitions)
            {
                ValidateDefinition(def);
            }
        }

        // If no definitions, fallback to EvaluateDAX logic for compatibility
        if (definitions == null || !definitions.Any())
        {
            string queryToExecute;
            if (query.StartsWith("EVALUATE", StringComparison.OrdinalIgnoreCase))
            {
                queryToExecute = query; // Already a full EVALUATE statement
            }
            else
            {
                // Determine if the core part of the query is a table expression
                bool isCoreQueryTableExpr = false;
                if (query.StartsWith("'") || // Table constructor or table name
                    query.StartsWith("SELECTCOLUMNS", StringComparison.OrdinalIgnoreCase) ||
                    query.StartsWith("ADDCOLUMNS", StringComparison.OrdinalIgnoreCase) ||
                    query.StartsWith("SUMMARIZE", StringComparison.OrdinalIgnoreCase) ||
                    query.StartsWith("FILTER", StringComparison.OrdinalIgnoreCase) ||
                    query.StartsWith("VALUES", StringComparison.OrdinalIgnoreCase) ||
                    query.StartsWith("ALL", StringComparison.OrdinalIgnoreCase))
                {
                    isCoreQueryTableExpr = true;
                }

                if (isCoreQueryTableExpr)
                {
                    if (topN > 0)
                    {
                        queryToExecute = $"EVALUATE TOPN({topN}, {query})";
                    }
                    else // topN is 0 or less, evaluate the whole table
                    {
                        queryToExecute = $"EVALUATE {query}";
                    }
                }
                else // Scalar expression
                {
                    // For scalar expressions, always wrap in ROW to make it a valid table for EVALUATE.
                    queryToExecute = $"EVALUATE ROW(\"Value\", {query})";
                }
            }
            var result = await tabular.ExecAsync(queryToExecute, QueryType.DAX);
            return result;
        }

        // Render DEFINE block
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("DEFINE");

        // Order: VAR > TABLE > COLUMN > MEASURE
        foreach (var def in definitions.Where(d => d.Type == DefinitionType.VAR))
        {
            sb.AppendLine($"    VAR {def.Name} = {def.Expression}");
        }
        foreach (var def in definitions.Where(d => d.Type == DefinitionType.TABLE))
        {
            sb.AppendLine($"    TABLE {def.Name} = {def.Expression}");
        }
        foreach (var def in definitions.Where(d => d.Type == DefinitionType.COLUMN))
        {
            if (string.IsNullOrWhiteSpace(def.TableName))
                throw new ArgumentException("TableName is required for COLUMN definitions.");
            sb.AppendLine($"    COLUMN {def.TableName}[{def.Name}] = {def.Expression}");
        }
        foreach (var def in definitions.Where(d => d.Type == DefinitionType.MEASURE))
        {
            if (string.IsNullOrWhiteSpace(def.TableName))
                throw new ArgumentException("TableName is required for MEASURE definitions.");
            sb.AppendLine($"    MEASURE {def.TableName}[{def.Name}] = {def.Expression}");
        }

        // Append EVALUATE statement(s)
        if (query.StartsWith("EVALUATE", StringComparison.OrdinalIgnoreCase))
        {
            sb.AppendLine(query); // User provided full EVALUATE statement
        }
        else
        {
            // Determine if the core part of the query is a table expression
            bool isCoreQueryTableExpr = false;
            if (query.StartsWith("'") || // Table constructor or table name
                query.StartsWith("SELECTCOLUMNS", StringComparison.OrdinalIgnoreCase) ||
                query.StartsWith("ADDCOLUMNS", StringComparison.OrdinalIgnoreCase) ||
                query.StartsWith("SUMMARIZE", StringComparison.OrdinalIgnoreCase) ||
                query.StartsWith("FILTER", StringComparison.OrdinalIgnoreCase) ||
                query.StartsWith("VALUES", StringComparison.OrdinalIgnoreCase) ||
                query.StartsWith("ALL", StringComparison.OrdinalIgnoreCase)
               )
            {
                isCoreQueryTableExpr = true;
            }

            if (isCoreQueryTableExpr)
            {
                if (topN > 0)
                {
                    sb.AppendLine($"EVALUATE TOPN({topN}, {query})");
                }
                else // topN is 0 or less, evaluate the whole table
                {
                    sb.AppendLine($"EVALUATE {query}");
                }
            }
            else // Scalar expression
            {
                // For scalar expressions, topN is not directly applicable for ROW.
                // We always wrap it in ROW to make it a valid table expression for EVALUATE.
                sb.AppendLine($"EVALUATE ROW(\"Value\", {query})");
            }
        }

        var finalQuery = sb.ToString();
        var resultDef = await tabular.ExecAsync(finalQuery, QueryType.DAX);
        return resultDef;
    }

    /// <summary>
    /// Validates a definition according to DAX naming rules and requirements.
    /// </summary>
    /// <param name="definition">The definition to validate</param>
    /// <exception cref="ArgumentException">Thrown when validation fails</exception>
    private static void ValidateDefinition(Definition definition)
    {
        // Validate name is not null/empty
        if (string.IsNullOrWhiteSpace(definition.Name))
            throw new ArgumentException("Definition name cannot be null or empty.");

        // Validate expression is not null/empty
        if (string.IsNullOrWhiteSpace(definition.Expression))
            throw new ArgumentException($"Expression for definition '{definition.Name}' cannot be null or empty.");

        // Validate TableName is provided for MEASURE and COLUMN types
        if ((definition.Type == DefinitionType.MEASURE || definition.Type == DefinitionType.COLUMN) &&
            string.IsNullOrWhiteSpace(definition.TableName))
        {
            throw new ArgumentException($"TableName is required for {definition.Type} definitions. Definition: '{definition.Name}'");
        }

        // Basic DAX naming validation - names cannot start with numbers
        if (char.IsDigit(definition.Name[0]))
            throw new ArgumentException($"Definition name '{definition.Name}' cannot start with a digit.");

        // Names cannot contain certain special characters
        var invalidChars = new char[] { ' ', '.', '[', ']', '(', ')', '{', '}', '\t', '\n', '\r' };
        if (definition.Name.IndexOfAny(invalidChars) >= 0)
            throw new ArgumentException($"Definition name '{definition.Name}' contains invalid characters. Names cannot contain spaces, dots, brackets, or special characters.");
    }
}
namespace pbi_local_mcp;

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using Microsoft.AnalysisServices.AdomdClient;
using pbi_local_mcp.Core;

/// <summary>
/// Provides DAX query tools for the Power BI Model Context Protocol server.
/// </summary>
public class DaxTools
{
    private readonly ITabularConnection _tabular;

    public DaxTools(ITabularConnection tabular)
    {
        _tabular = tabular ?? throw new ArgumentNullException(nameof(tabular));
    }

    /// <summary>
    /// Response object for tool calls
    /// </summary>
    public sealed class CallToolResponse
    {
        /// <summary>
        /// Collection of content items in the response
        /// </summary>
        public required List<Content> Content { get; init; } = new();
    }

    /// <summary>
    /// Content item in a tool response
    /// </summary>
    public sealed class Content
    {
        /// <summary>
        /// MIME type of the content
        /// </summary>
        public required string Type { get; init; } = "";

        /// <summary>
        /// Actual content text
        /// </summary>
        public required string Text { get; init; } = "";
    }

    private static CallToolResponse Wrap(string dax, object result) =>
        new()
        {
            Content = new List<Content>
            {
                new Content
                {
                    Type = "application/dax",
                    Text = dax
                },
                new Content
                {
                    Type = "application/json",
                    Text = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })
                }
            }
        };

    private static bool IsSyntaxError(Exception ex) =>
        ex.Message.Contains("syntax", StringComparison.OrdinalIgnoreCase) ||
        ex.Message.Contains("parser", StringComparison.OrdinalIgnoreCase) ||
        (ex.InnerException is { } ie &&
         (ie.Message.Contains("syntax", StringComparison.OrdinalIgnoreCase) ||
          ie.Message.Contains("parser", StringComparison.OrdinalIgnoreCase)));

    private async Task<object> Safe(
        Func<Task<IEnumerable<Dictionary<string, object?>>>> op,
        [CallerMemberName] string member = "")
    {
        try
        {
            return await op();
        }
        catch (UnauthorizedAccessException)
        {
            return new { error = "InsufficientPermissions" };
        }
        catch (AdomdErrorResponseException ex) when (IsSyntaxError(ex))
        {
            return new { error = $"InvalidDaxFilterExpression: {ex.Message}" };
        }
        catch (AdomdErrorResponseException ex)
        {
            return new { error = $"DaxExecutionError: {ex.Message}" };
        }
        catch (AdomdException ex)
        {
            return new { error = $"AdomdClientError: {ex.Message}" };
        }
        catch (Exception ex) when (IsSyntaxError(ex))
        {
            return new { error = $"InvalidDaxFilterExpression: {ex.Message}" };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[{DateTime.UtcNow:O}] [{member}] {ex}");
            return new { error = $"UnexpectedError: {ex.Message}" };
        }
    }

    /// <summary>
    /// Lists all measures in the model, optionally filtered by table name
    /// </summary>
    /// <param name="tableName">Optional table name to filter measures</param>
    /// <returns>Response containing the list of measures</returns>
    public async Task<CallToolResponse> ListMeasures(string? tableName = null)
    {
        string? filter = string.IsNullOrWhiteSpace(tableName)
            ? null
            : $"SEARCH(\"{tableName.Replace("\"", "\"\"")}\",[Table],1,0)>0";

        var dax = "EVALUATE INFO.VIEW.MEASURES()";
        if (!string.IsNullOrWhiteSpace(filter))
        {
            dax = $"EVALUATE FILTER(INFO.VIEW.MEASURES(), {filter})";
        }

        var result = await Safe(() => _tabular.ExecAsync(dax));
        return Wrap(dax, result);
    }

    /// <summary>
    /// Gets detailed information about a specific measure
    /// </summary>
    /// <param name="measureName">Name of the measure</param>
    /// <returns>Response containing measure details</returns>
    public async Task<CallToolResponse> GetMeasureDetails(string measureName)
    {
        string dax = string.Join(
            Environment.NewLine,
            "EVALUATE",
            $"VAR t = FILTER(INFO.VIEW.MEASURES(), [Name] = \"{measureName.Replace("\"", "\"\"")}\")",
            $"VAR d = COUNTROWS(FILTER(INFO.DEPENDENCIES(), " +
                $"[OBJECT_TYPE] = \"Measure\" && [OBJECT] = \"{measureName.Replace("\"", "\"\"")}\"))",
            "RETURN ADDCOLUMNS(t, \"Dependencies\", d)");

        var result = await Safe(() => _tabular.ExecAsync(dax));
        return Wrap(dax, result);
    }

    /// <summary>
    /// Lists all tables in the model
    /// </summary>
    /// <returns>Response containing the list of tables</returns>
    public async Task<CallToolResponse> ListTables()
    {
        string dax = "EVALUATE INFO.VIEW.TABLES()";
        var result = await Safe(() => _tabular.ExecAsync(dax));
        return Wrap(dax, result);
    }

    /// <summary>
    /// Gets detailed information about a specific table
    /// </summary>
    /// <param name="tableName">Name of the table</param>
    /// <returns>Response containing table details</returns>
    public async Task<CallToolResponse> GetTableDetails(string tableName)
    {
        string dax = string.Join(
            Environment.NewLine,
            "EVALUATE",
            $"VAR t = FILTER(INFO.VIEW.TABLES(), [Name] = \"{tableName.Replace("\"", "\"\"")}\")",
            $"VAR r = COUNTROWS(FILTER(INFO.VIEW.RELATIONSHIPS(), " +
                $"[FromTable] = \"{tableName.Replace("\"", "\"\"")}\" || " +
                $"[ToTable] = \"{tableName.Replace("\"", "\"\"")}\"))",
            $"VAR m = COUNTROWS(FILTER(INFO.VIEW.MEASURES(), [Table] = \"{tableName.Replace("\"", "\"\"")}\"))",
            "RETURN ADDCOLUMNS(t, \"RelationshipCount\", r, \"MeasureCount\", m)");

        var result = await Safe(() => _tabular.ExecAsync(dax));
        return Wrap(dax, result);
    }

    /// <summary>
    /// Gets all columns for a specific table
    /// </summary>
    /// <param name="tableName">Name of the table</param>
    /// <returns>Response containing table columns</returns>
    public async Task<CallToolResponse> GetTableColumns(string tableName)
    {
        string filter = $"SEARCH(\"{tableName.Replace("\"", "\"\"")}\",[Table],1,0)>0";
        string dax = $"EVALUATE FILTER(INFO.VIEW.COLUMNS(), {filter})";
        var result = await Safe(() => _tabular.ExecAsync(dax));
        return Wrap(dax, result);
    }

    /// <summary>
    /// Gets all relationships for a specific table
    /// </summary>
    /// <param name="tableName">Name of the table</param>
    /// <returns>Response containing table relationships</returns>
    public async Task<CallToolResponse> GetTableRelationships(string tableName)
    {
        string filterFrom = $"SEARCH(\"{tableName.Replace("\"", "\"\"")}\",[FromTable],1,0)>0";
        string filterTo = $"SEARCH(\"{tableName.Replace("\"", "\"\"")}\",[ToTable],1,0)>0";
        string dax = $"EVALUATE FILTER(INFO.VIEW.RELATIONSHIPS(), {filterFrom} || {filterTo})";
        var result = await Safe(() => _tabular.ExecAsync(dax));
        return Wrap(dax, result);
    }

    /// <summary>
    /// Previews data from a table
    /// </summary>
    /// <param name="tableName">Name of the table</param>
    /// <param name="topN">Maximum number of rows to return (default 10)</param>
    /// <returns>Response containing preview data</returns>
    public async Task<CallToolResponse> PreviewTableData(string tableName, int topN = 10)
    {
        int n = Math.Min(topN, 10);
        string dax = $"EVALUATE TOPN({n}, '{tableName.Replace("'", "''")}')";
        var result = await Safe(() => _tabular.ExecAsync(dax));
        return Wrap(dax, result);
    }

    /// <summary>
    /// Evaluates a DAX expression
    /// </summary>
    /// <param name="expression">The DAX expression to evaluate</param>
    /// <param name="topN">Maximum number of rows to return for table results (default 10)</param>
    /// <returns>Response containing evaluation results</returns>
    public async Task<CallToolResponse> EvaluateDAX(string expression, int topN = 10)
    {
        string trimmed = expression.TrimStart();
        string dax;

        // If the caller already supplied a full DAX query (starts with EVALUATE),
        // run it unchanged; otherwise assume a scalar/expression and wrap it so
        // that the engine always receives a *table* query
        if (trimmed.StartsWith("EVALUATE", StringComparison.OrdinalIgnoreCase))
        {
            dax = trimmed;
        }
        else
        {
            // Scalar-to-table wrapper (ROW returns a one-row, one-column table)
            dax = $"EVALUATE ROW(\"Value\", {expression})";
        }

        var result = await Safe(() => _tabular.ExecAsync(dax));
        return Wrap(dax, result);
    }
}
using System.ComponentModel;
using Microsoft.Extensions.Logging; // Added for ILogger
using ModelContextProtocol.Server;
using pbi_local_mcp.Core; // Added for ITabularConnection

namespace pbi_local_mcp;

/// <summary>
/// DAX Tools exposed as MCP server tools.
/// </summary>
[McpServerToolType]
public class DaxTools // Changed from static class
{
    private readonly ITabularConnection _tabularConnection;
    private readonly ILogger<DaxTools> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DaxTools"/> class.
    /// </summary>
    /// <param name="tabularConnection">The tabular connection service.</param>
    /// <param name="logger">The logger service.</param>
    public DaxTools(ITabularConnection tabularConnection, ILogger<DaxTools> logger)
    {
        _tabularConnection = tabularConnection ?? throw new ArgumentNullException(nameof(tabularConnection));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // Removed private static TabularConnection CreateConnection()

    [McpServerTool, Description("List all measures in the model, optionally filtered by table name.")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public async Task<object> ListMeasures(string? tableName = null) // Removed static
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        // var tabular = CreateConnection(); // Replaced
        string dmv;

        if (!string.IsNullOrEmpty(tableName))
        {
            var tableIdQuery = $"SELECT [ID] FROM $SYSTEM.TMSCHEMA_TABLES WHERE [NAME] = '{tableName}'";
            var tableIdResult = await _tabularConnection.ExecAsync(tableIdQuery, QueryType.DMV); // Use injected _tabularConnection

            if (tableIdResult is IEnumerable<Dictionary<string, object?>> rows && rows.Any())
            {
                var tableIdObj = rows.First()["ID"];
                if (tableIdObj != null && int.TryParse(tableIdObj.ToString(), out int actualTableId))
                {
                    dmv = $"SELECT * FROM $SYSTEM.TMSCHEMA_MEASURES WHERE [TableID] = {actualTableId}";
                }
                else
                {
                    throw new Exception($"Invalid or non-integer Table ID for table '{tableName}'. Please check the table configuration.");
                }
            }
            else
            {
                return new List<Dictionary<string, object?>>();
            }
        }
        else
        {
            dmv = "SELECT * FROM $SYSTEM.TMSCHEMA_MEASURES";
        }

        var result = await _tabularConnection.ExecAsync(dmv, QueryType.DMV); // Use injected _tabularConnection
        return result;
    }

    [McpServerTool, Description("Get details for a specific measure by name.")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public async Task<object> GetMeasureDetails(string measureName) // Removed static
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        // var tabular = CreateConnection(); // Replaced
        var dmv = $"SELECT * FROM $SYSTEM.TMSCHEMA_MEASURES WHERE [NAME] = '{measureName}'";
        var result = await _tabularConnection.ExecAsync(dmv, QueryType.DMV); // Use injected _tabularConnection
        return result;
    }

    [McpServerTool, Description("List all tables in the model.")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public async Task<object> ListTables() // Removed static
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        // var tabular = CreateConnection(); // Replaced
        var dmv = "SELECT * FROM $SYSTEM.TMSCHEMA_TABLES";
        var result = await _tabularConnection.ExecAsync(dmv, QueryType.DMV); // Use injected _tabularConnection
        return result;
    }

    [McpServerTool, Description("Get details for a specific table by name.")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public async Task<object> GetTableDetails(string tableName) // Removed static
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        // var tabular = CreateConnection(); // Replaced
        var dmv = $"SELECT * FROM $SYSTEM.TMSCHEMA_TABLES WHERE [NAME] = '{tableName}'";
        var result = await _tabularConnection.ExecAsync(dmv, QueryType.DMV); // Use injected _tabularConnection
        return result;
    }

    [McpServerTool, Description("Get columns for a specific table by name.")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public async Task<object> GetTableColumns(string tableName) // Removed static
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        // var tabular = CreateConnection(); // Replaced
        var tableIdQuery = $"SELECT [ID] FROM $SYSTEM.TMSCHEMA_TABLES WHERE [NAME] = '{tableName}'";
        var tableIdResult = await _tabularConnection.ExecAsync(tableIdQuery, QueryType.DMV); // Use injected _tabularConnection
        var tableId = tableIdResult is IEnumerable<Dictionary<string, object?>> rows && rows.Any()
            ? rows.First()["ID"]?.ToString()
            : null;

        if (string.IsNullOrEmpty(tableId))
            throw new Exception($"Table ID not found for table '{tableName}'");

        var dmv = $"SELECT * FROM $SYSTEM.TMSCHEMA_COLUMNS WHERE [TABLEID] = '{tableId}'";
        var result = await _tabularConnection.ExecAsync(dmv, QueryType.DMV); // Use injected _tabularConnection
        return result;
    }

    [McpServerTool, Description("Get relationships for a specific table by name.")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public async Task<object> GetTableRelationships(string tableName) // Removed static
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        // var tabular = CreateConnection(); // Replaced
        var tableIdQuery = $"SELECT [ID] FROM $SYSTEM.TMSCHEMA_TABLES WHERE [NAME] = '{tableName}'";
        var tableIdResult = await _tabularConnection.ExecAsync(tableIdQuery, QueryType.DMV); // Use injected _tabularConnection
        var tableId = tableIdResult is IEnumerable<Dictionary<string, object?>> rows && rows.Any()
            ? rows.First()["ID"]?.ToString()
            : null;

        if (string.IsNullOrEmpty(tableId))
            throw new Exception($"Table ID not found for table '{tableName}'");

        var dmv = $"SELECT * FROM $SYSTEM.TMSCHEMA_RELATIONSHIPS WHERE [FROMTABLEID] = '{tableId}' OR [TOTABLEID] = '{tableId}'";
        var result = await _tabularConnection.ExecAsync(dmv, QueryType.DMV); // Use injected _tabularConnection
        return result;
    }

    [McpServerTool, Description("Preview data from a table (top N rows).")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public async Task<object> PreviewTableData(string tableName, int topN = 10) // Removed static
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        // var tabular = CreateConnection(); // Replaced
        var dax = $"EVALUATE TOPN({topN}, '{tableName}')";
        var result = await _tabularConnection.ExecAsync(dax); // Use injected _tabularConnection
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
    public async Task<object> RunQuery(string dax, int topN = 10) // Removed static
    {
        // var tabular = CreateConnection(); // Replaced
        string originalDax = dax; 
        string query = dax.Trim();

        var initialValidationErrors = new List<string>();
        if (string.IsNullOrWhiteSpace(query))
        {
            initialValidationErrors.Add("Query cannot be empty.");
        }
        else
        {
            CheckBalancedDelimiters(query, '(', ')', "parentheses", initialValidationErrors);
            CheckBalancedDelimiters(query, '[', ']', "brackets", initialValidationErrors);
            CheckBalancedQuotes(query, initialValidationErrors);
        }

        if (initialValidationErrors.Any())
        {
            throw new ArgumentException(string.Join(" ", initialValidationErrors));
        }

        if (query.ToUpperInvariant().Contains("DEFINE"))
        {
            ValidateCompleteDAXQuery(query); 
        }

        try
        {
            if (query.StartsWith("DEFINE", StringComparison.OrdinalIgnoreCase))
            {
                var result = await _tabularConnection.ExecAsync(query, QueryType.DAX); // Use injected _tabularConnection
                return result;
            }

            if (query.StartsWith("EVALUATE", StringComparison.OrdinalIgnoreCase))
            {
                var result = await _tabularConnection.ExecAsync(query, QueryType.DAX); // Use injected _tabularConnection
                return result;
            }

            var evaluateStatement = ConstructEvaluateStatement(query, topN);
            var result2 = await _tabularConnection.ExecAsync(evaluateStatement, QueryType.DAX); // Use injected _tabularConnection
            return result2;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing DAX query: {OriginalQuery} (Processed: {ProcessedQuery})", originalDax, query);
            if (ex is DaxQueryExecutionException) // Check if it's already our custom exception
            {
                throw; // Re-throw it directly to preserve type and properties
            }
            // For other exceptions, wrap them as before, or consider a more specific DaxTools-level exception
            throw new Exception($"Error executing DAX query: {ex.Message}\n\nOriginal Query:\n{originalDax}", ex);
        }
    }

    /// <summary>
    /// Validates the structure of a DAX query according to proper syntax rules.
    /// Throws ArgumentException if validation fails.
    /// </summary>
    /// <param name="query">The DAX query to validate.</param>
    private static void ValidateCompleteDAXQuery(string query) // Kept static as it's a utility
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(query))
        {
            errors.Add("Query cannot be empty.");
        }
        else
        {
            var normalizedQuery = NormalizeDAXQuery(query);

            if (!normalizedQuery.Contains("EVALUATE", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("DAX query must contain at least one EVALUATE statement.");
            }

            if (normalizedQuery.Contains("DEFINE", StringComparison.OrdinalIgnoreCase))
            {
                int definePos = normalizedQuery.IndexOf("DEFINE", StringComparison.OrdinalIgnoreCase);
                int evaluatePos = normalizedQuery.IndexOf("EVALUATE", StringComparison.OrdinalIgnoreCase); 

                if (evaluatePos != -1 && definePos > evaluatePos) 
                {
                    errors.Add("DEFINE statement must come before any EVALUATE statement.");
                }

                var defineMatches = System.Text.RegularExpressions.Regex.Matches(normalizedQuery, @"\bDEFINE\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (defineMatches.Count > 1)
                {
                    errors.Add("Only one DEFINE block is allowed in a DAX query.");
                }
                
                var defineContentMatch = System.Text.RegularExpressions.Regex.Match(
                    normalizedQuery,
                    @"\bDEFINE\b\s*(?:MEASURE|VAR|TABLE|COLUMN)\s+",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline
                );

                if (!defineContentMatch.Success) 
                {
                    var defineBlockContentPattern = @"\bDEFINE\b(.*?)(?=\bEVALUATE\b|$)";
                    var defineBlockMatch = System.Text.RegularExpressions.Regex.Match(normalizedQuery, defineBlockContentPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);
                    if (defineBlockMatch.Success && string.IsNullOrWhiteSpace(defineBlockMatch.Groups[1].Value))
                    {
                        errors.Add("DEFINE block must contain at least one definition (MEASURE, VAR, TABLE, or COLUMN).");
                    }
                    else if (defineBlockMatch.Success) 
                    {
                        string defineContent = defineBlockMatch.Groups[1].Value.Trim();
                        if (!string.IsNullOrEmpty(defineContent) &&
                            !System.Text.RegularExpressions.Regex.IsMatch(defineContent, @"^\s*(MEASURE|VAR|TABLE|COLUMN)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                        {
                            errors.Add("DEFINE block must contain at least one valid definition (MEASURE, VAR, TABLE, or COLUMN).");
                        }
                    }
                }
            }

            CheckBalancedDelimiters(normalizedQuery, '(', ')', "parentheses", errors);
            CheckBalancedDelimiters(normalizedQuery, '[', ']', "brackets", errors);
            CheckBalancedQuotes(normalizedQuery, errors);
        }

        if (errors.Any())
        {
            throw new ArgumentException(string.Join(" ", errors));
        }
    }

    /// <summary>
    /// Normalizes a DAX query by standardizing whitespace and line endings.
    /// </summary>
    private static string NormalizeDAXQuery(string query) // Kept static
    {
        var normalized = System.Text.RegularExpressions.Regex.Replace(query, @"\r\n?|\n", "\n");
        normalized = NormalizeWhitespacePreservingStrings(normalized);
        return normalized.Trim(); 
    }

    /// <summary>
    /// Helper to normalize whitespace while preserving strings.
    /// Collapses multiple whitespace characters into a single space outside of strings.
    /// </summary>
    private static string NormalizeWhitespacePreservingStrings(string input) // Kept static
    {
        var result = new System.Text.StringBuilder();
        bool inString = false;
        char stringDelimiter = '"'; 
        bool lastCharWasWhitespace = false;

        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];

            if (!inString && (c == '"' || c == '\''))
            {
                if (c == '\'' && i + 1 < input.Length && input[i+1] == '\'')
                {
                }


                if (c == '"') 
                {
                    inString = true;
                    stringDelimiter = c;
                    result.Append(c);
                    lastCharWasWhitespace = false;
                    continue;
                }
            }
            else if (inString && c == stringDelimiter)
            {
                if (c == '"' && i + 1 < input.Length && input[i+1] == '"')
                {
                    result.Append(c); 
                    result.Append(input[i+1]); 
                    i++; 
                    lastCharWasWhitespace = false;
                    continue;
                }
                inString = false;
                result.Append(c);
                lastCharWasWhitespace = false;
                continue;
            }

            if (inString)
            {
                result.Append(c);
                lastCharWasWhitespace = false;
            }
            else
            {
                if (char.IsWhiteSpace(c))
                {
                    if (!lastCharWasWhitespace)
                    {
                        result.Append(' ');
                        lastCharWasWhitespace = true;
                    }
                }
                else
                {
                    result.Append(c);
                    lastCharWasWhitespace = false;
                }
            }
        }
        return result.ToString();
    }


    /// <summary>
    /// Checks if delimiters like parentheses and brackets are properly balanced.
    /// Skips delimiters found within string literals.
    /// </summary>
    private static void CheckBalancedDelimiters(string query, char openChar, char closeChar, string delimiterName, List<string> errors) // Kept static
    {
        int balance = 0;
        bool inString = false;
        char stringDelimiter = '\0'; 

        for (int i = 0; i < query.Length; i++)
        {
            char c = query[i];

            if (inString)
            {
                if (c == stringDelimiter)
                {
                    if (i + 1 < query.Length && query[i + 1] == stringDelimiter)
                    {
                        i++; 
                    }
                    else
                    {
                        inString = false;
                        stringDelimiter = '\0';
                    }
                }
            }
            else
            {
                if (c == '"' || c == '\'')
                {
                    inString = true;
                    stringDelimiter = c;
                }
                else if (c == openChar)
                {
                    balance++;
                }
                else if (c == closeChar)
                {
                    balance--;
                    if (balance < 0)
                    {
                        errors.Add($"DAX query has unbalanced {delimiterName}: extra '{closeChar}' found.");
                        return; 
                    }
                }
            }
        }

        if (balance > 0)
        {
            errors.Add($"DAX query has unbalanced {delimiterName}: {balance} '{openChar}' not closed.");
        }
    }


    /// <summary>
    /// Checks if string delimiters (quotes) are properly balanced.
    /// DAX uses " for string literals and ' for table/column names (which can contain spaces).
    /// Escaped quotes ("" inside strings, '' inside identifiers though less common) are handled.
    /// </summary>
    private static void CheckBalancedQuotes(string query, List<string> errors) // Kept static
    {
        bool inDoubleQuoteString = false;
        bool inSingleQuoteIdentifier = false; 

        for (int i = 0; i < query.Length; i++)
        {
            char c = query[i];

            if (c == '"')
            {
                if (inSingleQuoteIdentifier) continue; 

                if (i + 1 < query.Length && query[i + 1] == '"')
                {
                    i++; 
                }
                else
                {
                    inDoubleQuoteString = !inDoubleQuoteString;
                }
            }
            else if (c == '\'')
            {
                if (inDoubleQuoteString) continue; 
                inSingleQuoteIdentifier = !inSingleQuoteIdentifier;
            }
        }

        if (inDoubleQuoteString)
        {
            errors.Add("DAX query has unbalanced double quotes: a string literal is not properly closed.");
        }
        if (inSingleQuoteIdentifier)
        {
            errors.Add("DAX query has unbalanced single quotes: a table/column identifier might not be properly closed.");
        }
    }


    /// <summary>
    /// Constructs an EVALUATE statement based on the query and topN value.
    /// </summary>
    /// <param name="query">The core query expression.</param>
    /// <param name="topN">Maximum number of rows to return (default: 10).</param>
    /// <returns>The constructed EVALUATE statement.</returns>
    private static string ConstructEvaluateStatement(string query, int topN) // Kept static
    {
        query = query.Trim();
        bool isCoreQueryTableExpr = query.StartsWith("'") || 
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
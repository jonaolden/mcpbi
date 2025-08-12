// File: DaxTools.cs
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

    [McpServerTool, Description("List all measures in the model with essential information (name, table, data type, visibility), optionally filtered by table name. Use GetMeasureDetails for full DAX expressions.")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public async Task<object> ListMeasures(
        [Description("Optional table name to filter measures. If null, returns all measures.")] string? tableName = null)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        try
        {
            _logger.LogDebug("Starting ListMeasures with tableName: {TableName}", tableName ?? "ALL");

            // Build query with essential columns only (no DAX expressions)
            string daxQuery;
            var selectColumns = @"
                SELECTCOLUMNS(
                    INFO.VIEW.MEASURES(),
                    ""Name"", [Name],
                    ""Table"", [Table],
                    ""DataType"", [DataType],
                    ""IsHidden"", [IsHidden],
                    ""FormatString"", [FormatString],
                    ""Description"", [Description]
                )";

            if (!string.IsNullOrEmpty(tableName))
            {
                if (!DaxSecurityUtils.IsValidIdentifier(tableName))
                    throw new ArgumentException("Invalid table name format", nameof(tableName));

                var escapedTableName = $"\"{tableName.Replace("\"", "\"\"")}\"";
                daxQuery = $"EVALUATE FILTER({selectColumns}, [Table] = {escapedTableName})";
            }
            else
            {
                daxQuery = $"EVALUATE {selectColumns}";
            }

            _logger.LogDebug("Executing ListMeasures query: {Query}", daxQuery);
            var result = await _tabularConnection.ExecAsync(daxQuery, QueryType.DAX);
            _logger.LogDebug("Successfully executed ListMeasures");

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ListMeasures for tableName: {TableName}", tableName);
            throw new Exception($"Failed to list measures: {ex.Message}", ex);
        }
    }

    [McpServerTool, Description("Get details for a specific measure by name.")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public async Task<object> GetMeasureDetails(
        [Description("Name of the measure to get details for")] string measureName)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        try
        {
            if (!DaxSecurityUtils.IsValidIdentifier(measureName))
                throw new ArgumentException("Invalid measure name format", nameof(measureName));

            _logger.LogDebug("Starting GetMeasureDetails for measure: {MeasureName}", measureName);

            // For string comparison in DAX, we need double quotes, not single quotes
            var escapedMeasureName = $"\"{measureName.Replace("\"", "\"\"")}\"";
            var daxQuery = $"EVALUATE FILTER(INFO.VIEW.MEASURES(), [Name] = {escapedMeasureName})";

            _logger.LogDebug("Executing INFO.VIEW.MEASURES query: {Query}", daxQuery);
            var result = await _tabularConnection.ExecAsync(daxQuery, QueryType.DAX);
            _logger.LogDebug("Successfully executed GetMeasureDetails for measure: {MeasureName}", measureName);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetMeasureDetails for measure: {MeasureName}", measureName);
            throw new Exception($"Failed to get details for measure '{measureName}': {ex.Message}", ex);
        }
    }

    [McpServerTool, Description("List all tables in the model.")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public async Task<object> ListTables()
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        try
        {
            _logger.LogDebug("Starting ListTables");

            var daxQuery = "EVALUATE INFO.VIEW.TABLES()";

            _logger.LogDebug("Executing INFO.VIEW.TABLES query: {Query}", daxQuery);
            var result = await _tabularConnection.ExecAsync(daxQuery, QueryType.DAX);
            _logger.LogDebug("Successfully executed ListTables");

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ListTables");
            throw new Exception($"Failed to list tables: {ex.Message}", ex);
        }
    }

    [McpServerTool, Description("Get details for a specific table by name.")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public async Task<object> GetTableDetails(
        [Description("Name of the table to get details for")] string tableName)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        try
        {
            if (!DaxSecurityUtils.IsValidIdentifier(tableName))
                throw new ArgumentException("Invalid table name format", nameof(tableName));

            _logger.LogDebug("Starting GetTableDetails for table: {TableName}", tableName);

            // For string comparison in DAX, we need double quotes, not single quotes
            var escapedTableName = $"\"{tableName.Replace("\"", "\"\"")}\"";
            var daxQuery = $"EVALUATE FILTER(INFO.VIEW.TABLES(), [Name] = {escapedTableName})";

            _logger.LogDebug("Executing INFO.VIEW.TABLES query: {Query}", daxQuery);
            var result = await _tabularConnection.ExecAsync(daxQuery, QueryType.DAX);
            _logger.LogDebug("Successfully executed GetTableDetails for table: {TableName}", tableName);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetTableDetails for table: {TableName}", tableName);
            throw new Exception($"Failed to get details for table '{tableName}': {ex.Message}", ex);
        }
    }

    [McpServerTool, Description("Get columns for a specific table by name.")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public async Task<object> GetTableColumns(
        [Description("Name of the table to get columns for")] string tableName)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        try
        {
            if (!DaxSecurityUtils.IsValidIdentifier(tableName))
                throw new ArgumentException("Invalid table name format", nameof(tableName));

            _logger.LogDebug("Starting GetTableColumns for table: {TableName}", tableName);

            // For string comparison in DAX, we need double quotes, not single quotes
            var escapedTableName = $"\"{tableName.Replace("\"", "\"\"")}\"";
            var daxQuery = $"EVALUATE FILTER(INFO.VIEW.COLUMNS(), [Table] = {escapedTableName})";

            _logger.LogDebug("Executing INFO.VIEW.COLUMNS query: {Query}", daxQuery);
            var result = await _tabularConnection.ExecAsync(daxQuery, QueryType.DAX);
            _logger.LogDebug("Successfully executed GetTableColumns for table: {TableName}", tableName);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetTableColumns for table: {TableName}", tableName);
            throw new Exception($"Failed to get columns for table '{tableName}': {ex.Message}", ex);
        }
    }

    [McpServerTool, Description("Get relationships for a specific table by name.")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public async Task<object> GetTableRelationships(
        [Description("Name of the table to get relationships for")] string tableName)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        try
        {
            if (!DaxSecurityUtils.IsValidIdentifier(tableName))
                throw new ArgumentException("Invalid table name format", nameof(tableName));

            _logger.LogDebug("Starting GetTableRelationships for table: {TableName}", tableName);

            // For string comparison in DAX, we need double quotes, not single quotes
            var escapedTableName = $"\"{tableName.Replace("\"", "\"\"")}\"";
            var daxQuery = $"EVALUATE FILTER(INFO.VIEW.RELATIONSHIPS(), [FromTable] = {escapedTableName} || [ToTable] = {escapedTableName})";

            _logger.LogDebug("Executing INFO.VIEW.RELATIONSHIPS query: {Query}", daxQuery);
            var result = await _tabularConnection.ExecAsync(daxQuery, QueryType.DAX);
            _logger.LogDebug("Successfully executed GetTableRelationships for table: {TableName}", tableName);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetTableRelationships for table: {TableName}", tableName);
            throw new Exception($"Failed to get relationships for table '{tableName}': {ex.Message}", ex);
        }
    }

    [McpServerTool, Description("Preview data from a table (top N rows).")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public async Task<object> PreviewTableData(
        [Description("Name of the table to preview data for")] string tableName,
        [Description("Maximum number of rows to return (default: 10)")] int topN = 10)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        if (!DaxSecurityUtils.IsValidIdentifier(tableName))
            throw new ArgumentException("Invalid table name format", nameof(tableName));

        var escapedTableName = DaxSecurityUtils.EscapeDaxIdentifier(tableName);
        var dax = $"EVALUATE TOPN({topN}, {escapedTableName})";
        var result = await _tabularConnection.ExecAsync(dax);
        return result;
    }

    /// <summary>
    /// Execute a DAX query. Supports complete DAX queries with DEFINE blocks or simple expressions.
    /// </summary>
    /// <param name="dax">The DAX query to execute. Can be a complete query with DEFINE block, an EVALUATE statement, or a simple expression.</param>
    /// <param name="topN">Maximum number of rows to return for table expressions (default: 10). Ignored for complete queries.</param>
    /// <returns>Query execution result or detailed error information</returns>
    [McpServerTool, Description("Execute a DAX query. Supports complete DAX queries with DEFINE blocks, EVALUATE statements, or simple expressions.")]
    public async Task<object> RunQuery(
        [Description("The DAX query to execute. Can be a complete query with DEFINE block, an EVALUATE statement, or a simple expression.")] string dax,
        [Description("Maximum number of rows to return for table expressions (default: 10). Ignored for complete queries.")] int topN = 10)
    {
        string originalDax = dax;

        try
        {
            _logger.LogDebug("Starting RunQuery execution for query: {Query}", originalDax?.Substring(0, Math.Min(100, originalDax?.Length ?? 0)));

            // Input validation
            if (string.IsNullOrWhiteSpace(dax))
            {
                var error = "DAX query cannot be null or empty";
                _logger.LogWarning(error);
                throw new ArgumentException(error, nameof(dax));
            }

            string query = dax.Trim();

            // Pre-execution validation with detailed error reporting
            var validationErrors = ValidateQuerySyntax(query);
            if (validationErrors.Any())
            {
                var validationError = $"Query validation failed: {string.Join("; ", validationErrors)}";
                _logger.LogWarning("Query validation failed for query: {Query}. Errors: {Errors}",
                    originalDax, string.Join("; ", validationErrors));
                throw new ArgumentException(validationError, nameof(dax));
            }

            // Additional validation for complete DAX queries
            if (query.Contains("DEFINE", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    ValidateCompleteDAXQuery(query);
                }
                catch (ArgumentException validationEx)
                {
                    _logger.LogWarning("Complete DAX query validation failed: {Error}", validationEx.Message);
                    throw new ArgumentException($"DAX query structure validation failed: {validationEx.Message}", nameof(dax), validationEx);
                }
            }

            // Determine query type and construct final query
            string finalQuery;
            QueryType queryType;

            if (query.StartsWith("DEFINE", StringComparison.OrdinalIgnoreCase))
            {
                finalQuery = query;
                queryType = QueryType.DAX;
                _logger.LogDebug("Executing DEFINE query as DAX");
            }
            else if (query.StartsWith("EVALUATE", StringComparison.OrdinalIgnoreCase))
            {
                finalQuery = query;
                queryType = QueryType.DAX;
                _logger.LogDebug("Executing EVALUATE query as DAX");
            }
            else
            {
                // Construct EVALUATE statement for simple expressions
                try
                {
                    finalQuery = ConstructEvaluateStatement(query, topN);
                    queryType = QueryType.DAX;
                    _logger.LogDebug("Constructed EVALUATE statement for simple expression: {FinalQuery}",
                        finalQuery.Substring(0, Math.Min(100, finalQuery.Length)));
                }
                catch (Exception constructEx)
                {
                    _logger.LogError(constructEx, "Failed to construct EVALUATE statement for query: {Query}", originalDax);
                    throw new ArgumentException($"Failed to construct valid DAX query from expression: {constructEx.Message}", nameof(dax), constructEx);
                }
            }

            // Execute the query with enhanced error handling
            try
            {
                _logger.LogDebug("Executing query with QueryType: {QueryType}", queryType);
                var result = await _tabularConnection.ExecAsync(finalQuery, queryType);
                _logger.LogDebug("Query execution completed successfully");
                return result;
            }
            catch (Exception execEx)
            {
                // Log detailed execution error information
                _logger.LogError(execEx, "Query execution failed. Original: {OriginalQuery}, Final: {FinalQuery}, QueryType: {QueryType}",
                    originalDax, finalQuery, queryType);

                // Return structured error information instead of throwing
                return CreateStructuredErrorResponse(execEx, originalDax, finalQuery, queryType, "execution");
            }
        }
        catch (ArgumentException validationEx)
        {
            // Log validation errors
            _logger.LogWarning(validationEx, "Query validation failed for query: {Query}", originalDax);
            
            // Return structured validation error information
            return CreateStructuredErrorResponse(validationEx, originalDax, originalDax, QueryType.DAX, "validation");
        }
        catch (Exception ex)
        {
            // Log unexpected errors and return structured error information
            _logger.LogError(ex, "Unexpected error in RunQuery for query: {Query}", originalDax);

            // Return structured error information for unexpected errors
            return CreateStructuredErrorResponse(ex, originalDax, originalDax, QueryType.DAX, "unexpected");
        }
    }

    /// <summary>
    /// Validates basic DAX query syntax and returns a list of validation errors.
    /// </summary>
    /// <param name="query">The DAX query to validate.</param>
    /// <returns>List of validation error messages. Empty list if validation passes.</returns>
    private static List<string> ValidateQuerySyntax(string query)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(query))
        {
            errors.Add("Query cannot be empty");
            return errors;
        }

        try
        {
            // Check balanced delimiters
            CheckBalancedDelimiters(query, '(', ')', "parentheses", errors);
            CheckBalancedDelimiters(query, '[', ']', "brackets", errors);
            CheckBalancedQuotes(query, errors);
        }
        catch (Exception ex)
        {
            errors.Add($"Syntax validation error: {ex.Message}");
        }

        return errors;
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
                if (c == '\'' && i + 1 < input.Length && input[i + 1] == '\'')
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
                if (c == '"' && i + 1 < input.Length && input[i + 1] == '"')
                {
                    result.Append(c);
                    result.Append(input[i + 1]);
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
    /// <exception cref="ArgumentException">Thrown when query construction fails.</exception>
    private static string ConstructEvaluateStatement(string query, int topN) // Kept static
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException("Query expression cannot be empty", nameof(query));
        }

        if (topN < 0)
        {
            throw new ArgumentException("TopN value cannot be negative", nameof(topN));
        }

        try
        {
            query = query.Trim();

            // Check if the query is a table expression
            bool isCoreQueryTableExpr = IsTableExpression(query);

            string constructedQuery;
            if (isCoreQueryTableExpr)
            {
                // For table expressions, use TOPN if specified
                if (topN > 0)
                {
                    constructedQuery = $"EVALUATE TOPN({topN}, {query})";
                }
                else
                {
                    constructedQuery = $"EVALUATE {query}";
                }
            }
            else
            {
                // For scalar expressions, wrap in ROW
                constructedQuery = $"EVALUATE ROW(\"Value\", {query})";
            }

            // Basic validation of constructed query
            if (string.IsNullOrWhiteSpace(constructedQuery) || !constructedQuery.Contains("EVALUATE"))
            {
                throw new InvalidOperationException("Failed to construct valid EVALUATE statement");
            }

            return constructedQuery;
        }
        catch (Exception ex) when (!(ex is ArgumentException))
        {
            throw new ArgumentException($"Failed to construct EVALUATE statement: {ex.Message}", nameof(query), ex);
        }
    }

    /// <summary>
    /// Determines if a DAX expression is a table expression or a scalar expression.
    /// </summary>
    /// <param name="query">The DAX expression to analyze.</param>
    /// <returns>True if the expression is likely a table expression, false if it's a scalar expression.</returns>
    private static bool IsTableExpression(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return false;

        query = query.Trim();

        // Check for table reference patterns
        if (query.StartsWith("'") && query.EndsWith("'"))
            return true;

        // Check for common table functions
        var tableExpressionPatterns = new[]
        {
            "SELECTCOLUMNS", "ADDCOLUMNS", "SUMMARIZE", "FILTER", "VALUES", "ALL",
            "DISTINCT", "UNION", "INTERSECT", "EXCEPT", "CROSSJOIN", "NATURALINNERJOIN",
            "NATURALLEFTOUTERJOIN", "TOPN", "SAMPLE", "DATATABLE", "SUBSTITUTEWITHINDEX",
            "GROUPBY", "SUMMARIZECOLUMNS", "TREATAS", "CALCULATETABLE"
        };

        foreach (var pattern in tableExpressionPatterns)
        {
            if (query.StartsWith(pattern, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        // Check for calculated table patterns like { ... }
        if (query.StartsWith("{") && query.EndsWith("}"))
            return true;

        return false;
    }

    // New Tools Implementation - Based on Additional Tools Recommendations


    [McpServerTool, Description("Validate DAX syntax and identify potential issues with enhanced error analysis.")]
    public async Task<object> ValidateDaxSyntax(
        [Description("DAX expression to validate")] string daxExpression,
        [Description("Include performance and best practice recommendations")] bool includeRecommendations = true)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(daxExpression))
                throw new ArgumentException("DAX expression cannot be empty", nameof(daxExpression));

            // Basic syntax validation
            var syntaxErrors = new List<string>();
            var warnings = new List<string>();
            var recommendations = new List<string>();

            // Check balanced delimiters
            CheckBalancedDelimiters(daxExpression, '(', ')', "parentheses", syntaxErrors);
            CheckBalancedDelimiters(daxExpression, '[', ']', "brackets", syntaxErrors);
            CheckBalancedQuotes(daxExpression, syntaxErrors);

            // Check for common DAX patterns and issues
            AnalyzeDaxPatterns(daxExpression, warnings, recommendations, includeRecommendations);

            // Try to execute a simple validation query
            bool executionValid = false;
            string executionError = "";

            try
            {
                var testQuery = $"EVALUATE ROW(\"ValidationTest\", {daxExpression})";
                await _tabularConnection.ExecAsync(testQuery, QueryType.DAX);
                executionValid = true;
            }
            catch (Exception ex)
            {
                executionError = ex.Message;
                syntaxErrors.Add($"Execution validation failed: {ex.Message}");
            }

            // Calculate complexity metrics
            var complexityMetrics = CalculateDaxComplexity(daxExpression);

            return new
            {
                Expression = daxExpression.Trim(),
                IsValid = !syntaxErrors.Any() && executionValid,
                SyntaxErrors = syntaxErrors,
                Warnings = warnings,
                Recommendations = includeRecommendations ? recommendations : new List<string>(),
                ComplexityMetrics = complexityMetrics,
                ValidationDetails = new
                {
                    ExecutionValid = executionValid,
                    ExecutionError = executionError,
                    AnalyzedAt = DateTime.UtcNow,
                    ExpressionLength = daxExpression.Length
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating DAX syntax for expression: {Expression}", daxExpression);
            throw;
        }
    }

    [McpServerTool, Description("Analyze query performance characteristics and identify potential bottlenecks.")]
    public async Task<object> AnalyzeQueryPerformance(
        [Description("DAX query to analyze")] string daxQuery,
        [Description("Include complexity metrics and optimization suggestions")] bool includeOptimizations = true)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(daxQuery))
                throw new ArgumentException("DAX query cannot be empty", nameof(daxQuery));

            var startTime = DateTime.UtcNow;
            object? queryResult = null;
            string executionError = "";
            bool executionSuccessful = false;
            TimeSpan executionTime = TimeSpan.Zero;

            // Execute the query and measure performance
            try
            {
                var executionStart = DateTime.UtcNow;
                queryResult = await _tabularConnection.ExecAsync(daxQuery, QueryType.DAX);
                executionTime = DateTime.UtcNow - executionStart;
                executionSuccessful = true;
            }
            catch (Exception ex)
            {
                executionError = ex.Message;
                executionTime = DateTime.UtcNow - startTime;
            }

            // Analyze query structure and complexity
            var complexityAnalysis = AnalyzeQueryStructure(daxQuery);
            var performanceMetrics = CalculatePerformanceMetrics(daxQuery, executionTime, executionSuccessful);

            var optimizationSuggestions = new List<string>();
            if (includeOptimizations)
            {
                optimizationSuggestions = GenerateOptimizationSuggestions(daxQuery, complexityAnalysis, performanceMetrics);
            }

            // Count result rows if successful
            int resultRowCount = 0;
            if (executionSuccessful && queryResult is IEnumerable<Dictionary<string, object?>> rows)
            {
                resultRowCount = rows.Count();
            }

            return new
            {
                Query = daxQuery.Trim(),
                ExecutionSuccessful = executionSuccessful,
                ExecutionTime = new
                {
                    TotalMilliseconds = executionTime.TotalMilliseconds,
                    TotalSeconds = executionTime.TotalSeconds,
                    DisplayTime = $"{executionTime.TotalMilliseconds:F2} ms"
                },
                ExecutionError = executionError,
                ResultRowCount = resultRowCount,
                PerformanceMetrics = performanceMetrics,
                ComplexityAnalysis = complexityAnalysis,
                OptimizationSuggestions = includeOptimizations ? optimizationSuggestions : new List<string>(),
                AnalysisDetails = new
                {
                    AnalyzedAt = DateTime.UtcNow,
                    QueryLength = daxQuery.Length,
                    HasTimeIntelligence = daxQuery.Contains("CALCULATE", StringComparison.OrdinalIgnoreCase) ||
                                        daxQuery.Contains("FILTER", StringComparison.OrdinalIgnoreCase),
                    HasAggregations = daxQuery.Contains("SUM", StringComparison.OrdinalIgnoreCase) ||
                                    daxQuery.Contains("COUNT", StringComparison.OrdinalIgnoreCase) ||
                                    daxQuery.Contains("AVERAGE", StringComparison.OrdinalIgnoreCase)
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing query performance for query: {Query}", daxQuery);
            throw;
        }
    }

    // Helper methods for analysis tools

    private static void AnalyzeDaxPatterns(string expression, List<string> warnings, List<string> recommendations, bool includeRecommendations)
    {
        if (string.IsNullOrEmpty(expression))
            return;

        // Check for common anti-patterns
        if (expression.Contains("SUMX", StringComparison.OrdinalIgnoreCase) && expression.Contains("FILTER", StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add("SUMX with FILTER detected - consider using CALCULATE for better performance");
        }

        if (System.Text.RegularExpressions.Regex.IsMatch(expression, @"CALCULATE\s*\(\s*CALCULATE", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
        {
            warnings.Add("Nested CALCULATE functions detected - this may cause unexpected results");
        }

        var calculateCount = System.Text.RegularExpressions.Regex.Matches(expression, @"\bCALCULATE\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Count;
        if (calculateCount > 3)
        {
            warnings.Add($"High number of CALCULATE functions ({calculateCount}) - consider simplifying the expression");
        }

        if (includeRecommendations)
        {
            if (expression.Contains("SUM", StringComparison.OrdinalIgnoreCase) && !expression.Contains("CALCULATE", StringComparison.OrdinalIgnoreCase))
            {
                recommendations.Add("Consider using CALCULATE with filters instead of basic aggregation for more flexibility");
            }

            if (expression.Length > 500)
            {
                recommendations.Add("Long expression detected - consider breaking into multiple measures for better maintainability");
            }

            if (!expression.Contains("FORMAT", StringComparison.OrdinalIgnoreCase) &&
                (expression.Contains("/", StringComparison.OrdinalIgnoreCase) || expression.Contains("DIVIDE", StringComparison.OrdinalIgnoreCase)))
            {
                recommendations.Add("Consider using FORMAT function for better number presentation in reports");
            }
        }
    }

    private static object CalculateDaxComplexity(string expression)
    {
        if (string.IsNullOrEmpty(expression))
            return new { ComplexityScore = 0, Level = "None" };

        var functionCount = System.Text.RegularExpressions.Regex.Matches(expression, @"\b[A-Z]+\s*\(", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Count;
        var nestedLevels = CountMaxNestingLevel(expression);
        var filterCount = System.Text.RegularExpressions.Regex.Matches(expression, @"\bFILTER\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Count;
        var calculateCount = System.Text.RegularExpressions.Regex.Matches(expression, @"\bCALCULATE\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Count;

        var complexityScore = (functionCount * 2) + (nestedLevels * 3) + (filterCount * 4) + (calculateCount * 2);

        string level = complexityScore switch
        {
            <= 5 => "Low",
            <= 15 => "Medium",
            <= 30 => "High",
            _ => "Very High"
        };

        return new
        {
            ComplexityScore = complexityScore,
            Level = level,
            FunctionCount = functionCount,
            MaxNestingLevel = nestedLevels,
            FilterCount = filterCount,
            CalculateCount = calculateCount,
            ExpressionLength = expression.Length
        };
    }

    private static int CountMaxNestingLevel(string expression)
    {
        int maxLevel = 0;
        int currentLevel = 0;
        bool inString = false;

        foreach (char c in expression)
        {
            if (c == '"' && !inString)
                inString = true;
            else if (c == '"' && inString)
                inString = false;
            else if (!inString)
            {
                if (c == '(')
                {
                    currentLevel++;
                    maxLevel = Math.Max(maxLevel, currentLevel);
                }
                else if (c == ')')
                {
                    currentLevel--;
                }
            }
        }

        return maxLevel;
    }

    private static object AnalyzeQueryStructure(string query)
    {
        if (string.IsNullOrEmpty(query))
            return new { };

        var hasDefine = query.Contains("DEFINE", StringComparison.OrdinalIgnoreCase);
        var hasEvaluate = query.Contains("EVALUATE", StringComparison.OrdinalIgnoreCase);
        var measureCount = System.Text.RegularExpressions.Regex.Matches(query, @"\bMEASURE\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Count;
        var tableCount = System.Text.RegularExpressions.Regex.Matches(query, @"\bTABLE\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Count;

        return new
        {
            HasDefineBlock = hasDefine,
            HasEvaluateStatement = hasEvaluate,
            MeasureDefinitions = measureCount,
            TableDefinitions = tableCount,
            QueryType = hasDefine ? "Complex Query" : hasEvaluate ? "Table Query" : "Expression",
            EstimatedComplexity = (measureCount * 2) + (tableCount * 3) + (hasDefine ? 5 : 0)
        };
    }

    private static object CalculatePerformanceMetrics(string query, TimeSpan executionTime, bool successful)
    {
        var queryLength = query.Length;
        var functionCount = System.Text.RegularExpressions.Regex.Matches(query, @"\b[A-Z]+\s*\(", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Count;

        string performanceRating = "Unknown";
        if (successful)
        {
            performanceRating = executionTime.TotalMilliseconds switch
            {
                < 100 => "Excellent",
                < 500 => "Good",
                < 2000 => "Moderate",
                < 5000 => "Slow",
                _ => "Very Slow"
            };
        }

        return new
        {
            PerformanceRating = performanceRating,
            ExecutionTimeMs = executionTime.TotalMilliseconds,
            QueryComplexityFactor = (queryLength / 100.0) + (functionCount * 0.5),
            FunctionDensity = queryLength > 0 ? (double)functionCount / queryLength * 100 : 0,
            Successful = successful
        };
    }

    private static List<string> GenerateOptimizationSuggestions(string query, object complexityAnalysis, object performanceMetrics)
    {
        var suggestions = new List<string>();

        if (query.Contains("SUMX", StringComparison.OrdinalIgnoreCase) && query.Contains("FILTER", StringComparison.OrdinalIgnoreCase))
        {
            suggestions.Add("Replace SUMX(FILTER(...)) with CALCULATE(SUM(...), Filter) for better performance");
        }

        if (System.Text.RegularExpressions.Regex.Matches(query, @"\bCALCULATE\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Count > 2)
        {
            suggestions.Add("Consider consolidating multiple CALCULATE functions to reduce complexity");
        }

        if (query.Contains("ALL(", StringComparison.OrdinalIgnoreCase) && !query.Contains("CALCULATE", StringComparison.OrdinalIgnoreCase))
        {
            suggestions.Add("Using ALL() without CALCULATE may not provide expected results - consider wrapping in CALCULATE");
        }

        if (query.Length > 1000)
        {
            suggestions.Add("Consider breaking down this large query into smaller, more manageable parts");
        }

        var iteratorFunctions = System.Text.RegularExpressions.Regex.Matches(query, @"\b(SUMX|AVERAGEX|COUNTX|MAXX|MINX)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Count;
        if (iteratorFunctions > 2)
        {
            suggestions.Add("Multiple iterator functions detected - ensure they are necessary and consider alternatives");
        }

        return suggestions;
    }

    /// <summary>
    /// Creates a structured error response that can be properly serialized through MCP protocol
    /// </summary>
    /// <param name="exception">The original exception that occurred</param>
    /// <param name="originalQuery">The original DAX query</param>
    /// <param name="finalQuery">The final processed DAX query</param>
    /// <param name="queryType">The type of query (DAX or DMV)</param>
    /// <param name="errorCategory">The category of error (execution, validation, unexpected)</param>
    /// <returns>Structured error response object</returns>
    private static object CreateStructuredErrorResponse(Exception exception, string originalQuery, string finalQuery, QueryType queryType, string errorCategory)
    {
        var errorType = ClassifyError(exception);
        var suggestions = GetErrorSuggestions(errorType, exception.Message, originalQuery);
        
        return new
        {
            Success = false,
            ErrorCategory = errorCategory,
            ErrorType = errorType,
            ErrorDetails = new
            {
                ExceptionType = exception.GetType().Name,
                Message = exception.Message,
                InnerException = exception.InnerException?.Message,
                StackTrace = exception.StackTrace?.Split('\n').Take(5).ToArray() // First 5 lines only
            },
            QueryInfo = new
            {
                QueryType = queryType.ToString(),
                OriginalLength = originalQuery.Length,
                FinalLength = finalQuery.Length,
                WasModified = originalQuery != finalQuery,
                OriginalQuery = originalQuery.Length > 1000 ? originalQuery.Substring(0, 1000) + "..." : originalQuery,
                FinalQuery = originalQuery != finalQuery ? (finalQuery.Length > 1000 ? finalQuery.Substring(0, 1000) + "..." : finalQuery) : null
            },
            Suggestions = suggestions,
            Timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC"),
            DetailedErrorMessage = CreateDetailedErrorMessage(exception, originalQuery, finalQuery, queryType)
        };
    }

    /// <summary>
    /// Creates a detailed error message for query execution failures with diagnostics and suggestions
    /// </summary>
    /// <param name="exception">The original exception that occurred</param>
    /// <param name="originalQuery">The original DAX query</param>
    /// <param name="finalQuery">The final processed DAX query</param>
    /// <param name="queryType">The type of query (DAX or DMV)</param>
    /// <returns>Comprehensive error message with diagnostics</returns>
    public static string CreateDetailedErrorMessage(Exception exception, string originalQuery, string finalQuery, QueryType queryType)
    {
        var errorBuilder = new System.Text.StringBuilder();
        
        errorBuilder.AppendLine("=== DAX QUERY EXECUTION ERROR ===");
        errorBuilder.AppendLine();
        
        // Error classification
        var errorType = ClassifyError(exception);
        errorBuilder.AppendLine($"Error Classification: {errorType}");
        errorBuilder.AppendLine($"Exception Type: {exception.GetType().Name}");
        errorBuilder.AppendLine($"Error Message: {exception.Message}");
        errorBuilder.AppendLine();
        
        // Query information
        errorBuilder.AppendLine("Query Information:");
        errorBuilder.AppendLine($"   - Query Type: {queryType}");
        errorBuilder.AppendLine($"   - Original Length: {originalQuery.Length} characters");
        errorBuilder.AppendLine($"   - Final Length: {finalQuery.Length} characters");
        errorBuilder.AppendLine($"   - Query Modified: {(originalQuery != finalQuery ? "Yes" : "No")}");
        errorBuilder.AppendLine();
        
        // Original query
        errorBuilder.AppendLine("Original Query:");
        errorBuilder.AppendLine("+" + "-".PadRight(50, '-') + "+");
        var originalLines = originalQuery.Split('\n');
        foreach (var line in originalLines.Take(10)) // Show first 10 lines
        {
            errorBuilder.AppendLine($"| {line.PadRight(50).Substring(0, Math.Min(50, line.Length))}");
        }
        if (originalLines.Length > 10)
        {
            errorBuilder.AppendLine($"| ... ({originalLines.Length - 10} more lines)");
        }
        errorBuilder.AppendLine("+" + "-".PadRight(50, '-') + "+");
        errorBuilder.AppendLine();
        
        // Final query (if different)
        if (originalQuery != finalQuery)
        {
            errorBuilder.AppendLine("Processed Query:");
            errorBuilder.AppendLine("+" + "-".PadRight(50, '-') + "+");
            var finalLines = finalQuery.Split('\n');
            foreach (var line in finalLines.Take(10))
            {
                errorBuilder.AppendLine($"| {line.PadRight(50).Substring(0, Math.Min(50, line.Length))}");
            }
            if (finalLines.Length > 10)
            {
                errorBuilder.AppendLine($"| ... ({finalLines.Length - 10} more lines)");
            }
            errorBuilder.AppendLine("+" + "-".PadRight(50, '-') + "+");
            errorBuilder.AppendLine();
        }
        
        // Suggestions
        var suggestions = GetErrorSuggestions(errorType, exception.Message, originalQuery);
        if (suggestions.Any())
        {
            errorBuilder.AppendLine("Troubleshooting Suggestions:");
            foreach (var suggestion in suggestions)
            {
                errorBuilder.AppendLine($"   - {suggestion}");
            }
            errorBuilder.AppendLine();
        }
        
        // Inner exception details
        if (exception.InnerException != null)
        {
            errorBuilder.AppendLine("Inner Exception Details:");
            errorBuilder.AppendLine($"   Type: {exception.InnerException.GetType().Name}");
            errorBuilder.AppendLine($"   Message: {exception.InnerException.Message}");
            errorBuilder.AppendLine();
        }
        
        errorBuilder.AppendLine("===================================");
        
        return errorBuilder.ToString();
    }

    /// <summary>
    /// Creates a detailed error message for unexpected errors
    /// </summary>
    /// <param name="exception">The unexpected exception</param>
    /// <param name="originalQuery">The original query that caused the error</param>
    /// <returns>Detailed error message for unexpected errors</returns>
    private static string CreateUnexpectedErrorMessage(Exception exception, string originalQuery)
    {
        var errorBuilder = new System.Text.StringBuilder();
        
        errorBuilder.AppendLine("=== UNEXPECTED ERROR ===");
        errorBuilder.AppendLine();
        errorBuilder.AppendLine("An unexpected error occurred while processing your DAX query.");
        errorBuilder.AppendLine();
        errorBuilder.AppendLine($"Exception Type: {exception.GetType().Name}");
        errorBuilder.AppendLine($"Error Message: {exception.Message}");
        errorBuilder.AppendLine();
        errorBuilder.AppendLine("Query That Caused The Error:");
        errorBuilder.AppendLine("+" + "-".PadRight(60, '-') + "+");
        var queryLines = originalQuery.Split('\n');
        foreach (var line in queryLines.Take(15)) // Show more lines for debugging
        {
            var displayLine = line.Length > 60 ? line.Substring(0, 57) + "..." : line;
            errorBuilder.AppendLine($"| {displayLine.PadRight(60)}|");
        }
        if (queryLines.Length > 15)
        {
            errorBuilder.AppendLine($"| ... ({queryLines.Length - 15} more lines)".PadRight(61) + "|");
        }
        errorBuilder.AppendLine("+" + "-".PadRight(60, '-') + "+");
        errorBuilder.AppendLine();
        
        errorBuilder.AppendLine("General Troubleshooting Steps:");
        errorBuilder.AppendLine("   - Check that your Power BI instance is running");
        errorBuilder.AppendLine("   - Verify the connection to the tabular model");
        errorBuilder.AppendLine("   - Ensure the query syntax is valid DAX");
        errorBuilder.AppendLine("   - Try simplifying the query to isolate the issue");
        errorBuilder.AppendLine();
        
        if (exception.InnerException != null)
        {
            errorBuilder.AppendLine("Technical Details:");
            errorBuilder.AppendLine($"   Inner Exception: {exception.InnerException.GetType().Name}");
            errorBuilder.AppendLine($"   Inner Message: {exception.InnerException.Message}");
            errorBuilder.AppendLine();
        }
        
        errorBuilder.AppendLine("=============================");
        
        return errorBuilder.ToString();
    }

    /// <summary>
    /// Classifies the error based on the exception type and message
    /// </summary>
    /// <param name="exception">The exception to classify</param>
    /// <returns>Error classification string</returns>
    public static string ClassifyError(Exception exception)
    {
        var message = exception.Message.ToLowerInvariant();
        var exceptionType = exception.GetType().Name;
        
        if (message.Contains("syntax") || message.Contains("parse"))
            return "Syntax Error";
        if (message.Contains("column") && (message.Contains("not found") || message.Contains("doesn't exist")))
            return "Column Reference Error";
        if (message.Contains("table") && (message.Contains("not found") || message.Contains("doesn't exist")))
            return "Table Reference Error";
        if (message.Contains("measure") && (message.Contains("not found") || message.Contains("doesn't exist")))
            return "Measure Reference Error";
        if (message.Contains("function") && (message.Contains("not found") || message.Contains("unknown")))
            return "Function Error";
        if (message.Contains("connection") || message.Contains("timeout"))
            return "Connection Error";
        if (message.Contains("permission") || message.Contains("access"))
            return "Permission Error";
        if (exceptionType.Contains("Argument"))
            return "Parameter Error";
        if (message.Contains("memory") || message.Contains("resource"))
            return "Resource Error";
            
        return "General Execution Error";
    }

    /// <summary>
    /// Provides specific suggestions based on error type and message
    /// </summary>
    /// <param name="errorType">The classified error type</param>
    /// <param name="errorMessage">The original error message</param>
    /// <param name="query">The query that caused the error</param>
    /// <returns>List of specific suggestions</returns>
    public static List<string> GetErrorSuggestions(string errorType, string errorMessage, string query)
    {
        var suggestions = new List<string>();
        
        switch (errorType)
        {
            case "Syntax Error":
                suggestions.Add("Check for missing or unmatched parentheses, brackets, or quotes");
                suggestions.Add("Verify that all function names are spelled correctly");
                suggestions.Add("Ensure proper comma placement in function parameters");
                break;
                
            case "Column Reference Error":
                suggestions.Add("Verify the column name exists in the specified table");
                suggestions.Add("Check if the column name contains special characters that need escaping");
                suggestions.Add("Use the format 'TableName'[ColumnName] for column references");
                break;
                
            case "Table Reference Error":
                suggestions.Add("Confirm the table name exists in the model");
                suggestions.Add("Check if the table name contains spaces or special characters");
                suggestions.Add("Use single quotes around table names with spaces: 'Table Name'");
                break;
                
            case "Measure Reference Error":
                suggestions.Add("Verify the measure exists and is accessible");
                suggestions.Add("Check measure name spelling and capitalization");
                suggestions.Add("Ensure the measure is not hidden from client tools");
                break;
                
            case "Function Error":
                suggestions.Add("Check if the function name is spelled correctly");
                suggestions.Add("Verify the correct number of parameters for the function");
                suggestions.Add("Ensure you're using a supported DAX function");
                break;
                
            case "Connection Error":
                suggestions.Add("Verify your Power BI Desktop instance is running");
                suggestions.Add("Check that the correct port is being used");
                suggestions.Add("Ensure the tabular model is accessible");
                break;
                
            case "Permission Error":
                suggestions.Add("Check if you have read access to the data model");
                suggestions.Add("Verify row-level security settings if applicable");
                break;
                
            case "Parameter Error":
                suggestions.Add("Check that all required parameters are provided");
                suggestions.Add("Verify parameter data types match expected values");
                break;
                
            case "Resource Error":
                suggestions.Add("Try simplifying the query to reduce memory usage");
                suggestions.Add("Consider breaking complex calculations into smaller parts");
                suggestions.Add("Check if the dataset is too large for the operation");
                break;
                
            default:
                suggestions.Add("Review the error message for specific details");
                suggestions.Add("Try running a simpler version of the query first");
                suggestions.Add("Check the Power BI Desktop connection status");
                break;
        }
        
        // Add query-specific suggestions
        if (query.Contains("DEFINE") && !query.Contains("EVALUATE"))
        {
            suggestions.Add("DEFINE blocks must be followed by an EVALUATE statement");
        }
        
        if (System.Text.RegularExpressions.Regex.Matches(query, @"\(").Count != 
            System.Text.RegularExpressions.Regex.Matches(query, @"\)").Count)
        {
            suggestions.Add("Check for unbalanced parentheses in your query");
        }
        
        return suggestions;
    }
}
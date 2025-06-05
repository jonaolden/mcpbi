// File: DaxTools.cs
using System.ComponentModel;
using Microsoft.Extensions.Logging; // Added for ILogger
using ModelContextProtocol;
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
        [Description("Optional table name to filter measures. If null, returns all measures.")] string? tableName = null) // Removed static
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        // var tabular = CreateConnection(); // Replaced
        string dmv;

        if (!string.IsNullOrEmpty(tableName))
        {
            if (!DaxSecurityUtils.IsValidIdentifier(tableName))
                throw new ArgumentException("Invalid table name format", nameof(tableName));
            
            var escapedTableName = DaxSecurityUtils.EscapeDaxIdentifier(tableName);
            var tableIdQuery = $"SELECT [ID] FROM $SYSTEM.TMSCHEMA_TABLES WHERE [NAME] = {escapedTableName}";
            var tableIdResult = await _tabularConnection.ExecAsync(tableIdQuery, QueryType.DMV); // Use injected _tabularConnection

            if (tableIdResult is IEnumerable<Dictionary<string, object?>> rows && rows.Any())
            {
                var tableIdObj = rows.First()["ID"];
                if (tableIdObj != null && int.TryParse(tableIdObj.ToString(), out int actualTableId))
                {
                    dmv = $"SELECT m.[Name] as MeasureName, m.[TableID], t.[Name] as TableName, m.[DataType], m.[IsHidden] FROM $SYSTEM.TMSCHEMA_MEASURES m LEFT JOIN $SYSTEM.TMSCHEMA_TABLES t ON m.[TableID] = t.[ID] WHERE m.[TableID] = {actualTableId}";
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
            dmv = "SELECT m.[Name] as MeasureName, m.[TableID], t.[Name] as TableName, m.[DataType], m.[IsHidden] FROM $SYSTEM.TMSCHEMA_MEASURES m LEFT JOIN $SYSTEM.TMSCHEMA_TABLES t ON m.[TableID] = t.[ID]";
        }

        var result = await _tabularConnection.ExecAsync(dmv, QueryType.DMV); // Use injected _tabularConnection
        return result;
    }

    [McpServerTool, Description("Get details for a specific measure by name.")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public async Task<object> GetMeasureDetails(
        [Description("Name of the measure to get details for")] string measureName) // Removed static
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        if (!DaxSecurityUtils.IsValidIdentifier(measureName))
            throw new ArgumentException("Invalid measure name format", nameof(measureName));
        
        // var tabular = CreateConnection(); // Replaced
        var escapedMeasureName = DaxSecurityUtils.EscapeDaxIdentifier(measureName);
        var dmv = $"SELECT * FROM $SYSTEM.TMSCHEMA_MEASURES WHERE [NAME] = {escapedMeasureName}";
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
    public async Task<object> GetTableDetails(
        [Description("Name of the table to get details for")] string tableName) // Removed static
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        if (!DaxSecurityUtils.IsValidIdentifier(tableName))
            throw new ArgumentException("Invalid table name format", nameof(tableName));
        
        // var tabular = CreateConnection(); // Replaced
        var escapedTableName = DaxSecurityUtils.EscapeDaxIdentifier(tableName);
        var dmv = $"SELECT * FROM $SYSTEM.TMSCHEMA_TABLES WHERE [NAME] = {escapedTableName}";
        var result = await _tabularConnection.ExecAsync(dmv, QueryType.DMV); // Use injected _tabularConnection
        return result;
    }

    [McpServerTool, Description("Get columns for a specific table by name.")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public async Task<object> GetTableColumns(
        [Description("Name of the table to get columns for")] string tableName) // Removed static
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        if (!DaxSecurityUtils.IsValidIdentifier(tableName))
            throw new ArgumentException("Invalid table name format", nameof(tableName));
        
        // var tabular = CreateConnection(); // Replaced
        var escapedTableName = DaxSecurityUtils.EscapeDaxIdentifier(tableName);
        var tableIdQuery = $"SELECT [ID] FROM $SYSTEM.TMSCHEMA_TABLES WHERE [NAME] = {escapedTableName}";
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
    public async Task<object> GetTableRelationships(
        [Description("Name of the table to get relationships for")] string tableName) // Removed static
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        if (!DaxSecurityUtils.IsValidIdentifier(tableName))
            throw new ArgumentException("Invalid table name format", nameof(tableName));
        
        // var tabular = CreateConnection(); // Replaced
        var escapedTableName = DaxSecurityUtils.EscapeDaxIdentifier(tableName);
        var tableIdQuery = $"SELECT [ID] FROM $SYSTEM.TMSCHEMA_TABLES WHERE [NAME] = {escapedTableName}";
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
    public async Task<object> PreviewTableData(
        [Description("Name of the table to preview data for")] string tableName,
        [Description("Maximum number of rows to return (default: 10)")] int topN = 10) // Removed static
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        if (!DaxSecurityUtils.IsValidIdentifier(tableName))
            throw new ArgumentException("Invalid table name format", nameof(tableName));
        
        // var tabular = CreateConnection(); // Replaced
        var escapedTableName = DaxSecurityUtils.EscapeDaxIdentifier(tableName);
        var dax = $"EVALUATE TOPN({topN}, {escapedTableName})";
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
    public async Task<object> RunQuery(
        [Description("The DAX query to execute. Can be a complete query with DEFINE block, an EVALUATE statement, or a simple expression.")] string dax,
        [Description("Maximum number of rows to return for table expressions (default: 10). Ignored for complete queries.")] int topN = 10) // Removed static
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
            
            // The TabularConnection now throws standard exceptions with enhanced messages
            // We should preserve those enhanced messages and just add any additional context if needed
            if (ex.Message.Contains("DAX Query Error:") || ex.Message.Contains("DMV Query Error:"))
            {
                // The exception already has enhanced error information from TabularConnection
                // Just re-throw it to preserve the detailed message
                throw;
            }
            
            // For other exceptions (like validation errors), add DAX context
            throw new McpException($"Error executing DAX query: {ex.Message}\n\nOriginal Query:\n{originalDax}", ex);
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
}
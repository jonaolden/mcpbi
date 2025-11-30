// File: QueryExecutionTools.cs
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using ModelContextProtocol.Server;

using pbi_local_mcp.Core;

namespace pbi_local_mcp;

/// <summary>
/// DAX Query Execution Tools for running and validating DAX queries.
/// Provides both synchronous and asynchronous query execution with comprehensive error handling.
/// </summary>
[McpServerToolType]
public class QueryExecutionTools
{
    private readonly ITabularConnection _tabularConnection;
    private readonly ILogger<QueryExecutionTools> _logger;
    private readonly TruncationService _truncationService;
    private readonly DataObfuscationService _obfuscationService;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryExecutionTools"/> class.
    /// </summary>
    /// <param name="tabularConnection">The tabular connection service.</param>
    /// <param name="logger">The logger service.</param>
    /// <param name="truncationService">The truncation service for row limiting.</param>
    /// <param name="obfuscationService">The obfuscation service for data masking.</param>
    public QueryExecutionTools(
        ITabularConnection tabularConnection,
        ILogger<QueryExecutionTools> logger,
        TruncationService truncationService,
        DataObfuscationService obfuscationService)
    {
        _tabularConnection = tabularConnection ?? throw new ArgumentNullException(nameof(tabularConnection));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _truncationService = truncationService ?? throw new ArgumentNullException(nameof(truncationService));
        _obfuscationService = obfuscationService ?? throw new ArgumentNullException(nameof(obfuscationService));
    }

    /// <summary>
    /// Execute a DAX query. Supports complete DAX queries with DEFINE blocks or simple expressions.
    /// Results are automatically truncated based on server configuration (default 500 rows).
    /// Obfuscation is applied if configured via command-line arguments.
    /// </summary>
    /// <param name="dax">The DAX query to execute. Can be a complete query with DEFINE block, an EVALUATE statement, or a simple expression.</param>
    /// <param name="topN">Maximum number of rows to return for table expressions (default: 10). Capped by server max-rows setting.</param>
    /// <returns>Query execution result with metadata (totalRows, displayedRows, truncated, hasMore) or detailed error information. May include obfuscation metadata if data masking is enabled.</returns>
    [McpServerTool, Description("Execute DAX query or expression. Results include pagination metadata (totalRows, displayedRows, truncated, hasMore). Obfuscation applied if configured.")]
    public async Task<object> RunQuery(
        [Description("DAX query or expression")] string dax,
        [Description("Max rows for table expressions (capped by server max-rows setting, default 500)")] int topN = 10)
    {
        string originalDax = dax;

        try
        {
            // Validate connection before proceeding
            await _tabularConnection.ValidateConnectionAsync();

            _logger.LogDebug("Starting RunQuery execution for query: {Query}", originalDax?.Substring(0, Math.Min(100, originalDax?.Length ?? 0)));

            var startTime = DateTime.UtcNow;

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
                var executionTime = DateTime.UtcNow - startTime;
                _logger.LogDebug("Query execution completed successfully in {ExecutionTime}ms", executionTime.TotalMilliseconds);

                // Wrap result in expected format
                var resultList = (result as IEnumerable<Dictionary<string, object?>>)?.ToList() ?? new List<Dictionary<string, object?>>();
                var columns = resultList.FirstOrDefault()?.Keys.ToList() ?? new List<string>();

                // Apply obfuscation if enabled
                if (_obfuscationService.Strategy != ObfuscationStrategy.None && resultList.Count > 0)
                {
                    var obfResult = _obfuscationService.ObfuscateData(resultList, columns);
                    resultList = obfResult.Rows;
                    _logger.LogDebug("Applied {Strategy} obfuscation to {FieldCount} fields",
                        obfResult.Strategy, obfResult.ObfuscatedFields.Count);
                }

                // Apply truncation with metadata
                var response = _truncationService.ApplyTruncationWithMetadata(
                    resultList,
                    columns,
                    topN,
                    executionTime.TotalMilliseconds);

                // Add obfuscation metadata if applied
                if (_obfuscationService.Strategy != ObfuscationStrategy.None)
                {
                    response["obfuscated"] = true;
                    response["obfuscationStrategy"] = _obfuscationService.Strategy.ToString();
                }

                return response;
            }
            catch (Exception execEx)
            {
                // Log detailed execution error information
                _logger.LogError(execEx, "Query execution failed. Original: {OriginalQuery}, Final: {FinalQuery}, QueryType: {QueryType}",
                    originalDax, finalQuery, queryType);

                // Return structured error information instead of throwing
                return CreateStructuredErrorResponse(execEx,
                    originalDax ?? string.Empty,
                    finalQuery ?? string.Empty,
                    queryType, "execution");
            }
        }
        catch (PowerBiConnectionException connEx)
        {
            // Re-throw connection exceptions so tests can catch them
            _logger.LogError(connEx, "Connection error in RunQuery");
            throw;
        }
        catch (ArgumentException validationEx)
        {
            // Log validation errors
            _logger.LogWarning(validationEx, "Query validation failed for query: {Query}", originalDax);

            // Return structured validation error information
            return CreateStructuredErrorResponse(validationEx, originalDax, originalDax, QueryType.DAX, "validation");
        }
        catch (DaxQueryExecutionException execEx)
        {
            // Log execution errors and return structured error information
            _logger.LogError(execEx, "DAX execution error in RunQuery for query: {Query}", originalDax);

            // Return structured execution error information
            return CreateStructuredErrorResponse(execEx, originalDax, originalDax, QueryType.DAX, "execution");
        }
        catch (Exception ex)
        {
            // Log unexpected errors and return structured error information
            _logger.LogError(ex, "Unexpected error in RunQuery for query: {Query}", originalDax);

            // Determine if this is likely an execution error based on exception type
            var errorCategory = IsLikelyExecutionError(ex) ? "execution" : "unexpected";
            return CreateStructuredErrorResponse(ex, originalDax, originalDax, QueryType.DAX, errorCategory);
        }
    }

    /// <summary>
    /// Executes a DAX or DMV query with optional verbose diagnostic envelope.
    /// Non-verbose (default): returns raw execution result JSON (array of row dictionaries) and throws on any error.
    /// Verbose: returns a structured JSON envelope (VerboseQueryResult) containing success/output or detailed error info
    /// (never throws except for cancellation). Invalid queryType causes exception (non-verbose) or validation envelope (verbose).
    /// </summary>
    /// <param name="query">Query text (DAX expression, EVALUATE/DEFINE block, or DMV statement when queryType=DMV).</param>
    /// <param name="queryType">Query type hint ("DAX" | "DMV", default "DAX"). Case-insensitive.</param>
    /// <param name="verbose">If true returns diagnostic envelope and suppresses (non-cancellation) exceptions.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>JSON string (raw result array for non-verbose success; verbose diagnostic envelope otherwise).</returns>
    [McpServerTool, Description("Execute DAX or DMV query with optional verbose diagnostics. Returns JSON string with raw results or diagnostic envelope including error details and performance metrics.")]
    public async Task<string> RunQueryAsync(string query, string queryType = "DAX", bool verbose = false, CancellationToken ct = default)
    {
        // Validate connection before proceeding
        await _tabularConnection.ValidateConnectionAsync();

        var sw = Stopwatch.StartNew();
        string originalQuery = query;
        QueryType normalizedQueryType;

        // Local JSON options (re‑use if class later adds a shared instance)
        var jsonOptions = JsonOptions;

        // Helper local function to serialize
        string Serialize(object o) => JsonSerializer.Serialize(o, jsonOptions);

        // Normalize queryType
        bool TryNormalizeQueryType(string s, out QueryType qt)
        {
            if (string.Equals(s, "DAX", StringComparison.OrdinalIgnoreCase))
            {
                qt = QueryType.DAX;
                return true;
            }
            if (string.Equals(s, "DMV", StringComparison.OrdinalIgnoreCase))
            {
                qt = QueryType.DMV;
                return true;
            }
            qt = QueryType.DAX;
            return false;
        }

        if (!TryNormalizeQueryType(queryType ?? "DAX", out normalizedQueryType))
        {
            if (!verbose)
            {
                throw new ArgumentException($"Invalid queryType '{queryType}'. Expected 'DAX' or 'DMV'.", nameof(queryType));
            }

            var invalidTypeEnvelope = new VerboseQueryResult(
                Success: false,
                ErrorCategory: "validation",
                ErrorType: "Parameter Error",
                ErrorMessage: $"Invalid queryType '{queryType}'. Expected 'DAX' or 'DMV'.",
                Line: null,
                Position: null,
                Suggestions: new[] { "Use 'DAX' or 'DMV' (case-insensitive)." },
                Result: null,
                RawResult: null,
                WasModified: false,
                ElapsedMs: sw.ElapsedMilliseconds,
                QueryType: queryType ?? "",
                OriginalQuery: originalQuery ?? "",
                FinalQuery: originalQuery ?? "",
                TimestampUtc: DateTime.UtcNow
            );
            return Serialize(invalidTypeEnvelope);
        }

        // Non-verbose path: execute with legacy semantics (throw on error, return raw result JSON on success)
        if (!verbose)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query))
                    throw new ArgumentException("Query cannot be null or empty.", nameof(query));

                string working = query.Trim();
                string finalQuery = working;
                QueryType execType = normalizedQueryType;

                if (normalizedQueryType == QueryType.DAX)
                {
                    // Perform same validation logic as RunQuery (throwing instead of returning envelope)
                    var validationErrors = ValidateQuerySyntax(working);
                    if (validationErrors.Any())
                        throw new ArgumentException($"Query validation failed: {string.Join("; ", validationErrors)}", nameof(query));

                    if (working.Contains("DEFINE", StringComparison.OrdinalIgnoreCase))
                    {
                        ValidateCompleteDAXQuery(working);
                    }

                    if (!working.StartsWith("DEFINE", StringComparison.OrdinalIgnoreCase) &&
                        !working.StartsWith("EVALUATE", StringComparison.OrdinalIgnoreCase))
                    {
                        // Simple expression -> construct EVALUATE
                        finalQuery = ConstructEvaluateStatement(working, topN: 10); // Use default topN=10 consistent with RunQuery semantics
                    }
                }

                var execResult = await _tabularConnection.ExecAsync(finalQuery, execType, ct);
                return Serialize(execResult);
            }
            catch
            {
                // Preserve original exception (per requirements)
                throw;
            }
        }

        // Verbose path
        try
        {
            if (string.IsNullOrWhiteSpace(query))
                throw new ArgumentException("Query cannot be null or empty.", nameof(query));

            string working = query.Trim();
            string finalQuery = working;
            bool wasModified = false;

            if (normalizedQueryType == QueryType.DAX)
            {
                // Validation (ArgumentException should be captured into envelope)
                var validationErrors = ValidateQuerySyntax(working);
                if (validationErrors.Any())
                {
                    throw new ArgumentException($"Query validation failed: {string.Join("; ", validationErrors)}", nameof(query));
                }

                if (working.Contains("DEFINE", StringComparison.OrdinalIgnoreCase))
                {
                    ValidateCompleteDAXQuery(working);
                }

                if (!working.StartsWith("DEFINE", StringComparison.OrdinalIgnoreCase) &&
                    !working.StartsWith("EVALUATE", StringComparison.OrdinalIgnoreCase))
                {
                    finalQuery = ConstructEvaluateStatement(working, topN: 10);
                    wasModified = true;
                }
            }

            var execResult = await _tabularConnection.ExecAsync(finalQuery, normalizedQueryType, ct);
            sw.Stop();

            var successEnvelope = new VerboseQueryResult(
                Success: true,
                ErrorCategory: null,
                ErrorType: null,
                ErrorMessage: null,
                Line: null,
                Position: null,
                Suggestions: null,
                Result: execResult,
                RawResult: execResult,
                WasModified: wasModified,
                ElapsedMs: sw.ElapsedMilliseconds,
                QueryType: normalizedQueryType.ToString(),
                OriginalQuery: originalQuery,
                FinalQuery: finalQuery,
                TimestampUtc: DateTime.UtcNow
            );

            return Serialize(successEnvelope);
        }
        catch (OperationCanceledException)
        {
            // Cancellation still throws
            throw;
        }
        catch (Exception ex)
        {
            sw.Stop();

            string errorCategory = ex is ArgumentException ? "validation" : "execution";
            var errorType = QueryErrorClassifier.ClassifyError(ex);
            var suggestions = QueryErrorClassifier.GetErrorSuggestions(errorType, ex.Message, originalQuery ?? "");

            // Attempt line/position extraction
            int? line = null;
            int? pos = null;
            try
            {
                var msg = ex.Message ?? "";
                var pattern1 = new System.Text.RegularExpressions.Regex(@"[Ll]ine\s+(?<line>\d+)\s*(?:,|\))\s*(?:position|pos)\s*(?<pos>\d+)");
                var pattern2 = new System.Text.RegularExpressions.Regex(@"\((?:line|Line)\s+(?<line>\d+),\s*(?:position|pos)\s*(?<pos>\d+)\)");
                var m1 = pattern1.Match(msg);
                var m2 = !m1.Success ? pattern2.Match(msg) : null;
                var m = m1.Success ? m1 : (m2 != null && m2.Success ? m2 : null);
                if (m != null)
                {
                    if (int.TryParse(m.Groups["line"].Value, out var l)) line = l;
                    if (int.TryParse(m.Groups["pos"].Value, out var p)) pos = p;
                }
            }
            catch { /* ignore parsing errors */ }

            var errorEnvelope = new VerboseQueryResult(
                Success: false,
                ErrorCategory: errorCategory,
                ErrorType: errorType,
                ErrorMessage: ex.Message,
                Line: line,
                Position: pos,
                Suggestions: suggestions,
                Result: null,
                RawResult: null,
                WasModified: false,
                ElapsedMs: sw.ElapsedMilliseconds,
                QueryType: normalizedQueryType.ToString(),
                OriginalQuery: originalQuery ?? "",
                FinalQuery: originalQuery ?? "",
                TimestampUtc: DateTime.UtcNow
            );

            return Serialize(errorEnvelope);
        }
    }

    // ========================================================================
    // PRIVATE HELPER METHODS
    // ========================================================================

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
            DaxSyntaxValidator.CheckBalancedDelimiters(query, '(', ')', "parentheses", errors);
            DaxSyntaxValidator.CheckBalancedDelimiters(query, '[', ']', "brackets", errors);
            DaxSyntaxValidator.CheckBalancedQuotes(query, errors);
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
    private static void ValidateCompleteDAXQuery(string query)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(query))
        {
            errors.Add("Query cannot be empty.");
        }
        else
        {
            var normalizedQuery = DaxSyntaxValidator.NormalizeDAXQuery(query);

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

            DaxSyntaxValidator.CheckBalancedDelimiters(normalizedQuery, '(', ')', "parentheses", errors);
            DaxSyntaxValidator.CheckBalancedDelimiters(normalizedQuery, '[', ']', "brackets", errors);
            DaxSyntaxValidator.CheckBalancedQuotes(normalizedQuery, errors);
        }

        if (errors.Any())
        {
            throw new ArgumentException(string.Join(" ", errors));
        }
    }

    /// <summary>
    /// Constructs an EVALUATE statement based on the query and topN value.
    /// </summary>
    /// <param name="query">The core query expression.</param>
    /// <param name="topN">Maximum number of rows to return (default: 10).</param>
    /// <returns>The constructed EVALUATE statement.</returns>
    /// <exception cref="ArgumentException">Thrown when query construction fails.</exception>
    private static string ConstructEvaluateStatement(string query, int topN)
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
            bool isCoreQueryTableExpr = DaxQueryAnalyzer.IsTableExpression(query);

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
    /// Determines if an exception is likely an execution error rather than a validation error
    /// </summary>
    private static bool IsLikelyExecutionError(Exception ex)
    {
        var message = ex.Message.ToLowerInvariant();
        var typeName = ex.GetType().Name.ToLowerInvariant();

        // Check for common execution error patterns
        return message.Contains("function") && message.Contains("not") ||
               message.Contains("cannot find") ||
               message.Contains("does not exist") ||
               message.Contains("invalid") && !message.Contains("syntax") ||
               typeName.Contains("execution") ||
               typeName.Contains("runtime") ||
               ex is DaxQueryExecutionException;
    }

    /// <summary>
    /// Creates a structured error response with detailed diagnostics
    /// </summary>
    /// <param name="exception">The original exception that occurred</param>
    /// <param name="originalQuery">The original DAX query</param>
    /// <param name="finalQuery">The final processed DAX query</param>
    /// <param name="queryType">The type of query (DAX or DMV)</param>
    /// <param name="errorCategory">The category of error (execution, validation, unexpected)</param>
    /// <returns>Structured error response object</returns>
    private static object CreateStructuredErrorResponse(Exception exception, string originalQuery, string finalQuery, QueryType queryType, string errorCategory)
    {
        var errorType = QueryErrorClassifier.ClassifyError(exception);
        var suggestions = QueryErrorClassifier.GetErrorSuggestions(errorType, exception.Message, originalQuery);

        return new
        {
            Success = false,
            ErrorCategory = errorCategory,
            ErrorType = errorType,
            ErrorDetails = new
            {
                ExceptionType = exception.GetType().Name,
                Message = exception.Message
            },
            QueryInfo = new
            {
                QueryType = queryType.ToString(),
                WasModified = originalQuery != finalQuery,
                Query = originalQuery.Length > 200 ? originalQuery.Substring(0, 200) + "..." : originalQuery
            },
            Suggestions = suggestions.Take(3).ToList()
        };
    }

    /// <summary>
    /// Verbose result envelope returned when <c>RunQueryAsync</c> is invoked with <c>verbose = true</c>.
    /// Contains diagnostic information on failures or the execution result on success.
    /// </summary>
    internal record VerboseQueryResult(
        bool Success,
        string? ErrorCategory,
        string? ErrorType,
        string? ErrorMessage,
        int? Line,
        int? Position,
        IEnumerable<string>? Suggestions,
        object? Result,
        object? RawResult,
        bool WasModified,
        long ElapsedMs,
        string QueryType,
        string OriginalQuery,
        string FinalQuery,
        DateTime TimestampUtc
    );
}

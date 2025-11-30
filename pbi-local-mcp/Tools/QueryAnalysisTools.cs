// File: QueryAnalysisTools.cs
using System.ComponentModel;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using ModelContextProtocol.Server;

using pbi_local_mcp.Core;

namespace pbi_local_mcp;

/// <summary>
/// DAX Query Analysis Tools for validation and performance analysis.
/// Provides comprehensive DAX syntax validation and performance bottleneck identification.
/// </summary>
[McpServerToolType]
public class QueryAnalysisTools
{
    private readonly ITabularConnection _tabularConnection;
    private readonly ILogger<QueryAnalysisTools> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryAnalysisTools"/> class.
    /// </summary>
    /// <param name="tabularConnection">The tabular connection service.</param>
    /// <param name="logger">The logger service.</param>
    public QueryAnalysisTools(ITabularConnection tabularConnection, ILogger<QueryAnalysisTools> logger)
    {
        _tabularConnection = tabularConnection ?? throw new ArgumentNullException(nameof(tabularConnection));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Validates DAX syntax and identifies potential issues with enhanced error analysis.
    /// </summary>
    /// <param name="daxExpression">DAX expression to validate</param>
    /// <returns>Validation results including syntax errors and warnings</returns>
    [McpServerTool, Description("Validate DAX syntax and identify issues.")]
    public async Task<object> ValidateQuery(
        [Description("DAX expression")] string daxExpression)
    {
        try
        {
            // Validate connection before proceeding
            await _tabularConnection.ValidateConnectionAsync();

            if (string.IsNullOrWhiteSpace(daxExpression))
                throw new ArgumentException("DAX expression cannot be empty", nameof(daxExpression));

            // Basic syntax validation
            var syntaxErrors = new List<string>();
            var warnings = new List<string>();

            // Check balanced delimiters
            DaxSyntaxValidator.CheckBalancedDelimiters(daxExpression, '(', ')', "parentheses", syntaxErrors);
            DaxSyntaxValidator.CheckBalancedDelimiters(daxExpression, '[', ']', "brackets", syntaxErrors);
            DaxSyntaxValidator.CheckBalancedQuotes(daxExpression, syntaxErrors);

            // Check for common DAX patterns and issues
            AnalyzeDaxPatterns(daxExpression, warnings);

            // Try to execute a simple validation query
            bool executionValid = false;
            string executionError = "";

            try
            {
                // Check if the expression is already a complete EVALUATE query
                string testQuery;
                if (daxExpression.Trim().StartsWith("EVALUATE", StringComparison.OrdinalIgnoreCase) ||
                    daxExpression.Trim().StartsWith("DEFINE", StringComparison.OrdinalIgnoreCase))
                {
                    // Already a complete query, execute as-is
                    testQuery = daxExpression;
                }
                else
                {
                    // Wrap simple expression in ROW()
                    testQuery = $"EVALUATE ROW(\"ValidationTest\", {daxExpression})";
                }

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

            return new Dictionary<string, object?>
            {
                ["expression"] = daxExpression.Trim(),
                ["isValid"] = !syntaxErrors.Any() && executionValid,
                ["syntaxErrors"] = syntaxErrors,
                ["errors"] = syntaxErrors,
                ["warnings"] = warnings,
                ["complexityMetrics"] = complexityMetrics,
                ["validationDetails"] = new
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

    /// <summary>
    /// Analyzes query performance characteristics and identifies potential bottlenecks using DMV-based metrics.
    /// </summary>
    /// <param name="daxQuery">DAX query to analyze</param>
    /// <param name="iterations">Number of iterations to run the query for aggregated statistics (default: 1)</param>
    /// <returns>Performance analysis results including execution time and DMV-based engine metrics</returns>
    [McpServerTool, Description("Analyze query performance and identify bottlenecks.")]
    public async Task<object> AnalyzeQueryPerformance(
        [Description("DAX query")] string daxQuery,
        [Description("Iterations for statistics (1-100, default 1)")] int iterations = 1)
    {
        // Validate connection before proceeding
        await _tabularConnection.ValidateConnectionAsync();

        var diagnostics = new DmvDiagnostics();

        try
        {
            if (string.IsNullOrWhiteSpace(daxQuery))
                throw new ArgumentException("DAX query cannot be empty", nameof(daxQuery));

            if (iterations < 1 || iterations > 100)
                throw new ArgumentException("Iterations must be between 1 and 100", nameof(iterations));

            object? firstQueryResult = null;
            string executionError = "";
            bool allRunsSuccessful = true;

            var runTimesMs = new List<double>();
            // Capture baseline session metrics before execution with diagnostics
            var (sessionId, sessionIdMethod) = await GetCurrentSessionIdWithDiagnosticsAsync(diagnostics);
            diagnostics.SessionId = sessionId;
            diagnostics.SessionIdMethod = sessionIdMethod;

            var baselineMetrics = await GetSessionMetricsWithDiagnosticsAsync(sessionId, diagnostics, "baseline");

            // Execute the query N times for statistics
            for (int i = 0; i < iterations; i++)
            {
                var runStart = DateTime.UtcNow;
                try
                {
                    var queryResult = await _tabularConnection.ExecAsync(daxQuery, QueryType.DAX);
                    var runElapsed = (DateTime.UtcNow - runStart).TotalMilliseconds;
                    runTimesMs.Add(runElapsed);

                    // Keep the first result for row counting
                    if (i == 0)
                        firstQueryResult = queryResult;
                }
                catch (Exception ex)
                {
                    executionError = ex.Message;
                    allRunsSuccessful = false;
                    // Record elapsed for this failed run (time until exception)
                    var failedElapsed = (DateTime.UtcNow - runStart).TotalMilliseconds;
                    runTimesMs.Add(failedElapsed);
                    break;
                }
            }

            // Capture post-execution metrics with diagnostics
            var postMetrics = await GetSessionMetricsWithDiagnosticsAsync(sessionId, diagnostics, "post-execution");
            var commandMetrics = await GetRecentCommandMetricsWithDiagnosticsAsync(sessionId, diagnostics);

            // Calculate aggregated timing statistics
            var timings = runTimesMs.ToArray();
            double avg = timings.Length > 0 ? timings.Average() : 0.0;
            double median = 0.0;
            double stddev = 0.0;
            double min = timings.Length > 0 ? timings.Min() : 0.0;
            double max = timings.Length > 0 ? timings.Max() : 0.0;
            if (timings.Length > 0)
            {
                var sorted = timings.OrderBy(x => x).ToArray();
                int mid = sorted.Length / 2;
                median = sorted.Length % 2 == 0 ? (sorted[mid - 1] + sorted[mid]) / 2.0 : sorted[mid];
                double variance = sorted.Select(x => Math.Pow(x - avg, 2)).Sum() / sorted.Length;
                stddev = Math.Sqrt(variance);
            }

            var averageExecutionTime = TimeSpan.FromMilliseconds(avg);

            // Calculate engine metrics using average execution time for context
            var engineMetrics = CalculateEngineMetrics(baselineMetrics, postMetrics, commandMetrics, averageExecutionTime, diagnostics);

            // Analyze query structure and complexity using the original query
            var complexityAnalysis = AnalyzeQueryStructure(daxQuery);
            var performanceMetrics = CalculatePerformanceMetrics(daxQuery, averageExecutionTime, allRunsSuccessful);

            int resultRowCount = 0;
            if (allRunsSuccessful && firstQueryResult is IEnumerable<Dictionary<string, object?>> rows)
            {
                resultRowCount = rows.Count();
            }

            // Build condensed structured output with timing statistics
            return new
            {
                // Average execution time (ms) for compatibility
                executionTime = (int)Math.Round(avg),

                // Query plan availability note
                queryPlan = complexityAnalysis != null ? "Available in analysis section" : "Not available",

                execution = new
                {
                    successful = allRunsSuccessful,
                    iterations = iterations,
                    runtimesMs = runTimesMs,
                    stats = new
                    {
                        averageMs = Math.Round(avg, 2),
                        medianMs = Math.Round(median, 2),
                        stddevMs = Math.Round(stddev, 2),
                        minMs = Math.Round(min, 2),
                        maxMs = Math.Round(max, 2)
                    },
                    timeMs = Math.Round(avg, 2),
                    resultRowCount = resultRowCount,
                    error = string.IsNullOrEmpty(executionError) ? null : executionError
                },

                performance = new
                {
                    metrics = engineMetrics ?? performanceMetrics,
                    metricSource = engineMetrics != null ? "DMV" : "Heuristic"
                },

                analysis = complexityAnalysis,

                Diagnostics = diagnostics.Warnings.Count > 0 || sessionId == "unknown"
                    ? new
                    {
                        SessionId = sessionId != "unknown" ? sessionId : null,
                        DmvMethod = sessionId != "unknown" ? diagnostics.SessionIdMethod : null,
                        Warnings = diagnostics.Warnings.Count > 0 ? diagnostics.Warnings : null
                    }
                    : null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing query performance for query: {Query}", daxQuery);
            throw;
        }
    }

    // ========================================================================
    // PRIVATE HELPER METHODS - DMV DIAGNOSTICS
    // ========================================================================

    /// <summary>
    /// Diagnostics class to track DMV query success/failure and provide troubleshooting information
    /// </summary>
    private class DmvDiagnostics
    {
        public string SessionId { get; set; } = "unknown";
        public string SessionIdMethod { get; set; } = "not attempted";
        public bool BaselineSessionMetricsSuccess { get; set; }
        public string? BaselineSessionMetricsError { get; set; }
        public bool PostSessionMetricsSuccess { get; set; }
        public string? PostSessionMetricsError { get; set; }
        public bool CommandMetricsSuccess { get; set; }
        public string? CommandMetricsError { get; set; }
        public bool EngineMetricsCalculated { get; set; }
        public string? EngineMetricsError { get; set; }
        public List<string> Warnings { get; } = new();

        public string GetSummary()
        {
            var parts = new List<string>
            {
                $"SessionId: {SessionId} ({SessionIdMethod})",
                $"Baseline: {(BaselineSessionMetricsSuccess ? "OK" : BaselineSessionMetricsError ?? "Failed")}",
                $"Post: {(PostSessionMetricsSuccess ? "OK" : PostSessionMetricsError ?? "Failed")}",
                $"Command: {(CommandMetricsSuccess ? "OK" : CommandMetricsError ?? "Failed")}",
                $"EngineMetrics: {(EngineMetricsCalculated ? "Calculated" : EngineMetricsError ?? "Not calculated")}"
            };
            return string.Join(", ", parts);
        }

        public object GetQueryStatus()
        {
            return new
            {
                SessionId,
                SessionIdMethod,
                MetricsAvailability = new
                {
                    Baseline = BaselineSessionMetricsSuccess,
                    PostExecution = PostSessionMetricsSuccess,
                    Command = CommandMetricsSuccess
                },
                EngineMetricsCalculated,
                Warnings
            };
        }
    }

    /// <summary>
    /// Retrieves current session ID using DISCOVER_SESSIONS DMV with multiple fallback methods
    /// </summary>
    private async Task<(string sessionId, string method)> GetCurrentSessionIdWithDiagnosticsAsync(DmvDiagnostics diagnostics)
    {
        var attemptedMethods = new List<string>();
        var errors = new List<string>();

        // Method 1: Query DISCOVER_SESSIONS with ORDER BY (most recently active session)
        try
        {
            const string query = "SELECT TOP 1 SESSION_SPID FROM $System.DISCOVER_SESSIONS ORDER BY SESSION_LAST_COMMAND_ELAPSED_TIME_MS DESC";
            var result = await _tabularConnection.ExecAsync(query, QueryType.DMV);

            if (result is IEnumerable<Dictionary<string, object?>> rows)
            {
                var firstRow = rows.FirstOrDefault();
                if (firstRow != null && firstRow.TryGetValue("SESSION_SPID", out var spid) && spid != null)
                {
                    var sessionId = spid.ToString();
                    if (!string.IsNullOrEmpty(sessionId))
                    {
                        _logger.LogDebug("Session ID retrieved from DISCOVER_SESSIONS (ORDER BY): {SessionId}", sessionId);
                        diagnostics.SessionIdMethod = "DISCOVER_SESSIONS DMV (ORDER BY most recent activity)";
                        return (sessionId, "DISCOVER_SESSIONS DMV");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "DISCOVER_SESSIONS with ORDER BY failed");
            attemptedMethods.Add("DISCOVER_SESSIONS (ORDER BY)");
            errors.Add($"DISCOVER_SESSIONS (ORDER BY): {ex.Message}");
        }

        // Method 2: Try simpler query without ORDER BY
        try
        {
            const string query = "SELECT SESSION_SPID FROM $System.DISCOVER_SESSIONS";
            var result = await _tabularConnection.ExecAsync(query, QueryType.DMV);

            if (result is IEnumerable<Dictionary<string, object?>> rows)
            {
                var firstRow = rows.FirstOrDefault();
                if (firstRow != null && firstRow.TryGetValue("SESSION_SPID", out var spid) && spid != null)
                {
                    var sessionId = spid.ToString();
                    if (!string.IsNullOrEmpty(sessionId))
                    {
                        _logger.LogDebug("Session ID retrieved using simple DISCOVER_SESSIONS: {SessionId}", sessionId);
                        diagnostics.SessionIdMethod = "DISCOVER_SESSIONS DMV (simple query)";
                        return (sessionId, "DISCOVER_SESSIONS (simple)");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "All session ID retrieval methods failed");
            attemptedMethods.Add("DISCOVER_SESSIONS (simple)");
            errors.Add($"DISCOVER_SESSIONS (simple): {ex.Message}");
        }

        // All methods failed
        _logger.LogWarning("Could not retrieve session ID using any DMV method");
        diagnostics.SessionIdMethod = "none - all DMV methods failed";
        diagnostics.Warnings.Add($"Session ID detection failed. DMV access may be restricted or unavailable.");
        diagnostics.Warnings.Add($"Attempted methods: {string.Join(", ", attemptedMethods)}");
        if (errors.Count > 0)
        {
            diagnostics.Warnings.Add($"Errors: {string.Join(" | ", errors.Take(2))}");
        }

        return ("unknown", "none - all methods failed");
    }

    /// <summary>
    /// Retrieves session metrics with diagnostic tracking
    /// </summary>
    private async Task<Dictionary<string, object?>?> GetSessionMetricsWithDiagnosticsAsync(
        string sessionId, DmvDiagnostics diagnostics, string stage)
    {
        if (sessionId == "unknown")
        {
            if (stage == "baseline")
                diagnostics.BaselineSessionMetricsError = "Session ID unknown";
            else
                diagnostics.PostSessionMetricsError = "Session ID unknown";
            return null;
        }

        try
        {
            var query = $"SELECT * FROM $System.DISCOVER_SESSIONS WHERE SESSION_SPID = {sessionId}";
            var result = await _tabularConnection.ExecAsync(query, QueryType.DMV);

            if (result is IEnumerable<Dictionary<string, object?>> rows)
            {
                var metrics = rows.FirstOrDefault();
                if (metrics != null)
                {
                    if (stage == "baseline")
                        diagnostics.BaselineSessionMetricsSuccess = true;
                    else
                        diagnostics.PostSessionMetricsSuccess = true;

                    _logger.LogDebug("Retrieved {Stage} session metrics with {Count} columns", stage, metrics.Count);
                    return metrics;
                }
            }

            var errorMsg = "No session data returned from DISCOVER_SESSIONS";
            if (stage == "baseline")
                diagnostics.BaselineSessionMetricsError = errorMsg;
            else
                diagnostics.PostSessionMetricsError = errorMsg;

            return null;
        }
        catch (Exception ex)
        {
            var errorMsg = $"DMV query failed: {ex.Message}";
            _logger.LogWarning(ex, "Could not retrieve {Stage} session metrics", stage);

            if (stage == "baseline")
                diagnostics.BaselineSessionMetricsError = errorMsg;
            else
                diagnostics.PostSessionMetricsError = errorMsg;

            return null;
        }
    }

    /// <summary>
    /// Retrieves command metrics with diagnostic tracking
    /// </summary>
    private async Task<Dictionary<string, object?>?> GetRecentCommandMetricsWithDiagnosticsAsync(
        string sessionId, DmvDiagnostics diagnostics)
    {
        if (sessionId == "unknown")
        {
            diagnostics.CommandMetricsError = "Session ID unknown";
            return null;
        }

        try
        {
            var query = $"SELECT * FROM $System.DISCOVER_COMMANDS WHERE SESSION_SPID = {sessionId}";
            var result = await _tabularConnection.ExecAsync(query, QueryType.DMV);

            if (result is IEnumerable<Dictionary<string, object?>> rows)
            {
                var metrics = rows.FirstOrDefault();
                if (metrics != null)
                {
                    diagnostics.CommandMetricsSuccess = true;
                    _logger.LogDebug("Retrieved command metrics with {Count} columns", metrics.Count);
                    return metrics;
                }
            }

            diagnostics.CommandMetricsError = "No command data found";
            return null;
        }
        catch (Exception ex)
        {
            diagnostics.CommandMetricsError = $"DMV query failed: {ex.Message}";
            _logger.LogWarning(ex, "Could not retrieve command metrics from DISCOVER_COMMANDS DMV");
            return null;
        }
    }

    /// <summary>
    /// Enhanced engine metrics calculation with diagnostic tracking and flexible column handling
    /// </summary>
    private object? CalculateEngineMetrics(
        Dictionary<string, object?>? baseline,
        Dictionary<string, object?>? post,
        Dictionary<string, object?>? command,
        TimeSpan measuredExecutionTime,
        DmvDiagnostics diagnostics)
    {
        try
        {
            if (baseline == null && post == null && command == null)
            {
                diagnostics.EngineMetricsError = "No DMV data available";
                return null;
            }

            if (post != null)
            {
                _logger.LogDebug("Available DMV columns: {Columns}", string.Join(", ", post.Keys));
            }

            var cpuTime = GetLongValueFlexible(post, baseline, "CPU_TIME", "SESSION_CPU_TIME_MS", "COMMAND_CPU_TIME_MS");
            var memory = GetLongValueFlexible(post, baseline, "USED_MEMORY", "SESSION_USED_MEMORY");
            var readKb = GetLongValueFlexible(post, baseline, "READ_KB", "SESSION_READ_KB", "READS");
            var writeKb = GetLongValueFlexible(post, baseline, "WRITES_KB", "SESSION_WRITES_KB", "WRITES");

            var totalTime = measuredExecutionTime.TotalMilliseconds;

            var storageEngineRatio = readKb > 0 ? Math.Min(0.8, readKb / Math.Max(1.0, totalTime * 10)) : 0.3;
            var estimatedSeDuration = totalTime * storageEngineRatio;
            var estimatedFeDuration = totalTime * (1 - storageEngineRatio);

            diagnostics.EngineMetricsCalculated = true;

            return new
            {
                TotalCpuTimeMs = cpuTime,
                MemoryUsageKB = memory,

                StorageEngineDurationMs = (long)estimatedSeDuration,
                StorageEngineDataReadKB = readKb,

                FormulaEngineDurationMs = (long)estimatedFeDuration,
                FormulaEngineCpuRatio = cpuTime > 0 && totalTime > 0 ? Math.Round(cpuTime / totalTime, 2) : 0,

                TotalReadKB = readKb,
                TotalWriteKB = writeKb,

                CpuEfficiency = cpuTime > 0 && totalTime > 0 ?
                    Math.Round((cpuTime / totalTime) * 100, 1) : 0,
                StorageEnginePercentage = Math.Round(storageEngineRatio * 100, 1),
                FormulaEnginePercentage = Math.Round((1 - storageEngineRatio) * 100, 1),

                IsStorageEngineHeavy = storageEngineRatio > 0.6,
                IsFormulaEngineHeavy = storageEngineRatio < 0.4,
                IsBalanced = storageEngineRatio >= 0.4 && storageEngineRatio <= 0.6,

                DataQuality = new
                {
                    HasCpuData = cpuTime > 0,
                    HasMemoryData = memory > 0,
                    HasIoData = readKb > 0 || writeKb > 0,
                    EstimatedMetrics = "SE/FE split estimated from I/O patterns"
                }
            };
        }
        catch (Exception ex)
        {
            diagnostics.EngineMetricsError = $"Calculation failed: {ex.Message}";
            _logger.LogWarning(ex, "Error calculating engine metrics from DMV data");
            return null;
        }
    }

    /// <summary>
    /// Flexible value extraction that tries multiple column names
    /// </summary>
    private long GetLongValueFlexible(Dictionary<string, object?>? primary, Dictionary<string, object?>? secondary, params string[] columnNames)
    {
        long primaryValue = 0;
        long secondaryValue = 0;

        if (primary != null)
        {
            foreach (var colName in columnNames)
            {
                if (TryGetLongFromDict(primary, colName, out var val))
                {
                    primaryValue = val;
                    break;
                }
            }
        }

        if (secondary != null)
        {
            foreach (var colName in columnNames)
            {
                if (TryGetLongFromDict(secondary, colName, out var val))
                {
                    secondaryValue = val;
                    break;
                }
            }
        }

        return primaryValue > 0 && secondaryValue > 0 ? primaryValue - secondaryValue : primaryValue;
    }

    /// <summary>
    /// Helper to try extracting a long value from dictionary
    /// </summary>
    private bool TryGetLongFromDict(Dictionary<string, object?> dict, string key, out long value)
    {
        value = 0;
        if (dict.TryGetValue(key, out var obj) && obj != null)
        {
            if (obj is long longValue)
            {
                value = longValue;
                return true;
            }
            if (obj is int intValue)
            {
                value = intValue;
                return true;
            }
            if (long.TryParse(obj.ToString(), out var parsedValue))
            {
                value = parsedValue;
                return true;
            }
        }
        return false;
    }

    // ========================================================================
    // PRIVATE HELPER METHODS - ANALYSIS
    // ========================================================================

    /// <summary>
    /// Generates human-readable performance insights based on all available metrics
    /// </summary>
    private static void AnalyzeDaxPatterns(string expression, List<string> warnings)
    {
        if (string.IsNullOrEmpty(expression))
            return;

        if (expression.Contains("SUMX", StringComparison.OrdinalIgnoreCase) && expression.Contains("FILTER", StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add("SUMX with FILTER detected");
        }

        if (System.Text.RegularExpressions.Regex.IsMatch(expression, @"CALCULATE\s*\(\s*CALCULATE", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
        {
            warnings.Add("Nested CALCULATE functions detected");
        }

        var calculateCount = System.Text.RegularExpressions.Regex.Matches(expression, @"\bCALCULATE\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Count;
        if (calculateCount > 3)
        {
            warnings.Add($"High number of CALCULATE functions ({calculateCount})");
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

        return new
        {
            ExecutionTimeMs = executionTime.TotalMilliseconds,
            QueryComplexityFactor = (queryLength / 100.0) + (functionCount * 0.5),
            FunctionDensity = queryLength > 0 ? (double)functionCount / queryLength * 100 : 0,
            Successful = successful
        };
    }
}

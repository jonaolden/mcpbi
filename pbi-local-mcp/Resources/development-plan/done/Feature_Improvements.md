# PowerBI Tabular Model MCP Tools: Feature Improvement Suggestions

Based on testing the MCP MCPBI tools for PowerBI tabular model interaction, here are suggested features and improvements that would enhance the production readiness and usability of these tools.

## Core Functionality Improvements

### DAX Query Execution
- **Improved DEFINE Support**: Enhance handling of complex DAX queries with multiple DEFINE blocks
- **Query Timeout Controls**: Allow setting timeouts for long-running queries
- **DAX Batch Execution**: Support for running multiple DAX queries in sequence
- **Query Templates**: Pre-defined DAX query templates for common analytical patterns
- **Query History**: Track and recall recently executed queries
- **DAX Validation**: Separate tool to validate DAX syntax without execution

### Error Handling and Diagnostics
- **Enhanced Error Messages**: More descriptive error messages for DAX syntax issues
- **Query Profiling**: Performance metrics for executed queries (time, memory, etc.)
- **Execution Plans**: Access to DAX query execution plans
- **Logging Options**: Configurable logging levels for troubleshooting

### Data Exploration Extensions
- **Schema Navigation**: Hierarchical navigation of model schema
- **Data Sampling**: Smart sampling for large tables beyond simple TOPN
- **Data Distribution Analysis**: Statistics on column value distributions
- **Anomaly Detection**: Basic data quality checks (null values, outliers)

## Model Metadata Access

### Structure & Design
- **Perspective Browsing**: View and filter by model perspectives
- **Hierarchy Exploration**: Examine defined hierarchies in the model
- **Calculated Column Details**: Specifically view calculated column definitions
- **Dependency Analysis**: Show relationships between measures, columns, and tables

### Security & Governance
- **Role Information**: View defined security roles
- **RLS Definitions**: Examine row-level security definitions
- **Partition Information**: View table partitioning details
- **Refresh History**: Access to refresh timestamps and status

## User Experience Features

### Interaction & Output
- **Result Pagination**: Control result set size with pagination
- **Output Formatting**: Options for formatting numeric and date results
- **Export Capabilities**: Export query results to CSV, JSON, or Excel
- **Basic Visualization**: Simple charts for query results
- **Markdown Support**: Format descriptions and documentation in markdown

### Usability
- **Auto-completion**: DAX function and object name suggestions
- **Table/Column Picker**: UI for selecting tables and columns for queries
- **Saved Queries**: Save and organize frequently used queries
- **Context-sensitive Help**: Inline help for DAX functions

## Integration Capabilities

### System Integration
- **Environment Configuration**: Simplified configuration for different environments
- **Batch Processing**: Integration with scheduled tasks and batch processes
- **API Documentation**: Comprehensive API documentation with examples
- **Authentication Options**: Support for different authentication methods

### Extensibility
- **Custom Function Support**: Allow defining custom DAX functions
- **Plugin Architecture**: Extension points for third-party additions
- **Event Hooks**: Notifications for model changes or query completions
- **Scripting Support**: Support for scripting sequential operations

## Performance & Scalability

### Optimization
- **Connection Pooling**: Efficient management of connections to the tabular model
- **Parallel Query Execution**: Run multiple queries in parallel when appropriate
- **Smart Caching**: Cache query results where appropriate
- **Memory Management**: Controls for memory usage during large query execution

### Monitoring
- **Resource Usage Tracking**: Monitor memory and CPU usage
- **Connection Status**: View active connections and their status
- **Health Checks**: Basic connectivity and performance tests

## Implementation Considerations

When implementing these features, priority should be given to:
1. Stabilizing core query functionality, especially for complex DEFINE blocks
2. Improving error handling and diagnostics
3. Enhancing metadata exploration capabilities
4. Adding basic result formatting and export options

These improvements would significantly enhance the utility of the MCP MCPBI tools in production scenarios across both development and business user contexts.

## Implementation Approaches

This section outlines practical approaches to implement the suggested features, organized by complexity and dependency.

### Near-Term Enhancements (Low Complexity)

#### Improving DAX Query Support

```csharp
// Enhance the RunQuery method with better DEFINE block handling
public static async Task<object> RunQuery(string dax, int topN = 10, QueryOptions? options = null)
{
    options ??= new QueryOptions { Timeout = TimeSpan.FromSeconds(60) };
    var tabular = CreateConnection();
    string query = dax.Trim();

    try {
        if (IsCompleteDAXQuery(query)) {
            return await ExecuteWithTimeout(tabular, query, options.Timeout);
        }
        
        // Rest of existing implementation...
    }
    catch (Exception ex) {
        // Enhanced error handling with query analysis
        return new QueryErrorResponse(ex, AnalyzeQueryForCommonErrors(query));
    }
}

private static bool IsCompleteDAXQuery(string query)
{
    // More robust detection of DEFINE blocks and multi-statement DAX
    return query.StartsWith("DEFINE", StringComparison.OrdinalIgnoreCase) || 
           (query.Contains("DEFINE", StringComparison.OrdinalIgnoreCase) && 
            query.Contains("EVALUATE", StringComparison.OrdinalIgnoreCase));
}
```

#### Adding Query Timeout & Error Diagnostics

```csharp
// Add a QueryOptions class for configurable execution
public class QueryOptions
{
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
    public bool IncludeExecutionStats { get; set; } = false;
    public LogLevel LogLevel { get; set; } = LogLevel.Error;
}

// Implement timeout handling
private static async Task<object> ExecuteWithTimeout(TabularConnection connection, string query, TimeSpan timeout)
{
    using var cts = new CancellationTokenSource(timeout);
    var task = connection.ExecAsync(query, QueryType.DAX, cts.Token);
    
    try {
        return await task;
    }
    catch (OperationCanceledException) {
        throw new TimeoutException($"Query execution timed out after {timeout.TotalSeconds} seconds");
    }
}
```

#### Implement Basic Result Formatting & Export

```csharp
[McpServerTool, Description("Export query results to CSV")]
public static async Task<string> ExportQueryToCsv(string dax, string outputPath)
{
    var results = await RunQuery(dax);
    
    if (results is IEnumerable<Dictionary<string, object?>> rows)
    {
        using var writer = new StreamWriter(outputPath);
        
        // Write header
        if (rows.Any())
        {
            writer.WriteLine(string.Join(",", rows.First().Keys.Select(k => $"\"{k}\"")));
        }
        
        // Write data rows
        foreach (var row in rows)
        {
            writer.WriteLine(string.Join(",", row.Values.Select(FormatCsvValue)));
        }
        
        return $"Exported {rows.Count()} rows to {outputPath}";
    }
    
    return "No results to export";
}

private static string FormatCsvValue(object? value)
{
    if (value == null) return "\"\"";
    return $"\"{value.ToString()?.Replace("\"", "\"\"")}\"";
}
```

### Mid-Term Enhancements (Moderate Complexity)

#### Expanding Metadata Access

```csharp
[McpServerTool, Description("Get hierarchies for a specific table by name.")]
public static async Task<object> GetTableHierarchies(string tableName)
{
    var tabular = CreateConnection();
    var tableIdQuery = $"SELECT [ID] FROM $SYSTEM.TMSCHEMA_TABLES WHERE [NAME] = '{tableName}'";
    var tableIdResult = await tabular.ExecAsync(tableIdQuery, QueryType.DMV);
    var tableId = tableIdResult is IEnumerable<Dictionary<string, object?>> rows && rows.Any()
        ? rows.First()["ID"]?.ToString()
        : null;

    if (string.IsNullOrEmpty(tableId))
        throw new Exception($"Table ID not found for table '{tableName}'");

    // Query hierarchies from the model metadata
    var dmv = $@"
        SELECT 
            h.[ID], h.[Name], h.[Description], h.[IsHidden], h.[DisplayFolder],
            c.[Name] as ColumnName
        FROM $SYSTEM.TMSCHEMA_HIERARCHIES h
        JOIN $SYSTEM.TMSCHEMA_LEVELS l ON l.[HierarchyID] = h.[ID]
        JOIN $SYSTEM.TMSCHEMA_COLUMNS c ON l.[ColumnID] = c.[ID]
        WHERE h.[TableID] = '{tableId}'
        ORDER BY h.[Name], l.[Ordinal]";

    var result = await tabular.ExecAsync(dmv, QueryType.DMV);
    return result;
}

[McpServerTool, Description("Get security roles defined in the model.")]
public static async Task<object> GetModelRoles()
{
    var tabular = CreateConnection();
    var dmv = "SELECT * FROM $SYSTEM.TMSCHEMA_ROLES";
    var result = await tabular.ExecAsync(dmv, QueryType.DMV);
    return result;
}
```

#### Implementing Model Dependency Analysis

```csharp
[McpServerTool, Description("Analyze measure dependencies.")]
public static async Task<object> AnalyzeMeasureDependencies(string measureName)
{
    var tabular = CreateConnection();
    
    // Get measure details including DAX expression
    var measureQuery = $"SELECT [TableID], [Expression] FROM $SYSTEM.TMSCHEMA_MEASURES WHERE [NAME] = '{measureName}'";
    var measureResult = await tabular.ExecAsync(measureQuery, QueryType.DMV);
    
    if (measureResult is not IEnumerable<Dictionary<string, object?>> rows || !rows.Any())
        throw new Exception($"Measure '{measureName}' not found");
    
    // Parse the DAX expression to identify referenced tables, columns, and other measures
    var expression = rows.First()["Expression"]?.ToString() ?? "";
    
    var dependencies = new
    {
        ReferencedTables = ExtractTableReferences(expression),
        ReferencedColumns = ExtractColumnReferences(expression),
        ReferencedMeasures = ExtractMeasureReferences(expression),
        Expression = expression
    };
    
    return dependencies;
}

private static IEnumerable<string> ExtractTableReferences(string daxExpression)
{
    // Implementation would use regex to identify table references
    // This is a simplified example
    var tableMatches = System.Text.RegularExpressions.Regex.Matches(
        daxExpression, 
        @"'([^']+)'|(\w+)\s*\["
    );
    
    return tableMatches.Select(m => m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value)
                      .Distinct();
}

// Similar implementations for ExtractColumnReferences and ExtractMeasureReferences
```

### Long-Term Enhancements (Higher Complexity)

#### Query Profiling and Execution Plans

```csharp
[McpServerTool, Description("Execute a DAX query with profiling information.")]
public static async Task<object> RunQueryWithProfiling(string dax)
{
    var tabular = CreateConnection();
    
    // Start timing and collect pre-execution metrics
    var startMemory = GC.GetTotalMemory(false);
    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
    
    try
    {
        // Execute the DAX query
        var queryResult = await tabular.ExecAsync(dax, QueryType.DAX);
        
        // Get execution plan if available (this would require specific AMO/ADOMD.NET calls)
        var executionPlan = await GetQueryExecutionPlan(tabular, dax);
        
        // Collect post-execution metrics
        stopwatch.Stop();
        var endMemory = GC.GetTotalMemory(false);
        
        // Return both the result and the profiling information
        return new
        {
            Results = queryResult,
            Profiling = new
            {
                ExecutionTime = stopwatch.Elapsed,
                MemoryUsed = endMemory - startMemory,
                ExecutionPlan = executionPlan
            }
        };
    }
    catch (Exception ex)
    {
        stopwatch.Stop();
        throw new Exception($"Query execution failed: {ex.Message}", ex);
    }
}

private static async Task<object> GetQueryExecutionPlan(TabularConnection connection, string dax)
{
    // This would use AMO/ADOMD.NET specific APIs to get an execution plan
    // A simplified placeholder implementation
    return new { Message = "Execution plan retrieval requires additional AMO/ADOMD.NET integration" };
}
```

#### Connection Pooling and Management

```csharp
// Implement a connection pool manager
public class TabularConnectionPool
{
    private static readonly ConcurrentDictionary<string, ConcurrentBag<TabularConnection>> _connectionPools = 
        new ConcurrentDictionary<string, ConcurrentBag<TabularConnection>>();
    
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _poolSemaphores = 
        new ConcurrentDictionary<string, SemaphoreSlim>();
    
    public static async Task<TabularConnection> GetConnectionAsync(PowerBiConfig config)
    {
        var poolKey = $"{config.Port}_{config.DbId}";
        
        var semaphore = _poolSemaphores.GetOrAdd(poolKey, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync();
        
        try
        {
            var pool = _connectionPools.GetOrAdd(poolKey, _ => new ConcurrentBag<TabularConnection>());
            
            if (pool.TryTake(out var connection))
            {
                if (await TestConnection(connection))
                    return connection;
                // Connection failed test, let it be disposed and create a new one
            }
            
            // Create a new connection
            return new TabularConnection(config);
        }
        finally
        {
            semaphore.Release();
        }
    }
    
    public static void ReturnConnection(PowerBiConfig config, TabularConnection connection)
    {
        if (connection == null) return;
        
        var poolKey = $"{config.Port}_{config.DbId}";
        if (_connectionPools.TryGetValue(poolKey, out var pool))
        {
            pool.Add(connection);
        }
    }
    
    private static async Task<bool> TestConnection(TabularConnection connection)
    {
        try
        {
            // Simple test query
            await connection.ExecAsync("EVALUATE ROW(\"Test\", 1)", QueryType.DAX);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
```

### Configuration & Infrastructure Changes

#### Enhanced Configuration System

```csharp
// Enhanced configuration with more options
public class PowerBiConfig
{
    public string Port { get; set; } = string.Empty;
    public string DbId { get; set; } = string.Empty;
    
    // New configuration options
    public TimeSpan DefaultQueryTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public int MaxConnections { get; set; } = 10;
    public string LogLevel { get; set; } = "Info";
    public bool EnableQueryCaching { get; set; } = false;
    public string OutputFormat { get; set; } = "Json"; // Json, Csv, Table
    
    // Configuration file support
    public static PowerBiConfig LoadFromFile(string path)
    {
        if (!File.Exists(path))
            return new PowerBiConfig();
            
        var json = File.ReadAllText(path);
        return System.Text.Json.JsonSerializer.Deserialize<PowerBiConfig>(json) ?? new PowerBiConfig();
    }
    
    public void SaveToFile(string path)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(this, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }
    
    // Validates the configuration settings
    public void Validate()
    {
        if (string.IsNullOrEmpty(Port))
            throw new InvalidOperationException("PBI_PORT not set");
        if (string.IsNullOrEmpty(DbId))
            throw new InvalidOperationException("PBI_DB_ID not set");
            
        // Validate other settings
        if (DefaultQueryTimeout < TimeSpan.FromSeconds(1))
            throw new InvalidOperationException("DefaultQueryTimeout must be at least 1 second");
            
        if (MaxConnections < 1)
            throw new InvalidOperationException("MaxConnections must be at least 1");
    }
}
```

## Prioritized Implementation Roadmap

1. **Phase 1 (Model Analysis Focus)**
   - Enhance DAX query execution with robust DEFINE support
   - Implement timeout controls and advanced error diagnostics
   - Improve model metadata exploration capabilities

2. **Phase 2 (Metadata Expansion)**
   - Implement hierarchy exploration
   - Add security role viewing
   - Develop dependency analysis for measures and columns

3. **Phase 3 (Performance & Analysis)**
   - Add query profiling and execution plan viewing
   - Implement connection pooling
   - Develop enhanced configuration system

By following this progressive implementation approach, the MCP MCPBI tools can gradually evolve into a robust, production-ready solution that empowers developers to more effectively work with PowerBI semantic models.

## Enhanced DAX DEFINE Support - Implementation Details

The DEFINE statement is one of the most powerful features in DAX, allowing for the creation of complex queries with reusable components. However, implementing robust support for DEFINE requires careful handling of multiple scenarios. This section outlines a detailed approach to enhancing DEFINE support in the MCP tools.

### Understanding DAX DEFINE Structure

A complete DAX query with a DEFINE block follows this general structure:

```
DEFINE
  [MEASURE/VAR/TABLE/COLUMN definitions]
EVALUATE
  [Expression]
[ORDER BY/START AT clauses]
```

There can only be one DEFINE block in a DAX query, but it can contain multiple measure, variable, table, or column definitions. A query can contain multiple EVALUATE statements, and the definitions in the DEFINE block apply to all EVALUATE statements in the query:

```
DEFINE
  MEASURE Table1[Measure1] = SUM(Table1[Value])
  VAR Variable1 = FILTER(Table1, Table1[Column] = "Value")
  TABLE Table2 = FILTER(Table1, Table1[Category] = "A")

EVALUATE
  Table2

EVALUATE
  ADDCOLUMNS(VALUES(Table1[Category]), "Total", [Measure1])
```

### Implementation Strategy

#### 1. Enhanced Parsing and Validation

```csharp
/// <summary>
/// Validates and parses a DAX query with a DEFINE block and EVALUATE statements
/// </summary>
private static (bool IsValid, string DefineBlock, string[] EvaluateBlocks, string[] OrderByBlocks) ParseDAXQuery(string query)
{
    // Normalize the query to handle different whitespace, line endings, etc.
    var normalizedQuery = NormalizeDAXQuery(query);
    
    // Use a proper parser approach instead of simple string matching
    // Look for a single DEFINE block that must precede all EVALUATE statements
    var defineMatch = Regex.Match(
        normalizedQuery, 
        @"\bDEFINE\b\s+((?:(?:MEASURE|VAR|TABLE|COLUMN)(?:(?!\bEVALUATE\b).)*)+)",
        RegexOptions.IgnoreCase | RegexOptions.Singleline
    );
    
    // Find all EVALUATE statements
    var evaluateMatches = Regex.Matches(
        normalizedQuery,
        @"\bEVALUATE\b\s+((?:(?!\bEVALUATE\b|\bORDER\s+BY\b|\bSTART\s+AT\b).)*)",
        RegexOptions.IgnoreCase | RegexOptions.Singleline
    );
    
    // Find all ORDER BY / START AT clauses
    var orderByMatches = Regex.Matches(
        normalizedQuery,
        @"\bORDER\s+BY\b\s+((?:(?!\bEVALUATE\b|\bSTART\s+AT\b).)*)|START\s+AT\s+(.*?)(?=$|\bEVALUATE\b)",
        RegexOptions.IgnoreCase | RegexOptions.Singleline
    );
    
    // Extract the parts
    string defineBlock = defineMatch.Success 
        ? defineMatch.Groups[1].Value.Trim() 
        : "";
        
    var evaluateBlocks = evaluateMatches
        .Select(m => m.Groups[1].Value.Trim())
        .ToArray();
        
    var orderByBlocks = orderByMatches
        .Select(m => (m.Groups[1].Value + m.Groups[2].Value).Trim())
        .Where(s => !string.IsNullOrEmpty(s))
        .ToArray();
    
    // Validate that:
    // 1. We have at least one EVALUATE statement
    // 2. If DEFINE exists, it comes before the first EVALUATE
    bool isValid = evaluateBlocks.Length > 0;
    
    if (defineMatch.Success)
    {
        // Check if DEFINE appears before the first EVALUATE
        int definePos = normalizedQuery.IndexOf("DEFINE", StringComparison.OrdinalIgnoreCase);
        int evaluatePos = normalizedQuery.IndexOf("EVALUATE", StringComparison.OrdinalIgnoreCase);
        isValid = isValid && (definePos < evaluatePos);
    }
    
    return (isValid, defineBlock, evaluateBlocks, orderByBlocks);
}

/// <summary>
/// Validates the structure of a DAX query according to proper syntax rules
/// </summary>
private static List<string> ValidateDAXQueryStructure(string query)
{
    var errors = new List<string>();
    
    // Check for basic requirements
    if (string.IsNullOrWhiteSpace(query))
    {
        errors.Add("Query cannot be empty.");
        return errors;
    }
    
    // Normalize the query for easier parsing
    var normalizedQuery = NormalizeDAXQuery(query);
    
    // Check if EVALUATE exists
    if (!normalizedQuery.Contains("EVALUATE", StringComparison.OrdinalIgnoreCase))
    {
        errors.Add("DAX query must contain at least one EVALUATE statement.");
    }
    
    // Check DEFINE block position if it exists
    if (normalizedQuery.Contains("DEFINE", StringComparison.OrdinalIgnoreCase))
    {
        int definePos = normalizedQuery.IndexOf("DEFINE", StringComparison.OrdinalIgnoreCase);
        int evaluatePos = normalizedQuery.IndexOf("EVALUATE", StringComparison.OrdinalIgnoreCase);
        
        if (definePos > evaluatePos)
        {
            errors.Add("DEFINE statement must come before any EVALUATE statement.");
        }
        
        // Check for multiple DEFINE blocks which is not allowed
        var defineMatches = Regex.Matches(normalizedQuery, @"\bDEFINE\b", RegexOptions.IgnoreCase);
        if (defineMatches.Count > 1)
        {
            errors.Add("Only one DEFINE block is allowed in a DAX query.");
        }
        
        // Check if DEFINE has at least one definition
        var defineContentMatch = Regex.Match(
            normalizedQuery,
            @"\bDEFINE\b\s+((?:MEASURE|VAR|TABLE|COLUMN)\s+.+?)(?=\bEVALUATE\b)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline
        );
        
        if (!defineContentMatch.Success || string.IsNullOrWhiteSpace(defineContentMatch.Groups[1].Value))
        {
            errors.Add("DEFINE block must contain at least one definition (MEASURE, VAR, TABLE, or COLUMN).");
        }
    }
    
    // Check for balanced parentheses
    CheckBalancedDelimiters(normalizedQuery, '(', ')', errors);
    
    // Check for balanced brackets
    CheckBalancedDelimiters(normalizedQuery, '[', ']', errors);
    
    // Check for balanced quotes (both single and double)
    CheckBalancedQuotes(normalizedQuery, errors);
    
    return errors;
}

/// <summary>
/// Normalizes a DAX query by standardizing whitespace and line endings
/// </summary>
private static string NormalizeDAXQuery(string query)
{
    // Replace all line endings with a standard form
    var normalized = Regex.Replace(query, @"\r\n?|\n", "\n");
    
    // Normalize whitespace in keywords but preserve it inside strings
    normalized = NormalizeWhitespacePreservingStrings(normalized);
    
    return normalized;
}

/// <summary>
/// Helper to normalize whitespace while preserving strings
/// </summary>
private static string NormalizeWhitespacePreservingStrings(string input)
{
    var result = new StringBuilder();
    bool inString = false;
    char stringDelimiter = '"';
    
    foreach (char c in input)
    {
        if (!inString && (c == '"' || c == '\''))
        {
            inString = true;
            stringDelimiter = c;
            result.Append(c);
        }
        else if (inString && c == stringDelimiter)
        {
            // Check for escaped string delimiter (doubled up)
            if (result.Length > 0 && result[result.Length - 1] == stringDelimiter)
            {
                result.Append(c); // It's an escape, not end of string
            }
            else
            {
                inString = false;
                result.Append(c);
            }
        }
        else if (!inString && char.IsWhiteSpace(c))
        {
            // If the previous char wasn't whitespace, add a single space
            if (result.Length > 0 && !char.IsWhiteSpace(result[result.Length - 1]))
            {
                result.Append(' ');
            }
            // Otherwise skip this whitespace (collapse multiple)
        }
        else
        {
            result.Append(c);
        }
    }
    
    return result.ToString();
}

/// <summary>
/// Checks if delimiters like parentheses and brackets are properly balanced
/// </summary>
private static void CheckBalancedDelimiters(string query, char openChar, char closeChar, List<string> errors)
{
    int count = 0;
    bool inString = false;
    char stringDelimiter = '"';
    
    for (int i = 0; i < query.Length; i++)
    {
        char c = query[i];
        
        // Skip checking inside string literals
        if ((c == '"' || c == '\'') && (i == 0 || query[i - 1] != '\\'))
        {
            if (!inString)
            {
                inString = true;
                stringDelimiter = c;
            }
            else if (c == stringDelimiter)
            {
                inString = false;
            }
            continue;
        }
        
        if (inString) continue;
        
        if (c == openChar) count++;
        else if (c == closeChar) count--;
        
        if (count < 0)
        {
            errors.Add($"Unbalanced delimiters: extra '{closeChar}' found.");
            return;
        }
    }
    
    if (count > 0)
    {
        errors.Add($"Unbalanced delimiters: missing {count} '{closeChar}' character(s).");
    }
}

/// <summary>
/// Checks if string delimiters (quotes) are properly balanced
/// </summary>
private static void CheckBalancedQuotes(string query, List<string> errors)
{
    // Check for unbalanced double quotes
    bool inDoubleQuoteString = false;
    int doubleQuoteCount = 0;
    
    // Check for unbalanced single quotes
    bool inSingleQuoteString = false;
    int singleQuoteCount = 0;
    
    for (int i = 0; i < query.Length; i++)
    {
        char c = query[i];
        
        if (c == '"' && (i == 0 || query[i - 1] != '\\'))
        {
            // Handle doubled quotes which are escapes in DAX
            if (i < query.Length - 1 && query[i + 1] == '"')
            {
                i++; // Skip the next quote
                continue;
            }
            
            doubleQuoteCount++;
            inDoubleQuoteString = !inDoubleQuoteString;
        }
        else if (c == '\'' && !inDoubleQuoteString)
        {
            // Handle doubled quotes which are escapes in DAX
            if (i < query.Length - 1 && query[i + 1] == '\'')
            {
                i++; // Skip the next quote
                continue;
            }
            
            singleQuoteCount++;
            inSingleQuoteString = !inSingleQuoteString;
        }
    }
    
    if (inDoubleQuoteString)
    {
        errors.Add("Unbalanced double quotes: string literal is not properly closed.");
    }
    
    if (inSingleQuoteString)
    {
        errors.Add("Unbalanced single quotes: table or column identifier is not properly closed.");
    }
}
```

#### 2. Robust Query Execution with Error Handling

```csharp
/// <summary>
/// Executes a DAX query that may contain a DEFINE block and multiple EVALUATE statements
/// </summary>
public static async Task<object> RunComplexQuery(string dax, QueryOptions options = null)
{
    options ??= new QueryOptions();
    var tabular = CreateConnection();
    
    try
    {
        // Parse and validate the query structure
        var (isValid, defineBlock, evaluateBlocks, orderByBlocks) = ParseDAXQuery(dax);
        
        if (!isValid)
        {
            // Add specific validation failures to help the user
            var validationErrors = ValidateDAXQueryStructure(dax);
            return new
            {
                Success = false,
                Errors = validationErrors,
                Message = "DAX query validation failed. See errors for details."
            };
        }
        
        // Execute with proper timeout handling
        using var cts = new CancellationTokenSource(options.Timeout);
        var result = await tabular.ExecAsync(dax, QueryType.DAX, cts.Token);
        
        // If requested, also return query metadata
        if (options.IncludeQueryMetadata)
        {
            // This would require extending the TabularConnection class to capture metadata
            return new
            {
                Results = result,
                QueryStructure = new
                {
                    HasDefineBlock = !string.IsNullOrEmpty(defineBlock),
                    EvaluateBlocks = evaluateBlocks.Length,
                    OrderByBlocks = orderByBlocks.Length
                }
            };
        }
        
        return result;
    }
    catch (OperationCanceledException)
    {
        return new
        {
            Success = false,
            Error = $"Query execution timed out after {options.Timeout.TotalSeconds} seconds"
        };
    }
    catch (Exception ex)
    {
        // Enhance error messages with more context about the DAX syntax
        var enhancedError = EnhanceDAXErrorMessage(ex.Message, dax);
        
        return new
        {
            Success = false,
            Error = enhancedError,
            OriginalError = ex.Message
        };
    }
}
```

#### 3. Error Message Enhancement

```csharp
/// <summary>
/// Enhances DAX error messages with more context and suggestions
/// </summary>
private static string EnhanceDAXErrorMessage(string originalError, string query)
{
    // Common DAX error patterns and more helpful messages
    var errorPatterns = new Dictionary<string, Func<string, string, string>>
    {
        {
            "A table cannot be found", 
            (err, q) => {
                // Extract table name from error
                var tableNameMatch = Regex.Match(err, @"table '([^']+)'");
                if (tableNameMatch.Success)
                {
                    var tableName = tableNameMatch.Groups[1].Value;
                    return $"Table '{tableName}' was not found in the model. Check for typos or if it's properly defined in a DEFINE TABLE statement.";
                }
                return err;
            }
        },
        {
            "A function with name", 
            (err, q) => {
                // Extract function name from error
                var funcNameMatch = Regex.Match(err, @"function '([^']+)'");
                if (funcNameMatch.Success)
                {
                    var funcName = funcNameMatch.Groups[1].Value;
                    var suggestion = SuggestSimilarDAXFunction(funcName);
                    return $"Function '{funcName}' was not recognized. {suggestion}";
                }
                return err;
            }
        },
        {
            "The DEFINE statement must precede", 
            (err, q) => "DEFINE statements must come before any EVALUATE statement. Check the order of your statements."
        },
        {
            "The column", 
            (err, q) => {
                // Extract column info from error 
                var colInfoMatch = Regex.Match(err, @"column '([^']+)'");
                if (colInfoMatch.Success)
                {
                    var colInfo = colInfoMatch.Groups[1].Value;
                    var parts = colInfo.Split('[', ']').Where(p => !string.IsNullOrEmpty(p)).ToList();
                    
                    if (parts.Count >= 2)
                    {
                        var tableName = parts[0];
                        var columnName = parts[1];
                        return $"Column '{columnName}' not found in table '{tableName}'. Verify the column exists or check for typos.";
                    }
                }
                return err;
            }
        }
    };
    
    // Try to match and enhance the error message
    foreach (var pattern in errorPatterns.Keys)
    {
        if (originalError.Contains(pattern, StringComparison.OrdinalIgnoreCase))
        {
            return errorPatterns[pattern](originalError, query);
        }
    }
    
    return originalError;
}

/// <summary>
/// Suggests similar DAX functions when a function name is not recognized
/// </summary>
private static string SuggestSimilarDAXFunction(string unknownFunction)
{
    // This would contain a list of common DAX functions to suggest similar ones
    var commonDaxFunctions = new List<string> { 
        "SUM", "AVERAGE", "COUNT", "MIN", "MAX", "FILTER", "CALCULATE", 
        "SUMMARIZE", "SUMMARIZECOLUMNS", "ADDCOLUMNS", "ALL", "ALLEXCEPT",
        "DISTINCTCOUNT", "VALUES", "RELATED", "RELATEDTABLE", "VAR", "RETURN"
        // Many more would be included in a real implementation
    };
    
    // Find closest matches using Levenshtein distance or similar algorithm
    var similarFunctions = commonDaxFunctions
        .Where(f => LevenshteinDistance(f, unknownFunction) <= 2)
        .Take(3)
        .ToList();
        
    if (similarFunctions.Any())
    {
        return $"Did you mean: {string.Join(", ", similarFunctions)}?";
    }
    
    return "Check function spelling or verify that it's a valid DAX function.";
}

/// <summary>
/// Simple Levenshtein distance implementation for string similarity
/// </summary>
private static int LevenshteinDistance(string s, string t)
{
    // Basic implementation of Levenshtein distance
    // For production use, consider using a more optimized algorithm
    // or a library with this functionality
    
    // Simplified placeholder implementation
    if (string.IsNullOrEmpty(s))
        return t?.Length ?? 0;
    if (string.IsNullOrEmpty(t))
        return s.Length;
        
    if (s.Equals(t, StringComparison.OrdinalIgnoreCase))
        return 0;
        
    // Actual implementation would calculate edit distance
    // For now, just return a value based on first char similarity
    return s[0].Equals(t[0], StringComparison.OrdinalIgnoreCase) ? 1 : 2;
}
```

#### 4. Testing Strategy for DEFINE Support

Robust testing is crucial for proper DEFINE support. The testing strategy should include:

```csharp
// Sample test cases outline (would be implemented in test fixtures)
public class DefineQueryTests
{
    [Fact]
    public async Task SimpleDefineQueryExecutesSuccessfully()
    {
        // Arrange
        var query = @"
            DEFINE
              MEASURE 'financials'[TotalSales] = SUM('financials'[Sales])
            EVALUATE
              ADDCOLUMNS(
                VALUES('financials'[Country]),
                ""Sales"", [TotalSales]
              )
        ";
        
        // Act
        var result = await DaxTools.RunQuery(query);
        
        // Assert
        Assert.NotNull(result);
        // Additional assertions on result structure
    }
    
    [Fact]
    public async Task ComplexDefineWithMultipleBlocksExecutesSuccessfully()
    {
        // Arrange
        var query = @"
            DEFINE
              MEASURE 'financials'[TotalSales] = SUM('financials'[Sales])
            DEFINE
              VAR MinYear = MIN('financials'[Year])
              VAR MaxYear = MAX('financials'[Year])
            DEFINE TABLE YearlyTotals =
              SUMMARIZECOLUMNS(
                'financials'[Year],
                ""Sales"", [TotalSales]
              )
            EVALUATE
              YearlyTotals
            ORDER BY 'financials'[Year] ASC
        ";
        
        // Act
        var result = await DaxTools.RunQuery(query);
        
        // Assert
        Assert.NotNull(result);
        // Additional assertions on result structure
    }
    
    [Fact]
    public async Task ErrorInDefineBlockProvidesHelpfulErrorMessage()
    {
        // Arrange
        var query = @"
            DEFINE
              MEASURE 'financials'[TotalSals] = SUMM('financials'[Sales]) // Intentional error
            EVALUATE
              ADDCOLUMNS(VALUES('financials'[Country]), ""Sales"", [TotalSals])
        ";
        
        // Act & Assert
        var ex = await Assert.ThrowsAsync<Exception>(() => DaxTools.RunQuery(query));
        Assert.Contains("Did you mean: SUM", ex.Message);
    }
}
```

### Testing with Real-World DAX DEFINE Examples

To properly validate our enhanced DEFINE block support, we should test with a variety of real-world DAX query patterns. Here are several examples that would exercise different aspects of the implementation:

#### Example 1: Basic DEFINE with Measures

```csharp
[Fact]
public async Task SimpleDefineWithMeasures()
{
    var query = @"
        DEFINE
            MEASURE 'financials'[Total Sales] = SUM('financials'[Sales])
            MEASURE 'financials'[Total Profit] = SUM('financials'[Profit])
            
        EVALUATE
            SUMMARIZECOLUMNS(
                'financials'[Year],
                ""Total Sales"", [Total Sales],
                ""Total Profit"", [Total Profit],
                ""Profit Margin"", DIVIDE([Total Profit], [Total Sales])
            )
    ";
    
    var result = await DaxTools.RunQuery(query);
    Assert.NotNull(result);
}
```

#### Example 2: DEFINE with Variables and Multiple EVALUATE Statements

```csharp
[Fact]
public async Task DefineWithVariablesAndMultipleEvaluates()
{
    var query = @"
        DEFINE
            VAR MinYear = MIN('financials'[Year])
            VAR MaxYear = MAX('financials'[Year])
            MEASURE 'financials'[YoY Growth] = 
                VAR CurrentYearSales = [Total Sales]
                VAR PrevYearSales = CALCULATE([Total Sales], PREVIOUSYEAR('financials'[Date]))
                RETURN
                    DIVIDE(CurrentYearSales - PrevYearSales, PrevYearSales)
                    
        EVALUATE
            ADDCOLUMNS(
                VALUES('financials'[Segment]),
                ""Min Year"", MinYear,
                ""Max Year"", MaxYear
            )
            
        EVALUATE
            SUMMARIZECOLUMNS(
                'financials'[Year],
                ""YoY Growth"", [YoY Growth]
            )
    ";
    
    var result = await DaxTools.RunQuery(query);
    Assert.NotNull(result);
}
```

#### Example 3: DEFINE with Table and Column Definitions

```csharp
[Fact]
public async Task DefineWithTableAndColumn()
{
    var query = @"
        DEFINE
            TABLE FilteredProducts = 
                FILTER('financials', 'financials'[Product] = ""Montana"")
            COLUMN FilteredProducts[ProfitPerUnit] = 
                DIVIDE(FilteredProducts[Profit], FilteredProducts[Units Sold])
                
        EVALUATE
            SUMMARIZECOLUMNS(
                FilteredProducts[Country],
                ""Average Profit Per Unit"", AVERAGE(FilteredProducts[ProfitPerUnit])
            )
    ";
    
    var result = await DaxTools.RunQuery(query);
    Assert.NotNull(result);
}
```

#### Example 4: Common Error Cases to Handle

```csharp
[Fact]
public async Task DetectMultipleDefineError()
{
    var query = @"
        DEFINE
            MEASURE 'financials'[Total Sales] = SUM('financials'[Sales])
            
        DEFINE  -- This second DEFINE should be detected as an error
            MEASURE 'financials'[Total Profit] = SUM('financials'[Profit])
            
        EVALUATE
            SUMMARIZECOLUMNS(
                'financials'[Year],
                ""Total Sales"", [Total Sales],
                ""Total Profit"", [Total Profit]
            )
    ";
    
    // This should throw an exception or return an error result
    var ex = await Assert.ThrowsAsync<Exception>(() => DaxTools.RunQuery(query));
    Assert.Contains("Only one DEFINE block is allowed", ex.Message);
}

[Fact]
public async Task DetectDefineAfterEvaluateError()
{
    var query = @"
        EVALUATE
            VALUES('financials'[Product])
            
        DEFINE  -- DEFINE after EVALUATE should be detected as an error
            MEASURE 'financials'[Total Sales] = SUM('financials'[Sales])
    ";
    
    // This should throw an exception or return an error result
    var ex = await Assert.ThrowsAsync<Exception>(() => DaxTools.RunQuery(query));
    Assert.Contains("DEFINE statement must come before", ex.Message);
}
```

### Practical Use Cases for PowerBI Semantic Model Development

The enhanced DEFINE support enables several important scenarios for PowerBI semantic model developers:

1. **Measure Testing and Optimization**

   Developers can test and optimize measure definitions without modifying the model:

   ```dax
   DEFINE
       -- Test a complex calculation

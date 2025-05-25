# Tidy 

Address the warning suppressions by adding proper XML documentation
Implement consistent error handling across all methods
Consider using a configuration provider rather than direct environment variables

Add more robust error handling with detailed error messages
Implement logging to track usage patterns and errors
Add retry logic for connection issues

## Review naming conventions

Use Verb-Noun Pattern Consistently for distinct tools

Get: For retrieving a single item or specific information
- GetMeasureDetails
- GetTableDetails
- GetImplicitMeasureDAX

List: For retrieving multiple items
- ListMeasures
- ListTables
- ListTableColumns (instead of GetTableColumns)
- ListTableRelationships (instead of GetTableRelationships)

Analyze: For tools that perform analysis and return insights
- AnalyzeQueryPerformance
- AnalyzeMeasureDependencies (instead of GetMeasureDependencies)
- AnalyzeModelHealth (instead of ModelHealthCheck)

Find: For discovery operations
- FindUnusedObjects (instead of DetectUnusedObjects)

Query: For querying operations
- QueryTableData (instead of PreviewTableData)
- RunQuery (implemented as replacement for EvaluateDAX)


# Improvements 

## Add lineage tracing tool

To enable client to get full DAX from implicit measures

´´ 
[McpServerTool, Description("Get full DAX from implicit measures.")]
public static async Task<object> GetImplicitMeasureDAX(string tableName, string columnName)
{
    var tabular = CreateConnection();
    
    // First get the column ID
    var columnIdQuery = $"SELECT [ID] FROM $SYSTEM.TMSCHEMA_COLUMNS WHERE [TABLEID] IN " +
                        $"(SELECT [ID] FROM $SYSTEM.TMSCHEMA_TABLES WHERE [NAME] = '{tableName}') " +
                        $"AND [NAME] = '{columnName}'";
    var columnIdResult = await tabular.ExecAsync(columnIdQuery, QueryType.DMV);
    var columnId = columnIdResult is IEnumerable<Dictionary<string, object?>> rows && rows.Any()
        ? rows.First()["ID"]?.ToString()
        : null;

    if (string.IsNullOrEmpty(columnId))
        throw new Exception($"Column '{columnName}' not found in table '{tableName}'");

    // Generate the implicit measure DAX
    var summarizeBy = await GetColumnSummarizeBy(tabular, columnId);
    var dax = GenerateImplicitMeasureDAX(tableName, columnName, summarizeBy);

    return new Dictionary<string, object>
    {
        { "ColumnName", columnName },
        { "TableName", tableName },
        { "ImplicitDAX", dax },
        { "SummarizeBy", summarizeBy }
    };
}
´´ 

## GetMeasureDependencies tool

´´
[McpServerTool, Description("Analyze measure dependencies.")]
public static async Task<object> GetMeasureDependencies(string measureName)
{
    var tabular = CreateConnection();
    // Query dependencies using DMV
    return await tabular.ExecAsync($"SELECT * FROM $SYSTEM.DISCOVER_CALC_DEPENDENCY WHERE OBJECT_TYPE = 'MEASURE' AND OBJECT_NAME = '{measureName}'", QueryType.DMV);
}

[McpServerTool, Description("Detect unused columns and measures.")]
public static async Task<object> DetectUnusedObjects()
{
    var tabular = CreateConnection();
    // Complex query to detect unused objects
    // This would require implementation based on dependency analysis
    return new { Message = "Not yet implemented" };
}
´´

## Performance Analysis Tool

Tool to analyze performance of a DAX expression.


´´
[McpServerTool, Description("Analyze query performance for a DAX expression.")]
public static async Task<object> AnalyzeQueryPerformance(string dax)
{
    var tabular = CreateConnection();
    // Use server timing and profiling to measure query performance
    var stopwatch = new System.Diagnostics.Stopwatch();
    stopwatch.Start();
    var result = await tabular.ExecAsync(dax, QueryType.DAX);
    stopwatch.Stop();
    
    return new Dictionary<string, object>
    {
        { "Query", dax },
        { "ExecutionTimeMs", stopwatch.ElapsedMilliseconds },
        { "Results", result }
    };
}
´´



## Model Health Check Tool

´´[McpServerTool, Description("Run model health check to identify potential issues.")]
public static async Task<object> ModelHealthCheck()
{
    var tabular = CreateConnection();
    var issues = new List<Dictionary<string, object>>();
    
    // Check for tables without relationships
    var tables = await tabular.ExecAsync("SELECT [ID], [NAME] FROM $SYSTEM.TMSCHEMA_TABLES WHERE [ISVISIBLE] = true", QueryType.DMV);
    // Check for high cardinality columns
    // Check for missing relationships
    // Check for calculation issues
    
    return issues;
}
´´
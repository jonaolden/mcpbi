using System.Text.Json;

using Microsoft.Extensions.Logging; // Added for ILogger
using Microsoft.Extensions.Logging.Abstractions; // Added for NullLogger

using pbi_local_mcp.Configuration;
using pbi_local_mcp.Core; // Added for ITabularConnection

namespace pbi_local_mcp.Tests;

/// <summary>
/// Integration‑style smoke tests – their only job is to prove that the tools connect to *whatever* model the
/// .env points to and that they do not throw. They make no assumptions about table or measure names.
/// </summary>
public class Tests
{
    private static string? _connStr;
    private static Dictionary<string, JsonElement> _toolConfig = new();
    private static readonly DaxTools _daxTools; // Added DaxTools instance

    /// <summary>
    /// Initializes test environment by loading configuration and setting up tool instances
    /// </summary>
    static Tests()
    {
        // Check for environment variables first (from command line or environment)
        string? port = Environment.GetEnvironmentVariable("PBI_PORT");
        string? dbId = Environment.GetEnvironmentVariable("PBI_DB_ID");

        Console.WriteLine($"[Setup] Command line environment - PBI_PORT: {port}, PBI_DB_ID: {dbId}");

        // Only load .env file if we don't have a port from command line
        if (string.IsNullOrEmpty(port))
        {
            // Locate the solution root (6 levels up from the compiled test DLL)
            string dir = AppContext.BaseDirectory;
            for (int i = 0; i < 6; i++)
            {
                dir = Path.GetDirectoryName(dir) ??
                    throw new DirectoryNotFoundException("Cannot find solution root.");
            }

            string envPath = Path.Combine(dir, ".env");
            Console.WriteLine($"[Setup] No PBI_PORT from command line, attempting to load .env from: {envPath}");

            if (File.Exists(envPath))
            {
                foreach (var line in File.ReadAllLines(envPath))
                {
                    var parts = line.Split('=', 2, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2)
                    {
                        var key = parts[0].Trim();
                        var value = parts[1].Trim();

                        // Only set if not already present in environment
                        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                        {
                            Environment.SetEnvironmentVariable(key, value);
                        }
                    }
                }
                Console.WriteLine("[Setup] .env file loaded.");

                // Re-read after loading .env
                port = Environment.GetEnvironmentVariable("PBI_PORT");
                if (string.IsNullOrEmpty(dbId))
                {
                    dbId = Environment.GetEnvironmentVariable("PBI_DB_ID");
                }
            }
            else
            {
                Console.WriteLine($"[Setup] .env file not found at {envPath}. Using defaults.");
            }
        }
        else
        {
            Console.WriteLine($"[Setup] Using PBI_PORT from command line: {port}. Skipping .env file.");
        }

        // Use default values if still not available
        if (string.IsNullOrEmpty(port))
        {
            port = "62678"; // Default test port
            Console.WriteLine($"[Setup] PBI_PORT not found, using default: {port}");
        }

        Console.WriteLine($"[Setup] Final configuration - PBI_PORT: {port}, PBI_DB_ID: {dbId ?? "NOT_SET"}");

        // Initialize DaxTools instance with auto-discovery if dbId is not provided
        ITabularConnection tabularConnection;
        ILogger<DaxTools> logger = NullLogger<DaxTools>.Instance;

        if (string.IsNullOrEmpty(dbId))
        {
            Console.WriteLine($"[Setup] PBI_DB_ID not provided, attempting database auto-discovery on port {port}");
            try
            {
                // Try to discover and connect to the first available database
                var connectionLogger = NullLogger<TabularConnection>.Instance;
                var discoveryTask = TabularConnection.CreateWithDiscoveryAsync(port, connectionLogger);
                tabularConnection = discoveryTask.GetAwaiter().GetResult(); // Synchronous wait in static constructor
                Console.WriteLine($"[Setup] Successfully connected with auto-discovered database");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Setup] Database auto-discovery failed: {ex.Message}");
                Console.WriteLine($"[Setup] Using fallback database ID: TestDB");
                dbId = "TestDB";
                var config = new PowerBiConfig { Port = port, DbId = dbId };
                tabularConnection = new TabularConnection(config);
            }
        }
        else
        {
            Console.WriteLine($"[Setup] Using provided PBI_DB_ID: {dbId}");
            var config = new PowerBiConfig { Port = port, DbId = dbId };
            tabularConnection = new TabularConnection(config);
        }

        _connStr = $"Provider=MSOLAP;Data Source=localhost:{port};" +
                  $"Initial Catalog={dbId ?? "auto-discovered"};Integrated Security=SSPI;";
        Console.WriteLine($"[Setup] Connection string for tests: {_connStr}");

        _daxTools = new DaxTools(tabularConnection, logger); // Instantiate DaxTools

        // Load tooltest.config.json
        string dir2 = AppContext.BaseDirectory;
        for (int i = 0; i < 6; i++)
        {
            dir2 = Path.GetDirectoryName(dir2) ??
                throw new DirectoryNotFoundException("Cannot find solution root.");
        }

        string configPath = Path.Combine(dir2, "pbi-local-mcp", "pbi-local-mcp.Tests",
            "tooltest.config.json");

        if (File.Exists(configPath))
        {
            var configJson = File.ReadAllText(configPath);
            var doc = JsonDocument.Parse(configJson);
            _toolConfig = doc.RootElement.EnumerateObject()
                .ToDictionary(p => p.Name, p => p.Value.Clone());
            Console.WriteLine($"[Setup] Loaded tooltest.config.json with {_toolConfig.Count} tool configs.");
        }
        else
        {
            Console.WriteLine($"[Setup] tooltest.config.json not found at {configPath}. Using empty config.");
            _toolConfig = new Dictionary<string, JsonElement>();
        }
    }

    /// <summary>
    /// Verifies that the test infrastructure is working correctly
    /// </summary>
    [Fact]
    public void TestInfrastructureUp()
    {
        Console.WriteLine("[TestInfrastructureUp] Verifying basic assertion.");
        Assert.True(true);
        Console.WriteLine("[TestInfrastructureUp] Basic assertion passed.");
    }

    /// <summary>
    /// Tests that the ListMeasures tool functions without throwing exceptions
    /// </summary>
    [Fact]
    public async Task ListMeasuresTool_DoesNotThrow()
    {
        var args = _toolConfig["listMeasures"];
        string tableName = args.TryGetProperty("tableName", out var t) ?
            t.GetString() ?? "" : "";
        Console.WriteLine($"\n[ListMeasuresTool_DoesNotThrow] Listing measures for table: {tableName}");

        var response = await _daxTools.ListMeasures(tableName); // Changed to instance call
        LogToolResponse(response);

        var result = ExtractDataFromResponse(response);
        Assert.IsAssignableFrom<IEnumerable<Dictionary<string, object?>>>(result);
        Console.WriteLine(
            $"[ListMeasuresTool_DoesNotThrow] Found {((IEnumerable<Dictionary<string, object?>>)result).Count()} measures.");
    }

    /// <summary>
    /// Tests that the PreviewData tool functions without throwing exceptions
    /// </summary>
    [Fact]
    public async Task PreviewDataTool_DoesNotThrow()
    {
        var args = _toolConfig["previewTableData"];
        string tableName = args.GetProperty("tableName").GetString()!;
        int topN = args.TryGetProperty("topN", out var n) ? n.GetInt32() : 10;
        Console.WriteLine(
            $"\n[PreviewDataTool_DoesNotThrow] Previewing {topN} rows from table: {tableName}");

        var response = await _daxTools.PreviewTableData(tableName, topN); // Changed to instance call
        LogToolResponse(response);

        var result = ExtractDataFromResponse(response);
        Assert.IsAssignableFrom<IEnumerable<Dictionary<string, object?>>>(result);
        var rows = (IEnumerable<Dictionary<string, object?>>)result;
        Console.WriteLine($"[PreviewDataTool_DoesNotThrow] Retrieved {rows.Count()} rows.");
    }

    /// <summary>
    /// Tests that the GetTableDetails tool functions without throwing exceptions
    /// </summary>
    [Fact]
    public async Task GetTableDetailsTool_DoesNotThrow()
    {
        var args = _toolConfig["getTableDetails"];
        string tableName = args.GetProperty("tableName").GetString()!;
        Console.WriteLine(
            $"\n[GetTableDetailsTool_DoesNotThrow] Getting details for table: {tableName}");

        var response = await _daxTools.GetTableDetails(tableName); // Changed to instance call
        LogToolResponse(response);

        var result = ExtractDataFromResponse(response);
        Assert.IsAssignableFrom<IEnumerable<Dictionary<string, object?>>>(result);
        var rows = (IEnumerable<Dictionary<string, object?>>)result;
        Console.WriteLine(
            $"[GetTableDetailsTool_DoesNotThrow] Retrieved details with {rows.Count()} rows.");
    }

    /// <summary>
    /// Tests that the GetMeasureDetails tool functions without throwing exceptions
    /// </summary>
    [Fact]
    public async Task GetMeasureDetailsTool_DoesNotThrow()
    {
        var args = _toolConfig["getMeasureDetails"];
        string measureName = args.GetProperty("measureName").GetString()!;
        Console.WriteLine(
            $"\n[GetMeasureDetailsTool_DoesNotThrow] Getting details for measure: {measureName}");

        var response = await _daxTools.GetMeasureDetails(measureName); // Changed to instance call
        LogToolResponse(response);

        var result = ExtractDataFromResponse(response);
        Assert.IsAssignableFrom<IEnumerable<Dictionary<string, object?>>>(result);
        var rows = (IEnumerable<Dictionary<string, object?>>)result;
        Console.WriteLine(
            $"[GetMeasureDetailsTool_DoesNotThrow] Retrieved details with {rows.Count()} rows.");
    }

    /// <summary>
    /// Tests that the ListTables tool functions without throwing exceptions
    /// </summary>
    [Fact]
    public async Task ListTablesTool_DoesNotThrow()
    {
        Console.WriteLine("\n[ListTablesTool_DoesNotThrow] Listing all tables");

        var response = await _daxTools.ListTables(); // Changed to instance call
        LogToolResponse(response);

        var result = ExtractDataFromResponse(response);
        Assert.IsAssignableFrom<IEnumerable<Dictionary<string, object?>>>(result);
        var rows = (IEnumerable<Dictionary<string, object?>>)result;
        Console.WriteLine($"[ListTablesTool_DoesNotThrow] Found {rows.Count()} tables.");
    }

    /// <summary>
    /// Tests that the GetTableColumns tool functions without throwing exceptions
    /// </summary>
    [Fact]
    public async Task GetTableColumnsTool_DoesNotThrow()
    {
        var args = _toolConfig["getTableColumns"];
        string tableName = args.GetProperty("tableName").GetString()!;
        Console.WriteLine(
            $"\n[GetTableColumnsTool_DoesNotThrow] Getting columns for table: {tableName}");

        var response = await _daxTools.GetTableColumns(tableName); // Changed to instance call
        LogToolResponse(response);

        var result = ExtractDataFromResponse(response);
        Assert.IsAssignableFrom<IEnumerable<Dictionary<string, object?>>>(result);
        var rows = (IEnumerable<Dictionary<string, object?>>)result;
        Console.WriteLine($"[GetTableColumnsTool_DoesNotThrow] Found {rows.Count()} columns.");
    }

    /// <summary>
    /// Tests that the GetTableRelationships tool functions without throwing exceptions
    /// </summary>
    [Fact]
    public async Task GetTableRelationshipsTool_DoesNotThrow()
    {
        var args = _toolConfig["getTableRelationships"];
        string tableName = args.GetProperty("tableName").GetString()!;
        Console.WriteLine(
            $"\n[GetTableRelationshipsTool_DoesNotThrow] Getting relationships for table: {tableName}");

        var response = await _daxTools.GetTableRelationships(tableName); // Changed to instance call
        LogToolResponse(response);

        var result = ExtractDataFromResponse(response);
        Assert.IsAssignableFrom<IEnumerable<Dictionary<string, object?>>>(result);
        var rows = (IEnumerable<Dictionary<string, object?>>)result;
        Console.WriteLine(
            $"[GetTableRelationshipsTool_DoesNotThrow] Found {rows.Count()} relationships.");
    }

    internal static void LogToolResponse(object response)
    {
        Console.WriteLine("\nResponse Content:");
        Console.WriteLine(JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true }));
    }

    private void AssertToolResultIsCollectionOrError(object result, string toolNameForMessage)
    {
        if (result is IEnumerable<object> listResult)
        {
            Assert.NotNull(listResult);
            Console.WriteLine(
                $"[{toolNameForMessage}] Result is IEnumerable<object> with {listResult.Count()} items.");
        }
        else
        {
            AssertToolResultIsError(result, toolNameForMessage);
        }
    }

    private void AssertToolResultIsError(object result, string toolNameForMessage)
    {
        var type = result.GetType();
        var errorProp = type.GetProperty("error");
        Assert.NotNull(errorProp);
        var errorMessage = errorProp.GetValue(result) as string;
        Assert.False(string.IsNullOrWhiteSpace(errorMessage),
            $"{toolNameForMessage} returned an empty error message.");
        Console.WriteLine($"[{toolNameForMessage}] Correctly returned error: {errorMessage}");
    }

    internal static IEnumerable<Dictionary<string, object?>> ExtractDataFromResponse(object response)
    {
        Assert.NotNull(response);
        if (response is IEnumerable<Dictionary<string, object?>> rows)
        {
            return rows;
        }
        throw new InvalidOperationException("Response is not a collection of dictionaries.");
    }
}

/// <summary>
/// Comprehensive tests for the enhanced RunQuery method with DEFINE block support
/// </summary>
public class DaxToolsRunQueryTests
{
    private static readonly Dictionary<string, JsonElement> _toolConfig;
    private static readonly DaxTools _daxTools; // Added DaxTools instance

    static DaxToolsRunQueryTests()
    {
        // Check for environment variables first (from command line or environment)
        string? port = Environment.GetEnvironmentVariable("PBI_PORT");
        string? dbId = Environment.GetEnvironmentVariable("PBI_DB_ID");

        Console.WriteLine($"[DaxToolsRunQueryTests Setup] Command line environment - PBI_PORT: {port}, PBI_DB_ID: {dbId}");

        // Only load .env file if we don't have a port from command line
        if (string.IsNullOrEmpty(port))
        {
            string dir = AppContext.BaseDirectory;
            for (int i = 0; i < 6; i++)
            {
                dir = Path.GetDirectoryName(dir) ??
                    throw new DirectoryNotFoundException("Cannot find solution root.");
            }

            // Load .env for connection details
            string envPath = Path.Combine(dir, ".env");
            Console.WriteLine($"[DaxToolsRunQueryTests Setup] No PBI_PORT from command line, attempting to load .env from: {envPath}");

            if (File.Exists(envPath))
            {
                foreach (var line in File.ReadAllLines(envPath))
                {
                    var parts = line.Split('=', 2, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2)
                    {
                        var key = parts[0].Trim();
                        var value = parts[1].Trim();

                        // Only set if not already present in environment
                        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                        {
                            Environment.SetEnvironmentVariable(key, value);
                        }
                    }
                }

                // Re-read after loading .env
                port = Environment.GetEnvironmentVariable("PBI_PORT");
                if (string.IsNullOrEmpty(dbId))
                {
                    dbId = Environment.GetEnvironmentVariable("PBI_DB_ID");
                }
            }
            else
            {
                Console.WriteLine($"[DaxToolsRunQueryTests Setup] .env file not found at {envPath}. Using defaults.");
            }
        }
        else
        {
            Console.WriteLine($"[DaxToolsRunQueryTests Setup] Using PBI_PORT from command line: {port}. Skipping .env file.");
        }

        // Use default values if still not available
        if (string.IsNullOrEmpty(port))
        {
            port = "53437"; // Default test port
            Console.WriteLine($"[DaxToolsRunQueryTests Setup] PBI_PORT not found, using default: {port}");
        }

        Console.WriteLine($"[DaxToolsRunQueryTests Setup] Final configuration - PBI_PORT: {port}, PBI_DB_ID: {dbId ?? "NOT_SET"}");

        // Initialize DaxTools instance with auto-discovery if dbId is not provided
        ITabularConnection tabularConnection;
        ILogger<DaxTools> logger = NullLogger<DaxTools>.Instance;

        if (string.IsNullOrEmpty(dbId))
        {
            Console.WriteLine($"[DaxToolsRunQueryTests Setup] PBI_DB_ID not provided, attempting database auto-discovery on port {port}");
            try
            {
                // Try to discover and connect to the first available database
                var connectionLogger = NullLogger<TabularConnection>.Instance;
                var discoveryTask = TabularConnection.CreateWithDiscoveryAsync(port, connectionLogger);
                tabularConnection = discoveryTask.GetAwaiter().GetResult(); // Synchronous wait in static constructor
                Console.WriteLine($"[DaxToolsRunQueryTests Setup] Successfully connected with auto-discovered database");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DaxToolsRunQueryTests Setup] Database auto-discovery failed: {ex.Message}");
                Console.WriteLine($"[DaxToolsRunQueryTests Setup] Using fallback database ID: TestDB");
                dbId = "TestDB";
                var powerBiConfig = new PowerBiConfig { Port = port, DbId = dbId };
                tabularConnection = new TabularConnection(powerBiConfig);
            }
        }
        else
        {
            Console.WriteLine($"[DaxToolsRunQueryTests Setup] Using provided PBI_DB_ID: {dbId}");
            var powerBiConfig = new PowerBiConfig { Port = port, DbId = dbId };
            tabularConnection = new TabularConnection(powerBiConfig);
        }

        _daxTools = new DaxTools(tabularConnection, logger);

        // Load tooltest.config.json
        string dir2 = AppContext.BaseDirectory;
        for (int i = 0; i < 6; i++)
        {
            dir2 = Path.GetDirectoryName(dir2) ??
                throw new DirectoryNotFoundException("Cannot find solution root.");
        }

        string configPath = Path.Combine(dir2, "pbi-local-mcp", "pbi-local-mcp.Tests",
            "tooltest.config.json");
        if (!File.Exists(configPath))
        {
            Console.WriteLine($"[DaxToolsRunQueryTests Setup] tooltest.config.json not found at {configPath}. Using empty config.");
            _toolConfig = new Dictionary<string, JsonElement>();
            return;
        }

        var configJson = File.ReadAllText(configPath);
        var doc = JsonDocument.Parse(configJson);
        _toolConfig = doc.RootElement.EnumerateObject()
            .ToDictionary(p => p.Name, p => p.Value.Clone());
        Console.WriteLine($"[DaxToolsRunQueryTests Setup] Loaded tooltest.config.json with {_toolConfig.Count} tool configs.");
    }

    /// <summary>
    /// Tests that RunQuery executes a simple DAX expression without any definitions.
    /// </summary>
    [Fact]
    public async Task RunQuery_NoDefinitions_DoesNotThrow()
    {
        Console.WriteLine("\n[RunQuery_NoDefinitions_DoesNotThrow] Testing RunQuery without definitions");
        var args = _toolConfig["runQueryNoDefinitions"];
        string expression = args.GetProperty("expression").GetString()!;
        int topN = args.GetProperty("topN").GetInt32();

        var result = await _daxTools.RunQuery(expression, topN); // Changed to instance call
        Assert.NotNull(result);
        Console.WriteLine("[RunQuery_NoDefinitions_DoesNotThrow] Successfully executed query without definitions");
    }

    /// <summary>
    /// Tests that RunQuery executes a DAX expression with a VAR definition.
    /// </summary>
    [Fact]
    public async Task RunQuery_WithVarDefinition_DoesNotThrow()
    {
        Console.WriteLine("\n[RunQuery_WithVarDefinition_DoesNotThrow] Testing RunQuery with VAR definition");
        var args = _toolConfig["runQueryWithVarDefinition"];
        string expression = args.GetProperty("expression").GetString()!;
        int topN = args.GetProperty("topN").GetInt32();
        var result = await _daxTools.RunQuery(expression, topN); // Changed to instance call
        Assert.NotNull(result);
        Console.WriteLine("[RunQuery_WithVarDefinition_DoesNotThrow] Successfully executed query with VAR definition");
    }

    /// <summary>
    /// Tests that RunQuery executes a DAX expression with a MEASURE definition.
    /// </summary>
    [Fact]
    public async Task RunQuery_WithMeasureDefinition_DoesNotThrow()
    {
        Console.WriteLine("\n[RunQuery_WithMeasureDefinition_DoesNotThrow] Testing RunQuery with MEASURE definition");
        var args = _toolConfig["runQueryWithMeasureDefinition"];
        string expression = args.GetProperty("expression").GetString()!;
        int topN = args.GetProperty("topN").GetInt32();
        var result = await _daxTools.RunQuery(expression, topN); // Changed to instance call
        Assert.NotNull(result);
        Console.WriteLine("[RunQuery_WithMeasureDefinition_DoesNotThrow] Successfully executed query with MEASURE definition");
    }

    /// <summary>
    /// Tests that RunQuery executes a DAX expression with multiple definitions of different types.
    /// </summary>
    [Fact]
    public async Task RunQuery_WithMultipleDefinitions_DoesNotThrow()
    {
        Console.WriteLine("\n[RunQuery_WithMultipleDefinitions_DoesNotThrow] Testing RunQuery with multiple definitions");
        var args = _toolConfig["runQueryWithMultipleDefinitions"];
        string expression = args.GetProperty("expression").GetString()!;
        int topN = args.GetProperty("topN").GetInt32();
        var result = await _daxTools.RunQuery(expression, topN); // Changed to instance call
        Assert.NotNull(result);
        Console.WriteLine("[RunQuery_WithMultipleDefinitions_DoesNotThrow] Successfully executed query with multiple definitions");
    }

    /// <summary>
    /// Tests that RunQuery returns structured error response when a DEFINE query is missing an EVALUATE statement.
    /// </summary>
    [Fact]
    public async Task RunQuery_DefineWithoutEvaluate_ReturnsStructuredErrorResponse()
    {
        Console.WriteLine("\n[RunQuery_DefineWithoutEvaluate_ReturnsStructuredErrorResponse] Testing DEFINE without EVALUATE");
        string invalidDax = "DEFINE MEASURE Sales[Total] = SUM(Sales[Amount])";

        var result = await _daxTools.RunQuery(invalidDax, 0);

        // Verify structured error response
        Assert.NotNull(result);
        var resultType = result.GetType();
        var successProperty = resultType.GetProperty("Success");
        Assert.NotNull(successProperty);
        Assert.False((bool)successProperty.GetValue(result)!);

        var errorCategoryProperty = resultType.GetProperty("ErrorCategory");
        Assert.NotNull(errorCategoryProperty);
        Assert.Equal("validation", errorCategoryProperty.GetValue(result));

        Console.WriteLine("[RunQuery_DefineWithoutEvaluate_ReturnsStructuredErrorResponse] Correctly returned structured error response.");
    }

    /// <summary>
    /// Tests that RunQuery returns structured error response for unbalanced parentheses.
    /// </summary>
    [Fact]
    public async Task RunQuery_UnbalancedParentheses_ReturnsStructuredErrorResponse()
    {
        Console.WriteLine("\n[RunQuery_UnbalancedParentheses_ReturnsStructuredErrorResponse] Testing unbalanced parentheses");
        string invalidDax = "DEFINE VAR X = (1 + 2 EVALUATE {X}";

        var result = await _daxTools.RunQuery(invalidDax, 0);

        // Verify structured error response
        Assert.NotNull(result);
        var resultType = result.GetType();
        var successProperty = resultType.GetProperty("Success");
        Assert.NotNull(successProperty);
        Assert.False((bool)successProperty.GetValue(result)!);

        Console.WriteLine("[RunQuery_UnbalancedParentheses_ReturnsStructuredErrorResponse] Correctly returned structured error response.");
    }

    /// <summary>
    /// Tests that RunQuery returns structured error response for unbalanced brackets.
    /// </summary>
    [Fact]
    public async Task RunQuery_UnbalancedBrackets_ReturnsStructuredErrorResponse()
    {
        Console.WriteLine("\n[RunQuery_UnbalancedBrackets_ReturnsStructuredErrorResponse] Testing unbalanced brackets");
        string invalidDax = "DEFINE MEASURE Sales[Total = SUM(Sales[Amount]) EVALUATE {1}";

        var result = await _daxTools.RunQuery(invalidDax, 0);

        // Verify structured error response
        Assert.NotNull(result);
        var resultType = result.GetType();
        var successProperty = resultType.GetProperty("Success");
        Assert.NotNull(successProperty);
        Assert.False((bool)successProperty.GetValue(result)!);

        var errorCategoryProperty = resultType.GetProperty("ErrorCategory");
        Assert.NotNull(errorCategoryProperty);
        Assert.Equal("validation", errorCategoryProperty.GetValue(result));

        Console.WriteLine("[RunQuery_UnbalancedBrackets_ReturnsStructuredErrorResponse] Correctly returned structured error response.");
    }

    /// <summary>
    /// Tests that RunQuery correctly executes a query with a DEFINE block and executes without throwing.
    /// </summary>
    [Fact]
    public async Task RunQuery_DefinitionOrderingTest_DoesNotThrow()
    {
        Console.WriteLine("\n[RunQuery_DefinitionOrderingTest_DoesNotThrow] Testing definition ordering (VAR > TABLE > COLUMN > MEASURE)");
        var args = _toolConfig["runQueryDefinitionOrdering"];
        string expression = args.GetProperty("expression").GetString()!;
        int topN = args.GetProperty("topN").GetInt32();
        var result = await _daxTools.RunQuery(expression, topN); // Changed to instance call
        Assert.NotNull(result);
        Console.WriteLine("[RunQuery_DefinitionOrderingTest_DoesNotThrow] Successfully executed query with mixed definition types");
    }

    [Fact]
    public async Task RunQuery_BasicExpression_DoesNotThrow()
    {
        var args = _toolConfig["runQueryNoDefinitions"];
        string expr = args.GetProperty("expression").GetString()!;
        int topN = args.TryGetProperty("topN", out var n) ? n.GetInt32() : 0;
        Console.WriteLine($"\n[RunQuery_BasicExpression_DoesNotThrow] Running DAX expression: {expr}");

        var response = await _daxTools.RunQuery(expr, topN); // Changed to instance call
        Tests.LogToolResponse(response);

        var result = Tests.ExtractDataFromResponse(response);
        Assert.IsAssignableFrom<IEnumerable<Dictionary<string, object?>>>(result);
        var rows = (IEnumerable<Dictionary<string, object?>>)result;
        Assert.Single(rows);
        Assert.Equal(2L, Convert.ToInt64(rows.First()["[Value]"]));
        Console.WriteLine($"[RunQuery_BasicExpression_DoesNotThrow] Retrieved {rows.Count()} rows, with value {rows.First()["[Value]"]}.");
    }

    [Fact]
    public async Task RunQuery_MultipleDefineBlocks_ReturnsStructuredErrorResponse()
    {
        Console.WriteLine("\n[RunQuery_MultipleDefineBlocks_ReturnsStructuredErrorResponse] Testing multiple DEFINE blocks");
        string invalidDax = @"
                DEFINE MEASURE Sales[Total] = SUM(Sales[Amount])
                DEFINE VAR X = 1
                EVALUATE {X}";

        var result = await _daxTools.RunQuery(invalidDax, 0);

        // Verify structured error response
        Assert.NotNull(result);
        var resultType = result.GetType();
        var successProperty = resultType.GetProperty("Success");
        Assert.NotNull(successProperty);
        Assert.False((bool)successProperty.GetValue(result)!);

        var errorCategoryProperty = resultType.GetProperty("ErrorCategory");
        Assert.NotNull(errorCategoryProperty);
        Assert.Equal("validation", errorCategoryProperty.GetValue(result));

        Console.WriteLine("[RunQuery_MultipleDefineBlocks_ReturnsStructuredErrorResponse] Correctly returned structured error response.");
    }

    [Fact]
    public async Task RunQuery_DefineAfterEvaluate_ReturnsStructuredErrorResponse()
    {
        Console.WriteLine("\n[RunQuery_DefineAfterEvaluate_ReturnsStructuredErrorResponse] Testing DEFINE after EVALUATE");
        string invalidDax = @"
                EVALUATE {1}
                DEFINE MEASURE Sales[Total] = SUM(Sales[Amount])";

        var result = await _daxTools.RunQuery(invalidDax, 0);

        // Verify structured error response
        Assert.NotNull(result);
        var resultType = result.GetType();
        var successProperty = resultType.GetProperty("Success");
        Assert.NotNull(successProperty);
        Assert.False((bool)successProperty.GetValue(result)!);

        var errorCategoryProperty = resultType.GetProperty("ErrorCategory");
        Assert.NotNull(errorCategoryProperty);
        Assert.Equal("validation", errorCategoryProperty.GetValue(result));

        Console.WriteLine("[RunQuery_DefineAfterEvaluate_ReturnsStructuredErrorResponse] Correctly returned structured error response.");
    }

    [Fact]
    public async Task RunQuery_EmptyDefineBlock_ReturnsStructuredErrorResponse()
    {
        Console.WriteLine("\n[RunQuery_EmptyDefineBlock_ReturnsStructuredErrorResponse] Testing empty DEFINE block");
        string invalidDax = @"
                DEFINE
                EVALUATE {1}";

        var result = await _daxTools.RunQuery(invalidDax, 0);

        // Verify structured error response
        Assert.NotNull(result);
        var resultType = result.GetType();
        var successProperty = resultType.GetProperty("Success");
        Assert.NotNull(successProperty);
        Assert.False((bool)successProperty.GetValue(result)!);

        var errorCategoryProperty = resultType.GetProperty("ErrorCategory");
        Assert.NotNull(errorCategoryProperty);
        Assert.Equal("validation", errorCategoryProperty.GetValue(result));

        Console.WriteLine("[RunQuery_EmptyDefineBlock_ReturnsStructuredErrorResponse] Correctly returned structured error response.");
    }

    [Fact]
    public async Task RunQuery_DefineBlockWithNoValidDefinition_ReturnsStructuredErrorResponse()
    {
        Console.WriteLine("\n[RunQuery_DefineBlockWithNoValidDefinition_ReturnsStructuredErrorResponse] Testing DEFINE block with no valid definition keyword");
        string invalidDax = @"
                DEFINE
                  MyVar = 10  // Missing VAR keyword
                EVALUATE {1}";

        var result = await _daxTools.RunQuery(invalidDax, 0);

        // Verify structured error response
        Assert.NotNull(result);
        var resultType = result.GetType();
        var successProperty = resultType.GetProperty("Success");
        Assert.NotNull(successProperty);
        Assert.False((bool)successProperty.GetValue(result)!);

        var errorCategoryProperty = resultType.GetProperty("ErrorCategory");
        Assert.NotNull(errorCategoryProperty);
        Assert.Equal("validation", errorCategoryProperty.GetValue(result));

        Console.WriteLine("[RunQuery_DefineBlockWithNoValidDefinition_ReturnsStructuredErrorResponse] Correctly returned structured error response.");
    }

    [Fact]
    public async Task RunQuery_UnbalancedSingleQuotes_ReturnsStructuredErrorResponse()
    {
        Console.WriteLine("\n[RunQuery_UnbalancedSingleQuotes_ReturnsStructuredErrorResponse] Testing unbalanced single quotes");
        string invalidDax = "EVALUATE 'Sales[Amount]";

        var result = await _daxTools.RunQuery(invalidDax, 0);

        // Verify structured error response
        Assert.NotNull(result);
        var resultType = result.GetType();
        var successProperty = resultType.GetProperty("Success");
        Assert.NotNull(successProperty);
        Assert.False((bool)successProperty.GetValue(result)!);

        var errorCategoryProperty = resultType.GetProperty("ErrorCategory");
        Assert.NotNull(errorCategoryProperty);
        Assert.Equal("validation", errorCategoryProperty.GetValue(result));

        Console.WriteLine("[RunQuery_UnbalancedSingleQuotes_ReturnsStructuredErrorResponse] Correctly returned structured error response.");
    }

    [Fact]
    public async Task RunQuery_UnbalancedDoubleQuotes_ReturnsStructuredErrorResponse()
    {
        Console.WriteLine("\n[RunQuery_UnbalancedDoubleQuotes_ReturnsStructuredErrorResponse] Testing unbalanced double quotes");
        string invalidDax = "EVALUATE ROW(\"Value\", \"Hello World)";

        var result = await _daxTools.RunQuery(invalidDax, 0);

        // Verify structured error response
        Assert.NotNull(result);
        var resultType = result.GetType();
        var successProperty = resultType.GetProperty("Success");
        Assert.NotNull(successProperty);
        Assert.False((bool)successProperty.GetValue(result)!);

        var errorCategoryProperty = resultType.GetProperty("ErrorCategory");
        Assert.NotNull(errorCategoryProperty);
        Assert.Equal("validation", errorCategoryProperty.GetValue(result));

        Console.WriteLine("[RunQuery_UnbalancedDoubleQuotes_ReturnsStructuredErrorResponse] Correctly returned structured error response.");
    }

    [Fact]
    public async Task RunQuery_QueryIsEmpty_ReturnsStructuredErrorResponse()
    {
        Console.WriteLine("\n[RunQuery_QueryIsEmpty_ReturnsStructuredErrorResponse] Testing empty query");
        string invalidDax = "";

        var result = await _daxTools.RunQuery(invalidDax, 0);

        // Verify structured error response
        Assert.NotNull(result);
        var resultType = result.GetType();
        var successProperty = resultType.GetProperty("Success");
        Assert.NotNull(successProperty);
        Assert.False((bool)successProperty.GetValue(result)!);

        var errorCategoryProperty = resultType.GetProperty("ErrorCategory");
        Assert.NotNull(errorCategoryProperty);
        Assert.Equal("validation", errorCategoryProperty.GetValue(result));

        Console.WriteLine("[RunQuery_QueryIsEmpty_ReturnsStructuredErrorResponse] Correctly returned structured error response.");
    }

    [Fact]
    public async Task RunQuery_QueryIsWhitespace_ReturnsStructuredErrorResponse()
    {
        Console.WriteLine("\n[RunQuery_QueryIsWhitespace_ReturnsStructuredErrorResponse] Testing whitespace query");
        string invalidDax = "   \n\t   ";

        var result = await _daxTools.RunQuery(invalidDax, 0);

        // Verify structured error response
        Assert.NotNull(result);
        var resultType = result.GetType();
        var successProperty = resultType.GetProperty("Success");
        Assert.NotNull(successProperty);
        Assert.False((bool)successProperty.GetValue(result)!);

        var errorCategoryProperty = resultType.GetProperty("ErrorCategory");
        Assert.NotNull(errorCategoryProperty);
        Assert.Equal("validation", errorCategoryProperty.GetValue(result));

        Console.WriteLine("[RunQuery_QueryIsWhitespace_ReturnsStructuredErrorResponse] Correctly returned structured error response.");
    }

    [Fact]
    public async Task RunQuery_InvalidDaxSemanticError_ReturnsStructuredErrorResponse()
    {
        Console.WriteLine("\n[RunQuery_InvalidDaxSemanticError_ReturnsStructuredErrorResponse] Testing semantically incorrect DAX query");
        // This query is syntactically fine for the pre-checks but will fail on the server
        // if 'NonExistentTable' or '[NonExistentColumn]' do not exist.
        string invalidDaxQuery = "EVALUATE { NonExistentTable[NonExistentColumn] }";

        // Execute and expect a structured error envelope (new RunQuery behavior returns envelopes for execution errors)
        var result = await _daxTools.RunQuery(invalidDaxQuery, 0);

        Assert.NotNull(result);
        var resultType = result.GetType();
        var successProperty = resultType.GetProperty("Success");
        Assert.NotNull(successProperty);
        Assert.False((bool)successProperty.GetValue(result)!);

        var errorCategoryProperty = resultType.GetProperty("ErrorCategory");
        Assert.NotNull(errorCategoryProperty);
        Assert.Equal("execution", errorCategoryProperty.GetValue(result));

        var queryInfoProperty = resultType.GetProperty("QueryInfo");
        Assert.NotNull(queryInfoProperty);
        var queryInfo = queryInfoProperty.GetValue(result);
        var originalQueryProperty = queryInfo!.GetType().GetProperty("OriginalQuery");
        Assert.Contains(invalidDaxQuery, originalQueryProperty!.GetValue(queryInfo)!.ToString());

        Console.WriteLine("[RunQuery_InvalidDaxSemanticError_ReturnsStructuredErrorResponse] Correctly returned structured error response for invalid DAX.");
    }
}

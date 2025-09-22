using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging; // Added for ILogger
using Microsoft.Extensions.Logging.Abstractions; // Added for NullLogger
using pbi_local_mcp;
using pbi_local_mcp.Configuration;
using pbi_local_mcp.Core; // Added for ITabularConnection
using Xunit;

namespace pbi_local_mcp.Tests
{
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
                // Updated default to standardized test port 62678 (requested)
                port = "62678";
                Console.WriteLine($"[Setup] PBI_PORT not found, using standardized default: {port}");
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

            try
            {
                var response = await _daxTools.ListMeasures(tableName); // Changed to instance call
                LogToolResponse(response);

                var result = ExtractDataFromResponse(response);
                Assert.IsAssignableFrom<IEnumerable<Dictionary<string, object?>>>(result);
                Console.WriteLine(
                    $"[ListMeasuresTool_DoesNotThrow] Found {((IEnumerable<Dictionary<string, object?>>)result).Count()} measures.");
            }
            catch (Exception ex) when (
                ex.Message.Contains("Connection Error", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("A connection cannot be made", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("actively refused", StringComparison.OrdinalIgnoreCase)
            )
            {
                Console.WriteLine("[ListMeasuresTool_DoesNotThrow][SKIP] Connection not available. Skipping success assertion.");
            }
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

            try
            {
                var response = await _daxTools.PreviewTableData(tableName, topN); // Changed to instance call
                LogToolResponse(response);

                var result = ExtractDataFromResponse(response);
                Assert.IsAssignableFrom<IEnumerable<Dictionary<string, object?>>>(result);
                var rows = (IEnumerable<Dictionary<string, object?>>)result;
                Console.WriteLine($"[PreviewDataTool_DoesNotThrow] Retrieved {rows.Count()} rows.");
            }
            catch (Exception ex) when (
                ex.Message.Contains("Connection Error", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("A connection cannot be made", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("actively refused", StringComparison.OrdinalIgnoreCase)
            )
            {
                Console.WriteLine("[PreviewDataTool_DoesNotThrow][SKIP] Connection not available. Skipping success assertion.");
            }
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

            try
            {
                var response = await _daxTools.GetTableDetails(tableName); // Changed to instance call
                LogToolResponse(response);

                var result = ExtractDataFromResponse(response);
                Assert.IsAssignableFrom<IEnumerable<Dictionary<string, object?>>>(result);
                var rows = (IEnumerable<Dictionary<string, object?>>)result;
                Console.WriteLine(
                    $"[GetTableDetailsTool_DoesNotThrow] Retrieved details with {rows.Count()} rows.");
            }
            catch (Exception ex) when (
                ex.Message.Contains("Connection Error", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("A connection cannot be made", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("actively refused", StringComparison.OrdinalIgnoreCase)
            )
            {
                Console.WriteLine("[GetTableDetailsTool_DoesNotThrow][SKIP] Connection not available. Skipping success assertion.");
            }
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

            try
            {
                var response = await _daxTools.GetMeasureDetails(measureName); // Changed to instance call
                LogToolResponse(response);

                var result = ExtractDataFromResponse(response);
                Assert.IsAssignableFrom<IEnumerable<Dictionary<string, object?>>>(result);
                var rows = (IEnumerable<Dictionary<string, object?>>)result;
                Console.WriteLine(
                    $"[GetMeasureDetailsTool_DoesNotThrow] Retrieved details with {rows.Count()} rows.");
            }
            catch (Exception ex) when (
                ex.Message.Contains("Connection Error", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("A connection cannot be made", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("actively refused", StringComparison.OrdinalIgnoreCase)
            )
            {
                Console.WriteLine("[GetMeasureDetailsTool_DoesNotThrow][SKIP] Connection not available. Skipping success assertion.");
            }
        }

        /// <summary>
        /// Tests that the ListTables tool functions without throwing exceptions
        /// </summary>
        [Fact]
        public async Task ListTablesTool_DoesNotThrow()
        {
            Console.WriteLine("\n[ListTablesTool_DoesNotThrow] Listing all tables");

            try
            {
                var response = await _daxTools.ListTables(); // Changed to instance call
                LogToolResponse(response);

                var result = ExtractDataFromResponse(response);
                Assert.IsAssignableFrom<IEnumerable<Dictionary<string, object?>>>(result);
                var rows = (IEnumerable<Dictionary<string, object?>>)result;
                Console.WriteLine($"[ListTablesTool_DoesNotThrow] Found {rows.Count()} tables.");
            }
            catch (Exception ex) when (
                ex.Message.Contains("Connection Error", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("A connection cannot be made", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("actively refused", StringComparison.OrdinalIgnoreCase)
            )
            {
                Console.WriteLine("[ListTablesTool_DoesNotThrow][SKIP] Connection not available. Skipping success assertion.");
            }
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

            try
            {
                var response = await _daxTools.GetTableColumns(tableName); // Changed to instance call
                LogToolResponse(response);

                var result = ExtractDataFromResponse(response);
                Assert.IsAssignableFrom<IEnumerable<Dictionary<string, object?>>>(result);
                var rows = (IEnumerable<Dictionary<string, object?>>)result;
                Console.WriteLine($"[GetTableColumnsTool_DoesNotThrow] Found {rows.Count()} columns.");
            }
            catch (Exception ex) when (
                ex.Message.Contains("Connection Error", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("A connection cannot be made", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("actively refused", StringComparison.OrdinalIgnoreCase)
            )
            {
                Console.WriteLine("[GetTableColumnsTool_DoesNotThrow][SKIP] Connection not available. Skipping success assertion.");
            }
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

            try
            {
                var response = await _daxTools.GetTableRelationships(tableName); // Changed to instance call
                LogToolResponse(response);

                var result = ExtractDataFromResponse(response);
                Assert.IsAssignableFrom<IEnumerable<Dictionary<string, object?>>>(result);
                var rows = (IEnumerable<Dictionary<string, object?>>)result;
                Console.WriteLine(
                    $"[GetTableRelationshipsTool_DoesNotThrow] Found {rows.Count()} relationships.");
            }
            catch (Exception ex) when (
                ex.Message.Contains("Connection Error", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("A connection cannot be made", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("actively refused", StringComparison.OrdinalIgnoreCase)
            )
            {
                Console.WriteLine("[GetTableRelationshipsTool_DoesNotThrow][SKIP] Connection not available. Skipping success assertion.");
            }
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
                port = "55098"; // Default test port
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

            try
            {
                var result = await _daxTools.RunQuery(expression, topN); // Changed to instance call
                Assert.NotNull(result);
                Console.WriteLine("[RunQuery_NoDefinitions_DoesNotThrow] Successfully executed query without definitions");
            }
            catch (Exception ex) when (
                ex.Message.Contains("Connection Error", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("A connection cannot be made", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("actively refused", StringComparison.OrdinalIgnoreCase)
            )
            {
                Console.WriteLine("[RunQuery_NoDefinitions_DoesNotThrow][SKIP] Connection not available. Skipping success assertion.");
            }
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
            try
            {
                var result = await _daxTools.RunQuery(expression, topN); // Changed to instance call
                Assert.NotNull(result);
                Console.WriteLine("[RunQuery_WithVarDefinition_DoesNotThrow] Successfully executed query with VAR definition");
            }
            catch (Exception ex) when (
                ex.Message.Contains("Connection Error", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("A connection cannot be made", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("actively refused", StringComparison.OrdinalIgnoreCase)
            )
            {
                Console.WriteLine("[RunQuery_WithVarDefinition_DoesNotThrow][SKIP] Connection not available. Skipping success assertion.");
            }
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
            try
            {
                var result = await _daxTools.RunQuery(expression, topN); // Changed to instance call
                Assert.NotNull(result);
                Console.WriteLine("[RunQuery_WithMeasureDefinition_DoesNotThrow] Successfully executed query with MEASURE definition");
            }
            catch (Exception ex) when (
                ex.Message.Contains("Connection Error", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("A connection cannot be made", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("actively refused", StringComparison.OrdinalIgnoreCase)
            )
            {
                Console.WriteLine("[RunQuery_WithMeasureDefinition_DoesNotThrow][SKIP] Connection not available. Skipping success assertion.");
            }
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
            try
            {
                var result = await _daxTools.RunQuery(expression, topN); // Changed to instance call
                Assert.NotNull(result);
                Console.WriteLine("[RunQuery_WithMultipleDefinitions_DoesNotThrow] Successfully executed query with multiple definitions");
            }
            catch (Exception ex) when (
                ex.Message.Contains("Connection Error", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("A connection cannot be made", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("actively refused", StringComparison.OrdinalIgnoreCase)
            )
            {
                Console.WriteLine("[RunQuery_WithMultipleDefinitions_DoesNotThrow][SKIP] Connection not available. Skipping success assertion.");
            }
        }

        /// <summary>
        /// Tests that RunQuery throws an ArgumentException when a DEFINE query is missing an EVALUATE statement.
        /// </summary>
        [Fact]
        public async Task RunQuery_DefineWithoutEvaluate_ThrowsArgumentException()
        {
            Console.WriteLine("\n[RunQuery_DefineWithoutEvaluate_ThrowsArgumentException] Testing DEFINE without EVALUATE");
            string invalidDax = "DEFINE MEASURE Sales[Total] = SUM(Sales[Amount])"; 

            var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
                await _daxTools.RunQuery(invalidDax, 0)); // Changed to instance call
            Assert.Contains("DAX query must contain at least one EVALUATE statement.", exception.Message);
            Console.WriteLine("[RunQuery_DefineWithoutEvaluate_ThrowsArgumentException] Correctly threw exception.");
        }

        /// <summary>
        /// Tests that RunQuery throws an ArgumentException for unbalanced parentheses.
        /// </summary>
        [Fact]
        public async Task RunQuery_UnbalancedParentheses_ThrowsArgumentException()
        {
            Console.WriteLine("\n[RunQuery_UnbalancedParentheses_ThrowsArgumentException] Testing unbalanced parentheses");
            string invalidDax = "DEFINE VAR X = (1 + 2 EVALUATE {X}"; 

            var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
                await _daxTools.RunQuery(invalidDax, 0)); // Changed to instance call
            Assert.Contains("unbalanced parentheses", exception.Message);
            Console.WriteLine("[RunQuery_UnbalancedParentheses_ThrowsArgumentException] Correctly threw exception.");
        }

        /// <summary>
        /// Tests that RunQuery throws an ArgumentException for unbalanced brackets.
        /// </summary>
        [Fact]
        public async Task RunQuery_UnbalancedBrackets_ThrowsArgumentException()
        {
            Console.WriteLine("\n[RunQuery_UnbalancedBrackets_ThrowsArgumentException] Testing unbalanced brackets");
            string invalidDax = "DEFINE MEASURE Sales[Total = SUM(Sales[Amount]) EVALUATE {1}"; 

            var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
                await _daxTools.RunQuery(invalidDax, 0)); // Changed to instance call
            Assert.Contains("unbalanced brackets", exception.Message);
            Console.WriteLine("[RunQuery_UnbalancedBrackets_ThrowsArgumentException] Correctly threw exception.");
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
            try
            {
                var result = await _daxTools.RunQuery(expression, topN); // Changed to instance call
                Assert.NotNull(result);
                Console.WriteLine("[RunQuery_DefinitionOrderingTest_DoesNotThrow] Successfully executed query with mixed definition types");
            }
            catch (Exception ex) when (
                ex.Message.Contains("Connection Error", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("A connection cannot be made", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("actively refused", StringComparison.OrdinalIgnoreCase)
            )
            {
                Console.WriteLine("[RunQuery_DefinitionOrderingTest_DoesNotThrow][SKIP] Connection not available. Skipping success assertion.");
            }
        }

        [Fact]
        public async Task RunQuery_BasicExpression_DoesNotThrow()
        {
            var args = _toolConfig["runQueryNoDefinitions"];
            string expr = args.GetProperty("expression").GetString()!;
            int topN = args.TryGetProperty("topN", out var n) ? n.GetInt32() : 0;
            Console.WriteLine($"\n[RunQuery_BasicExpression_DoesNotThrow] Running DAX expression: {expr}");

            try
            {
                var response = await _daxTools.RunQuery(expr, topN); // Changed to instance call
                Tests.LogToolResponse(response);

                var result = Tests.ExtractDataFromResponse(response);
                Assert.IsAssignableFrom<IEnumerable<Dictionary<string, object?>>>(result);
                var rows = (IEnumerable<Dictionary<string, object?>>)result;
                Assert.Single(rows);
                Assert.Equal(2L, Convert.ToInt64(rows.First()["[Value]"]));
                Console.WriteLine($"[RunQuery_BasicExpression_DoesNotThrow] Retrieved {rows.Count()} rows, with value {rows.First()["[Value]"]}.");
            }
            catch (Exception ex) when (
                ex.Message.Contains("Connection Error", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("A connection cannot be made", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("actively refused", StringComparison.OrdinalIgnoreCase)
            )
            {
                Console.WriteLine("[RunQuery_BasicExpression_DoesNotThrow][SKIP] Connection not available. Skipping success assertion.");
            }
        }

        [Fact]
        public async Task RunQuery_MultipleDefineBlocks_ThrowsArgumentException()
        {
            Console.WriteLine("\n[RunQuery_MultipleDefineBlocks_ThrowsArgumentException] Testing multiple DEFINE blocks");
            string invalidDax = @"
                DEFINE MEASURE Sales[Total] = SUM(Sales[Amount])
                DEFINE VAR X = 1
                EVALUATE {X}";

            var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
                await _daxTools.RunQuery(invalidDax, 0)); // Changed to instance call
            Assert.Contains("Only one DEFINE block is allowed in a DAX query.", exception.Message);
            Console.WriteLine("[RunQuery_MultipleDefineBlocks_ThrowsArgumentException] Correctly threw exception.");
        }

        [Fact]
        public async Task RunQuery_DefineAfterEvaluate_ThrowsArgumentException()
        {
            Console.WriteLine("\n[RunQuery_DefineAfterEvaluate_ThrowsArgumentException] Testing DEFINE after EVALUATE");
            string invalidDax = @"
                EVALUATE {1}
                DEFINE MEASURE Sales[Total] = SUM(Sales[Amount])";
            
            var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
                await _daxTools.RunQuery(invalidDax, 0)); // Changed to instance call
            Assert.Contains("DEFINE statement must come before any EVALUATE statement.", exception.Message);
            Console.WriteLine("[RunQuery_DefineAfterEvaluate_ThrowsArgumentException] Correctly threw exception.");
        }

        [Fact]
        public async Task RunQuery_EmptyDefineBlock_ThrowsArgumentException()
        {
            Console.WriteLine("\n[RunQuery_EmptyDefineBlock_ThrowsArgumentException] Testing empty DEFINE block");
            string invalidDax = @"
                DEFINE
                EVALUATE {1}";

            var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
                await _daxTools.RunQuery(invalidDax, 0)); // Changed to instance call
            Assert.Contains("DEFINE block must contain at least one definition (MEASURE, VAR, TABLE, or COLUMN).", exception.Message);
            Console.WriteLine("[RunQuery_EmptyDefineBlock_ThrowsArgumentException] Correctly threw exception.");
        }
        
        [Fact]
        public async Task RunQuery_DefineBlockWithNoValidDefinition_ThrowsArgumentException()
        {
            Console.WriteLine("\n[RunQuery_DefineBlockWithNoValidDefinition_ThrowsArgumentException] Testing DEFINE block with no valid definition keyword");
            string invalidDax = @"
                DEFINE
                  MyVar = 10  // Missing VAR keyword
                EVALUATE {1}";

            var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
                await _daxTools.RunQuery(invalidDax, 0)); // Changed to instance call
            Assert.Contains("DEFINE block must contain at least one valid definition (MEASURE, VAR, TABLE, or COLUMN).", exception.Message);
            Console.WriteLine("[RunQuery_DefineBlockWithNoValidDefinition_ThrowsArgumentException] Correctly threw exception.");
        }

        [Fact]
        public async Task RunQuery_UnbalancedSingleQuotes_ThrowsArgumentException()
        {
            Console.WriteLine("\n[RunQuery_UnbalancedSingleQuotes_ThrowsArgumentException] Testing unbalanced single quotes");
            string invalidDax = "EVALUATE 'Sales[Amount]"; 

            var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
                await _daxTools.RunQuery(invalidDax, 0)); // Changed to instance call
            Assert.Contains("DAX query has unbalanced single quotes", exception.Message);
            Console.WriteLine("[RunQuery_UnbalancedSingleQuotes_ThrowsArgumentException] Correctly threw exception.");
        }

        [Fact]
        public async Task RunQuery_UnbalancedDoubleQuotes_ThrowsArgumentException()
        {
            Console.WriteLine("\n[RunQuery_UnbalancedDoubleQuotes_ThrowsArgumentException] Testing unbalanced double quotes");
            string invalidDax = "EVALUATE ROW(\"Value\", \"Hello World)"; 

            var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
                await _daxTools.RunQuery(invalidDax, 0)); // Changed to instance call
            Assert.Contains("DAX query has unbalanced double quotes", exception.Message);
            Console.WriteLine("[RunQuery_UnbalancedDoubleQuotes_ThrowsArgumentException] Correctly threw exception.");
        }
        
        [Fact]
        public async Task RunQuery_QueryIsEmpty_ThrowsArgumentException()
        {
            Console.WriteLine("\n[RunQuery_QueryIsEmpty_ThrowsArgumentException] Testing empty query");
            string invalidDax = "";

            var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
                await _daxTools.RunQuery(invalidDax, 0)); // Changed to instance call
            Assert.Contains("DAX query cannot be null or empty", exception.Message);
            Console.WriteLine("[RunQuery_QueryIsEmpty_ThrowsArgumentException] Correctly threw exception.");
        }

        [Fact]
        public async Task RunQuery_QueryIsWhitespace_ThrowsArgumentException()
        {
            Console.WriteLine("\n[RunQuery_QueryIsWhitespace_ThrowsArgumentException] Testing whitespace query");
            string invalidDax = "   \n\t   ";

            var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
                await _daxTools.RunQuery(invalidDax, 0)); // Changed to instance call
            Assert.Contains("DAX query cannot be null or empty", exception.Message);
            Console.WriteLine("[RunQuery_QueryIsWhitespace_ThrowsArgumentException] Correctly threw exception.");
        }

        [Fact]
        public async Task RunQuery_InvalidDaxSemanticError_ThrowsDaxQueryExecutionException()
        {
            Console.WriteLine("\n[RunQuery_InvalidDaxSemanticError_ThrowsDaxQueryExecutionException] Testing semantically incorrect DAX query");
            string invalidDaxQuery = "EVALUATE { NonExistentTable[NonExistentColumn] }";
            QueryType expectedQueryType = QueryType.DAX;

            var exception = await Assert.ThrowsAsync<Exception>(async () =>
                await _daxTools.RunQuery(invalidDaxQuery, 0));

            Assert.NotNull(exception);

            // Allow either semantic error (preferred) or connection error (environmental)
            if (exception.Message.Contains("Connection Error", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("[RunQuery_InvalidDaxSemanticError][INFO] Connection unavailable; semantic server-side validation not reached.");
                Assert.Contains("DAX query execution failed", exception.Message);
                return;
            }

            Assert.Contains("DAX query execution failed", exception.Message);
            Assert.Contains("Cannot find table 'NonExistentTable'", exception.Message);
            Assert.Contains(invalidDaxQuery, exception.Message);

            Console.WriteLine($"[RunQuery_InvalidDaxSemanticError_ThrowsDaxQueryExecutionException] Correctly threw Exception with DAX semantic error: {exception.Message}");
        }
    }
}
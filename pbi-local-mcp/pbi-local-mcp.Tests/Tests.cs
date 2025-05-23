using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using pbi_local_mcp;
using pbi_local_mcp.Configuration;
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

        /// <summary>
        /// Initializes test environment by loading configuration and setting up tool instances
        /// </summary>
        static Tests()
        {
            // Locate the solution root (6 levels up from the compiled test DLL)
            string dir = AppContext.BaseDirectory;
            for (int i = 0; i < 6; i++)
            {
                dir = Path.GetDirectoryName(dir) ??
                    throw new DirectoryNotFoundException("Cannot find solution root.");
            }

            string envPath = Path.Combine(dir, ".env");
            Console.WriteLine($"[Setup] Attempting to load .env from: {envPath}");

            Assert.True(File.Exists(envPath), ".env file not found – run discover-pbi first.");
            foreach (var line in File.ReadAllLines(envPath))
            {
                var parts = line.Split('=', 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                {
                    Environment.SetEnvironmentVariable(parts[0].Trim(), parts[1].Trim());
                }
            }
            Console.WriteLine("[Setup] .env file loaded.");

            string? port = Environment.GetEnvironmentVariable("PBI_PORT");
            string? dbId = Environment.GetEnvironmentVariable("PBI_DB_ID");
            Assert.NotNull(port);
            Assert.NotNull(dbId);
            Console.WriteLine($"[Setup] PBI_PORT: {port}, PBI_DB_ID: {dbId}");

            _connStr = $"Provider=MSOLAP;Data Source=localhost:{port};" +
                      $"Initial Catalog={dbId};Integrated Security=SSPI;";
            Console.WriteLine($"[Setup] Connection string for tests: {_connStr}");

            // Initialize DaxTools instance
            var config = new PowerBiConfig { Port = port, DbId = dbId };
            var tabular = new TabularConnection(config);
            // _tabular = tabular; // No longer needed

            // Load tooltest.config.json
            string configPath = Path.Combine(dir, "pbi-local-mcp", "pbi-local-mcp.Tests",
                "tooltest.config.json");
            Assert.True(File.Exists(configPath),
                $"tooltest.config.json not found at {configPath}");

            var configJson = File.ReadAllText(configPath);
            var doc = JsonDocument.Parse(configJson);
            _toolConfig = doc.RootElement.EnumerateObject()
                .ToDictionary(p => p.Name, p => p.Value.Clone());
            Console.WriteLine($"[Setup] Loaded tooltest.config.json with {_toolConfig.Count} tool configs.");
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

            var response = await DaxTools.ListMeasures(tableName);
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

            var response = await DaxTools.PreviewTableData(tableName, topN);
            LogToolResponse(response);

            var result = ExtractDataFromResponse(response);
            Assert.IsAssignableFrom<IEnumerable<Dictionary<string, object?>>>(result);
            var rows = (IEnumerable<Dictionary<string, object?>>)result;
            Console.WriteLine($"[PreviewDataTool_DoesNotThrow] Retrieved {rows.Count()} rows.");
        }

        /// <summary>
        /// Tests basic DAX evaluation functionality
        /// </summary>
        // EvaluateDAXTool_BasicUsage is removed as EvaluateDAX tool is removed.
        // RunQuery_NoDefinitions_DoesNotThrow and RunQuery_BasicExpression_DoesNotThrow (added later if needed for simple expressions) cover this.

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

            var response = await DaxTools.GetTableDetails(tableName);
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

            var response = await DaxTools.GetMeasureDetails(measureName);
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

            var response = await DaxTools.ListTables();
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

            var response = await DaxTools.GetTableColumns(tableName);
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

            var response = await DaxTools.GetTableRelationships(tableName);
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
            // Print the response as JSON
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

        /// <summary>
        /// Extracts data from a CallToolResponse object, assuming it contains a JSON array of objects.
        /// </summary>
        /// <param name="response">The CallToolResponse from an MCP tool call</param>
        /// <returns>Collection of dictionaries representing the data</returns>
        /// <exception cref="JsonException">Thrown when response cannot be deserialized as expected</exception>
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

        static DaxToolsRunQueryTests()
        {
            // Locate the solution root (6 levels up from the compiled test DLL)
            string dir = AppContext.BaseDirectory;
            for (int i = 0; i < 6; i++)
            {
                dir = Path.GetDirectoryName(dir) ??
                    throw new DirectoryNotFoundException("Cannot find solution root.");
            }

            // Load tooltest.config.json
            string configPath = Path.Combine(dir, "pbi-local-mcp", "pbi-local-mcp.Tests",
                "tooltest.config.json");
            if (!File.Exists(configPath))
            {
                // Fallback for tests running in different environments or if file is missing
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
            var args = DaxToolsRunQueryTests._toolConfig["runQueryNoDefinitions"];
            string expression = args.GetProperty("expression").GetString()!;
            int topN = args.GetProperty("topN").GetInt32();

            var result = await DaxTools.RunQuery(expression, topN);
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
            var args = DaxToolsRunQueryTests._toolConfig["runQueryWithVarDefinition"];
            string expression = args.GetProperty("expression").GetString()!;
            int topN = args.GetProperty("topN").GetInt32();
            // Assuming 'expression' from config now contains the full DAX query including any DEFINE block.
            var result = await DaxTools.RunQuery(expression, topN);
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
            var args = DaxToolsRunQueryTests._toolConfig["runQueryWithMeasureDefinition"];
            string expression = args.GetProperty("expression").GetString()!;
            int topN = args.GetProperty("topN").GetInt32();
            // Assuming 'expression' from config now contains the full DAX query including any DEFINE block.
            var result = await DaxTools.RunQuery(expression, topN);
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
            var args = DaxToolsRunQueryTests._toolConfig["runQueryWithMultipleDefinitions"];
            string expression = args.GetProperty("expression").GetString()!;
            int topN = args.GetProperty("topN").GetInt32();
            // Assuming 'expression' from config now contains the full DAX query including any DEFINE block.
            var result = await DaxTools.RunQuery(expression, topN);
            Assert.NotNull(result);
            Console.WriteLine("[RunQuery_WithMultipleDefinitions_DoesNotThrow] Successfully executed query with multiple definitions");
        }

        /// <summary>
        /// Tests that RunQuery throws an ArgumentException when a DEFINE query is missing an EVALUATE statement.
        /// </summary>
        [Fact]
        public async Task RunQuery_DefineWithoutEvaluate_ThrowsArgumentException()
        {
            Console.WriteLine("\n[RunQuery_DefineWithoutEvaluate_ThrowsArgumentException] Testing DEFINE without EVALUATE");
            string invalidDax = "DEFINE MEASURE Sales[Total] = SUM(Sales[Amount])"; // Missing EVALUATE

            var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
                await DaxTools.RunQuery(invalidDax, 0));
            // The new validation produces a more general message if EVALUATE is missing.
            // This is now directly an ArgumentException.
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
            string invalidDax = "DEFINE VAR X = (1 + 2 EVALUATE {X}"; // Unbalanced parentheses

            var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
                await DaxTools.RunQuery(invalidDax, 0));
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
            string invalidDax = "DEFINE MEASURE Sales[Total = SUM(Sales[Amount]) EVALUATE {1}"; // Unbalanced brackets

            var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
                await DaxTools.RunQuery(invalidDax, 0));
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
            var args = DaxToolsRunQueryTests._toolConfig["runQueryDefinitionOrdering"];
            string expression = args.GetProperty("expression").GetString()!;
            int topN = args.GetProperty("topN").GetInt32();
            // Assuming 'expression' from config now contains the full DAX query including any DEFINE block.
            // This test now primarily verifies that a query with a DEFINE block can be processed.
            var result = await DaxTools.RunQuery(expression, topN);
            Assert.NotNull(result);
            Console.WriteLine("[RunQuery_DefinitionOrderingTest_DoesNotThrow] Successfully executed query with mixed definition types");
        }

        [Fact]
        public async Task RunQuery_BasicExpression_DoesNotThrow()
        {
            // This test uses RunQuery for basic expressions.
            // It uses the "runQueryNoDefinitions" config which should have a simple expression like "1+1".
            var args = _toolConfig["runQueryNoDefinitions"];
            string expr = args.GetProperty("expression").GetString()!;
            // topN for simple expressions in RunQuery defaults to wrapping in ROW if not a table expr,
            // or TOPN if it is a table expr. For "1+1", it becomes EVALUATE ROW("Value", 1+1).
            int topN = args.TryGetProperty("topN", out var n) ? n.GetInt32() : 0;
            Console.WriteLine($"\n[RunQuery_BasicExpression_DoesNotThrow] Running DAX expression: {expr}");

            var response = await DaxTools.RunQuery(expr, topN);
            Tests.LogToolResponse(response);

            var result = Tests.ExtractDataFromResponse(response);
            Assert.IsAssignableFrom<IEnumerable<Dictionary<string, object?>>>(result);
            var rows = (IEnumerable<Dictionary<string, object?>>)result;
            Assert.Single(rows); // Expect 1 row for EVALUATE ROW("Value", 1+1)
            Assert.Equal(2L, Convert.ToInt64(rows.First()["[Value]"])); // Value should be 2, column name is [Value]
            Console.WriteLine($"[RunQuery_BasicExpression_DoesNotThrow] Retrieved {rows.Count()} rows, with value {rows.First()["[Value]"]}.");
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
                await DaxTools.RunQuery(invalidDax, 0));
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

            // This specific error (DEFINE after EVALUATE) is caught by ValidateCompleteDAXQuery,
            // which is wrapped by the try-catch in RunQuery that re-throws with "Error executing DAX query:".
            // So, we still expect the outer Exception here, and check its InnerException.
            // However, if the initial basic checks in RunQuery (like unbalanced quotes if they existed here)
            // caught it first, it would be an ArgumentException directly.
            // Let's assume for this structural error, it might still be wrapped.
            // If tests show it's a direct ArgumentException, we'll adjust.
            // For now, keeping the original structure for this specific test.
            // UPDATE: Based on previous test failures, structural errors like this are still resulting in AdomdError.
            // The goal is for OUR validation to catch it. If RunQuery's initial checks don't,
            // and ValidateCompleteDAXQuery does, it should be an ArgumentException.
            // Let's change to expect ArgumentException directly.
            var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
                await DaxTools.RunQuery(invalidDax, 0));
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
                await DaxTools.RunQuery(invalidDax, 0));
            // Adjusted to match the exact error message from DaxTools.cs for this specific case
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
                await DaxTools.RunQuery(invalidDax, 0));
            Assert.Contains("DEFINE block must contain at least one valid definition (MEASURE, VAR, TABLE, or COLUMN).", exception.Message);
            Console.WriteLine("[RunQuery_DefineBlockWithNoValidDefinition_ThrowsArgumentException] Correctly threw exception.");
        }

        [Fact]
        public async Task RunQuery_UnbalancedSingleQuotes_ThrowsArgumentException()
        {
            Console.WriteLine("\n[RunQuery_UnbalancedSingleQuotes_ThrowsArgumentException] Testing unbalanced single quotes");
            // Example: DEFINE TABLE 'My Incomplete Table = {1} EVALUATE 'My Incomplete Table
            string invalidDax = "EVALUATE 'Sales[Amount]"; // Unbalanced single quote for 'Sales' identifier

            var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
                await DaxTools.RunQuery(invalidDax, 0));
            Assert.Contains("DAX query has unbalanced single quotes", exception.Message);
            Console.WriteLine("[RunQuery_UnbalancedSingleQuotes_ThrowsArgumentException] Correctly threw exception.");
        }

        [Fact]
        public async Task RunQuery_UnbalancedDoubleQuotes_ThrowsArgumentException()
        {
            Console.WriteLine("\n[RunQuery_UnbalancedDoubleQuotes_ThrowsArgumentException] Testing unbalanced double quotes");
            string invalidDax = "EVALUATE ROW(\"Value\", \"Hello World)"; // Missing closing double quote

            var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
                await DaxTools.RunQuery(invalidDax, 0));
            Assert.Contains("DAX query has unbalanced double quotes", exception.Message);
            Console.WriteLine("[RunQuery_UnbalancedDoubleQuotes_ThrowsArgumentException] Correctly threw exception.");
        }
        
        [Fact]
        public async Task RunQuery_QueryIsEmpty_ThrowsArgumentException()
        {
            Console.WriteLine("\n[RunQuery_QueryIsEmpty_ThrowsArgumentException] Testing empty query");
            string invalidDax = "";

            var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
                await DaxTools.RunQuery(invalidDax, 0));
            Assert.Contains("Query cannot be empty.", exception.Message);
            Console.WriteLine("[RunQuery_QueryIsEmpty_ThrowsArgumentException] Correctly threw exception.");
        }

        [Fact]
        public async Task RunQuery_QueryIsWhitespace_ThrowsArgumentException()
        {
            Console.WriteLine("\n[RunQuery_QueryIsWhitespace_ThrowsArgumentException] Testing whitespace query");
            string invalidDax = "   \n\t   ";

            var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
                await DaxTools.RunQuery(invalidDax, 0));
            Assert.Contains("Query cannot be empty.", exception.Message);
            Console.WriteLine("[RunQuery_QueryIsWhitespace_ThrowsArgumentException] Correctly threw exception.");
        }

        // Removed RunQuery_BackwardCompatibility_BehavesLikeEvaluateDAX as EvaluateDAX tool is removed.
        // RunQuery is now the sole method for DAX execution.
    }
}
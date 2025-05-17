using System.Data;
using System.Text.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using pbi_local_mcp.Configuration;

namespace pbi_local_mcp.Tests
{
    /// <summary>
    /// Integration‑style smoke tests – their only job is to prove that the tools connect to *whatever* model the
    /// .env points to and that they do not throw. They make no assumptions about table or measure names.
    /// </summary>
    [TestClass]
    public class Tests
    {
        private static string? _connStr;
        private static Dictionary<string, JsonElement> _toolConfig = new();
        private Tools _tools;

        [TestInitialize]
        public void Setup()
        {
            // Locate the solution root (6 levels up from the compiled test DLL)
            string dir = AppContext.BaseDirectory;
            for (int i = 0; i < 6; i++)
            {
                dir = Path.GetDirectoryName(dir) ?? throw new DirectoryNotFoundException("Cannot find solution root.");
            }
            string envPath = Path.Combine(dir, ".env");
            Console.WriteLine($"[Setup] Attempting to load .env from: {envPath}");

            Assert.IsTrue(File.Exists(envPath), ".env file not found – run discover-pbi first.");
            // Local .env loader
            foreach (var line in File.ReadAllLines(envPath))
            {
                var parts = line.Split('=', 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                    Environment.SetEnvironmentVariable(parts[0].Trim(), parts[1].Trim());
            }
            Console.WriteLine($"[Setup] .env file loaded.");

            string? port = Environment.GetEnvironmentVariable("PBI_PORT");
            string? dbId = Environment.GetEnvironmentVariable("PBI_DB_ID");
            Assert.IsNotNull(port, "PBI_PORT missing after loading .env");
            Assert.IsNotNull(dbId, "PBI_DB_ID missing after loading .env");
            Console.WriteLine($"[Setup] PBI_PORT: {port}, PBI_DB_ID: {dbId}");

            _connStr = $"Provider=MSOLAP;Data Source=localhost:{port};Initial Catalog={dbId};Integrated Security=SSPI;";
            Console.WriteLine($"[Setup] Connection string for tests: {_connStr}");

            // Initialize Tools instance
            var config = new PowerBiConfig { Port = port, DbId = dbId };
            var tabular = new TabularConnection(config);
            _tools = new Tools(tabular);

            // Load tooltest.config.json
            string configPath = Path.Combine(dir, "pbi-local-mcp", "pbi-local-mcp.Tests", "tooltest.config.json");
            Assert.IsTrue(File.Exists(configPath), $"tooltest.config.json not found at {configPath}");
            var configJson = File.ReadAllText(configPath);
            var doc = JsonDocument.Parse(configJson);
            _toolConfig = doc.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone());
            Console.WriteLine($"[Setup] Loaded tooltest.config.json with {_toolConfig.Count} tool configs.");
        }

        [TestMethod]
        public void TestInfrastructureUp()
        {
            Console.WriteLine("[TestInfrastructureUp] Verifying basic assertion.");
            Assert.IsTrue(true);
            Console.WriteLine("[TestInfrastructureUp] Basic assertion passed.");
        }

        [TestMethod]
        public async Task ListMeasuresTool_DoesNotThrow()
        {
            var args = _toolConfig["listMeasures"];
            string tableName = args.TryGetProperty("tableName", out var t) ? t.GetString() ?? "" : "";
            var response = await _tools.ListMeasures(tableName);
            var result = ExtractDataFromResponse(response);
            Assert.IsInstanceOfType(result, typeof(IEnumerable<Dictionary<string, object?>>));
            Console.WriteLine($"[ListMeasuresTool_DoesNotThrow] Result: {((IEnumerable<Dictionary<string, object?>>)result).Count()} rows.");
        }

        [TestMethod]
        public async Task PreviewDataTool_DoesNotThrow()
        {
            var args = _toolConfig["previewTableData"];
            string tableName = args.GetProperty("tableName").GetString()!;
            int topN = args.TryGetProperty("topN", out var n) ? n.GetInt32() : 10;
            var response = await _tools.PreviewTableData(tableName, topN);
            var result = ExtractDataFromResponse(response);
            Assert.IsInstanceOfType(result, typeof(IEnumerable<Dictionary<string, object?>>));
            var rows = (IEnumerable<Dictionary<string, object?>>)result;
            Console.WriteLine($"[PreviewDataTool_DoesNotThrow] PreviewTableData(): {rows.Count()} rows for table '{tableName}'.");
        }

        [TestMethod]
        public async Task EvaluateDAXTool_BasicUsage()
        {
            var args = _toolConfig["evaluateDAX"];
            string expr = args.GetProperty("expression").GetString()!;
            int topN = args.TryGetProperty("topN", out var n) ? n.GetInt32() : 10;
            var response = await _tools.EvaluateDAX(expr, topN);
            var result = ExtractDataFromResponse(response);
            Assert.IsInstanceOfType(result, typeof(IEnumerable<Dictionary<string, object?>>));
            var rows = (IEnumerable<Dictionary<string, object?>>)result;
            Console.WriteLine($"[EvaluateDAXTool_BasicUsage] Result: {rows.Count()} rows.");
        }

        [TestMethod]
        public async Task GetTableDetailsTool_DoesNotThrow()
        {
            var args = _toolConfig["getTableDetails"];
            string tableName = args.GetProperty("tableName").GetString()!;
            var response = await _tools.GetTableDetails(tableName);
            var result = ExtractDataFromResponse(response);
            Assert.IsInstanceOfType(result, typeof(IEnumerable<Dictionary<string, object?>>));
            var rows = (IEnumerable<Dictionary<string, object?>>)result;
            Console.WriteLine($"[GetTableDetailsTool_DoesNotThrow] Result: {rows.Count()} rows for table '{tableName}'.");
        }

        [TestMethod]
        public async Task GetMeasureDetailsTool_DoesNotThrow()
        {
            var args = _toolConfig["getMeasureDetails"];
            string measureName = args.GetProperty("measureName").GetString()!;
            var response = await _tools.GetMeasureDetails(measureName);
            var result = ExtractDataFromResponse(response);
            Assert.IsInstanceOfType(result, typeof(IEnumerable<Dictionary<string, object?>>));
            var rows = (IEnumerable<Dictionary<string, object?>>)result;
            Console.WriteLine($"[GetMeasureDetailsTool_DoesNotThrow] Result: {rows.Count()} rows for measure '{measureName}'.");
        }

        [TestMethod]
        public async Task ListTablesTool_DoesNotThrow()
        {
            var args = _toolConfig["listTables"];
            var response = await _tools.ListTables();
            var result = ExtractDataFromResponse(response);
            Assert.IsInstanceOfType(result, typeof(IEnumerable<Dictionary<string, object?>>));
            var rows = (IEnumerable<Dictionary<string, object?>>)result;
            Console.WriteLine($"[ListTablesTool_DoesNotThrow] Result: {rows.Count()} tables.");
        }

        [TestMethod]
        public async Task GetTableColumnsTool_DoesNotThrow()
        {
            var args = _toolConfig["getTableColumns"];
            string tableName = args.GetProperty("tableName").GetString()!;
            var response = await _tools.GetTableColumns(tableName);
            var result = ExtractDataFromResponse(response);
            Assert.IsInstanceOfType(result, typeof(IEnumerable<Dictionary<string, object?>>));
            var rows = (IEnumerable<Dictionary<string, object?>>)result;
            Console.WriteLine($"[GetTableColumnsTool_DoesNotThrow] Result: {rows.Count()} columns for table '{tableName}'.");
        }

        [TestMethod]
        public async Task GetTableRelationshipsTool_DoesNotThrow()
        {
            var args = _toolConfig["getTableRelationships"];
            string tableName = args.GetProperty("tableName").GetString()!;
            var response = await _tools.GetTableRelationships(tableName);
            var result = ExtractDataFromResponse(response);
            Assert.IsInstanceOfType(result, typeof(IEnumerable<Dictionary<string, object?>>));
            var rows = (IEnumerable<Dictionary<string, object?>>)result;
            Console.WriteLine($"[GetTableRelationshipsTool_DoesNotThrow] Result: {rows.Count()} relationships for table '{tableName}'.");
        }

        private void AssertToolResultIsCollectionOrError(object result, string toolNameForMessage)
        {
            if (result is IEnumerable<object> listResult)
            {
                Assert.IsNotNull(listResult, $"{toolNameForMessage} returned null list.");
                Console.WriteLine($"[{toolNameForMessage}] Result is IEnumerable<object> with {listResult.Count()} items.");
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
            Assert.IsNotNull(errorProp, $"{toolNameForMessage} did not return an 'error' property when expected.");
            var errorMessage = errorProp.GetValue(result) as string;
            Assert.IsFalse(string.IsNullOrWhiteSpace(errorMessage), $"{toolNameForMessage} returned an empty error message.");
            Console.WriteLine($"[{toolNameForMessage}] Correctly returned error: {errorMessage}");
        }

        /// <summary>
        /// Extracts data from a CallToolResponse object, assuming it contains a JSON array of objects.
        /// </summary>
        /// <param name="response">The CallToolResponse from an MCP tool call</param>
        /// <returns>Collection of dictionaries representing the data, or null if error</returns>
        private IEnumerable<Dictionary<string, object?>> ExtractDataFromResponse(Tools.CallToolResponse response)
        {
            Assert.IsNotNull(response, "CallToolResponse is null");
            Assert.IsNotNull(response.Content, "CallToolResponse Content is null");
            Assert.IsTrue(response.Content.Count > 0, "CallToolResponse Content is empty");

            var content = response.Content[0];
            Assert.AreEqual("application/json", content.Type, "Expected JSON content");
            Assert.IsNotNull(content.Text, "JSON content is null");

            var jsonText = content.Text;
            try
            {
                var result = System.Text.Json.JsonSerializer.Deserialize<IEnumerable<Dictionary<string, object?>>>(jsonText,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                Assert.IsNotNull(result, "Failed to deserialize JSON content");
                return result;
            }
            catch (System.Text.Json.JsonException ex)
            {
                Console.WriteLine("[ExtractDataFromResponse] Failed to deserialize as array. Raw JSON:");
                Console.WriteLine(jsonText);
                throw;
            }
        }
    }
}
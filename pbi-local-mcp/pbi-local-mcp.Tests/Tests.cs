using System.Text.Json;

using pbi_local_mcp.Configuration;

namespace pbi_local_mcp.Tests;

/// <summary>
/// Integration‑style smoke tests – their only job is to prove that the tools connect to *whatever* model the
/// .env points to and that they do not throw. They make no assumptions about table or measure names.
/// </summary>
[TestClass]
public class Tests
{
    private static string? _connStr;
    private static Dictionary<string, JsonElement> _toolConfig = new();
    private DaxTools _tools = null!;

    /// <summary>
    /// Initializes test environment by loading configuration and setting up tool instances
    /// </summary>
    [TestInitialize]
    public void Setup()
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

        Assert.IsTrue(File.Exists(envPath), ".env file not found – run discover-pbi first.");
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
        Assert.IsNotNull(port, "PBI_PORT missing after loading .env");
        Assert.IsNotNull(dbId, "PBI_DB_ID missing after loading .env");
        Console.WriteLine($"[Setup] PBI_PORT: {port}, PBI_DB_ID: {dbId}");

        _connStr = $"Provider=MSOLAP;Data Source=localhost:{port};" +
                  $"Initial Catalog={dbId};Integrated Security=SSPI;";
        Console.WriteLine($"[Setup] Connection string for tests: {_connStr}");

        // Initialize DaxTools instance
        var config = new PowerBiConfig { Port = port, DbId = dbId };
        var tabular = new TabularConnection(config);
        _tools = new DaxTools(tabular);

        // Load tooltest.config.json
        string configPath = Path.Combine(dir, "pbi-local-mcp", "pbi-local-mcp.Tests",
            "tooltest.config.json");
        Assert.IsTrue(File.Exists(configPath),
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
    [TestMethod]
    public void TestInfrastructureUp()
    {
        Console.WriteLine("[TestInfrastructureUp] Verifying basic assertion.");
        Assert.IsTrue(true);
        Console.WriteLine("[TestInfrastructureUp] Basic assertion passed.");
    }

    /// <summary>
    /// Tests that the ListMeasures tool functions without throwing exceptions
    /// </summary>
    [TestMethod]
    public async Task ListMeasuresTool_DoesNotThrow()
    {
        var args = _toolConfig["listMeasures"];
        string tableName = args.TryGetProperty("tableName", out var t) ?
            t.GetString() ?? "" : "";
        Console.WriteLine($"\n[ListMeasuresTool_DoesNotThrow] Listing measures for table: {tableName}");

        var response = await _tools.ListMeasures(tableName);
        LogToolResponse(response);

        var result = ExtractDataFromResponse(response);
        Assert.IsInstanceOfType(result, typeof(IEnumerable<Dictionary<string, object?>>));
        Console.WriteLine(
            $"[ListMeasuresTool_DoesNotThrow] Found {((IEnumerable<Dictionary<string, object?>>)result).Count()} measures.");
    }

    /// <summary>
    /// Tests that the PreviewData tool functions without throwing exceptions
    /// </summary>
    [TestMethod]
    public async Task PreviewDataTool_DoesNotThrow()
    {
        var args = _toolConfig["previewTableData"];
        string tableName = args.GetProperty("tableName").GetString()!;
        int topN = args.TryGetProperty("topN", out var n) ? n.GetInt32() : 10;
        Console.WriteLine(
            $"\n[PreviewDataTool_DoesNotThrow] Previewing {topN} rows from table: {tableName}");

        var response = await _tools.PreviewTableData(tableName, topN);
        LogToolResponse(response);

        var result = ExtractDataFromResponse(response);
        Assert.IsInstanceOfType(result, typeof(IEnumerable<Dictionary<string, object?>>));
        var rows = (IEnumerable<Dictionary<string, object?>>)result;
        Console.WriteLine($"[PreviewDataTool_DoesNotThrow] Retrieved {rows.Count()} rows.");
    }

    /// <summary>
    /// Tests basic DAX evaluation functionality
    /// </summary>
    [TestMethod]
    public async Task EvaluateDAXTool_BasicUsage()
    {
        var args = _toolConfig["evaluateDAX"];
        string expr = args.GetProperty("expression").GetString()!;
        int topN = args.TryGetProperty("topN", out var n) ? n.GetInt32() : 10;
        Console.WriteLine($"\n[EvaluateDAXTool_BasicUsage] Evaluating DAX expression: {expr}");

        var response = await _tools.EvaluateDAX(expr, topN);
        LogToolResponse(response);

        var result = ExtractDataFromResponse(response);
        Assert.IsInstanceOfType(result, typeof(IEnumerable<Dictionary<string, object?>>));
        var rows = (IEnumerable<Dictionary<string, object?>>)result;
        Console.WriteLine($"[EvaluateDAXTool_BasicUsage] Retrieved {rows.Count()} rows.");
    }

    /// <summary>
    /// Tests that the GetTableDetails tool functions without throwing exceptions
    /// </summary>
    [TestMethod]
    public async Task GetTableDetailsTool_DoesNotThrow()
    {
        var args = _toolConfig["getTableDetails"];
        string tableName = args.GetProperty("tableName").GetString()!;
        Console.WriteLine(
            $"\n[GetTableDetailsTool_DoesNotThrow] Getting details for table: {tableName}");

        var response = await _tools.GetTableDetails(tableName);
        LogToolResponse(response);

        var result = ExtractDataFromResponse(response);
        Assert.IsInstanceOfType(result, typeof(IEnumerable<Dictionary<string, object?>>));
        var rows = (IEnumerable<Dictionary<string, object?>>)result;
        Console.WriteLine(
            $"[GetTableDetailsTool_DoesNotThrow] Retrieved details with {rows.Count()} rows.");
    }

    /// <summary>
    /// Tests that the GetMeasureDetails tool functions without throwing exceptions
    /// </summary>
    [TestMethod]
    public async Task GetMeasureDetailsTool_DoesNotThrow()
    {
        var args = _toolConfig["getMeasureDetails"];
        string measureName = args.GetProperty("measureName").GetString()!;
        Console.WriteLine(
            $"\n[GetMeasureDetailsTool_DoesNotThrow] Getting details for measure: {measureName}");

        var response = await _tools.GetMeasureDetails(measureName);
        LogToolResponse(response);

        var result = ExtractDataFromResponse(response);
        Assert.IsInstanceOfType(result, typeof(IEnumerable<Dictionary<string, object?>>));
        var rows = (IEnumerable<Dictionary<string, object?>>)result;
        Console.WriteLine(
            $"[GetMeasureDetailsTool_DoesNotThrow] Retrieved details with {rows.Count()} rows.");
    }

    /// <summary>
    /// Tests that the ListTables tool functions without throwing exceptions
    /// </summary>
    [TestMethod]
    public async Task ListTablesTool_DoesNotThrow()
    {
        Console.WriteLine("\n[ListTablesTool_DoesNotThrow] Listing all tables");

        var response = await _tools.ListTables();
        LogToolResponse(response);

        var result = ExtractDataFromResponse(response);
        Assert.IsInstanceOfType(result, typeof(IEnumerable<Dictionary<string, object?>>));
        var rows = (IEnumerable<Dictionary<string, object?>>)result;
        Console.WriteLine($"[ListTablesTool_DoesNotThrow] Found {rows.Count()} tables.");
    }

    /// <summary>
    /// Tests that the GetTableColumns tool functions without throwing exceptions
    /// </summary>
    [TestMethod]
    public async Task GetTableColumnsTool_DoesNotThrow()
    {
        var args = _toolConfig["getTableColumns"];
        string tableName = args.GetProperty("tableName").GetString()!;
        Console.WriteLine(
            $"\n[GetTableColumnsTool_DoesNotThrow] Getting columns for table: {tableName}");

        var response = await _tools.GetTableColumns(tableName);
        LogToolResponse(response);

        var result = ExtractDataFromResponse(response);
        Assert.IsInstanceOfType(result, typeof(IEnumerable<Dictionary<string, object?>>));
        var rows = (IEnumerable<Dictionary<string, object?>>)result;
        Console.WriteLine($"[GetTableColumnsTool_DoesNotThrow] Found {rows.Count()} columns.");
    }

    /// <summary>
    /// Tests that the GetTableRelationships tool functions without throwing exceptions
    /// </summary>
    [TestMethod]
    public async Task GetTableRelationshipsTool_DoesNotThrow()
    {
        var args = _toolConfig["getTableRelationships"];
        string tableName = args.GetProperty("tableName").GetString()!;
        Console.WriteLine(
            $"\n[GetTableRelationshipsTool_DoesNotThrow] Getting relationships for table: {tableName}");

        var response = await _tools.GetTableRelationships(tableName);
        LogToolResponse(response);

        var result = ExtractDataFromResponse(response);
        Assert.IsInstanceOfType(result, typeof(IEnumerable<Dictionary<string, object?>>));
        var rows = (IEnumerable<Dictionary<string, object?>>)result;
        Console.WriteLine(
            $"[GetTableRelationshipsTool_DoesNotThrow] Found {rows.Count()} relationships.");
    }

    private void LogToolResponse(DaxTools.CallToolResponse response)
    {
        Console.WriteLine("\nResponse Content:");
        foreach (var content in response.Content)
        {
            Console.WriteLine($"Type: {content.Type}");
            Console.WriteLine("Data:");
            if (content.Type == "application/json")
            {
                try
                {
                    var jsonObject = JsonSerializer.Deserialize<object>(content.Text);
                    var formattedJson = JsonSerializer.Serialize(jsonObject,
                        new JsonSerializerOptions { WriteIndented = true });
                    Console.WriteLine(formattedJson);
                }
                catch (JsonException)
                {
                    Console.WriteLine("[Failed to parse as JSON]");
                    Console.WriteLine(content.Text);
                }
            }
            else
            {
                Console.WriteLine(content.Text);
            }
            Console.WriteLine();
        }
    }

    private void AssertToolResultIsCollectionOrError(object result, string toolNameForMessage)
    {
        if (result is IEnumerable<object> listResult)
        {
            Assert.IsNotNull(listResult, $"{toolNameForMessage} returned null list.");
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
        Assert.IsNotNull(errorProp,
            $"{toolNameForMessage} did not return an 'error' property when expected.");
        var errorMessage = errorProp.GetValue(result) as string;
        Assert.IsFalse(string.IsNullOrWhiteSpace(errorMessage),
            $"{toolNameForMessage} returned an empty error message.");
        Console.WriteLine($"[{toolNameForMessage}] Correctly returned error: {errorMessage}");
    }

    /// <summary>
    /// Extracts data from a CallToolResponse object, assuming it contains a JSON array of objects.
    /// </summary>
    /// <param name="response">The CallToolResponse from an MCP tool call</param>
    /// <returns>Collection of dictionaries representing the data</returns>
    /// <exception cref="JsonException">Thrown when response cannot be deserialized as expected</exception>
    private IEnumerable<Dictionary<string, object?>> ExtractDataFromResponse(
        DaxTools.CallToolResponse response)
    {
        Assert.IsNotNull(response, "CallToolResponse is null");
        Assert.IsNotNull(response.Content, "CallToolResponse Content is null");
        Assert.IsTrue(response.Content.Count > 0, "CallToolResponse Content is empty");

        var content = response.Content.FirstOrDefault(c => c.Type == "application/json");
        Assert.IsNotNull(content, "No JSON content found in response");
        Assert.IsNotNull(content.Text, "JSON content is null");

        var jsonText = content.Text;
        try
        {
            var result = JsonSerializer.Deserialize<IEnumerable<Dictionary<string, object?>>>(
                jsonText,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            Assert.IsNotNull(result, "Failed to deserialize JSON content");
            return result;
        }
        catch (JsonException)
        {
            Console.WriteLine("[ExtractDataFromResponse] Failed to deserialize as array. Raw JSON:");
            Console.WriteLine(jsonText);
            throw;
        }
    }
}
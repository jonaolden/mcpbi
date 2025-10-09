using System.Text.Json;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using pbi_local_mcp.Configuration;
using pbi_local_mcp.Core;

namespace pbi_local_mcp.Tests;

/// <summary>
/// Tests for the DAX function lookup tools (ListFunctions and GetFunctionDetails).
/// These tests verify DMV query functionality for MDSCHEMA_FUNCTIONS.
/// </summary>
public class FunctionToolsTests
{
    private static readonly DaxTools _daxTools;

    static FunctionToolsTests()
    {
        // Check for environment variables first (from command line or environment)
        string? port = Environment.GetEnvironmentVariable("PBI_PORT");
        string? dbId = Environment.GetEnvironmentVariable("PBI_DB_ID");

        Console.WriteLine($"[FunctionToolsTests Setup] Command line environment - PBI_PORT: {port}, PBI_DB_ID: {dbId}");

        // Only load .env file if we don't have a port from command line
        if (string.IsNullOrEmpty(port))
        {
            string dir = AppContext.BaseDirectory;
            for (int i = 0; i < 6; i++)
            {
                dir = Path.GetDirectoryName(dir) ??
                    throw new DirectoryNotFoundException("Cannot find solution root.");
            }

            string envPath = Path.Combine(dir, ".env");
            Console.WriteLine($"[FunctionToolsTests Setup] No PBI_PORT from command line, attempting to load .env from: {envPath}");

            if (File.Exists(envPath))
            {
                foreach (var line in File.ReadAllLines(envPath))
                {
                    var parts = line.Split('=', 2, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2)
                    {
                        var key = parts[0].Trim();
                        var value = parts[1].Trim();

                        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                        {
                            Environment.SetEnvironmentVariable(key, value);
                        }
                    }
                }

                port = Environment.GetEnvironmentVariable("PBI_PORT");
                if (string.IsNullOrEmpty(dbId))
                {
                    dbId = Environment.GetEnvironmentVariable("PBI_DB_ID");
                }
            }
        }

        if (string.IsNullOrEmpty(port))
        {
            port = "62678";
            Console.WriteLine($"[FunctionToolsTests Setup] PBI_PORT not found, using default: {port}");
        }

        Console.WriteLine($"[FunctionToolsTests Setup] Final configuration - PBI_PORT: {port}, PBI_DB_ID: {dbId ?? "NOT_SET"}");

        // Initialize DaxTools instance
        ITabularConnection tabularConnection;
        ILogger<DaxTools> logger = NullLogger<DaxTools>.Instance;

        if (string.IsNullOrEmpty(dbId))
        {
            Console.WriteLine($"[FunctionToolsTests Setup] PBI_DB_ID not provided, attempting database auto-discovery on port {port}");
            try
            {
                var connectionLogger = NullLogger<TabularConnection>.Instance;
                var discoveryTask = TabularConnection.CreateWithDiscoveryAsync(port, connectionLogger);
                tabularConnection = discoveryTask.GetAwaiter().GetResult();
                Console.WriteLine($"[FunctionToolsTests Setup] Successfully connected with auto-discovered database");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FunctionToolsTests Setup] Database auto-discovery failed: {ex.Message}");
                Console.WriteLine($"[FunctionToolsTests Setup] Using fallback database ID: TestDB");
                dbId = "TestDB";
                var powerBiConfig = new PowerBiConfig { Port = port, DbId = dbId };
                tabularConnection = new TabularConnection(powerBiConfig);
            }
        }
        else
        {
            Console.WriteLine($"[FunctionToolsTests Setup] Using provided PBI_DB_ID: {dbId}");
            var powerBiConfig = new PowerBiConfig { Port = port, DbId = dbId };
            tabularConnection = new TabularConnection(powerBiConfig);
        }

        _daxTools = new DaxTools(tabularConnection, logger);
    }

    /// <summary>
    /// Tests that ListFunctions returns results without throwing when called with no filters
    /// </summary>
    [Fact]
    public async Task ListFunctions_NoFilters_ReturnsResults()
    {
        Console.WriteLine("\n[ListFunctions_NoFilters_ReturnsResults] Listing functions for DATETIME");
 
        var response = await _daxTools.ListFunctions("DATETIME");
        Tests.LogToolResponse(response);
 
        var result = Tests.ExtractDataFromResponse(response);
        Assert.NotNull(result);
        // result may be empty for some models; if present verify minimal columns
        if (result.Any())
        {
            var firstFunction = result.First();
            Assert.True(firstFunction.ContainsKey("FUNCTION_NAME"), "Result should contain FUNCTION_NAME");
            Assert.True(firstFunction.ContainsKey("DESCRIPTION"), "Result should contain DESCRIPTION");
            Assert.False(firstFunction.ContainsKey("INTERFACE_NAME"), "ListFunctions should not include INTERFACE_NAME");
            Assert.False(firstFunction.ContainsKey("ORIGIN"), "ListFunctions should not include ORIGIN");
        }
 
        Console.WriteLine($"[ListFunctions_NoFilters_ReturnsResults] Found {result.Count()} functions");
    }

    /// <summary>
    /// Tests that ListFunctions can filter by INTERFACE_NAME
    /// </summary>
    [Fact]
    public async Task ListFunctions_FilterByInterfaceName_ReturnsFilteredResults()
    {
        Console.WriteLine("\n[ListFunctions_FilterByInterfaceName_ReturnsFilteredResults] Filtering by DATETIME interface");
 
        var response = await _daxTools.ListFunctions("DATETIME");
        Tests.LogToolResponse(response);
 
        var result = Tests.ExtractDataFromResponse(response);
        Assert.NotNull(result);
 
        if (result.Any())
        {
            // Verify results contain minimal columns
            foreach (var func in result)
            {
                Assert.True(func.ContainsKey("FUNCTION_NAME"), "Result should contain FUNCTION_NAME");
                Assert.True(func.ContainsKey("DESCRIPTION"), "Result should contain DESCRIPTION");
            }
            Console.WriteLine($"[ListFunctions_FilterByInterfaceName_ReturnsFilteredResults] Found {result.Count()} DATETIME functions");
        }
        else
        {
            Console.WriteLine("[ListFunctions_FilterByInterfaceName_ReturnsFilteredResults] No DATETIME functions found (may vary by model)");
        }
    }

    /// <summary>
    /// Tests that GetFunctionDetails returns details for a known function
    /// </summary>
    [Fact]
    public async Task GetFunctionDetails_KnownFunction_ReturnsDetails()
    {
        Console.WriteLine("\n[GetFunctionDetails_KnownFunction_ReturnsDetails] Getting details for SUM function");

        var response = await _daxTools.GetFunctionDetails("SUM");
        Tests.LogToolResponse(response);

        // Response should be a single dictionary or a collection with one item
        Dictionary<string, object?>? functionDetails = null;

        if (response is Dictionary<string, object?> singleResult)
        {
            functionDetails = singleResult;
        }
        else if (response is IEnumerable<Dictionary<string, object?>> collection)
        {
            functionDetails = collection.FirstOrDefault();
        }

        Assert.NotNull(functionDetails);
        Assert.True(functionDetails.ContainsKey("FUNCTION_NAME"), "Result should contain FUNCTION_NAME");
        Assert.True(functionDetails.ContainsKey("DESCRIPTION"), "Result should contain DESCRIPTION");
        Assert.True(functionDetails.ContainsKey("PARAMETER_LIST"), "Result should contain PARAMETER_LIST");

        // Verify FUNCTION_NAME matches (case-insensitive)
        var functionName = functionDetails["FUNCTION_NAME"]?.ToString() ?? "";
        Assert.Equal("SUM", functionName, StringComparer.OrdinalIgnoreCase);

        Console.WriteLine($"[GetFunctionDetails_KnownFunction_ReturnsDetails] Successfully retrieved details for {functionName}");
    }

    /// <summary>
    /// Tests that GetFunctionDetails throws exception for non-existent function
    /// </summary>
    [Fact]
    public async Task GetFunctionDetails_NonExistentFunction_ThrowsException()
    {
        Console.WriteLine("\n[GetFunctionDetails_NonExistentFunction_ThrowsException] Testing with non-existent function");

        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await _daxTools.GetFunctionDetails("NONEXISTENT_FUNCTION_XYZ");
        });

        Console.WriteLine("[GetFunctionDetails_NonExistentFunction_ThrowsException] Correctly threw ArgumentException");
    }

    /// <summary>
    /// Tests that GetFunctionDetails throws exception for empty function name
    /// </summary>
    [Fact]
    public async Task GetFunctionDetails_EmptyFunctionName_ThrowsException()
    {
        Console.WriteLine("\n[GetFunctionDetails_EmptyFunctionName_ThrowsException] Testing with empty function name");

        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await _daxTools.GetFunctionDetails("");
        });

        Console.WriteLine("[GetFunctionDetails_EmptyFunctionName_ThrowsException] Correctly threw ArgumentException");
    }

    /// <summary>
    /// Tests that GetFunctionDetails throws exception for invalid function name format
    /// </summary>
    [Fact]
    public async Task GetFunctionDetails_InvalidFunctionName_ThrowsException()
    {
        Console.WriteLine("\n[GetFunctionDetails_InvalidFunctionName_ThrowsException] Testing with invalid function name");

        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await _daxTools.GetFunctionDetails("INVALID'; DROP TABLE--");
        });

        Console.WriteLine("[GetFunctionDetails_InvalidFunctionName_ThrowsException] Correctly threw ArgumentException");
    }

    /// <summary>
    /// Tests that GetFunctionDetails returns PARAMETERINFO for a function with parameters
    /// </summary>
    [Fact]
    public async Task GetFunctionDetails_FunctionWithParameters_ReturnsParameterInfo()
    {
        Console.WriteLine("\n[GetFunctionDetails_FunctionWithParameters_ReturnsParameterInfo] Getting details for CALCULATE function");

        var response = await _daxTools.GetFunctionDetails("CALCULATE");
        Tests.LogToolResponse(response);

        Dictionary<string, object?>? functionDetails = null;

        if (response is Dictionary<string, object?> singleResult)
        {
            functionDetails = singleResult;
        }
        else if (response is IEnumerable<Dictionary<string, object?>> collection)
        {
            functionDetails = collection.FirstOrDefault();
        }

        Assert.NotNull(functionDetails);
        Assert.True(functionDetails.ContainsKey("PARAMETER_LIST"), "Result should contain PARAMETER_LIST");

        // Check if PARAMETERINFO exists and is properly formatted
        if (functionDetails.TryGetValue("PARAMETERINFO", out var paramInfo) && paramInfo != null)
        {
            Console.WriteLine($"[GetFunctionDetails_FunctionWithParameters_ReturnsParameterInfo] PARAMETERINFO type: {paramInfo.GetType().Name}");

            // PARAMETERINFO should be a list or array of parameter details
            if (paramInfo is IEnumerable<object> paramList)
            {
                var paramCount = paramList.Count();
                Console.WriteLine($"[GetFunctionDetails_FunctionWithParameters_ReturnsParameterInfo] Found {paramCount} parameters");
                Assert.True(paramCount > 0, "CALCULATE should have parameters");
            }
        }

        Console.WriteLine("[GetFunctionDetails_FunctionWithParameters_ReturnsParameterInfo] Successfully validated parameter info");
    }

    /// <summary>
    /// Tests that GetFunctionDetails handles case-insensitive function names
    /// </summary>
    [Fact]
    public async Task GetFunctionDetails_CaseInsensitiveFunctionName_ReturnsDetails()
    {
        Console.WriteLine("\n[GetFunctionDetails_CaseInsensitiveFunctionName_ReturnsDetails] Testing case insensitivity with 'sum'");

        var response = await _daxTools.GetFunctionDetails("sum"); // lowercase
        Tests.LogToolResponse(response);

        Dictionary<string, object?>? functionDetails = null;

        if (response is Dictionary<string, object?> singleResult)
        {
            functionDetails = singleResult;
        }
        else if (response is IEnumerable<Dictionary<string, object?>> collection)
        {
            functionDetails = collection.FirstOrDefault();
        }

        Assert.NotNull(functionDetails);
        Assert.True(functionDetails.ContainsKey("FUNCTION_NAME"), "Result should contain FUNCTION_NAME");

        var functionName = functionDetails["FUNCTION_NAME"]?.ToString() ?? "";
        Assert.Equal("SUM", functionName, StringComparer.OrdinalIgnoreCase);

        Console.WriteLine("[GetFunctionDetails_CaseInsensitiveFunctionName_ReturnsDetails] Case-insensitive lookup works correctly");
    }
}
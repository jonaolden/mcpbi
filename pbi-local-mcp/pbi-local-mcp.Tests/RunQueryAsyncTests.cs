using System.Text.Json;

using Microsoft.Extensions.Logging.Abstractions;

using pbi_local_mcp.Configuration;

namespace pbi_local_mcp.Tests;

public class RunQueryAsyncTests
{
    private static DaxTools CreateTools()
    {
        // Always read the repository root .env and prefer environment variables.
        string? port = Environment.GetEnvironmentVariable("PBI_PORT");
        string? dbId = Environment.GetEnvironmentVariable("PBI_DB_ID");

        // Project root .env path (repository root relative to test runtime)
        var repoEnv = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".env"));

        if (File.Exists(repoEnv))
        {
            foreach (var rawLine in File.ReadAllLines(repoEnv))
            {
                if (string.IsNullOrWhiteSpace(rawLine)) continue;
                var line = rawLine.Trim();
                if (line.StartsWith("#") || line.StartsWith("//")) continue;
                var idx = line.IndexOf('=');
                if (idx < 0) continue;

                var key = line.Substring(0, idx).Trim().TrimStart('\uFEFF').ToUpperInvariant();
                var value = line.Substring(idx + 1).Trim().Trim('"').Trim('\'');

                if (string.IsNullOrWhiteSpace(port) && key == "PBI_PORT")
                    port = value;
                if (string.IsNullOrWhiteSpace(dbId) && key == "PBI_DB_ID")
                    dbId = value;

                if (!string.IsNullOrWhiteSpace(port) && !string.IsNullOrWhiteSpace(dbId))
                    break;
            }

            // propagate into environment so other code paths observe the values
            if (!string.IsNullOrWhiteSpace(port)) Environment.SetEnvironmentVariable("PBI_PORT", port);

            // If DB id missing but port present, attempt to discover the database id from running instances
            if (string.IsNullOrWhiteSpace(dbId) && !string.IsNullOrWhiteSpace(port) && int.TryParse(port, out var discoveredPort))
            {
                try
                {
                    var discovery = new InstanceDiscovery(NullLogger<InstanceDiscovery>.Instance);
                    var instances = discovery.DiscoverInstances().GetAwaiter().GetResult();
                    foreach (var inst in instances)
                    {
                        if (inst.Port == discoveredPort && inst.Databases != null && inst.Databases.Count > 0)
                        {
                            dbId = inst.Databases[0].Id;
                            break;
                        }
                    }
                }
                catch
                {
                    // Ignore discovery failures in test harness; PowerBiConfig will surface missing values.
                }
            }

            if (!string.IsNullOrWhiteSpace(dbId)) Environment.SetEnvironmentVariable("PBI_DB_ID", dbId);
        }

        // No fallback defaults: require values from environment or .env (PowerBiConfig will validate)
        var config = new PowerBiConfig { Port = port ?? string.Empty, DbId = dbId ?? string.Empty };
        var connectionLogger = NullLogger<TabularConnection>.Instance;
        var connection = new TabularConnection(config, connectionLogger);
        var daxToolsLogger = NullLogger<DaxTools>.Instance;
        return new DaxTools(connection, daxToolsLogger);
    }

    [Fact]
    public async Task RunQueryAsync_Verbose_SuccessEnvelope()
    {
        var tools = CreateTools();

        // Simple scalar expression (will be wrapped -> WasModified = true)
        var json = await tools.RunQueryAsync("1+1", verbose: true);
        Assert.False(string.IsNullOrWhiteSpace(json));

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.True(root.GetProperty("wasModified").GetBoolean());
        Assert.Equal("DAX", root.GetProperty("queryType").GetString());

        var resultProp = root.GetProperty("result");
        Assert.Equal(JsonValueKind.Array, resultProp.ValueKind);
    }

    [Fact]
    public async Task RunQueryAsync_Verbose_ValidationErrorEnvelope()
    {
        var tools = CreateTools();

        var json = await tools.RunQueryAsync("", verbose: true);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.Equal("validation", root.GetProperty("errorCategory").GetString());
        Assert.NotNull(root.GetProperty("errorMessage").GetString());
    }

    [Fact]
    public async Task RunQueryAsync_NonVerbose_SuccessRaw()
    {
        var tools = CreateTools();

        var json = await tools.RunQueryAsync("EVALUATE {1}", verbose: false);
        using var doc = JsonDocument.Parse(json);

        // Raw successful result should be a JSON array (not an envelope with success property)
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
    }

    [Fact]
    public async Task RunQueryAsync_NonVerbose_ExecutionError_Throws()
    {
        var tools = CreateTools();

        // Invalid function to force execution error
        await Assert.ThrowsAsync<Exception>(async () =>
            await tools.RunQueryAsync("EVALUATE BADFUNCTION()", verbose: false));
    }

    [Fact]
    public async Task RunQueryAsync_InvalidQueryType_Verbose_ErrorEnvelope()
    {
        var tools = CreateTools();

        var json = await tools.RunQueryAsync("EVALUATE {1}", queryType: "NotAType", verbose: true);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.Equal("validation", root.GetProperty("errorCategory").GetString());
        Assert.Contains("Invalid queryType", root.GetProperty("errorMessage").GetString());
    }

    [Fact]
    public async Task RunQueryAsync_InvalidQueryType_NonVerbose_Throws()
    {
        var tools = CreateTools();

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await tools.RunQueryAsync("EVALUATE {1}", queryType: "BadType", verbose: false));
    }
}

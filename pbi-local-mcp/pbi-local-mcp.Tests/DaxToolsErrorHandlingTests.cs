using Microsoft.Extensions.Logging.Abstractions;

using pbi_local_mcp.Configuration;

namespace pbi_local_mcp.Tests;

public class DaxToolsErrorHandlingTests
{
    [Fact]
    public async Task RunQuery_DAXError_ReturnsStructuredErrorResponse()
    {
        // Arrange
        var port = Environment.GetEnvironmentVariable("PBI_PORT") ?? "12345";
        var dbId = Environment.GetEnvironmentVariable("PBI_DB_ID") ?? "TestDB";
        var config = new PowerBiConfig { Port = port, DbId = dbId };
        var logger = NullLogger<TabularConnection>.Instance;
        var connection = new TabularConnection(config, logger);
        var daxToolsLogger = NullLogger<DaxTools>.Instance;
        var daxTools = new DaxTools(connection, daxToolsLogger);

        // Act - Execute query with invalid DAX that will cause execution error
        var result = await daxTools.RunQuery("EVALUATE BADFUNCTION()");

        // Assert - Verify structured error response
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
        Assert.Contains("EVALUATE BADFUNCTION()", originalQueryProperty!.GetValue(queryInfo)!.ToString());
    }

    [Fact]
    public async Task RunQuery_ValidationError_ReturnsStructuredErrorResponse()
    {
        // Arrange
        var port = Environment.GetEnvironmentVariable("PBI_PORT") ?? "12345";
        var dbId = Environment.GetEnvironmentVariable("PBI_DB_ID") ?? "TestDB";
        var config = new PowerBiConfig { Port = port, DbId = dbId };
        var logger = NullLogger<TabularConnection>.Instance;
        var connection = new TabularConnection(config, logger);
        var daxToolsLogger = NullLogger<DaxTools>.Instance;
        var daxTools = new DaxTools(connection, daxToolsLogger);

        // Act - Test with an empty query that will trigger validation error
        var result = await daxTools.RunQuery("");

        // Assert - Verify structured error response
        Assert.NotNull(result);
        var resultType = result.GetType();
        var successProperty = resultType.GetProperty("Success");
        Assert.NotNull(successProperty);
        Assert.False((bool)successProperty.GetValue(result)!);

        var errorCategoryProperty = resultType.GetProperty("ErrorCategory");
        Assert.NotNull(errorCategoryProperty);
        Assert.Equal("validation", errorCategoryProperty.GetValue(result));

        var errorDetailsProperty = resultType.GetProperty("ErrorDetails");
        Assert.NotNull(errorDetailsProperty);
        var errorDetails = errorDetailsProperty.GetValue(result);
        var messageProperty = errorDetails!.GetType().GetProperty("Message");
        Assert.Contains("DAX query cannot be null or empty", messageProperty!.GetValue(errorDetails)!.ToString());
    }

    [Fact]
    public async Task RunQuery_UnbalancedParentheses_ReturnsStructuredErrorResponse()
    {
        // Arrange
        var port = Environment.GetEnvironmentVariable("PBI_PORT") ?? "12345";
        var dbId = Environment.GetEnvironmentVariable("PBI_DB_ID") ?? "TestDB";
        var config = new PowerBiConfig { Port = port, DbId = dbId };
        var logger = NullLogger<TabularConnection>.Instance;
        var connection = new TabularConnection(config, logger);
        var daxToolsLogger = NullLogger<DaxTools>.Instance;
        var daxTools = new DaxTools(connection, daxToolsLogger);

        // Act - Test with unbalanced parentheses
        var result = await daxTools.RunQuery("SUM(Sales[Amount]");

        // Assert - Verify structured error response
        Assert.NotNull(result);
        var resultType = result.GetType();
        var successProperty = resultType.GetProperty("Success");
        Assert.NotNull(successProperty);
        Assert.False((bool)successProperty.GetValue(result)!);

        var errorCategoryProperty = resultType.GetProperty("ErrorCategory");
        Assert.NotNull(errorCategoryProperty);
        Assert.Equal("validation", errorCategoryProperty.GetValue(result));

        var suggestionsProperty = resultType.GetProperty("Suggestions");
        Assert.NotNull(suggestionsProperty);
        var suggestions = suggestionsProperty.GetValue(result) as System.Collections.IEnumerable;
        Assert.NotNull(suggestions);
        var suggestionsList = suggestions.Cast<string>().ToList();
        Assert.Contains(suggestionsList, s => s.Contains("unbalanced parentheses"));
    }

    [Fact]
    public async Task RunQuery_WhitespaceOnly_ReturnsStructuredErrorResponse()
    {
        // Arrange
        var port = Environment.GetEnvironmentVariable("PBI_PORT") ?? "12345";
        var dbId = Environment.GetEnvironmentVariable("PBI_DB_ID") ?? "TestDB";
        var config = new PowerBiConfig { Port = port, DbId = dbId };
        var logger = NullLogger<TabularConnection>.Instance;
        var connection = new TabularConnection(config, logger);
        var daxToolsLogger = NullLogger<DaxTools>.Instance;
        var daxTools = new DaxTools(connection, daxToolsLogger);

        // Act - Test with whitespace-only query
        var result = await daxTools.RunQuery("   \t\n  ");

        // Assert - Verify structured error response
        Assert.NotNull(result);
        var resultType = result.GetType();
        var successProperty = resultType.GetProperty("Success");
        Assert.NotNull(successProperty);
        Assert.False((bool)successProperty.GetValue(result)!);

        var errorCategoryProperty = resultType.GetProperty("ErrorCategory");
        Assert.NotNull(errorCategoryProperty);
        Assert.Equal("validation", errorCategoryProperty.GetValue(result));
    }

    [Fact]
    public async Task RunQuery_InvalidDefineQuery_ReturnsStructuredErrorResponse()
    {
        // Arrange
        var port = Environment.GetEnvironmentVariable("PBI_PORT") ?? "12345";
        var dbId = Environment.GetEnvironmentVariable("PBI_DB_ID") ?? "TestDB";
        var config = new PowerBiConfig { Port = port, DbId = dbId };
        var logger = NullLogger<TabularConnection>.Instance;
        var connection = new TabularConnection(config, logger);
        var daxToolsLogger = NullLogger<DaxTools>.Instance;
        var daxTools = new DaxTools(connection, daxToolsLogger);

        // Act - Test with invalid DEFINE query structure (missing EVALUATE)
        var result = await daxTools.RunQuery("DEFINE MEASURE Sales[Total] = SUM(Sales[Amount])");

        // Assert - Verify structured error response
        Assert.NotNull(result);
        var resultType = result.GetType();
        var successProperty = resultType.GetProperty("Success");
        Assert.NotNull(successProperty);
        Assert.False((bool)successProperty.GetValue(result)!);

        var errorCategoryProperty = resultType.GetProperty("ErrorCategory");
        Assert.NotNull(errorCategoryProperty);
        Assert.Equal("validation", errorCategoryProperty.GetValue(result));

        var suggestionsProperty = resultType.GetProperty("Suggestions");
        Assert.NotNull(suggestionsProperty);
        var suggestions = suggestionsProperty.GetValue(result) as System.Collections.IEnumerable;
        Assert.NotNull(suggestions);
        var suggestionsList = suggestions.Cast<string>().ToList();
        Assert.Contains(suggestionsList, s => s.Contains("DEFINE blocks must be followed by an EVALUATE statement"));
    }
}

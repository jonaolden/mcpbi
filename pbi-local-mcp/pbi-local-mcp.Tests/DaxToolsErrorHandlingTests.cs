using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol;
using pbi_local_mcp.Configuration;

namespace pbi_local_mcp.Tests;

public class DaxToolsErrorHandlingTests
{
    [Fact]
    public async Task RunQuery_DAXError_PreservesEnhancedErrorMessage()
    {
        // Arrange
        var config = new PowerBiConfig { Port = "12345", DbId = "TestDB" };
        var logger = NullLogger<TabularConnection>.Instance;
        var connection = new TabularConnection(config, logger);
        var daxToolsLogger = NullLogger<DaxTools>.Instance;
        var daxTools = new DaxTools(connection, daxToolsLogger);
        
        // Act & Assert - Expect standard Exception (not McpException to avoid serialization issues)
        var exception = await Assert.ThrowsAsync<Exception>(() =>
            daxTools.RunQuery("EVALUATE BADFUNCTION()"));
        
        // Verify the enhanced message is preserved and contains all expected information
        Assert.Contains("DAX query execution failed:", exception.Message);
        Assert.Contains("Query Type: DAX", exception.Message);
        Assert.Contains("Original Query:", exception.Message);
        Assert.Contains("EVALUATE BADFUNCTION()", exception.Message);
        Assert.Contains("Final Query:", exception.Message);
    }

    [Fact]
    public async Task RunQuery_ValidationError_ThrowsArgumentException()
    {
        // Arrange
        var config = new PowerBiConfig { Port = "12345", DbId = "TestDB" };
        var logger = NullLogger<TabularConnection>.Instance;
        var connection = new TabularConnection(config, logger);
        var daxToolsLogger = NullLogger<DaxTools>.Instance;
        var daxTools = new DaxTools(connection, daxToolsLogger);
        
        // Act & Assert - Test with an empty query that will trigger validation error
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            daxTools.RunQuery(""));
        
        // Verify validation errors get proper context
        Assert.Contains("DAX query cannot be null or empty", exception.Message);
        Assert.Equal("dax", exception.ParamName);
    }

    [Fact]
    public async Task RunQuery_UnbalancedParentheses_ThrowsArgumentException()
    {
        // Arrange
        var config = new PowerBiConfig { Port = "12345", DbId = "TestDB" };
        var logger = NullLogger<TabularConnection>.Instance;
        var connection = new TabularConnection(config, logger);
        var daxToolsLogger = NullLogger<DaxTools>.Instance;
        var daxTools = new DaxTools(connection, daxToolsLogger);
        
        // Act & Assert - Test with unbalanced parentheses
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            daxTools.RunQuery("SUM(Sales[Amount]"));
        
        // Verify validation errors get proper context
        Assert.Contains("Query validation failed:", exception.Message);
        Assert.Contains("unbalanced parentheses", exception.Message);
        Assert.Equal("dax", exception.ParamName);
    }

    [Fact]
    public async Task RunQuery_WhitespaceOnly_ThrowsArgumentException()
    {
        // Arrange
        var config = new PowerBiConfig { Port = "12345", DbId = "TestDB" };
        var logger = NullLogger<TabularConnection>.Instance;
        var connection = new TabularConnection(config, logger);
        var daxToolsLogger = NullLogger<DaxTools>.Instance;
        var daxTools = new DaxTools(connection, daxToolsLogger);
        
        // Act & Assert - Test with whitespace-only query
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            daxTools.RunQuery("   \t\n  "));
        
        // Verify validation errors get proper context
        Assert.Contains("DAX query cannot be null or empty", exception.Message);
        Assert.Equal("dax", exception.ParamName);
    }

    [Fact]
    public async Task RunQuery_InvalidDefineQuery_ThrowsArgumentException()
    {
        // Arrange
        var config = new PowerBiConfig { Port = "12345", DbId = "TestDB" };
        var logger = NullLogger<TabularConnection>.Instance;
        var connection = new TabularConnection(config, logger);
        var daxToolsLogger = NullLogger<DaxTools>.Instance;
        var daxTools = new DaxTools(connection, daxToolsLogger);
        
        // Act & Assert - Test with invalid DEFINE query structure
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            daxTools.RunQuery("DEFINE EVALUATE ROW(\"Test\", 1)"));
        
        // Verify validation errors get proper context
        Assert.Contains("DAX query structure validation failed:", exception.Message);
        Assert.Equal("dax", exception.ParamName);
    }
}
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
        
        // Act & Assert
        var exception = await Assert.ThrowsAsync<McpException>(() =>
            daxTools.RunQuery("EVALUATE BADFUNCTION()"));
        
        // Verify the enhanced message is preserved and contains all expected information
        Assert.Contains("DAX Query Error:", exception.Message);
        Assert.Contains("Query Type: DAX", exception.Message);
        Assert.Contains("EVALUATE BADFUNCTION()", exception.Message);
        
        // Ensure we're not double-wrapping the error message
        Assert.DoesNotContain("Error executing DAX query: DAX Query Error:", exception.Message);
    }

    [Fact]
    public async Task RunQuery_ValidationError_AddsDAXContext()
    {
        // Arrange
        var config = new PowerBiConfig { Port = "12345", DbId = "TestDB" };
        var logger = NullLogger<TabularConnection>.Instance;
        var connection = new TabularConnection(config, logger);
        var daxToolsLogger = NullLogger<DaxTools>.Instance;
        var daxTools = new DaxTools(connection, daxToolsLogger);
        
        // Act & Assert - Test with an empty query that will trigger validation error
        var exception = await Assert.ThrowsAsync<McpException>(() =>
            daxTools.RunQuery(""));
        
        // Verify validation errors get proper context
        Assert.Contains("Error executing DAX query:", exception.Message);
        Assert.Contains("Query cannot be empty", exception.Message);
        Assert.Contains("Original Query:", exception.Message);
    }

    [Fact]
    public async Task RunQuery_UnbalancedParentheses_AddsDAXContext()
    {
        // Arrange
        var config = new PowerBiConfig { Port = "12345", DbId = "TestDB" };
        var logger = NullLogger<TabularConnection>.Instance;
        var connection = new TabularConnection(config, logger);
        var daxToolsLogger = NullLogger<DaxTools>.Instance;
        var daxTools = new DaxTools(connection, daxToolsLogger);
        
        // Act & Assert - Test with unbalanced parentheses
        var exception = await Assert.ThrowsAsync<McpException>(() =>
            daxTools.RunQuery("SUM(Sales[Amount]"));
        
        // Verify validation errors get proper context
        Assert.Contains("Error executing DAX query:", exception.Message);
        Assert.Contains("unbalanced parentheses", exception.Message);
        Assert.Contains("Original Query:", exception.Message);
        Assert.Contains("SUM(Sales[Amount]", exception.Message);
    }
}
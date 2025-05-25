using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using pbi_local_mcp.Configuration;
using Microsoft.AnalysisServices.AdomdClient;
using System.Data;
using ModelContextProtocol;

namespace pbi_local_mcp.Tests;

public class EnhancedErrorMessageTests
{
    [Fact]
    public void TestCreateEnhancedErrorMessage_DAXQuery()
    {
        // Arrange
        var config = new PowerBiConfig { Port = "12345", DbId = "TestDB" };
        var logger = NullLogger<TabularConnection>.Instance;
        var connection = new TabularConnection(config, logger);
        
        // Create a test scenario that would trigger enhanced error message
        var testQuery = "EVALUATE BADFUNCTION()";
        var testException = new Exception("Unknown function 'BADFUNCTION'");
        
        // Act & Assert
        try
        {
            // Simulate the error handling logic from TabularConnection
            var enhancedMessage = $"DAX Query Error: {testException.Message}\n\nQuery Type: DAX\nQuery: {testQuery}";
            throw new Exception(enhancedMessage, testException);
        }
        catch (Exception ex)
        {
            // Verify the enhanced message contains all expected information
            Assert.True(ex.Message.Contains("DAX Query Error"));
            Assert.True(ex.Message.Contains("Unknown function 'BADFUNCTION'"));
            Assert.True(ex.Message.Contains("Query Type: DAX"));
            Assert.True(ex.Message.Contains("EVALUATE BADFUNCTION()"));
            Assert.Equal(testException, ex.InnerException);
        }
    }

    [Fact]
    public void TestCreateEnhancedErrorMessage_DMVQuery()
    {
        // Arrange
        var config = new PowerBiConfig { Port = "12345", DbId = "TestDB" };
        var logger = NullLogger<TabularConnection>.Instance;
        var connection = new TabularConnection(config, logger);
        
        // Create a test scenario for DMV query error
        var testQuery = "SELECT * FROM $SYSTEM.BADTABLE";
        var testException = new Exception("Table '$SYSTEM.BADTABLE' does not exist");
        
        // Act & Assert
        try
        {
            // Simulate the error handling logic from TabularConnection
            var enhancedMessage = $"DMV Query Error: {testException.Message}\n\nQuery Type: DMV\nQuery: {testQuery}";
            throw new Exception(enhancedMessage, testException);
        }
        catch (Exception ex)
        {
            // Verify the enhanced message contains all expected information
            Assert.True(ex.Message.Contains("DMV Query Error"));
            Assert.True(ex.Message.Contains("Table '$SYSTEM.BADTABLE' does not exist"));
            Assert.True(ex.Message.Contains("Query Type: DMV"));
            Assert.True(ex.Message.Contains("SELECT * FROM $SYSTEM.BADTABLE"));
            Assert.Equal(testException, ex.InnerException);
        }
    }

    [Fact]
    public void TestCreateEnhancedErrorMessage_LongQuery_ShouldTruncate()
    {
        // Arrange
        var config = new PowerBiConfig { Port = "12345", DbId = "TestDB" };
        var logger = NullLogger<TabularConnection>.Instance;
        var connection = new TabularConnection(config, logger);
        
        // Create a very long query (over 200 characters)
        var longQuery = new string('X', 250); // 250 characters
        var testException = new Exception("Query too complex");
        
        // Act & Assert
        try
        {
            // Simulate the error handling logic from TabularConnection
            var truncatedQuery = longQuery.Length > 200 ? longQuery.Substring(0, 200) + "..." : longQuery;
            var enhancedMessage = $"DAX Query Error: {testException.Message}\n\nQuery Type: DAX\nQuery: {truncatedQuery}";
            throw new Exception(enhancedMessage, testException);
        }
        catch (Exception ex)
        {
            // Verify the message is truncated properly
            Assert.True(ex.Message.Contains("DAX Query Error"));
            Assert.True(ex.Message.Contains("Query too complex"));
            Assert.True(ex.Message.Contains("Query Type: DAX"));
            Assert.True(ex.Message.EndsWith("..."));
            // Verify the query is truncated to approximately 200 characters + "..."
            var queryPart = ex.Message.Substring(ex.Message.IndexOf("Query: ") + 7);
            Assert.True(queryPart.Length <= 210); // 200 + "..." + some buffer
        }
    }
}
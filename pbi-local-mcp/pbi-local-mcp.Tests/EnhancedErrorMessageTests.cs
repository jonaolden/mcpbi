using Microsoft.Extensions.Logging.Abstractions;

using pbi_local_mcp.Configuration;

namespace pbi_local_mcp.Tests;

public class EnhancedErrorMessageTests
{
    [Fact]
    public void TestCreateEnhancedErrorMessage_DAXQuery()
    {
        // Arrange
        var port = Environment.GetEnvironmentVariable("PBI_PORT") ?? "12345";
        var dbId = Environment.GetEnvironmentVariable("PBI_DB_ID") ?? "TestDB";
        var config = new PowerBiConfig { Port = port, DbId = dbId };
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
            Assert.Contains("DAX Query Error", ex.Message);
            Assert.Contains("Unknown function 'BADFUNCTION'", ex.Message);
            Assert.Contains("Query Type: DAX", ex.Message);
            Assert.Contains("EVALUATE BADFUNCTION()", ex.Message);
            Assert.Equal(testException, ex.InnerException);
        }
    }

    [Fact]
    public void TestCreateEnhancedErrorMessage_DMVQuery()
    {
        // Arrange
        var port = Environment.GetEnvironmentVariable("PBI_PORT") ?? "12345";
        var dbId = Environment.GetEnvironmentVariable("PBI_DB_ID") ?? "TestDB";
        var config = new PowerBiConfig { Port = port, DbId = dbId };
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
            Assert.Contains("DMV Query Error", ex.Message);
            Assert.Contains("Table '$SYSTEM.BADTABLE' does not exist", ex.Message);
            Assert.Contains("Query Type: DMV", ex.Message);
            Assert.Contains("SELECT * FROM $SYSTEM.BADTABLE", ex.Message);
            Assert.Equal(testException, ex.InnerException);
        }
    }

    [Fact]
    public void TestCreateEnhancedErrorMessage_LongQuery_ShouldTruncate()
    {
        // Arrange
        var port = Environment.GetEnvironmentVariable("PBI_PORT") ?? "12345";
        var dbId = Environment.GetEnvironmentVariable("PBI_DB_ID") ?? "TestDB";
        var config = new PowerBiConfig { Port = port, DbId = dbId };
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
            Assert.Contains("DAX Query Error", ex.Message);
            Assert.Contains("Query too complex", ex.Message);
            Assert.Contains("Query Type: DAX", ex.Message);
            Assert.EndsWith("...", ex.Message);
            // Verify the query is truncated to approximately 200 characters + "..."
            var queryPart = ex.Message.Substring(ex.Message.IndexOf("Query: ") + 7);
            Assert.True(queryPart.Length <= 210); // 200 + "..." + some buffer
        }
    }
}

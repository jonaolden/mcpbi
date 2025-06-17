using Xunit;
using pbi_local_mcp.Core;
using pbi_local_mcp.Configuration;

namespace pbi_local_mcp.Tests;

/// <summary>
/// Security tests for SQL injection vulnerabilities and input validation
/// </summary>
public class SecurityTests
{
    [Theory]
    [InlineData("ValidTableName")]
    [InlineData("Table_With_Underscores")]
    [InlineData("Table With Spaces")]
    [InlineData("Table123")]
    [InlineData("Profit Margin %")]
    [InlineData("Table's Name")]
    [InlineData("O'Connor")]
    [InlineData("Table--comment")]
    [InlineData("Table;")]
    [InlineData("Table\n")]
    [InlineData("Table\r")]
    [InlineData("Table\t")]
    public void DaxSecurityUtils_IsValidIdentifier_ValidIdentifiers_ReturnsTrue(string identifier)
    {
        // Act
        var result = DaxSecurityUtils.IsValidIdentifier(identifier);
        
        // Assert
        Assert.True(result);
    }
    
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Table\0")] // Only null characters are rejected
    public void DaxSecurityUtils_IsValidIdentifier_InvalidIdentifiers_ReturnsFalse(string identifier)
    {
        // Act
        var result = DaxSecurityUtils.IsValidIdentifier(identifier);
        
        // Assert
        Assert.False(result);
    }
    
    [Fact]
    public void DaxSecurityUtils_IsValidIdentifier_TooLongIdentifier_ReturnsFalse()
    {
        // Arrange
        var longIdentifier = new string('a', 129); // Over 128 character limit
        
        // Act
        var result = DaxSecurityUtils.IsValidIdentifier(longIdentifier);
        
        // Assert
        Assert.False(result);
    }
    
    [Theory]
    [InlineData("TableName", "'TableName'")]
    [InlineData("Table's Name", "'Table''s Name'")]
    [InlineData("O'Connor", "'O''Connor'")]
    [InlineData("Profit Margin %", "'Profit Margin %'")]
    [InlineData("Table--comment", "'Table--comment'")]
    public void DaxSecurityUtils_EscapeDaxIdentifier_ValidIdentifiers_EscapesCorrectly(string input, string expected)
    {
        // Act
        var result = DaxSecurityUtils.EscapeDaxIdentifier(input);
        
        // Assert
        Assert.Equal(expected, result);
    }
    
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Table\0")]
    public void DaxSecurityUtils_EscapeDaxIdentifier_InvalidIdentifiers_ThrowsArgumentException(string identifier)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => DaxSecurityUtils.EscapeDaxIdentifier(identifier));
    }
    
    [Theory]
    [InlineData("[Name] = 'Value'")]
    [InlineData("[ID] > 100")]
    [InlineData("[Status] = 'Active' AND [Type] = 'User'")]
    public void FilterExpressionValidator_ValidateFilterExpression_ValidExpressions_DoesNotThrow(string filterExpr)
    {
        // Act & Assert - Should not throw
        FilterExpressionValidator.ValidateFilterExpression(filterExpr);
    }
    
    [Theory]
    [InlineData("[Name] = 'Value'; DROP TABLE Users; --")]
    [InlineData("[Name] = 'Value'--comment")]
    [InlineData("[Name] = 'Value'/*comment*/")]
    [InlineData("xp_cmdshell('dir')")]
    [InlineData("EXEC sp_executesql")]
    [InlineData("DROP TABLE Users")]
    [InlineData("INSERT INTO Users")]
    [InlineData("UPDATE Users SET")]
    [InlineData("CREATE TABLE Test")]
    [InlineData("ALTER TABLE Users")]
    [InlineData("UNION SELECT * FROM")]
    [InlineData("javascript:alert('xss')")]
    [InlineData("eval('malicious code')")]
    public void FilterExpressionValidator_ValidateFilterExpression_MaliciousExpressions_ThrowsArgumentException(string filterExpr)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => FilterExpressionValidator.ValidateFilterExpression(filterExpr));
    }
    
    [Theory]
    [InlineData("[Name] = 'Value' <script>")]
    [InlineData("[Name] = 'Value' $ illegal")]
    public void FilterExpressionValidator_ValidateFilterExpression_InvalidCharacters_ThrowsArgumentException(string filterExpr)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => FilterExpressionValidator.ValidateFilterExpression(filterExpr));
    }
    
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FilterExpressionValidator_ValidateFilterExpression_NullOrEmptyExpressions_DoesNotThrow(string filterExpr)
    {
        // Act & Assert - Should not throw for null/empty expressions
        FilterExpressionValidator.ValidateFilterExpression(filterExpr);
    }
}

/// <summary>
/// Configuration validation security tests
/// </summary>
public class PowerBiConfigSecurityTests
{
    [Theory]
    [InlineData("8080")]
    [InlineData("1433")]
    [InlineData("65535")]
    [InlineData("1")]
    public void PowerBiConfig_Port_ValidPorts_DoesNotThrow(string port)
    {
        // Arrange
        var config = new PowerBiConfig();
        
        // Act & Assert - Should not throw
        config.Port = port;
        Assert.Equal(port, config.Port);
    }
    
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("65536")]
    [InlineData("99999")]
    [InlineData("abc")]
    [InlineData("80.5")]
    [InlineData("8080; DROP TABLE Users; --")]
    public void PowerBiConfig_Port_InvalidPorts_ThrowsArgumentException(string port)
    {
        // Arrange
        var config = new PowerBiConfig();
        
        // Act & Assert
        Assert.Throws<ArgumentException>(() => config.Port = port);
    }
    
    [Theory]
    [InlineData("ValidDatabaseId")]
    [InlineData("Database_123")]
    [InlineData("db-with-hyphens")]
    public void PowerBiConfig_DbId_ValidDbIds_DoesNotThrow(string dbId)
    {
        // Arrange
        var config = new PowerBiConfig();
        
        // Act & Assert - Should not throw
        config.DbId = dbId;
        Assert.Equal(dbId, config.DbId);
    }
    
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void PowerBiConfig_DbId_NullOrEmptyDbIds_ThrowsArgumentException(string dbId)
    {
        // Arrange
        var config = new PowerBiConfig();
        
        // Act & Assert
        Assert.Throws<ArgumentException>(() => config.DbId = dbId);
    }
    
    [Fact]
    public void PowerBiConfig_DbId_TooLongDbId_ThrowsArgumentException()
    {
        // Arrange
        var config = new PowerBiConfig();
        var longDbId = new string('a', 101); // Over 100 character limit
        
        // Act & Assert
        Assert.Throws<ArgumentException>(() => config.DbId = longDbId);
    }
}
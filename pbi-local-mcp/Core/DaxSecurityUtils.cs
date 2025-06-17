using System.Text.RegularExpressions;

namespace pbi_local_mcp.Core;

/// <summary>
/// Security utilities for validating and escaping DAX identifiers and expressions
/// </summary>
public static class DaxSecurityUtils
{
    /// <summary>
    /// Validates that an identifier is safe for use in DAX/DMV queries
    /// Allows all characters but enforces reasonable length and non-empty constraints
    /// </summary>
    /// <param name="identifier">The identifier to validate</param>
    /// <returns>True if the identifier is valid and safe</returns>
    public static bool IsValidIdentifier(string identifier)
    {
        return !string.IsNullOrWhiteSpace(identifier) &&
               identifier.Length <= 128 && // Max reasonable length
               !identifier.Contains('\0'); // No null characters
    }
    
    /// <summary>
    /// Escapes a DAX identifier for safe use in queries
    /// </summary>
    /// <param name="identifier">The identifier to escape</param>
    /// <returns>The escaped identifier wrapped in quotes</returns>
    /// <exception cref="ArgumentException">Thrown if the identifier is invalid</exception>
    public static string EscapeDaxIdentifier(string identifier)
    {
        if (!IsValidIdentifier(identifier))
            throw new ArgumentException($"Invalid identifier: {identifier}");
        
        // Escape single quotes by doubling them
        return "'" + identifier.Replace("'", "''") + "'";
    }
}

/// <summary>
/// Validates filter expressions for DMV queries
/// </summary>
public static class FilterExpressionValidator
{
    private static readonly string[] ForbiddenPatterns = {
        ";", "--", "/*", "*/", "xp_", "sp_", "exec", "execute", 
        "drop", "delete", "insert", "update", "create", "alter",
        "union", "script", "eval", "javascript"
    };
    
    /// <summary>
    /// Validates a filter expression for safe use in DMV queries
    /// </summary>
    /// <param name="filterExpr">The filter expression to validate</param>
    /// <exception cref="ArgumentException">Thrown if the filter expression contains forbidden patterns</exception>
    public static void ValidateFilterExpression(string filterExpr)
    {
        if (string.IsNullOrWhiteSpace(filterExpr)) return;
        
        var lowerExpr = filterExpr.ToLowerInvariant();
        foreach (var pattern in ForbiddenPatterns)
        {
            if (lowerExpr.Contains(pattern))
                throw new ArgumentException($"Filter expression contains forbidden pattern: {pattern}");
        }
        
        // Additional validation: only allow alphanumeric, spaces, brackets, quotes, operators
        if (!Regex.IsMatch(filterExpr, @"^[a-zA-Z0-9\s\[\]'""=<>!&|().,_-]+$"))
            throw new ArgumentException("Filter expression contains invalid characters");
    }
}
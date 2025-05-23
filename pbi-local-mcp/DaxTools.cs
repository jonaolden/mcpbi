using System.ComponentModel;

using ModelContextProtocol.Server;

namespace pbi_local_mcp;

/// <summary>
/// DAX Tools exposed as MCP server tools.
/// </summary>
[McpServerToolType]
public static class DaxTools
{
    private static TabularConnection CreateConnection()
    {
        var port = Environment.GetEnvironmentVariable("PBI_PORT");
        var dbId = Environment.GetEnvironmentVariable("PBI_DB_ID");
        var config = new Configuration.PowerBiConfig { Port = port ?? "", DbId = dbId ?? "" };
        config.Validate();
        return new TabularConnection(config);
    }

    [McpServerTool, Description("List all measures in the model, optionally filtered by table name.")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public static async Task<object> ListMeasures(string? tableName = null)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        var tabular = CreateConnection();
        string dmv;

        if (!string.IsNullOrEmpty(tableName))
        {
            // First, get the actual Table ID (integer) from the table name
            var tableIdQuery = $"SELECT [ID] FROM $SYSTEM.TMSCHEMA_TABLES WHERE [NAME] = '{tableName}'";
            var tableIdResult = await tabular.ExecAsync(tableIdQuery, QueryType.DMV);

            if (tableIdResult is IEnumerable<Dictionary<string, object?>> rows && rows.Any())
            {
                var tableIdObj = rows.First()["ID"];
                if (tableIdObj != null && int.TryParse(tableIdObj.ToString(), out int actualTableId))
                {
                    dmv = $"SELECT * FROM $SYSTEM.TMSCHEMA_MEASURES WHERE [TableID] = {actualTableId}";
                }
                else
                {
                    // Table name found but ID is not an int or is null - likely indicates an issue or no measures for a non-standard table.
                    throw new Exception($"Invalid or non-integer Table ID for table '{tableName}'. Please check the table configuration.");
                }
            }
            else
            {
                // Table name not found, so no measures for this table.
                return new List<Dictionary<string, object?>>();
            }
        }
        else
        {
            dmv = "SELECT * FROM $SYSTEM.TMSCHEMA_MEASURES";
        }

        var result = await tabular.ExecAsync(dmv, QueryType.DMV);
        return result;
    }

    [McpServerTool, Description("Get details for a specific measure by name.")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public static async Task<object> GetMeasureDetails(string measureName)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        var tabular = CreateConnection();
        var dmv = $"SELECT * FROM $SYSTEM.TMSCHEMA_MEASURES WHERE [NAME] = '{measureName}'";
        var result = await tabular.ExecAsync(dmv, QueryType.DMV);
        return result;
    }

    [McpServerTool, Description("List all tables in the model.")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public static async Task<object> ListTables()
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        var tabular = CreateConnection();
        var dmv = "SELECT * FROM $SYSTEM.TMSCHEMA_TABLES";
        var result = await tabular.ExecAsync(dmv, QueryType.DMV);
        return result;
    }

    [McpServerTool, Description("Get details for a specific table by name.")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public static async Task<object> GetTableDetails(string tableName)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        var tabular = CreateConnection();
        var dmv = $"SELECT * FROM $SYSTEM.TMSCHEMA_TABLES WHERE [NAME] = '{tableName}'";
        var result = await tabular.ExecAsync(dmv, QueryType.DMV);
        return result;
    }

    [McpServerTool, Description("Get columns for a specific table by name.")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public static async Task<object> GetTableColumns(string tableName)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        var tabular = CreateConnection();
        var tableIdQuery = $"SELECT [ID] FROM $SYSTEM.TMSCHEMA_TABLES WHERE [NAME] = '{tableName}'";
        var tableIdResult = await tabular.ExecAsync(tableIdQuery, QueryType.DMV);
        var tableId = tableIdResult is IEnumerable<Dictionary<string, object?>> rows && rows.Any()
            ? rows.First()["ID"]?.ToString()
            : null;

        if (string.IsNullOrEmpty(tableId))
            throw new Exception($"Table ID not found for table '{tableName}'");

        var dmv = $"SELECT * FROM $SYSTEM.TMSCHEMA_COLUMNS WHERE [TABLEID] = '{tableId}'";
        var result = await tabular.ExecAsync(dmv, QueryType.DMV);
        return result;
    }

    [McpServerTool, Description("Get relationships for a specific table by name.")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public static async Task<object> GetTableRelationships(string tableName)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        var tabular = CreateConnection();
        var tableIdQuery = $"SELECT [ID] FROM $SYSTEM.TMSCHEMA_TABLES WHERE [NAME] = '{tableName}'";
        var tableIdResult = await tabular.ExecAsync(tableIdQuery, QueryType.DMV);
        var tableId = tableIdResult is IEnumerable<Dictionary<string, object?>> rows && rows.Any()
            ? rows.First()["ID"]?.ToString()
            : null;

        if (string.IsNullOrEmpty(tableId))
            throw new Exception($"Table ID not found for table '{tableName}'");

        var dmv = $"SELECT * FROM $SYSTEM.TMSCHEMA_RELATIONSHIPS WHERE [FROMTABLEID] = '{tableId}' OR [TOTABLEID] = '{tableId}'";
        var result = await tabular.ExecAsync(dmv, QueryType.DMV);
        return result;
    }

    [McpServerTool, Description("Preview data from a table (top N rows).")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public static async Task<object> PreviewTableData(string tableName, int topN = 10)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        var tabular = CreateConnection();
        var dax = $"EVALUATE TOPN({topN}, '{tableName}')";
        var result = await tabular.ExecAsync(dax);
        return result;
    }

    [McpServerTool, Description("Evaluate a DAX expression, optionally limiting to top N rows.")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public static async Task<object> EvaluateDAX(string dax, int topN = 10)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        var tabular = CreateConnection();
        string query = dax.Trim();
        bool isTableExpr = false;

        if (query.StartsWith("EVALUATE", StringComparison.OrdinalIgnoreCase))
        {
            isTableExpr = false;
        }
        else if (query.StartsWith("'") || query.StartsWith("SELECTCOLUMNS", StringComparison.OrdinalIgnoreCase) ||
                 query.StartsWith("ADDCOLUMNS", StringComparison.OrdinalIgnoreCase) ||
                 query.StartsWith("SUMMARIZE", StringComparison.OrdinalIgnoreCase) ||
                 query.StartsWith("FILTER", StringComparison.OrdinalIgnoreCase) ||
                 query.StartsWith("VALUES", StringComparison.OrdinalIgnoreCase) ||
                 query.StartsWith("ALL", StringComparison.OrdinalIgnoreCase))
        {
            isTableExpr = true;
        }
        else
        {
            isTableExpr = false;
        }

        if (topN > 0 && isTableExpr)
        {
            query = $"EVALUATE TOPN({topN}, {query})";
        }
        else if (topN > 0 && !query.StartsWith("EVALUATE", StringComparison.OrdinalIgnoreCase) && !isTableExpr)
        {
            query = $"EVALUATE ROW(\"Value\", {query})";
        }
        else if (!query.StartsWith("EVALUATE", StringComparison.OrdinalIgnoreCase) && isTableExpr)
        {
            query = $"EVALUATE {query}";
        }

        var result = await tabular.ExecAsync(query, QueryType.DAX);
        return result;
    }
    /// <summary>
    /// Execute a DAX query. Supports complete DAX queries with DEFINE blocks or simple expressions.
    /// </summary>
    /// <param name="dax">The DAX query to execute. Can be a complete query with DEFINE block, an EVALUATE statement, or a simple expression.</param>
    /// <param name="topN">Maximum number of rows to return for table expressions (default: 10). Ignored for complete queries.</param>
    /// <returns>Query execution result</returns>
    /// <exception cref="ArgumentException">Thrown when query validation fails</exception>
    /// <exception cref="Exception">Thrown when query execution fails</exception>
    [McpServerTool, Description("Execute a DAX query. Supports complete DAX queries with DEFINE blocks, EVALUATE statements, or simple expressions.")]
    public static async Task<object> RunQuery(string dax, int topN = 10)
    {
        var tabular = CreateConnection();
        string originalDax = dax; // Keep original for error messages if needed
        string query = dax.Trim();

        // Perform initial basic validations that should throw ArgumentException directly
        var initialValidationErrors = new List<string>();
        if (string.IsNullOrWhiteSpace(query))
        {
            initialValidationErrors.Add("Query cannot be empty.");
        }
        else
        {
            // Normalize for delimiter checks, but use original query for execution if these pass.
            // The more complex NormalizeDAXQuery (which alters whitespace significantly)
            // is better suited for the deeper ValidateCompleteDAXQuery.
            // For simple delimiter checks, we just need to iterate through the trimmed query.
            CheckBalancedDelimiters(query, '(', ')', "parentheses", initialValidationErrors);
            CheckBalancedDelimiters(query, '[', ']', "brackets", initialValidationErrors);
            CheckBalancedQuotes(query, initialValidationErrors);
        }

        if (initialValidationErrors.Any())
        {
            throw new ArgumentException(string.Join(" ", initialValidationErrors));
        }

        // If the query contains a DEFINE statement, validate its overall structure *before* the main try-catch.
        // This ensures ArgumentException from our structural validation is not wrapped by the generic engine error catch.
        // This covers cases like DEFINE after EVALUATE, or issues within the DEFINE block itself.
        if (query.ToUpperInvariant().Contains("DEFINE"))
        {
            ValidateCompleteDAXQuery(query); // This will throw ArgumentException directly if structure is wrong
        }

        try
        {
            // If it's a complete DAX query that starts with DEFINE (already validated structurally)
            if (query.StartsWith("DEFINE", StringComparison.OrdinalIgnoreCase))
            {
                var result = await tabular.ExecAsync(query, QueryType.DAX);
                return result;
            }

            // If it's already an EVALUATE statement, execute as-is
            // (basic delimiter/quote validation already done)
            if (query.StartsWith("EVALUATE", StringComparison.OrdinalIgnoreCase))
            {
                // We could also call ValidateCompleteDAXQuery here if we want to enforce
                // that EVALUATE statements not part of a DEFINE block are also well-formed
                // according to its rules (e.g., no DEFINE after EVALUATE, though that's less likely here).
                // For now, assume EVALUATE on its own is simpler and less prone to structural issues
                // beyond balanced delimiters, which are already checked.
                var result = await tabular.ExecAsync(query, QueryType.DAX);
                return result;
            }

            // Otherwise, it's a simple expression; wrap in appropriate EVALUATE statement
            var evaluateStatement = ConstructEvaluateStatement(query, topN);
            var result2 = await tabular.ExecAsync(evaluateStatement, QueryType.DAX);
            return result2;
        }
        catch (Exception ex)
        {
            throw new Exception($"Error executing DAX query: {ex.Message}\n\nQuery:\n{query}", ex);
        }
    }

    /// <summary>
    /// Validates the structure of a DAX query according to proper syntax rules.
    /// Throws ArgumentException if validation fails.
    /// </summary>
    /// <param name="query">The DAX query to validate.</param>
    private static void ValidateCompleteDAXQuery(string query)
    {
        var errors = new List<string>();

        // Check for basic requirements
        if (string.IsNullOrWhiteSpace(query))
        {
            errors.Add("Query cannot be empty.");
        }
        else
        {
            // Normalize the query for easier parsing
            var normalizedQuery = NormalizeDAXQuery(query);

            // Check if EVALUATE exists
            if (!normalizedQuery.Contains("EVALUATE", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("DAX query must contain at least one EVALUATE statement.");
            }

            // Check DEFINE block position if it exists
            if (normalizedQuery.Contains("DEFINE", StringComparison.OrdinalIgnoreCase))
            {
                int definePos = normalizedQuery.IndexOf("DEFINE", StringComparison.OrdinalIgnoreCase);
                int evaluatePos = normalizedQuery.IndexOf("EVALUATE", StringComparison.OrdinalIgnoreCase); // Assumes EVALUATE exists based on prior check

                if (evaluatePos != -1 && definePos > evaluatePos) // evaluatePos could be -1 if the EVALUATE check fails
                {
                    errors.Add("DEFINE statement must come before any EVALUATE statement.");
                }

                // Check for multiple DEFINE blocks which is not allowed
                var defineMatches = System.Text.RegularExpressions.Regex.Matches(normalizedQuery, @"\bDEFINE\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (defineMatches.Count > 1)
                {
                    errors.Add("Only one DEFINE block is allowed in a DAX query.");
                }

                // Check if DEFINE has at least one definition
                // This regex is a simplified check. A full parser would be more robust.
                var defineContentMatch = System.Text.RegularExpressions.Regex.Match(
                    normalizedQuery,
                    @"\bDEFINE\b\s*(?:MEASURE|VAR|TABLE|COLUMN)\s+",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline
                );

                if (!defineContentMatch.Success) // If DEFINE exists, it must have content
                {
                     // Further check if the content after DEFINE and before EVALUATE is just whitespace or empty
                    var defineBlockContentPattern = @"\bDEFINE\b(.*?)(?=\bEVALUATE\b|$)";
                    var defineBlockMatch = System.Text.RegularExpressions.Regex.Match(normalizedQuery, defineBlockContentPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);
                    if (defineBlockMatch.Success && string.IsNullOrWhiteSpace(defineBlockMatch.Groups[1].Value))
                    {
                        errors.Add("DEFINE block must contain at least one definition (MEASURE, VAR, TABLE, or COLUMN).");
                    }
                    // Check if the trimmed content of the DEFINE block (after "DEFINE" and before "EVALUATE")
                    // starts with one of the valid definition keywords.
                    else if (defineBlockMatch.Success) // Ensure defineBlockMatch was successful before accessing Groups
                    {
                        string defineContent = defineBlockMatch.Groups[1].Value.Trim();
                        if (!string.IsNullOrEmpty(defineContent) &&
                            !System.Text.RegularExpressions.Regex.IsMatch(defineContent, @"^\s*(MEASURE|VAR|TABLE|COLUMN)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                        {
                            // If there's content but it doesn't start with a valid definition keyword
                            errors.Add("DEFINE block must contain at least one valid definition (MEASURE, VAR, TABLE, or COLUMN).");
                        }
                        // If defineContent is empty after trimming, the previous check for IsNullOrWhiteSpace would have caught it.
                    }
                }
            }

            // Check for balanced parentheses, brackets, and quotes
            CheckBalancedDelimiters(normalizedQuery, '(', ')', "parentheses", errors);
            CheckBalancedDelimiters(normalizedQuery, '[', ']', "brackets", errors);
            CheckBalancedQuotes(normalizedQuery, errors);
        }

        if (errors.Any())
        {
            throw new ArgumentException(string.Join(" ", errors));
        }
    }

    /// <summary>
    /// Normalizes a DAX query by standardizing whitespace and line endings.
    /// </summary>
    private static string NormalizeDAXQuery(string query)
    {
        // Replace all line endings with a standard form
        var normalized = System.Text.RegularExpressions.Regex.Replace(query, @"\r\n?|\n", "\n");
        // Normalize whitespace in keywords but preserve it inside strings
        normalized = NormalizeWhitespacePreservingStrings(normalized);
        return normalized.Trim(); // Trim leading/trailing whitespace from the whole query
    }

    /// <summary>
    /// Helper to normalize whitespace while preserving strings.
    /// Collapses multiple whitespace characters into a single space outside of strings.
    /// </summary>
    private static string NormalizeWhitespacePreservingStrings(string input)
    {
        var result = new System.Text.StringBuilder();
        bool inString = false;
        char stringDelimiter = '"'; // Can be ' or "
        bool lastCharWasWhitespace = false;

        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];

            if (!inString && (c == '"' || c == '\''))
            {
                // Check if this quote is an escape for a quote of the same type (e.g., '' in 'O''Reilly')
                // This is not standard DAX for identifiers but common in string literals.
                // DAX identifiers use 'TableName'[ColumnName] or 'Table Name'[Column Name]
                // DAX string literals use "string" or ""escaped string""
                if (c == '\'' && i + 1 < input.Length && input[i+1] == '\'')
                {
                    // This is an escaped single quote within a potential identifier if not already in a string.
                    // However, our primary goal here is to identify string literals "..."
                    // For '...', they are usually table/column names and don't typically contain escaped quotes this way.
                    // Let's assume ' only starts a string if it's not part of an identifier pattern.
                    // This simplification might need refinement for extremely complex DAX.
                    // The main string delimiters are double quotes.
                }


                if (c == '"') // Only double quotes define string literals for this normalization pass
                {
                    inString = true;
                    stringDelimiter = c;
                    result.Append(c);
                    lastCharWasWhitespace = false;
                    continue;
                }
            }
            else if (inString && c == stringDelimiter)
            {
                 // Handle DAX escaped double quote ("")
                if (c == '"' && i + 1 < input.Length && input[i+1] == '"')
                {
                    result.Append(c); // Append the first quote of the escape
                    result.Append(input[i+1]); // Append the second quote
                    i++; // Skip the next character as it's part of the escape
                    lastCharWasWhitespace = false;
                    continue;
                }
                inString = false;
                result.Append(c);
                lastCharWasWhitespace = false;
                continue;
            }

            if (inString)
            {
                result.Append(c);
                lastCharWasWhitespace = false;
            }
            else
            {
                if (char.IsWhiteSpace(c))
                {
                    if (!lastCharWasWhitespace)
                    {
                        result.Append(' ');
                        lastCharWasWhitespace = true;
                    }
                    // Skip if last char was already whitespace (collapse multiple)
                }
                else
                {
                    result.Append(c);
                    lastCharWasWhitespace = false;
                }
            }
        }
        return result.ToString();
    }


    /// <summary>
    /// Checks if delimiters like parentheses and brackets are properly balanced.
    /// Skips delimiters found within string literals.
    /// </summary>
    private static void CheckBalancedDelimiters(string query, char openChar, char closeChar, string delimiterName, List<string> errors)
    {
        int balance = 0;
        bool inString = false;
        char stringDelimiter = '\0'; // Can be ' or "

        for (int i = 0; i < query.Length; i++)
        {
            char c = query[i];

            if (inString)
            {
                if (c == stringDelimiter)
                {
                    // Handle DAX escaped double quote ("") or escaped single quote ('')
                    if (i + 1 < query.Length && query[i + 1] == stringDelimiter)
                    {
                        i++; // Skip next char
                    }
                    else
                    {
                        inString = false;
                        stringDelimiter = '\0';
                    }
                }
            }
            else
            {
                if (c == '"' || c == '\'')
                {
                    inString = true;
                    stringDelimiter = c;
                }
                else if (c == openChar)
                {
                    balance++;
                }
                else if (c == closeChar)
                {
                    balance--;
                    if (balance < 0)
                    {
                        errors.Add($"DAX query has unbalanced {delimiterName}: extra '{closeChar}' found.");
                        return; // Stop at first structural error for this type
                    }
                }
            }
        }

        if (balance > 0)
        {
            errors.Add($"DAX query has unbalanced {delimiterName}: {balance} '{openChar}' not closed.");
        }
        // If balance < 0, it would have been caught and returned earlier.
    }


    /// <summary>
    /// Checks if string delimiters (quotes) are properly balanced.
    /// DAX uses " for string literals and ' for table/column names (which can contain spaces).
    /// Escaped quotes ("" inside strings, '' inside identifiers though less common) are handled.
    /// </summary>
    private static void CheckBalancedQuotes(string query, List<string> errors)
    {
        bool inDoubleQuoteString = false;
        bool inSingleQuoteIdentifier = false; // For table/column names like 'My Table'

        for (int i = 0; i < query.Length; i++)
        {
            char c = query[i];

            if (c == '"')
            {
                if (inSingleQuoteIdentifier) continue; // Ignore " if inside '...'

                // Handle DAX escaped double quote ("")
                if (i + 1 < query.Length && query[i + 1] == '"')
                {
                    i++; // Skip the next quote as it's part of the escape
                }
                else
                {
                    inDoubleQuoteString = !inDoubleQuoteString;
                }
            }
            else if (c == '\'')
            {
                if (inDoubleQuoteString) continue; // Ignore ' if inside "..."

                // Single quotes in DAX are primarily for identifiers 'Table Name'[Column Name]
                // They are not typically escaped with '' inside themselves, but we can be lenient.
                // A simple toggle should suffice for basic validation.
                // More complex scenarios (e.g. a string literal containing a single quote for an identifier)
                // are tricky without a full parser.
                // This check assumes single quotes are for identifiers and not nested string literals.
                inSingleQuoteIdentifier = !inSingleQuoteIdentifier;
            }
        }

        if (inDoubleQuoteString)
        {
            errors.Add("DAX query has unbalanced double quotes: a string literal is not properly closed.");
        }
        if (inSingleQuoteIdentifier)
        {
            // This might be too strict if a single quote is legitimately at the end of a query part.
            // However, for identifiers, they should be balanced.
            errors.Add("DAX query has unbalanced single quotes: a table/column identifier might not be properly closed.");
        }
    }


    /// <summary>
    /// Constructs an EVALUATE statement based on the query and topN value.
    /// </summary>
    /// <param name="query">The core query expression.</param>
    /// <param name="topN">Maximum number of rows to return (default: 10).</param>
    /// <returns>The constructed EVALUATE statement.</returns>
    private static string ConstructEvaluateStatement(string query, int topN)
    {
        query = query.Trim();
        bool isCoreQueryTableExpr = query.StartsWith("'") || // Table constructor or table name
                                    query.StartsWith("SELECTCOLUMNS", StringComparison.OrdinalIgnoreCase) ||
                                    query.StartsWith("ADDCOLUMNS", StringComparison.OrdinalIgnoreCase) ||
                                    query.StartsWith("SUMMARIZE", StringComparison.OrdinalIgnoreCase) ||
                                    query.StartsWith("FILTER", StringComparison.OrdinalIgnoreCase) ||
                                    query.StartsWith("VALUES", StringComparison.OrdinalIgnoreCase) ||
                                    query.StartsWith("ALL", StringComparison.OrdinalIgnoreCase);

        if (isCoreQueryTableExpr)
        {
            return topN > 0 ? $"EVALUATE TOPN({topN}, {query})" : $"EVALUATE {query}";
        }
        else
        {
            return $"EVALUATE ROW(\"Value\", {query})";
        }
    }

}
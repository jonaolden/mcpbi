// File: Tools/ObjectRetrievalTools.cs
using System.ComponentModel;

using Microsoft.Extensions.Logging;

using ModelContextProtocol.Server;

using pbi_local_mcp.Core;

namespace pbi_local_mcp.Tools;

/// <summary>
/// Tools for retrieving metadata about semantic model objects (tables, measures, columns, etc.)
/// </summary>
[McpServerToolType]
public class ObjectRetrievalTools
{
    private readonly ITabularConnection _tabularConnection;
    private readonly ILogger<ObjectRetrievalTools> _logger;

    public ObjectRetrievalTools(ITabularConnection tabularConnection, ILogger<ObjectRetrievalTools> logger)
    {
        _tabularConnection = tabularConnection ?? throw new ArgumentNullException(nameof(tabularConnection));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // ========================================================================
    // CONSOLIDATED TOOLS
    // ========================================================================

    [McpServerTool, Description("List model objects with filtering by type, table, visibility, and description. Valid types: table, column, measure, hierarchy, calculation_group, calculation_item, relationship.")]
    public async Task<object> ListObjects(
        [Description("Object type (table|column|measure|hierarchy|calculation_group|calculation_item|relationship) or null for summary")] string? type = null,
        [Description("Filter by parent table name")] string? tableName = null,
        [Description("Filter by hidden status")] bool? isHidden = null,
        [Description("Filter by description presence")] bool? hasDescription = null,
        [Description("Include dependency count")] bool includeBasicDependencies = false)
    {
        try
        {
            // Validate connection before proceeding
            await _tabularConnection.ValidateConnectionAsync();

            _logger.LogDebug("ListObjects called with type={Type}, tableName={TableName}", type, tableName);

            if (!string.IsNullOrWhiteSpace(tableName) && !DaxSecurityUtils.IsValidIdentifier(tableName))
                throw new ArgumentException("Invalid table name format", nameof(tableName));

            // Early validation for type parameter
            if (!string.IsNullOrWhiteSpace(type))
            {
                var validTypes = new[] { "table", "column", "measure", "hierarchy",
                    "calculation_group", "calculationgroup", "calculation_item", "calculationitem", "relationship" };
                var normalizedInput = type.ToLowerInvariant().Trim();
                if (!validTypes.Contains(normalizedInput))
                {
                    throw new ArgumentException(
                        $"Invalid type '{type}'. Valid types: table, column, measure, hierarchy, calculation_group, calculation_item, relationship",
                        nameof(type));
                }
            }

            var normalizedType = type?.ToLowerInvariant().Trim();
            var results = new List<Dictionary<string, object?>>();

            results = normalizedType switch
            {
                null or "" => await ListAllObjectsSummary(),
                "table" => await ListTablesDetailed(isHidden, hasDescription, includeBasicDependencies),
                "column" => await ListColumnsDetailed(tableName, isHidden, hasDescription),
                "measure" => await ListMeasuresDetailed(tableName, isHidden, hasDescription, includeBasicDependencies),
                "hierarchy" => await ListHierarchiesDetailed(tableName, isHidden, hasDescription),
                "calculation_group" or "calculationgroup" => await ListCalculationGroupsDetailed(isHidden, hasDescription),
                "calculation_item" or "calculationitem" => await ListCalculationItemsDetailed(tableName, isHidden),
                "relationship" => await ListRelationshipsDetailed(tableName, isHidden),
                _ => throw new ArgumentException($"Unknown object type '{type}'. Valid types: table, column, measure, hierarchy, calculation_group, calculation_item, relationship", nameof(type))
            };

            return new
            {
                objects = results,
                totalCount = results.Count,
                filteredCount = results.Count,
                objectType = type ?? "mixed",
                filters = new { tableName, isHidden, hasDescription, includeBasicDependencies }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ListObjects with type={Type}", type);
            throw;
        }
    }

    [McpServerTool, Description("Get detailed information about a semantic model object by name and type. For columns/measures/calculation_items, tableName parameter is required.")]
    public async Task<object> GetObjectDetails(
        [Description("Object name")] string name,
        [Description("Object type: 'table', 'measure', 'column', 'relationship', 'calculation_group', 'calculation_item'")] string type,
        [Description("Parent table name (required for column, measure, calculation_item types)")] string? tableName = null,
        [Description("Dependency level: 'none' (default), 'direct' (immediate refs), 'full' (complete graph)")] string includeDependencies = "none",
        [Description("Include extended metadata and calculation items list")] bool includeMetadata = true)
    {
        try
        {
            // Validate connection before proceeding
            await _tabularConnection.ValidateConnectionAsync();

            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name cannot be null or empty", nameof(name));

            if (string.IsNullOrWhiteSpace(type))
                throw new ArgumentException("Type parameter is required. Valid types: 'table', 'measure', 'column', 'relationship', 'calculation_group', 'calculation_item'", nameof(type));

            var normalizedType = type.ToLowerInvariant().Trim();
            var depLevel = includeDependencies?.ToLowerInvariant().Trim() ?? "none";

            if (depLevel != "none" && depLevel != "direct" && depLevel != "full")
                throw new ArgumentException("includeDependencies must be 'none', 'direct', or 'full'", nameof(includeDependencies));

            // Validate that tableName is provided for types that require it
            if ((normalizedType == "column" || normalizedType == "measure" || normalizedType == "calculation_item")
                && string.IsNullOrWhiteSpace(tableName))
            {
                throw new ArgumentException($"tableName parameter is required for type '{type}'", nameof(tableName));
            }

            Dictionary<string, object?>? objectDetails = await GetObjectByName(name, normalizedType, tableName, includeMetadata);

            if (objectDetails == null)
            {
                throw new ArgumentException($"Object '{name}' of type '{type}' not found" +
                    (string.IsNullOrWhiteSpace(tableName) ? "" : $" in table '{tableName}'"));
            }

            if (depLevel != "none")
            {
                var dependencies = await GetObjectDependencies(objectDetails, depLevel == "full");
                objectDetails["dependencies"] = dependencies;
            }

            return objectDetails;
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Validation error in GetObjectDetails: {Message}", ex.Message);
            return new { error = ex.Message, parameter = ex.ParamName };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetObjectDetails for name={Name}, type={Type}", name, type);
            return new { error = $"Failed to retrieve object details: {ex.Message}" };
        }
    }

    // ========================================================================
    // RETAINED TOOLS - Not redundant with consolidated tools
    // ========================================================================


    [McpServerTool, Description("List DAX functions by category.")]
    public async Task<object> ListFunctions(
        [Description("Function category (e.g., DATETIME, LOGICAL)")] string interfaceName)
    {
        try
        {
            // Validate connection before proceeding
            await _tabularConnection.ValidateConnectionAsync();

            if (string.IsNullOrWhiteSpace(interfaceName))
                throw new ArgumentException("interfaceName is required", nameof(interfaceName));

            if (!DaxSecurityUtils.IsValidIdentifier(interfaceName))
                throw new ArgumentException("Invalid interfaceName format", nameof(interfaceName));

            var escapedInterfaceName = interfaceName.Replace("\"", "\"\"");
            var quotedInterfaceName = $"\"{escapedInterfaceName}\"";

            var daxQuery = $@"EVALUATE
                SELECTCOLUMNS(
                    FILTER(INFO.FUNCTIONS(), [INTERFACE_NAME] = {quotedInterfaceName}),
                    ""FUNCTION_NAME"", [FUNCTION_NAME],
                    ""DESCRIPTION"", [DESCRIPTION]
                )";

            object? rawResult;
            try
            {
                rawResult = await _tabularConnection.ExecAsync(daxQuery, QueryType.DAX);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute INFO.FUNCTIONS() query for interfaceName: {InterfaceName}", interfaceName);
                throw new Exception($"Failed to list functions for interface '{interfaceName}'. The interface may not exist or DAX query execution failed: {ex.Message}", ex);
            }

            var results = (rawResult as IEnumerable<Dictionary<string, object?>>)?.ToList() ?? new List<Dictionary<string, object?>>();

            // Transform results to remove brackets from column names
            var transformedResults = results.Select(row =>
            {
                var newRow = new Dictionary<string, object?>();
                foreach (var kvp in row)
                {
                    var cleanKey = kvp.Key.TrimStart('[').TrimEnd(']');
                    newRow[cleanKey] = kvp.Value;
                }
                return newRow;
            }).ToList();

            // Wrap in expected format for test compatibility
            return new Dictionary<string, object?>
            {
                ["functions"] = transformedResults,
                ["totalCount"] = transformedResults.Count,
                ["filteredCount"] = transformedResults.Count,
                ["filters"] = new Dictionary<string, object?>
                {
                    ["interfaceName"] = interfaceName
                }
            };
        }
        catch (ArgumentException)
        {
            // Re-throw ArgumentException without wrapping
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ListFunctions with interfaceName: {InterfaceName}", interfaceName);
            throw new Exception($"Failed to list functions: {ex.Message}", ex);
        }
    }

    [McpServerTool, Description("Get DAX function details and parameters.")]
    public async Task<object> GetFunctionDetails(
        [Description("Function name")] string functionName)
    {
        try
        {
            // Validate connection before proceeding
            await _tabularConnection.ValidateConnectionAsync();

            if (string.IsNullOrWhiteSpace(functionName))
                throw new ArgumentException("Function name cannot be null or empty", nameof(functionName));

            if (!DaxSecurityUtils.IsValidIdentifier(functionName))
                throw new ArgumentException("Invalid function name format", nameof(functionName));

            var escapedFunctionName = functionName.Replace("'", "''");
            var dmvQuery = $"SELECT * FROM $SYSTEM.MDSCHEMA_FUNCTIONS WHERE FUNCTION_NAME = '{escapedFunctionName}'";

            object? rawResult;
            try
            {
                rawResult = await _tabularConnection.ExecAsync(dmvQuery, QueryType.DMV);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "DMV query failed for function: {FunctionName}", functionName);
                throw new ArgumentException($"Failed to retrieve function details for '{functionName}'. The function may not exist or DMV access may be restricted.", nameof(functionName), ex);
            }

            var resultList = (rawResult as IEnumerable<Dictionary<string, object?>>)?.ToList();

            if (resultList == null || !resultList.Any())
                throw new ArgumentException($"Function '{functionName}' not found in the model", nameof(functionName));

            // Transform and wrap the result to match test expectations
            var transformedResults = resultList.Select(dict =>
            {
                var transformed = new Dictionary<string, object?>();

                // Add lowercase 'name' field from FUNCTION_NAME
                if (dict.ContainsKey("FUNCTION_NAME"))
                {
                    transformed["name"] = dict["FUNCTION_NAME"];
                }

                // Transform PARAMETER_LIST string into parameters array
                if (dict.ContainsKey("PARAMETER_LIST"))
                {
                    var paramList = dict["PARAMETER_LIST"]?.ToString() ?? "";
                    // Split by comma and clean up parameters
                    var parameters = string.IsNullOrWhiteSpace(paramList)
                        ? new List<string>()
                        : paramList.Split(',').Select(p => p.Trim()).Where(p => !string.IsNullOrEmpty(p)).ToList();
                    transformed["parameters"] = parameters;
                }
                else
                {
                    transformed["parameters"] = new List<string>();
                }

                // Add description
                if (dict.ContainsKey("DESCRIPTION"))
                {
                    transformed["description"] = dict["DESCRIPTION"];
                }

                // Create syntax field
                var funcName = dict.ContainsKey("FUNCTION_NAME") ? dict["FUNCTION_NAME"]?.ToString() : "FUNCTION";
                var paramListForSyntax = dict.ContainsKey("PARAMETER_LIST") ? dict["PARAMETER_LIST"]?.ToString() : "";
                transformed["syntax"] = $"{funcName}({paramListForSyntax})";

                // Copy any additional fields
                foreach (var kvp in dict)
                {
                    if (!transformed.ContainsKey(kvp.Key.ToLowerInvariant()))
                    {
                        transformed[kvp.Key] = kvp.Value;
                    }
                }

                return transformed;
            }).ToList();

            return transformedResults.Count == 1 ? transformedResults[0] : transformedResults;
        }
        catch (ArgumentException)
        {
            // Re-throw ArgumentException without wrapping
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetFunctionDetails for function: {FunctionName}", functionName);
            throw new Exception($"Failed to get details for function '{functionName}': {ex.Message}", ex);
        }
    }

    // ========================================================================
    // PRIVATE HELPER METHODS - Object Type Handlers
    // ========================================================================

    private async Task<List<Dictionary<string, object?>>> ListAllObjectsSummary()
    {
        var summary = new List<Dictionary<string, object?>>();

        var tables = await _tabularConnection.ExecAsync("EVALUATE INFO.VIEW.TABLES()", QueryType.DAX) as IEnumerable<Dictionary<string, object?>>;
        var measures = await _tabularConnection.ExecAsync("EVALUATE INFO.VIEW.MEASURES()", QueryType.DAX) as IEnumerable<Dictionary<string, object?>>;
        var columns = await _tabularConnection.ExecAsync("EVALUATE INFO.VIEW.COLUMNS()", QueryType.DAX) as IEnumerable<Dictionary<string, object?>>;
        var relationships = await _tabularConnection.ExecAsync("EVALUATE INFO.VIEW.RELATIONSHIPS()", QueryType.DAX) as IEnumerable<Dictionary<string, object?>>;

        var tablesList = tables?.ToList() ?? new List<Dictionary<string, object?>>();
        int regularTableCount = tablesList.Count;
        int calcGroupCount = 0;

        // Query calculation groups to distinguish them from regular tables
        try
        {
            var cgQuery = "SELECT * FROM $SYSTEM.TMSCHEMA_CALCULATION_GROUPS";
            var cgResult = await _tabularConnection.ExecAsync(cgQuery, QueryType.DMV);
            var calcGroups = (cgResult as IEnumerable<Dictionary<string, object?>>)?.ToList() ?? new List<Dictionary<string, object?>>();

            if (calcGroups.Any())
            {
                var tablesQueryDmv = "SELECT * FROM $SYSTEM.TMSCHEMA_TABLES";
                var tablesResult = await _tabularConnection.ExecAsync(tablesQueryDmv, QueryType.DMV);
                var tablesDmv = (tablesResult as IEnumerable<Dictionary<string, object?>>)?.ToList() ?? new List<Dictionary<string, object?>>();

                // Get IDs of tables that are calculation groups
                var calcGroupTableIds = (from cg in calcGroups
                                         select cg.GetValueOrDefault("TableID")?.ToString())
                                        .Where(id => !string.IsNullOrEmpty(id))
                                        .ToHashSet();

                // Join INFO.VIEW.TABLES with DMV tables to get LineageTags
                var calcGroupLineageTags = (from t in tablesDmv
                                            where calcGroupTableIds.Contains(t.GetValueOrDefault("ID")?.ToString() ?? "")
                                            select t.GetValueOrDefault("LineageTag")?.ToString())
                                           .Where(lt => !string.IsNullOrEmpty(lt))
                                           .ToHashSet();

                // Count calculation groups and subtract from total table count
                calcGroupCount = calcGroupLineageTags.Count;
                regularTableCount = tablesList.Count - calcGroupCount;

                _logger.LogDebug("Found {TotalTables} tables in INFO.VIEW, {CalcGroups} are calculation groups, {RegularTables} are regular tables",
                    tablesList.Count, calcGroupCount, regularTableCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Calculation groups not available in this model, all tables are regular tables");
        }

        summary.Add(new Dictionary<string, object?> { ["objectType"] = "table", ["count"] = regularTableCount });
        summary.Add(new Dictionary<string, object?> { ["objectType"] = "measure", ["count"] = measures?.Count() ?? 0 });
        summary.Add(new Dictionary<string, object?> { ["objectType"] = "column", ["count"] = columns?.Count() ?? 0 });
        summary.Add(new Dictionary<string, object?> { ["objectType"] = "relationship", ["count"] = relationships?.Count() ?? 0 });
        
        // Add calculation_group with integrated calculation_item count
        var calcItemCount = 0;
        try
        {
            if (calcGroupCount > 0)
            {
                var calcItems = await _tabularConnection.ExecAsync("SELECT COUNT(*) AS Count FROM $SYSTEM.TMSCHEMA_CALCULATION_ITEMS", QueryType.DMV) as IEnumerable<Dictionary<string, object?>>;
                var ciCount = calcItems?.FirstOrDefault()?.GetValueOrDefault("Count");
                calcItemCount = ciCount != null ? Convert.ToInt32(ciCount) : 0;
                _logger.LogDebug("Found {CalcItemCount} calculation items across {CalcGroupCount} calculation groups", calcItemCount, calcGroupCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Calculation items not available in this model");
        }
        
        summary.Add(new Dictionary<string, object?> {
            ["objectType"] = "calculation_group",
            ["count"] = calcGroupCount,
            ["calculationItemCount"] = calcItemCount
        });
        
        // Also add calculation_item as separate entry for backward compatibility and filtering
        summary.Add(new Dictionary<string, object?> { ["objectType"] = "calculation_item", ["count"] = calcItemCount });

        return summary;
    }

    private async Task<List<Dictionary<string, object?>>> ListTablesDetailed(bool? isHidden, bool? hasDescription, bool includeBasicDependencies)
    {
        var result = await _tabularConnection.ExecAsync("EVALUATE INFO.VIEW.TABLES()", QueryType.DAX);
        var tables = (result as IEnumerable<Dictionary<string, object?>>)?.ToList() ?? new List<Dictionary<string, object?>>();

        // Exclude calculation groups from table listing
        try
        {
            var cgQuery = "SELECT * FROM $SYSTEM.TMSCHEMA_CALCULATION_GROUPS";
            var cgResult = await _tabularConnection.ExecAsync(cgQuery, QueryType.DMV);
            var calcGroups = (cgResult as IEnumerable<Dictionary<string, object?>>)?.ToList() ?? new List<Dictionary<string, object?>>();

            if (calcGroups.Any())
            {
                var tablesQueryDmv = "SELECT * FROM $SYSTEM.TMSCHEMA_TABLES";
                var tablesResult = await _tabularConnection.ExecAsync(tablesQueryDmv, QueryType.DMV);
                var tablesDmv = (tablesResult as IEnumerable<Dictionary<string, object?>>)?.ToList() ?? new List<Dictionary<string, object?>>();

                // Get IDs of tables that are calculation groups
                var calcGroupTableIds = (from cg in calcGroups
                                         select cg.GetValueOrDefault("TableID")?.ToString())
                                        .Where(id => !string.IsNullOrEmpty(id))
                                        .ToHashSet();

                // Get LineageTags of calculation group tables
                var calcGroupLineageTags = (from t in tablesDmv
                                            where calcGroupTableIds.Contains(t.GetValueOrDefault("ID")?.ToString() ?? "")
                                            select t.GetValueOrDefault("LineageTag")?.ToString())
                                           .Where(lt => !string.IsNullOrEmpty(lt))
                                           .ToHashSet();

                // Filter out calculation groups from tables list
                tables = tables.Where(t =>
                {
                    var lineageTag = GetInfoViewValue(t, "LineageTag", "TABLES")?.ToString();
                    return !calcGroupLineageTags.Contains(lineageTag);
                }).ToList();

                _logger.LogDebug("Filtered out {Count} calculation groups from table listing", calcGroupLineageTags.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not filter calculation groups, returning all tables");
        }

        if (isHidden.HasValue)
            tables = tables.Where(t => t.TryGetValue("IsHidden", out var hidden) && hidden != null && Convert.ToBoolean(hidden) == isHidden.Value).ToList();

        if (hasDescription.HasValue)
            tables = tables.Where(t => (t.TryGetValue("Description", out var desc) && !string.IsNullOrWhiteSpace(desc?.ToString())) == hasDescription.Value).ToList();

        return tables.Select(t => new Dictionary<string, object?>
        {
            ["lineageTag"] = GetInfoViewValue(t, "LineageTag", "TABLES"),
            ["name"] = GetInfoViewValue(t, "Name", "TABLES"),
            ["type"] = "table",
            ["isHidden"] = GetInfoViewValue(t, "IsHidden", "TABLES"),
            ["description"] = GetInfoViewValue(t, "Description", "TABLES"),
            ["dataCategory"] = GetInfoViewValue(t, "DataCategory", "TABLES")
        }).ToList();
    }

    private async Task<List<Dictionary<string, object?>>> ListColumnsDetailed(string? tableName, bool? isHidden, bool? hasDescription)
    {
        string daxQuery = !string.IsNullOrEmpty(tableName)
            ? $"EVALUATE FILTER(INFO.VIEW.COLUMNS(), [Table] = \"{tableName.Replace("\"", "\"\"")}\")"
            : "EVALUATE INFO.VIEW.COLUMNS()";

        var result = await _tabularConnection.ExecAsync(daxQuery, QueryType.DAX);
        var columns = (result as IEnumerable<Dictionary<string, object?>>)?.ToList() ?? new List<Dictionary<string, object?>>();

        if (isHidden.HasValue)
            columns = columns.Where(c => c.TryGetValue("IsHidden", out var hidden) && hidden != null && Convert.ToBoolean(hidden) == isHidden.Value).ToList();

        if (hasDescription.HasValue)
            columns = columns.Where(c => (c.TryGetValue("Description", out var desc) && !string.IsNullOrWhiteSpace(desc?.ToString())) == hasDescription.Value).ToList();

        return columns.Select(c => new Dictionary<string, object?>
        {
            ["lineageTag"] = GetInfoViewValue(c, "LineageTag", "COLUMNS"),
            ["name"] = GetInfoViewValue(c, "Name", "COLUMNS"),
            ["type"] = "column",
            ["table"] = GetInfoViewValue(c, "Table", "COLUMNS"),
            ["dataType"] = GetInfoViewValue(c, "DataType", "COLUMNS"),
            ["isHidden"] = GetInfoViewValue(c, "IsHidden", "COLUMNS"),
            ["description"] = GetInfoViewValue(c, "Description", "COLUMNS")
        }).ToList();
    }

    private async Task<List<Dictionary<string, object?>>> ListMeasuresDetailed(string? tableName, bool? isHidden, bool? hasDescription, bool includeBasicDependencies)
    {
        string daxQuery = !string.IsNullOrEmpty(tableName)
            ? $"EVALUATE FILTER(INFO.VIEW.MEASURES(), [Table] = \"{tableName.Replace("\"", "\"\"")}\")"
            : "EVALUATE INFO.VIEW.MEASURES()";

        var result = await _tabularConnection.ExecAsync(daxQuery, QueryType.DAX);
        var measures = (result as IEnumerable<Dictionary<string, object?>>)?.ToList() ?? new List<Dictionary<string, object?>>();

        if (isHidden.HasValue)
            measures = measures.Where(m => m.TryGetValue("IsHidden", out var hidden) && hidden != null && Convert.ToBoolean(hidden) == isHidden.Value).ToList();

        if (hasDescription.HasValue)
            measures = measures.Where(m => (m.TryGetValue("Description", out var desc) && !string.IsNullOrWhiteSpace(desc?.ToString())) == hasDescription.Value).ToList();

        return measures.Select(m =>
        {
            var expression = GetInfoViewValue(m, "Expression", "MEASURES")?.ToString() ?? "";
            var expressionPreview = expression.Length > 100 ? expression.Substring(0, 100) : expression;

            var result = new Dictionary<string, object?>
            {
                ["lineageTag"] = GetInfoViewValue(m, "LineageTag", "MEASURES"),
                ["name"] = GetInfoViewValue(m, "Name", "MEASURES"),
                ["type"] = "measure",
                ["table"] = GetInfoViewValue(m, "Table", "MEASURES"),
                ["dataType"] = GetInfoViewValue(m, "DataType", "MEASURES"),
                ["isHidden"] = GetInfoViewValue(m, "IsHidden", "MEASURES"),
                ["expressionPreview"] = expressionPreview,
                ["description"] = GetInfoViewValue(m, "Description", "MEASURES")
            };

            // Add basicDependencies if requested
            if (includeBasicDependencies)
            {
                // Extract basic references from expression
                var refs = new List<string>();
                if (!string.IsNullOrEmpty(expression))
                {
                    var matches = System.Text.RegularExpressions.Regex.Matches(expression, @"'([^']+)'\[([^\]]+)\]|\[([^\]]+)\]");
                    foreach (System.Text.RegularExpressions.Match match in matches)
                    {
                        if (match.Groups[1].Success && match.Groups[2].Success)
                            refs.Add($"'{match.Groups[1].Value}'[{match.Groups[2].Value}]");
                        else if (match.Groups[3].Success)
                            refs.Add($"[{match.Groups[3].Value}]");
                    }
                }
                result["basicDependencies"] = refs.Distinct().Take(10).ToList(); // Limit to 10 for performance
            }

            return result;
        }).ToList();
    }

    private async Task<List<Dictionary<string, object?>>> ListHierarchiesDetailed(string? tableName, bool? isHidden, bool? hasDescription)
    {
        try
        {
            // Try the standard query with HierarchyName column
            string daxQuery = !string.IsNullOrEmpty(tableName)
                ? $"EVALUATE FILTER(INFO.VIEW.COLUMNS(), [Table] = \"{tableName.Replace("\"", "\"\"")}\" && NOT(ISBLANK([HierarchyName])))"
                : "EVALUATE FILTER(INFO.VIEW.COLUMNS(), NOT(ISBLANK([HierarchyName])))";

            var result = await _tabularConnection.ExecAsync(daxQuery, QueryType.DAX);
            var hierarchies = (result as IEnumerable<Dictionary<string, object?>>)?.ToList() ?? new List<Dictionary<string, object?>>();

            return hierarchies.GroupBy(h => new { HierarchyName = GetInfoViewValue(h, "HierarchyName", "COLUMNS")?.ToString() ?? "", Table = GetInfoViewValue(h, "Table", "COLUMNS")?.ToString() ?? "" })
                .Select(g => g.First())
                .Select(h => new Dictionary<string, object?>
                {
                    ["lineageTag"] = GetInfoViewValue(h, "LineageTag", "COLUMNS"),
                    ["name"] = GetInfoViewValue(h, "HierarchyName", "COLUMNS"),
                    ["type"] = "hierarchy",
                    ["table"] = GetInfoViewValue(h, "Table", "COLUMNS"),
                    ["isHidden"] = GetInfoViewValue(h, "IsHidden", "COLUMNS")
                })
                .ToList();
        }
        catch (Exception ex)
        {
            // HierarchyName column doesn't exist in Power BI Desktop's DMV schema
            // Return empty list gracefully
            _logger.LogDebug(ex, "HierarchyName column not available in this model (likely Power BI Desktop). Returning empty hierarchy list.");
            return new List<Dictionary<string, object?>>();
        }
    }

    private async Task<List<Dictionary<string, object?>>> ListCalculationGroupsDetailed(bool? isHidden, bool? hasDescription)
    {
        try
        {
            // DMV does not support JOINs or column selection, so we must query separately and join in C#
            var cgQuery = "SELECT * FROM $SYSTEM.TMSCHEMA_CALCULATION_GROUPS";
            var tablesQuery = "SELECT * FROM $SYSTEM.TMSCHEMA_TABLES";

            _logger.LogDebug("Querying calculation groups and tables for C# join");

            var cgResult = await _tabularConnection.ExecAsync(cgQuery, QueryType.DMV);
            var calcGroups = (cgResult as IEnumerable<Dictionary<string, object?>>)?.ToList() ?? new List<Dictionary<string, object?>>();

            var tablesResult = await _tabularConnection.ExecAsync(tablesQuery, QueryType.DMV);
            var tables = (tablesResult as IEnumerable<Dictionary<string, object?>>)?.ToList() ?? new List<Dictionary<string, object?>>();

            _logger.LogDebug("Found {CGCount} calculation groups and {TableCount} tables", calcGroups.Count, tables.Count);

            // Perform in-memory join: match calculation groups with their corresponding tables
            var joined = from cg in calcGroups
                         join t in tables on cg.GetValueOrDefault("TableID")?.ToString() equals t.GetValueOrDefault("ID")?.ToString()
                         select new Dictionary<string, object?>
                         {
                             ["lineageTag"] = t.GetValueOrDefault("LineageTag"),
                             ["name"] = t.GetValueOrDefault("Name"),
                             ["type"] = "calculation_group",
                             ["isHidden"] = t.GetValueOrDefault("IsHidden"),
                             ["description"] = cg.GetValueOrDefault("Description"),  // Description from calc group
                             ["precedence"] = cg.GetValueOrDefault("Precedence")
                         };

            var result = joined.ToList();

            // Apply filters after join
            if (isHidden.HasValue)
                result = result.Where(cg =>
                    cg.TryGetValue("isHidden", out var hidden) &&
                    hidden != null &&
                    Convert.ToBoolean(hidden) == isHidden.Value).ToList();

            if (hasDescription.HasValue)
                result = result.Where(cg =>
                    (cg.TryGetValue("description", out var desc) &&
                     !string.IsNullOrWhiteSpace(desc?.ToString())) == hasDescription.Value).ToList();

            _logger.LogDebug("Returning {Count} calculation groups after filtering", result.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error listing calculation groups, returning empty list");
            return new List<Dictionary<string, object?>>();
        }
    }

    private async Task<List<Dictionary<string, object?>>> ListCalculationItemsDetailed(string? tableName, bool? isHidden)
    {
        try
        {
            // DMV does not support JOINs, so query separately and join in C#
            var ciQuery = "SELECT * FROM $SYSTEM.TMSCHEMA_CALCULATION_ITEMS";
            var cgQuery = "SELECT * FROM $SYSTEM.TMSCHEMA_CALCULATION_GROUPS";
            var tablesQuery = "SELECT * FROM $SYSTEM.TMSCHEMA_TABLES";

            _logger.LogDebug("Querying calculation items, groups, and tables for C# join");

            var ciResult = await _tabularConnection.ExecAsync(ciQuery, QueryType.DMV);
            var calcItems = (ciResult as IEnumerable<Dictionary<string, object?>>)?.ToList() ?? new List<Dictionary<string, object?>>();

            var cgResult = await _tabularConnection.ExecAsync(cgQuery, QueryType.DMV);
            var calcGroups = (cgResult as IEnumerable<Dictionary<string, object?>>)?.ToList() ?? new List<Dictionary<string, object?>>();

            var tablesResult = await _tabularConnection.ExecAsync(tablesQuery, QueryType.DMV);
            var tables = (tablesResult as IEnumerable<Dictionary<string, object?>>)?.ToList() ?? new List<Dictionary<string, object?>>();

            // Create lookup for calc group ID to table name
            var cgIdToName = (from cg in calcGroups
                              join t in tables on cg.GetValueOrDefault("TableID")?.ToString() equals t.GetValueOrDefault("ID")?.ToString()
                              select new { CGID = cg.GetValueOrDefault("ID")?.ToString(), Name = t.GetValueOrDefault("Name")?.ToString() })
                             .ToDictionary(x => x.CGID ?? "", x => x.Name ?? "");

            // Join calculation items with their group names
            var joined = calcItems.Select(ci =>
            {
                var cgId = ci.GetValueOrDefault("CalculationGroupID")?.ToString() ?? "";
                var groupName = cgIdToName.TryGetValue(cgId, out var name) ? name : null;

                var expression = ci.GetValueOrDefault("Expression")?.ToString() ?? "";
                var expressionPreview = expression.Length > 100 ? expression.Substring(0, 100) : expression;

                return new Dictionary<string, object?>
                {
                    ["lineageTag"] = ci.GetValueOrDefault("LineageTag"),
                    ["name"] = ci.GetValueOrDefault("Name"),
                    ["type"] = "calculation_item",
                    ["calculationGroup"] = groupName,
                    ["ordinal"] = ci.GetValueOrDefault("Ordinal"),
                    ["expressionPreview"] = expressionPreview,
                    ["description"] = ci.GetValueOrDefault("Description")
                };
            }).ToList();

            // Apply filters
            if (!string.IsNullOrEmpty(tableName))
                joined = joined.Where(ci =>
                    ci.TryGetValue("calculationGroup", out var cgName) &&
                    cgName?.ToString() == tableName).ToList();

            _logger.LogDebug("Returning {Count} calculation items after filtering", joined.Count);
            return joined;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error listing calculation items, returning empty list");
            return new List<Dictionary<string, object?>>();
        }
    }

    private async Task<List<Dictionary<string, object?>>> ListRelationshipsDetailed(string? tableName, bool? isHidden)
    {
        string daxQuery = !string.IsNullOrEmpty(tableName)
            ? $"EVALUATE FILTER(INFO.VIEW.RELATIONSHIPS(), [FromTable] = \"{tableName.Replace("\"", "\"\"")}\" || [ToTable] = \"{tableName.Replace("\"", "\"\"")}\")"
            : "EVALUATE INFO.VIEW.RELATIONSHIPS()";

        var result = await _tabularConnection.ExecAsync(daxQuery, QueryType.DAX);
        var relationships = (result as IEnumerable<Dictionary<string, object?>>)?.ToList() ?? new List<Dictionary<string, object?>>();

        return relationships.Select(r => new Dictionary<string, object?>
        {
            ["type"] = "relationship",
            ["fromTable"] = GetInfoViewValue(r, "FromTable", "RELATIONSHIPS"),
            ["fromColumn"] = GetInfoViewValue(r, "FromColumn", "RELATIONSHIPS"),
            ["toTable"] = GetInfoViewValue(r, "ToTable", "RELATIONSHIPS"),
            ["toColumn"] = GetInfoViewValue(r, "ToColumn", "RELATIONSHIPS")
        }).ToList();
    }

    private async Task<Dictionary<string, object?>?> GetObjectByName(string name, string type, string? tableName, bool includeMetadata)
    {
        var escapedName = $"\"{name.Replace("\"", "\"\"")}\"";

        if (type == "table")
        {
            var query = $"EVALUATE FILTER(INFO.VIEW.TABLES(), [Name] = {escapedName})";

            _logger.LogDebug("GetObjectByName: Executing query for table '{Name}': {Query}", name, query);
            var result = await _tabularConnection.ExecAsync(query, QueryType.DAX);
            var tables = (result as IEnumerable<Dictionary<string, object?>>)?.ToList();
            _logger.LogDebug("GetObjectByName: Query returned {Count} results", tables?.Count ?? 0);

            if (tables != null && tables.Any())
            {
                var table = tables.First();
                var tableResult = new Dictionary<string, object?>
                {
                    ["lineageTag"] = GetInfoViewValue(table, "LineageTag", "TABLES"),
                    ["name"] = GetInfoViewValue(table, "Name", "TABLES"),
                    ["type"] = "table",
                    ["isHidden"] = GetInfoViewValue(table, "IsHidden", "TABLES"),
                    ["description"] = GetInfoViewValue(table, "Description", "TABLES"),
                    ["dataCategory"] = GetInfoViewValue(table, "DataCategory", "TABLES")
                };

                // Add metadata if requested
                if (includeMetadata)
                {
                    tableResult["metadata"] = new Dictionary<string, object?>
                    {
                        ["source"] = "INFO.VIEW.TABLES()",
                        ["retrievedAt"] = DateTime.UtcNow,
                        ["hasDescription"] = !string.IsNullOrWhiteSpace(tableResult["description"]?.ToString())
                    };
                }

                return tableResult;
            }
        }
        else if (type == "measure")
        {
            var query = string.IsNullOrEmpty(tableName)
                ? $"EVALUATE FILTER(INFO.VIEW.MEASURES(), [Name] = {escapedName})"
                : $"EVALUATE FILTER(INFO.VIEW.MEASURES(), [Name] = {escapedName} && [Table] = \"{tableName.Replace("\"", "\"\"")}\")";

            _logger.LogDebug("GetObjectByName: Executing query for measure '{Name}' in table '{TableName}': {Query}", name, tableName ?? "(all)", query);
            var result = await _tabularConnection.ExecAsync(query, QueryType.DAX);
            var measures = (result as IEnumerable<Dictionary<string, object?>>)?.ToList();
            _logger.LogDebug("GetObjectByName: Query returned {Count} results", measures?.Count ?? 0);

            if (measures != null && measures.Any())
            {
                var measure = measures.First();
                return new Dictionary<string, object?>
                {
                    ["lineageTag"] = GetInfoViewValue(measure, "LineageTag", "MEASURES"),
                    ["name"] = GetInfoViewValue(measure, "Name", "MEASURES"),
                    ["type"] = "measure",
                    ["table"] = GetInfoViewValue(measure, "Table", "MEASURES"),
                    ["expression"] = GetInfoViewValue(measure, "Expression", "MEASURES"),
                    ["dataType"] = GetInfoViewValue(measure, "DataType", "MEASURES"),
                    ["isHidden"] = GetInfoViewValue(measure, "IsHidden", "MEASURES"),
                    ["description"] = GetInfoViewValue(measure, "Description", "MEASURES")
                };
            }
        }
        else if (type == "calculation_group")
        {
            try
            {
                // Need to join with TABLES to get LineageTag
                var cgQuery = "SELECT * FROM $SYSTEM.TMSCHEMA_CALCULATION_GROUPS";
                var tablesQuery = "SELECT * FROM $SYSTEM.TMSCHEMA_TABLES";

                var cgResult = await _tabularConnection.ExecAsync(cgQuery, QueryType.DMV);
                var calcGroups = (cgResult as IEnumerable<Dictionary<string, object?>>)?.ToList() ?? new List<Dictionary<string, object?>>();

                var tablesResult = await _tabularConnection.ExecAsync(tablesQuery, QueryType.DMV);
                var tables = (tablesResult as IEnumerable<Dictionary<string, object?>>)?.ToList() ?? new List<Dictionary<string, object?>>();

                // Find the calculation group by name and join with table to get LineageTag
                var joined = from cg in calcGroups
                             join t in tables on cg.GetValueOrDefault("TableID")?.ToString() equals t.GetValueOrDefault("ID")?.ToString()
                             where t.GetValueOrDefault("Name")?.ToString() == name
                             select new Dictionary<string, object?>
                             {
                                 ["lineageTag"] = t.GetValueOrDefault("LineageTag"),
                                 ["name"] = t.GetValueOrDefault("Name"),
                                 ["type"] = "calculation_group",
                                 ["isHidden"] = t.GetValueOrDefault("IsHidden"),
                                 ["description"] = cg.GetValueOrDefault("Description"),
                                 ["precedence"] = cg.GetValueOrDefault("Precedence")
                             };

                var result = joined.FirstOrDefault();
                if (result != null)
                {
                    // Add calculation items if includeMetadata is true
                    if (includeMetadata)
                    {
                        try
                        {
                            // Use ListCalculationItemsDetailed which we know works
                            var calcItemsList = await ListCalculationItemsDetailed(name, null);

                            // Map items to match expected format
                            result["calculationItems"] = calcItemsList.Select(ci => new Dictionary<string, object?>
                            {
                                ["lineageTag"] = ci.GetValueOrDefault("lineageTag"),
                                ["name"] = ci.GetValueOrDefault("name"),
                                ["ordinal"] = ci.GetValueOrDefault("ordinal"),
                                ["expression"] = ci.GetValueOrDefault("expressionPreview"),
                                ["description"] = ci.GetValueOrDefault("description"),
                                ["calculationGroup"] = ci.GetValueOrDefault("calculationGroup")
                            }).ToList();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "Failed to retrieve calculation items for group: {Name}", name);
                            result["calculationItems"] = new List<Dictionary<string, object?>>();
                        }
                    }

                    return result;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Calculation group lookup failed for name: {Name}", name);
            }
        }
        else if (type == "column")
        {
            var query = string.IsNullOrEmpty(tableName)
                ? $"EVALUATE FILTER(INFO.VIEW.COLUMNS(), [Name] = {escapedName})"
                : $"EVALUATE FILTER(INFO.VIEW.COLUMNS(), [Name] = {escapedName} && [Table] = \"{tableName.Replace("\"", "\"\"")}\")";

            _logger.LogDebug("GetObjectByName: Executing query for column '{Name}' in table '{TableName}': {Query}", name, tableName ?? "(all)", query);
            var result = await _tabularConnection.ExecAsync(query, QueryType.DAX);
            var columns = (result as IEnumerable<Dictionary<string, object?>>)?.ToList();
            _logger.LogDebug("GetObjectByName: Query returned {Count} results", columns?.Count ?? 0);

            if (columns != null && columns.Any())
            {
                var column = columns.First();
                return new Dictionary<string, object?>
                {
                    ["lineageTag"] = GetInfoViewValue(column, "LineageTag", "COLUMNS"),
                    ["name"] = GetInfoViewValue(column, "Name", "COLUMNS"),
                    ["type"] = "column",
                    ["table"] = GetInfoViewValue(column, "Table", "COLUMNS"),
                    ["dataType"] = GetInfoViewValue(column, "DataType", "COLUMNS"),
                    ["isHidden"] = GetInfoViewValue(column, "IsHidden", "COLUMNS"),
                    ["description"] = GetInfoViewValue(column, "Description", "COLUMNS")
                };
            }
        }
        else if (type == "relationship")
        {
            var query = "EVALUATE INFO.VIEW.RELATIONSHIPS()";

            _logger.LogDebug("GetObjectByName: Executing query for relationship '{Name}': {Query}", name, query);
            var result = await _tabularConnection.ExecAsync(query, QueryType.DAX);
            var relationships = (result as IEnumerable<Dictionary<string, object?>>)?.ToList();

            if (relationships != null)
            {
                // For relationships, try to match by name or by a combination of from/to tables
                var relationship = relationships.FirstOrDefault(r =>
                    GetInfoViewValue(r, "Name", "RELATIONSHIPS")?.ToString() == name ||
                    (GetInfoViewValue(r, "FromTable", "RELATIONSHIPS")?.ToString()?.Contains(name, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (GetInfoViewValue(r, "ToTable", "RELATIONSHIPS")?.ToString()?.Contains(name, StringComparison.OrdinalIgnoreCase) ?? false));

                if (relationship != null)
                {
                    return new Dictionary<string, object?>
                    {
                        ["lineageTag"] = GetInfoViewValue(relationship, "LineageTag", "RELATIONSHIPS"),
                        ["name"] = GetInfoViewValue(relationship, "Name", "RELATIONSHIPS"),
                        ["type"] = "relationship",
                        ["fromTable"] = GetInfoViewValue(relationship, "FromTable", "RELATIONSHIPS"),
                        ["fromColumn"] = GetInfoViewValue(relationship, "FromColumn", "RELATIONSHIPS"),
                        ["toTable"] = GetInfoViewValue(relationship, "ToTable", "RELATIONSHIPS"),
                        ["toColumn"] = GetInfoViewValue(relationship, "ToColumn", "RELATIONSHIPS"),
                        ["crossFilterDirection"] = GetInfoViewValue(relationship, "CrossFilterDirection", "RELATIONSHIPS"),
                        ["isActive"] = GetInfoViewValue(relationship, "IsActive", "RELATIONSHIPS")
                    };
                }
            }
        }
        else if (type == "calculation_item")
        {
            try
            {
                // tableName parameter represents the calculation group name for calculation items
                var ciQuery = string.IsNullOrEmpty(tableName)
                    ? $@"SELECT CI.*, CG.Name AS GroupName
                        FROM $SYSTEM.TMSCHEMA_CALCULATION_ITEMS CI
                        INNER JOIN $SYSTEM.TMSCHEMA_CALCULATION_GROUPS CG ON CI.CalculationGroupID = CG.ID
                        WHERE CI.Name = '{name.Replace("'", "''")}'"
                    : $@"SELECT CI.*, CG.Name AS GroupName
                        FROM $SYSTEM.TMSCHEMA_CALCULATION_ITEMS CI
                        INNER JOIN $SYSTEM.TMSCHEMA_CALCULATION_GROUPS CG ON CI.CalculationGroupID = CG.ID
                        WHERE CI.Name = '{name.Replace("'", "''")}' AND CG.Name = '{tableName.Replace("'", "''")}'";

                var result = await _tabularConnection.ExecAsync(ciQuery, QueryType.DMV);
                var ciItems = (result as IEnumerable<Dictionary<string, object?>>)?.ToList();

                if (ciItems != null && ciItems.Any())
                {
                    var ci = ciItems.First();
                    return new Dictionary<string, object?>
                    {
                        ["lineageTag"] = GetInfoViewValue(ci, "LineageTag"),
                        ["name"] = GetInfoViewValue(ci, "Name"),
                        ["type"] = "calculation_item",
                        ["calculationGroup"] = GetInfoViewValue(ci, "GroupName"),
                        ["ordinal"] = GetInfoViewValue(ci, "Ordinal"),
                        ["expression"] = GetInfoViewValue(ci, "Expression"),
                        ["description"] = GetInfoViewValue(ci, "Description")
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Calculation item lookup failed for name: {Name}, group: {Group}", name, tableName);
            }
        }

        return null;
    }

    private Task<object> GetObjectDependencies(Dictionary<string, object?> objectDetails, bool includeFullDependencies)
    {
        var dependencies = new Dictionary<string, object?> { ["direct"] = new List<string>() };

        if (objectDetails.TryGetValue("expression", out var exprObj) && exprObj != null)
        {
            var expression = exprObj.ToString() ?? "";
            var refs = new List<string>();
            var matches = System.Text.RegularExpressions.Regex.Matches(expression, @"'([^']+)'\[([^\]]+)\]|\[([^\]]+)\]");

            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                if (match.Groups[1].Success && match.Groups[2].Success)
                    refs.Add($"'{match.Groups[1].Value}'[{match.Groups[2].Value}]");
                else if (match.Groups[3].Success)
                    refs.Add($"[{match.Groups[3].Value}]");
            }

            dependencies["direct"] = refs.Distinct().ToList();
        }

        return Task.FromResult<object>(dependencies);
    }

    /// <summary>
    /// Helper method to retrieve values from dictionaries returned by INFO.VIEW functions.
    /// INFO.VIEW functions return column names with prefixes like "[TABLES][Name]", "[MEASURES][LineageTag]", etc.
    /// This method tries multiple key formats to find the value.
    /// </summary>
    private static object? GetInfoViewValue(Dictionary<string, object?> dict, string columnName, string? viewPrefix = null)
    {
        // Try with view prefix first (e.g., "[TABLES][Name]")
        if (viewPrefix != null && dict.TryGetValue($"[{viewPrefix}][{columnName}]", out var prefixedValue))
            return prefixedValue;

        // Try with just brackets (e.g., "[Name]")
        if (dict.TryGetValue($"[{columnName}]", out var bracketedValue))
            return bracketedValue;

        // Try without brackets (e.g., "Name")
        if (dict.TryGetValue(columnName, out var plainValue))
            return plainValue;

        // Try any key that ends with the column name
        var matchingKey = dict.Keys.FirstOrDefault(k => k.EndsWith($"[{columnName}]", StringComparison.OrdinalIgnoreCase));
        if (matchingKey != null)
            return dict[matchingKey];

        return null;
    }
}

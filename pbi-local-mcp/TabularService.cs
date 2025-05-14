// pbi-local-mcp/TabularService.cs
using Microsoft.AnalysisServices.AdomdClient;
using System.Data;

namespace pbi_local_mcp
{
    /// <summary>
    /// Provides services for interacting with a Tabular model via ADOMD.NET.
    /// This class encapsulates DAX query execution and schema retrieval logic.
    /// </summary>
    public class TabularService
    {
        // Constructor can take the connection string, or it can be passed to methods.
        // For simplicity in direct testing, let's have methods take it.

        /// <summary>
        /// Initializes a new instance of the <see cref="TabularService"/> class.
        /// </summary>
        public TabularService() { }

        /// <summary>
        /// Retrieves a list of table names from the connected tabular model.
        /// Uses the TMSCHEMA_TABLES DMV and filters out system tables (names starting with '$').
        /// Note: This method is not currently used by the PbiLocalTools MCP tools.
        /// ADOMD exceptions during connection or schema retrieval will propagate.
        /// </summary>
        /// <param name="connectionString">The ADOMD connection string to the tabular model.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a list of table names.</returns>
        public async Task<List<string>> GetTablesAsync(string connectionString)
        {
            var tableNames = new List<string>();
            await Task.Run(() => // Run ADOMD operations on a thread pool thread
            {
                using (var connection = new AdomdConnection(connectionString))
                {
                    connection.Open();
                    // Get schema for tables, filtering for actual tables (not system tables or views if any)
                    var restrictions = new AdomdRestrictionCollection();
                    // TABLE_TYPE can be 'TABLE', 'SYSTEM TABLE', 'VIEW' etc. We want user tables.
                    // Some PBI models might just list them without needing a TABLE_TYPE filter if only user tables exist.
                    // For robust PBI Desktop compatibility, often just getting all tables and then filtering client-side
                    // or relying on the fact that system tables are usually hidden or have specific naming is okay.
                    // Let's get all tables and then filter for those not starting with '$' (common for system/hidden tables).
                    // A more precise filter would be on TABLE_TYPE = 'TABLE' if available and consistent.
                    // For PBI Desktop, TABLE_TYPE might not be as straightforward as with SSAS.
                    // Let's try to get all tables first.
                    var tablesSchema = connection.GetSchemaDataSet("TMSCHEMA_TABLES", null); // Using DMV for more detail

                    if (tablesSchema != null && tablesSchema.Tables.Count > 0)
                    {
                        foreach (DataRow row in tablesSchema.Tables[0].Rows)
                        {
                            string? tableName = row["Name"]?.ToString(); // From TMSCHEMA_TABLES, column is "Name"
                            // Optionally, filter out system/hidden tables if necessary
                            if (!string.IsNullOrEmpty(tableName) && !tableName.StartsWith("$")) // Basic filter for system tables
                            {
                                tableNames.Add(tableName);
                            }
                        }
                    }
                    // The TMSCHEMA_TABLES DMV is generally preferred for modern PBI Desktop models.
                    // The older AdomdSchemaGuid.Tables might be useful for older SSAS versions but is less relevant here.
                }
            });
            return tableNames;
        }

        private async Task<IEnumerable<Dictionary<string, object?>>> ExecuteDaxQueryAsync(string connectionString, string daxQuery)
        {
            var results = new List<Dictionary<string, object?>>();

            await Task.Run(() =>
            {
                using var connection = new AdomdConnection(connectionString);
                try
                {
                    connection.Open();
                    using var cmd = new AdomdCommand(daxQuery, connection);
                    using var reader = cmd.ExecuteReader();

                    while (reader.Read())
                    {
                        var row = new Dictionary<string, object?>();
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            var value = reader.GetValue(i);
                            row[reader.GetName(i)] = value == DBNull.Value ? null : value;
                        }
                        results.Add(row);
                    }
                }
                catch (Exception ex) when (ex.Message.Contains("permission", StringComparison.OrdinalIgnoreCase))
                {
                    throw new UnauthorizedAccessException("Insufficient permissions to execute DAX query", ex);
                }
            });

            return results;
        }

        private async Task<IEnumerable<Dictionary<string, object?>>> ExecuteInfoFunctionAsync(string baseDaxFunctionName, string connectionString, string? daxFilterExpression)
        {
            string query;
            if (!string.IsNullOrEmpty(daxFilterExpression))
            {
                query = $"EVALUATE FILTER({baseDaxFunctionName}(), {daxFilterExpression})";
            }
            else
            {
                query = $"EVALUATE {baseDaxFunctionName}()";
            }
            return await ExecuteDaxQueryAsync(connectionString, query);
        }

        /// <summary>
        /// Executes the INFO.VIEW.TABLES() DAX function.
        /// </summary>
        /// <param name="connectionString">The ADOMD connection string.</param>
        /// <param name="daxFilterExpression">Optional DAX filter expression.</param>
        /// <returns>A collection of dictionaries representing the query results.</returns>
        public async Task<IEnumerable<Dictionary<string, object?>>> InfoViewTablesAsync(string connectionString, string? filter = null)
        {
            // Always use SEARCH(filter, [Name], 1, 0) > 0 as the filter expression
            string searchFilter = $"SEARCH(\"{(string.IsNullOrEmpty(filter) ? "" : filter.Replace("\"", "\"\""))}\", [Name], 1, 0) > 0";
            return await ExecuteInfoFunctionAsync("INFO.VIEW.TABLES", connectionString, searchFilter);
        }

        /// <summary>
        /// Executes the INFO.VIEW.COLUMNS() DAX function.
        /// </summary>
        /// <param name="connectionString">The ADOMD connection string.</param>
        /// <param name="daxFilterExpression">Optional DAX filter expression.</param>
        /// <returns>A collection of dictionaries representing the query results.</returns>
        public async Task<IEnumerable<Dictionary<string, object?>>> InfoViewColumnsAsync(string connectionString, string? filter = null)
        {
            // Always use SEARCH(filter, [Name], 1, 0) > 0 as the filter expression
            string searchFilter = $"SEARCH(\"{(string.IsNullOrEmpty(filter) ? "" : filter.Replace("\"", "\"\""))}\", [Name], 1, 0) > 0";
            return await ExecuteInfoFunctionAsync("INFO.VIEW.COLUMNS", connectionString, searchFilter);
        }

        /// <summary>
        /// Executes the INFO.VIEW.MEASURES() DAX function.
        /// </summary>
        /// <param name="connectionString">The ADOMD connection string.</param>
        /// <param name="daxFilterExpression">Optional DAX filter expression.</param>
        /// <returns>A collection of dictionaries representing the query results.</returns>
        public async Task<IEnumerable<Dictionary<string, object?>>> InfoViewMeasuresAsync(string connectionString, string? filter = null)
        {
            // Always use SEARCH(filter, [Name], 1, 0) > 0 as the filter expression
            string searchFilter = $"SEARCH(\"{(string.IsNullOrEmpty(filter) ? "" : filter.Replace("\"", "\"\""))}\", [Name], 1, 0) > 0";
            return await ExecuteInfoFunctionAsync("INFO.VIEW.MEASURES", connectionString, searchFilter);
        }

        /// <summary>
        /// Executes the INFO.VIEW.RELATIONSHIPS() DAX function.
        /// </summary>
        /// <param name="connectionString">The ADOMD connection string.</param>
        /// <param name="daxFilterExpression">Optional DAX filter expression.</param>
        /// <returns>A collection of dictionaries representing the query results.</returns>
        /// <summary>
        /// Executes the INFO.VIEW.RELATIONSHIPS() DAX function with filter on FromTable or ToTable.
        /// </summary>
        /// <param name="connectionString">The ADOMD connection string.</param>
        /// <param name="filter">Optional filter string.</param>
        /// <param name="tableRole">"from" (default) or "to" to select which column to filter on.</param>
        /// <returns>A collection of dictionaries representing the query results.</returns>
        public async Task<IEnumerable<Dictionary<string, object?>>> InfoViewRelationshipsAsync(
            string connectionString,
            string? filter = null,
            string tableRole = "from")
        {
            string column = tableRole.Equals("to", StringComparison.OrdinalIgnoreCase) ? "ToTable" : "FromTable";
            string searchFilter = $"SEARCH(\"{(string.IsNullOrEmpty(filter) ? "" : filter.Replace("\"", "\"\""))}\", [{column}], 1, 0) > 0";
            return await ExecuteInfoFunctionAsync("INFO.VIEW.RELATIONSHIPS", connectionString, searchFilter);
        }

        /// <summary>
        /// Executes the INFO.COLUMNS() DAX function.
        /// </summary>
        /// <param name="connectionString">The ADOMD connection string.</param>
        /// <param name="daxFilterExpression">Optional DAX filter expression.</param>
        /// <returns>A collection of dictionaries representing the query results.</returns>
        public async Task<IEnumerable<Dictionary<string, object?>>> InfoColumnsAsync(string connectionString, string? daxFilterExpression = null)
        {
            return await ExecuteInfoFunctionAsync("INFO.COLUMNS", connectionString, daxFilterExpression);
        }

        /// <summary>
        /// Executes the INFO.MEASURES() DAX function.
        /// </summary>
        /// <param name="connectionString">The ADOMD connection string.</param>
        /// <param name="daxFilterExpression">Optional DAX filter expression.</param>
        /// <returns>A collection of dictionaries representing the query results.</returns>
        public async Task<IEnumerable<Dictionary<string, object?>>> InfoMeasuresAsync(string connectionString, string? daxFilterExpression = null)
        {
            return await ExecuteInfoFunctionAsync("INFO.MEASURES", connectionString, daxFilterExpression);
        }

        /// <summary>
        /// Executes the INFO.RELATIONSHIPS() DAX function.
        /// </summary>
        /// <param name="connectionString">The ADOMD connection string.</param>
        /// <param name="daxFilterExpression">Optional DAX filter expression.</param>
/// <summary>
        /// Executes the INFO.DEPENDENCIES() DAX function with optional filters on key columns.
        /// </summary>
        /// <param name="connectionString">The ADOMD connection string.</param>
        /// <param name="objectType">Optional filter for OBJECT_TYPE.</param>
        /// <param name="objectName">Optional filter for OBJECT.</param>
        /// <param name="referencedObjectType">Optional filter for REFERENCED_OBJECT_TYPE.</param>
        /// <param name="referencedTable">Optional filter for REFERENCED_TABLE.</param>
        /// <param name="referencedObject">Optional filter for REFERENCED_OBJECT.</param>
        /// <returns>A collection of dictionaries representing the query results.</returns>
        public async Task<IEnumerable<Dictionary<string, object?>>> InfoDependenciesAsync(
            string connectionString,
            string? objectType = null,
            string? objectName = null,
            string? referencedObjectType = null,
            string? referencedTable = null,
            string? referencedObject = null)
        {
            var filters = new List<string>();
            if (!string.IsNullOrEmpty(objectType))
                filters.Add($"SEARCH(\"{objectType.Replace("\"", "\"\"")}\", [OBJECT_TYPE], 1, 0) > 0");
            if (!string.IsNullOrEmpty(objectName))
                filters.Add($"SEARCH(\"{objectName.Replace("\"", "\"\"")}\", [OBJECT], 1, 0) > 0");
            if (!string.IsNullOrEmpty(referencedObjectType))
                filters.Add($"SEARCH(\"{referencedObjectType.Replace("\"", "\"\"")}\", [REFERENCED_OBJECT_TYPE], 1, 0) > 0");
            if (!string.IsNullOrEmpty(referencedTable))
                filters.Add($"SEARCH(\"{referencedTable.Replace("\"", "\"\"")}\", [REFERENCED_TABLE], 1, 0) > 0");
            if (!string.IsNullOrEmpty(referencedObject))
                filters.Add($"SEARCH(\"{referencedObject.Replace("\"", "\"\"")}\", [REFERENCED_OBJECT], 1, 0) > 0");

            string filterExpr = filters.Count > 0 ? string.Join(" && ", filters) : "TRUE()";
            string query = $"EVALUATE FILTER(INFO.DEPENDENCIES(), {filterExpr})";
            return await ExecuteDaxQueryAsync(connectionString, query);
        }
        /// <returns>A collection of dictionaries representing the query results.</returns>
        public async Task<IEnumerable<Dictionary<string, object?>>> InfoRelationshipsAsync(string connectionString, string? daxFilterExpression = null)
        {
            return await ExecuteInfoFunctionAsync("INFO.RELATIONSHIPS", connectionString, daxFilterExpression);
        }

        /// <summary>
        /// Executes the INFO.ANNOTATIONS() DAX function.
        /// </summary>
        /// <param name="connectionString">The ADOMD connection string.</param>
        /// <param name="daxFilterExpression">Optional DAX filter expression.</param>
        /// <returns>A collection of dictionaries representing the query results.</returns>
        public async Task<IEnumerable<Dictionary<string, object?>>> InfoAnnotationsAsync(string connectionString, string? daxFilterExpression = null)
        {
            return await ExecuteInfoFunctionAsync("INFO.ANNOTATIONS", connectionString, daxFilterExpression);
        }
    }
}

// pbi-local-mcp/TabularService.cs
using Microsoft.AnalysisServices.AdomdClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace pbi_local_mcp
{
    public class TabularService
    {
        private readonly string _connectionString;

        // Constructor can take the connection string, or it can be passed to methods.
        // For simplicity in direct testing, let's have methods take it.
        public TabularService() { }

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
                            string tableName = row["Name"].ToString(); // From TMSCHEMA_TABLES, column is "Name"
                            // Optionally, filter out system/hidden tables if necessary
                            if (!tableName.StartsWith("$")) // Basic filter for system tables
                            {
                                tableNames.Add(tableName);
                            }
                        }
                    }
                    // Fallback or alternative: AdomdSchemaGuid.Tables
                    // if (tableNames.Count == 0) {
                    //    var legacyTablesSchema = connection.GetSchemaDataSet(AdomdSchemaGuid.Tables, null);
                    //    if (legacyTablesSchema != null && legacyTablesSchema.Tables.Count > 0) {
                    //        foreach (DataRow row in legacyTablesSchema.Tables[0].Rows) {
                    //            if (row["TABLE_TYPE"]?.ToString() == "TABLE") { // Check TABLE_TYPE
                    //                 tableNames.Add(row["TABLE_NAME"].ToString());
                    //            }
                    //        }
                    //    }
                    // }
                }
            });
            return tableNames;
        }
    }
}
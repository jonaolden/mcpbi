using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AnalysisServices.Tabular;
using System.Data;

namespace pbi_local_mcp.Tests
{
    /// <summary>
    /// Integration‑style smoke tests – their only job is to prove that the tools connect to *whatever* model the
    /// .env points to and that they do not throw. They make no assumptions about table or measure names.
    /// </summary>
    [TestClass]
    public class ProgramTests
    {
        private static string? _connStr;

        [TestInitialize]
        public void Setup()
        {
            // Locate the solution root (6 levels up from the compiled test DLL)
            string dir = AppContext.BaseDirectory;
            for (int i = 0; i < 6; i++)
            {
                dir = Path.GetDirectoryName(dir) ?? throw new DirectoryNotFoundException("Cannot find solution root.");
            }
            string envPath = Path.Combine(dir, ".env");
            Console.WriteLine($"[Setup] Attempting to load .env from: {envPath}");

            Assert.IsTrue(File.Exists(envPath), ".env file not found – run discover-pbi first.");
            Program.LoadEnvFile(envPath);
            Console.WriteLine($"[Setup] .env file loaded.");

            string? port = Environment.GetEnvironmentVariable("PBI_PORT");
            string? dbId = Environment.GetEnvironmentVariable("PBI_DB_ID");
            Assert.IsNotNull(port, "PBI_PORT missing after loading .env");
            Assert.IsNotNull(dbId, "PBI_DB_ID missing after loading .env");
            Console.WriteLine($"[Setup] PBI_PORT: {port}, PBI_DB_ID: {dbId}");

            _connStr = $"Provider=MSOLAP;Data Source=localhost:{port};Initial Catalog={dbId};Integrated Security=SSPI;";
            Console.WriteLine($"[Setup] Connection string for tests: {_connStr}");
        }

        [TestMethod]
        public void TestInfrastructureUp()
        {
            Console.WriteLine("[TestInfrastructureUp] Verifying basic assertion.");
            Assert.IsTrue(true);
            Console.WriteLine("[TestInfrastructureUp] Basic assertion passed.");
        }

        [TestMethod]
        public async Task TabularService_GetTables_NoThrow()
        {
            Console.WriteLine("[TabularService_GetTables_NoThrow] Initializing TabularService.");
            var svc = new pbi_local_mcp.TabularService();
            Console.WriteLine($"[TabularService_GetTables_NoThrow] Calling GetTablesAsync with connection string: {_connStr}");
            List<string> tables = await svc.GetTablesAsync(_connStr!);
            Assert.IsNotNull(tables);
            Assert.IsTrue(tables.Count >= 0, "GetTablesAsync should return a list (possibly empty).");
            Console.WriteLine($"[TabularService_GetTables_NoThrow] Model contains {tables.Count} tables.");
            if (tables.Any())
            {
                Console.WriteLine($"[TabularService_GetTables_NoThrow] Table names: {string.Join(", ", tables)}");
            }
        }

        [TestMethod]
        public void SchemaTool_ReturnsDataTable()
        {
            Console.WriteLine("[SchemaTool_ReturnsDataTable] Calling PbiLocalTools.Schema().");
            var result = PbiLocalTools.Schema();
            Assert.IsInstanceOfType(result, typeof(System.Data.DataTable)); // Explicitly System.Data.DataTable
            var dt = (System.Data.DataTable)result; // Explicitly System.Data.DataTable
            Assert.IsTrue(dt.Columns.Count > 0);
            Console.WriteLine($"[SchemaTool_ReturnsDataTable] Schema(): {dt.Rows.Count} rows, {dt.Columns.Count} cols.");
            if (dt.Columns.Count > 0)
            {
                var columnNames = string.Join(", ", dt.Columns.Cast<System.Data.DataColumn>().Select(c => c.ColumnName).Take(5));
                Console.WriteLine($"[SchemaTool_ReturnsDataTable] First 5 column names (or fewer): {columnNames}");
            }
        }

        [TestMethod]
        public void RelationshipsTool_ReturnsDataTable()
        {
            Console.WriteLine("[RelationshipsTool_ReturnsDataTable] Calling PbiLocalTools.Relationships().");
            var result = PbiLocalTools.Relationships();
            Assert.IsInstanceOfType(result, typeof(System.Data.DataTable)); // Explicitly System.Data.DataTable
            var dt = (System.Data.DataTable)result; // Explicitly System.Data.DataTable
            Assert.IsTrue(dt.Columns.Count > 0, "Relationships() returned 0‑column DataTable – unexpected.");
            Console.WriteLine($"[RelationshipsTool_ReturnsDataTable] Relationships(): {dt.Rows.Count} rows, {dt.Columns.Count} cols.");
            if (dt.Rows.Count > 0)
            {
                Console.WriteLine($"[RelationshipsTool_ReturnsDataTable] First relationship (if any): FromTable='{dt.Rows[0]["FromTable"]}', FromColumn='{dt.Rows[0]["FromColumn"]}', ToTable='{dt.Rows[0]["ToTable"]}', ToColumn='{dt.Rows[0]["ToColumn"]}'");
            }
        }

        [TestMethod]
        public void MeasureDetailsTool_SucceedsAndErrorsWhenExpected()
        {
            Console.WriteLine("[MeasureDetailsTool_SucceedsAndErrorsWhenExpected] Connecting to server.");
            using var server = new Server();
            server.Connect(_connStr);
            var model = server.Databases[0].Model;
            Console.WriteLine($"[MeasureDetailsTool_SucceedsAndErrorsWhenExpected] Connected to model: {model.Name}");

            // ***** happy path – first table that actually contains a measure *****
            Console.WriteLine("[MeasureDetailsTool_SucceedsAndErrorsWhenExpected] Searching for a table with measures.");
            var tableWithMeasure = model.Tables.FirstOrDefault(t => t.Measures.Any());
            if (tableWithMeasure is null)
            {
                string msg = "[MeasureDetailsTool_SucceedsAndErrorsWhenExpected] Model contains no measures – cannot perform MeasureDetails happy-path assertions. This is treated as a test failure.";
                Console.WriteLine(msg);
                Assert.Fail(msg);
                return; 
            }
            var measure = tableWithMeasure.Measures.First();
            Console.WriteLine($"[MeasureDetailsTool_SucceedsAndErrorsWhenExpected] Testing happy path with Table: '{tableWithMeasure.Name}', Measure: '{measure.Name}'.");
            var ok = PbiLocalTools.MeasureDetails(tableWithMeasure.Name, measure.Name);
            var okType = ok.GetType();
            var okErrorProp = okType.GetProperty("error")?.GetValue(ok);
            Assert.IsNull(okErrorProp, $"MeasureDetails returned error for existing measure: {okErrorProp}");
            var okDaxProp = okType.GetProperty("dax")?.GetValue(ok);
            Assert.IsNotNull(okDaxProp, "Existing measure should return a dax property.");
            Console.WriteLine($"[MeasureDetailsTool_SucceedsAndErrorsWhenExpected] Happy path successful. DAX: '{okDaxProp}', Description: '{okType.GetProperty("Description")?.GetValue(ok)}'.");

            // ***** missing measure *****
            string bogusMeasure = $"NoSuchMeasure_{Guid.NewGuid():N}";
            Console.WriteLine($"[MeasureDetailsTool_SucceedsAndErrorsWhenExpected] Testing missing measure. Table: '{tableWithMeasure.Name}', Bogus Measure: '{bogusMeasure}'.");
            var bad1 = PbiLocalTools.MeasureDetails(tableWithMeasure.Name, bogusMeasure);
            var bad1Type = bad1.GetType();
            var bad1ErrorProp = bad1Type.GetProperty("error")?.GetValue(bad1);
            Assert.IsNotNull(bad1ErrorProp, "Non‑existent measure should return error.");
            Console.WriteLine($"[MeasureDetailsTool_SucceedsAndErrorsWhenExpected] Missing measure test successful. Error: '{bad1ErrorProp}'.");

            // ***** missing table *****
            string bogusTable = $"NoSuchTable_{Guid.NewGuid():N}";
            Console.WriteLine($"[MeasureDetailsTool_SucceedsAndErrorsWhenExpected] Testing missing table. Bogus Table: '{bogusTable}', Measure: 'Anything'.");
            var bad2 = PbiLocalTools.MeasureDetails(bogusTable, "Anything");
            var bad2Type = bad2.GetType();
            var bad2ErrorProp = bad2Type.GetProperty("error")?.GetValue(bad2);
            Assert.IsNotNull(bad2ErrorProp, "Non‑existent table should return error.");
            Console.WriteLine($"[MeasureDetailsTool_SucceedsAndErrorsWhenExpected] Missing table test successful. Error: '{bad2ErrorProp}'.");
        }

        [TestMethod]
        public async Task PreviewTool_DoesNotThrow()
        {
            Console.WriteLine("[PreviewTool_DoesNotThrow] Initializing TabularService.");
            var svc = new pbi_local_mcp.TabularService();
            Console.WriteLine($"[PreviewTool_DoesNotThrow] Calling GetTablesAsync with connection string: {_connStr}");
            List<string> tables = await svc.GetTablesAsync(_connStr!);
            Console.WriteLine($"[PreviewTool_DoesNotThrow] Found {tables.Count} tables.");

            if (tables.Any())
            {
                var firstTable = tables.First();
                Console.WriteLine($"[PreviewTool_DoesNotThrow] Attempting to preview first table: '{firstTable}' with TOPN=5.");
                var toolResult = PbiLocalTools.Preview(firstTable, 5);
                Assert.IsInstanceOfType(toolResult, typeof(System.Data.DataTable)); // Explicitly System.Data.DataTable
                var dt = (System.Data.DataTable)toolResult; // Explicitly System.Data.DataTable
                Assert.IsNotNull(dt);
                Console.WriteLine($"[PreviewTool_DoesNotThrow] Preview(): {dt.Rows.Count} rows for table '{firstTable}'.");
            }
            else
            {
                Console.WriteLine("[PreviewTool_DoesNotThrow] No tables found in model. Testing preview with a non-existent table 'Definitely_Not_A_Table'.");
                // No tables – expect Adomd to throw for bogus table
                Assert.ThrowsException<Microsoft.AnalysisServices.AdomdClient.AdomdErrorResponseException>(() =>
                {
                    Console.WriteLine("[PreviewTool_DoesNotThrow] Calling PbiLocalTools.Preview for 'Definitely_Not_A_Table'. Expecting AdomdErrorResponseException.");
                    PbiLocalTools.Preview("Definitely_Not_A_Table", 5);
                });
                Console.WriteLine("[PreviewTool_DoesNotThrow] AdomdErrorResponseException correctly thrown for non-existent table.");
            }
        }

        [TestMethod]
        public void EvalTool_ReturnsDataTableOrError()
        {
            string daxExpression = "ROW(\"Col1\", 1)";
            Console.WriteLine($"[EvalTool_ReturnsDataTableOrError] Evaluating DAX expression: {daxExpression}");
            var result = PbiLocalTools.Eval(daxExpression);
            if (result is System.Data.DataTable dt) // Explicitly System.Data.DataTable
            {
                Assert.IsTrue(dt.Columns.Count > 0);
                Console.WriteLine($"[EvalTool_ReturnsDataTableOrError] Evaluation successful. Result is DataTable with {dt.Rows.Count} rows and {dt.Columns.Count} columns.");
                if (dt.Rows.Count > 0 && dt.Columns.Count > 0)
                {
                    Console.WriteLine($"[EvalTool_ReturnsDataTableOrError] First cell value: {dt.Rows[0][0]}");
                }
            }
            else
            {
                var type = result.GetType();
                var err = type.GetProperty("error")?.GetValue(result) as string;
                Assert.IsFalse(string.IsNullOrWhiteSpace(err), "EvalTool returned an object that is not a DataTable and has no 'error' property or the error is empty.");
                Console.WriteLine($"[EvalTool_ReturnsDataTableOrError] Evaluation resulted in an error object. Error: '{err}'. This is expected for invalid DAX or other issues.");
                // This part of the test assumes ROW("Col1",1) is valid. If testing an invalid DAX, the 'else' block is the success path.
                // For this specific DAX, we expect a DataTable. If it's an error, the Assert.IsFalse above will fail if err is null/whitespace.
            }

            string invalidDaxExpression = "EVALUATE NonExistentTable";
            Console.WriteLine($"[EvalTool_ReturnsDataTableOrError] Evaluating invalid DAX expression: {invalidDaxExpression}");
            var errorResult = PbiLocalTools.Eval(invalidDaxExpression);
            Assert.IsNotInstanceOfType(errorResult, typeof(System.Data.DataTable), "Evaluating invalid DAX should not return a DataTable."); // Explicitly System.Data.DataTable
            var errorType = errorResult.GetType();
            var errorMessage = errorType.GetProperty("error")?.GetValue(errorResult) as string;
            Assert.IsFalse(string.IsNullOrWhiteSpace(errorMessage), "Evaluating invalid DAX should return an error message.");
            Console.WriteLine($"[EvalTool_ReturnsDataTableOrError] Evaluation of invalid DAX successful. Error: '{errorMessage}'.");
        }
    }
}

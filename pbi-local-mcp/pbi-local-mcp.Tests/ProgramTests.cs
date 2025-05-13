using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using pbi_local_mcp;
using System.Threading.Tasks;

namespace pbi_local_mcp.Tests
{
    [TestClass]
    public class ProgramTests
    {
        [TestInitialize]
        public void Setup()
        {
            // Find solution root by traversing up 6 directories from AppContext.BaseDirectory
            string currentDirectory = AppContext.BaseDirectory;
            string solutionRoot = currentDirectory;
            for (int i = 0; i < 6; i++)
            {
                solutionRoot = Path.GetDirectoryName(solutionRoot)!;
                if (solutionRoot == null) throw new DirectoryNotFoundException("Could not find solution root.");
            }
            string envFilePath = Path.Combine(solutionRoot, ".env");
            Console.WriteLine($"Attempting to load .env file from: {envFilePath}");
            if (File.Exists(envFilePath))
            {
                Program.LoadEnvFile(envFilePath);
                Console.WriteLine($"PBI_PORT from env: {Environment.GetEnvironmentVariable("PBI_PORT")}");
                Assert.IsNotNull(Environment.GetEnvironmentVariable("PBI_PORT"), ".env file loaded but PBI_PORT not set.");
            }
            else
            {
                Console.WriteLine($".env file not found at {envFilePath}. Ensure discover-pbi has been run and .env exists.");
                Assert.Fail($".env file not found at {envFilePath}. Run discover-pbi first.");
            }
        }

        [TestMethod]
        public void TestSetupWorks()
        {
            // This test always passes, confirming the test infrastructure is working.
            Assert.IsTrue(true);
        }
        [TestMethod]
        public async Task TestGetTablesAction_ConnectsAndRetrievesTablesAsync()
        {
            string port = Environment.GetEnvironmentVariable("PBI_PORT");
            string dbId = Environment.GetEnvironmentVariable("PBI_DB_ID");
            Assert.IsNotNull(port, "PBI_PORT not loaded from .env for test.");
            Assert.IsNotNull(dbId, "PBI_DB_ID not loaded from .env for test.");
            string connectionString = $"Provider=MSOLAP;Data Source=localhost:{port};Initial Catalog={dbId};";
            Console.WriteLine($"Test Connection String: {connectionString}");

            var tabularService = new pbi_local_mcp.TabularService(); // Instantiate the service
            List<string> tableNames = null;
            Exception serviceException = null;

            try
            {
                tableNames = await tabularService.GetTablesAsync(connectionString);
            }
            catch (Exception ex)
            {
                serviceException = ex;
                Console.WriteLine($"Service Exception: {ex}");
            }

            Assert.IsNull(serviceException, $"TabularService.GetTablesAsync threw an exception: {serviceException?.Message}");
            Assert.IsNotNull(tableNames, "GetTablesAsync returned null instead of a list of tables.");

            // At least one table should exist in a typical PBI model, even if it's a hidden date table.
            // For a more specific test, you'd check against known tables in your test PBIX file.
            Assert.IsTrue(tableNames.Any(), "No tables were retrieved from the PBI model. The model might be empty or connection failed silently.");
            Console.WriteLine($"Successfully retrieved {tableNames.Count} tables: {string.Join(", ", tableNames)}");

            // Example: Assert a specific known table exists if you have a test model
            // Assert.IsTrue(tableNames.Contains("YourKnownTable"), "Expected table 'YourKnownTable' not found.");
        }
[TestMethod]
        public void TestSchemaTool_ReturnsDataTable()
        {
            Assert.IsNotNull(Environment.GetEnvironmentVariable("PBI_PORT"), "PBI_PORT not set.");
            Assert.IsNotNull(Environment.GetEnvironmentVariable("PBI_DB_ID"), "PBI_DB_ID not set.");
            var result = PbiLocalTools.Schema();
            Assert.IsNotNull(result, "Schema() returned null.");
            Assert.IsInstanceOfType(result, typeof(System.Data.DataTable), "Schema() did not return a DataTable.");
            var dt = (System.Data.DataTable)result;
            Assert.IsTrue(dt.Columns.Count > 0, "Schema DataTable has no columns.");
        }

        [TestMethod]
        public void TestRelationshipsTool_ReturnsDataTable()
        {
            Assert.IsNotNull(Environment.GetEnvironmentVariable("PBI_PORT"), "PBI_PORT not set.");
            Assert.IsNotNull(Environment.GetEnvironmentVariable("PBI_DB_ID"), "PBI_DB_ID not set.");
            var result = PbiLocalTools.Relationships();
            Assert.IsNotNull(result, "Relationships() returned null.");
            Assert.IsInstanceOfType(result, typeof(System.Data.DataTable), "Relationships() did not return a DataTable.");
            var dt = (System.Data.DataTable)result;
            // Columns expected: FromTable, FromCol, ToTable, ToCol, CARDINALITY, CROSSFILTER_DIRECTION
            Assert.IsTrue(dt.Columns.Contains("FromTable"), "Relationships DataTable missing 'FromTable' column.");
        }

        [TestMethod]
        public async Task TestMeasureDetailsTool_HandlesMissingOrReturnsExpected()
        {
            Assert.IsNotNull(Environment.GetEnvironmentVariable("PBI_PORT"), "PBI_PORT not set.");
            Assert.IsNotNull(Environment.GetEnvironmentVariable("PBI_DB_ID"), "PBI_DB_ID not set.");

            var tabularService = new pbi_local_mcp.TabularService();
            List<string> tableNames = null;
            try
            {
                string port = Environment.GetEnvironmentVariable("PBI_PORT");
                string dbId = Environment.GetEnvironmentVariable("PBI_DB_ID");
                string connectionString = $"Provider=MSOLAP;Data Source=localhost:{port};Initial Catalog={dbId};";
                tableNames = await tabularService.GetTablesAsync(connectionString);
            }
            catch (Exception ex)
            {
                tableNames = new List<string>();
                Console.WriteLine($"TabularService.GetTablesAsync failed: {ex}");
            }

            string tableNameToTest = (tableNames != null && tableNames.Any())
                ? tableNames.First()
                : "NonExistentTableForMeasureTest";

            try
            {
                var result = PbiLocalTools.MeasureDetails(tableNameToTest, "NonExistentTestMeasure");
                Assert.IsNotNull(result, "MeasureDetails() returned null.");
                var type = result.GetType();
                Assert.IsNotNull(type.GetProperty("dax"), "Result missing 'dax' property.");
            }
            catch (Microsoft.AnalysisServices.AdomdClient.AdomdErrorResponseException ex)
            {
                Assert.IsTrue(ex.Message.Contains("cannot find") || ex.Message.Contains("not found"), "Unexpected AdomdErrorResponseException message.");
            }
            catch (KeyNotFoundException)
            {
                Assert.IsTrue(true);
            }
            catch (Exception ex)
            {
                Assert.Fail($"Unexpected exception: {ex}");
            }
        }

        [TestMethod]
        public async Task TestPreviewTool_ReturnsDataTable()
        {
            Assert.IsNotNull(Environment.GetEnvironmentVariable("PBI_PORT"), "PBI_PORT not set.");
            Assert.IsNotNull(Environment.GetEnvironmentVariable("PBI_DB_ID"), "PBI_DB_ID not set.");

            var tabularService = new pbi_local_mcp.TabularService();
            List<string> tableNames = null;
            try
            {
                string port = Environment.GetEnvironmentVariable("PBI_PORT");
                string dbId = Environment.GetEnvironmentVariable("PBI_DB_ID");
                string connectionString = $"Provider=MSOLAP;Data Source=localhost:{port};Initial Catalog={dbId};";
                tableNames = await tabularService.GetTablesAsync(connectionString);
            }
            catch (Exception ex)
            {
                tableNames = new List<string>();
                Console.WriteLine($"TabularService.GetTablesAsync failed: {ex}");
            }

            if (tableNames != null && tableNames.Any())
            {
                string firstTable = tableNames.First();
                var result = PbiLocalTools.Preview(firstTable, 5);
                Assert.IsNotNull(result, "Preview() returned null.");
                Assert.IsInstanceOfType(result, typeof(System.Data.DataTable), "Preview() did not return a DataTable.");
            }
            else
            {
                try
                {
                    var result = PbiLocalTools.Preview("NonExistentTableForPreviewTest", 5);
                    Assert.Fail("Preview() should have thrown for non-existent table, but did not.");
                }
                catch (Microsoft.AnalysisServices.AdomdClient.AdomdErrorResponseException ex)
                {
                    Assert.IsTrue(ex.Message.Contains("cannot find") || ex.Message.Contains("not found"), "Unexpected AdomdErrorResponseException message.");
                }
                catch (Exception ex)
                {
                    Assert.Fail($"Unexpected exception: {ex}");
                }
            }
        }

        [TestMethod]
        public void TestEvalTool_ReturnsDataTableOrError()
        {
            Assert.IsNotNull(Environment.GetEnvironmentVariable("PBI_PORT"), "PBI_PORT not set.");
            Assert.IsNotNull(Environment.GetEnvironmentVariable("PBI_DB_ID"), "PBI_DB_ID not set.");
            var result = PbiLocalTools.Eval("ROW(\"Col1\", 1)");
            Assert.IsNotNull(result, "Eval() returned null.");
            if (result is System.Data.DataTable dt)
            {
                Assert.IsTrue(dt.Columns.Count > 0, "Eval DataTable has no columns.");
            }
            else
            {
                var type = result.GetType();
                var errorProp = type.GetProperty("error");
                Assert.IsNotNull(errorProp, "Eval() returned non-DataTable object without 'error' property.");
                var errorMsg = errorProp.GetValue(result) as string;
                Assert.IsFalse(string.IsNullOrWhiteSpace(errorMsg), "Eval() error property is empty.");
            }
        }
    }
}
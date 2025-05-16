// Tools.cs
namespace pbi_local_mcp;

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using Microsoft.AnalysisServices.AdomdClient;
public static class Tools
{
    private static readonly TabularConnection _tabular = new();
    // Minimal local CallToolResponse for compatibility
    public class CallToolResponse
    {
        public List<Content> Content { get; set; } = new();
    }
    public class Content
    {
        public string Type { get; set; } = "";
        public string Text { get; set; } = "";
    }

    private static CallToolResponse Wrap(object result) =>
        new CallToolResponse
        {
            Content = new List<Content>
            {
                new Content
                {
                    Type = "application/json",
                    Text = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })
                }
            }
        };

    // ---------- error handling helpers ------------------------------------
    private static bool IsSyntaxError(Exception ex) =>
        ex.Message.Contains("syntax", StringComparison.OrdinalIgnoreCase) ||
        ex.Message.Contains("parser", StringComparison.OrdinalIgnoreCase) ||
        (ex.InnerException is { } ie &&
         (ie.Message.Contains("syntax", StringComparison.OrdinalIgnoreCase) ||
          ie.Message.Contains("parser", StringComparison.OrdinalIgnoreCase)));

    private static async Task<object> Safe(Func<Task<IEnumerable<Dictionary<string, object?>>>> op,
                                           [CallerMemberName] string member = "")
    {
        try
        {
            return await op();
        }
        catch (UnauthorizedAccessException)
        {
            return new { error = "InsufficientPermissions" };
        }
        catch (AdomdErrorResponseException ex) when (IsSyntaxError(ex))
        {
            return new { error = $"InvalidDaxFilterExpression: {ex.Message}" };
        }
        catch (AdomdErrorResponseException ex)
        {
            return new { error = $"DaxExecutionError: {ex.Message}" };
        }
        catch (AdomdException ex)
        {
            return new { error = $"AdomdClientError: {ex.Message}" };
        }
        catch (Exception ex) when (IsSyntaxError(ex))
        {
            return new { error = $"InvalidDaxFilterExpression: {ex.Message}" };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[{DateTime.UtcNow:O}] [{member}] {ex}");
            return new { error = $"UnexpectedError: {ex.Message}" };
        }
    }

    // ----------------- DAX requirements MCP tools -------------------------

    public static async Task<CallToolResponse> ListMeasures(string? tableName = "")
    {
        string filter = string.IsNullOrWhiteSpace(tableName)
            ? null
            : $"SEARCH(\"{tableName.Replace("\"", "\"\"")}\",[Table],1,0)>0";
        var result = await Safe(() => _tabular.ExecInfoAsync("INFO.VIEW.MEASURES", filter));
        return Wrap(result);
    }

    public static async Task<CallToolResponse> GetMeasureDetails(string measureName)
    {
        string dax = $@"
EVALUATE
VAR t = FILTER(INFO.VIEW.MEASURES(),[Name]=""{measureName.Replace("\"","\"\"")}"")
VAR d = COUNTROWS(FILTER(INFO.DEPENDENCIES(),[OBJECT_TYPE]=""Measure""&&[OBJECT]=""{measureName.Replace("\"","\"\"")}""))
RETURN ADDCOLUMNS(t,""Dependencies"",d)";
        var result = await Safe(() => _tabular.ExecAsync(dax));
        return Wrap(result);
    }

    public static async Task<CallToolResponse> ListTables()
    {
        var result = await Safe(() => _tabular.ExecInfoAsync("INFO.VIEW.TABLES", null));
        return Wrap(result);
    }

    public static async Task<CallToolResponse> GetTableDetails(string tableName)
    {
        string dax = $@"
EVALUATE
VAR t = FILTER(INFO.VIEW.TABLES(),[Name]=""{tableName.Replace("\"","\"\"")}"")
VAR r = COUNTROWS(FILTER(INFO.VIEW.RELATIONSHIPS(),[FromTable]=""{tableName.Replace("\"","\"\"")}""||[ToTable]=""{tableName.Replace("\"","\"\"")}""))
VAR m = COUNTROWS(FILTER(INFO.VIEW.MEASURES(),[Table]=""{tableName.Replace("\"","\"\"")}""))
RETURN ADDCOLUMNS(t,""RelationshipCount"",r,""MeasureCount"",m)";
        var result = await Safe(() => _tabular.ExecAsync(dax));
        return Wrap(result);
    }

    public static async Task<CallToolResponse> GetTableColumns(string tableName)
    {
        string filter = $"SEARCH(\"{tableName.Replace("\"", "\"\"")}\",[Table],1,0)>0";
        var result = await Safe(() => _tabular.ExecInfoAsync("INFO.VIEW.COLUMNS", filter));
        return Wrap(result);
    }

    public static async Task<CallToolResponse> GetTableRelationships(string tableName)
    {
        string filterFrom = $"SEARCH(\"{tableName.Replace("\"", "\"\"")}\",[FromTable],1,0)>0";
        string filterTo = $"SEARCH(\"{tableName.Replace("\"", "\"\"")}\",[ToTable],1,0)>0";
        string filter = $"{filterFrom} || {filterTo}";
        var result = await Safe(() => _tabular.ExecInfoAsync("INFO.VIEW.RELATIONSHIPS", filter));
        return Wrap(result);
    }

    public static async Task<CallToolResponse> PreviewTableData(string tableName, int topN = 10)
    {
        int n = Math.Min(topN, 10);
        string dax = $"EVALUATE TOPN({n}, '{tableName.Replace("'", "''")}')";
        var result = await Safe(() => _tabular.ExecAsync(dax));
        return Wrap(result);
    }

    public static async Task<CallToolResponse> EvaluateDAX(string expression, int topN = 10)
    {
        string trimmed = expression.TrimStart();
        string dax;

        // If the caller already supplied a full DAX query (starts with EVALUATE),
        // run it unchanged; otherwise assume a scalar/expression and wrap it so
        // that the engine always receives a *table* query.
        if (trimmed.StartsWith("EVALUATE", StringComparison.OrdinalIgnoreCase))
        {
            dax = trimmed;
        }
        else
        {
            // Scalar-to-table wrapper (ROW returns a one-row, one-column table)
            dax = $"EVALUATE ROW(\"Value\", {expression})";
        }

        var result = await Safe(() => _tabular.ExecDaxAsync(dax));
        return Wrap(result);
    }
}
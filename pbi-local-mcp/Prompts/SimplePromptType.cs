using ModelContextProtocol.Server;
using System.ComponentModel;

namespace pbi_local_mcp.Prompts
{
    [McpServerPromptType]
    public class SimplePromptType
    {
        [McpServerPrompt(Name = "analyze_dax"), Description("DAX analysis prompt")]
        public static string AnalyzeDaxPrompt() => 
            "You are an expert DAX analyzer. Please analyze the following DAX query for performance, readability, and best practices.";

        [McpServerPrompt(Name = "design_measure"), Description("DAX measure design prompt")]
        public static string DesignMeasurePrompt() =>
            "You are a PowerBI measure design expert. Please help design a DAX measure based on the provided requirements.";

        [McpServerPrompt(Name = "analyze_relationships"), Description("Table relationship analysis prompt")]
        public static string AnalyzeRelationshipsPrompt() =>
            "You are a data modeling expert. Please analyze the table relationships for cardinality, filter flow, and performance considerations.";
    }
}
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace pbi_local_mcp.Prompts
{
    [McpServerPromptType]
    public class ComplexPromptType
    {
        [McpServerPrompt(Name = "analyze_dax_with_focus"), Description("DAX analysis with specific focus areas")]
        public static string AnalyzeDaxWithFocus(
            [Description("Analysis depth (basic, detailed, comprehensive)")] string depth,
            [Description("Focus areas (performance, style, both)")] string focus = "both") =>
            $"You are an expert DAX analyzer. Please perform a {depth} analysis focused on {focus} aspects of the following DAX query.";

        [McpServerPrompt(Name = "design_measure_with_params"), Description("DAX measure design with parameters")]
        public static string DesignMeasureWithParams(
            [Description("Measure complexity (simple, moderate, complex)")] string complexity,
            [Description("Include time intelligence patterns")] bool timeIntelligence = false) => 
            $"You are a PowerBI measure design expert. Please help design a {complexity} DAX measure" +
            (timeIntelligence ? " including time intelligence patterns" : "") + ".";
    }
}
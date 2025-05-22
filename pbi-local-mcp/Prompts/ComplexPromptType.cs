using ModelContextProtocol.Server;

using System.ComponentModel;

namespace pbi_local_mcp.Prompts;

/// <summary>
/// Provides customizable prompt templates for DAX analysis and PowerBI measure design tasks.
/// Allows fine-tuning of analysis depth, focus areas, and measure complexity through parameters.
/// </summary>
[McpServerPromptType]
public class ComplexPromptType
{
    /// <summary>
    /// Returns a customized prompt template for analyzing DAX queries with specific focus areas
    /// and analysis depth levels.
    /// </summary>
    /// <param name="depth">The depth of analysis requested: 'basic', 'detailed', or 'comprehensive'</param>
    /// <param name="focus">The focus areas for analysis: 'performance', 'style', or 'both' (default)</param>
    /// <returns>A specialized prompt for DAX query analysis with specified parameters</returns>
    [McpServerPrompt(Name = "analyze_dax_with_focus"), Description("DAX analysis with specific focus areas")]
    public static string AnalyzeDaxWithFocus(
        [Description("Analysis depth (basic, detailed, comprehensive)")] string depth,
        [Description("Focus areas (performance, style, both)")] string focus = "both")
    {
        return $"You are an expert DAX analyzer. Please perform a {depth} analysis focused on {focus} aspects of the following DAX query.";
    }

    /// <summary>
    /// Returns a customized prompt template for designing DAX measures with specified complexity
    /// and optional time intelligence patterns.
    /// </summary>
    /// <param name="complexity">The desired measure complexity: 'simple', 'moderate', or 'complex'</param>
    /// <param name="timeIntelligence">Whether to include time intelligence patterns in the measure design</param>
    /// <returns>A specialized prompt for DAX measure design with specified parameters</returns>
    [McpServerPrompt(Name = "design_measure_with_params"), Description("DAX measure design with parameters")]
    public static string DesignMeasureWithParams(
        [Description("Measure complexity (simple, moderate, complex)")] string complexity,
        [Description("Include time intelligence patterns")] bool timeIntelligence = false)
    {
        return $"You are a PowerBI measure design expert. Please help design a {complexity} DAX measure" +
        (timeIntelligence ? " including time intelligence patterns" : "") + ".";
    }
}
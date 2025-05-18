using ModelContextProtocol.Server;

using System.ComponentModel;

namespace pbi_local_mcp.Prompts;

/// <summary>
/// Provides a collection of predefined prompts for DAX analysis and PowerBI measure design tasks.
/// Contains templates for analyzing DAX queries, designing measures, and analyzing table relationships.
/// </summary>
[McpServerPromptType]
public class SimplePromptType
{
    /// <summary>
    /// Returns a prompt template for analyzing DAX queries, focusing on performance optimization,
    /// code readability, and adherence to best practices.
    /// </summary>
    /// <returns>A specialized prompt for DAX query analysis and improvement recommendations.</returns>
    [McpServerPrompt(Name = "analyze_dax"), Description("DAX analysis prompt")]
    public static string AnalyzeDaxPrompt() =>
        "You are an expert DAX analyzer. Please analyze the following DAX query for performance, readability, and best practices.";

    /// <summary>
    /// Returns a prompt template for designing DAX measures based on specific requirements.
    /// Guides the creation of efficient and maintainable PowerBI measures.
    /// </summary>
    /// <returns>A specialized prompt for DAX measure design assistance.</returns>
    [McpServerPrompt(Name = "design_measure"), Description("DAX measure design prompt")]
    public static string DesignMeasurePrompt() =>
        "You are a PowerBI measure design expert. Please help design a DAX measure based on the provided requirements.";

    /// <summary>
    /// Returns a prompt template for analyzing table relationships in a data model,
    /// examining cardinality, filter flow patterns, and performance implications.
    /// </summary>
    /// <returns>A specialized prompt for data model relationship analysis.</returns>
    [McpServerPrompt(Name = "analyze_relationships"), Description("Table relationship analysis prompt")]
    public static string AnalyzeRelationshipsPrompt() =>
        "You are a data modeling expert. Please analyze the table relationships for cardinality, filter flow, and performance considerations.";
}
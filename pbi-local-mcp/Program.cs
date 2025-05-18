using pbi_local_mcp.Resources;

/// <summary>
/// Entry point for the Power BI Model Context Protocol application
/// </summary>
public static partial class Program
{
    /// <summary>
    /// Main entry point for the application
    /// </summary>
    /// <param name="args">Command line arguments</param>
    public static Task Main(string[] args) =>
        ServerConfigurator.RunServerAsync(args);
}
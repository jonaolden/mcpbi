namespace pbi_local_mcp.Resources;
using Microsoft.Extensions.Logging;

internal static class LoggingExtensions
{
    /// <summary>
    /// Configures standardized logging for the MCP server. Routes ALL console logs to stderr
    /// (required so stdout stays reserved for JSON-RPC) and sets baseline minimum level.
    /// </summary>
    /// <param name="logging">The logging builder.</param>
    /// <returns>The same logging builder for chaining.</returns>
    public static ILoggingBuilder ConfigureMcpLogging(this ILoggingBuilder logging)
    {
        if (logging == null) throw new ArgumentNullException(nameof(logging));

        // Minimum level - consider environment variable override in future:
        // TODO: Make minimum level conditional (e.g. Information in production, Debug when PBI_MCP_VERBOSE=1)
        logging.SetMinimumLevel(LogLevel.Debug);

        logging.AddConsole(o =>
        {
            // Ensure no stdout writes
            o.LogToStandardErrorThreshold = LogLevel.Trace;
        });

        // Reduce noise from framework libraries while preserving warnings/errors.
        logging.AddFilter("Microsoft", LogLevel.Information);

        return logging;
    }
}

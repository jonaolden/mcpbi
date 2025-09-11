using System;
using System.ComponentModel;
using System.Threading.Tasks;
using ModelContextProtocol.Server;

namespace pbi_local_mcp.Tools
{
    /// <summary>
    /// Diagnostic static tool set to validate MCP reflection discovery (static class + static methods scenario).
    /// If this registers while instance tools do not, the issue is with instance construction.
    /// </summary>
    [McpServerToolType]
    public static class DaxDiagnosticTools
    {
        /// <summary>
        /// Simple ping tool to confirm tool discovery pipeline.
        /// </summary>
        [McpServerTool, Description("Simple health check / discovery validation tool returning timestamp and assembly identity.")]
        public static Task<object> Ping()
        {
            var asm = typeof(DaxDiagnosticTools).Assembly.GetName();
            return Task.FromResult<object>(new
            {
                Status = "OK",
                Utc = DateTime.UtcNow,
                Assembly = asm.Name,
                Version = asm.Version?.ToString() ?? "n/a",
                ToolType = typeof(DaxDiagnosticTools).FullName
            });
        }
    }
}
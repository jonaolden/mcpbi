// MCP LongRunningTool sample implementation for PowerBI MCP server

using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Threading.Tasks;

namespace pbi_local_mcp.Tools;

/// <summary>
/// Provides long-running tool implementation for demonstration purposes
/// </summary>
[McpServerToolType]
public class LongRunningTool
{    /// <summary>
    /// Demonstrates a long running operation with progress updates
    /// </summary>
    /// <param name="server">The MCP server instance</param>
    /// <param name="context">The request context</param>
    /// <param name="duration">Duration of the operation in seconds</param>
    /// <param name="steps">Number of steps to divide the operation into</param>
    /// <returns>A message indicating completion of the operation</returns>
    [McpServerTool(Name = "longRunningOperation"), Description("Demonstrates a long running operation with progress updates")]
    public static async Task<string> LongRunningOperation(
        IMcpServer server,
        RequestContext<CallToolRequestParams> context,
        int duration = 10,
        int steps = 5)
    {
        var progressToken = context.Params?.Meta?.ProgressToken;
        var stepDuration = duration / steps;

        for (int i = 1; i <= steps + 1; i++)
        {
            await Task.Delay(stepDuration * 1000);

            if (progressToken is not null)
            {
                await server.SendNotificationAsync("notifications/progress", new
                {
                    Progress = i,
                    Total = steps,
                    progressToken
                });
            }
        }

        return $"Long running operation completed. Duration: {duration} seconds. Steps: {steps}.";
    }
}
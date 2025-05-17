// MCP AddTool sample implementation for PowerBI MCP server

using ModelContextProtocol.Server;
using System.ComponentModel;

namespace pbi_local_mcp.Tools;

[McpServerToolType]
public class AddTool
{
    [McpServerTool(Name = "add"), Description("Adds two numbers.")]
    public static string Add(int a, int b) => $"The sum of {a} and {b} is {a + b}";
}
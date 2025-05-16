// Program.cs
using System;

namespace pbi_local_mcp
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            // Entry point for the application.
            // Handles CLI argument routing for main features.

            if (args.Length > 0 &&
                (args[0].Equals("discover-pbi", StringComparison.OrdinalIgnoreCase) ||
                 args[0].Equals("InstanceDiscovery", StringComparison.OrdinalIgnoreCase)))
            {
                InstanceDiscovery.RunInteractive();
                return;
            }

            // Default: start the MCP server
            await Server.RunServerAsync(args);
        }
    }
}
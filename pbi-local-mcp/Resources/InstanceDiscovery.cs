using Microsoft.Extensions.Logging;
using pbi_local_mcp.Core;
using System.Data;
using System.Management;
using System.Runtime.Versioning;
using Microsoft.AnalysisServices.AdomdClient;

namespace pbi_local_mcp;

/// <summary>
/// Discovers running Power BI Desktop instances and their Analysis Services databases
/// </summary>
[SupportedOSPlatform("windows")]
public class InstanceDiscovery : IInstanceDiscovery
{
    private readonly ILogger<InstanceDiscovery> _logger;

    /// <summary>
    /// Initializes a new instance of the InstanceDiscovery class.
    /// </summary>
    /// <param name="logger">The logger instance used for recording diagnostic information and errors</param>
    /// <remarks>
    /// The provided logger is used throughout the discovery process to log errors
    /// and important diagnostic information about Power BI instance discovery.
    /// </remarks>
    public InstanceDiscovery(ILogger<InstanceDiscovery> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Starts a new instance of Power BI Desktop using its default installation path.
    /// </summary>
    /// <remarks>
    /// This method attempts to launch PBIDesktop.exe from the standard installation directory.
    /// Any launch failures are logged to the console error stream but do not throw exceptions.
    /// </remarks>
    private static void RunPowerBiDesktop()
    {
        try
        {
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = @"C:\Program Files\Microsoft Power BI Desktop\bin\PBIDesktop.exe",
                    UseShellExecute = true
                }
            };
            process.Start();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to start Power BI Desktop: {ex.Message}");
        }
    }

    /// <summary>
    /// Enables interactive mode allowing users to select a Power BI instance and database.
    /// Updates the .env file with the selected instance's port and database ID.
    /// </summary>
    /// <remarks>
    /// This method:
    /// 1. Discovers all running Power BI instances
    /// 2. If no instances found, launches Power BI Desktop
    /// 3. Lists available instances and their databases
    /// 4. Prompts user to select an instance and database
    /// 5. Writes connection settings to .env file
    /// </remarks>
    /// <returns>A task representing the asynchronous operation</returns>
    public static async Task RunInteractiveAsync()
    {
        var instances = await DiscoverInstancesAsync().ConfigureAwait(false);
        var instanceList = instances.ToList();

        if (!instanceList.Any())
        {
            Console.WriteLine("No Power BI instances found. Starting Power BI Desktop...");
            RunPowerBiDesktop();
            return;
        }

        Console.WriteLine("Found Power BI instances:");
        foreach (var inst in instanceList)
        {
            Console.WriteLine($"Port: {inst.Port} - Databases: {string.Join(", ", inst.Databases.Select(d => d.Name))}");
        }

        var portList = instanceList.Select(i => i.Port.ToString()).ToList();
        var portIdx = SelectFromList("Select port number:", portList);
        var instance = instanceList[portIdx];

        Core.DatabaseInfo? db = null;
        if (instance.Databases.Count == 1)
        {
            db = instance.Databases[0];
        }
        else
        {
            var dbDisplay = instance.Databases.Select(d => $"{d.Name} ({d.Id})").ToList();
            var dbIdx = SelectFromList("Select database:", dbDisplay);
            db = instance.Databases[dbIdx];
        }

        var envContent = $"PBI_PORT={instance.Port}\nPBI_DB_ID={db.Id}\n";
        await File.WriteAllTextAsync(".env", envContent).ConfigureAwait(false);

        Console.WriteLine($".env updated: PBI_PORT={instance.Port}, PBI_DB_ID={db.Id}");
    }

    /// <summary>
    /// Prompts the user to select an item from a list of options.
    /// </summary>
    /// <param name="prompt">The message to display above the list of options</param>
    /// <param name="items">The list of items to choose from</param>
    /// <returns>The zero-based index of the selected item</returns>
    /// <exception cref="ArgumentException">Thrown when the items list is empty</exception>
    /// <remarks>
    /// Displays numbered options and repeatedly prompts until a valid selection is made.
    /// If only one item exists, automatically returns its index without prompting.
    /// </remarks>
    private static int SelectFromList(string prompt, List<string> items)
    {
        if (!items.Any())
        {
            throw new ArgumentException("No items to select from");
        }

        if (items.Count == 1)
        {
            return 0;
        }

        while (true)
        {
            Console.WriteLine();
            Console.WriteLine(prompt);

            for (int i = 0; i < items.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {items[i]}");
            }

            Console.Write($"Selection (1-{items.Count}): ");
            var input = Console.ReadLine();

            if (int.TryParse(input, out int selection) &&
                selection >= 1 &&
                selection <= items.Count)
            {
                return selection - 1;
            }

            Console.WriteLine("Invalid selection, try again");
        }
    }

    /// <summary>
    /// Discovers running Power BI Desktop Analysis Services instances by querying WMI for msmdsrv.exe processes.
    /// For each instance found, extracts the workspace directory and port from command line parameters,
    /// then attempts to enumerate databases through ADOMD.NET connection.
    /// </summary>
    /// <returns>
    /// A collection of InstanceInfo objects containing details about found Power BI instances,
    /// including workspace paths, ports, and available databases.
    /// Returns an empty collection if no instances are found or if discovery fails.
    /// </returns>
    /// <exception cref="ManagementException">Thrown when WMI query fails.</exception>
    /// <exception cref="AdomdConnectionException">Thrown when unable to connect to Analysis Services instance.</exception>
    public async Task<IEnumerable<Core.InstanceInfo>> DiscoverInstances()
    {
        var processes = new List<Core.InstanceInfo>();

        try
        {
            List<ManagementObject> instances;
            try
            {
                instances = await Task.Run(() =>
                {
                    // Create a WMI query to find all Analysis Services processes
                    using var searcher = new ManagementObjectSearcher(
                        "SELECT * FROM Win32_Process WHERE Name = 'msmdsrv.exe'");
                    using var results = searcher.Get();
                    // Convert to list to avoid enumeration issues after searcher is disposed
                    return results.Cast<ManagementObject>().ToList();
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WMI query for msmdsrv.exe processes failed");
                return processes;
            }

            foreach (var process in instances)
            {
#pragma warning disable CA1416 // Validate platform compatibility
                var commandLine = process["CommandLine"]?.ToString() ?? string.Empty;
#pragma warning restore CA1416
                var workspaceDir = ExtractWorkspaceDir(commandLine);
                var port = ExtractPort(commandLine);

                if (string.IsNullOrEmpty(workspaceDir) || port == 0)
                {
                    continue;
                }

                // If workspace directory exists and contains .pbix file
                if (Directory.Exists(workspaceDir) &&
                    Directory.GetFiles(workspaceDir, "*.pbix").Any())
                {
                    var dbs = await EnumerateDatabasesAsync(port).ConfigureAwait(false);

                    if (dbs.Any())
                    {
                        processes.Add(new Core.InstanceInfo
                        {
                            WorkspacePath = workspaceDir,
                            Port = port,
                            Databases = dbs
                        });
                    }
                }

                process.Dispose();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to discover Power BI instances");
        }

        return processes;
    }

    /// <summary>
    /// Extracts the workspace directory from an msmdsrv.exe process command line.
    /// </summary>
    /// <param name="commandLine">The full command line of the msmdsrv.exe process</param>
    /// <returns>The workspace directory path if found; otherwise, an empty string</returns>
    /// <remarks>
    /// The workspace directory is specified with the -s parameter in the command line.
    /// Example format: -s "C:\Users\username\Documents\Power BI Desktop"
    /// </remarks>
    private static string ExtractWorkspaceDir(string commandLine)
    {
        var match = System.Text.RegularExpressions.Regex.Match(commandLine,
            @"-s\s*""([^""]+)""");
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    /// <summary>
    /// Extracts the port number from an msmdsrv.exe process command line.
    /// </summary>
    /// <param name="commandLine">The full command line of the msmdsrv.exe process</param>
    /// <returns>The port number if found and valid; otherwise, 0</returns>
    /// <remarks>
    /// The port is specified with the -p parameter in the command line.
    /// Example format: -p 12345
    /// </remarks>
    private static int ExtractPort(string commandLine)
    {
        var match = System.Text.RegularExpressions.Regex.Match(commandLine,
            @"-p\s+(\d+)");
        return match.Success && int.TryParse(match.Groups[1].Value, out var port)
            ? port
            : 0;
    }

    /// <summary>
    /// Enumerates all databases available on a specified Analysis Services port using ADOMD.NET.
    /// </summary>
    /// <param name="port">The port number where the Analysis Services instance is listening</param>
    /// <returns>A list of DatabaseInfo objects containing database names and IDs</returns>
    /// <remarks>
    /// This method attempts to connect to localhost on the specified port and query
    /// the DBSCHEMA_CATALOGS schema rowset to discover available databases.
    /// Connection failures are logged but do not throw exceptions.
    /// </remarks>
    private static async Task<List<Core.DatabaseInfo>> EnumerateDatabasesAsync(int port)
    {
        var dbs = new List<Core.DatabaseInfo>();
        var connectionString = $"Data Source=localhost:{port}";

        try
        {
            using var conn = new AdomdConnection(connectionString);
            await Task.Run(() => conn.Open()).ConfigureAwait(false);

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM $SYSTEM.DBSCHEMA_CATALOGS";

            using var reader = await Task.Run(() => cmd.ExecuteReader()).ConfigureAwait(false);
            while (await Task.Run(() => reader.Read()).ConfigureAwait(false))
            {
                var name = reader["CATALOG_NAME"]?.ToString();
                if (!string.IsNullOrEmpty(name))
                {
                    dbs.Add(new Core.DatabaseInfo
                    {
                        Id = name,
                        Name = name
                    });
                }
            }
        }
        catch (Exception ex)
        {
            // Failed to connect or enumerate - skip this instance
            Console.Error.WriteLine($"Failed to enumerate databases on port {port}: {ex.Message}");
        }

        return dbs;
    }

    /// <summary>
    /// Creates a new instance discovery service and discovers running Power BI instances.
    /// </summary>
    /// <returns>A collection of InstanceInfo objects for all discovered Power BI instances</returns>
    /// <remarks>
    /// This is a helper method that creates a new InstanceDiscovery with a NullLogger
    /// for use in static contexts where dependency injection is not available.
    /// </remarks>
    private static async Task<IEnumerable<Core.InstanceInfo>> DiscoverInstancesAsync()
    {
        var discovery = new InstanceDiscovery(
            new Microsoft.Extensions.Logging.Abstractions.NullLogger<InstanceDiscovery>());
        return await discovery.DiscoverInstances().ConfigureAwait(false);
    }
}
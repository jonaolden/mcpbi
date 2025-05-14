using System.Data;
using System.Text;
using Microsoft.AnalysisServices.AdomdClient;

/// <summary>
/// Provides functionality to discover running Power BI Desktop instances and their Analysis Services ports.
/// It also allows for interactive selection to update the .env file with connection details.
/// </summary>
public static class PbiInstanceDiscovery
{
    private static readonly string[] WorkspaceRoots = new[]
    {
        // Standard install
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Power BI Desktop", "AnalysisServicesWorkspaces"),
        // Store App install (legacy, but keep for completeness)
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages", "Microsoft.MicrosoftPowerBIDesktop_8wekyb3d8bbwe", "LocalState", "AnalysisServicesWorkspaces"),
        // Store App as per Python script
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Microsoft", "Power BI Desktop Store App", "AnalysisServicesWorkspaces")
    };

    /// <summary>
    /// Represents information about a discovered Power BI Desktop instance.
    /// </summary>
    public class InstanceInfo
    {
        /// <summary>
        /// Gets or sets the file system path to the Analysis Services workspace directory.
        /// </summary>
        public string? WorkspacePath { get; set; }
        /// <summary>
        /// Gets or sets the port number on which the Analysis Services instance is listening.
        /// </summary>
        public int Port { get; set; }
        /// <summary>
        /// Gets or sets the list of databases found within this instance.
        /// </summary>
        public List<DatabaseInfo> Databases { get; set; } = new();
    }

    /// <summary>
    /// Represents information about a database within a Power BI Desktop instance.
    /// </summary>
    public class DatabaseInfo
    {
        /// <summary>
        /// Gets or sets the ID (catalog name) of the database.
        /// </summary>
        public string? Id { get; set; }
        /// <summary>
        /// Gets or sets the friendly name of the database.
        /// </summary>
        public string? Name { get; set; }
    }

    /// <summary>
    /// Runs an interactive discovery process in the console.
    /// Allows the user to select a Power BI instance and database, then updates the .env file.
    /// </summary>
    public static void RunInteractive()
    {
        var instances = DiscoverInstances();
        if (instances.Count == 0)
        {
            Console.WriteLine("No Power BI Desktop instances found.");
            return;
        }

        // Prepare instance display strings
        var instanceDisplay = instances.Select((inst, i) =>
            $"Port: {inst.Port} - Databases: {string.Join(", ", inst.Databases.Select(d => d.Name))}"
        ).ToList();

        int instIdx = SelectFromList("Select a Power BI instance:", instanceDisplay);
        if (instIdx == -1)
        {
            Console.WriteLine("Selection cancelled.");
            return;
        }
        var instance = instances[instIdx];

        DatabaseInfo? db = null;
        if (instance.Databases.Count == 1)
        {
            db = instance.Databases[0];
        }
        else
        {
            var dbDisplay = instance.Databases.Select(d => $"{d.Name} ({d.Id})").ToList();
            int dbIdx = SelectFromList("Select a database:", dbDisplay);
            if (dbIdx == -1)
            {
                Console.WriteLine("Selection cancelled.");
                return;
            }
            db = instance.Databases[dbIdx];
        }

        var envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
        var envContent = $"PBI_PORT={instance.Port}\nPBI_DB_ID={db.Id}\n";
        File.WriteAllText(envPath, envContent, Encoding.UTF8);
        Console.WriteLine($".env updated: PBI_PORT={instance.Port}, PBI_DB_ID={db.Id}");
    }

    /// <summary>
    /// Displays a list of options and allows navigation with Up/Down arrows and selection with Enter.
    /// Returns the selected index, or -1 if cancelled (Escape).
    /// </summary>
    private static int SelectFromList(string prompt, List<string> items)
    {
        if (items.Count == 0)
        {
            Console.WriteLine("No items to select.");
            return -1;
        }

        int selected = 0;
        ConsoleKey key;

        Console.WriteLine(prompt);
        int initialListTop = Console.CursorTop;

        Console.CursorVisible = false;
        try
        {
            void Draw()
            {
                // Defensive: If initialListTop is already out of bounds, skip drawing
                if (initialListTop >= Console.BufferHeight)
                {
                    Console.Error.WriteLine("Error: Console buffer too small to display list.");
                    return;
                }
                for (int i = 0; i < items.Count; i++)
                {
                    int targetLine = initialListTop + i;
                    if (targetLine >= Console.BufferHeight)
                    {
                        // Skip drawing this item if it's outside the console buffer.
                        // The check at initialListTop >= Console.BufferHeight should handle most cases
                        // where the list itself is too long to even start drawing.
                        continue;
                    }
                    Console.SetCursorPosition(0, targetLine);
                    string prefix = (i == selected) ? "> " : "  ";
                    string lineToPrint = $"{prefix}{items[i]}";
                    Console.Write(lineToPrint);
                    int clearLen = Math.Max(0, Console.WindowWidth - Math.Min(Console.CursorLeft, Console.WindowWidth - 1));
                    if (clearLen > 0)
                        Console.Write(new string(' ', clearLen));
                }
                // Clear one line below the list to remove artifacts if list shrinks
                int afterListLine = initialListTop + items.Count;
                if (afterListLine < Console.BufferHeight)
                {
                    Console.SetCursorPosition(0, afterListLine);
                    Console.Write(new string(' ', Console.WindowWidth > 0 ? Console.WindowWidth - 1 : 0));
                }
            }

            Draw();

            while (true)
            {
                var keyInfo = Console.ReadKey(true);
                key = keyInfo.Key;
                if (key == ConsoleKey.UpArrow)
                {
                    if (selected > 0) selected--;
                    Draw();
                }
                else if (key == ConsoleKey.DownArrow)
                {
                    if (selected < items.Count - 1) selected++;
                    Draw();
                }
                else if (key == ConsoleKey.Enter)
                {
                    Console.CursorVisible = true;
                    int afterListLine = initialListTop + 1 + items.Count;
                    if (afterListLine < Console.BufferHeight)
                        Console.SetCursorPosition(0, afterListLine);
                    else
                        Console.SetCursorPosition(0, Console.BufferHeight - 1);
                    Console.WriteLine();
                    return selected;
                }
                else if (key == ConsoleKey.Escape)
                {
                    Console.CursorVisible = true;
                    int afterListLine = initialListTop + 1 + items.Count;
                    if (afterListLine < Console.BufferHeight)
                        Console.SetCursorPosition(0, afterListLine);
                    else
                        Console.SetCursorPosition(0, Console.BufferHeight - 1);
                    Console.WriteLine();
                    return -1;
                }
            }
        }
        finally
        {
            Console.CursorVisible = true;
        }
    }

    /// <summary>
    /// Discovers running Power BI Desktop instances by checking known workspace root paths
    /// for msmdsrv.port.txt files and enumerating their databases.
    /// </summary>
    /// <returns>A list of <see cref="InstanceInfo"/> objects representing discovered instances.</returns>
    public static List<InstanceInfo> DiscoverInstances()
    {
        var result = new List<InstanceInfo>();
        foreach (var rootPath in WorkspaceRoots)
        {
            if (!Directory.Exists(rootPath))
            {
                continue;
            }

            var workspaceDirs = Directory.GetDirectories(rootPath)
                .Where(d => Path.GetFileName(d).StartsWith("AnalysisServicesWorkspace", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var workspaceDir in workspaceDirs)
            {
                try
                {
                    var portFileCandidate1 = Path.Combine(workspaceDir, "msmdsrv.port.txt");
                    var portFileCandidate2 = Path.Combine(workspaceDir, "Data", "msmdsrv.port.txt");

                    string? foundPortFile = null;
                    foreach (var candidate in new[] { portFileCandidate1, portFileCandidate2 })
                    {
                        if (File.Exists(candidate))
                        {
                            foundPortFile = candidate;
                            break;
                        }
                    }
                    if (foundPortFile == null)
                    {
                        continue;
                    }

                    var portStr = File.ReadAllText(foundPortFile, Encoding.Unicode).Trim();
                    if (!int.TryParse(portStr, out int port))
                    {
                        continue;
                    }

                    var dbs = EnumerateDatabases(port);
                    if (dbs.Count == 0)
                    {
                        continue;
                    }

                    result.Add(new InstanceInfo
                    {
                        WorkspacePath = workspaceDir,
                        Port = port,
                        Databases = dbs
                    });
                }
                catch (Exception ex)
                {
                    // Log and continue, so one problematic workspace doesn't stop discovery of others.
                    Console.Error.WriteLine($"[{DateTime.UtcNow:O}] [DiscoverInstances] Error processing workspace directory '{workspaceDir}': {ex.GetType().Name} - {ex.Message}{Environment.NewLine}{ex.StackTrace}");
                }
            }
        }
        return result;
    }

    private static List<DatabaseInfo> EnumerateDatabases(int port)
    {
        var dbs = new List<DatabaseInfo>();
        var connStr = $"Data Source=localhost:{port};Integrated Security=SSPI;Provider=MSOLAP;";
        try
        {
            using var conn = new AdomdConnection(connStr);
            conn.Open();
            var ds = conn.GetSchemaDataSet("DBSCHEMA_CATALOGS", null);
            foreach (DataRow row in ds.Tables[0].Rows)
            {
                dbs.Add(new DatabaseInfo
                {
                    Id = row["CATALOG_NAME"] as string,
                    Name = row["CATALOG_NAME"] as string
                });
            }
        }
        catch (Exception ex)
        {
            // Intentionally ignoring connection errors during discovery, but log for diagnostics.
            Console.Error.WriteLine($"Error enumerating databases for port {port}: {ex.Message}");
        }
        return dbs;
    }
}

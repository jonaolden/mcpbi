namespace pbi_local_mcp.Core;

/// <summary>
/// Interface for discovering Power BI Desktop instances and their Analysis Services ports
/// </summary>
public interface IInstanceDiscovery
{
    /// <summary>
    /// Discovers all running Power BI Desktop instances
    /// </summary>
    /// <returns>A collection of discovered instances</returns>
    Task<IEnumerable<InstanceInfo>> DiscoverInstances();
}

/// <summary>
/// Contains information about a discovered Power BI Desktop instance
/// </summary>
public class InstanceInfo
{
    /// <summary>
    /// Gets or sets the workspace path where the instance is running
    /// </summary>
    public string? WorkspacePath { get; set; }

    /// <summary>
    /// Gets or sets the port number on which Analysis Services is listening
    /// </summary>
    public int Port { get; set; }

    /// <summary>
    /// Gets or sets the collection of databases in this instance
    /// </summary>
    public List<DatabaseInfo> Databases { get; set; } = new();
}

/// <summary>
/// Contains information about a database within a Power BI Desktop instance
/// </summary>
public class DatabaseInfo
{
    /// <summary>
    /// Gets or sets the database ID (catalog name)
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Gets or sets the friendly name of the database
    /// </summary>
    public string? Name { get; set; }
}
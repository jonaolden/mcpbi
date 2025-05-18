namespace pbi_local_mcp.Core;

/// <summary>
/// Interface for server operations in the Power BI Model Context Protocol
/// </summary>
public interface IServer
{
    /// <summary>
    /// Gets the list of available model instances
    /// </summary>
    /// <returns>A collection of model instances</returns>
    Task<IEnumerable<ModelInstance>> GetInstances();

    /// <summary>
    /// Connects to a specific model instance
    /// </summary>
    /// <param name="instanceId">The ID of the instance to connect to</param>
    /// <returns>True if connection was successful, false otherwise</returns>
    Task<bool> ConnectToInstance(string instanceId);
}

/// <summary>
/// Represents a Power BI model instance
/// </summary>
public class ModelInstance
{
    /// <summary>
    /// Gets or sets the unique identifier for the instance
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name of the instance
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the connection port for the instance
    /// </summary>
    public int Port { get; set; }
}
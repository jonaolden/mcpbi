using System;

namespace pbi_local_mcp.Configuration;

/// <summary>
/// Configuration settings for Power BI connection
/// </summary>
public class PowerBiConfig
{
    /// <summary>
    /// Gets or sets the port number for connecting to the Power BI instance
    /// </summary>
    public string Port { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the database ID (catalog name) to connect to
    /// </summary>
    public string DbId { get; set; } = string.Empty;

    /// <summary>
    /// Validates the configuration settings
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when required settings are missing</exception>
    public void Validate()
    {
        if (string.IsNullOrEmpty(Port))
            throw new InvalidOperationException("PBI_PORT not set");
        if (string.IsNullOrEmpty(DbId))
            throw new InvalidOperationException("PBI_DB_ID not set");
    }
}
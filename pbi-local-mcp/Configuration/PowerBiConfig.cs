namespace pbi_local_mcp.Configuration;

/// <summary>
/// Configuration settings for Power BI connection
/// </summary>
public class PowerBiConfig
{
    private string _port = string.Empty;
    private string _dbId = string.Empty;
    
    /// <summary>
    /// Gets or sets the port number for connecting to the Power BI instance
    /// </summary>
    public string Port
    {
        get => _port;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Port cannot be null or empty");
                
            if (!int.TryParse(value, out var port) || port < 1 || port > 65535)
                throw new ArgumentException($"Invalid port number: {value}. Must be between 1 and 65535.");
                
            _port = value;
        }
    }

    /// <summary>
    /// Gets or sets the database ID (catalog name) to connect to
    /// </summary>
    public string DbId
    {
        get => _dbId;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Database ID cannot be null or empty");
                
            if (value.Length > 100) // Reasonable limit
                throw new ArgumentException("Database ID too long");
                
            _dbId = value;
        }
    }

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
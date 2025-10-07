namespace pbi_local_mcp.Configuration;

/// <summary>
/// Configuration options for command-line arguments
/// </summary>
public class CommandLineOptions
{
    /// <summary>
    /// Gets or sets the PowerBI port number from command-line argument
    /// </summary>
    public string? Port { get; set; }

    /// <summary>
    /// Validates the command-line options
    /// </summary>
    /// <returns>True if valid, false otherwise</returns>
    public bool IsValid()
    {
        if (string.IsNullOrWhiteSpace(Port))
            return false;

        if (!int.TryParse(Port, out var port) || port < 1 || port > 65535)
            return false;

        return true;
    }
}

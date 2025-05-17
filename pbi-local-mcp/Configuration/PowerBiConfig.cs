using System;

namespace pbi_local_mcp.Configuration
{
    /// <summary>
    /// Configuration settings for Power BI connection
    /// </summary>
    public class PowerBiConfig
    {
        public string Port { get; set; } = string.Empty;
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
}
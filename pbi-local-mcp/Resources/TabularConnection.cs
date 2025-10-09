using System.Data;

using Microsoft.AnalysisServices.AdomdClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using pbi_local_mcp.Configuration;
using pbi_local_mcp.Core;

namespace pbi_local_mcp;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
public enum QueryType
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    DAX,
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    DMV
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}

/// <summary>
/// Implements connection and query execution for PowerBI's tabular model
/// </summary>
public class TabularConnection : ITabularConnection, IDisposable
{
    private readonly ILogger<TabularConnection> _logger = null!;
    private readonly string _connectionString;
    private readonly AdomdConnection _connection;
    private bool _disposed;

    // Metadata / diagnostics
    private readonly int _port;
    private readonly string? _databaseId;
    private readonly Version _assemblyVersion = typeof(TabularConnection).Assembly.GetName().Version ?? new Version(0, 0, 0, 0);
    /// <summary>
    /// Gets the numeric port the connection was initialized with (0 if unknown or not parsed).
    /// </summary>
    public int Port => _port;

    /// <summary>
    /// Gets the database (catalog) identifier for the connected model, or null if not provided.
    /// </summary>
    public string? DatabaseId => _databaseId;

    /// <summary>
    /// UTC timestamp when this connection instance was created.
    /// </summary>
    public DateTime StartupUtc { get; } = DateTime.UtcNow;

    /// <summary>
    /// Version of the assembly implementing <see cref="TabularConnection"/>.
    /// </summary>
    public Version AssemblyVersion => _assemblyVersion;

    // Schema summary caching
    private volatile SchemaSummary? _schemaCache;
    private DateTime _schemaCacheExpiry;
    private static readonly TimeSpan SchemaCacheTtl = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Default command timeout in seconds
    /// </summary>
    private const int DefaultCommandTimeout = 60;

    /// <summary>
    /// Initializes a new instance of the TabularConnection class with configuration settings
    /// </summary>
    /// <param name="config">Power BI configuration settings</param>
    /// <param name="logger">Logger instance for tracing.</param>
    /// <exception cref="ArgumentNullException">Thrown when configuration is null</exception>
    /// <exception cref="InvalidOperationException">Thrown when configuration values are missing or invalid</exception>
    public TabularConnection(PowerBiConfig config, ILogger<TabularConnection> logger)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));

        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        config.Validate();
        _connectionString = $"Data Source=localhost:{config.Port};Initial Catalog={config.DbId}";
        _connection = new AdomdConnection(_connectionString);

        if (!int.TryParse(config.Port, out _port) || _port < 0) _port = 0;
        _databaseId = string.IsNullOrWhiteSpace(config.DbId) ? null : config.DbId;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="TabularConnection"/> using the default null logger.
    /// </summary>
    /// <param name="config">Configuration settings for the Power BI connection.</param>
    public TabularConnection(PowerBiConfig config)
        : this(config, NullLogger<TabularConnection>.Instance)
    {
    }

    /// <summary>
    /// Initializes a new instance of the TabularConnection class with explicit connection parameters
    /// </summary>
    /// <param name="logger">Logger for diagnostic information</param>
    /// <param name="port">Port number for the Power BI instance</param>
    /// <param name="dbId">Database ID (catalog name) to connect to</param>
    /// <exception cref="ArgumentNullException">Thrown when required parameters are null</exception>
    /// <exception cref="ArgumentException">Thrown when required parameters are empty</exception>
    public TabularConnection(ILogger<TabularConnection> logger, string port, string dbId)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (string.IsNullOrEmpty(port))
            throw new ArgumentException("Port cannot be null or empty", nameof(port));

        if (string.IsNullOrEmpty(dbId))
            throw new ArgumentException("Database ID cannot be null or empty", nameof(dbId));

        _connectionString = $"Data Source=localhost:{port};Initial Catalog={dbId}";
        _connection = new AdomdConnection(_connectionString);

        if (!int.TryParse(port, out _port) || _port < 0) _port = 0;
        _databaseId = dbId;
    }

    /// <summary>
    /// Initializes a new instance of the TabularConnection class with a pre-configured connection
    /// Used primarily for testing with mock connections
    /// </summary>
    /// <param name="logger">Logger for diagnostic information</param>
    /// <param name="connection">Pre-configured ADOMD connection</param>
    /// <exception cref="ArgumentNullException">Thrown when required parameters are null</exception>
    protected internal TabularConnection(ILogger<TabularConnection> logger, AdomdConnection connection)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _connectionString = connection.ConnectionString;

        // Attempt to parse port & catalog from connection string (best-effort)
        try
        {
            var parts = _connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var p in parts)
            {
                if (p.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
                {
                    var ds = p.Substring("Data Source=".Length);
                    var colonIdx = ds.LastIndexOf(':');
                    if (colonIdx > -1 && int.TryParse(ds[(colonIdx + 1)..], out var portParsed))
                        _port = portParsed;
                }
                else if (p.StartsWith("Initial Catalog=", StringComparison.OrdinalIgnoreCase))
                {
                    _databaseId = p.Substring("Initial Catalog=".Length);
                }
            }
        }
        catch
        {
            _port = 0;
            _databaseId = null;
        }
    }

    /// <inheritdoc/>
    public virtual async Task<IEnumerable<Dictionary<string, object?>>> ExecAsync(string query, QueryType queryType = QueryType.DAX)
    {
        if (string.IsNullOrEmpty(query))
            throw new ArgumentNullException(nameof(query), "Query cannot be null or empty");

        // queryType parameter is available here if specific logic is needed,
        // but AdomdClient typically uses CommandType.Text for both DAX and DMVs.
        // The distinction might be more for logging or higher-level logic if any.
        _logger.LogDebug("Executing query ({QueryType}): {Query}", queryType, query);

        await EnsureConnectionOpenAsync();

        try
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = query;
            cmd.CommandType = CommandType.Text; // Standard for both DAX and DMVs via AdomdClient
            cmd.CommandTimeout = DefaultCommandTimeout;

            var results = new List<Dictionary<string, object?>>();
            using var reader = await Task.Run(() => cmd.ExecuteReader()).ConfigureAwait(false);

            while (await Task.Run(() => reader.Read()).ConfigureAwait(false))
            {
                var row = new Dictionary<string, object?>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var name = reader.GetName(i);
                    if (reader.IsDBNull(i))
                    {
                        row[name] = DBNull.Value;
                    }
                    else
                    {
                        var value = reader.GetValue(i);
                        
                        // Check if this is a nested rowset (common in DMV queries like PARAMETERINFO)
                        if (value is AdomdDataReader nestedReader)
                        {
                            var nestedResults = new List<Dictionary<string, object?>>();
                            while (nestedReader.Read())
                            {
                                var nestedRow = new Dictionary<string, object?>();
                                for (int j = 0; j < nestedReader.FieldCount; j++)
                                {
                                    var nestedName = nestedReader.GetName(j);
                                    var nestedValue = nestedReader.IsDBNull(j) ? null : nestedReader.GetValue(j);
                                    nestedRow[nestedName] = nestedValue;
                                }
                                nestedResults.Add(nestedRow);
                            }
                            row[name] = nestedResults;
                        }
                        else
                        {
                            row[name] = value;
                        }
                    }
                }
                results.Add(row);
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing {QueryType} query: {Query}", queryType, query);

            // Create enhanced error message with query details for better error reporting
            var enhancedMessage = DaxTools.CreateDetailedErrorMessage(ex, query, query, queryType);

            // Use standard Exception instead of McpException for application-level database errors
            // McpException is only for MCP protocol-level errors, not database query failures
            throw new Exception(enhancedMessage, ex);
        }
    }

    /// <inheritdoc/>
    public virtual async Task<IEnumerable<Dictionary<string, object?>>> ExecAsync(string query, QueryType queryType, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(query))
            throw new ArgumentNullException(nameof(query), "Query cannot be null or empty");

        _logger.LogDebug("Executing query ({QueryType}) with cancellation: {Query}", queryType, query);

        cancellationToken.ThrowIfCancellationRequested();
        await EnsureConnectionOpenAsync(cancellationToken);

        try
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = query;
            cmd.CommandType = CommandType.Text;
            cmd.CommandTimeout = DefaultCommandTimeout;

            var results = new List<Dictionary<string, object?>>();

            using var reader = await Task.Run(() => cmd.ExecuteReader(), cancellationToken).ConfigureAwait(false);

            while (await Task.Run(() => reader.Read(), cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var row = new Dictionary<string, object?>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var name = reader.GetName(i);
                    var value = reader.IsDBNull(i) ? DBNull.Value : reader.GetValue(i);
                    row[name] = value;
                }
                results.Add(row);
            }

            return results;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error executing {QueryType} query: {Query}", queryType, query);

            // Create enhanced error message with query details for better error reporting
            var enhancedMessage = DaxTools.CreateDetailedErrorMessage(ex, query, query, queryType);

            // Use standard Exception instead of McpException for application-level database errors
            // McpException is only for MCP protocol-level errors, not database query failures
            throw new Exception(enhancedMessage, ex);
        }
    }

    /// <inheritdoc/>
    public virtual async Task<IEnumerable<Dictionary<string, object?>>> ExecInfoAsync(string func, string filterExpr)
    {
        if (string.IsNullOrEmpty(func))
            throw new ArgumentNullException(nameof(func), "Function name cannot be null or empty");

        if (func.Contains(";") || func.Contains("--") || func.Contains("/*") || func.Contains("*/"))
            throw new ArgumentException("Function name contains invalid characters", nameof(func));

        string query;
        if (string.IsNullOrEmpty(filterExpr))
        {
            query = $"SELECT * FROM ${func}";
        }
        else
        {
            FilterExpressionValidator.ValidateFilterExpression(filterExpr);
            query = $"SELECT * FROM ${func} WHERE {filterExpr}";
        }
        // ExecInfoAsync implies a DMV query
        return await ExecAsync(query, QueryType.DMV);
    }

    /// <inheritdoc/>
    public virtual async Task<IEnumerable<Dictionary<string, object?>>> ExecInfoAsync(string func, string filterExpr, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(func))
            throw new ArgumentNullException(nameof(func), "Function name cannot be null or empty");

        if (func.Contains(";") || func.Contains("--") || func.Contains("/*") || func.Contains("*/"))
            throw new ArgumentException("Function name contains invalid characters", nameof(func));

        string query;
        if (string.IsNullOrEmpty(filterExpr))
        {
            query = $"SELECT * FROM ${func}";
        }
        else
        {
            FilterExpressionValidator.ValidateFilterExpression(filterExpr);
            query = $"SELECT * FROM ${func} WHERE {filterExpr}";
        }
        // ExecInfoAsync implies a DMV query
        return await ExecAsync(query, QueryType.DMV, cancellationToken);
    }

    /// <summary>
    /// Ensures that the connection is open, opening it if necessary
    /// </summary>
    /// <returns>A task representing the asynchronous operation</returns>
    /// <exception cref="InvalidOperationException">Thrown when the connection cannot be opened</exception>
    private async Task EnsureConnectionOpenAsync()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(TabularConnection), "Connection has been disposed");

        try
        {
            if (_connection.State != ConnectionState.Open)
            {
                await Task.Run(() => _connection.Open()).ConfigureAwait(false);
            }

            if (_connection.State != ConnectionState.Open)
                throw new InvalidOperationException("Failed to open connection to PowerBI instance");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error connecting to PowerBI instance: {ConnectionString}",
                _connectionString.Replace(";", ", "));
            throw;
        }
    }

    /// <summary>
    /// Ensures that the connection is open, opening it if necessary, with cancellation support
    /// </summary>
    /// <param name="cancellationToken">Token to monitor for cancellation requests</param>
    /// <returns>A task representing the asynchronous operation</returns>
    /// <exception cref="InvalidOperationException">Thrown when the connection cannot be opened</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled</exception>
    private async Task EnsureConnectionOpenAsync(CancellationToken cancellationToken)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(TabularConnection), "Connection has been disposed");

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            if (_connection.State != ConnectionState.Open)
            {
                await Task.Run(() => _connection.Open(), cancellationToken).ConfigureAwait(false);
            }

            if (_connection.State != ConnectionState.Open)
                throw new InvalidOperationException("Failed to open connection to PowerBI instance");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error connecting to PowerBI instance: {ConnectionString}",
                _connectionString.Replace(";", ", "));
            throw;
        }
    }

    /// <summary>
    /// Disposes resources used by the connection
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the unmanaged resources used by the connection and optionally releases the managed resources
    /// </summary>
    /// <param name="disposing">True to release both managed and unmanaged resources; false to release only unmanaged resources</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                try
                {
                    if (_connection != null)
                    {
                        if (_connection.State == ConnectionState.Open)
                        {
                            _connection.Close();
                        }
                        _connection.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error disposing connection");
                }
            }
            _disposed = true;
        }
    }

    /// <summary>
    /// Finalizer to ensure resource cleanup
    /// </summary>
    ~TabularConnection()
    {
        Dispose(false);
    }
    /// <summary>
    /// Discovers available databases on the Power BI instance at the specified port
    /// </summary>
    /// <param name="port">Port number for the Power BI instance</param>
    /// <param name="logger">Logger instance for diagnostic information</param>
    /// <returns>List of available database names</returns>
    /// <exception cref="ArgumentException">Thrown when port is invalid</exception>
    /// <exception cref="InvalidOperationException">Thrown when no databases are found or connection fails</exception>
    public static async Task<List<string>> DiscoverDatabasesAsync(string port, ILogger<TabularConnection>? logger = null)
    {
        if (string.IsNullOrWhiteSpace(port))
            throw new ArgumentException("Port cannot be null or empty", nameof(port));

        if (!int.TryParse(port, out var portNumber) || portNumber < 1 || portNumber > 65535)
            throw new ArgumentException($"Invalid port number: {port}", nameof(port));

        logger = logger ?? NullLogger<TabularConnection>.Instance;

        // Use a temporary connection without specifying Initial Catalog to discover databases
        var connectionString = $"Data Source=localhost:{port}";

        try
        {
            using var connection = new AdomdConnection(connectionString);
            logger.LogDebug("Attempting to discover databases on port {Port}", port);

            await Task.Run(() => connection.Open()).ConfigureAwait(false);

            // Query for available catalogs/databases (using same format as InstanceDiscovery)
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT * FROM $SYSTEM.DBSCHEMA_CATALOGS";
            cmd.CommandType = CommandType.Text;
            cmd.CommandTimeout = 30; // Shorter timeout for discovery

            var databases = new List<string>();
            using var reader = await Task.Run(() => cmd.ExecuteReader()).ConfigureAwait(false);

            while (await Task.Run(() => reader.Read()).ConfigureAwait(false))
            {
                var catalogName = reader["CATALOG_NAME"]?.ToString();
                if (!string.IsNullOrWhiteSpace(catalogName))
                {
                    databases.Add(catalogName);
                }
            }

            logger.LogDebug("Discovered {Count} databases on port {Port}: {Databases}",
                databases.Count, port, string.Join(", ", databases));

            if (!databases.Any())
            {
                throw new InvalidOperationException($"No databases found on Power BI instance at port {port}");
            }

            return databases;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to discover databases on port {Port}", port);
            throw new InvalidOperationException($"Could not discover databases on port {port}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Returns a lightweight cached summary of model schema counts (tables, measures, columns).
    /// Uses DMV queries and caches results for a short interval.
    /// </summary>
    public async Task<SchemaSummary> GetSchemaSummaryAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var cache = _schemaCache;
        if (cache != null && now < _schemaCacheExpiry)
        {
            return cache;
        }

        // Simple double-checked locking
        lock (this)
        {
            cache = _schemaCache;
            if (cache != null && now < _schemaCacheExpiry)
                return cache;
        }

        try
        {
            ct.ThrowIfCancellationRequested();

            int tableCount = await GetCountAsync("SELECT COUNT(*) AS C FROM $SYSTEM.TMSCHEMA_TABLES", ct);
            int measureCount = await GetCountAsync("SELECT COUNT(*) AS C FROM $SYSTEM.TMSCHEMA_MEASURES", ct);
            int columnCount = await GetCountAsync("SELECT COUNT(*) AS C FROM $SYSTEM.TMSCHEMA_COLUMNS", ct);

            DateTime? lastRefreshed = null;
            try
            {
                var dbRows = await ExecAsync("SELECT LASTPROCESSED FROM $SYSTEM.TMSCHEMA_DATABASES", QueryType.DMV, ct);
                var first = dbRows.FirstOrDefault();
                if (first != null &&
                    first.TryGetValue("LASTPROCESSED", out var v) &&
                    v != null &&
                    v != DBNull.Value)
                {
                    // Convert & normalize to UTC
                    lastRefreshed = Convert.ToDateTime(v, System.Globalization.CultureInfo.InvariantCulture).ToUniversalTime();
                }
            }
            catch
            {
                // Non-fatal: ignore if DMV not available or fails
            }

            var summary = new SchemaSummary(tableCount, measureCount, columnCount, lastRefreshed);

            lock (this)
            {
                _schemaCache = summary;
                _schemaCacheExpiry = DateTime.UtcNow + SchemaCacheTtl;
            }

            return summary;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "Failed to build schema summary");
            // Return empty summary to avoid throwing for metadata endpoints
            var fallback = new SchemaSummary(0, 0, 0, null);
            lock (this)
            {
                _schemaCache = fallback;
                _schemaCacheExpiry = DateTime.UtcNow + TimeSpan.FromSeconds(10);
            }
            return fallback;
        }
    }

    private async Task<int> GetCountAsync(string dmvQuery, CancellationToken ct)
    {
        var rows = await ExecAsync(dmvQuery, QueryType.DMV, ct);
        var first = rows.FirstOrDefault();
        if (first == null) return 0;
        // Try common keys
        object? value = null;
        if (first.Count == 1)
        {
            value = first.Values.FirstOrDefault();
        }
        else if (first.TryGetValue("C", out var cVal))
        {
            value = cVal;
        }
        if (value == null || value == DBNull.Value) return 0;
        try
        {
            return Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Creates a TabularConnection with automatic database discovery
    /// Connects to the Power BI instance and selects the first available database
    /// </summary>
    /// <param name="port">Port number for the Power BI instance</param>
    /// <param name="logger">Logger instance for diagnostic information</param>
    /// <returns>TabularConnection configured with the discovered database</returns>
    /// <exception cref="ArgumentException">Thrown when port is invalid</exception>
    /// <exception cref="InvalidOperationException">Thrown when no databases are found or connection fails</exception>
    public static async Task<TabularConnection> CreateWithDiscoveryAsync(string port, ILogger<TabularConnection>? logger = null)
    {
        logger = logger ?? NullLogger<TabularConnection>.Instance;

        var databases = await DiscoverDatabasesAsync(port, logger);

        // Use the first available database
        var selectedDatabase = databases.First();
        logger.LogInformation("Auto-selected database '{Database}' from {Count} available databases on port {Port}",
            selectedDatabase, databases.Count, port);

        return new TabularConnection(logger, port, selectedDatabase);
    }
}

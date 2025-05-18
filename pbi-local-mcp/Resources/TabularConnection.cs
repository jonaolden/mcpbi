using Microsoft.Extensions.Logging;
using pbi_local_mcp.Core;
using pbi_local_mcp.Configuration;
using Microsoft.AnalysisServices.AdomdClient;
using System.Data;
using Microsoft.Extensions.Logging.Abstractions;

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
    }    /// <inheritdoc/>
         /// <summary>
         /// Executes a DAX query and returns the results
         /// </summary>
         /// <param name="dax">The DAX query to execute</param>
         /// <returns>A collection of query results as dictionaries</returns>
         /// <exception cref="ArgumentNullException">Thrown when the DAX query is null</exception>
         /// <exception cref="InvalidOperationException">Thrown when the connection is not available</exception>
         /// <exception cref="Exception">Propagates any exceptions from the query execution</exception>
    // Overload for interface compatibility
    public virtual async Task<IEnumerable<Dictionary<string, object?>>> ExecAsync(string query)
    {
        return await ExecAsync(query, QueryType.DAX);
    }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public virtual async Task<IEnumerable<Dictionary<string, object?>>> ExecAsync(string query, QueryType queryType = QueryType.DAX)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        if (string.IsNullOrEmpty(query))
            throw new ArgumentNullException(nameof(query), "Query cannot be null or empty");

        await EnsureConnectionOpenAsync();

        try
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = query;
            cmd.CommandType = CommandType.Text;
            cmd.CommandTimeout = DefaultCommandTimeout;

            var results = new List<Dictionary<string, object?>>();
            using var reader = await Task.Run(() => cmd.ExecuteReader());

            while (await Task.Run(() => reader.Read()))
            {
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing query: {Query}", query);
            throw;
        }
    }

    /// <inheritdoc/>
    /// <summary>
    /// Executes a DAX query with cancellation support and returns the results
    /// </summary>
    /// <param name="dax">The DAX query to execute</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests</param>
    /// <returns>A collection of query results as dictionaries</returns>
    /// <exception cref="ArgumentNullException">Thrown when the DAX query is null</exception>
    /// <exception cref="InvalidOperationException">Thrown when the connection is not available</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled</exception>
    /// <exception cref="Exception">Propagates any exceptions from the query execution</exception>
    public virtual async Task<IEnumerable<Dictionary<string, object?>>> ExecAsync(string dax, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(dax))
            throw new ArgumentNullException(nameof(dax), "DAX query cannot be null or empty");

        cancellationToken.ThrowIfCancellationRequested();
        await EnsureConnectionOpenAsync(cancellationToken);

        try
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = dax;
            cmd.CommandType = CommandType.Text;
            cmd.CommandTimeout = DefaultCommandTimeout;

            var results = new List<Dictionary<string, object?>>();

            using var reader = await Task.Run(() => cmd.ExecuteReader(), cancellationToken);

            while (await Task.Run(() => reader.Read(), cancellationToken))
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
            _logger.LogError(ex, "Error executing DAX query: {Query}", dax);
            throw;
        }
    }    /// <inheritdoc/>
         /// <summary>
         /// Executes a DAX info function with a filter and returns the results
         /// </summary>
         /// <param name="func">The name of the INFO function to execute</param>
         /// <param name="filterExpr">Filter expression to apply</param>
         /// <returns>A collection of query results as dictionaries</returns>
         /// <exception cref="ArgumentNullException">Thrown when func is null</exception>
         /// <exception cref="ArgumentException">Thrown when func contains invalid characters</exception>
    public virtual async Task<IEnumerable<Dictionary<string, object?>>> ExecInfoAsync(string func, string filterExpr)
    {
        if (string.IsNullOrEmpty(func))
            throw new ArgumentNullException(nameof(func), "Function name cannot be null or empty");

        // Sanitize input to prevent injection
        if (func.Contains(";") || func.Contains("--") || func.Contains("/*") || func.Contains("*/"))
            throw new ArgumentException("Function name contains invalid characters", nameof(func));

        string query;
        if (string.IsNullOrEmpty(filterExpr))
        {
            query = $"SELECT * FROM ${func}";
        }
        else
        {
            // Basic sanitization for the filter expression
            if (filterExpr.Contains(";") && filterExpr.Contains("--"))
                throw new ArgumentException("Filter expression contains invalid characters", nameof(filterExpr));

            query = $"SELECT * FROM ${func} WHERE {filterExpr}";
        }

        return await ExecAsync(query);
    }

    /// <summary>
    /// Executes a DAX info function with a filter and returns the results with cancellation support
    /// </summary>
    /// <param name="func">The name of the INFO function to execute</param>
    /// <param name="filterExpr">Filter expression to apply</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests</param>
    /// <returns>A collection of query results as dictionaries</returns>
    /// <exception cref="ArgumentNullException">Thrown when func is null</exception>
    /// <exception cref="ArgumentException">Thrown when func contains invalid characters</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled</exception>
    public virtual async Task<IEnumerable<Dictionary<string, object?>>> ExecInfoAsync(string func, string filterExpr, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(func))
            throw new ArgumentNullException(nameof(func), "Function name cannot be null or empty");

        // Sanitize input to prevent injection
        if (func.Contains(";") || func.Contains("--") || func.Contains("/*") || func.Contains("*/"))
            throw new ArgumentException("Function name contains invalid characters", nameof(func));

        string query;
        if (string.IsNullOrEmpty(filterExpr))
        {
            query = $"SELECT * FROM ${func}";
        }
        else
        {
            // Basic sanitization for the filter expression
            if (filterExpr.Contains(";") && filterExpr.Contains("--"))
                throw new ArgumentException("Filter expression contains invalid characters", nameof(filterExpr));

            query = $"SELECT * FROM ${func} WHERE {filterExpr}";
        }

        return await ExecAsync(query, cancellationToken);
    }    /// <summary>
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
                // Create a new connection if needed for better connection pooling
                await Task.Run(() => _connection.Open());
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
                // Create a new connection if needed for better connection pooling
                await Task.Run(() => _connection.Open(), cancellationToken);
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
}


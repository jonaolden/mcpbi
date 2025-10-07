using System;

namespace pbi_local_mcp.Core;

/// <summary>
/// Interface for connecting to and executing queries against a tabular model.
/// Extended with lightweight model/server metadata accessors.
/// </summary>
public interface ITabularConnection
{
    /// <summary>
    /// Executes a query (DAX or DMV) and returns the results.
    /// </summary>
    /// <param name="query">The query to execute.</param>
    /// <param name="queryType">The type of query (DAX or DMV).</param>
    /// <returns>A collection of query results as dictionaries.</returns>
    Task<IEnumerable<Dictionary<string, object?>>> ExecAsync(
        string query,
        QueryType queryType = QueryType.DAX);

    /// <summary>
    /// Executes a query (DAX or DMV) with cancellation support and returns the results.
    /// </summary>
    /// <param name="query">The query to execute.</param>
    /// <param name="queryType">The type of query (DAX or DMV).</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A collection of query results as dictionaries.</returns>
    Task<IEnumerable<Dictionary<string, object?>>> ExecAsync(
        string query,
        QueryType queryType,
        CancellationToken cancellationToken);

    /// <summary>
    /// Executes a DAX info (DMV surface) function with optional filter and returns the results.
    /// </summary>
    /// <param name="func">The name of the INFO function to execute (without leading $).</param>
    /// <param name="filterExpr">Filter expression to apply (optional / may be empty).</param>
    /// <returns>A collection of query results as dictionaries.</returns>
    Task<IEnumerable<Dictionary<string, object?>>> ExecInfoAsync(
        string func,
        string filterExpr);

    /// <summary>
    /// Executes a DAX info (DMV surface) function with optional filter and cancellation support.
    /// </summary>
    /// <param name="func">The name of the INFO function to execute (without leading $).</param>
    /// <param name="filterExpr">Filter expression to apply (optional / may be empty).</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A collection of query results as dictionaries.</returns>
    Task<IEnumerable<Dictionary<string, object?>>> ExecInfoAsync(
        string func,
        string filterExpr,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets the numeric port the connection was initialized with (0 if invalid or unknown).
    /// </summary>
    int Port { get; }

    /// <summary>
    /// Gets the database (catalog) identifier if provided; otherwise null.
    /// </summary>
    string? DatabaseId { get; }

    /// <summary>
    /// Gets the UTC timestamp when this connection object was created.
    /// </summary>
    DateTime StartupUtc { get; }

    /// <summary>
    /// Gets the assembly version of the component implementing the connection.
    /// </summary>
    Version AssemblyVersion { get; }

    /// <summary>
    /// Returns a lightweight cached summary of model schema counts.
    /// Implementations should internally cache for a short interval (e.g. 30s).
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Schema summary containing table, measure and column counts.</returns>
    Task<SchemaSummary> GetSchemaSummaryAsync(CancellationToken ct = default);
}

/// <summary>
/// Lightweight summary counts for the connected model schema.
/// </summary>
/// <param name="TableCount">Number of tables.</param>
/// <param name="MeasureCount">Number of measures.</param>
/// <param name="ColumnCount">Number of columns.</param>
/// <param name="LastRefreshedUtc">Last refresh time if available; otherwise null.</param>
public record SchemaSummary(
    int TableCount,
    int MeasureCount,
    int ColumnCount,
    DateTime? LastRefreshedUtc);

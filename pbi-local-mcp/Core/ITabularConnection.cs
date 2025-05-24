namespace pbi_local_mcp.Core;

/// <summary>
/// Interface for connecting to and executing queries against a tabular model
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
        QueryType queryType = QueryType.DAX); // Added QueryType, default to DAX

    /// <summary>
    /// Executes a query (DAX or DMV) with cancellation support and returns the results.
    /// </summary>
    /// <param name="query">The query to execute.</param>
    /// <param name="queryType">The type of query (DAX or DMV).</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A collection of query results as dictionaries.</returns>
    Task<IEnumerable<Dictionary<string, object?>>> ExecAsync(
        string query,
        QueryType queryType, // Added QueryType
        CancellationToken cancellationToken);

    /// <summary>
    /// Executes a DAX info function with filter and returns the results
    /// </summary>
    /// <param name="func">The name of the INFO function to execute</param>
    /// <param name="filterExpr">Filter expression to apply</param>
    /// <returns>A collection of query results as dictionaries</returns>
    Task<IEnumerable<Dictionary<string, object?>>> ExecInfoAsync(
        string func,
        string filterExpr);

    /// <summary>
    /// Executes a DAX info function with filter and cancellation support and returns the results
    /// </summary>
    /// <param name="func">The name of the INFO function to execute</param>
    /// <param name="filterExpr">Filter expression to apply</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests</param>
    /// <returns>A collection of query results as dictionaries</returns>
    Task<IEnumerable<Dictionary<string, object?>>> ExecInfoAsync(
        string func,
        string filterExpr,
        CancellationToken cancellationToken);
}

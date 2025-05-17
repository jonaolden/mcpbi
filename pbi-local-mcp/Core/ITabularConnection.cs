using System.Collections.Generic;
using System.Threading.Tasks;

namespace pbi_local_mcp.Core
{
    /// <summary>
    /// Interface for executing DAX queries against a tabular database connection
    /// </summary>
    public interface ITabularConnection
    {
        /// <summary>
        /// Executes a DAX query using the default connection
        /// </summary>
        Task<IEnumerable<Dictionary<string, object?>>> ExecAsync(string dax);

        /// <summary>
        /// Executes an info function query with optional filter
        /// </summary>
        Task<IEnumerable<Dictionary<string, object?>>> ExecInfoAsync(string func, string? filterExpr);

        /// <summary>
        /// Executes a DAX query using the default connection
        /// </summary>
        Task<IEnumerable<Dictionary<string, object?>>> ExecDaxAsync(string dax);
    }
}
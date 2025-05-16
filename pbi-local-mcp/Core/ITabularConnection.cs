using System.Collections.Generic;
using System.Threading.Tasks;

namespace pbi_local_mcp.Core
{
    public interface ITabularConnection
    {
        Task<IEnumerable<Dictionary<string, object?>>> RunQueryAsync(string connectionString, string dax);
        // Add other relevant method signatures as needed
    }
}
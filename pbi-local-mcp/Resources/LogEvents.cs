using Microsoft.Extensions.Logging;

namespace pbi_local_mcp.Resources;

/// <summary>
/// Central EventId catalog for structured logging to avoid scattered literals.
/// Add new events here when introducing additional logging categories.
/// </summary>
internal static class LogEvents
{
    // Resource provider events (1100-1199 reserved)
    internal static readonly EventId ResourceRequest = new(1100, "ResourceRequest");
    internal static readonly EventId CacheMiss = new(1102, "CacheMiss");
    internal static readonly EventId ResourceError = new(1105, "ResourceError");
}

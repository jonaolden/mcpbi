using System.ComponentModel;

using ModelContextProtocol.Server;

using pbi_local_mcp.Resources;

namespace pbi_local_mcp.Tools;

/// <summary>
/// Fallback introspection tools exposing resource provider contents when fluent
/// registration (.WithResourceProvider<T>()) is unavailable in the current SDK.
/// Remove once native resource provider registration is supported.
/// </summary>
[McpServerToolType]
public sealed class PowerBiResourceIntrospectionTools
{
    private readonly PowerBiResourceProvider _provider;

    /// <summary>
    /// Initializes a new instance of the <see cref="PowerBiResourceIntrospectionTools"/> class.
    /// </summary>
    public PowerBiResourceIntrospectionTools(PowerBiResourceProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    /// <summary>
    /// Lists all resource URIs exposed by the Power BI resource provider (fallback path).
    /// </summary>
    [McpServerTool, Description("List Power BI resource URIs exposed by the fallback provider.")]
    public async Task<object> ListPowerBiResources()
    {
        var list = await _provider.ListResourcesAsync().ConfigureAwait(false);
        // Project to simple anonymous objects for tool friendliness
        return list.Select(r => new { r.Uri, r.Description });
    }

    /// <summary>
    /// Reads a specific Power BI resource by URI using the underlying provider (fallback path).
    /// </summary>
    /// <param name="uri">Resource URI returned from ListPowerBiResources.</param>
    [McpServerTool, Description("Read a specific Power BI resource payload by URI (use ListPowerBiResources first).")]
    public async Task<object> ReadPowerBiResource([Description("Resource URI to fetch.")] string uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
            throw new ArgumentException("URI cannot be empty.", nameof(uri));

        return await _provider.ReadResourceAsync(uri).ConfigureAwait(false);
    }
}

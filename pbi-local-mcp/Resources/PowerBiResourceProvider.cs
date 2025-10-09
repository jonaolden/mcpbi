using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using pbi_local_mcp.Configuration;
using pbi_local_mcp.Core;

namespace pbi_local_mcp.Resources;

/// <summary>
/// Provides Power BI specific metadata and predefined DAX template resources to MCP clients.
/// Resources exposed (URIs):
///  - powerbi://server/info        (basic connection/server metadata)
///  - powerbi://instances          (discovered local Power BI Desktop instances - cached 5s)
///  - powerbi://schema/summary     (lightweight model schema counts - internally cached in ITabularConnection)
///  - dax://templates/*            (static DAX template descriptors)
/// </summary>
public sealed class PowerBiResourceProvider
{
    // EventIds centralized in LogEvents

    private readonly ITabularConnection _tabular;
    private readonly IInstanceDiscovery? _instanceDiscovery;
    private readonly ILogger<PowerBiResourceProvider> _logger;
    private readonly IMemoryCache _cache;
    private readonly ServerInfo _serverInfo;

    private static readonly IReadOnlyDictionary<string, DaxTemplateDescriptor> _templates = BuildTemplates();

    /// <summary>
    /// Initializes a new instance of the <see cref="PowerBiResourceProvider"/> class.
    /// </summary>
    /// <param name="tabularConnection">Connection used to query the tabular model for metadata and schema information.</param>
    /// <param name="logger">Logger instance for diagnostic messages.</param>
    /// <param name="memoryCache">Memory cache used for short-lived resource caching.</param>
    /// <param name="instanceDiscovery">Optional instance discovery service for enumerating local Power BI instances.</param>
    /// <param name="config">Optional Power BI configuration options.</param>
    public PowerBiResourceProvider(
        ITabularConnection tabularConnection,
        ILogger<PowerBiResourceProvider> logger,
        IMemoryCache memoryCache,
        IInstanceDiscovery? instanceDiscovery = null,
        IOptions<PowerBiConfig>? config = null)
    {
        _tabular = tabularConnection ?? throw new ArgumentNullException(nameof(tabularConnection));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        _instanceDiscovery = instanceDiscovery;

        _serverInfo = new ServerInfo(
            _tabular.Port,
            _tabular.DatabaseId,
            _tabular.StartupUtc,
            _tabular.AssemblyVersion);
    }

    /// <summary>
    /// Lists available resource descriptors exposed by this provider.
    /// </summary>
    public Task<IEnumerable<ResourceDescriptor>> ListResourcesAsync(CancellationToken ct = default)
    {
        _logger.LogDebug(LogEvents.ResourceRequest, "Listing resources");
        var list = new List<ResourceDescriptor>
        {
            new("powerbi://server/info",        "Power BI connection/server metadata"),
            new("powerbi://instances",          "Discovered local Power BI Desktop instances (cached 5s)"),
            new("powerbi://schema/summary",     "Lightweight model schema summary (tables/measures/columns)"),
            new("powerbi://functions/interface-names", "List of available INTERFACE_NAME values for functions (cached)")
        };
        list.AddRange(_templates.Keys.Select(k => new ResourceDescriptor(k, _templates[k].Description)));
        return Task.FromResult<IEnumerable<ResourceDescriptor>>(list);
    }

    /// <summary>
    /// Reads a specific resource by URI.
    /// </summary>
    /// <param name="uri">Resource URI.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<object> ReadResourceAsync(string uri, CancellationToken ct = default)
    {
        _logger.LogDebug(LogEvents.ResourceRequest, "ReadResource start Uri={Uri}", uri);

        try
        {
            return uri switch
            {
                "powerbi://server/info" => _serverInfo,
                "powerbi://instances" => await GetInstancesAsync(ct).ConfigureAwait(false),
                "powerbi://schema/summary" => await _tabular.GetSchemaSummaryAsync(ct).ConfigureAwait(false),
                "powerbi://functions/interface-names" => await GetFunctionInterfaceNamesAsync(ct).ConfigureAwait(false),
                _ when _templates.ContainsKey(uri) => _templates[uri],
                _ => throw new Exception($"[ResourceProvider] Unknown resource URI: {uri}")
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(LogEvents.ResourceError, ex, "Failed reading resource {Uri}", uri);
            throw new Exception($"[ResourceProvider] Failed to read resource '{uri}': {ex.Message}", ex);
        }
    }

    private async Task<IEnumerable<InstanceInfo>> GetInstancesAsync(CancellationToken ct)
    {
        if (_instanceDiscovery == null)
        {
            return Array.Empty<InstanceInfo>();
        }

        const string cacheKey = "PowerBiResourceProvider.Instances";
        if (_cache.TryGetValue(cacheKey, out IEnumerable<InstanceInfo>? cached) && cached != null)
        {
            return cached;
        }

        _logger.LogDebug(LogEvents.CacheMiss, "Instance list cache miss");
        var instances = await _instanceDiscovery.DiscoverInstances().ConfigureAwait(false);
 
        _cache.Set(cacheKey, instances, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(5)
        });
 
        return instances;
    }
 
    private const string InterfaceNamesCacheKey = "PowerBiResourceProvider.InterfaceNames";
 
    private async Task<IEnumerable<string>> GetFunctionInterfaceNamesAsync(CancellationToken ct)
    {
        if (_cache.TryGetValue(InterfaceNamesCacheKey, out IEnumerable<string>? cached) && cached != null)
        {
            return cached;
        }
 
        _logger.LogDebug(LogEvents.CacheMiss, "Function interface names cache miss");
 
        // Use DAX INFO function to retrieve interface name values
        var daxQuery = "EVALUATE DISTINCT(SELECTCOLUMNS(INFO.FUNCTIONS(), \"INTERFACE_NAME\", [INTERFACE_NAME]))";
        var raw = await _tabular.ExecAsync(daxQuery, QueryType.DAX, ct).ConfigureAwait(false);
 
        var list = raw
            .Where(r => r.TryGetValue("INTERFACE_NAME", out var v) && v != null)
            .Select(r => r["INTERFACE_NAME"]!.ToString()!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s)
            .ToList();
 
        _cache.Set(InterfaceNamesCacheKey, list, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60)
        });
 
        return list;
    }

    // ----- Static template catalog -----

    private static IReadOnlyDictionary<string, DaxTemplateDescriptor> BuildTemplates()
    {
        var dict = new Dictionary<string, DaxTemplateDescriptor>(StringComparer.OrdinalIgnoreCase);

        dict["dax://templates/topn"] = new DaxTemplateDescriptor(
            "dax://templates/topn",
            "Top N template",
            "Return top N rows ordered by an expression",
            "EVALUATE TOPN(@N, @Table, @OrderExpression)",
            "EVALUATE TOPN(10, 'Sales', [Total Sales])",
            new[]
            {
                new DaxTemplateParameter("N","Number of rows to return", true, "10"),
                new DaxTemplateParameter("Table","Source table reference", true, "'Sales'"),
                new DaxTemplateParameter("OrderExpression","Ordering expression (descending)", true, "[Total Sales]")
            });

        dict["dax://templates/distinct"] = new DaxTemplateDescriptor(
            "dax://templates/distinct",
            "Distinct values template",
            "Return distinct values from a column",
            "EVALUATE DISTINCT(SELECTCOLUMNS(@Table, \"Value\", @Column))",
            "EVALUATE DISTINCT(SELECTCOLUMNS('Customers', \"Value\", 'Customers'[Country]))",
            new[]
            {
                new DaxTemplateParameter("Table","Source table", true, "'Customers'"),
                new DaxTemplateParameter("Column","Column reference", true, "'Customers'[Country]")
            });

        dict["dax://templates/calculate"] = new DaxTemplateDescriptor(
            "dax://templates/calculate",
            "CALCULATE with filter",
            "Apply a filter to an expression using CALCULATE",
            "EVALUATE ROW(\"Value\", CALCULATE(@Expression, @Filter))",
            "EVALUATE ROW(\"Value\", CALCULATE([Total Sales], 'Calendar'[Year]=2024))",
            new[]
            {
                new DaxTemplateParameter("Expression","Base measure or expression", true, "[Total Sales]"),
                new DaxTemplateParameter("Filter","Filter predicate", true, "'Calendar'[Year]=2024")
            });

        dict["dax://templates/filter"] = new DaxTemplateDescriptor(
            "dax://templates/filter",
            "FILTER wrapper",
            "Filter a table expression with a predicate",
            "EVALUATE FILTER(@Table, @Predicate)",
            "EVALUATE FILTER('Sales', [Total Sales] > 1000)",
            new[]
            {
                new DaxTemplateParameter("Table","Table expression", true, "'Sales'"),
                new DaxTemplateParameter("Predicate","Filter predicate", true, "[Total Sales] > 1000")
            });

        dict["dax://templates/summarize"] = new DaxTemplateDescriptor(
            "dax://templates/summarize",
            "Summarize template",
            "Summarize a table by grouping columns and aggregating measures",
            "EVALUATE SUMMARIZE(@Table, @GroupByColumns, \"Value\", @MeasureExpression)",
            "EVALUATE SUMMARIZE('Sales', 'Sales'[Region], \"Value\", [Total Sales])",
            new[]
            {
                new DaxTemplateParameter("Table","Base table", true, "'Sales'"),
                new DaxTemplateParameter("GroupByColumns","Grouping columns (comma-separated)", true, "'Sales'[Region]"),
                new DaxTemplateParameter("MeasureExpression","Aggregation measure/expression", true, "[Total Sales]")
            });

        return dict;
    }

    // ----- Records -----

    /// <summary>Descriptor for a resource URI.</summary>
    public record ResourceDescriptor(string Uri, string Description);

    /// <summary>Power BI server / connection metadata.</summary>
    public record ServerInfo(int Port, string? DatabaseId, DateTime StartupUtc, Version AssemblyVersion);

    /// <summary>Template parameter descriptor.</summary>
    public record DaxTemplateParameter(string Name, string Description, bool Required, string? DefaultValue);

    /// <summary>DAX template descriptor.</summary>
    public record DaxTemplateDescriptor(
        string Uri,
        string Name,
        string Description,
        string Template,
        string Sample,
        IReadOnlyList<DaxTemplateParameter> Parameters);
}

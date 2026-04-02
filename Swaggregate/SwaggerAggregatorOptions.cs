namespace Swaggregate;

public class SwaggerAggregatorOptions
{
    /// <summary>Route prefix where the portal is served. Default: "api-docs".</summary>
    public string RoutePrefix { get; set; } = "api-docs";

    /// <summary>How long to cache fetched specs in memory. Default: 5 minutes.</summary>
    public int CacheTtlMinutes { get; set; } = 5;

    /// <summary>Title shown in the portal header.</summary>
    public string Title { get; set; } = "API Documentation";

    /// <summary>List of swagger.json endpoints to aggregate.</summary>
    public List<SwaggerAggregatorEndpoint> Endpoints { get; } = new();

    public SwaggerAggregatorOptions AddEndpoint(string name, string url, string? pathPrefix = null)
    {
        Endpoints.Add(new SwaggerAggregatorEndpoint { Name = name, Url = url, PathPrefix = pathPrefix });
        return this;
    }
}

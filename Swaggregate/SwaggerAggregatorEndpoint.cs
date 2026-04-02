namespace Swaggregate;

public class SwaggerAggregatorEndpoint
{
    /// <summary>Display name for this service in the portal.</summary>
    public required string Name { get; init; }

    /// <summary>Full URL to the swagger.json endpoint.</summary>
    public required string Url { get; init; }

    /// <summary>Optional path prefix to prepend to all paths from this spec.</summary>
    public string? PathPrefix { get; init; }
}

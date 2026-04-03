namespace Swaggregate.Models;

public class ServiceGroup
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Version { get; set; }
    public string SourceUrl { get; set; } = string.Empty;
    /// <summary>The base URL used for "Try it out" requests (e.g. https://api.example.com/v1). Null if it cannot be determined from the spec.</summary>
    public string? BaseUrl { get; set; }
    /// <summary>The path prefix configured for this endpoint, if any.</summary>
    public string? PathPrefix { get; set; }
    public bool FetchError { get; set; }
    public string? FetchErrorMessage { get; set; }
    public List<EndpointInfo> Endpoints { get; set; } = new();
    public List<TagInfo> Tags { get; set; } = new();
}

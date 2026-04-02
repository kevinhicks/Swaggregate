namespace Swaggregate.Services;

public interface ISpecFetcher
{
    /// <summary>
    /// Fetches a spec from <paramref name="url"/>.
    /// Returns the raw content (JSON or YAML), the HTTP Content-Type, and any error message.
    /// </summary>
    Task<(bool success, string? content, string? contentType, string? error)> FetchAsync(
        string url, int cacheTtlMinutes = 5, CancellationToken ct = default);
}

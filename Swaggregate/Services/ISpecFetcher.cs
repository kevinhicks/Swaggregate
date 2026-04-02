using Swaggregate.Models;

namespace Swaggregate.Services;

public interface ISpecFetcher
{
    Task<(bool success, string? json, string? error)> FetchAsync(string url, int cacheTtlMinutes = 5, CancellationToken ct = default);
}

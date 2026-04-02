using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Swaggregate.Services;

public class SpecFetcher : ISpecFetcher
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<SpecFetcher> _logger;

    public SpecFetcher(IHttpClientFactory httpClientFactory, IMemoryCache cache, ILogger<SpecFetcher> logger)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _logger = logger;
    }

    public async Task<(bool success, string? content, string? contentType, string? error)> FetchAsync(string url, int cacheTtlMinutes = 5, CancellationToken ct = default)
    {
        var cacheKey = $"swagger_spec_{url}";

        if (_cache.TryGetValue(cacheKey, out CachedSpec? cached) && cached is not null)
        {
            _logger.LogDebug("Serving spec from cache: {Url}", url);
            return (cached.Success, cached.Content, cached.ContentType, cached.Error);
        }

        _logger.LogDebug("Fetching spec: {Url}", url);
        var client = _httpClientFactory.CreateClient("Swaggregate");

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(DefaultTimeout);

            var response = await client.GetAsync(url, cts.Token);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync(cts.Token);
            var contentType = response.Content.Headers.ContentType?.MediaType;

            var result = new CachedSpec { Success = true, Content = content, ContentType = contentType };
            _cache.Set(cacheKey, result, TimeSpan.FromMinutes(cacheTtlMinutes));
            return (true, content, contentType, null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch spec from {Url}", url);
            var error = new CachedSpec { Success = false, Error = ex.Message };
            // Cache failures briefly to avoid hammering a down service
            _cache.Set(cacheKey, error, TimeSpan.FromSeconds(30));
            return (false, null, null, ex.Message);
        }
    }

    private sealed class CachedSpec
    {
        public bool Success { get; init; }
        public string? Content { get; init; }
        public string? ContentType { get; init; }
        public string? Error { get; init; }
    }
}

using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Swaggregate.Services;

namespace Swaggregate.Middleware;

public class SwaggerAggregatorMiddleware
{
    private readonly RequestDelegate _next;
    private readonly SwaggerAggregatorOptions _options;
    private readonly SpecAggregator _aggregator;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SwaggerAggregatorMiddleware> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public SwaggerAggregatorMiddleware(
        RequestDelegate next,
        SwaggerAggregatorOptions options,
        SpecAggregator aggregator,
        IHttpClientFactory httpClientFactory,
        ILogger<SwaggerAggregatorMiddleware> logger)
    {
        _next = next;
        _options = options;
        _aggregator = aggregator;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var prefix = _options.RoutePrefix.Trim('/');
        var path = context.Request.Path.Value?.TrimStart('/') ?? "";

        // Match /{prefix} or /{prefix}/
        if (path.Equals(prefix, StringComparison.OrdinalIgnoreCase) ||
            path.Equals(prefix + "/", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.Redirect($"/{prefix}/index.html");
            return;
        }

        // Match /{prefix}/proxy — server-side proxy for "Try it out"
        if (path.Equals(prefix + "/proxy", StringComparison.OrdinalIgnoreCase) &&
            context.Request.Method == "POST")
        {
            await ServeProxyAsync(context);
            return;
        }

        // Match /{prefix}/specs — returns aggregated JSON
        if (path.Equals(prefix + "/specs", StringComparison.OrdinalIgnoreCase))
        {
            await ServeSpecsAsync(context);
            return;
        }

        // Match /{prefix}/{asset} — serves embedded static files
        if (path.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase))
        {
            var assetName = path[(prefix.Length + 1)..];
            if (!string.IsNullOrEmpty(assetName))
            {
                await ServeEmbeddedFileAsync(context, assetName);
                return;
            }
        }

        await _next(context);
    }

    private async Task ServeProxyAsync(HttpContext context)
    {
        ProxyRequest? req;
        try
        {
            req = await JsonSerializer.DeserializeAsync<ProxyRequest>(
                context.Request.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                context.RequestAborted);
        }
        catch
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync("{\"error\":\"Invalid request body\"}");
            return;
        }

        if (req is null || string.IsNullOrWhiteSpace(req.Url) || string.IsNullOrWhiteSpace(req.Method))
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync("{\"error\":\"url and method are required\"}");
            return;
        }

        // Security: only allow forwarding to configured service origins
        if (!Uri.TryCreate(req.Url, UriKind.Absolute, out var targetUri))
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync("{\"error\":\"Invalid target URL\"}");
            return;
        }

        var allowedOrigins = _options.Endpoints
            .Select(e => Uri.TryCreate(e.Url, UriKind.Absolute, out var u)
                ? $"{u.Scheme}://{u.Authority}"
                : null)
            .Where(o => o is not null)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var targetOrigin = $"{targetUri.Scheme}://{targetUri.Authority}";
        if (!allowedOrigins.Contains(targetOrigin))
        {
            context.Response.StatusCode = 403;
            await context.Response.WriteAsync("{\"error\":\"Target URL is not an allowed service origin\"}");
            return;
        }

        try
        {
            var client = _httpClientFactory.CreateClient("Swaggregate");
            using var httpReq = new HttpRequestMessage(new HttpMethod(req.Method.ToUpperInvariant()), req.Url);

            // Forward custom headers (skip hop-by-hop)
            if (req.Headers is not null)
            {
                foreach (var (key, value) in req.Headers)
                    httpReq.Headers.TryAddWithoutValidation(key, value);
            }

            if (!string.IsNullOrEmpty(req.Body))
            {
                var mediaType = req.Headers?.GetValueOrDefault("Content-Type") ?? "application/json";
                httpReq.Content = new StringContent(req.Body, Encoding.UTF8, mediaType);
            }

            using var httpResp = await client.SendAsync(httpReq, context.RequestAborted);
            var body = await httpResp.Content.ReadAsStringAsync(context.RequestAborted);

            var responseHeaders = httpResp.Headers
                .Concat(httpResp.Content.Headers)
                .ToDictionary(h => h.Key.ToLowerInvariant(), h => string.Join(", ", h.Value));

            var proxyResult = new
            {
                status = (int)httpResp.StatusCode,
                statusText = httpResp.ReasonPhrase ?? httpResp.StatusCode.ToString(),
                headers = responseHeaders,
                body
            };

            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(proxyResult, JsonOpts));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Proxy request to {Url} failed", req.Url);
            context.Response.StatusCode = 502;
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(new { error = ex.Message }, JsonOpts));
        }
    }

    private sealed class ProxyRequest
    {
        public string Url { get; init; } = string.Empty;
        public string Method { get; init; } = string.Empty;
        public Dictionary<string, string>? Headers { get; init; }
        public string? Body { get; init; }
    }

    private async Task ServeSpecsAsync(HttpContext context)
    {
        try
        {
            var spec = await _aggregator.AggregateAsync(_options, context.RequestAborted);
            var json = JsonSerializer.Serialize(spec, JsonOpts);
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error aggregating specs");
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync("{\"error\":\"Failed to aggregate specs\"}");
        }
    }

    private static async Task ServeEmbeddedFileAsync(HttpContext context, string fileName)
    {
        var assembly = typeof(SwaggerAggregatorMiddleware).Assembly;
        // Embedded resources use dots as separators; EmbeddedUI/index.html → SwaggerAggregator.EmbeddedUI.index.html
        var resourceName = $"Swaggregate.EmbeddedUI.{fileName.Replace('/', '.')}";

        var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            context.Response.StatusCode = 404;
            return;
        }

        context.Response.ContentType = GetContentType(fileName);
        using (stream)
        {
            await stream.CopyToAsync(context.Response.Body);
        }
    }

    private static string GetContentType(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch
    {
        ".html" => "text/html; charset=utf-8",
        ".js" => "application/javascript; charset=utf-8",
        ".css" => "text/css; charset=utf-8",
        ".json" => "application/json",
        ".svg" => "image/svg+xml",
        ".ico" => "image/x-icon",
        _ => "application/octet-stream"
    };
}

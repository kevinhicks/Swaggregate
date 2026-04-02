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
        ILogger<SwaggerAggregatorMiddleware> logger)
    {
        _next = next;
        _options = options;
        _aggregator = aggregator;
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

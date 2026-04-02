using System.Text.Json;
using Microsoft.Extensions.Logging;
using Swaggregate.Models;

namespace Swaggregate.Services;

/// <summary>
/// Fetches and normalizes OpenAPI 2.0 (Swagger) and 3.0 specs into the unified model.
/// </summary>
public class SpecAggregator
{
    private readonly ISpecFetcher _fetcher;
    private readonly ILogger<SpecAggregator> _logger;

    public SpecAggregator(ISpecFetcher fetcher, ILogger<SpecAggregator> logger)
    {
        _fetcher = fetcher;
        _logger = logger;
    }

    public async Task<AggregatedSpec> AggregateAsync(
        SwaggerAggregatorOptions options, CancellationToken ct = default)
    {
        var tasks = options.Endpoints
            .Select(ep => ProcessEndpointAsync(ep, options.CacheTtlMinutes, ct))
            .ToList();

        var groups = await Task.WhenAll(tasks);

        return new AggregatedSpec
        {
            Title = options.Title,
            Services = groups.ToList(),
            FetchedAt = DateTime.UtcNow
        };
    }

    private async Task<ServiceGroup> ProcessEndpointAsync(
        SwaggerAggregatorEndpoint endpoint, int cacheTtlMinutes, CancellationToken ct)
    {
        var group = new ServiceGroup
        {
            Name = endpoint.Name,
            SourceUrl = endpoint.Url
        };

        var (success, content, contentType, error) = await _fetcher.FetchAsync(endpoint.Url, cacheTtlMinutes, ct);

        if (!success || content is null)
        {
            group.FetchError = true;
            group.FetchErrorMessage = error ?? "Unknown error";
            return group;
        }

        try
        {
            var json = IsYaml(endpoint.Url, contentType, content)
                ? YamlConverter.ToJson(content)
                : content;

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Detect spec version
            if (root.TryGetProperty("openapi", out var openApiProp))
                ParseOpenApi3(root, group, endpoint.PathPrefix);
            else if (root.TryGetProperty("swagger", out _))
                ParseSwagger2(root, group, endpoint.PathPrefix);
            else
            {
                group.FetchError = true;
                group.FetchErrorMessage = "Unrecognized spec format (neither OpenAPI 3.x nor Swagger 2.0)";
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse spec from {Url}", endpoint.Url);
            group.FetchError = true;
            group.FetchErrorMessage = $"Parse error: {ex.Message}";
        }

        return group;
    }

    // -------------------------------------------------------------------------
    // OpenAPI 3.x parser
    // -------------------------------------------------------------------------

    private static void ParseOpenApi3(JsonElement root, ServiceGroup group, string? pathPrefix)
    {
        if (root.TryGetProperty("info", out var info))
        {
            group.Description = info.TryGetProperty("description", out var d) ? d.GetString() : null;
            group.Version = info.TryGetProperty("version", out var v) ? v.GetString() : null;
        }

        if (root.TryGetProperty("tags", out var tagsEl))
            group.Tags = ParseTags(tagsEl);

        if (!root.TryGetProperty("paths", out var paths))
            return;

        foreach (var pathProp in paths.EnumerateObject())
        {
            var path = (pathPrefix ?? "") + pathProp.Name;
            foreach (var methodProp in pathProp.Value.EnumerateObject())
            {
                var method = methodProp.Name.ToUpperInvariant();
                if (!IsHttpMethod(method)) continue;

                var op = methodProp.Value;
                var endpoint = new EndpointInfo
                {
                    Method = method,
                    Path = path,
                    Summary = TryGetString(op, "summary"),
                    Description = TryGetString(op, "description"),
                    OperationId = TryGetString(op, "operationId"),
                    Deprecated = op.TryGetProperty("deprecated", out var dep) && dep.GetBoolean()
                };

                if (op.TryGetProperty("tags", out var tags))
                    endpoint.Tags = tags.EnumerateArray().Select(t => t.GetString() ?? "").Where(t => t != "").ToList();

                if (op.TryGetProperty("parameters", out var parameters))
                    endpoint.Parameters = ParseParameters3(parameters);

                if (op.TryGetProperty("requestBody", out var reqBody))
                    endpoint.RequestBody = ParseRequestBody3(reqBody);

                if (op.TryGetProperty("responses", out var responses))
                    endpoint.Responses = ParseResponses3(responses);

                group.Endpoints.Add(endpoint);
            }
        }
    }

    private static List<ParameterInfo> ParseParameters3(JsonElement parameters)
    {
        var result = new List<ParameterInfo>();
        foreach (var p in parameters.EnumerateArray())
        {
            var param = new ParameterInfo
            {
                Name = TryGetString(p, "name") ?? "",
                In = TryGetString(p, "in") ?? "",
                Description = TryGetString(p, "description"),
                Required = p.TryGetProperty("required", out var req) && req.GetBoolean()
            };

            if (p.TryGetProperty("schema", out var schema))
            {
                param.Type = TryGetString(schema, "type");
                param.Format = TryGetString(schema, "format");
                param.Schema = JsonElementToObject(schema);
            }

            result.Add(param);
        }
        return result;
    }

    private static RequestBodyInfo? ParseRequestBody3(JsonElement reqBody)
    {
        var info = new RequestBodyInfo
        {
            Description = TryGetString(reqBody, "description"),
            Required = reqBody.TryGetProperty("required", out var req) && req.GetBoolean()
        };

        if (reqBody.TryGetProperty("content", out var content))
        {
            foreach (var mediaType in content.EnumerateObject())
            {
                var schema = new SchemaInfo();
                if (mediaType.Value.TryGetProperty("schema", out var schemaEl))
                {
                    schema.Ref = TryGetString(schemaEl, "$ref");
                    schema.Type = TryGetString(schemaEl, "type");
                    schema.RawSchema = JsonElementToObject(schemaEl);
                }
                info.Content[mediaType.Name] = schema;
            }
        }

        return info;
    }

    private static List<ResponseInfo> ParseResponses3(JsonElement responses)
    {
        var result = new List<ResponseInfo>();
        foreach (var resp in responses.EnumerateObject())
        {
            var info = new ResponseInfo
            {
                StatusCode = resp.Name,
                Description = TryGetString(resp.Value, "description")
            };

            if (resp.Value.TryGetProperty("content", out var content))
            {
                foreach (var mediaType in content.EnumerateObject())
                {
                    var schema = new SchemaInfo();
                    if (mediaType.Value.TryGetProperty("schema", out var schemaEl))
                    {
                        schema.Ref = TryGetString(schemaEl, "$ref");
                        schema.Type = TryGetString(schemaEl, "type");
                        schema.RawSchema = JsonElementToObject(schemaEl);
                    }
                    info.Content[mediaType.Name] = schema;
                }
            }

            result.Add(info);
        }
        return result;
    }

    // -------------------------------------------------------------------------
    // Swagger 2.0 parser
    // -------------------------------------------------------------------------

    private static void ParseSwagger2(JsonElement root, ServiceGroup group, string? pathPrefix)
    {
        if (root.TryGetProperty("info", out var info))
        {
            group.Description = info.TryGetProperty("description", out var d) ? d.GetString() : null;
            group.Version = info.TryGetProperty("version", out var v) ? v.GetString() : null;
        }

        if (root.TryGetProperty("tags", out var tagsEl))
            group.Tags = ParseTags(tagsEl);

        if (!root.TryGetProperty("paths", out var paths))
            return;

        foreach (var pathProp in paths.EnumerateObject())
        {
            var path = (pathPrefix ?? "") + pathProp.Name;
            foreach (var methodProp in pathProp.Value.EnumerateObject())
            {
                var method = methodProp.Name.ToUpperInvariant();
                if (!IsHttpMethod(method)) continue;

                var op = methodProp.Value;
                var endpoint = new EndpointInfo
                {
                    Method = method,
                    Path = path,
                    Summary = TryGetString(op, "summary"),
                    Description = TryGetString(op, "description"),
                    OperationId = TryGetString(op, "operationId"),
                    Deprecated = op.TryGetProperty("deprecated", out var dep) && dep.GetBoolean()
                };

                if (op.TryGetProperty("tags", out var tags))
                    endpoint.Tags = tags.EnumerateArray().Select(t => t.GetString() ?? "").Where(t => t != "").ToList();

                if (op.TryGetProperty("parameters", out var parameters))
                    endpoint.Parameters = ParseParameters2(parameters);

                if (op.TryGetProperty("responses", out var responses))
                    endpoint.Responses = ParseResponses2(responses);

                group.Endpoints.Add(endpoint);
            }
        }
    }

    private static List<ParameterInfo> ParseParameters2(JsonElement parameters)
    {
        var result = new List<ParameterInfo>();
        foreach (var p in parameters.EnumerateArray())
        {
            var inVal = TryGetString(p, "in") ?? "";
            var param = new ParameterInfo
            {
                Name = TryGetString(p, "name") ?? "",
                In = inVal,
                Description = TryGetString(p, "description"),
                Required = p.TryGetProperty("required", out var req) && req.GetBoolean()
            };

            // In Swagger 2.0, body params have a "schema"; others have type/format directly
            if (inVal == "body" && p.TryGetProperty("schema", out var schema))
            {
                param.Type = TryGetString(schema, "type") ?? "$ref";
                param.Schema = JsonElementToObject(schema);
            }
            else
            {
                param.Type = TryGetString(p, "type");
                param.Format = TryGetString(p, "format");
            }

            result.Add(param);
        }
        return result;
    }

    private static List<ResponseInfo> ParseResponses2(JsonElement responses)
    {
        var result = new List<ResponseInfo>();
        foreach (var resp in responses.EnumerateObject())
        {
            var info = new ResponseInfo
            {
                StatusCode = resp.Name,
                Description = TryGetString(resp.Value, "description")
            };

            if (resp.Value.TryGetProperty("schema", out var schema))
            {
                info.Content["application/json"] = new SchemaInfo
                {
                    Ref = TryGetString(schema, "$ref"),
                    Type = TryGetString(schema, "type"),
                    RawSchema = JsonElementToObject(schema)
                };
            }

            result.Add(info);
        }
        return result;
    }

    // -------------------------------------------------------------------------
    // Shared helpers
    // -------------------------------------------------------------------------

    private static List<TagInfo> ParseTags(JsonElement tagsEl)
    {
        var result = new List<TagInfo>();
        foreach (var t in tagsEl.EnumerateArray())
        {
            result.Add(new TagInfo
            {
                Name = TryGetString(t, "name") ?? "",
                Description = TryGetString(t, "description")
            });
        }
        return result;
    }

    private static bool IsYaml(string url, string? contentType, string content)
    {
        // 1. URL extension
        var path = new Uri(url, UriKind.RelativeOrAbsolute).IsAbsoluteUri
            ? new Uri(url).AbsolutePath
            : url;
        if (path.EndsWith(".yml", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase))
            return true;

        // 2. Content-Type header
        if (contentType is not null &&
            (contentType.Contains("yaml", StringComparison.OrdinalIgnoreCase) ||
             contentType.Contains("x-yaml", StringComparison.OrdinalIgnoreCase)))
            return true;

        // 3. Content sniff — JSON always starts with { or [
        var trimmed = content.TrimStart();
        return trimmed.Length > 0 && trimmed[0] != '{' && trimmed[0] != '[';
    }

    private static string? TryGetString(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var val) && val.ValueKind == JsonValueKind.String
            ? val.GetString()
            : null;

    private static bool IsHttpMethod(string m) =>
        m is "GET" or "POST" or "PUT" or "PATCH" or "DELETE" or "HEAD" or "OPTIONS";

    private static object? JsonElementToObject(JsonElement el)
    {
        return JsonSerializer.Deserialize<object>(el.GetRawText());
    }
}

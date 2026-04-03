using Microsoft.Extensions.Logging.Abstractions;
using Swaggregate.Models;
using Swaggregate.Services;
using Xunit;

namespace Swaggregate.Tests;

public class SpecAggregatorTests
{
    // ── Sample spec strings ──────────────────────────────────────────────────

    private const string OpenApi3Json = """
        {
          "openapi": "3.0.0",
          "info": { "title": "Test API", "version": "2.0", "description": "A test service" },
          "tags": [{ "name": "Users", "description": "User operations" }],
          "paths": {
            "/users": {
              "get": {
                "summary": "List users",
                "operationId": "listUsers",
                "tags": ["Users"],
                "parameters": [
                  { "name": "page", "in": "query", "required": false, "schema": { "type": "integer" } }
                ],
                "responses": { "200": { "description": "OK" } }
              },
              "post": {
                "summary": "Create user",
                "operationId": "createUser",
                "requestBody": {
                  "required": true,
                  "content": { "application/json": { "schema": { "type": "object" } } }
                },
                "responses": {
                  "201": { "description": "Created" },
                  "400": { "description": "Bad Request" }
                }
              }
            },
            "/users/{id}": {
              "delete": {
                "summary": "Delete user",
                "deprecated": true,
                "parameters": [
                  { "name": "id", "in": "path", "required": true, "schema": { "type": "string" } }
                ],
                "responses": { "204": { "description": "No Content" } }
              }
            }
          }
        }
        """;

    private const string Swagger2Json = """
        {
          "swagger": "2.0",
          "info": { "title": "Legacy API", "version": "1.5", "description": "A legacy service" },
          "paths": {
            "/items/{id}": {
              "get": {
                "summary": "Get item",
                "tags": ["Items"],
                "parameters": [
                  { "name": "id", "in": "path", "required": true, "type": "integer", "format": "int64" }
                ],
                "responses": {
                  "200": { "description": "OK", "schema": { "type": "object" } },
                  "404": { "description": "Not found" }
                }
              }
            }
          }
        }
        """;

    private const string OpenApi3Yaml = """
        openapi: 3.0.0
        info:
          title: YAML API
          version: "1.0"
        paths:
          /health:
            get:
              summary: Health check
              operationId: getHealth
              responses:
                '200':
                  description: OK
          /ping:
            post:
              summary: Ping
              responses:
                '204':
                  description: No Content
        """;

    private const string UnrecognizedJson = """{ "not_openapi": true }""";

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static SpecAggregator CreateAggregator(ISpecFetcher fetcher) =>
        new(fetcher, NullLogger<SpecAggregator>.Instance);

    private static SwaggerAggregatorOptions OneEndpoint(string name, string url) =>
        new SwaggerAggregatorOptions().AddEndpoint(name, url);

    // ── OpenAPI 3.x JSON tests ───────────────────────────────────────────────

    [Fact]
    public async Task AggregateAsync_OpenApi3Json_ParsesEndpoints()
    {
        var fetcher = new FakeSpecFetcher(OpenApi3Json, "application/json");
        var agg = CreateAggregator(fetcher);

        var result = await agg.AggregateAsync(OneEndpoint("Test", "https://example.com/swagger.json"));

        var svc = Assert.Single(result.Services);
        Assert.False(svc.FetchError);
        Assert.Equal("2.0", svc.Version);
        Assert.Equal("A test service", svc.Description);
        Assert.Equal(3, svc.Endpoints.Count);
    }

    [Fact]
    public async Task AggregateAsync_OpenApi3Json_ParsesGetEndpointCorrectly()
    {
        var fetcher = new FakeSpecFetcher(OpenApi3Json, "application/json");
        var agg = CreateAggregator(fetcher);

        var result = await agg.AggregateAsync(OneEndpoint("Test", "https://example.com/swagger.json"));

        var svc = result.Services[0];
        var get = svc.Endpoints.First(e => e.Method == "GET" && e.Path == "/users");
        Assert.Equal("List users", get.Summary);
        Assert.Equal("listUsers", get.OperationId);
        Assert.Contains("Users", get.Tags);
        Assert.Single(get.Parameters);
        Assert.Equal("page", get.Parameters[0].Name);
        Assert.Equal("query", get.Parameters[0].In);
        Assert.False(get.Parameters[0].Required);
    }

    [Fact]
    public async Task AggregateAsync_OpenApi3Json_ParsesPostWithRequestBody()
    {
        var fetcher = new FakeSpecFetcher(OpenApi3Json, "application/json");
        var agg = CreateAggregator(fetcher);

        var result = await agg.AggregateAsync(OneEndpoint("Test", "https://example.com/swagger.json"));

        var svc = result.Services[0];
        var post = svc.Endpoints.First(e => e.Method == "POST");
        Assert.NotNull(post.RequestBody);
        Assert.True(post.RequestBody!.Required);
        Assert.Contains("application/json", post.RequestBody.Content.Keys);
        Assert.Equal(2, post.Responses.Count);
    }

    [Fact]
    public async Task AggregateAsync_OpenApi3Json_ParsesDeprecatedFlag()
    {
        var fetcher = new FakeSpecFetcher(OpenApi3Json, "application/json");
        var agg = CreateAggregator(fetcher);

        var result = await agg.AggregateAsync(OneEndpoint("Test", "https://example.com/swagger.json"));

        var svc = result.Services[0];
        var delete = svc.Endpoints.First(e => e.Method == "DELETE");
        Assert.True(delete.Deprecated);
    }

    [Fact]
    public async Task AggregateAsync_OpenApi3Json_ParsesTags()
    {
        var fetcher = new FakeSpecFetcher(OpenApi3Json, "application/json");
        var agg = CreateAggregator(fetcher);

        var result = await agg.AggregateAsync(OneEndpoint("Test", "https://example.com/swagger.json"));

        var svc = result.Services[0];
        Assert.Single(svc.Tags);
        Assert.Equal("Users", svc.Tags[0].Name);
    }

    // ── Swagger 2.0 JSON tests ───────────────────────────────────────────────

    [Fact]
    public async Task AggregateAsync_Swagger2Json_ParsesEndpoints()
    {
        var fetcher = new FakeSpecFetcher(Swagger2Json, "application/json");
        var agg = CreateAggregator(fetcher);

        var result = await agg.AggregateAsync(OneEndpoint("Legacy", "https://example.com/swagger.json"));

        var svc = Assert.Single(result.Services);
        Assert.False(svc.FetchError);
        Assert.Equal("1.5", svc.Version);
        Assert.Single(svc.Endpoints);
    }

    [Fact]
    public async Task AggregateAsync_Swagger2Json_ParsesPathParameterCorrectly()
    {
        var fetcher = new FakeSpecFetcher(Swagger2Json, "application/json");
        var agg = CreateAggregator(fetcher);

        var result = await agg.AggregateAsync(OneEndpoint("Legacy", "https://example.com/swagger.json"));

        var ep = result.Services[0].Endpoints[0];
        Assert.Equal("GET", ep.Method);
        Assert.Equal("/items/{id}", ep.Path);
        var param = Assert.Single(ep.Parameters);
        Assert.Equal("id", param.Name);
        Assert.Equal("path", param.In);
        Assert.True(param.Required);
        Assert.Equal("integer", param.Type);
        Assert.Equal(2, ep.Responses.Count);
    }

    // ── YAML detection tests ─────────────────────────────────────────────────

    [Fact]
    public async Task AggregateAsync_YmlUrlExtension_ParsesYamlContent()
    {
        var fetcher = new FakeSpecFetcher(OpenApi3Yaml, "text/plain"); // plain content-type to force URL detection
        var agg = CreateAggregator(fetcher);

        var result = await agg.AggregateAsync(OneEndpoint("YAML", "https://example.com/openapi.yml"));

        var svc = Assert.Single(result.Services);
        Assert.False(svc.FetchError, svc.FetchErrorMessage);
        Assert.Equal(2, svc.Endpoints.Count);
    }

    [Fact]
    public async Task AggregateAsync_YamlUrlExtension_ParsesYamlContent()
    {
        var fetcher = new FakeSpecFetcher(OpenApi3Yaml, "text/plain");
        var agg = CreateAggregator(fetcher);

        var result = await agg.AggregateAsync(OneEndpoint("YAML", "https://example.com/openapi.yaml"));

        var svc = Assert.Single(result.Services);
        Assert.False(svc.FetchError, svc.FetchErrorMessage);
    }

    [Fact]
    public async Task AggregateAsync_YamlContentType_ParsesYamlContent()
    {
        var fetcher = new FakeSpecFetcher(OpenApi3Yaml, "application/yaml");
        var agg = CreateAggregator(fetcher);

        var result = await agg.AggregateAsync(OneEndpoint("YAML", "https://example.com/spec"));

        var svc = Assert.Single(result.Services);
        Assert.False(svc.FetchError, svc.FetchErrorMessage);
        Assert.Equal(2, svc.Endpoints.Count);
    }

    [Fact]
    public async Task AggregateAsync_YamlContentSniff_ParsesYamlContent()
    {
        // URL and content-type both look like JSON/unknown — but content doesn't start with {
        var fetcher = new FakeSpecFetcher(OpenApi3Yaml, "application/json");
        var agg = CreateAggregator(fetcher);

        var result = await agg.AggregateAsync(OneEndpoint("YAML", "https://example.com/swagger.json"));

        var svc = Assert.Single(result.Services);
        Assert.False(svc.FetchError, svc.FetchErrorMessage);
    }

    [Fact]
    public async Task AggregateAsync_YamlEndpoint_ParsesPathsCorrectly()
    {
        var fetcher = new FakeSpecFetcher(OpenApi3Yaml, "text/plain");
        var agg = CreateAggregator(fetcher);

        var result = await agg.AggregateAsync(OneEndpoint("YAML", "https://example.com/api.yml"));

        var svc = result.Services[0];
        var health = svc.Endpoints.First(e => e.Path == "/health");
        Assert.Equal("GET", health.Method);
        Assert.Equal("Health check", health.Summary);
        Assert.Equal("getHealth", health.OperationId);
    }

    // ── Path prefix tests ────────────────────────────────────────────────────

    [Fact]
    public async Task AggregateAsync_WithPathPrefix_PrependsPrefixToAllPaths()
    {
        var fetcher = new FakeSpecFetcher(OpenApi3Json, "application/json");
        var agg = CreateAggregator(fetcher);
        var options = new SwaggerAggregatorOptions();
        options.AddEndpoint("Test", "https://example.com/swagger.json", pathPrefix: "/v1");

        var result = await agg.AggregateAsync(options);

        var svc = result.Services[0];
        Assert.All(svc.Endpoints, ep => Assert.StartsWith("/v1", ep.Path));
    }

    // ── Error handling tests ─────────────────────────────────────────────────

    [Fact]
    public async Task AggregateAsync_FetchFailure_SetsFetchError()
    {
        var fetcher = new FakeSpecFetcher(error: "Connection refused");
        var agg = CreateAggregator(fetcher);

        var result = await agg.AggregateAsync(OneEndpoint("Down", "https://down.example.com/swagger.json"));

        var svc = Assert.Single(result.Services);
        Assert.True(svc.FetchError);
        Assert.Equal("Connection refused", svc.FetchErrorMessage);
        Assert.Empty(svc.Endpoints);
    }

    [Fact]
    public async Task AggregateAsync_UnrecognizedFormat_SetsFetchError()
    {
        var fetcher = new FakeSpecFetcher(UnrecognizedJson, "application/json");
        var agg = CreateAggregator(fetcher);

        var result = await agg.AggregateAsync(OneEndpoint("Bad", "https://example.com/swagger.json"));

        var svc = Assert.Single(result.Services);
        Assert.True(svc.FetchError);
        Assert.NotNull(svc.FetchErrorMessage);
    }

    [Fact]
    public async Task AggregateAsync_MultipleEndpoints_AggregatesAll()
    {
        var fetcher = new FakeSpecFetcher(OpenApi3Json, "application/json");
        var agg = CreateAggregator(fetcher);
        var options = new SwaggerAggregatorOptions();
        options.AddEndpoint("Service A", "https://a.example.com/swagger.json");
        options.AddEndpoint("Service B", "https://b.example.com/swagger.json");
        options.AddEndpoint("Service C", "https://c.example.com/swagger.json");

        var result = await agg.AggregateAsync(options);

        Assert.Equal(3, result.Services.Count);
        Assert.All(result.Services, svc => Assert.False(svc.FetchError));
    }

    [Fact]
    public async Task AggregateAsync_MixedSuccessAndFailure_ReturnsPartialResults()
    {
        var fetcher = new MixedSpecFetcher(
            successUrl: "https://ok.example.com/swagger.json",
            successContent: OpenApi3Json,
            errorUrl: "https://down.example.com/swagger.json",
            errorMessage: "Timeout");
        var agg = CreateAggregator(fetcher);
        var options = new SwaggerAggregatorOptions();
        options.AddEndpoint("OK", "https://ok.example.com/swagger.json");
        options.AddEndpoint("Down", "https://down.example.com/swagger.json");

        var result = await agg.AggregateAsync(options);

        Assert.Equal(2, result.Services.Count);
        Assert.False(result.Services.First(s => s.Name == "OK").FetchError);
        Assert.True(result.Services.First(s => s.Name == "Down").FetchError);
    }

    // ── BaseUrl parsing tests ─────────────────────────────────────────────────

    private const string OpenApi3WithServers = """
        {
          "openapi": "3.0.0",
          "info": { "title": "Served API", "version": "1.0" },
          "servers": [{ "url": "https://api.myservice.com/v2" }],
          "paths": {}
        }
        """;

    private const string Swagger2WithHost = """
        {
          "swagger": "2.0",
          "info": { "title": "Hosted API", "version": "1.0" },
          "host": "api.legacy.com",
          "basePath": "/v1",
          "schemes": ["https"],
          "paths": {}
        }
        """;

    [Fact]
    public async Task AggregateAsync_OpenApi3WithServers_SetsBaseUrl()
    {
        var fetcher = new FakeSpecFetcher(OpenApi3WithServers, "application/json");
        var agg = CreateAggregator(fetcher);

        var result = await agg.AggregateAsync(OneEndpoint("Test", "https://example.com/swagger.json"));

        Assert.Equal("https://api.myservice.com/v2", result.Services[0].BaseUrl);
    }

    [Fact]
    public async Task AggregateAsync_OpenApi3NoServers_FallsBackToSpecUrlOrigin()
    {
        var fetcher = new FakeSpecFetcher(OpenApi3Json, "application/json");
        var agg = CreateAggregator(fetcher);

        var result = await agg.AggregateAsync(OneEndpoint("Test", "https://orders.internal/swagger/v1/swagger.json"));

        Assert.Equal("https://orders.internal", result.Services[0].BaseUrl);
    }

    [Fact]
    public async Task AggregateAsync_Swagger2WithHost_SetsBaseUrl()
    {
        var fetcher = new FakeSpecFetcher(Swagger2WithHost, "application/json");
        var agg = CreateAggregator(fetcher);

        var result = await agg.AggregateAsync(OneEndpoint("Legacy", "https://example.com/swagger.json"));

        Assert.Equal("https://api.legacy.com/v1", result.Services[0].BaseUrl);
    }

    [Fact]
    public async Task AggregateAsync_PathPrefix_StoredOnServiceGroup()
    {
        var fetcher = new FakeSpecFetcher(OpenApi3Json, "application/json");
        var agg = CreateAggregator(fetcher);
        var options = new SwaggerAggregatorOptions();
        options.AddEndpoint("Test", "https://example.com/swagger.json", pathPrefix: "/gateway/v1");

        var result = await agg.AggregateAsync(options);

        Assert.Equal("/gateway/v1", result.Services[0].PathPrefix);
    }

    // ── AggregatedSpec metadata ───────────────────────────────────────────────

    [Fact]
    public async Task AggregateAsync_SetsTitle()
    {
        var fetcher = new FakeSpecFetcher(OpenApi3Json, "application/json");
        var agg = CreateAggregator(fetcher);
        var options = new SwaggerAggregatorOptions { Title = "My Platform Docs" };
        options.AddEndpoint("Test", "https://example.com/swagger.json");

        var result = await agg.AggregateAsync(options);

        Assert.Equal("My Platform Docs", result.Title);
    }

    [Fact]
    public async Task AggregateAsync_SetsFetchedAt()
    {
        var before = DateTime.UtcNow;
        var fetcher = new FakeSpecFetcher(OpenApi3Json, "application/json");
        var agg = CreateAggregator(fetcher);

        var result = await agg.AggregateAsync(OneEndpoint("Test", "https://example.com/swagger.json"));

        Assert.True(result.FetchedAt >= before);
        Assert.True(result.FetchedAt <= DateTime.UtcNow);
    }
}

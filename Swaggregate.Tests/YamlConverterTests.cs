using System.Text.Json;
using Swaggregate.Services;
using Xunit;

namespace Swaggregate.Tests;

public class YamlConverterTests
{
    [Fact]
    public void ToJson_SimpleKeyValueYaml_ReturnsValidJson()
    {
        var yaml = "name: test\nvalue: 42";

        var json = YamlConverter.ToJson(yaml);

        using var doc = JsonDocument.Parse(json);
        Assert.Equal("test", doc.RootElement.GetProperty("name").GetString());
        // YamlDotNet deserializes all scalars as strings when using Deserialize<object>
        Assert.Equal("42", doc.RootElement.GetProperty("value").GetString());
    }

    [Fact]
    public void ToJson_NestedYaml_PreservesStructure()
    {
        var yaml = """
            info:
              title: My API
              version: "1.0"
            """;

        var json = YamlConverter.ToJson(yaml);

        using var doc = JsonDocument.Parse(json);
        var info = doc.RootElement.GetProperty("info");
        Assert.Equal("My API", info.GetProperty("title").GetString());
        Assert.Equal("1.0", info.GetProperty("version").GetString());
    }

    [Fact]
    public void ToJson_OpenApiStyleYaml_ProducesParseableJson()
    {
        var yaml = """
            openapi: 3.0.0
            info:
              title: Test API
              version: "1.0"
            paths:
              /users:
                get:
                  summary: List users
                  responses:
                    '200':
                      description: OK
            """;

        var json = YamlConverter.ToJson(yaml);

        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("openapi", out _));
        Assert.True(doc.RootElement.TryGetProperty("paths", out _));
    }

    [Fact]
    public void ToJson_YamlWithList_ProducesJsonArray()
    {
        var yaml = """
            tags:
              - name: Users
              - name: Orders
            """;

        var json = YamlConverter.ToJson(yaml);

        using var doc = JsonDocument.Parse(json);
        var tags = doc.RootElement.GetProperty("tags");
        Assert.Equal(JsonValueKind.Array, tags.ValueKind);
        Assert.Equal(2, tags.GetArrayLength());
    }
}

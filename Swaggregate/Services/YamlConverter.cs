using System.Text.Json;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Swaggregate.Services;

/// <summary>Converts a YAML OpenAPI/Swagger spec string into its JSON equivalent.</summary>
internal static class YamlConverter
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(NullNamingConvention.Instance)
        .Build();

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = false
    };

    /// <summary>Deserializes <paramref name="yaml"/> and re-serializes it as JSON.</summary>
    public static string ToJson(string yaml)
    {
        var obj = Deserializer.Deserialize<object>(yaml);
        return JsonSerializer.Serialize(obj, JsonOpts);
    }
}

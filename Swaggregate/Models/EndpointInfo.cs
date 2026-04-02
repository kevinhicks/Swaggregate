namespace Swaggregate.Models;

public class EndpointInfo
{
    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? Description { get; set; }
    public List<string> Tags { get; set; } = new();
    public string? OperationId { get; set; }
    public bool Deprecated { get; set; }
    public List<ParameterInfo> Parameters { get; set; } = new();
    public RequestBodyInfo? RequestBody { get; set; }
    public List<ResponseInfo> Responses { get; set; } = new();
}

public class ParameterInfo
{
    public string Name { get; set; } = string.Empty;
    public string In { get; set; } = string.Empty; // path, query, header, cookie
    public string? Description { get; set; }
    public bool Required { get; set; }
    public string? Type { get; set; }
    public string? Format { get; set; }
    public object? Schema { get; set; }
}

public class RequestBodyInfo
{
    public string? Description { get; set; }
    public bool Required { get; set; }
    public Dictionary<string, SchemaInfo> Content { get; set; } = new();
}

public class ResponseInfo
{
    public string StatusCode { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Dictionary<string, SchemaInfo> Content { get; set; } = new();
}

public class SchemaInfo
{
    public string? Type { get; set; }
    public string? Ref { get; set; }
    public object? Example { get; set; }
    public object? RawSchema { get; set; }
}

public class TagInfo
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

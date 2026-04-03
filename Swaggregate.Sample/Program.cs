using Swaggregate;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSwaggerAggregator();

var app = builder.Build();

// Real public specs — run the app and browse to /api-docs to see Swaggregate in action.
// Replace with your own service URLs when integrating into your platform.
app.UseSwaggerAggregator(opt =>
{
    opt.Title = "Swaggregate Sample";
    opt.RoutePrefix = "api-docs";
    opt.CacheTtlMinutes = 5;

    // YAML spec (OpenAPI 3.x)
    opt.AddEndpoint("Open Weather Map",
        "https://idratherbewriting.com/docs/openapi_spec_and_generated_ref_docs/openapi_openweathermapv3.yml");

    // JSON specs
    opt.AddEndpoint("FakeRESTApi",
        "https://fakerestapi.azurewebsites.net/swagger/v1/swagger.json");
    opt.AddEndpoint("Pet Store (Swagger 2)",
        "https://petstore.swagger.io/v2/swagger.json");
});

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();

using Swaggregate;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSwaggerAggregator();

var app = builder.Build();

// Configure Swaggregate with the swagger.json URLs for each of your services.
// Replace the placeholder URLs below with the actual endpoints in your environment.
app.UseSwaggerAggregator(opt =>
{
    opt.Title = "My API Documentation";
    opt.RoutePrefix = "api-docs";
    opt.CacheTtlMinutes = 5;

    opt.AddEndpoint("Open Weather",       "https://idratherbewriting.com/docs/openapi_spec_and_generated_ref_docs/openapi_openweathermapv3.yml");
    opt.AddEndpoint("FakeRESTApi",        "https://fakerestapi.azurewebsites.net/swagger/v1/swagger.json");
    opt.AddEndpoint("Pet Store",          "https://petstore.swagger.io/v2/swagger.json"); 
});

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();

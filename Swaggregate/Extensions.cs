using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Swaggregate.Middleware;
using Swaggregate.Services;

namespace Swaggregate;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSwaggerAggregator(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddHttpClient("Swaggregate");
        services.TryAddSingleton<ISpecFetcher, SpecFetcher>();
        services.TryAddSingleton<SpecAggregator>();
        return services;
    }
}

public static class ApplicationBuilderExtensions
{
    public static IApplicationBuilder UseSwaggerAggregator(
        this IApplicationBuilder app,
        Action<SwaggerAggregatorOptions>? configure = null)
    {
        var options = new SwaggerAggregatorOptions();
        configure?.Invoke(options);

        app.UseMiddleware<SwaggerAggregatorMiddleware>(options);
        return app;
    }
}

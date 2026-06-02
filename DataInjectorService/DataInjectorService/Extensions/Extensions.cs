using Microsoft.OpenApi;

namespace DataInjectorService.Extensions;

public static class Extensions
{
    public static void AddSwaggerGenConfiguration(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc(
                "v1",
                new OpenApiInfo { Title = "DataInjectorService", Version = "v1" }
            );
        });
    }
}

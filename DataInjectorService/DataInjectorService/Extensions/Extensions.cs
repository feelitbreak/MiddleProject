namespace DataInjectorService.Extensions;

using Microsoft.OpenApi;

public static class Extensions
{
    public static void AddSwaggerGenConfiguration(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc(
                "v1",
                new OpenApiInfo { Title = "DataInjectorService", Version = "v1" });
        });
    }

    public static void AddCorsConfiguration(this IServiceCollection services, IConfigurationManager configuration)
    {
        services.AddCors(options =>
        {
            options.AddPolicy("AllowOrigins", policy =>
            {
                var allowLocalhost = configuration.GetValue("Cors:AllowLocalhost", true);
                var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
                var normalizedAllowedOrigins = allowedOrigins.Select(origin => origin.Trim().TrimEnd('/'));

                policy
                    .SetIsOriginAllowed(origin =>
                    {
                        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                        {
                            return false;
                        }

                        if (allowLocalhost && uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }

                        return normalizedAllowedOrigins.Contains(origin.Trim().TrimEnd('/'));
                    })
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });
    }
}

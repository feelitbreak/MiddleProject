namespace DataProcessorService.Application.Dispatch;

using DataProcessorService.Application.Abstractions.Messaging;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Entry point for wiring up the CQRS dispatcher.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Dependency injection wiring, exercised indirectly by every integration test.")]
public static class CqrsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the dispatcher, then the handlers and behaviours declared by
    /// <paramref name="configure"/>.
    /// </summary>
    public static IServiceCollection AddCqrs(
        this IServiceCollection services,
        Action<CqrsBuilder> configure
    )
    {
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new CqrsBuilder(services);
        configure(builder);

        services.AddSingleton(new RequestExecutorRegistry(builder.Executors));
        services.AddScoped<ISender, Sender>();

        return services;
    }
}

namespace DataProcessorService.UnitTests.Dispatch;

using DataProcessorService.Api.Extensions;
using DataProcessorService.Application.Abstractions.Messaging;
using DataProcessorService.Application.Dispatch;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

/// <summary>
/// The safety net that pays for explicit handler registration.
/// <para>
/// Production registers handlers by hand, because assembly scanning needs
/// <c>MakeGenericType</c> and that raises trimming warnings which fail the build under
/// <c>TreatWarningsAsErrors</c>. The guarantee scanning would have given --- that no handler is
/// ever forgotten --- is recovered here instead: this test does the scan, so a handler added
/// without a matching registration fails the build rather than throwing on first dispatch in
/// production.
/// </para>
/// </summary>
public sealed class HandlerRegistrationTests
{
    [Fact]
    public void EveryHandlerInTheApplicationAssembly_IsRegistered()
    {
        var registered = BuildRegistry().RegisteredRequestTypes;
        var discovered = DiscoverHandledRequestTypes();

        Assert.NotEmpty(discovered);

        var missing = discovered.Except(registered).ToList();

        Assert.True(
            missing.Count == 0,
            "These request types have a handler but are not registered in "
                + $"{nameof(Extensions.AddCqrsHandlers)}: {string.Join(", ", missing.Select(type => type.Name))}"
        );
    }

    [Fact]
    public void EveryRegisteredRequestType_ResolvesItsHandler()
    {
        var services = new ServiceCollection();
        services.AddCqrsHandlers();

        var handlerRegistrations = services
            .Where(descriptor =>
                descriptor.ServiceType.IsGenericType
                && descriptor.ServiceType.GetGenericTypeDefinition()
                    == typeof(IRequestHandler<,>)
            )
            .Select(descriptor => descriptor.ServiceType.GetGenericArguments()[0])
            .ToHashSet();

        foreach (var requestType in BuildRegistry().RegisteredRequestTypes)
        {
            Assert.Contains(requestType, handlerRegistrations);
        }
    }

    private static RequestExecutorRegistry BuildRegistry()
    {
        var services = new ServiceCollection();
        services.AddCqrsHandlers();

        return services
            .Single(descriptor => descriptor.ServiceType == typeof(RequestExecutorRegistry))
            .ImplementationInstance as RequestExecutorRegistry
            ?? throw new InvalidOperationException(
                "The executor registry is expected to be registered as a singleton instance."
            );
    }

    private static IReadOnlyList<Type> DiscoverHandledRequestTypes()
    {
        var applicationAssembly = typeof(CqrsBuilder).Assembly;

        return
        [
            .. applicationAssembly
                .GetTypes()
                .Where(type => type is { IsAbstract: false, IsInterface: false })
                .SelectMany(type => type.GetInterfaces())
                .Where(@interface =>
                    @interface.IsGenericType
                    && @interface.GetGenericTypeDefinition() == typeof(IRequestHandler<,>)
                )
                .Select(@interface => @interface.GetGenericArguments()[0])
                .Distinct(),
        ];
    }
}

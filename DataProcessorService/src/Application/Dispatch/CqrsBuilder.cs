namespace DataProcessorService.Application.Dispatch;

using DataProcessorService.Application.Abstractions.Messaging;
using DataProcessorService.Domain.Common;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers CQRS handlers and pipeline behaviours.
/// <para>
/// Registration is explicit rather than discovered by assembly scanning. Scanning needs
/// <c>MakeGenericType</c>, which produces IL2055/IL3050 trimming warnings that fail the build
/// under <c>TreatWarningsAsErrors</c>, and it only reports a forgotten interface at runtime. A
/// unit test performs the scan instead and asserts every handler in the assembly is registered
/// here, so the guarantee is kept without the runtime cost.
/// </para>
/// </summary>
/// <param name="services">The service collection being configured.</param>
public sealed class CqrsBuilder(IServiceCollection services)
{
    private readonly Dictionary<Type, object> executors = [];

    /// <summary>Gets the executors registered so far, keyed by request type.</summary>
    internal IReadOnlyDictionary<Type, object> Executors => this.executors;

    /// <summary>Registers a handler for a command that returns no value.</summary>
    /// <typeparam name="TCommand">The command type.</typeparam>
    /// <typeparam name="THandler">The handler implementation.</typeparam>
    /// <returns>This builder, for chaining.</returns>
    public CqrsBuilder AddCommandHandler<TCommand, THandler>()
        where TCommand : ICommand
        where THandler : class, ICommandHandler<TCommand>
    {
        services.AddScoped<IRequestHandler<TCommand, Result>, THandler>();
        this.executors[typeof(TCommand)] = new RequestExecutor<TCommand, Result>();
        return this;
    }

    /// <summary>Registers a handler for a command that returns a value.</summary>
    /// <typeparam name="TCommand">The command type.</typeparam>
    /// <typeparam name="TValue">The value returned on success.</typeparam>
    /// <typeparam name="THandler">The handler implementation.</typeparam>
    /// <returns>This builder, for chaining.</returns>
    public CqrsBuilder AddCommandHandler<TCommand, TValue, THandler>()
        where TCommand : ICommand<TValue>
        where THandler : class, ICommandHandler<TCommand, TValue>
    {
        services.AddScoped<IRequestHandler<TCommand, Result<TValue>>, THandler>();
        this.executors[typeof(TCommand)] = new RequestExecutor<TCommand, Result<TValue>>();
        return this;
    }

    /// <summary>Registers a handler for a query.</summary>
    /// <typeparam name="TQuery">The query type.</typeparam>
    /// <typeparam name="TValue">The value returned on success.</typeparam>
    /// <typeparam name="THandler">The handler implementation.</typeparam>
    /// <returns>This builder, for chaining.</returns>
    public CqrsBuilder AddQueryHandler<TQuery, TValue, THandler>()
        where TQuery : IQuery<TValue>
        where THandler : class, IQueryHandler<TQuery, TValue>
    {
        services.AddScoped<IRequestHandler<TQuery, Result<TValue>>, THandler>();
        this.executors[typeof(TQuery)] = new RequestExecutor<TQuery, Result<TValue>>();
        return this;
    }

    /// <summary>
    /// Registers an open-generic pipeline behaviour, for example
    /// <c>typeof(LoggingBehavior&lt;,&gt;)</c>. Behaviours run in registration order, outermost
    /// first. A behaviour's own generic constraints decide which requests it applies to, because
    /// the container skips closed types that violate them.
    /// </summary>
    /// <param name="openGenericBehaviorType">The open generic behaviour type.</param>
    /// <returns>This builder, for chaining.</returns>
    public CqrsBuilder AddBehavior(Type openGenericBehaviorType)
    {
        services.AddScoped(typeof(IPipelineBehavior<,>), openGenericBehaviorType);
        return this;
    }
}

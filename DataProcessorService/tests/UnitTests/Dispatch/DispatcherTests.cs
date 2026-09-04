namespace DataProcessorService.UnitTests.Dispatch;

using DataProcessorService.Application.Abstractions.Messaging;
using DataProcessorService.Application.Dispatch;
using DataProcessorService.Domain.Common;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Covers the hand-rolled CQRS dispatcher used in place of MediatR.
/// <para>
/// The behaviour worth pinning is the constraint filtering: behaviours are registered as open
/// generics, and the claim that a command-only behaviour never runs for a query rests entirely on
/// the container skipping closed types that violate a registration's generic constraints. That is
/// subtle container behaviour, not something the compiler enforces, so it is asserted here.
/// </para>
/// </summary>
public sealed class DispatcherTests
{
    [Fact]
    public async Task SendAsync_RegisteredCommand_ReachesItsHandler()
    {
        var provider = BuildProvider();
        var sender = provider.GetRequiredService<ISender>();

        var result = await sender.SendAsync(
            new SampleCommand("payload"),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.IsSuccess);
        Assert.Equal("handled:payload", result.Value);
    }

    [Fact]
    public async Task SendAsync_RegisteredQuery_ReachesItsHandler()
    {
        var provider = BuildProvider();
        var sender = provider.GetRequiredService<ISender>();

        var result = await sender.SendAsync(
            new SampleQuery(7),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.IsSuccess);
        Assert.Equal(14, result.Value);
    }

    [Fact]
    public async Task SendAsync_UnregisteredRequest_ThrowsNamingTheRequest()
    {
        var provider = BuildProvider();
        var sender = provider.GetRequiredService<ISender>();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sender.SendAsync(new UnregisteredQuery(), TestContext.Current.CancellationToken)
        );

        Assert.Contains(nameof(UnregisteredQuery), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAsync_Command_RunsBehavioursOutermostFirst()
    {
        var log = new List<string>();
        var provider = BuildProvider(log);
        var sender = provider.GetRequiredService<ISender>();

        await sender.SendAsync(new SampleCommand("x"), TestContext.Current.CancellationToken);

        // Registration order is Outer, Inner, CommandOnly, so each wraps the next in that order.
        Assert.Equal(
            [
                "outer:before",
                "inner:before",
                "command-only",
                "handler",
                "inner:after",
                "outer:after",
            ],
            log
        );
    }

    [Fact]
    public async Task SendAsync_Query_SkipsCommandOnlyBehaviours()
    {
        var log = new List<string>();
        var provider = BuildProvider(log);
        var sender = provider.GetRequiredService<ISender>();

        await sender.SendAsync(new SampleQuery(1), TestContext.Current.CancellationToken);

        // CommandOnlyBehavior is constrained to IBaseCommand, so it must not have been constructed.
        Assert.DoesNotContain("command-only", log);
        Assert.Contains("outer:before", log);
    }

    [Fact]
    public async Task SendAsync_Command_RunsCommandOnlyBehaviours()
    {
        var log = new List<string>();
        var provider = BuildProvider(log);
        var sender = provider.GetRequiredService<ISender>();

        await sender.SendAsync(new SampleCommand("x"), TestContext.Current.CancellationToken);

        Assert.Contains("command-only", log);
    }

    [Fact]
    public void Registry_ExposesEveryRegisteredRequestType()
    {
        var provider = BuildProvider();
        var registry = provider.GetRequiredService<RequestExecutorRegistry>();

        Assert.Contains(typeof(SampleCommand), registry.RegisteredRequestTypes);
        Assert.Contains(typeof(SampleQuery), registry.RegisteredRequestTypes);
        Assert.DoesNotContain(typeof(UnregisteredQuery), registry.RegisteredRequestTypes);
    }

    [Fact]
    public async Task SendAsync_NullRequest_Throws()
    {
        var provider = BuildProvider();
        var sender = provider.GetRequiredService<ISender>();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            sender.SendAsync((ICommand)null!, TestContext.Current.CancellationToken)
        );
    }

    private static ServiceProvider BuildProvider(List<string>? log = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(log ?? []);

        services.AddCqrs(cqrs =>
            cqrs.AddBehavior(typeof(OuterBehavior<,>))
                .AddBehavior(typeof(InnerBehavior<,>))
                .AddBehavior(typeof(CommandOnlyBehavior<,>))
                .AddCommandHandler<SampleCommand, string, SampleCommandHandler>()
                .AddQueryHandler<SampleQuery, int, SampleQueryHandler>()
        );

        return services.BuildServiceProvider();
    }

    private sealed class SampleCommand(string payload) : ICommand<string>
    {
        public string Payload { get; } = payload;
    }

    private sealed class SampleQuery(int value) : IQuery<int>
    {
        public int Value { get; } = value;
    }

    private sealed class UnregisteredQuery : IQuery<int>;

    private sealed class SampleCommandHandler(List<string> log)
        : ICommandHandler<SampleCommand, string>
    {
        public Task<Result<string>> HandleAsync(
            SampleCommand command,
            CancellationToken cancellationToken
        )
        {
            log.Add("handler");
            return Task.FromResult(Result.Success($"handled:{command.Payload}"));
        }
    }

    private sealed class SampleQueryHandler(List<string> log) : IQueryHandler<SampleQuery, int>
    {
        public Task<Result<int>> HandleAsync(SampleQuery query, CancellationToken cancellationToken)
        {
            log.Add("handler");
            return Task.FromResult(Result.Success(query.Value * 2));
        }
    }

    private sealed class OuterBehavior<TRequest, TResult>(List<string> log)
        : IPipelineBehavior<TRequest, TResult>
        where TRequest : IRequest<TResult>
        where TResult : Result
    {
        public async Task<TResult> HandleAsync(
            TRequest request,
            RequestHandlerDelegate<TResult> next,
            CancellationToken cancellationToken
        )
        {
            log.Add("outer:before");
            var result = await next(cancellationToken);
            log.Add("outer:after");
            return result;
        }
    }

    private sealed class InnerBehavior<TRequest, TResult>(List<string> log)
        : IPipelineBehavior<TRequest, TResult>
        where TRequest : IRequest<TResult>
        where TResult : Result
    {
        public async Task<TResult> HandleAsync(
            TRequest request,
            RequestHandlerDelegate<TResult> next,
            CancellationToken cancellationToken
        )
        {
            log.Add("inner:before");
            var result = await next(cancellationToken);
            log.Add("inner:after");
            return result;
        }
    }

    private sealed class CommandOnlyBehavior<TRequest, TResult>(List<string> log)
        : IPipelineBehavior<TRequest, TResult>
        where TRequest : IRequest<TResult>, IBaseCommand
        where TResult : Result
    {
        public Task<TResult> HandleAsync(
            TRequest request,
            RequestHandlerDelegate<TResult> next,
            CancellationToken cancellationToken
        )
        {
            log.Add("command-only");
            return next(cancellationToken);
        }
    }
}

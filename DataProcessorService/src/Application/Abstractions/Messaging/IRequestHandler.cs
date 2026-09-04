namespace DataProcessorService.Application.Abstractions.Messaging;

using DataProcessorService.Domain.Common;

/// <summary>Handles a single request type.</summary>
/// <typeparam name="TRequest">The request type handled.</typeparam>
/// <typeparam name="TResult">The result type produced.</typeparam>
public interface IRequestHandler<in TRequest, TResult>
    where TRequest : IRequest<TResult>
    where TResult : Result
{
    /// <summary>Handles the request.</summary>
    /// <param name="request">The request to handle.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The outcome of handling the request.</returns>
    Task<TResult> HandleAsync(TRequest request, CancellationToken cancellationToken);
}

/// <summary>Handles a command that returns no value.</summary>
/// <typeparam name="TCommand">The command type handled.</typeparam>
public interface ICommandHandler<in TCommand> : IRequestHandler<TCommand, Result>
    where TCommand : ICommand;

/// <summary>Handles a command that returns a value.</summary>
/// <typeparam name="TCommand">The command type handled.</typeparam>
/// <typeparam name="TValue">The value returned on success.</typeparam>
public interface ICommandHandler<in TCommand, TValue> : IRequestHandler<TCommand, Result<TValue>>
    where TCommand : ICommand<TValue>;

/// <summary>Handles a query.</summary>
/// <typeparam name="TQuery">The query type handled.</typeparam>
/// <typeparam name="TValue">The value returned on success.</typeparam>
public interface IQueryHandler<in TQuery, TValue> : IRequestHandler<TQuery, Result<TValue>>
    where TQuery : IQuery<TValue>;

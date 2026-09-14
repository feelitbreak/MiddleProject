namespace DataProcessorService.Application.Behaviors;

using DataProcessorService.Application.Abstractions.Messaging;
using DataProcessorService.Application.Abstractions.Persistence;
using DataProcessorService.Domain.Common;

/// <summary>
/// Wraps command handling in a database transaction, so that a batch is either fully applied or
/// not applied at all.
/// <para>
/// Constrained to <see cref="IBaseCommand"/>, which is what keeps it off the query path: the
/// dependency injection container skips closed generic types that violate a registration's
/// constraints, so this behaviour is never constructed for an <see cref="IQuery{TValue}"/>.
/// </para>
/// <para>
/// A failed <see cref="Result"/> rolls back just as an exception does. Ingestion classifies a
/// transient failure as retryable, and retrying a half-applied batch would be unsound.
/// </para>
/// </summary>
public sealed class UnitOfWorkBehavior<TRequest, TResult>(IUnitOfWork unitOfWork)
    : IPipelineBehavior<TRequest, TResult>
    where TRequest : IRequest<TResult>, IBaseCommand
    where TResult : Result
{
    /// <inheritdoc/>
    public async Task<TResult> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResult> next,
        CancellationToken cancellationToken
    )
    {
        await unitOfWork.BeginAsync(cancellationToken);

        try
        {
            var result = await next(cancellationToken);

            if (result.IsSuccess)
            {
                await unitOfWork.CommitAsync(cancellationToken);
            }
            else
            {
                await unitOfWork.RollbackAsync(cancellationToken);
            }

            return result;
        }
        catch
        {
            await unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}

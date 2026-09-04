namespace DataProcessorService.Infrastructure.Persistence;

using DataProcessorService.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

/// <summary>
/// Unit of work backed by an EF Core database transaction, scoped to one request.
/// </summary>
/// <param name="context">The scoped database context.</param>
public sealed class UnitOfWork(MeterReadingsDbContext context) : IUnitOfWork, IAsyncDisposable
{
    private IDbContextTransaction? transaction;

    /// <inheritdoc/>
    public bool HasActiveTransaction => this.transaction is not null;

    /// <inheritdoc/>
    public async Task BeginAsync(CancellationToken cancellationToken)
    {
        if (this.transaction is not null)
        {
            throw new InvalidOperationException("A transaction is already open on this scope.");
        }

        this.transaction = await context.Database.BeginTransactionAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        if (this.transaction is null)
        {
            throw new InvalidOperationException("There is no open transaction to commit.");
        }

        try
        {
            await this.transaction.CommitAsync(cancellationToken);
        }
        finally
        {
            await this.DisposeTransactionAsync();
        }
    }

    /// <inheritdoc/>
    public async Task RollbackAsync(CancellationToken cancellationToken)
    {
        if (this.transaction is null)
        {
            return;
        }

        try
        {
            await this.transaction.RollbackAsync(cancellationToken);
        }
        finally
        {
            await this.DisposeTransactionAsync();
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await this.DisposeTransactionAsync();

    private async ValueTask DisposeTransactionAsync()
    {
        if (this.transaction is null)
        {
            return;
        }

        await this.transaction.DisposeAsync();
        this.transaction = null;
    }
}

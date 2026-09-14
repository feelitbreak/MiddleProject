namespace DataProcessorService.UnitTests.Behaviors;

using DataProcessorService.Application.Abstractions.Messaging;
using DataProcessorService.Application.Abstractions.Persistence;
using DataProcessorService.Application.Behaviors;
using DataProcessorService.Domain.Common;
using Moq;

/// <summary>
/// Covers transaction boundaries around command handling.
/// <para>
/// The case that matters beyond the obvious one is a handler returning a failed
/// <see cref="Result"/> rather than throwing. Ingestion treats some failures as retryable, and
/// retrying a half-applied batch would be unsound, so a failed result has to roll back exactly as
/// an exception does.
/// </para>
/// </summary>
public sealed class UnitOfWorkBehaviorTests
{
    [Fact]
    public async Task HandleAsync_HandlerSucceeds_Commits()
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        var behavior = new UnitOfWorkBehavior<SampleCommand, Result>(unitOfWork.Object);

        var result = await behavior.HandleAsync(
            new SampleCommand(),
            _ => Task.FromResult(Result.Success()),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.IsSuccess);
        unitOfWork.Verify(u => u.BeginAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_HandlerReturnsFailure_RollsBack()
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        var behavior = new UnitOfWorkBehavior<SampleCommand, Result>(unitOfWork.Object);

        var result = await behavior.HandleAsync(
            new SampleCommand(),
            _ => Task.FromResult(Result.Failure(Error.CreateTransient("database unavailable"))),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.IsFailure);
        unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_HandlerThrows_RollsBackAndRethrows()
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        var behavior = new UnitOfWorkBehavior<SampleCommand, Result>(unitOfWork.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            behavior.HandleAsync(
                new SampleCommand(),
                _ => throw new InvalidOperationException("boom"),
                TestContext.Current.CancellationToken
            )
        );

        unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Cancelled_StillRollsBack()
    {
        // Rollback is issued with CancellationToken.None on the failure path: rolling back is
        // exactly the work that must still happen once the caller's token is already cancelled.
        var unitOfWork = new Mock<IUnitOfWork>();
        var behavior = new UnitOfWorkBehavior<SampleCommand, Result>(unitOfWork.Object);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            behavior.HandleAsync(
                new SampleCommand(),
                token => Task.FromException<Result>(new OperationCanceledException(token)),
                cts.Token
            )
        );

        unitOfWork.Verify(u => u.RollbackAsync(CancellationToken.None), Times.Once);
    }

    private sealed class SampleCommand : ICommand;
}

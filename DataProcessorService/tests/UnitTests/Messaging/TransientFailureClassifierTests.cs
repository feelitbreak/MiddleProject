namespace DataProcessorService.UnitTests.Messaging;

using DataProcessorService.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Net.Sockets;

/// <summary>
/// Covers the transient/permanent split that decides whether a failed batch is retried or
/// dead-lettered.
/// <para>
/// Getting this wrong is asymmetric. Classifying a permanent failure as transient retries forever
/// and wedges the partition; classifying a transient failure as permanent dead-letters data that
/// would have persisted a second later. The classifier therefore recognises transient failures
/// positively and treats everything else as permanent, which fails visibly rather than silently.
/// </para>
/// </summary>
public sealed class TransientFailureClassifierTests
{
    [Theory]
    [InlineData("08000")] // connection exception
    [InlineData("08006")] // connection failure
    [InlineData("53300")] // too many connections
    [InlineData("57P01")] // admin shutdown
    [InlineData("40001")] // serialization failure
    [InlineData("40P01")] // deadlock detected
    public void IsTransient_RetryableSqlState_IsTrue(string sqlState) =>
        Assert.True(TransientFailureClassifier.IsTransient(PostgresError(sqlState)));

    [Theory]
    [InlineData("23505")] // unique violation
    [InlineData("23502")] // not null violation
    [InlineData("42P01")] // undefined table
    [InlineData("22003")] // numeric value out of range
    public void IsTransient_NonRetryableSqlState_IsFalse(string sqlState) =>
        Assert.False(TransientFailureClassifier.IsTransient(PostgresError(sqlState)));

    [Fact]
    public void IsTransient_SocketException_IsTrue() =>
        Assert.True(TransientFailureClassifier.IsTransient(new SocketException(10061)));

    [Fact]
    public void IsTransient_TimeoutException_IsTrue() =>
        Assert.True(TransientFailureClassifier.IsTransient(new TimeoutException()));

    [Fact]
    public void IsTransient_WrappedInDbUpdateException_IsUnwrapped()
    {
        // EF wraps provider exceptions, so the classifier has to walk the inner chain rather than
        // inspect only the outermost type.
        var wrapped = new DbUpdateException("update failed", PostgresError("08006"));

        Assert.True(TransientFailureClassifier.IsTransient(wrapped));
    }

    [Fact]
    public void IsTransient_DeeplyWrapped_IsUnwrapped()
    {
        var wrapped = new InvalidOperationException(
            "outer",
            new DbUpdateException("middle", PostgresError("40001"))
        );

        Assert.True(TransientFailureClassifier.IsTransient(wrapped));
    }

    [Fact]
    public void IsTransient_UnrecognisedException_IsFalse() =>
        Assert.False(TransientFailureClassifier.IsTransient(new InvalidOperationException("boom")));

    [Fact]
    public void IsTransient_Null_IsFalse() =>
        Assert.False(TransientFailureClassifier.IsTransient(null));

    private static PostgresException PostgresError(string sqlState) =>
        new(
            messageText: $"simulated {sqlState}",
            severity: "ERROR",
            invariantSeverity: "ERROR",
            sqlState: sqlState
        );
}

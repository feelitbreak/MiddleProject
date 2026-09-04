namespace DataProcessorService.Infrastructure.Messaging;

using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Net.Sockets;

/// <summary>
/// Decides whether a failure while persisting a batch is worth retrying.
/// <para>
/// The distinction is load-bearing. A transient failure retried is a blip; a permanent failure
/// retried forever wedges the partition with nothing to alert on. Anything not positively
/// recognised as transient is therefore treated as permanent and dead-lettered, which fails
/// visibly rather than silently.
/// </para>
/// </summary>
public static class TransientFailureClassifier
{
    /// <summary>
    /// PostgreSQL SQLSTATE values worth retrying. Class 08 is connection failures, class 53 is
    /// resource exhaustion, class 57P is admin shutdown, and 40001/40P01 are serialization
    /// failures and deadlocks, which succeed on a second attempt by definition.
    /// </summary>
    private static readonly HashSet<string> TransientSqlStates =
    [
        "08000",
        "08003",
        "08006",
        "08001",
        "08004",
        "53000",
        "53100",
        "53200",
        "53300",
        "57P01",
        "57P02",
        "57P03",
        "40001",
        "40P01",
    ];

    /// <summary>Determines whether the given exception is worth retrying.</summary>
    /// <param name="exception">The exception thrown while persisting.</param>
    /// <returns><see langword="true"/> when a retry could plausibly succeed.</returns>
    public static bool IsTransient(Exception? exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            switch (current)
            {
                case PostgresException postgres when TransientSqlStates.Contains(postgres.SqlState):
                case NpgsqlException { IsTransient: true }:
                case SocketException:
                case TimeoutException:
                    return true;
                case DbUpdateConcurrencyException:
                    return false;
            }
        }

        return false;
    }
}

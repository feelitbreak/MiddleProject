namespace DataProcessorService.Application.Abstractions.Messaging;

using DataProcessorService.Domain.Common;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Marker for anything dispatchable through <see cref="ISender"/>. Every request resolves to a
/// <see cref="Result"/> or <see cref="Result{T}"/>, which is what lets the dispatcher work through
/// a single non-generic executor abstraction rather than reflecting over generic arguments.
/// </summary>
/// <typeparam name="TResult">The result type the request produces.</typeparam>
[SuppressMessage(
    "Major Code Smell",
    "S2326:Unused type parameters should be removed",
    Justification = "TResult carries the result type through the type system rather than through a "
        + "member. It is what lets ISender.SendAsync infer what a request returns, and what lets "
        + "the handler and behaviour interfaces constrain to a matching pair. Removing it would "
        + "make dispatch untyped."
)]
public interface IRequest<TResult>
    where TResult : Result;

/// <summary>
/// Marker shared by every command, used as a generic constraint so that write-only pipeline
/// behaviours (such as the unit of work) close only over commands and never over queries.
/// </summary>
public interface IBaseCommand;

/// <summary>A command that mutates state and returns no value.</summary>
public interface ICommand : IRequest<Result>, IBaseCommand;

/// <summary>A command that mutates state and returns a value.</summary>
/// <typeparam name="TValue">The value returned on success.</typeparam>
public interface ICommand<TValue> : IRequest<Result<TValue>>, IBaseCommand;

/// <summary>A read-only query.</summary>
/// <typeparam name="TValue">The value returned on success.</typeparam>
public interface IQuery<TValue> : IRequest<Result<TValue>>;

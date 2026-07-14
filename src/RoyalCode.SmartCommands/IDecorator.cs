namespace RoyalCode.SmartCommands;

/// <summary>
/// <para>
///     A decorator that wraps the execution of a command marked with <see cref="WithDecoratorsAttribute"/>,
///     allowing cross-cutting behavior (logging, caching, transactions, etc.) to run around the command logic.
/// </para>
/// <para>
///     Registered decorators are resolved from Dependency Injection and executed, in registration order,
///     by <see cref="Mediator{TModel, TResult}"/>.
/// </para>
/// </summary>
/// <typeparam name="TModel">The type of the command being handled.</typeparam>
/// <typeparam name="TResult">The type of the result produced by the command.</typeparam>
public interface IDecorator<in TModel, TResult>
{
    /// <summary>
    /// Handles the command, calling <paramref name="next"/> to continue the pipeline.
    /// </summary>
    /// <param name="command">The command instance being handled.</param>
    /// <param name="next">Invokes the next decorator in the pipeline, or the command logic when there are no more decorators.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The result produced by the command.</returns>
    Task<TResult> HandleAsync(TModel command, Func<Task<TResult>> next, CancellationToken ct);
}
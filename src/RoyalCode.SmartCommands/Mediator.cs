namespace RoyalCode.SmartCommands;

/// <summary>
/// <para>
///     Drives the pipeline of <see cref="IDecorator{TModel, TResult}"/> instances applied to a command when the
///     command method is decorated with <see cref="WithDecoratorsAttribute"/>. Instantiated by the source generator
///     in the generated handler implementation; not intended to be used directly.
/// </para>
/// <para>
///     The pipeline is composed once, in reverse order, as a chain of delegates: each decorator receives a
///     <c>next</c> delegate that executes the remainder of the pipeline. There is no shared mutable position,
///     so invoking <c>next</c> more than once deterministically re-executes the rest of the pipeline, and no
///     enumerator or other disposable resource is retained.
/// </para>
/// </summary>
/// <typeparam name="TModel">The type of the command being handled.</typeparam>
/// <typeparam name="TResult">The type of the result produced by the command.</typeparam>
public class Mediator<TModel, TResult>
{
    private readonly Func<Task<TResult>> pipeline;

    /// <summary>
    /// Initializes a new instance of the <see cref="Mediator{TModel, TResult}"/> class.
    /// </summary>
    /// <param name="decorators">The decorators to run, in registration order, before the final handler.</param>
    /// <param name="finalHandler">The delegate that executes the command logic once all decorators have run.</param>
    /// <param name="model">The command instance being handled.</param>
    /// <param name="ct">Cancellation token.</param>
    public Mediator(
        IEnumerable<IDecorator<TModel, TResult>> decorators,
        Func<Task<TResult>> finalHandler,
        TModel model,
        CancellationToken ct)
    {
        var next = finalHandler;

        // composição em ordem reversa: o primeiro decorator registrado é o mais externo,
        // e o 'next' capturado por cada decorator executa o restante do pipeline.
        var list = decorators as IReadOnlyList<IDecorator<TModel, TResult>> ?? decorators.ToList();
        for (var index = list.Count - 1; index >= 0; index--)
        {
            var decorator = list[index];
            var capturedNext = next;
            next = () => decorator.HandleAsync(model, capturedNext, ct);
        }

        pipeline = next;
    }

    /// <summary>
    /// Executes the pipeline: the decorators in registration order and, at the end, the final handler.
    /// Each invocation runs the whole pipeline again.
    /// </summary>
    /// <returns>The result produced by the pipeline.</returns>
    public Task<TResult> NextAsync() => pipeline();
}

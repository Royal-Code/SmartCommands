namespace RoyalCode.SmartCommands;

/// <summary>
/// <para>
///     Drives the pipeline of <see cref="IDecorator{TModel, TResult}"/> instances applied to a command when the
///     command method is decorated with <see cref="WithDecoratorsAttribute"/>. Instantiated by the source generator
///     in the generated handler implementation; not intended to be used directly.
/// </para>
/// </summary>
/// <typeparam name="TModel">The type of the command being handled.</typeparam>
/// <typeparam name="TResult">The type of the result produced by the command.</typeparam>
public class Mediator<TModel, TResult>
{
    private readonly IEnumerator<IDecorator<TModel, TResult>> decorators;
    private readonly Func<Task<TResult>> nextDecorator;
    private readonly Func<Task<TResult>> finalHandler;
    private readonly TModel model;
    private readonly CancellationToken ct;

    /// <summary>
    /// Initializes a new instance of the <see cref="Mediator{TModel, TResult}"/> class.
    /// </summary>
    /// <param name="decorators">The decorators to run, in order, before the final handler.</param>
    /// <param name="finalHandler">The delegate that executes the command logic once all decorators have run.</param>
    /// <param name="model">The command instance being handled.</param>
    /// <param name="ct">Cancellation token.</param>
    public Mediator(
        IEnumerable<IDecorator<TModel, TResult>> decorators,
        Func<Task<TResult>> finalHandler,
        TModel model,
        CancellationToken ct)
    {
        this.decorators = decorators.GetEnumerator();
        this.finalHandler = finalHandler;
        nextDecorator = NextAsync;
        this.model = model;
        this.ct = ct;
    }

    /// <summary>
    /// Invokes the next decorator in the pipeline, or the final handler when there are no more decorators.
    /// </summary>
    /// <returns>The result produced by the next decorator or by the final handler.</returns>
    public Task<TResult> NextAsync()
    {
        return decorators.MoveNext()
            ? decorators.Current.HandleAsync(model, nextDecorator, ct)
            : finalHandler();
    }
}
namespace RoyalCode.SmartCommands;

public class Mediator<TModel, TResult>
{
    private readonly IEnumerator<IDecorator<TModel, TResult>> decorators;
    private readonly Func<Task<TResult>> nextDecorator;
    private readonly Func<Task<TResult>> finalHandler;
    private readonly TModel model;
    private readonly CancellationToken ct;

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

    public Task<TResult> NextAsync()
    {
        return decorators.MoveNext()
            ? decorators.Current.HandleAsync(model, nextDecorator, ct)
            : finalHandler();
    }
}
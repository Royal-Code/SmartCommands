namespace RoyalCode.SmartCommands.Tests.Scenarios.As;

public class DoSomethingWithDecorators
{
    public string? Name { get; set; }

    [Command, WithDecorators]
    public Some Get() => new Some(Name ?? throw new Exception("Bad Name"));
}

public interface IDoSomethingWithDecoratorsHandler
{
    public Task<Some> HandleAsync(DoSomethingWithDecorators model, CancellationToken ct);
}

public class DoSomethingWithDecoratorsHandler : IDoSomethingWithDecoratorsHandler
{
    private readonly IEnumerable<IDecorator<DoSomethingWithDecorators, Some>> decorators;

    public DoSomethingWithDecoratorsHandler(IEnumerable<IDecorator<DoSomethingWithDecorators, Some>> decorators)
    {
        this.decorators = decorators;
    }

    public async Task<Some> HandleAsync(DoSomethingWithDecorators model, CancellationToken ct)
    {
        var decoratorsMediator = new Mediator<DoSomethingWithDecorators, Some>(
            this.decorators,
            () => Task.FromResult(model.Get()),
            model,
            ct);

        return await decoratorsMediator.NextAsync();
    }
}
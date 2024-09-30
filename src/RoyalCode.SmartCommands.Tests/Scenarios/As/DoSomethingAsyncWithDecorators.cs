namespace Coreum.NewCommands.Tests.Scenarios.As;

public class DoSomethingAsyncWithDecorators
{
    public string? Name { get; set; }

    [Command, WithDecorators]
    public Task<Some> GetAsync() => Task.FromResult(new Some(Name ?? throw new Exception("Bad Name")));
}

public interface IDoSomethingAsyncWithDecoratorsHandler
{
    public Task<Some> HandleAsync(DoSomethingAsyncWithDecorators command, CancellationToken ct);
}

public class DoSomethingAsyncWithDecoratorsHandler : IDoSomethingAsyncWithDecoratorsHandler
{
    private readonly IEnumerable<IDecorator<DoSomethingAsyncWithDecorators, Some>> decorators;

    public DoSomethingAsyncWithDecoratorsHandler(IEnumerable<IDecorator<DoSomethingAsyncWithDecorators, Some>> decorators)
    {
        this.decorators = decorators;
    }

    public async Task<Some> HandleAsync(DoSomethingAsyncWithDecorators command, CancellationToken ct)
    {
        var decoratorsMediator = new Mediator<DoSomethingAsyncWithDecorators, Some>(
            this.decorators,
            async () => await command.GetAsync(),
            command,
            ct);

        return await decoratorsMediator.NextAsync();
    }
}
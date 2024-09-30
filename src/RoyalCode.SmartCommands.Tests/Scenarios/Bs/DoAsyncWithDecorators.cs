using RoyalCode.SmartProblems;

namespace Coreum.NewCommands.Tests.Scenarios.Bs;

public class DoAsyncWithDecorators
{
    public string? Name { get; set; }

    [Command, WithDecorators]
    public Task<Result> GetAsync() => Task.FromResult(Result.Ok());
}

public interface IDoAsyncWithDecoratorsHandler
{
    public Task<Result> HandleAsync(DoAsyncWithDecorators command, CancellationToken ct);
}

public class DoAsyncWithDecoratorsHandler : IDoAsyncWithDecoratorsHandler
{
    private readonly IEnumerable<IDecorator<DoAsyncWithDecorators, Result>> decorators;

    public DoAsyncWithDecoratorsHandler(IEnumerable<IDecorator<DoAsyncWithDecorators, Result>> decorators)
    {
        this.decorators = decorators;
    }

    public async Task<Result> HandleAsync(DoAsyncWithDecorators command, CancellationToken ct)
    {
        var decoratorsMediator = new Mediator<DoAsyncWithDecorators, Result>(
            this.decorators,
            async () => await command.GetAsync(),
            command,
            ct);

        return await decoratorsMediator.NextAsync();
    }
}
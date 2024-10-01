using RoyalCode.SmartProblems;

namespace RoyalCode.SmartCommands.Tests.Scenarios.Bs;

public class DoWithDecorators
{
    public string? Name { get; set; }

    [Command, WithDecorators]
    public Result Get() => Result.Ok();
}

public interface IDoWithDecoratorsHandler
{
    public Task<Result> HandleAsync(DoWithDecorators command, CancellationToken ct);
}

public class DoWithDecoratorsHandler : IDoWithDecoratorsHandler
{
    private readonly IEnumerable<IDecorator<DoWithDecorators, Result>> decorators;

    public DoWithDecoratorsHandler(IEnumerable<IDecorator<DoWithDecorators, Result>> decorators)
    {
        this.decorators = decorators;
    }

    public async Task<Result> HandleAsync(DoWithDecorators command, CancellationToken ct)
    {
        var decoratorsMediator = new Mediator<DoWithDecorators, Result>(
            this.decorators,
            () => Task.FromResult(command.Get()),
            command,
            ct);

        return await decoratorsMediator.NextAsync();
    }
}
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;
using System.Diagnostics.CodeAnalysis;

namespace Coreum.NewCommands.Tests.Scenarios.Bs;

public class DoAsyncWithValidateModelWithDecorators
{
    public string? Name { get; set; }

    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<DoAsyncWithValidateModelWithDecorators>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }

    [Command, WithValidateModel, WithDecorators]
    public Task<Result> GetAsync() => Task.FromResult(Result.Ok());
}

public interface IDoAsyncWithValidateModelWithDecoratorsHandler
{
    public Task<Result> HandleAsync(DoAsyncWithValidateModelWithDecorators command, CancellationToken ct);
}

public class DoAsyncWithValidateModelWithDecoratorsHandler : IDoAsyncWithValidateModelWithDecoratorsHandler
{
    private readonly IEnumerable<IDecorator<DoAsyncWithValidateModelWithDecorators, Result>> decorators;

    public DoAsyncWithValidateModelWithDecoratorsHandler(IEnumerable<IDecorator<DoAsyncWithValidateModelWithDecorators, Result>> decorators)
    {
        this.decorators = decorators;
    }

    public async Task<Result> HandleAsync(DoAsyncWithValidateModelWithDecorators command, CancellationToken ct)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        var decoratorsMediator = new Mediator<DoAsyncWithValidateModelWithDecorators, Result>(
            this.decorators,
            async () => await command.GetAsync(),
            command,
            ct);

        return await decoratorsMediator.NextAsync();
    }
}
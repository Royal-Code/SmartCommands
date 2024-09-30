using System.Diagnostics.CodeAnalysis;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;

namespace Coreum.NewCommands.Tests.Scenarios.As;

public class DoSomethingAsyncWithValidateModelWithDecorators
{
    public string? Name { get; set; }

    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<DoSomethingAsyncWithValidateModelWithDecorators>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }

    [Command, WithValidateModel, WithDecorators]
    public Task<Some> GetAsync() => Task.FromResult(new Some(Name!));
}

public interface IDoSomethingAsyncWithValidateModelWithDecoratorsHandler
{
    public Task<Result<Some>> HandleAsync(DoSomethingAsyncWithValidateModelWithDecorators command, CancellationToken ct);
}

public class DoSomethingAsyncWithValidateModelWithDecoratorsHandler : IDoSomethingAsyncWithValidateModelWithDecoratorsHandler
{
    private readonly IEnumerable<IDecorator<DoSomethingAsyncWithValidateModelWithDecorators, Some>> decorators;

    public DoSomethingAsyncWithValidateModelWithDecoratorsHandler(IEnumerable<IDecorator<DoSomethingAsyncWithValidateModelWithDecorators, Some>> decorators)
    {
        this.decorators = decorators;
    }

    public async Task<Result<Some>> HandleAsync(DoSomethingAsyncWithValidateModelWithDecorators command, CancellationToken ct)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        var decoratorsMediator = new Mediator<DoSomethingAsyncWithValidateModelWithDecorators, Some>(
            this.decorators,
            async () => await command.GetAsync(),
            command,
            ct);

        return await decoratorsMediator.NextAsync();
    }
}
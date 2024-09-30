using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;
using System.Diagnostics.CodeAnalysis;

namespace Coreum.NewCommands.Tests.Scenarios.Bs;

public class DoWithValidateModelWithDecorators
{
    public string? Name { get; set; }

    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<DoWithValidateModelWithDecorators>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }

    [Command, WithValidateModel, WithDecorators]
    public Result Get() => Result.Ok();
}

public interface IDoWithValidateModelWithDecoratorsHandler
{
    public Task<Result> HandleAsync(DoWithValidateModelWithDecorators command, CancellationToken ct);
}

public class DoWithValidateModelWithDecoratorsHandler : IDoWithValidateModelWithDecoratorsHandler
{
    private readonly IEnumerable<IDecorator<DoWithValidateModelWithDecorators, Result>> decorators;

    public DoWithValidateModelWithDecoratorsHandler(IEnumerable<IDecorator<DoWithValidateModelWithDecorators, Result>> decorators)
    {
        this.decorators = decorators;
    }

    public async Task<Result> HandleAsync(DoWithValidateModelWithDecorators command, CancellationToken ct)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        var decoratorsMediator = new Mediator<DoWithValidateModelWithDecorators, Result>(
            this.decorators,
            () => Task.FromResult(command.Get()),
            command,
            ct);

        return await decoratorsMediator.NextAsync();
    }
}
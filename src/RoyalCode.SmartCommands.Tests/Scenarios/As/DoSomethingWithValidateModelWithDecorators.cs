using System.Diagnostics.CodeAnalysis;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;

namespace RoyalCode.SmartCommands.Tests.Scenarios.As;

public class DoSomethingWithValidateModelWithDecorators
{
    public string? Name { get; set; }

    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<DoSomethingWithValidateModelWithDecorators>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }

    [Command, WithValidateModel, WithDecorators]
    public Some Get() => new Some(Name!);
}

public interface IDoSomethingWithValidateModelWithDecoratorsHandler
{
    public Task<Result<Some>> Handle(DoSomethingWithValidateModelWithDecorators command, CancellationToken ct);
}

public class DoSomethingWithValidateModelWithDecoratorsHandler : IDoSomethingWithValidateModelWithDecoratorsHandler
{
    private readonly IEnumerable<IDecorator<DoSomethingWithValidateModelWithDecorators, Some>> decorators;

    public DoSomethingWithValidateModelWithDecoratorsHandler(IEnumerable<IDecorator<DoSomethingWithValidateModelWithDecorators, Some>> decorators)
    {
        this.decorators = decorators;
    }

    public async Task<Result<Some>> Handle(DoSomethingWithValidateModelWithDecorators command, CancellationToken ct)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        var decoratorsMediator = new Mediator<DoSomethingWithValidateModelWithDecorators, Some>(
            this.decorators,
            () => Task.FromResult(command.Get()),
            command,
            ct);

        return await decoratorsMediator.NextAsync();
    }
}